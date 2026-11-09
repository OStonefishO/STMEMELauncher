using System.Diagnostics;
using System.Text.RegularExpressions;
using Aurora.Core.Infrastructure;

namespace Aurora.Core.Java;

public sealed class JavaRuntime
{
    public string JavaPath { get; init; } = string.Empty;
    public string Home { get; init; } = string.Empty;
    public int Major { get; init; }
    public string VersionText { get; init; } = string.Empty;
    public string Vendor { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public bool Is64Bit { get; init; } = true;

    public override string ToString() => $"Java {Major} ({VersionText}) {JavaPath}";
}

public sealed class JavaLocator
{
    private readonly List<string> _extraRoots;

    public JavaLocator(IEnumerable<string>? extraRoots = null)
    {
        _extraRoots = extraRoots?.ToList() ?? new List<string>();
    }

    public IReadOnlyList<JavaRuntime> Scan()
    {
        var found = new Dictionary<string, JavaRuntime>(StringComparer.OrdinalIgnoreCase);

        foreach (var home in EnumerateJavaHomes())
        {
            var runtime = Probe(home);
            if (runtime != null && !found.ContainsKey(runtime.JavaPath))
            {
                found[runtime.JavaPath] = runtime;
            }
        }

        return found.Values
            .OrderByDescending(j => j.Major)
            .ThenBy(j => j.VersionText)
            .ToList();
    }

    /// <summary>为指定 Java 主版本挑一个最合适的运行时；没有精确匹配时退而求其次。</summary>
    public JavaRuntime? Select(int requiredMajor, IReadOnlyList<JavaRuntime>? pool = null)
    {
        var all = pool ?? Scan();
        if (all.Count == 0)
        {
            return null;
        }

        var exact = all.Where(j => j.Major == requiredMajor)
            .OrderByDescending(j => ParseVersion(j.VersionText))
            .ThenByDescending(j => j.Is64Bit)
            .FirstOrDefault();
        if (exact != null)
        {
            return exact;
        }

        // 1.8 及更早不能用高版本 JDK 启动，反之亦然
        if (requiredMajor <= 8)
        {
            return all.Where(j => j.Major == 8).OrderByDescending(j => j.VersionText).FirstOrDefault();
        }

        return all.Where(j => j.Major >= requiredMajor)
            .OrderBy(j => j.Major)
            .ThenByDescending(j => ParseVersion(j.VersionText))
            .FirstOrDefault()
            ?? all.OrderByDescending(j => j.Major).FirstOrDefault();
    }

    private IEnumerable<string> EnumerateJavaHomes()
    {
        foreach (var root in _extraRoots)
        {
            foreach (var home in ExpandRoot(root))
            {
                yield return home;
            }
        }

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            yield return javaHome;
        }

        var locations = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Amazon Corretto"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zulu"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Eclipse Adoptium"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        };

        foreach (var location in locations)
        {
            if (!Directory.Exists(location))
            {
                continue;
            }

            foreach (var sub in SafeDirs(location))
            {
                if (LooksLikeJavaHome(sub))
                {
                    yield return sub;
                }
            }
        }
    }

    private static IEnumerable<string> ExpandRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            yield break;
        }

        if (File.Exists(root))
        {
            var home = Path.GetDirectoryName(root);
            if (home != null)
            {
                yield return home;
            }

            yield break;
        }

        if (!Directory.Exists(root))
        {
            yield break;
        }

        if (LooksLikeJavaHome(root))
        {
            yield return root;
        }

        // 根目录下再找一层，方便直接填 "jdks" 这种父目录
        foreach (var sub in SafeDirs(root))
        {
            if (LooksLikeJavaHome(sub))
            {
                yield return sub;
            }
        }
    }

    private static IEnumerable<string> SafeDirs(string dir)
    {
        try
        {
            return Directory.GetDirectories(dir);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool LooksLikeJavaHome(string dir)
    {
        var exe = Path.Combine(dir, "bin", "javaw.exe");
        if (File.Exists(exe))
        {
            return true;
        }

        return File.Exists(Path.Combine(dir, "bin", "java.exe")) ||
               File.Exists(Path.Combine(dir, "bin", "java"));
    }

    private static JavaRuntime? Probe(string home)
    {
        var exe64 = Path.Combine(home, "bin", "javaw.exe");
        var exe = File.Exists(exe64) ? exe64 : Path.Combine(home, "bin", "java.exe");
        if (!File.Exists(exe))
        {
            exe = Path.Combine(home, "bin", "java");
            if (!File.Exists(exe))
            {
                return null;
            }
        }

        var versionText = ReadReleaseVersion(home) ?? ReadVersionByProcess(exe);
        if (string.IsNullOrEmpty(versionText))
        {
            return null;
        }

        var major = ParseMajor(versionText);
        if (major <= 0)
        {
            return null;
        }

        var release = Path.Combine(home, "release");
        var vendor = string.Empty;
        if (File.Exists(release))
        {
            try
            {
                foreach (var line in File.ReadAllLines(release))
                {
                    if (line.StartsWith("IMPLEMENTOR=", StringComparison.OrdinalIgnoreCase))
                    {
                        vendor = line.Split('=', 2)[1].Trim('"');
                        break;
                    }
                }
            }
            catch
            {
                // 忽略
            }
        }

        var is64 = true;
        // 用 release 里的 OS_ARCH 判定位数，没有就默认按 64 位处理
        if (File.Exists(release))
        {
            try
            {
                var archLine = File.ReadAllLines(release).FirstOrDefault(l => l.StartsWith("OS_ARCH=", StringComparison.OrdinalIgnoreCase));
                if (archLine != null)
                {
                    is64 = archLine.Contains("64", StringComparison.OrdinalIgnoreCase) ||
                           archLine.Contains("aarch64", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // 忽略
            }
        }

        return new JavaRuntime
        {
            JavaPath = exe,
            Home = home,
            Major = major,
            VersionText = versionText,
            Vendor = vendor,
            Source = home,
            Is64Bit = is64,
        };
    }

    private static string? ReadReleaseVersion(string home)
    {
        var release = Path.Combine(home, "release");
        if (!File.Exists(release))
        {
            return null;
        }

        try
        {
            foreach (var line in File.ReadAllLines(release))
            {
                if (line.StartsWith("JAVA_VERSION=", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Split('=', 2)[1].Trim().Trim('"');
                }
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }

    private static string? ReadVersionByProcess(string exe)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, "-version")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process == null)
            {
                return null;
            }

            var text = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            process.WaitForExit(4000);
            var match = Regex.Match(text, "\"(\\d+(?:\\.\\d+)*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }

    public static int ParseMajor(string versionText)
    {
        var match = Regex.Match(versionText, @"(\d+)");
        if (!match.Success)
        {
            return 0;
        }

        var first = int.Parse(match.Groups[1].Value);
        if (first == 1)
        {
            var rest = versionText.Substring(versionText.IndexOf("1.", StringComparison.Ordinal) + 2);
            var minor = Regex.Match(rest, @"(\d+)");
            return minor.Success ? int.Parse(minor.Groups[1].Value) : 8;
        }

        return first;
    }

    private static Version ParseVersion(string text)
    {
        var cleaned = Regex.Replace(text, "[^0-9.]", " ");
        var part = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "0";
        return Version.TryParse(part, out var v) ? v : new Version(0, 0);
    }
}
