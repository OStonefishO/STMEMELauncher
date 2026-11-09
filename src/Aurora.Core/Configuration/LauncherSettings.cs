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
    public int WindowWidth { get; set; }

    public int WindowHeight { get; set; }

    public string ExtraJvmArgs { get; set; } = string.Empty;

    public string ExtraGameArgs { get; set; } = string.Empty;

    /// <summary>下载时走国内镜像（BMCLAPI）。</summary>
    public bool UseMirror { get; set; } = true;

    /// <summary>自建镜像站地址（例如 http://127.0.0.1:8123）。留空则直接用 BMCLAPI。</summary>
    public string MirrorBaseUrl { get; set; } = string.Empty;

    /// <summary>微软登录用的 Azure 应用 ClientId（没有就只用离线登录）。</summary>
    public string MicrosoftClientId { get; set; } = string.Empty;

    /// <summary>版本隔离：auto（默认，按目录内容判断）/ on / off。</summary>
    public string VersionIsolation { get; set; } = "auto";

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
