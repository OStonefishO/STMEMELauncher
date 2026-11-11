using System.Text.Json;
using Aurora.Core.Infrastructure;

namespace Aurora.Core.Configuration;

public sealed class LauncherSettings
{
    /// <summary>.minecraft 目录。</summary>
    public string GameDirectory { get; set; } = string.Empty;

    /// <summary>离线登录用的玩家名。</summary>
    public string PlayerName { get; set; } = "Steve";

    /// <summary>留空表示自动挑选。</summary>
    public string JavaPath { get; set; } = string.Empty;

    public int MinMemoryMb { get; set; } = 512;

    public int MaxMemoryMb { get; set; } = 4096;

    /// <summary>窗口宽高，0 表示交给游戏默认。</summary>
    /// <summary>启动方式：window（窗口）/ borderless（窗口化全屏）/ fullscreen（独占全屏）</summary>
    public string LaunchMode { get; set; } = "window";
    public int WindowWidth { get; set; }

    public int WindowHeight { get; set; }

    public string ExtraJvmArgs { get; set; } = string.Empty;

    public string ExtraGameArgs { get; set; } = string.Empty;

    /// <summary>下载时走国内镜像（BMCLAPI）。</summary>
    public bool UseMirror { get; set; } = true;

    /// <summary>保存的账号列表。</summary>
    public List<Accounts.SavedAccount> Accounts { get; set; } = new();

    /// <summary>当前使用的账号 Id。</summary>
    public string ActiveAccountId { get; set; } = string.Empty;

    /// <summary>外置登录（Yggdrasil）服务器地址，默认 LittleSkin。</summary>
    public string YggdrasilApiRoot { get; set; } = "https://littleskin.cn/api/yggdrasil";

    public string YggdrasilEmail { get; set; } = string.Empty;

    public string YggdrasilToken { get; set; } = string.Empty;

    public string YggdrasilClientToken { get; set; } = string.Empty;

    public string YggdrasilUuid { get; set; } = string.Empty;

    public string YggdrasilName { get; set; } = string.Empty;

    /// <summary>当前选中的版本 id（安装 mod 的目标）。</summary>
    public string SelectedVersionId { get; set; } = string.Empty;

    /// <summary>用过的游戏目录（版本管理页的文件夹下拉）。</summary>
    public List<string> KnownGameDirs { get; set; } = new();

    /// <summary>每次安装内容前询问装到哪个目录。</summary>
    public bool AskInstallFolder { get; set; } = true;

    /// <summary>内容安装时上次选择的目录。</summary>
    public string LastInstallFolder { get; set; } = string.Empty;

    /// <summary>Gitee 镜像仓库（owner/repo），留空关闭；用于国内更新下载。</summary>
    public string GiteeRepo { get; set; } = "stonefish8/STMEMELauncher";

    /// <summary>CurseForge API key（console.curseforge.com 免费申请），留空则只用 Modrinth。</summary>
    public string CurseForgeApiKey { get; set; } = string.Empty;

    /// <summary>自建镜像站地址（例如 http://127.0.0.1:8123）。留空则直接用 BMCLAPI。</summary>
    public string MirrorBaseUrl { get; set; } = string.Empty;

    /// <summary>微软登录用的 Azure 应用 ClientId（没有就只用离线登录）。</summary>
    public string MicrosoftClientId { get; set; } = "52fb2aba-9fa1-4605-be0d-431776c0880d";

    /// <summary>版本隔离：on（默认，总是隔离，新装的版本自动隔离）/ auto（按目录内容判断）/ off（共用根目录）。</summary>
    public string VersionIsolation { get; set; } = "on";

    /// <summary>单个版本的隔离开关（true=强制隔离，false=强制共用全局），没记录的跟随上面的全局设置。</summary>
    public Dictionary<string, bool> IsolatedVersions { get; set; } = new();

    /// <summary>主页 3D 方块岛的群系：0 主世界 / 1 下界 / 2 末地。</summary>
    /// <summary>内容页要不要把英文简介和标签翻成中文</summary>
    public bool TranslateContent { get; set; } = true;

