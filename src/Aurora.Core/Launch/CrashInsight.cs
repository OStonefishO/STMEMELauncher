using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Aurora.Core.Launch;

public sealed class CrashCause
{
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

/// <summary>一次崩溃的分析结果：一句话结论 + 可能原因 + 建议 + 原始报告。</summary>
public sealed class CrashInsight
{
    public string Headline { get; init; } = "游戏异常退出";
    public string Verdict { get; init; } = string.Empty;
    public string Category { get; init; } = "未知";
    public bool Confident { get; init; }
    public IReadOnlyList<CrashCause> Causes { get; init; } = Array.Empty<CrashCause>();
    public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();
    public string? GameCrashReportPath { get; init; }
    public string ReportText { get; init; } = string.Empty;
    public int ExitCode { get; init; }
    public DateTimeOffset AnalyzedAt { get; init; } = DateTimeOffset.Now;

    public string ToPlainText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("MEME 启动器 · 游戏崩溃报告");
        sb.AppendLine("时间：" + AnalyzedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("结论：" + Headline);
        if (!string.IsNullOrWhiteSpace(Verdict))
        {
            sb.AppendLine("说明：" + Verdict);
        }

        sb.AppendLine("类别：" + Category + (Confident ? "（已确认）" : "（推测）"));
        sb.AppendLine("退出码：" + ExitCode);
        if (!string.IsNullOrEmpty(GameCrashReportPath))
        {
            sb.AppendLine("游戏崩溃报告：" + GameCrashReportPath);
        }

        if (Causes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("可能的原因：");
            foreach (var cause in Causes)
            {
                sb.AppendLine("  · " + cause.Title);
                if (!string.IsNullOrWhiteSpace(cause.Detail))
                {
                    sb.AppendLine("    " + cause.Detail);
                }
            }
        }

        if (Suggestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("建议：");
            foreach (var tip in Suggestions)
            {
                sb.AppendLine("  · " + tip);
            }
        }

        sb.AppendLine();
        sb.AppendLine("================ 原始报告 / 日志 ================");
        sb.AppendLine(ReportText);
        return sb.ToString();
    }
}

/// <summary>从游戏崩溃报告 + 启动器抓到的日志里判断“为什么崩了”。</summary>
public static class CrashInspector
{
    private sealed class Rule
    {
        public string Category = "其他";
        public string Headline = string.Empty;
        public string Verdict = string.Empty;
        public string[] Patterns = Array.Empty<string>();
        public string[] Suggestions = Array.Empty<string>();
        public bool Strong;
    }

