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

    /// <summary>启动器自己装 Java 的位置（和游戏的 runtime 分开，删游戏不会把 Java 弄没）。</summary>
    public static string JavaRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MEMELauncher", "runtime");

    public Aurora.Core.Java.JavaInstaller CreateJavaInstaller() => new(SharedDownloader, JavaRoot);

    /// <summary>
    /// 启动前补齐这个版本缺的库文件（Forge/NeoForge 的 bootstraplauncher、securejarhandler 这些）。
    /// 返回补不上的文件列表，空表示齐了。
    /// </summary>
    /// <summary>从 versions\xxx 反推游戏根目录（assets、libraries 都在那一层）。</summary>
    private static string? GameRootOf(string? versionDirectory)
    {
        if (string.IsNullOrWhiteSpace(versionDirectory))
        {
            return null;
        }

        var versionsDir = Path.GetDirectoryName(versionDirectory.TrimEnd(Path.DirectorySeparatorChar));
        return string.IsNullOrEmpty(versionsDir) ? null : Path.GetDirectoryName(versionsDir);
    }

    /// <summary>
    /// 补齐版本目录里的基础配置：常用文件夹、options.txt、游戏根目录的 launcher_profiles.json。
    /// 缺这些不影响启动器，但不少模组 / 加载器安装器会因此报错，先垫上更省事。
    /// </summary>
    public List<string> EnsureConfigFiles(ResolvedVersion version)
    {
        var created = new List<string>();

        try
        {
            var gameDir = string.IsNullOrWhiteSpace(version.Directory)
                ? Settings.GameDirectory
                : version.Directory;

            var root = GameRootOf(version.Directory) ?? Settings.GameDirectory;

            foreach (var folder in new[] { "mods", "config", "saves", "resourcepacks", "shaderpacks", "logs" })
            {
                var path = Path.Combine(gameDir, folder);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    created.Add(folder + "/");
                }
            }

            var options = Path.Combine(gameDir, "options.txt");
            if (!File.Exists(options))
            {
                File.WriteAllText(options,
                    "version:3955\n" +
                    "lang:zh_cn\n" +
                    "renderDistance:12\n" +
                    "graphicsMode:1\n" +
                    "guiScale:0\n" +
                    "fov:0.0\n" +
                    "maxFps:120\n" +
                    "pauseOnLostFocus:true\n");
                created.Add("options.txt");
            }

            var profiles = Path.Combine(root, "launcher_profiles.json");
            if (!File.Exists(profiles))
            {
                File.WriteAllText(profiles,
                    "{\n  \"profiles\": {},\n  \"settings\": {},\n  \"version\": 3\n}\n");
                created.Add("launcher_profiles.json");
            }
        }
        catch
        {
            // 补配置失败不影响启动
        }

        return created;
    }

    /// <summary>
    /// 启动前体检：assets 缺不缺，缺就自动补齐（补完再启动）。
    /// 返回补了多少个文件，0 表示本来就齐。
    /// </summary>
    public async Task<int> EnsureAssetsAsync(ResolvedVersion version, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var root = GameRootOf(version.Directory) ?? Settings.GameDirectory;
        var installer = CreateInstaller(root);

        var missing = installer.CountMissingAssets(version);
        if (missing <= 0)
        {
            return 0;
        }

        progress?.Report($"游戏资源缺 {missing} 个，正在补齐…");
        var detail = new Progress<InstallProgress>(p => progress?.Report($"[{p.Stage}] {p.Message} · {p.Percent:0}%"));
        return await installer.RepairAsync(version, detail, ct);
    }

    public async Task<List<string>> EnsureLibrariesAsync(ResolvedVersion version, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var missing = new List<string>();
        var features = new Aurora.Core.Versions.FeatureSet();

        foreach (var lib in version.Libraries)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (!Aurora.Core.Versions.RuleEvaluator.IsAllowed(lib.Rules, features))
                {
                    continue;
                }

                // 只有 natives 的库不参与 classpath
                if (lib.Natives != null && lib.Downloads?.Artifact == null)
                {
                    continue;
                }

                var path = Aurora.Core.Versions.MavenHelper.ResolveArtifactPath(lib, Versions.LibrariesDirectory);
                if (string.IsNullOrEmpty(path) || File.Exists(path))
                {
                    continue;
                }

                var url = lib.Downloads?.Artifact?.Url;
                if (string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(lib.Url))
                {
                    var coordinate = Aurora.Core.Versions.MavenHelper.Parse(lib.Name);
                    if (coordinate != null)
                    {
                        url = lib.Url!.TrimEnd('/') + "/" + coordinate.RelativePath.Replace('\\', '/');
                    }
                }

                if (string.IsNullOrEmpty(url))
                {
                    missing.Add(path);
                    continue;
                }

                progress?.Report("补下载 " + Path.GetFileName(path));
                await SharedDownloader.DownloadFileAsync(url, path, ct: ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                missing.Add(lib.Name ?? "未知库");
            }
        }

        return missing;
    }

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

    /// <summary>这个版本单独指定的 java.exe（没指定返回 null）。</summary>
    public string? VersionJavaPath(string versionId)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            return null;
        }

        return Settings.VersionJavaPaths.TryGetValue(versionId, out var path) && !string.IsNullOrWhiteSpace(path)
            ? path
            : null;
    }

    /// <summary>给版本单独指定 java.exe；传 null / 空就恢复自动。</summary>
    public void SetVersionJavaPath(string versionId, string? javaPath)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(javaPath))
        {
            Settings.VersionJavaPaths.Remove(versionId);
        }
        else
        {
            Settings.VersionJavaPaths[versionId] = javaPath!;
        }

        Aurora.Core.Configuration.ConfigStore.Save(Settings);
    }

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
        roots.Add(JavaRoot);
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
        roots.Add(JavaRoot);

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
            LaunchMode = Settings.LaunchMode,
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

    /// <summary>给指定游戏目录造一个安装器（版本页可以修别的目录里的版本）。</summary>
    public Installer CreateInstaller(string? gameDirectory)
    {
        var dir = string.IsNullOrWhiteSpace(gameDirectory) ? Settings.GameDirectory : gameDirectory!;
        return new Installer(new DownloadClient(Settings.UseMirror) { MirrorBaseUrl = Settings.MirrorBaseUrl }, dir);
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
