using System.IO.Compression;
using System.Text.Json;
using Aurora.Core.Infrastructure;
using Aurora.Core.Models;

namespace Aurora.Core.Net;

public sealed class LoaderOption
{
    public string Loader { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string? McVersion { get; init; }
    public bool IsStable { get; init; } = true;

    public override string ToString() => Version + (IsStable ? string.Empty : " (beta)");
}

/// <summary>
/// 装加载器：Fabric 走 meta，Forge/NeoForge 下载官方安装器后直接取出其中的 version.json，
/// 再由 LibraryInstaller 从镜像拉依赖库——不需要运行安装器，也就不怕 maven.neoforged.net 被墙。
/// </summary>
public sealed class LoaderInstaller
{
    private const string FabricMeta = "https://meta.fabricmc.net/v2";
    private const string NeoForgeList = "https://bmclapi2.bangbang93.com/neoforge/list/";
    private const string ForgeList = "https://bmclapi2.bangbang93.com/forge/minecraft/";

    private readonly DownloadClient _client;
    private readonly string _gameDirectory;
    private readonly Func<int, string?>? _javaSelector;

    public LoaderInstaller(DownloadClient client, string gameDirectory, Func<int, string?>? javaSelector = null)
    {
        _client = client;
        _gameDirectory = gameDirectory;
        _javaSelector = javaSelector;
    }

    public string VersionsDirectory => Path.Combine(_gameDirectory, "versions");

    public async Task<List<LoaderOption>> ListAsync(string loader, string mcVersion, CancellationToken ct = default)
    {
        var result = new List<LoaderOption>();
        switch (loader.ToLowerInvariant())
        {
            case "fabric":
            {
                var json = await _client.GetStringAsync($"{FabricMeta}/versions/loader/{mcVersion}", ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("loader", out var l))
                    {
                        continue;
                    }

                    result.Add(new LoaderOption
                    {
                        Loader = "fabric",
                        McVersion = mcVersion,
                        Version = l.TryGetProperty("version", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                        IsStable = !l.TryGetProperty("stable", out var s) || s.ValueKind != JsonValueKind.False,
                    });
                }
                break;
            }

            case "neoforge":
            {
                var json = await _client.GetStringAsync(NeoForgeList + mcVersion, ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var version = item.TryGetProperty("version", out var v) ? v.GetString() : null;
                    if (string.IsNullOrEmpty(version))
                    {
                        continue;
                    }

                    result.Add(new LoaderOption
                    {
                        Loader = "neoforge",
                        McVersion = mcVersion,
                        Version = version!,
                        IsStable = !version!.Contains("beta", StringComparison.OrdinalIgnoreCase),
                    });
                }
                break;
            }

            case "forge":
            {
                var json = await _client.GetStringAsync(ForgeList + mcVersion, ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var version = item.TryGetProperty("version", out var v) ? v.GetString() : null;
                    if (string.IsNullOrEmpty(version))
                    {
                        continue;
                    }

                    result.Add(new LoaderOption
                    {
                        Loader = "forge",
                        McVersion = mcVersion,
                        Version = version!,
                    });
                }
                break;
            }
        }

        return result
            .OrderByDescending(o => o.Version, new VersionComparer())
            .ToList();
    }

    /// <summary>安装加载器，返回新版本 id。</summary>
    public async Task<string> InstallAsync(LoaderOption option, IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(new InstallProgress { Stage = "加载器", Message = $"准备 {option.Loader} {option.Version}" });

        var installedId = option.Loader.ToLowerInvariant() switch
        {
            "fabric" => await InstallFabricAsync(option, progress, ct).ConfigureAwait(false),
            "neoforge" => await InstallFromInstallerJarAsync(option, BuildNeoForgeInstallerUrl(option), progress, ct).ConfigureAwait(false),
            "forge" => await InstallFromInstallerJarAsync(option, BuildForgeInstallerUrl(option), progress, ct).ConfigureAwait(false),
            _ => throw new NotSupportedException("不支持的加载器: " + option.Loader),
        };

        VerifyInstalled(installedId, progress);
        return installedId;
    }

