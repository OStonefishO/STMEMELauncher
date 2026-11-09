using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace Aurora.Core.Network;

/// <summary>
/// 樱花穿透（SakuraFrp）客户端封装：
/// 自动从官方 CDN 下载 frpc，用 `-f 访问密钥:隧道ID` 拉起，日志实时回传。
/// </summary>
public sealed class SakuraFrp : IDisposable
{
    private const string CdnBase = "https://nya.globalslb.net/natfrp/client/frpc/";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private Process? _process;

    public static string WorkDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MEMELauncher", "frpc");

    public static string ExePath => Path.Combine(WorkDirectory, "frpc.exe");

    public event Action<string>? Output;

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>没有 frpc 就自动下载最新版（约 8 MB）。</summary>
    public async Task<string> EnsureAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (File.Exists(ExePath) && new FileInfo(ExePath).Length > 1_000_000)
        {
            return ExePath;
        }

        Directory.CreateDirectory(WorkDirectory);
        progress?.Report("获取 frpc 版本列表…");

        var listing = await _http.GetStringAsync(CdnBase, ct).ConfigureAwait(false);
        var versions = Regex.Matches(listing, "href=\"([0-9]+\\.[0-9]+\\.[0-9]+-sakura-[0-9.]+)/\"")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderByDescending(v => ParseVersion(v))
            .ToList();

        if (versions.Count == 0)
        {
            throw new InvalidOperationException("拿不到 frpc 版本列表，可能是官方 CDN 变了");
        }

        var url = $"{CdnBase}{versions[0]}/frpc_windows_amd64.exe";
        progress?.Report($"下载 frpc {versions[0]}…");

        var bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
        var temp = ExePath + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, ct).ConfigureAwait(false);
        if (File.Exists(ExePath))
        {
            File.Delete(ExePath);
        }

        File.Move(temp, ExePath);
        progress?.Report("frpc 已就绪");
        return ExePath;
    }

    public void Start(string token, string tunnelIds)
    {
        if (IsRunning)
        {
            return;
        }

        if (!File.Exists(ExePath))
        {
            throw new InvalidOperationException("还没有下载 frpc");
        }

        var psi = new ProcessStartInfo
        {
            FileName = ExePath,
            WorkingDirectory = WorkDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add($"{token.Trim()}:{tunnelIds.Trim()}");

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => Emit(e.Data);
        process.ErrorDataReceived += (_, e) => Emit(e.Data);
        process.Exited += (_, __) => Output?.Invoke("[frpc] 进程已退出");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;
        Output?.Invoke($"[启动器] frpc 已启动，参数 -f {Mask(token)}:{tunnelIds}");
    }

    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 忽略
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

    private static string Mask(string token) =>
        token.Length <= 6 ? "***" : token[..3] + "***" + token[^3..];

    private static Version ParseVersion(string text)
    {
        var numbers = Regex.Matches(text, "[0-9]+").Select(m => int.TryParse(m.Value, out var n) ? n : 0).ToArray();
        while (numbers.Length < 4)
        {
            numbers = numbers.Append(0).ToArray();
        }

        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }
}