    private static readonly Rule[] Rules =
    {
        new()
        {
            Category = "Java 版本",
            Headline = "Java 版本不对，游戏起不来",
            Verdict = "模组或游戏本体是用更新的 Java 编译的，这次用的 Java 太旧（或太新）。",
            Patterns = new[]
            {
                @"UnsupportedClassVersionError",
                @"has been compiled by a more recent version of the Java Runtime",
                @"class file version \d+",
                @"requires Java \d+",
                @"Unsupported Java",
            },
            Suggestions = new[]
            {
                "打开「设置 → Java 路径」，换成 1.17 及以上版本时对应的 Java（1.17 用 17，1.20+ 用 17 或 21）。",
                "偷懒一点：把 Java 路径清空，让启动器自动挑版本。",
            },
            Strong = true,
        },
        new()
        {
            Category = "内存",
            Headline = "内存不够，Java 撑不住了",
            Verdict = "游戏在加载资源或进世界时把分配的内存用完了。",
            Patterns = new[]
            {
                @"OutOfMemoryError",
                @"Java heap space",
                @"GC overhead limit exceeded",
                @"Metaspace",
            },
            Suggestions = new[]
            {
                "把「设置 → 最大内存」调大一档（比如 4G 起步，大整合包 6~8G），别超过物理内存的一半。",
                "先关掉光影 / 高清材质 / 远景模组再试一次。",
            },
            Strong = true,
        },
        new()
        {
            Category = "模组",
            Headline = "模组之间冲突了（Mixin 注入失败）",
            Verdict = "有两个模组都想改同一段代码，改崩了。",
            Patterns = new[]
            {
                @"Mixin apply failed",
                @"Critical injection failure",
                @"MixinApplyError",
                @"MixinTransformerError",
                @"mixin.*(failed|error)",
            },
            Suggestions = new[]
            {
                "想想崩溃前最后装的是哪个模组，先把它删掉再启动。",
                "还不行就二分法：先停一半模组，能进就说明问题在停掉的那一半里。",
            },
            Strong = true,
        },
        new()
        {
            Category = "模组",
            Headline = "缺少前置模组，或者模组版本对不上",
            Verdict = "某个模组要的前置没装，或者装的版本和游戏版本不匹配。",
            Patterns = new[]
            {
                @"Missing or unsupported mandatory dependencies",
                @"which is missing",
                @"requires .* but it is not installed",
                @"NoSuchMethodError",
                @"NoClassDefFoundError",
                @"ClassNotFoundException",
                @"Failed to load mod",
                @"ModLoadingException",
                @"LoadingFailedException",
            },
            Suggestions = new[]
            {
                "按日志里提示的模组名去补前置；注意前置也要下对应游戏版本的。",
                "确认所有模组都是同一个游戏版本（1.20.1 的模组不能丢进 1.21）。",
            },
        },
        new()
        {
            Category = "模组",
            Headline = "同一个模组装了两份",
            Verdict = "mods 文件夹里有重复的模组文件，游戏不知道听谁的。",
            Patterns = new[]
            {
                @"Duplicate mod",
                @"duplicate mod id",
                @"Found a duplicate",
            },
            Suggestions = new[]
            {
                "打开 mods 文件夹，把名字一样、只是版本号不同的旧文件删掉。",
            },
            Strong = true,
        },
        new()
        {
            Category = "显卡",
            Headline = "显卡 / OpenGL 初始化失败",
            Verdict = "游戏没能创建显示窗口，通常是显卡驱动或显卡不支持当前的 OpenGL。",
            Patterns = new[]
            {
                @"GLFW error",
                @"Failed to create window",
                @"Pixel format not accelerated",
                @"WGL",
                @"Could not initialize OpenGL",
                @"OpenGL 3\.2",
            },
            Suggestions = new[]
            {
                "更新显卡驱动（NVIDIA / AMD 官网，别用驱动精灵）。",
                "笔记本切到独显；台式机确认显示器接的是显卡不是主板。",
                "开了光影的话先关掉，再试一次。",
            },
            Strong = true,
        },
        new()
        {
            Category = "网络",
            Headline = "资源没下全，游戏加载到一半就断了",
            Verdict = "启动器下载游戏文件时网络断了或缺文件。",
            Patterns = new[]
            {
                @"Connection reset",
                @"Read timed out",
                @"SocketTimeout",
                @"Unable to download",
                @"Failed to download",
                @"Server returned HTTP 5",
                @"UnknownHostException",
            },
            Suggestions = new[]
            {
                "在「内容 → 安装版本」里对同一个版本重装一次，会补齐缺的文件。",
                "换成国内镜像再下一次。",
            },
        },
        new()
        {
            Category = "服务端",
            Headline = "端口被占用了",
            Verdict = "开服用的端口已经有别的程序在用。",
            Patterns = new[]
            {
                @"Address already in use",
                @"BindException",
            },
            Suggestions = new[]
            {
                "把端口换成 25566 之类的其它数字再开服。",
            },
            Strong = true,
        },
        new()
        {
            Category = "权限",
            Headline = "文件没权限读写",
            Verdict = "游戏目录被占用或没有写权限（常见于放在 C 盘根目录 / 被安全软件拦）。",
            Patterns = new[]
            {
                @"Access is denied",
                @"UnauthorizedAccessException",
                @"Permission denied",
                @"拒绝访问",
            },
            Suggestions = new[]
            {
                "把游戏目录换到 D 盘这类普通目录，别放 C:\\Program Files。",
                "把启动器和游戏目录加进杀毒软件白名单。",
            },
        },
        new()
        {
            Category = "登录",
            Headline = "登录状态失效了",
            Verdict = "服务器没认出来这个账号，可能是登录过期或被踢。",
            Patterns = new[]
            {
                @"Invalid session",
                @"Failed to authenticate",
                @"invalid credentials",
            },
            Suggestions = new[]
            {
                "回「账号」页重新登录一次再启动。",
            },
        },
    };

    public static CrashInsight Analyze(
        string gameDirectory,
        int exitCode,
        IReadOnlyList<string> logLines,
        DateTimeOffset launchStartedAt)
    {
        var reportPath = FindCrashReport(gameDirectory, launchStartedAt);
        var reportText = string.Empty;
        if (reportPath != null)
        {
            try
            {
                reportText = File.ReadAllText(reportPath);
            }
            catch
            {
                reportText = string.Empty;
            }
        }

        var tail = logLines.Count > 220
            ? logLines.Skip(logLines.Count - 220).ToList()
            : logLines.ToList();

        var haystack = reportText + "\n" + string.Join("\n", tail);

        var causes = new List<CrashCause>();
        var suggestions = new List<string>();
        string? headline = null;
        string? verdict = null;
        var category = "未知";
        var confident = false;

        foreach (var rule in Rules)
        {
            if (!Matches(rule, haystack, out var evidence))
            {
                continue;
            }

            if (headline == null)
            {
                headline = rule.Headline;
                verdict = rule.Verdict;
                category = rule.Category;
                confident = rule.Strong && !string.IsNullOrWhiteSpace(reportPath);
            }

            causes.Add(new CrashCause
            {
                Title = rule.Headline,
                Detail = string.IsNullOrWhiteSpace(evidence) ? rule.Verdict : evidence,
            });

            foreach (var tip in rule.Suggestions)
            {
                if (!suggestions.Contains(tip))
                {
                    suggestions.Add(tip);
                }
            }
        }

        // 崩溃报告自己的描述往往比关键字更准，放最前面
        var description = ExtractReportDescription(reportText);
        if (!string.IsNullOrWhiteSpace(description))
        {
            causes.Insert(0, new CrashCause
            {
                Title = "游戏自己写的崩溃原因",
                Detail = description,
            });

            if (headline == null)
            {
                headline = FirstLine(description);
                verdict = "这段是 Minecraft 自己写进崩溃报告的结论。";
                category = "游戏内异常";
                confident = true;
            }
        }

        var causedBy = ExtractCausedBy(reportText);
        if (!string.IsNullOrWhiteSpace(causedBy))
        {
            causes.Add(new CrashCause { Title = "最底层异常", Detail = causedBy });
        }

        if (headline == null)
        {
            headline = exitCode == 0
                ? "游戏正常退出了"
                : $"游戏异常退出（退出码 {exitCode}），日志里没有明确的报错";
            verdict = "可能是内存、驱动这类外部原因，也可能是模组在很早期就崩了。先看下面的日志尾巴。";
            category = "未知";
            suggestions.Add("把这份报告导出发给作者，能更快定位。");
            suggestions.Add("顺手更新一下显卡驱动，很多「莫名闪退」都是它。");
        }

        if (suggestions.Count > 5)
        {
            suggestions = suggestions.Take(5).ToList();
        }

        var logTailText = tail.Count == 0 ? "（启动器没有抓到日志输出）" : string.Join("\n", tail);
        var body = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(reportText))
        {
            body.AppendLine("=== crash-reports 里的崩溃报告 ===");
            body.AppendLine(reportText.TrimEnd());
            body.AppendLine();
        }

