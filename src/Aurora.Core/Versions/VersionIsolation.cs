namespace Aurora.Core.Versions;

public static class VersionIsolation
{
    /// <summary>判断某个版本是否应该使用自己的目录作为游戏目录（版本隔离）。</summary>
    public static bool ShouldIsolate(string versionDirectory, string mode)
    {
        switch ((mode ?? "auto").Trim().ToLowerInvariant())
        {
            case "on":
            case "true":
                return true;
            case "off":
            case "false":
                return false;
        }

        // auto：版本目录里出现了玩家数据/模组就说明它需要独立目录
        try
        {
            if (HasFiles(Path.Combine(versionDirectory, "mods"), "*.jar"))
            {
                return true;
            }

            if (HasFiles(Path.Combine(versionDirectory, "config")))
            {
                return true;
            }

            if (Directory.Exists(Path.Combine(versionDirectory, "saves")))
            {
                return true;
            }

            if (File.Exists(Path.Combine(versionDirectory, "options.txt")))
            {
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool HasFiles(string directory, string pattern = "*")
    {
        return Directory.Exists(directory) && Directory.EnumerateFiles(directory, pattern).Any();
    }
}
