using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace Aurora.Core.Net;

/// <summary>
/// 启动器自更新：下载新 exe → 用一段 cmd 脚本等旧进程退出 → 覆盖 → 重新启动。
/// 只有玩家点「立即更新」才会走到这里，检查更新本身从不自动动手。
/// </summary>
public sealed class SelfUpdater
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static string CurrentExePath
    {
        get
        {
            try
            {
                var path = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }
            catch
            {
                // 忽略
            }

            return Path.Combine(AppContext.BaseDirectory, "MemeLauncher.exe");
        }
    }

    public static ReleaseAsset? PickAsset(ReleaseInfo release)
    {
        return release.Assets
            .Where(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Name.Contains("启动器", StringComparison.Ordinal) ||
                                    a.Name.Contains("MemeLauncher", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    /// <summary>下载新版本到临时目录，返回文件路径。</summary>
    public async Task<string> DownloadAsync(ReleaseInfo release, IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var asset = PickAsset(release)
                    ?? throw new InvalidOperationException("这个版本没有附带 exe，请到 Release 页面手动下载");

        var target = Path.Combine(Path.GetTempPath(), $"MemeLauncher-{release.VersionText}.exe");

        // GitHub 直连在部分网络不可达，依次尝试
        var candidates = new[]
        {
            asset.Url,
            "https://gh-proxy.com/" + asset.Url,
            "https://ghfast.top/" + asset.Url,
        };

        Exception? last = null;
        foreach (var url in candidates)
        {
            try
            {
                await DownloadToAsync(url, target, progress, ct).ConfigureAwait(false);
                var size = new FileInfo(target).Length;
                if (size < 20 * 1024 * 1024)
                {
                    throw new InvalidOperationException($"下载的文件太小（{size / 1024 / 1024} MB），可能不是完整包");
                }

                return target;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException("下载失败：" + (last?.Message ?? "未知错误"));
    }

    private async Task DownloadToAsync(string url, string target, IProgress<double>? progress, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var destination = File.Create(target);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received += read;
            if (total > 0)
            {
                progress?.Report((double)received / total);
            }
        }
    }

    /// <summary>把替换脚本写好并执行，随后调用方应该立刻退出程序。</summary>
    public static void ApplyAndRestart(string newExePath)
    {
        var current = CurrentExePath;
        var script = Path.Combine(Path.GetTempPath(), $"meme-update-{Guid.NewGuid():N}.cmd");

        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul");
        sb.AppendLine("ping 127.0.0.1 -n 3 >nul");
        sb.AppendLine("set /a tries=0");
        sb.AppendLine(":retry");
        sb.AppendLine($"move /y \"{newExePath}\" \"{current}\" >nul 2>&1");
        sb.AppendLine("if not errorlevel 1 goto done");
        sb.AppendLine("set /a tries+=1");
        sb.AppendLine("if %tries% geq 20 goto giveup");
        sb.AppendLine("timeout /t 1 /nobreak >nul");
        sb.AppendLine("goto retry");
        sb.AppendLine(":giveup");
        sb.AppendLine("del \"%~f0\" >nul 2>&1");
        sb.AppendLine("exit /b 1");
        sb.AppendLine(":done");
        sb.AppendLine($"start \"\" \"{current}\"");
        sb.AppendLine("del \"%~f0\" >nul 2>&1");

        File.WriteAllText(script, sb.ToString(), new UTF8Encoding(false));

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c \"" + script + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}