    /// <summary>界面布局：vivid（新版动感）/ classic（经典旧版）。</summary>
    public string UiLayout { get; set; } = "vivid";

    /// <summary>新版布局的主题色相偏移（-160..160）。</summary>
    public int UiHue { get; set; }

    /// <summary>界面音效音量（0..100），0 表示静音。</summary>
    public int UiVolume { get; set; } = 55;

    /// <summary>翻译用的大模型 Key（OpenAI 兼容，例如 DeepSeek）；留空就用免费接口</summary>
    public string TranslateApiKey { get; set; } = string.Empty;

    public string TranslateBaseUrl { get; set; } = "https://api.deepseek.com/v1";

    public string TranslateModel { get; set; } = "deepseek-chat";

    public int HeroBiome { get; set; }

    /// <summary>每个版本的备注（整合包作者写给玩家看的说明），键是版本 id。</summary>
    public Dictionary<string, string> VersionNotes { get; set; } = new();

    /// <summary>额外的 Java 搜索目录（例如自己装的 JDK 根目录）。</summary>
    public List<string> ExtraJavaPaths { get; set; } = new();
}

public static class ConfigStore
{
    public static string RootDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MEMELauncher");

    private static string LegacyDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuroraLauncher");

    private static void MigrateLegacy()
    {
        try
        {
            if (Directory.Exists(RootDirectory) || !Directory.Exists(LegacyDirectory))
            {
                return;
            }

            Directory.CreateDirectory(RootDirectory);
            foreach (var file in Directory.GetFiles(LegacyDirectory))
            {
                File.Copy(file, Path.Combine(RootDirectory, Path.GetFileName(file)), overwrite: false);
            }
        }
        catch
        {
            // 迁移失败不影响使用
        }
    }

    public static string ConfigPath => Path.Combine(RootDirectory, "config.json");

    public static LauncherSettings Load()
    {
        MigrateLegacy();
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var settings = Json.Deserialize<LauncherSettings>(json);
                if (settings != null)
                {
                    settings.GameDirectory = NormalizeOrAutoDetect(settings.GameDirectory);
                    if (settings.MaxMemoryMb <= 0)
                    {
                        settings.MaxMemoryMb = 4096;
                    }

                    return settings;
                }
            }
        }
        catch
        {
            // 配置损坏时退回默认值
        }

        var fresh = new LauncherSettings
        {
            GameDirectory = AutoDetectGameDirectory(),
            MaxMemoryMb = DefaultMaxMemory(),
        };
        Save(fresh);
        return fresh;
    }

    public static void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(RootDirectory);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(settings, Json.Options));
    }

    private static string NormalizeOrAutoDetect(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                var full = Path.GetFullPath(path);
                if (Directory.Exists(full))
                {
                    return full;
                }
            }
            catch
            {
                // 忽略非法路径
            }
        }

        return AutoDetectGameDirectory();
    }

    /// <summary>在常见位置里挑一个版本最多的 .minecraft 目录。</summary>
    public static string AutoDetectGameDirectory()
    {
        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"),
            Path.Combine(Environment.CurrentDirectory, ".minecraft"),
        };

        foreach (var drive in new[] { "D", "E", "F" })
        {
            candidates.Add($"{drive}:\\PCL2\\.minecraft");
            candidates.Add($"{drive}:\\\u6e38\u620f\\\\.minecraft");
        }

        var best = candidates
            .Where(Directory.Exists)
            .Select(p => (Path: p, Score: CountVersions(p)))
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return best.Path ?? Path.Combine(Environment.CurrentDirectory, ".minecraft");
    }

    private static int CountVersions(string gameDir)
    {
        try
        {
            var versions = Path.Combine(gameDir, "versions");
            return Directory.Exists(versions) ? Directory.GetDirectories(versions).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static int DefaultMaxMemory()
    {
        try
        {
            var bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var mb = (int)(bytes / 1024 / 1024);
            return Math.Clamp(mb / 2, 2048, 16384);
        }
        catch
        {
            return 4096;
        }
    }
}
