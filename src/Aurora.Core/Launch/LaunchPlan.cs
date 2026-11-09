using Aurora.Core.Models;

namespace Aurora.Core.Launch;

public sealed class LaunchOptions
{
    public string PlayerName { get; set; } = "Steve";
    public string? Uuid { get; set; }
    public string AccessToken { get; set; } = "0";
    public string UserType { get; set; } = "legacy";
    public string? GameDirectory { get; set; }
    public string? JavaPath { get; set; }
    public int MinMemoryMb { get; set; } = 512;
    public int MaxMemoryMb { get; set; } = 4096;
    public string ExtraJvmArgs { get; set; } = string.Empty;
    public string ExtraGameArgs { get; set; } = string.Empty;
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public string LauncherName { get; set; } = "MemeLauncher";
    public string LauncherVersion { get; set; } = "0.2.0";
    /// <summary>外置登录：authlib-injector 路径与皮肤站 API 根地址。</summary>
    public string? AuthlibInjectorPath { get; set; }

    public string? YggdrasilApiRoot { get; set; }

    public string? ServerAddress { get; set; }
    public int ServerPort { get; set; }
}

public sealed class LaunchPlan
{
    public string JavaPath { get; init; } = string.Empty;
    public string MainClass { get; init; } = string.Empty;
    public List<string> JvmArguments { get; init; } = new();
    public List<string> GameArguments { get; init; } = new();
    public string WorkingDirectory { get; init; } = string.Empty;
    public string NativesDirectory { get; init; } = string.Empty;
    public List<string> ClassPath { get; init; } = new();
    public List<string> Warnings { get; init; } = new();

    /// <summary>完整参数（不含可执行文件本身）。</summary>
    public List<string> AllArguments
    {
        get
        {
            var all = new List<string>(JvmArguments.Count + GameArguments.Count + 1);
            all.AddRange(JvmArguments);
            all.Add(MainClass);
            all.AddRange(GameArguments);
            return all;
        }
    }

    public string CommandLinePreview
    {
        get
        {
            var parts = new List<string> { Quote(JavaPath) };
            parts.AddRange(AllArguments.Select(Quote));
            return string.Join(" ", parts);
        }
    }

    private static string Quote(string value) =>
        value.Contains(' ') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\\\"") + "\""
            : value;
}

public sealed class LaunchResult
{
    public bool Started { get; init; }
    public int ExitCode { get; init; }
    public string? Error { get; init; }
    public TimeSpan Duration { get; init; }
    public LaunchPlan? Plan { get; init; }
    public string? CrashReportPath { get; init; }
    public string? CrashSummary { get; init; }

    /// <summary>退出码非 0 时的分析结果（启动器据此弹崩溃窗口）。</summary>
    public CrashInsight? Insight { get; init; }
}
