using Aurora.Core.Accounts;
using Aurora.Core.Auth;
using Aurora.Core.Configuration;
using Aurora.Core.Java;
using Aurora.Core.Launch;
using Aurora.Core.Models;
using Aurora.Core.Net;
using Aurora.Core.Versions;

namespace Aurora.Core;

/// <summary>把配置、版本仓库、Java 探测、命令行构建、进程启动串起来的门面层。</summary>
public sealed partial class LauncherService
{
    public LauncherService(LauncherSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Versions = new VersionRepository(settings.GameDirectory);
    }

    private DownloadClient? _shared;

    public LauncherSettings Settings { get; }

    public VersionRepository Versions { get; private set; }

    // ---------------- 账号 ----------------

    /// <summary>当前使用的账号。</summary>
    public SavedAccount? ActiveAccount =>
        Settings.Accounts.FirstOrDefault(a => a.Id == Settings.ActiveAccountId)
        ?? Settings.Accounts.FirstOrDefault();

    /// <summary>首次启动时把旧的单一账号迁移成账号列表。</summary>
    public void MigrateAccounts()
    {
        if (Settings.Accounts.Count > 0)
        {
            return;
        }

        var account = new SavedAccount
        {
            Type = "offline",
            Name = string.IsNullOrWhiteSpace(Settings.PlayerName) ? "Steve" : Settings.PlayerName,
        };
        account.Uuid = SavedAccount.NewOffline(account.Name);
        Settings.Accounts.Add(account);
        Settings.ActiveAccountId = account.Id;
        ConfigStore.Save(Settings);
    }

    public void UpsertAccount(SavedAccount account)
    {
        var index = Settings.Accounts.FindIndex(a => a.Id == account.Id);
        account.LastUsed = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (index >= 0)
        {
            Settings.Accounts[index] = account;
        }
        else
        {
            Settings.Accounts.Add(account);
        }

        Settings.ActiveAccountId = account.Id;
        ConfigStore.Save(Settings);
    }

    public void SwitchAccount(string id)
    {
        if (Settings.Accounts.Any(a => a.Id == id))
        {
            Settings.ActiveAccountId = id;
            var account = Settings.Accounts.First(a => a.Id == id);
            account.LastUsed = DateTimeOffset.Now.ToUnixTimeSeconds();
            ConfigStore.Save(Settings);
        }
    }

    public void RemoveAccount(string id)
    {
        Settings.Accounts.RemoveAll(a => a.Id == id);
        if (Settings.ActiveAccountId == id)
        {
            Settings.ActiveAccountId = Settings.Accounts.FirstOrDefault()?.Id ?? string.Empty;
        }

        ConfigStore.Save(Settings);
    }

    /// <summary>当前登录的正版账号（null 表示离线登录）。</summary>
    public AuthSession? Account { get; set; }

    /// <summary>当前登录的外置账号（LittleSkin 等）。</summary>
    public YggdrasilSession? Yggdrasil { get; set; }

    public void RestoreYggdrasil()
    {
        if (!string.IsNullOrEmpty(Settings.YggdrasilToken) && !string.IsNullOrEmpty(Settings.YggdrasilUuid))
        {
            Yggdrasil = new YggdrasilSession
            {
                ApiRoot = Settings.YggdrasilApiRoot,
                AccessToken = Settings.YggdrasilToken,
                ClientToken = Settings.YggdrasilClientToken,
                Uuid = Settings.YggdrasilUuid,
                PlayerName = Settings.YggdrasilName,
            };
        }
    }

    public void SaveYggdrasil(YggdrasilSession? session)
    {
        Yggdrasil = session;
        Settings.YggdrasilToken = session?.AccessToken ?? string.Empty;
        Settings.YggdrasilClientToken = session?.ClientToken ?? string.Empty;
        Settings.YggdrasilUuid = session?.Uuid ?? string.Empty;
        Settings.YggdrasilName = session?.PlayerName ?? string.Empty;
        if (session != null)
        {
            Settings.YggdrasilApiRoot = session.ApiRoot;
        }

        ConfigStore.Save(Settings);
    }

    public YggdrasilAuth CreateYggdrasil(string? apiRoot = null) =>
        new(string.IsNullOrWhiteSpace(apiRoot) ? Settings.YggdrasilApiRoot : apiRoot);

    public AuthlibInjectorInstaller CreateInjectorInstaller() => new();

