using Aurora.Core.Models;

namespace Aurora.Core.Versions;

public sealed class FeatureSet
{
    public bool IsDemoUser { get; init; }
    public bool HasCustomResolution { get; init; }
    public bool HasQuickPlaysSupport { get; init; }
    public bool IsQuickPlaySingleplayer { get; init; }
    public bool IsQuickPlayMultiplayer { get; init; }
    public bool IsQuickPlayRealms { get; init; }
}

/// <summary>按 Mojang 的 rules 语义判断一条库/参数是否在当前平台上生效。</summary>
public static class RuleEvaluator
{
    public static bool IsAllowed(List<Rule>? rules, FeatureSet features)
    {
        if (rules == null || rules.Count == 0)
        {
            return true;
        }

        // 有 rules 时默认拒绝，按顺序匹配覆盖
        var allowed = false;
        foreach (var rule in rules)
        {
            if (!Matches(rule, features))
            {
                continue;
            }

            allowed = string.Equals(rule.Action, "allow", StringComparison.OrdinalIgnoreCase);
        }

        return allowed;
    }

    public static bool Matches(Rule rule, FeatureSet features)
    {
        if (rule.Os != null && !MatchesOs(rule.Os))
        {
            return false;
        }

        if (rule.Features != null)
        {
            foreach (var (key, expected) in rule.Features)
            {
                if (GetFeature(key, features) != expected)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool GetFeature(string key, FeatureSet f) => key switch
    {
        "is_demo_user" => f.IsDemoUser,
        "has_custom_resolution" => f.HasCustomResolution,
        "has_quick_plays_support" => f.HasQuickPlaysSupport,
        "is_quick_play_singleplayer" => f.IsQuickPlaySingleplayer,
        "is_quick_play_multiplayer" => f.IsQuickPlayMultiplayer,
        "is_quick_play_realms" => f.IsQuickPlayRealms,
        _ => false,
    };

    public static string CurrentOsName =>
        OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsMacOS() ? "osx" : "linux";

    public static string CurrentOsArch
    {
        get
        {
            var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
            return arch switch
            {
                System.Runtime.InteropServices.Architecture.X86 => "x86",
                System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                _ => "x86_64",
            };
        }
    }

    /// <summary>1.7 及更早的 natives 分类器里会用 ${arch} 占位（32/64）。</summary>
    public static string ArchBits =>
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
        System.Runtime.InteropServices.Architecture.X86 ? "32" : "64";

    private static bool MatchesOs(OsRule os)
    {
        if (!string.IsNullOrEmpty(os.Name) &&
            !string.Equals(os.Name, CurrentOsName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(os.Arch) &&
            !string.Equals(os.Arch, CurrentOsArch, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(os.Version))
        {
            // os.version 用正则匹配，现实中只有极少数库使用
            try
            {
                var pattern = os.Version;
                if (!System.Text.RegularExpressions.Regex.IsMatch(Environment.OSVersion.Version.ToString(), pattern))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        return true;
    }
}
