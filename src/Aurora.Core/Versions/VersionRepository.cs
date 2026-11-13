using System.Text.Json;
using System.Text.RegularExpressions;
using Aurora.Core.Infrastructure;
using Aurora.Core.Models;

namespace Aurora.Core.Versions;

/// <summary>负责扫描 .minecraft/versions、解析 version.json 以及处理 inheritsFrom 继承链。</summary>
public sealed class VersionRepository
{
    private readonly Dictionary<string, string> _jsonById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VersionJson> _rawCache = new(StringComparer.OrdinalIgnoreCase);

    public VersionRepository(string gameDirectory)
    {
        GameDirectory = Path.GetFullPath(gameDirectory);
    }

    public string GameDirectory { get; }

    public string VersionsDirectory => Path.Combine(GameDirectory, "versions");

    public string LibrariesDirectory => Path.Combine(GameDirectory, "libraries");

    public string AssetsDirectory => Path.Combine(GameDirectory, "assets");

    /// <summary>扫描版本目录，返回可直接展示的摘要列表（按名称排序）。</summary>
    /// <summary>顺 inheritsFrom 找 Java 主版本（加载器版本自己没有 javaVersion）。</summary>
    private int JavaMajorOf(string id, VersionJson? json, int depth)
    {
        var own = json?.JavaVersion is { MajorVersion: > 0 } version ? version.MajorVersion : 0;

        // 子版本自己写的 javaVersion 常常是老的（比如整包 json 里残留 8），
        // 而真正要跑的是继承链上最新的那个，所以沿链取最大值。
        var inherited = 0;
        if (depth < 6 && !string.IsNullOrEmpty(json?.InheritsFrom))
        {
            var parent = LoadRaw(json!.InheritsFrom!);
            if (parent != null)
            {
                inherited = JavaMajorOf(json.InheritsFrom!, parent, depth + 1);
            }
        }

        var guessed = InferJavaMajor(NormalizeClientVersion(json?.ClientVersion, id));
        return Math.Max(own, Math.Max(inherited, guessed));
    }

