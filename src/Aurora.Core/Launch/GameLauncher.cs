using System.Diagnostics;
using System.Text;

namespace Aurora.Core.Launch;

public sealed class GameLauncher
{
    /// <summary>游戏的标准输出/错误，逐行回调。</summary>
    public event Action<string>? OutputReceived;

    /// <summary>进程启动后回调（pid）。</summary>
    public event Action<int>? Started;

    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>强制结束游戏进程（游戏卡死时用）。</summary>
    public bool Kill()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
                return true;
            }
        }
        catch
        {
            // 忽略
        }

        return false;
    }

    public async Task<LaunchResult> LaunchAsync(LaunchPlan plan, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(plan.JavaPath))
        {
            return new LaunchResult
            {
                Started = false,
                Error = "找不到 Java: " + plan.JavaPath,
                Plan = plan,
            };
        }

        var psi = new ProcessStartInfo
        {
            FileName = plan.JavaPath,
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };

        foreach (var arg in plan.AllArguments)
        {
            psi.ArgumentList.Add(arg);
        }

        var stopwatch = Stopwatch.StartNew();
        var normalizer = new Infrastructure.LogNormalizer();
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process = process;

        process.OutputDataReceived += (_, e) => Emit(normalizer, e.Data);
        process.ErrorDataReceived += (_, e) => Emit(normalizer, e.Data);

        try
        {
            if (!process.Start())
            {
                return new LaunchResult { Started = false, Error = "进程启动失败", Plan = plan };
            }
        }
        catch (Exception ex)
        {
            return new LaunchResult { Started = false, Error = "进程启动异常: " + ex.Message, Plan = plan };
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        Started?.Invoke(process.Id);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return new LaunchResult
            {
                Started = true,
                ExitCode = -1,
                Error = "已取消",
                Duration = stopwatch.Elapsed,
                Plan = plan,
            };
        }

        stopwatch.Stop();
        foreach (var line in normalizer.Flush())
        {
            OutputReceived?.Invoke(line);
        }

        var exitCode = process.ExitCode;
        var crash = exitCode != 0 ? CrashAnalyzer.FindLatest(plan.WorkingDirectory) : null;

        return new LaunchResult
        {
            Started = true,
            ExitCode = exitCode,
            Duration = stopwatch.Elapsed,
            Plan = plan,
            CrashReportPath = crash?.Path,
            CrashSummary = crash?.Summary,
        };
    }

    /// <summary>启动后不等待退出，直接返回进程句柄（给 UI 用）。</summary>
    public Process? StartDetached(LaunchPlan plan, out string? error)
    {
        error = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = plan.JavaPath,
                WorkingDirectory = plan.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
            };

            foreach (var arg in plan.AllArguments)
            {
                psi.ArgumentList.Add(arg);
            }

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var normalizer = new Infrastructure.LogNormalizer();
            process.OutputDataReceived += (_, e) => Emit(normalizer, e.Data);
            process.ErrorDataReceived += (_, e) => Emit(normalizer, e.Data);

            if (!process.Start())
            {
                error = "进程启动失败";
                return null;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private void Emit(Infrastructure.LogNormalizer normalizer, string? raw)
    {
        foreach (var line in normalizer.Feed(raw))
        {
            OutputReceived?.Invoke(line);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 忽略
        }
    }
}

public sealed class CrashInfo
{
    public string Path { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string[] HeadLines { get; init; } = Array.Empty<string>();
}

/// <summary>从 crash-reports / logs 里提取最有用的崩溃信息。</summary>
public static class CrashAnalyzer
{
    public static CrashInfo? FindLatest(string gameDirectory)
    {
        var crashDir = Path.Combine(gameDirectory, "crash-reports");
        if (Directory.Exists(crashDir))
        {
            var latest = Directory.GetFiles(crashDir, "*.txt")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (latest != null && DateTime.UtcNow - File.GetLastWriteTimeUtc(latest) < TimeSpan.FromMinutes(30))
            {
                return ReadCrash(latest);
            }
        }

        return null;
    }

    private static CrashInfo ReadCrash(string path)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch
        {
            return new CrashInfo { Path = path, Summary = "无法读取崩溃报告" };
        }

        var description = lines.FirstOrDefault(l => l.TrimStart().StartsWith("Description:", StringComparison.OrdinalIgnoreCase));
        var cause = lines.FirstOrDefault(l => l.StartsWith("\t", StringComparison.Ordinal) && l.Contains("Exception", StringComparison.OrdinalIgnoreCase));
        var summary = string.Join(" | ", new[] { description, cause }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new CrashInfo
        {
            Path = path,
            Summary = string.IsNullOrWhiteSpace(summary) ? "退出码非 0（无崩溃报告）" : summary,
            HeadLines = lines.Take(40).ToArray(),
        };
    }
}