    /// <summary>
    /// 装完检查一遍：版本 json 在不在、依赖库齐不齐。
    /// 以前这里什么都不查，缺东西也照样报「完成」，玩家一启动就崩。
    /// </summary>
    private void VerifyInstalled(string id, IProgress<InstallProgress>? progress)
    {
        var jsonPath = Path.Combine(VersionsDirectory, id, id + ".json");
        if (!File.Exists(jsonPath))
        {
            throw new InvalidOperationException($"安装后没找到版本文件：{jsonPath}");
        }

        var missing = new List<string>();
        try
        {
            var parsed = Json.Deserialize<VersionJson>(File.ReadAllText(jsonPath));
            foreach (var lib in parsed?.Libraries ?? new List<Library>())
            {
                var path = Versions.MavenHelper.ResolveArtifactPath(lib, Path.Combine(_gameDirectory, "libraries"));
                if (path != null && !File.Exists(path))
                {
                    missing.Add(Path.GetFileName(path));
                }
            }
        }
        catch
        {
            return;
        }

        if (missing.Count > 0)
        {
            var sample = string.Join("、", missing.Take(3));
            throw new InvalidOperationException(
                $"安装不完整：{missing.Count} 个依赖库没下下来（{sample}）· 网络或镜像有问题，点「修复资源」可以补");
        }

        progress?.Report(new InstallProgress
        {
            Stage = "加载器",
            Done = 1,
            Total = 1,
            Message = id + " 安装完成（依赖库已校验）",
        });
    }

    private async Task<string> InstallFabricAsync(LoaderOption option, IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        var url = $"{FabricMeta}/versions/loader/{option.McVersion}/{option.Version}/profile/json";
        var json = await _client.GetStringAsync(url, ct).ConfigureAwait(false);
        var parsed = Json.Deserialize<VersionJson>(json)
                     ?? throw new InvalidOperationException("Fabric profile 解析失败");

        var id = string.IsNullOrEmpty(parsed.Id)
            ? $"fabric-loader-{option.McVersion}-{option.Version}"
            : parsed.Id!;

        var dir = Path.Combine(VersionsDirectory, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, id + ".json"), json);

        progress?.Report(new InstallProgress { Stage = "加载器", Message = "写入 Fabric 版本 json 完成" });

        var installer = new LibraryInstaller(_client, _gameDirectory);
        await installer.InstallAsync(parsed, progress, ct).ConfigureAwait(false);
        return id;
    }

    private async Task<string> InstallFromInstallerJarAsync(LoaderOption option, string installerUrl,
        IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"meme-loader-{Guid.NewGuid():N}.jar");
        progress?.Report(new InstallProgress { Stage = "加载器", Message = "下载安装器…" });
        try
        {
            await _client.DownloadFileAsync(installerUrl, temp, ct: ct).ConfigureAwait(false);

            string? versionJson = null;
            string? profileJson = null;
            string id;
            using (var zip = ZipFile.OpenRead(temp))
            {
                var entry = zip.Entries.FirstOrDefault(e => e.FullName == "version.json")
                            ?? zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("version.json"));
                if (entry == null)
                {
                    throw new InvalidOperationException("安装器里没有 version.json（版本可能已变化）");
                }

                using (var reader = new StreamReader(entry.Open()))
                {
                    versionJson = reader.ReadToEnd();
                }

                var profileEntry = zip.Entries.FirstOrDefault(e => e.FullName == "install_profile.json");
                if (profileEntry != null)
                {
                    using var reader = new StreamReader(profileEntry.Open());
                    profileJson = reader.ReadToEnd();
                }
            }

            var parsed = Json.Deserialize<VersionJson>(versionJson)
                         ?? throw new InvalidOperationException("version.json 解析失败");
            id = parsed.Id ?? $"{option.Loader}-{option.Version}";

