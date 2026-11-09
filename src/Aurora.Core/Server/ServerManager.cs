using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Aurora.Core.Server;

public sealed class ServerLaunchOptions
{
    public string Directory { get; init; } = string.Empty;
    public string JarPath { get; init; } = string.Empty;
    public int Port { get; init; } = 25565;
    public int MaxPlayers { get; init; } = 20;
    public string Motd { get; init; } = "MEME Launcher 服务器";
    public bool OnlineMode { get; init; }
    public int MemoryMb { get; init; } = 2048;
    public string JavaPath { get; init; } = string.Empty;
    public bool AcceptEula { get; init; }
}

/// <summary>
/// 一键开服：下载服务端核心（原版 / Fabric）、写 eula 与 server.properties、起进程、转发控制台。
/// Forge / NeoForge 服务端需要跑安装器，暂未接入。
/// </summary>
public sealed class ServerManager : IDisposable
{
    private readonly Net.DownloadClient _client;
    private readonly string _gameDirectory;
    private Process? _process;

    public ServerManager(Net.DownloadClient client, string gameDirectory)
    {
        _client = client;
        _gameDirectory = gameDirectory;
    }

    public event Action<string>? Output;

    public bool IsRunning => _process is { HasExited: false };

    public static string ServersRoot(string gameDirectory) => Path.Combine(gameDirectory, "servers");

    /// <summary>准备服务端 jar，返回路径。</summary>
    public async Task<string> EnsureServerJarAsync(string versionId, string loader, string targetDirectory,
        IProgress<Net.InstallProgress>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(targetDirectory);
        var jarName = loader.Equals("fabric", StringComparison.OrdinalIgnoreCase)
            ? "fabric-server-launch.jar"
            : "server.jar";
        var jarPath = Path.Combine(targetDirectory, jarName);
        if (File.Exists(jarPath) && new FileInfo(jarPath).Length > 100_000)
        {
            return jarPath;
        }

        if (loader.Equals("fabric", StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report(new Net.InstallProgress { Stage = "服务端", Message = "获取 Fabric 服务端…" });
            var mcVersion = versionId;
            var loaderVersion = await GetLatestFabricLoaderAsync(mcVersion, ct).ConfigureAwait(false);
            var url = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/server/jar";
            await _client.DownloadFileAsync(url, jarPath, ct: ct).ConfigureAwait(false);
        }
        else
        {
            progress?.Report(new Net.InstallProgress { Stage = "服务端", Message = "读取原版服务端地址…" });
            var url = await GetVanillaServerUrlAsync(versionId, ct).ConfigureAwait(false)
                      ?? throw new InvalidOperationException("这个版本没有服务端下载地址：" + versionId);
            await _client.DownloadFileAsync(url, jarPath, ct: ct).ConfigureAwait(false);
        }

        progress?.Report(new Net.InstallProgress { Stage = "服务端", Done = 1, Total = 1, Message = "服务端核心已就绪" });
        return jarPath;
    }

    private async Task<string?> GetVanillaServerUrlAsync(string versionId, CancellationToken ct)
    {
        // 优先读本地已安装版本的 json（省一次网络请求）
        var local = Path.Combine(_gameDirectory, "versions", versionId, versionId + ".json");
        if (File.Exists(local))
        {
            var url = ReadServerUrl(File.ReadAllText(local));
            if (!string.IsNullOrEmpty(url))
            {
                return url;
            }
        }

        var manifest = await _client
            .GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", ct)
            .ConfigureAwait(false);
        using var doc = JsonDocument.Parse(manifest);
        if (!doc.RootElement.TryGetProperty("versions", out var versions))
        {
            return null;
        }

        foreach (var item in versions.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var i) ? i.GetString() : null;
            if (!string.Equals(id, versionId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var metaUrl = item.GetProperty("url").GetString();
            if (string.IsNullOrEmpty(metaUrl))
            {
                return null;
            }

            var json = await _client.GetStringAsync(metaUrl!, ct).ConfigureAwait(false);
            return ReadServerUrl(json);
        }

        return null;
    }

    private static string? ReadServerUrl(string versionJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(versionJson);
            if (doc.RootElement.TryGetProperty("downloads", out var downloads) &&
                downloads.TryGetProperty("server", out var server) &&
                server.TryGetProperty("url", out var url))
            {
                return url.GetString();
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }

    private async Task<string> GetLatestFabricLoaderAsync(string mcVersion, CancellationToken ct)
    {
        var json = await _client.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}", ct)
            .ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("loader", out var loader) &&
                loader.TryGetProperty("version", out var version))
            {
                var stable = !loader.TryGetProperty("stable", out var s) || s.ValueKind != JsonValueKind.False;
                if (stable)
                {
                    return version.GetString() ?? string.Empty;
                }
            }
        }

        throw new InvalidOperationException("没有找到适配的 Fabric 加载器版本");
    }

    /// <summary>写 eula.txt 和 server.properties。</summary>
    public void Configure(ServerLaunchOptions options)
    {
        Directory.CreateDirectory(options.Directory);
        File.WriteAllText(Path.Combine(options.Directory, "eula.txt"),
            "# 由 MEME Launcher 生成\neula=" + (options.AcceptEula ? "true" : "false") + "\n");

        var props = Path.Combine(options.Directory, "server.properties");
        var lines = new List<string>
        {
            "server-port=" + options.Port,
            "online-mode=" + (options.OnlineMode ? "true" : "false"),
            "max-players=" + options.MaxPlayers,
            "motd=" + options.Motd,
            "enable-command-block=true",
            "allow-flight=true",
            "spawn-protection=0",
            "view-distance=8",
            "simulation-distance=6",
        };

        // 保留用户手改过的其它配置项
        if (File.Exists(props))
        {
            foreach (var line in File.ReadAllLines(props))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#') || !line.Contains('='))
                {
                    continue;
                }

                var key = line[..line.IndexOf('=')];
                if (!lines.Any(l => l.StartsWith(key + "=", StringComparison.Ordinal)))
                {
                    lines.Add(line);
                }
            }
        }

        File.WriteAllText(props, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    }

    public void Start(ServerLaunchOptions options)
    {
        if (IsRunning)
        {
            return;
        }

        if (!options.AcceptEula)
        {
            throw new InvalidOperationException("需要先勾选同意 Minecraft EULA");
        }

        var psi = new ProcessStartInfo
        {
            FileName = options.JavaPath,
            WorkingDirectory = options.Directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        psi.ArgumentList.Add($"-Xmx{options.MemoryMb}M");
        psi.ArgumentList.Add("-Xms512M");
        psi.ArgumentList.Add("-Dfile.encoding=UTF-8");
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(options.JarPath);
        psi.ArgumentList.Add("nogui");

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => Emit(e.Data);
        process.ErrorDataReceived += (_, e) => Emit(e.Data);
        process.Exited += (_, __) => Output?.Invoke("[启动器] 服务端已退出");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;
        Output?.Invoke($"[启动器] 服务端已启动，端口 {options.Port}");
    }

    /// <summary>发 stop 命令，让服务端自己保存世界再退出。</summary>
    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.StandardInput.WriteLine("stop");
                _process.StandardInput.Flush();
                if (!_process.WaitForExit(15000))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        catch
        {
            try
            {
                _process?.Kill(entireProcessTree: true);
            }
            catch
            {
                // 忽略
            }
        }
        finally
        {
            _process = null;
        }
    }

    public void Dispose() => Stop();

    private void Emit(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            Output?.Invoke(line!);
        }
    }
}
