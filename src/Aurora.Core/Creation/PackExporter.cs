using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Aurora.Core.Creation;

/// <summary>整合包里可以勾选的一项（一个目录或一个文件）。</summary>
public sealed class PackCandidate
{
    public string Name { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public bool IsFolder { get; init; }
    public int FileCount { get; init; }
    public long SizeBytes { get; init; }
    public bool Suggested { get; init; }
    public string Hint { get; init; } = string.Empty;

    public string SizeText => SizeBytes switch
    {
        >= 1073741824 => (SizeBytes / 1073741824.0).ToString("0.##") + " GB",
        >= 1048576 => (SizeBytes / 1048576.0).ToString("0.#") + " MB",
        >= 1024 => (SizeBytes / 1024.0).ToString("0") + " KB",
        _ => SizeBytes + " B",
    };

    public string DetailText => IsFolder ? $"{FileCount} 个文件 · {SizeText}" : SizeText;
}

public sealed class PackExportOptions
{
    public string SourceDirectory { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string Name { get; set; } = "我的整合包";
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Loader { get; set; } = string.Empty;
    public string McVersion { get; set; } = string.Empty;

    /// <summary>要打包的相对路径（目录或文件）。</summary>
    public List<string> Includes { get; set; } = new();

    public IProgress<string>? Progress { get; set; }
}

public sealed class PackExportResult
{
    public string Path { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public int FileCount { get; init; }
}

/// <summary>把一个版本的目录打包成整合包 zip：可选项由用户勾，附带说明与元数据。</summary>
public static class PackExporter
{
    /// <summary>已知的、常见可打包内容（顺序即界面顺序）。</summary>
    private static readonly (string Path, bool Folder, bool Suggested, string Hint)[] Known =
    {
        ("mods", true, true, "模组本体，一般都要"),
        ("config", true, true, "模组配置文件，别人玩你的包需要它"),
        ("defaultconfigs", true, true, "新世界默认配置"),
        ("options.txt", false, false, "按键、画质等本机设置，可能带你的键位"),
        ("saves", true, false, "存档/地图，体积可能很大"),
        ("resourcepacks", true, false, "资源包"),
        ("shaderpacks", true, false, "光影包"),
        ("scripts", true, false, "KubeJS 等脚本"),
        ("kubejs", true, false, "KubeJS 目录"),
        ("servers.dat", false, false, "服务器列表"),
    };

    public static List<PackCandidate> Inspect(string gameDir)
    {
        var list = new List<PackCandidate>();
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
        {
            return list;
        }

        foreach (var (relative, isFolder, suggested, hint) in Known)
        {
            var full = Path.Combine(gameDir, relative);
            if (isFolder)
            {
                if (!Directory.Exists(full))
                {
                    continue;
                }

                var files = SafeFiles(full);
                list.Add(new PackCandidate
                {
                    Name = relative,
                    RelativePath = relative,
                    IsFolder = true,
                    FileCount = files.Count,
                    SizeBytes = files.Sum(f => f.Length),
                    Suggested = suggested,
                    Hint = hint,
                });
            }
            else
            {
                if (!File.Exists(full))
                {
                    continue;
                }

                list.Add(new PackCandidate
                {
                    Name = relative,
                    RelativePath = relative,
                    IsFolder = false,
                    FileCount = 1,
                    SizeBytes = new FileInfo(full).Length,
                    Suggested = suggested,
                    Hint = hint,
                });
            }
        }

        return list;
    }

    public static PackExportResult Export(PackExportOptions options)
    {
        if (!Directory.Exists(options.SourceDirectory))
        {
            throw new DirectoryNotFoundException("找不到版本目录: " + options.SourceDirectory);
        }

        if (options.Includes.Count == 0)
        {
            throw new InvalidOperationException("没有勾选任何要打包的内容");
        }

        if (File.Exists(options.OutputPath))
        {
            File.Delete(options.OutputPath);
        }

        var added = 0;
        var bytes = 0L;
        using var zip = ZipFile.Open(options.OutputPath, ZipArchiveMode.Create);

        foreach (var relative in options.Includes)
        {
            var full = Path.Combine(options.SourceDirectory, relative);
            if (Directory.Exists(full))
            {
                foreach (var file in SafeFiles(full))
                {
                    var entryName = Path.GetRelativePath(options.SourceDirectory, file.FullName).Replace('\\', '/');
                    zip.CreateEntryFromFile(file.FullName, entryName, CompressionLevel.Optimal);
                    added++;
                    bytes += file.Length;
                    if (added % 40 == 0)
                    {
                        options.Progress?.Report($"正在打包 {added} 个文件…");
                    }
                }
            }
            else if (File.Exists(full))
            {
                zip.CreateEntryFromFile(full, relative.Replace('\\', '/'), CompressionLevel.Optimal);
                added++;
                bytes += new FileInfo(full).Length;
            }
        }

        // 说明文件
        var readme = new StringBuilder();
        readme.AppendLine(options.Name);
        readme.AppendLine(new string('=', 32));
        if (!string.IsNullOrWhiteSpace(options.Description))
        {
            readme.AppendLine(options.Description);
            readme.AppendLine();
        }

        readme.AppendLine("版本: " + options.Version);
        if (!string.IsNullOrWhiteSpace(options.McVersion))
        {
            readme.AppendLine("Minecraft: " + options.McVersion);
        }

        if (!string.IsNullOrWhiteSpace(options.Loader))
        {
            readme.AppendLine("加载器: " + options.Loader);
        }

        readme.AppendLine("打包内容: " + string.Join(", ", options.Includes));
        readme.AppendLine();
        readme.AppendLine("导出自 MEME Launcher · STONEFISH8");
        WriteText(zip, "README.txt", readme.ToString());

        var manifest = JsonSerializer.Serialize(new
        {
            name = options.Name,
            description = options.Description,
            version = options.Version,
            minecraft = options.McVersion,
            loader = options.Loader,
            includes = options.Includes,
            exportedAt = DateTimeOffset.Now.ToString("O"),
            exportedBy = "MEME Launcher",
        }, new JsonSerializerOptions { WriteIndented = true });
        WriteText(zip, "meme-modpack.json", manifest);

        return new PackExportResult
        {
            Path = options.OutputPath,
            SizeBytes = new FileInfo(options.OutputPath).Length,
            FileCount = added,
        };
    }

    private static void WriteText(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var buffer = Encoding.UTF8.GetBytes(content);
        stream.Write(buffer, 0, buffer.Length);
    }

    private static List<FileInfo> SafeFiles(string dir)
    {
        try
        {
            return new DirectoryInfo(dir)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(f => f.Exists)
                .ToList();
        }
        catch
        {
            return new List<FileInfo>();
        }
    }
}
