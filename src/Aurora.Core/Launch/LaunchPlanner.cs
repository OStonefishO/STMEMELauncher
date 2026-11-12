using System.IO.Compression;
using Aurora.Core.Infrastructure;
using Aurora.Core.Models;
using Aurora.Core.Versions;

namespace Aurora.Core.Launch;

public sealed class LaunchPlanner
{
    private readonly string _librariesDirectory;

    public LaunchPlanner(string librariesDirectory)
    {
        _librariesDirectory = librariesDirectory;
    }

    public LaunchPlan Build(ResolvedVersion version, LaunchOptions options)
    {
        var warnings = new List<string>();
        var features = new FeatureSet
        {
            HasCustomResolution = options.WindowWidth > 0 && options.WindowHeight > 0,
        };

        var nativesDir = Path.Combine(version.Directory, "natives");
        var nativeFiles = ExtractNatives(version, nativesDir, features, warnings, out var declaredNatives);

        var classPath = BuildClassPath(version, features, warnings);
        var classPathText = string.Join(Path.PathSeparator, classPath);

        var playerName = string.IsNullOrWhiteSpace(options.PlayerName) ? "Steve" : options.PlayerName.Trim();
        var uuid = options.Uuid ?? TextUtil.OfflineUuid(playerName);

        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["${auth_player_name}"] = playerName,
            ["${version_name}"] = version.Id,
            ["${game_directory}"] = options.GameDirectory ?? version.Directory,
            ["${assets_root}"] = version.AssetsRoot ?? string.Empty,
            ["${assets_index_name}"] = version.AssetIndex?.Id ?? version.ClientVersion,
            ["${auth_uuid}"] = uuid.Replace("-", string.Empty),
            ["${auth_access_token}"] = options.AccessToken,
            ["${auth_session}"] = "token:" + options.AccessToken + ":" + uuid.Replace("-", string.Empty),
            ["${user_type}"] = options.UserType,
            ["${version_type}"] = version.Type ?? "release",
            ["${user_properties}"] = "{}",
            ["${clientid}"] = string.Empty,
            ["${auth_xuid}"] = string.Empty,
            ["${natives_directory}"] = nativesDir,
            ["${launcher_name}"] = options.LauncherName,
            ["${launcher_version}"] = options.LauncherVersion,
            ["${classpath}"] = classPathText,
            ["${library_directory}"] = _librariesDirectory,
            ["${classpath_separator}"] = Path.PathSeparator.ToString(),
            ["${resolution_width}"] = options.WindowWidth.ToString(),
            ["${resolution_height}"] = options.WindowHeight.ToString(),
            ["${quickPlayPath}"] = string.Empty,
            ["${quickPlaySingleplayer}"] = string.Empty,
            ["${quickPlayMultiplayer}"] = string.Empty,
            ["${quickPlayRealms}"] = string.Empty,
        };

        var jvmArgs = new List<string>
        {
            $"-Xms{Math.Max(256, options.MinMemoryMb)}M",
            $"-Xmx{Math.Max(512, options.MaxMemoryMb)}M",
            "-Dfile.encoding=UTF-8",
            "-Dstdout.encoding=UTF-8",
            "-Dstderr.encoding=UTF-8",
        };

        // 外置登录：authlib-injector 必须在最前面注入
        if (!string.IsNullOrEmpty(options.AuthlibInjectorPath) && File.Exists(options.AuthlibInjectorPath))
        {
            var root = string.IsNullOrEmpty(options.YggdrasilApiRoot)
                ? "https://littleskin.cn/api/yggdrasil"
                : options.YggdrasilApiRoot!;
            jvmArgs.Add($"-javaagent:{options.AuthlibInjectorPath}={root}");
        }

        if (!string.IsNullOrWhiteSpace(options.ExtraJvmArgs))
        {
            jvmArgs.AddRange(TextUtil.SplitCommandLine(options.ExtraJvmArgs));
        }

        var hasJvmSection = version.JvmArguments.Count > 0;
        if (hasJvmSection)
        {
            foreach (var arg in ResolveArguments(version.JvmArguments, features, replacements))
            {
                jvmArgs.Add(arg);
            }
        }
        else
        {
            // 1.12 及更早：自己拼 JVM 参数
            jvmArgs.Add("-Djava.library.path=" + nativesDir);
            jvmArgs.Add("-cp");
            jvmArgs.Add(classPathText);
        }

