using Aurora.Core.Auth;
using Aurora.Core.Configuration;
using Aurora.Core.Java;
using Aurora.Core.Launch;
using Aurora.Core.Models;
using Aurora.Core.Net;
using Aurora.Core.Versions;

namespace Aurora.Core;

/// <summary>把配置、版本仓库、Java 探测、命令行构建、进程启动串起来的门面层。</summary>
public sealed class LauncherService
{
    public LauncherService(LauncherSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Versions = new VersionRepository(settings.GameDirectory);
    }

    private DownloadClient? _shared;

    public LauncherSettings Settings { get; }

    public VersionRepository Versions { get; }

    /// <summary>当前登录的正版账号（null 表示离线登录）。</summary>
    public AuthSession? Account { get; set; }

    public DownloadClient SharedDownloader =>
        _shared ??= new DownloadClient(Settings.UseMirror) { MirrorBaseUrl = Settings.MirrorBaseUrl };

    public MicrosoftAuth CreateAuth() => new(Settings.MicrosoftClientId);

    public LoaderInstaller CreateLoaderInstaller() =>
        new(SharedDownloader, Settings.GameDirectory, major => SelectJava(major, out _)?.JavaPath);

    public ContentInstaller CreateContentInstaller() => new(SharedDownloader);

    public ModpackInstaller CreateModpackInstaller() => new(SharedDownloader, Settings.GameDirectory);

    public ModrinthClient CreateModrinth() => new(SharedDownloader);

    public CurseForgeClient CreateCurseForge() => new(SharedDownloader);

    public IReadOnlyList<JavaRuntime> ScanJava()
    {
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(Settings.JavaPath))
        {
            roots.Add(Settings.JavaPath);
        }

        roots.AddRange(Settings.ExtraJavaPaths);
        roots.Add(Path.Combine(Settings.GameDirectory, "runtime"));
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "runtime"));
        return new JavaLocator(roots).Scan();
    }

    public JavaRuntime? SelectJava(int requiredMajor, out IReadOnlyList<JavaRuntime> all)
    {
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(Settings.JavaPath))
        {
            roots.Add(Settings.JavaPath);
        }

        roots.AddRange(Settings.ExtraJavaPaths);
        roots.Add(Path.Combine(Settings.GameDirectory, "runtime"));
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "runtime"));

        var locator = new JavaLocator(roots);
        all = locator.Scan();
        return locator.Select(requiredMajor, all);
    }

    public List<VersionSummary> ListVersions() => Versions.Scan();

    public ResolvedVersion? Resolve(string id) => Versions.Resolve(id);

    public LaunchPlan? BuildPlan(string versionId, out ResolvedVersion? resolved, out List<string> problems,
        string? playerName = null, string? javaPath = null)
    {
        problems = new List<string>();
        resolved = Versions.Resolve(versionId);
        if (resolved == null)
        {
            problems.Add("找不到版本: " + versionId);
            return null;
        }

        var java = !string.IsNullOrWhiteSpace(javaPath)
            ? new JavaRuntime { JavaPath = javaPath!, Major = resolved.JavaMajor, VersionText = "custom" }
            : SelectJava(resolved.JavaMajor, out _);

        if (java == null)
        {
            problems.Add($"没有找到 Java {resolved.JavaMajor}，请先在设置里指定");
            return null;
        }

        var gameDir = GameDirectoryFor(resolved);
        var account = Account;
        var options = new LaunchOptions
        {
            PlayerName = account is { IsValid: true } ? account.PlayerName : (playerName ?? Settings.PlayerName),
            Uuid = account is { IsValid: true } ? account.Uuid : null,
            AccessToken = account is { IsValid: true } ? account.AccessToken : "0",
            UserType = account is { IsValid: true } ? account.UserType : "legacy",
            JavaPath = java.JavaPath,
            MinMemoryMb = Settings.MinMemoryMb,
            MaxMemoryMb = Settings.MaxMemoryMb,
            ExtraJvmArgs = Settings.ExtraJvmArgs,
            ExtraGameArgs = Settings.ExtraGameArgs,
            WindowWidth = Settings.WindowWidth,
            WindowHeight = Settings.WindowHeight,
            GameDirectory = gameDir,
            LauncherName = "MemeLauncher",
            LauncherVersion = "0.2.0",
        };

        var planner = new LaunchPlanner(Versions.LibrariesDirectory);
        var plan = planner.Build(resolved, options);
        problems.AddRange(plan.Warnings);
        return plan;
    }

    /// <summary>该版本实际使用的游戏目录（考虑版本隔离）。</summary>
    public string GameDirectoryFor(ResolvedVersion version)
    {
        return VersionIsolation.ShouldIsolate(version.Directory, Settings.VersionIsolation)
            ? version.Directory
            : Settings.GameDirectory;
    }

    public async Task<LaunchResult> LaunchAsync(string versionId, string? playerName = null,
        Action<string>? onOutput = null, CancellationToken ct = default)
    {
        var plan = BuildPlan(versionId, out _, out var problems, playerName);
        if (plan == null)
        {
            return new LaunchResult { Started = false, Error = string.Join("; ", problems) };
        }

        var launcher = new GameLauncher();
        if (onOutput != null)
        {
            launcher.OutputReceived += onOutput;
        }

        return await launcher.LaunchAsync(plan, ct).ConfigureAwait(false);
    }

    public Installer CreateInstaller(bool? useMirror = null)
    {
        if (useMirror.HasValue && useMirror.Value != Settings.UseMirror)
        {
            return new Installer(new DownloadClient(useMirror.Value) { MirrorBaseUrl = Settings.MirrorBaseUrl },
                Settings.GameDirectory);
        }

        return new Installer(SharedDownloader, Settings.GameDirectory);
    }
}