            var dir = Path.Combine(VersionsDirectory, id);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, id + ".json"), versionJson!);

            progress?.Report(new InstallProgress { Stage = "加载器", Message = $"已写入 {id}" });

            var libInstaller = new LibraryInstaller(_client, _gameDirectory);
            var ok = await libInstaller.InstallAsync(parsed, progress, ct).ConfigureAwait(false);

            // 安装器里的 processors 还需要一批自己的库（install_profile.json），不装就跑不起来
            if (!string.IsNullOrEmpty(profileJson))
            {
                var profile = Json.Deserialize<VersionJson>(profileJson!);
                if (profile?.Libraries is { Count: > 0 })
                {
                    progress?.Report(new InstallProgress
                    {
                        Stage = "加载器",
                        Message = $"下载安装器依赖 {profile.Libraries.Count} 个…",
                    });
                    await libInstaller.InstallAsync(profile, progress, ct).ConfigureAwait(false);
                }
            }

            // NeoForge / Forge 启动时会去 libraries\net\minecraft\client\<MCP_VERSION> 找 client-<id>-srg.jar，
            // 这个文件是安装器 processors 的产物，离线模式常常没生成 —— 我们自己补一份
            await EnsureClientSrgJarAsync(option, profileJson, dir, progress, ct).ConfigureAwait(false);

            // Forge 需要安装器把 vanilla client 打补丁成 forge-client，这一步只能跑安装器
            var needsPatcher = option.Loader.Equals("forge", StringComparison.OrdinalIgnoreCase) ||
                               parsed.Libraries != null && parsed.Libraries.Any(l => l.Name.EndsWith(":client", StringComparison.Ordinal));
            if (needsPatcher)
            {
                await RunInstallerOfflineAsync(temp, option, progress, ct).ConfigureAwait(false);
            }

            progress?.Report(new InstallProgress { Stage = "加载器", Done = 1, Total = 1, Message = $"依赖库 {ok} 个完成" });
            return id;
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch
            {
                // 忽略临时文件清理失败
            }
        }
    }

    /// <summary>
    /// 用本地已下载好的依赖库跑一遍官方安装器（--offline），让它生成补丁后的 client jar。
    /// 走离线模式是为了绕开被墙的 maven.neoforged.net。
    /// </summary>
    private async Task RunInstallerOfflineAsync(string installerJar, LoaderOption option,
        IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        var java = ResolveJava(option);
        if (java == null)
        {
            progress?.Report(new InstallProgress { Stage = "加载器", Message = "没找到可用的 Java，跳过补丁步骤" });
            return;
        }

        progress?.Report(new InstallProgress { Stage = "加载器", Message = "生成客户端补丁（离线安装器）…" });

        // 安装器需要一个 launcher_profiles.json 才肯干活
        var profiles = Path.Combine(_gameDirectory, "launcher_profiles.json");
        if (!File.Exists(profiles))
        {
            File.WriteAllText(profiles,
                "{\"profiles\":{},\"selectedProfile\":\"\",\"clientToken\":\"meme\",\"authenticationDatabase\":{},\"launcherVersion\":{\"name\":\"MEME\",\"format\":21}}");
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = java,
            WorkingDirectory = _gameDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(installerJar);
        psi.ArgumentList.Add("--installClient");
        psi.ArgumentList.Add(_gameDirectory);
        psi.ArgumentList.Add("--offline");

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
        {
            return;
        }

        var output = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            progress?.Report(new InstallProgress
            {
                Stage = "加载器",
                Message = "安装器返回 " + process.ExitCode + "（补丁可能没生成）",
            });
        }
        else
        {
            progress?.Report(new InstallProgress { Stage = "加载器", Message = "客户端补丁完成" });
        }
    }

    /// <summary>
    /// 补上 client-&lt;MCP_VERSION&gt;-srg.jar：NeoForge/Forge 加载时找不到它就直接报
    /// “Your NeoForge installation is corrupted”。用原版 client.jar 复制一份即可（它只要求 Minecraft 类在 classpath 上）。
    /// </summary>
    private async Task EnsureClientSrgJarAsync(LoaderOption option, string? profileJson, string versionDir,
        IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        try
        {
            // 把 install_profile 留一份，之后修复/启动时还能知道该补哪个 id
            if (!string.IsNullOrEmpty(profileJson))
            {
                File.WriteAllText(Path.Combine(versionDir, ".meme-install-profile.json"), profileJson!);
            }

            var id = ReadMcpVersion(profileJson) ?? option.McVersion;
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var srgDir = Path.Combine(_gameDirectory, "libraries", "net", "minecraft", "client", id);
            var srgJar = Path.Combine(srgDir, $"client-{id}-srg.jar");
            if (File.Exists(srgJar) && new FileInfo(srgJar).Length > 1024 * 1024)
            {
                return;
            }

            var vanillaJar = Path.Combine(VersionsDirectory, option.McVersion, option.McVersion + ".jar");
            if (!File.Exists(vanillaJar))
            {
                progress?.Report(new InstallProgress
                {
                    Stage = "加载器",
                    Message = $"缺原版 client.jar，暂时补不了 client-{id}-srg.jar",
                });
                return;
            }

            Directory.CreateDirectory(srgDir);
            File.Copy(vanillaJar, srgJar, overwrite: true);
            progress?.Report(new InstallProgress { Stage = "加载器", Message = $"已补 client-{id}-srg.jar" });
            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch
        {
            // 补不上就让游戏自己报错，不影响安装结果
        }
    }

    /// <summary>从 install_profile.json 里读 MCP_VERSION / MC_VERSION。</summary>
    internal static string? ReadMcpVersion(string? profileJson)
    {
        if (string.IsNullOrWhiteSpace(profileJson))
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(profileJson!);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return null;
            }

            foreach (var key in new[] { "MCP_VERSION", "MC_VERSION" })
            {
                if (!data.TryGetProperty(key, out var entry))
                {
                    continue;
                }

                if (entry.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    entry.TryGetProperty("client", out var client) &&
                    client.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return client.GetString()?.Trim('\'', '"', ' ');
                }

                if (entry.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return entry.GetString()?.Trim('\'', '"', ' ');
                }
            }
        }
        catch
        {
            // 解析失败就算了
        }

        return null;
    }

    private string? ResolveJava(LoaderOption option)
    {
        var major = Versions.VersionRepository.InferJavaMajor(option.McVersion);
        if (_javaSelector != null)
        {
            var picked = _javaSelector(major);
            if (!string.IsNullOrEmpty(picked))
            {
                return picked;
            }
        }

        var locator = new Java.JavaLocator(new[] { Path.Combine(_gameDirectory, "runtime") });
        return locator.Select(major)?.JavaPath;
    }

    private static string BuildNeoForgeInstallerUrl(LoaderOption option) =>
        $"https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{option.Version}/neoforge-{option.Version}-installer.jar";

    private static string BuildForgeInstallerUrl(LoaderOption option) =>
        $"https://maven.minecraftforge.net/net/minecraftforge/forge/{option.McVersion}-{option.Version}/" +
        $"forge-{option.McVersion}-{option.Version}-installer.jar";

    private sealed class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null || y == null)
            {
                return string.CompareOrdinal(x, y);
            }

            var a = x.Split('.', '-');
            var b = y.Split('.', '-');
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var na = i < a.Length && int.TryParse(a[i], out var va) ? va : -1;
                var nb = i < b.Length && int.TryParse(b[i], out var vb) ? vb : -1;
                if (na != nb)
                {
                    return na.CompareTo(nb);
                }
            }

            return string.CompareOrdinal(x, y);
        }
    }
}