        // 某些老版本 json 缺少 library path 声明，补上以避免 natives 找不到
        if (!jvmArgs.Any(a => a.StartsWith("-Djava.library.path=", StringComparison.Ordinal)))
        {
            jvmArgs.Add("-Djava.library.path=" + nativesDir);
        }

        var loggingArg = BuildLoggingArgument(version, replacements, warnings);
        if (loggingArg != null)
        {
            jvmArgs.Add(loggingArg);
        }

        var gameArgs = ResolveArguments(version.GameArguments, features, replacements);

        if (!string.IsNullOrWhiteSpace(options.ExtraGameArgs))
        {
            gameArgs.AddRange(TextUtil.SplitCommandLine(options.ExtraGameArgs));
        }

        if (!string.IsNullOrEmpty(options.ServerAddress))
        {
            gameArgs.Add("--server");
            gameArgs.Add(options.ServerAddress);
            gameArgs.Add("--port");
            gameArgs.Add(options.ServerPort > 0 ? options.ServerPort.ToString() : "25565");
        }

        // 窗口化全屏：铺满主屏但不走独占全屏；全屏：直接用游戏的 --fullscreen
        if (string.Equals(options.LaunchMode, "fullscreen", StringComparison.OrdinalIgnoreCase) &&
            !gameArgs.Any(a => a.Equals("--fullscreen", StringComparison.OrdinalIgnoreCase)))
        {
            gameArgs.Add("--fullscreen");
        }

        if (options.WindowWidth > 0 && options.WindowHeight > 0 &&
            !gameArgs.Any(a => a.Equals("--width", StringComparison.OrdinalIgnoreCase)))
        {
            gameArgs.Add("--width");
            gameArgs.Add(options.WindowWidth.ToString());
            gameArgs.Add("--height");
            gameArgs.Add(options.WindowHeight.ToString());
        }

        if (declaredNatives > 0 && nativeFiles == 0)
        {
            warnings.Add("版本声明了 natives 但一个都没解压出来，请检查 libraries 完整性");
        }