        body.AppendLine("=== 启动器抓到的最后 " + tail.Count + " 行日志 ===");
        body.AppendLine(logTailText);

        return new CrashInsight
        {
            Headline = headline,
            Verdict = verdict ?? string.Empty,
            Category = category,
            Confident = confident,
            Causes = causes,
            Suggestions = suggestions,
            GameCrashReportPath = reportPath,
            ReportText = body.ToString(),
            ExitCode = exitCode,
        };
    }

    private static bool Matches(Rule rule, string haystack, out string evidence)
    {
        evidence = string.Empty;
        foreach (var pattern in rule.Patterns)
        {
            Match match;
            try
            {
                match = Regex.Match(haystack, pattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                continue;
            }

            if (!match.Success)
            {
                continue;
            }

            // 抓到关键字所在的那一行（再带上下一条，通常就是具体的模组名），作为证据展示
            var line = LineAround(haystack, match.Index, out var nextLine);
            evidence = string.IsNullOrWhiteSpace(nextLine) ? line : line + " ｜ " + nextLine;
            evidence = evidence.Length > 220 ? evidence[..220] + "…" : evidence;
            return true;
        }

        return false;
    }

    private static string LineAround(string text, int index, out string nextLine)
    {
        nextLine = string.Empty;
        var start = text.LastIndexOf('\n', Math.Max(0, index - 1));
        start = start < 0 ? 0 : start + 1;

        int end;
        try
        {
            end = text.IndexOf('\n', index);
            end = end < 0 ? text.Length : end;
            var current = text[start..end].Trim();

            var nextStart = end + 1;
            if (nextStart < text.Length)
            {
                var nextEnd = text.IndexOf('\n', nextStart);
                nextEnd = nextEnd < 0 ? text.Length : nextEnd;
                var candidate = text[nextStart..nextEnd].Trim();
                if (candidate.Length is > 3 and < 160 &&
                    (candidate.Contains("Mod ID", StringComparison.OrdinalIgnoreCase) ||
                     candidate.StartsWith("\tat ", StringComparison.Ordinal) == false))
                {
                    nextLine = candidate;
                }
            }

            return current;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? FindCrashReport(string gameDirectory, DateTimeOffset launchedAt)
    {
        try
        {
            var dir = Path.Combine(gameDirectory, "crash-reports");
            if (!Directory.Exists(dir))
            {
                return null;
            }

            var threshold = launchedAt.UtcDateTime.AddMinutes(-1);
            return Directory.GetFiles(dir, "*.txt")
                .Select(path => new FileInfo(path))
                .Where(info => info.LastWriteTimeUtc >= threshold)
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .Select(info => info.FullName)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractReportDescription(string report)
    {
        if (string.IsNullOrWhiteSpace(report))
        {
            return null;
        }

        var lines = report.Split('\n');
        var description = lines.FirstOrDefault(l =>
            l.TrimStart().StartsWith("Description:", StringComparison.OrdinalIgnoreCase));
        if (description != null)
        {
            return description.Trim();
        }

        var title = lines.FirstOrDefault(l => l.Contains("The game crashed whilst", StringComparison.OrdinalIgnoreCase));
        return title?.Trim();
    }

    private static string? ExtractCausedBy(string report)
    {
        if (string.IsNullOrWhiteSpace(report))
        {
            return null;
        }

        var match = Regex.Match(report, @"Caused by: ([^\r\n]{10,200})");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n')[0].Trim();
        if (line.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
        {
            line = line["Description:".Length..].Trim();
        }

        return line.Length > 90 ? line[..90] + "…" : line;
    }
}
