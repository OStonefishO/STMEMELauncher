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
    private readonly List<string> _recentOutput = new();
    private DateTimeOffset _startedAt = DateTimeOffset.Now;

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>最近抓到的日志（给崩溃分析用）。</summary>
    public IReadOnlyList<string> RecentOutput => _recentOutput;

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
        _startedAt = DateTimeOffset.Now;
        _recentOutput.Clear();
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
            Remember(line);
            OutputReceived?.Invoke(line);
        }

        var exitCode = process.ExitCode;
        CrashInsight? insight = null;
        if (exitCode != 0)
        {
            insight = CrashInspector.Analyze(plan.WorkingDirectory, exitCode, _recentOutput, _startedAt);
        }

        return new LaunchResult
        {
            Started = true,
            ExitCode = exitCode,
            Duration = stopwatch.Elapsed,
            Plan = plan,
            CrashReportPath = insight?.GameCrashReportPath,
            CrashSummary = insight == null ? null : insight.Headline,
            Insight = insight,
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
            Remember(line);
            OutputReceived?.Invoke(line);
        }
    }

    private void Remember(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        _recentOutput.Add(line.Length > 800 ? line[..800] : line);
        if (_recentOutput.Count > 800)
        {
            _recentOutput.RemoveRange(0, _recentOutput.Count - 800);
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