        return new LaunchPlan
        {
            JavaPath = options.JavaPath ?? string.Empty,
            MainClass = version.MainClass,
            JvmArguments = jvmArgs,
            GameArguments = gameArgs,
            WorkingDirectory = options.GameDirectory ?? version.Directory,
            NativesDirectory = nativesDir,
            ClassPath = classPath,
            Warnings = warnings,
        };
    }

    private static List<string> ResolveArguments(
        List<ArgumentEntry> entries,
        FeatureSet features,
        Dictionary<string, string> replacements)
    {
        var result = new List<string>();
        foreach (var entry in entries)
        {
            if (!RuleEvaluator.IsAllowed(entry.Rules, features))
            {
                continue;
            }

            foreach (var value in entry.Values)
            {
                // 空占位符（如 ${clientId}）要保留为空参数，否则后面的开关会把下一个参数当成自己的值
                result.Add(Replace(value, replacements));
            }
        }

        return result;
    }

    public static string Replace(string input, Dictionary<string, string> replacements)
    {
        if (string.IsNullOrEmpty(input) || input.IndexOf("${", StringComparison.Ordinal) < 0)
        {
            return input;
        }

        var result = input;
        foreach (var (key, value) in replacements)
        {
            if (result.Contains(key, StringComparison.Ordinal))
            {
                result = result.Replace(key, value, StringComparison.Ordinal);
            }
        }

        // 剩下的未知占位符直接清掉，避免把字面量传给游戏
        if (result.Contains("${", StringComparison.Ordinal))
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\$\{[^}]*\}", string.Empty);
        }

        return result;
    }

    private List<string> BuildClassPath(ResolvedVersion version, FeatureSet features, List<string> warnings)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!File.Exists(path))
            {
                warnings.Add("缺少库文件: " + path);
                return;
            }

            if (seen.Add(path))
            {
                paths.Add(path);
            }
        }

        foreach (var lib in version.Libraries)
        {
            if (!RuleEvaluator.IsAllowed(lib.Rules, features))
            {
                continue;
            }

            if (IsNativeOnly(lib))
            {
                continue;
            }

            // net.minecraft:client:*（srg / extra / slim）属于 Forge/NeoForge 的 -p 模块路径，
            // 再塞进 classpath 会出现两个 minecraft 模块 → module.ResolutionException
            if (lib.Name.StartsWith("net.minecraft:client", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var libPath = MavenHelper.ResolveArtifactPath(lib, _librariesDirectory);
            if (libPath != null && libPath.Replace('\\', '/').Contains("net/minecraft/client/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Add(libPath);
        }

        // 客户端 jar 放最后，和官方启动器一致；
        // Forge/NeoForge 的客户端已经在 -p 模块路径里，再塞原版 jar 会出现两个 minecraft 模块
        if (version.Loader is not ("forge" or "neoforge"))
        {
            Add(version.JarPath);
        }

        return paths;
    }

    private static bool IsNativeOnly(Library lib)
    {
        // 只带 natives 分类器、没有主构件的库不参与 classpath
        return lib.Natives != null && lib.Downloads?.Artifact == null;
    }

    private int ExtractNatives(ResolvedVersion version, string nativesDir, FeatureSet features, List<string> warnings,
        out int declared)
    {
        var count = 0;
        declared = 0;
        try
        {
            if (Directory.Exists(nativesDir))
            {
                Directory.Delete(nativesDir, true);
            }

            Directory.CreateDirectory(nativesDir);
        }
        catch (Exception ex)
        {
            warnings.Add("无法重建 natives 目录: " + ex.Message);
            return 0;
        }

        var osName = RuleEvaluator.CurrentOsName;
        foreach (var lib in version.Libraries)
        {
            if (lib.Natives == null || !RuleEvaluator.IsAllowed(lib.Rules, features))
            {
                continue;
            }

            if (!lib.Natives.TryGetValue(osName, out var classifierTemplate))
            {
                continue;
            }

            if (string.IsNullOrEmpty(classifierTemplate))
            {
                continue;
            }

            var classifier = classifierTemplate.Replace("${arch}", RuleEvaluator.ArchBits);
            declared++;
            var jarPath = ResolveNativeJar(lib, classifier);
            if (jarPath == null || !File.Exists(jarPath))
            {
                warnings.Add($"缺少 natives: {lib.Name} ({classifier})");
                continue;
            }

            try
            {
                using var zip = ZipFile.OpenRead(jarPath);
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    if (entry.FullName.StartsWith("META-INF", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (IsExcluded(lib.Extract, entry.FullName))
                    {
                        continue;
                    }

                    var target = Path.Combine(nativesDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, overwrite: true);
                    count++;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"解压 natives 失败 {lib.Name}: {ex.Message}");
            }
        }

        return count;
    }

    private string? ResolveNativeJar(Library lib, string classifier)
    {
        var artifact = lib.Downloads?.Classifiers != null &&
                       lib.Downloads.Classifiers.TryGetValue(classifier, out var info)
            ? info
            : null;

        if (!string.IsNullOrEmpty(artifact?.Path))
        {
            return Path.Combine(_librariesDirectory, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
        }

        var coord = MavenHelper.Parse(lib.Name);
        if (coord == null)
        {
            return null;
        }

        var withClassifier = new MavenHelper.Coordinate(coord.Group, coord.Artifact, coord.Version, classifier, "jar");
        return Path.Combine(_librariesDirectory, withClassifier.RelativePath);
    }

    private static bool IsExcluded(ExtractRule? extract, string entryName)
    {
        if (extract?.Exclude == null)
        {
            return false;
        }

        return extract.Exclude.Any(prefix => entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string? BuildLoggingArgument(
        ResolvedVersion version,
        Dictionary<string, string> replacements,
        List<string> warnings)
    {
        var client = version.Logging?.Client;
        if (client?.File == null || string.IsNullOrEmpty(client.Argument))
        {
            return null;
        }

        var gameDir = replacements["${game_directory}"];
        var path = Path.Combine(gameDir, "log4j", client.File.Id ?? "client-1.12.xml");
        try
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var http = new HttpClient();
                var bytes = http.GetByteArrayAsync(client.File.Url).GetAwaiter().GetResult();
                File.WriteAllBytes(path, bytes);
            }
        }
        catch (Exception ex)
        {
            warnings.Add("下载 log4j 配置失败: " + ex.Message);
            return null;
        }

        return client.Argument.Replace("${path}", path, StringComparison.Ordinal);
    }
}