    public List<VersionSummary> Scan()
    {
        _jsonById.Clear();
        var result = new List<VersionSummary>();

        if (!Directory.Exists(VersionsDirectory))
        {
            return result;
        }

        foreach (var dir in Directory.GetDirectories(VersionsDirectory))
        {
            var folder = Path.GetFileName(dir);
            var summaries = ScanFolder(dir, folder);
            result.AddRange(summaries);
        }

        return result
            .OrderByDescending(v => v.ClientVersion, new VersionTextComparer())
            .ThenBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<VersionSummary> ScanFolder(string dir, string folder)
    {
        var jsonFiles = Directory.GetFiles(dir, "*.json");
        if (jsonFiles.Length == 0)
        {
            yield return new VersionSummary
            {
                Id = folder,
                Directory = dir,
                Error = "缺少 version.json",
            };
            yield break;
        }

        // 优先选与文件夹同名的 json，否则选能解析出 mainClass 的最大的那个
        var preferred = jsonFiles.FirstOrDefault(f =>
            string.Equals(Path.GetFileNameWithoutExtension(f), folder, StringComparison.OrdinalIgnoreCase));

        var ordered = preferred != null
            ? new[] { preferred }.Concat(jsonFiles.Where(f => !string.Equals(f, preferred, StringComparison.OrdinalIgnoreCase)))
            : jsonFiles.OrderByDescending(f => new FileInfo(f).Length);

        foreach (var jsonPath in ordered)
        {
            VersionJson? json = null;
            string? parseError = null;
            try
            {
                json = Json.Deserialize<VersionJson>(File.ReadAllText(jsonPath));
            }
            catch (Exception ex)
            {
                parseError = ex.Message;
            }

            if (parseError != null)
            {
                yield return new VersionSummary
                {
                    Id = Path.GetFileNameWithoutExtension(jsonPath),
                    Directory = dir,
                    JsonPath = jsonPath,
                    Error = "JSON 解析失败: " + parseError,
                };
                continue;
            }

            if (json == null)
            {
                continue;
            }

            // 没有 mainClass 也没有 inheritsFrom 的 json 无法启动，跳过
            if (string.IsNullOrEmpty(json.MainClass) && string.IsNullOrEmpty(json.InheritsFrom))
            {
                continue;
            }

            var id = string.IsNullOrEmpty(json.Id) ? Path.GetFileNameWithoutExtension(jsonPath) : json.Id!;
            _jsonById[id] = jsonPath;

            var jarPath = FindJar(dir, id, json) ?? FindJarInChain(id);
            var (loader, loaderVersion) = DetectLoader(id, json);
            var modsDir = Path.Combine(dir, "mods");
            var modCount = Directory.Exists(modsDir)
                ? Directory.GetFiles(modsDir, "*.jar").Length
                : 0;

            yield return new VersionSummary
            {
                Id = id,
                Directory = dir,
                JsonPath = jsonPath,
                JarPath = jarPath,
                ClientVersion = NormalizeClientVersion(json.ClientVersion ?? json.InheritsFrom, id),
                Loader = loader,
                LoaderVersion = loaderVersion,
                // 加载器版本的 json 里没有 javaVersion，要顺继承链找，否则首页会显示「Java 8」
                JavaMajor = JavaMajorOf(id, json, 0),
                ModCount = modCount,
                HasJar = jarPath != null && File.Exists(jarPath),
                HasJson = true,
            };

            yield break;
        }
    }

    /// <summary>
    /// 沿 inheritsFrom 一路找 jar：整包版本常继承到「fabric-loader-x-y」这种加载器版本，
    /// 加载器目录里没有 jar，真正有 jar 的是更上层原版版本。
    /// </summary>
    private string? FindJarInChain(string id, int depth = 0)
    {
        if (depth > 8 || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var dir = Path.Combine(VersionsDirectory, id);
        var json = LoadRaw(id);
        var own = FindJar(dir, id, json);
        if (own != null && File.Exists(own))
        {
            return own;
        }

        return string.IsNullOrEmpty(json?.InheritsFrom) ? null : FindJarInChain(json!.InheritsFrom!, depth + 1);
    }

    private static string? FindJar(string dir, string id, VersionJson? json)
    {
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(json?.Jar))
        {
            candidates.Add(Path.Combine(dir, json!.Jar + ".jar"));
        }

        candidates.Add(Path.Combine(dir, id + ".jar"));
        candidates.Add(Path.Combine(dir, Path.GetFileNameWithoutExtension(dir) + ".jar"));

        return candidates.FirstOrDefault(File.Exists);
    }

    public VersionJson? LoadRaw(string id)
    {
        if (_rawCache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        if (!_jsonById.TryGetValue(id, out var path))
        {
            // 懒扫描一次，允许直接按 id 解析
            if (_jsonById.Count == 0)
            {
                Scan();
            }

            if (!_jsonById.TryGetValue(id, out path))
            {
                return null;
            }
        }

        try
        {
            var json = Json.Deserialize<VersionJson>(File.ReadAllText(path));
            if (json != null)
            {
                _rawCache[id] = json;
            }

            return json;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>合并 inheritsFrom 继承链，得到可直接用于启动的完整描述。</summary>
    public ResolvedVersion? Resolve(string id)
    {
        if (_jsonById.Count == 0)
        {
            Scan();
        }

        var (merged, jsonPath) = MergeChain(id, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        if (merged == null)
        {
            return null;
        }

        var directory = Path.GetDirectoryName(jsonPath) ?? Path.Combine(VersionsDirectory, id);
        var jar = FindJar(directory, id, merged);
        if (jar == null && !string.IsNullOrEmpty(merged.Jar))
        {
            var alt = Path.Combine(VersionsDirectory, merged.Jar, merged.Jar + ".jar");
            if (File.Exists(alt))
            {
                jar = alt;
            }
        }

        // 加载器版本（Fabric/Quilt）自己没 jar，Minecraft 的类在被继承的原版版本里 —— 不加进 classpath，
        // Fabric 会直接报 "Minecraft game provider couldn't locate the game"。
        // Forge/NeoForge 有自己的 srg/patched jar，这里就不重复加了。
        if (jar == null && !id.Contains("forge", StringComparison.OrdinalIgnoreCase))
        {
            // merged.InheritsFrom 在合并时被清成 null 了，这里从原始 json 取（取不到就按版本号猜）
            var inheritedId = LoadRaw(id)?.InheritsFrom;
            if (string.IsNullOrEmpty(inheritedId))
            {
                inheritedId = GuessClientVersion(id);
            }

            jar = FindJarInChain(id);
        }

        var (loader, loaderVersion) = DetectLoader(id, merged);
        var (gameArgs, jvmArgs) = FlattenArguments(merged);

        // json 里没有 clientVersion / javaVersion 时（例如加载器只写了 inheritsFrom），按 inheritsFrom 和版本号推断
        var clientVersion = merged.ClientVersion;
        if (string.IsNullOrEmpty(clientVersion))
        {
            var raw = LoadRaw(id);
            clientVersion = !string.IsNullOrEmpty(raw?.InheritsFrom)
                ? raw!.InheritsFrom
                : GuessClientVersion(id);
        }

        clientVersion = NormalizeClientVersion(clientVersion, id);

        var javaMajor = merged.JavaVersion?.MajorVersion ?? InferJavaMajor(clientVersion);

        return new ResolvedVersion
        {
            Id = id,
            Directory = directory,
            JarPath = jar,
            MainClass = merged.MainClass ?? string.Empty,
            ClientVersion = clientVersion,
            Loader = loader,
            LoaderVersion = loaderVersion,
            JavaMajor = javaMajor,
            AssetIndex = merged.AssetIndex,
            AssetsRoot = AssetsDirectory,
            Libraries = merged.Libraries ?? new List<Library>(),
            GameArguments = gameArgs,
            JvmArguments = jvmArgs,
            Logging = merged.Logging,
            Type = merged.Type,
            JsonPath = jsonPath,
        };
    }

    private (VersionJson? Json, string JsonPath) MergeChain(string id, HashSet<string> visited)
    {
        if (!visited.Add(id))
        {
            return (null, string.Empty);
        }

        if (!_jsonById.TryGetValue(id, out var path))
        {
            return (null, string.Empty);
        }

        VersionJson? child;
        try
        {
            child = Json.Deserialize<VersionJson>(File.ReadAllText(path));
        }
        catch
        {
            return (null, string.Empty);
        }

        if (child == null)
        {
            return (null, string.Empty);
        }

        if (string.IsNullOrEmpty(child.InheritsFrom))
        {
            return (child, path);
        }

        var (parent, _) = MergeChain(child.InheritsFrom!, visited);
        if (parent == null)
        {
            return (child, path);
        }

        return (Merge(parent, child), path);
    }

    /// <summary>child 覆盖 parent；libraries / arguments 做拼接并去重。</summary>
    /// <summary>按 maven 坐标比较版本号，数字段按数值比，其余按字符串。</summary>
    private static int CompareVersions(string leftName, string rightName)
    {
        var a = MavenHelper.Parse(leftName)?.Version ?? leftName;
        var b = MavenHelper.Parse(rightName)?.Version ?? rightName;
        var pa = a.Split('.', '-', '+');
        var pb = b.Split('.', '-', '+');
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var sa = i < pa.Length ? pa[i] : "0";
            var sb = i < pb.Length ? pb[i] : "0";
            if (long.TryParse(sa, out var na) && long.TryParse(sb, out var nb))
            {
                if (na != nb)
                {
                    return na.CompareTo(nb);
                }
            }
            else
            {
                var cmp = string.CompareOrdinal(sa, sb);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
        }

        return 0;
    }

    private static VersionJson Merge(VersionJson parent, VersionJson child)
    {
        // 同一个 group:artifact:classifier 只留一份，取版本更高的那个；
        // natives / classifiers 字典合并（1.18.2 的 lwjgl 3.2.1 只有 macos natives，3.2.2 才有 windows 的）
        var libraries = new List<Library>();
        var byKey = new Dictionary<string, Library>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        void AddLibrary(Library lib)
        {
            var key = MavenHelper.DedupKey(lib);
            if (!byKey.TryGetValue(key, out var existing))
            {
                byKey[key] = lib;
                order.Add(key);
                return;
            }

            var keepNew = CompareVersions(lib.Name, existing.Name) > 0;
            var winner = keepNew ? lib : existing;
            var loser = keepNew ? existing : lib;

            if (loser.Natives is { Count: > 0 })
            {
                winner.Natives ??= new Dictionary<string, string>();
                foreach (var pair in loser.Natives)
                {
                    winner.Natives.TryAdd(pair.Key, pair.Value);
                }
            }

            if (loser.Downloads?.Classifiers is { Count: > 0 })
            {
                winner.Downloads ??= new LibraryDownloads();
                winner.Downloads.Classifiers ??= new Dictionary<string, ArtifactInfo>();
                foreach (var pair in loser.Downloads.Classifiers)
                {
                    winner.Downloads.Classifiers.TryAdd(pair.Key, pair.Value);
                }
            }

            byKey[key] = winner;
        }

        foreach (var lib in child.Libraries ?? new List<Library>())
        {
            AddLibrary(lib);
        }

        foreach (var lib in parent.Libraries ?? new List<Library>())
        {
            AddLibrary(lib);
        }

        foreach (var key in order)
        {
            libraries.Add(byKey[key]);
        }

        var game = new List<ArgumentEntry>();
        var jvm = new List<ArgumentEntry>();
        AppendArguments(game, child.Arguments?.Game);
        AppendArguments(game, parent.Arguments?.Game);
        AppendArguments(jvm, child.Arguments?.Jvm);
        AppendArguments(jvm, parent.Arguments?.Jvm);

        return new VersionJson
        {
            Id = child.Id,
            InheritsFrom = null,
            Type = child.Type ?? parent.Type,
            MainClass = child.MainClass ?? parent.MainClass,
            Assets = child.Assets ?? parent.Assets,
            AssetIndex = child.AssetIndex ?? parent.AssetIndex,
            JavaVersion = child.JavaVersion ?? parent.JavaVersion,
            MinecraftArguments = child.MinecraftArguments ?? parent.MinecraftArguments,
            Arguments = new ArgumentsSection { Game = game, Jvm = jvm },
            Libraries = libraries,
            Logging = child.Logging ?? parent.Logging,
            ClientVersion = child.ClientVersion ?? parent.ClientVersion,
            ComplianceLevel = child.ComplianceLevel ?? parent.ComplianceLevel,
            Jar = child.Jar ?? parent.Jar,
            ReleaseTime = child.ReleaseTime ?? parent.ReleaseTime,
            Time = child.Time ?? parent.Time,
        };
    }

    private static void AppendArguments(List<ArgumentEntry> target, List<ArgumentEntry>? source)
    {
        if (source == null)
        {
            return;
        }

        foreach (var entry in source)
        {
            target.Add(entry);
        }
    }

    private static (List<ArgumentEntry> Game, List<ArgumentEntry> Jvm) FlattenArguments(VersionJson json)
    {
        var game = new List<ArgumentEntry>();
        var jvm = new List<ArgumentEntry>();

        if (json.Arguments != null)
        {
            AppendArguments(game, json.Arguments.Game);
            AppendArguments(jvm, json.Arguments.Jvm);
        }

        if (game.Count == 0 && !string.IsNullOrEmpty(json.MinecraftArguments))
        {
            game.Add(new ArgumentEntry { Values = TextUtil.SplitCommandLine(json.MinecraftArguments) });
        }

        return (game, jvm);
    }

    public static (string Loader, string? Version) DetectLoader(string id, VersionJson json)
    {
        var main = json.MainClass ?? string.Empty;
        var libraries = json.Libraries ?? new List<Library>();

        string? LibVersion(string group, string artifact)
        {
            foreach (var lib in libraries)
            {
                var coord = MavenHelper.Parse(lib.Name);
                if (coord != null &&
                    string.Equals(coord.Group, group, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(coord.Artifact, artifact, StringComparison.OrdinalIgnoreCase))
                {
                    return coord.Version;
                }
            }

            return null;
        }

        bool HasGroup(string group) => libraries.Any(l =>
            string.Equals(MavenHelper.Parse(l.Name)?.Group, group, StringComparison.OrdinalIgnoreCase));

        var anyLib = string.Join(";", libraries.Select(l => l.Name));
        var text = id + " " + main + " " + anyLib;

        string loader;
        string? version = null;

        if (text.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
        {
            loader = "neoforge";
            version = LibVersion("net.neoforged", "neoforge")
                      ?? LibVersion("net.neoforged", "forge");
        }
        else if (text.Contains("fabricmc", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("fabric-loader", StringComparison.OrdinalIgnoreCase) ||
                 id.Contains("fabric", StringComparison.OrdinalIgnoreCase))
        {
            loader = "fabric";
            version = LibVersion("net.fabricmc", "fabric-loader");
        }
        else if (text.Contains("quilt", StringComparison.OrdinalIgnoreCase))
        {
            loader = "quilt";
            version = LibVersion("org.quiltmc", "quilt-loader");
        }
        else if (text.Contains("cpw.mods.bootstraplauncher", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("minecraftforge", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("launchwrapper", StringComparison.OrdinalIgnoreCase) ||
                 HasGroup("net.minecraftforge") ||
                 id.Contains("forge", StringComparison.OrdinalIgnoreCase))
        {
            loader = "forge";
            version = LibVersion("net.minecraftforge", "forge")
                      ?? LibVersion("net.minecraftforge", "fmlloader");
        }
        else if (text.Contains("optifine", StringComparison.OrdinalIgnoreCase))
        {
            loader = "optifine";
        }
        else
        {
            loader = "vanilla";
        }

        if (string.IsNullOrEmpty(version))
        {
            var match = Regex.Match(id, @"(?:NeoForge|Forge|Fabric|Quilt|OptiFine)[_\- ]v?([0-9][0-9A-Za-z.\-+]*)",
                RegexOptions.IgnoreCase);
            if (match.Success)
            {
                version = match.Groups[1].Value.TrimEnd('-', '_');
            }
        }

        // Forge 的版本号形如 1.21.1-52.0.1，展示时去掉 MC 前缀
        if (!string.IsNullOrEmpty(version) &&
            !string.IsNullOrEmpty(json.ClientVersion) &&
            version.StartsWith(json.ClientVersion + "-", StringComparison.OrdinalIgnoreCase))
        {
            version = version[(json.ClientVersion.Length + 1)..];
        }

        return (loader, string.IsNullOrEmpty(version) ? null : version);
    }

    /// <summary>按 MC 版本推断需要的 Java 主版本。</summary>
    public static int InferJavaMajor(string? clientVersion)
    {
        var v = clientVersion ?? string.Empty;
        if (v.StartsWith("26.", StringComparison.Ordinal) || v.StartsWith("27.", StringComparison.Ordinal) ||
            v.StartsWith("28.", StringComparison.Ordinal))
        {
            return 25;
        }

        if (v.StartsWith("1.23", StringComparison.Ordinal) || v.StartsWith("1.22", StringComparison.Ordinal) ||
            v.StartsWith("1.21", StringComparison.Ordinal) || v.StartsWith("1.20.5", StringComparison.Ordinal) ||
            v.StartsWith("1.20.6", StringComparison.Ordinal))
        {
            return 21;
        }

        if (v.StartsWith("1.18", StringComparison.Ordinal) || v.StartsWith("1.19", StringComparison.Ordinal) ||
            v.StartsWith("1.20", StringComparison.Ordinal))
        {
            return 17;
        }

        if (v.StartsWith("1.17", StringComparison.Ordinal))
        {
            return 16;
        }

        return 8;
    }

    /// <summary>有些加载器把 clientVersion 写成自己的版本名（neoforge-26.3.0.101），这里把真正的 MC 版本抠出来。</summary>
    public static string NormalizeClientVersion(string? value, string id)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GuessClientVersion(id);
        }

        var v = value.Trim();
        var looksLikeLoaderName =
            v.StartsWith("fabric", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("forge", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("neoforge", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("quilt", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("optifine", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("liteloader", StringComparison.OrdinalIgnoreCase);

        if (!looksLikeLoaderName)
        {
            return v;
        }

        return ExtractMcVersion(v) ?? GuessClientVersion(id);
    }

    /// <summary>
    /// 版本 json 里没写 clientVersion（也没写 inheritsFrom）时，从版本号里猜 MC 版本。
    /// 注意加载器的版本号也长这样（fabric-loader-0.20.1-26.3），不能简单取第一个数字。
    /// </summary>
    public static string GuessClientVersion(string id)
    {
        // fabric-loader-<loader>-<mc> / quilt-loader-<loader>-<mc>：后半截才是 MC
        var loaderForm = Regex.Match(
            id,
            @"^(?:fabric|quilt)[-_ ]loader[-_ ]+[0-9][0-9A-Za-z.+]*[-_ ]+(?<mc>.+)$",
            RegexOptions.IgnoreCase);
        if (loaderForm.Success)
        {
            var tail = ExtractMcVersion(loaderForm.Groups["mc"].Value);
            if (tail != null)
            {
                return tail;
            }
        }

        return ExtractMcVersion(id) ?? id;
    }

    /// <summary>
    /// 从一串文本里挑出最像 Minecraft 版本号的那段：
    /// 1.x / 1.xx.x，或者 26.3 这种按年份的新版本号；0.x 开头的多半是加载器版本，跳过。
    /// </summary>
    public static string? ExtractMcVersion(string text)
    {
        // 允许后面还跟着 .101 这种加载器小版本，先抓整段数字再截断
        var matches = Regex.Matches(text, @"(?<!\d)(\d{1,3}(?:\.\d{1,3}){1,3})(?!\d)");
        string? fallback = null;

        foreach (Match match in matches)
        {
            var parts = match.Groups[1].Value.Split('.');
            if (parts.Length < 2)
            {
                continue;
            }

            // 0.20.1 这类是加载器版本，不是 MC
            if (parts[0] == "0")
            {
                continue;
            }

            var raw2 = match.Groups[1].Value;

            // 1.x 习惯写三段（1.21.11），年份版本写两段（26.3）
            var take = parts[0] == "1" ? Math.Min(3, parts.Length) : Math.Min(2, parts.Length);
            var candidate = string.Join('.', parts.Take(take));

            if (candidate.StartsWith("1.", StringComparison.Ordinal) ||
                candidate.StartsWith("2", StringComparison.Ordinal))
            {
                return candidate;
            }

            fallback ??= raw2;
        }

        return fallback;
    }

    /// <summary>按版本号自然排序（26.3 > 1.21.11）。</summary>
    private sealed class VersionTextComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null || y == null)
            {
                return string.CompareOrdinal(x, y);
            }

            var a = x.Split('.');
            var b = y.Split('.');
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var na = i < a.Length && int.TryParse(a[i], out var va) ? va : -1;
                var nb = i < b.Length && int.TryParse(b[i], out var vb) ? vb : -1;
                if (na != nb)
                {
                    return na.CompareTo(nb);
                }
            }

            return string.CompareOrdinal(x, y);
        }
    }
}
