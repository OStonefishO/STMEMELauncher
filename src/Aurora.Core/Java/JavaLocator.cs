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

        // PATH 里的 java（很多玩家是解压版，直接加了环境变量）
        foreach (var fromPath in FromPath())
        {
            yield return fromPath;
        }

        foreach (var location in KnownRoots())
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

        // 启动器自带的运行时（官方启动器、PCL2、HMCL 下载的 Java 都在这种目录里）
        foreach (var runtimeRoot in RuntimeRoots())
        {
            foreach (var home in FindUnder(runtimeRoot, 5))
            {
                yield return home;
            }
        }

        // 最后再兜一层：Program Files 下两级里找 bin\java.exe
        foreach (var broad in ProgramFilesRoots())
        {
            foreach (var home in FindUnder(broad, 2))
            {
                yield return home;
            }
        }
    }

    /// <summary>PATH 里的 java.exe（解析符号链接前先看 bin 目录）。</summary>
    private static IEnumerable<string> FromPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var results = new List<string>();

        foreach (var part in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var dir = part.Trim().Trim('"');
            if (string.IsNullOrEmpty(dir))
            {
                continue;
            }

            try
            {
                if (!File.Exists(Path.Combine(dir, "javaw.exe")) && !File.Exists(Path.Combine(dir, "java.exe")))
                {
                    continue;
                }

                var home = Path.GetDirectoryName(dir.TrimEnd('\\'));
                if (!string.IsNullOrEmpty(home) && LooksLikeJavaHome(home) && !results.Contains(home))
                {
                    results.Add(home);
                }
            }
            catch
            {
                // 忽略坏的 PATH 项
            }
        }

        return results;
    }

    private static IEnumerable<string> KnownRoots()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return Path.Combine(programFiles, "Java");
        yield return Path.Combine(programFiles, "Eclipse Adoptium");
        yield return Path.Combine(programFiles, "Eclipse Foundation");
        yield return Path.Combine(programFiles, "Amazon Corretto");
        yield return Path.Combine(programFiles, "Zulu");
        yield return Path.Combine(programFiles, "BellSoft");
        yield return Path.Combine(programFiles, "Semeru");
        yield return Path.Combine(programFiles, "Microsoft");
        yield return Path.Combine(programFiles, "JetBrains");
        yield return Path.Combine(programFiles, "Android", "Android Studio", "jbr");
        yield return Path.Combine(programFilesX86, "Java");
        yield return Path.Combine(programFilesX86, "Zulu");
        yield return Path.Combine(localAppData, "Programs", "Eclipse Adoptium");
        yield return Path.Combine(localAppData, "Programs");
        yield return Path.Combine(localAppData, "JetBrains", "Toolbox", "apps");
    }

    /// <summary>各大启动器把 Java 下在自己目录里的位置。</summary>
    private static IEnumerable<string> RuntimeRoots()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return Path.Combine(appData, ".minecraft", "runtime");
        yield return Path.Combine(appData, ".minecraft", "jre");
        yield return Path.Combine(localAppData, "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe",
            "LocalCache", "Local", "runtime");
        yield return Path.Combine(localAppData, "Programs", "Minecraft Launcher", "runtime");
    }

    private static IEnumerable<string> ProgramFilesRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    }

    /// <summary>在目录里往下找 bin\java(w).exe，最多 maxDepth 层，带访问上限防止卡死。</summary>
    private static IEnumerable<string> FindUnder(string root, int maxDepth)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        var queue = new Queue<(string Dir, int Depth)>();
        queue.Enqueue((root, 0));
        var visited = 0;

        while (queue.Count > 0 && visited < 4000)
        {
            var (dir, depth) = queue.Dequeue();
            visited++;

            if (LooksLikeJavaHome(dir))
            {
                yield return dir;
                continue;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var sub in SafeDirs(dir))
            {
                // 跳过明显没用的目录，省时间
                var name = Path.GetFileName(sub);
                if (name is "cache" or "logs" or "assets" or "libraries" or "versions")
                {
                    continue;
                }

                queue.Enqueue((sub, depth + 1));
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