    /// <summary>
    /// 启动前把外置登录准备好：token 失效就自动 refresh，authlib-injector 缺了就下下来。
    /// 任何一步失败都会返回原因，调用方据此提示玩家重新登录。
    /// </summary>
    public async Task<string?> PrepareYggdrasilAsync(IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (Yggdrasil is not { } session || string.IsNullOrEmpty(session.AccessToken))
        {
            return null;
        }

        var auth = CreateYggdrasil(session.ApiRoot);

        // token 可能早就过期了，先验一下，能用就不动
        var valid = await auth.ValidateAsync(session, ct).ConfigureAwait(false);
        if (!valid)
        {
            progress?.Report("外置登录已过期，正在自动续期…");
            YggdrasilSession refreshed;
            try
            {
                refreshed = await auth.RefreshAsync(session, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return "外置登录已过期，自动续期也失败了（" + ex.Message + "），去「账号」页重新登录一次";
            }

            SaveYggdrasil(refreshed);
            var account = Settings.Accounts.FirstOrDefault(a =>
                a.Type == "yggdrasil" && string.Equals(a.Uuid, refreshed.Uuid, StringComparison.OrdinalIgnoreCase));
            if (account != null)
            {
                account.AccessToken = refreshed.AccessToken;
                account.ClientToken = refreshed.ClientToken;
                account.Name = refreshed.PlayerName;
                ConfigStore.Save(Settings);
            }
        }

        // 没有注入器，游戏里就看不到皮肤站皮肤（会显示成默认皮肤）
        try
        {
            await CreateInjectorInstaller().EnsureAsync(Settings.GameDirectory, progress, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return "authlib-injector 下载失败（" + ex.Message + "），没有它游戏里不会显示皮肤站皮肤";
        }

        return null;
    }

    public DownloadClient SharedDownloader =>
        _shared ??= new DownloadClient(Settings.UseMirror) { MirrorBaseUrl = Settings.MirrorBaseUrl };

    public MicrosoftAuth CreateAuth() => new(Settings.MicrosoftClientId);

    public LoaderInstaller CreateLoaderInstaller() =>
        new(SharedDownloader, Settings.GameDirectory, major => SelectJava(major, out _)?.JavaPath);

    private readonly DownloadManager _downloads = new();

    public DownloadManager CreateDownloadManager() => _downloads;

    public ContentInstaller CreateContentInstaller() => new(SharedDownloader);

    public ModpackInstaller CreateModpackInstaller() => new(SharedDownloader, Settings.GameDirectory);

    public ModrinthClient CreateModrinth() => new(SharedDownloader);

    public UpdateFeed CreateUpdateFeed() => new(SharedDownloader);

    public UpdateChecker CreateUpdateChecker() =>
        new(SharedDownloader, "OStonefishO/STMEMELauncher", Settings.GiteeRepo);

    public SelfUpdater CreateSelfUpdater() => new(Settings.GiteeRepo);

    public Skins.OfflineSkinService CreateOfflineSkinService() => new(SharedDownloader);

    public CurseForgeClient CreateCurseForge() => new(Settings.CurseForgeApiKey);

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

    /// <summary>切换游戏目录（多 .minecraft 文件夹）。</summary>
    public void SetGameDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new InvalidOperationException("目录不存在: " + directory);
        }

        Settings.GameDirectory = directory;
        ConfigStore.Save(Settings);
        Versions = new VersionRepository(directory);
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
        var active = ActiveAccount;

        string player;
        string? uuid;
        string token;
        string userType;
        string? injector = null;
        string? apiRoot = null;

        if (active is { Type: "yggdrasil" } && !string.IsNullOrEmpty(active.AccessToken))
        {
            player = active.Name;
            uuid = active.Uuid;
            token = active.AccessToken;
            userType = "mojang";
            apiRoot = string.IsNullOrEmpty(active.ApiRoot) ? Settings.YggdrasilApiRoot : active.ApiRoot;
            var jar = Path.Combine(Settings.GameDirectory, "authlib-injector.jar");
            if (File.Exists(jar))
            {
                injector = jar;
            }
            else
            {
                // 没有注入器就直接别启动：否则进游戏是默认皮肤，玩家会以为皮肤丢了
                problems.Add("外置登录需要 authlib-injector（没下下来），请重新登录外置账号");
                return null;
            }
        }
        else if (active is { Type: "microsoft" } && !string.IsNullOrEmpty(active.AccessToken))
        {
            player = active.Name;
            uuid = active.Uuid;
            token = active.AccessToken;
            userType = "msa";
        }
        else
        {
            player = playerName ?? active?.Name ?? Settings.PlayerName;
            uuid = string.IsNullOrEmpty(active?.Uuid) ? SavedAccount.NewOffline(player) : active!.Uuid;
            token = "0";
            userType = "legacy";
        }

        var options = new LaunchOptions
        {
            PlayerName = player,
            Uuid = uuid,
            AccessToken = token,
            UserType = userType,
            AuthlibInjectorPath = injector,
            YggdrasilApiRoot = apiRoot,
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
        return IsIsolated(version) ? version.Directory : Settings.GameDirectory;
    }

    /// <summary>这个版本要不要隔离：先看单版本开关，没设置再按全局策略判断。</summary>
    public bool IsIsolated(ResolvedVersion version)
    {
        if (Settings.IsolatedVersions.TryGetValue(version.Id, out var forced))
        {
            return forced;
        }

        return VersionIsolation.ShouldIsolate(version.Directory, Settings.VersionIsolation);
    }

    /// <summary>单版本的设置：null 表示跟随全局。</summary>
    public bool? IsolationOverride(string versionId) =>
        Settings.IsolatedVersions.TryGetValue(versionId, out var forced) ? forced : null;

    /// <summary>设置某个版本是否隔离，开的时候顺手把目录建出来。</summary>
    public void SetIsolation(ResolvedVersion version, bool isolated)
    {
        Settings.IsolatedVersions[version.Id] = isolated;
        ConfigStore.Save(Settings);

        if (!isolated)
        {
            return;
        }

        try
        {
            foreach (var name in new[] { "mods", "config", "saves", "resourcepacks", "shaderpacks", "crash-reports" })
            {
                Directory.CreateDirectory(Path.Combine(version.Directory, name));
            }
        }
        catch
        {
            // 目录建不出来也不影响启动
        }
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
