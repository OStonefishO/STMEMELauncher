using System.IO.Compression;
using System.Text.Json;
using Aurora.Core.Infrastructure;

namespace Aurora.Core.Net;

/// <summary>
/// 整合包解析结果：.mrpack 已经下到临时目录、清单也读出来了，
/// 但还没落盘 —— 等调用方确认加载器就位（必要时先自动装）再装。
/// </summary>
public sealed class ModpackPlan
{
    /// <summary>已下载的 .mrpack 临时文件。</summary>
    public string TempPath { get; init; } = string.Empty;

    public string Name { get; init; } = "Modpack";

    /// <summary>用作版本目录名的安全名字。</summary>
    public string SafeName { get; init; } = "Modpack";

    public string McVersion { get; init; } = "?";

    /// <summary>fabric / quilt / forge / neoforge；纯资源包整合就是空字符串。</summary>
    public string Loader { get; init; } = string.Empty;

    /// <summary>整合包指定的加载器版本号（可能为空）。</summary>
    public string? LoaderVersion { get; init; }

    /// <summary>落盘时 inheritsFrom 用的版本 id（本机已装的匹配版本）。</summary>
    public string LoaderId { get; set; } = string.Empty;

    /// <summary>要下载的文件清单（相对路径 + 直链）。</summary>
    public List<(string Path, string Url)> Files { get; init; } = new();
}

/// <summary>
/// 整合包安装（Modrinth .mrpack）。做成独立版本目录：写一个只带 inheritsFrom 的 version.json，
/// mods/overrides 全部落进该目录，配合「版本隔离」就能直接启动。
/// 不做依赖树求解，只按 index 里的清单逐条下载。
/// </summary>
public sealed class ModpackInstaller
{
    private readonly DownloadClient _client;
    private readonly string _gameDirectory;

    public ModpackInstaller(DownloadClient client, string gameDirectory)
    {
        _client = client;
        _gameDirectory = gameDirectory;
    }

    public string GameDirectory => _gameDirectory;

    /// <summary>下载 + 解析清单，不做任何落盘。缺加载器时由调用方先补。</summary>
    public async Task<ModpackPlan> PrepareAsync(ContentFile file, IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"meme-pack-{Guid.NewGuid():N}.mrpack");

        progress?.Report(new InstallProgress { Stage = "整合包", Message = "下载 " + file.FileName });
        await _client.DownloadFileAsync(file.Url, temp, ct: ct).ConfigureAwait(false);

        using var zip = ZipFile.OpenRead(temp);
        var indexEntry = zip.Entries.FirstOrDefault(e => e.FullName == "modrinth.index.json")
                         ?? throw new InvalidOperationException("这不是有效的 .mrpack（缺少 modrinth.index.json）");

        string indexJson;
        using (var reader = new StreamReader(indexEntry.Open()))
        {
            indexJson = reader.ReadToEnd();
        }

        using var doc = JsonDocument.Parse(indexJson);
        var root = doc.RootElement;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "Modpack" : "Modpack";
        var safeName = Sanitize(name);

        var mcVersion = "?";
        var loader = string.Empty;
        string? loaderVersion = null;

        if (root.TryGetProperty("dependencies", out var deps))
        {
            mcVersion = deps.TryGetProperty("minecraft", out var mc) ? mc.GetString() ?? "?" : "?";
            (loader, loaderVersion) = ReadLoader(deps);
        }

        var files = new List<(string Path, string Url)>();
        if (root.TryGetProperty("files", out var fileArray) && fileArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in fileArray.EnumerateArray())
            {
                if (!item.TryGetProperty("path", out var p) || p.GetString() == null)
                {
                    continue;
                }

                if (!item.TryGetProperty("downloads", out var dl) || dl.ValueKind != JsonValueKind.Array ||
                    dl.GetArrayLength() == 0)
                {
                    continue;
                }

                var url = dl[0].GetString();
                if (!string.IsNullOrEmpty(url))
                {
                    files.Add((p.GetString()!, url!));
                }
            }
        }

        return new ModpackPlan
        {
            TempPath = temp,
            Name = name,
            SafeName = safeName,
            McVersion = mcVersion,
            Loader = loader,
            LoaderVersion = loaderVersion,
            Files = files,
            LoaderId = FindLoader(mcVersion, loader, loaderVersion),
        };
    }

    /// <summary>加载器就位后落盘：写版本 json、下清单、解 overrides。</summary>
    public async Task<string> InstallPreparedAsync(ModpackPlan plan, IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(plan.LoaderId))
            {
                plan.LoaderId = FindLoader(plan.McVersion, plan.Loader, plan.LoaderVersion);
            }

            if (string.IsNullOrEmpty(plan.LoaderId))
            {
                throw new InvalidOperationException(
                    $"没找到与 {plan.McVersion} 匹配的已安装加载器版本，请先装好 {plan.Loader}");
            }

            var versionDir = Path.Combine(_gameDirectory, "versions", plan.SafeName);
            Directory.CreateDirectory(versionDir);

            var versionJson = JsonSerializer.Serialize(new
            {
                id = plan.SafeName,
                inheritsFrom = plan.LoaderId,
                type = "release",
                time = DateTimeOffset.Now,
            });

            File.WriteAllText(Path.Combine(versionDir, plan.SafeName + ".json"), versionJson);

            // 1. 逐个下载清单里的文件
            var done = 0;
            foreach (var (path, url) in plan.Files)
            {
                ct.ThrowIfCancellationRequested();
                var target = Path.Combine(versionDir, path.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    await _client.DownloadFileAsync(url, target, ct: ct).ConfigureAwait(false);
                }
                catch
                {
                    // 单个文件失败不阻断整包
                }

                done++;
                if (done % 10 == 0 || done == plan.Files.Count)
                {
                    progress?.Report(new InstallProgress
                    {
                        Stage = "整合包",
                        Done = done,
                        Total = plan.Files.Count,
                        Message = $"文件 {done}/{plan.Files.Count}",
                    });
                }
            }

            // 2. 解压 overrides / client-overrides
            using (var zip = ZipFile.OpenRead(plan.TempPath))
            {
                foreach (var prefix in new[] { "overrides/", "client-overrides/" })
                {
                    foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal) &&
                                                                  !string.IsNullOrEmpty(e.Name)))
                    {
                        var relative = entry.FullName[prefix.Length..];
                        var target = Path.Combine(versionDir, relative.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        entry.ExtractToFile(target, overwrite: true);
                    }
                }
            }

            progress?.Report(new InstallProgress
            {
                Stage = "整合包",
                Done = 1,
                Total = 1,
                Message = $"{plan.Name} 安装完成（基于 {plan.LoaderId}）",
            });

            return plan.SafeName;
        }
        finally
        {
            try
            {
                if (File.Exists(plan.TempPath))
                {
                    File.Delete(plan.TempPath);
                }
            }
            catch
            {
                // 忽略
            }
        }
    }

    public async Task<string> InstallAsync(ContentFile file, IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var plan = await PrepareAsync(file, progress, ct).ConfigureAwait(false);
        return await InstallPreparedAsync(plan, progress, ct).ConfigureAwait(false);
    }

    /// <summary>按 MC 版本 + 加载器找本机已有的匹配版本 id（找不到返回空串）。</summary>
    public string FindLoader(string mcVersion, string loader, string? loaderVersion)
    {
        var versionsDir = Path.Combine(_gameDirectory, "versions");
        if (!Directory.Exists(versionsDir) || string.IsNullOrEmpty(loader))
        {
            return string.Empty;
        }

        var candidates = new List<string>();
        switch (loader)
        {
            case "fabric":
                if (!string.IsNullOrEmpty(loaderVersion))
                {
                    candidates.Add($"fabric-loader-{mcVersion}-{loaderVersion}");
                    candidates.Add($"fabric-loader-{loaderVersion}-{mcVersion}");
                }

                break;
            case "quilt":
                if (!string.IsNullOrEmpty(loaderVersion))
                {
                    candidates.Add($"quilt-loader-{mcVersion}-{loaderVersion}");
                }

                break;
            case "forge":
                if (!string.IsNullOrEmpty(loaderVersion))
                {
                    candidates.Add($"{mcVersion}-forge-{loaderVersion}");
                }

                break;
            case "neoforge":
                if (!string.IsNullOrEmpty(loaderVersion))
                {
                    candidates.Add($"neoforge-{loaderVersion}");
                }

                break;
        }

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(Path.Combine(versionsDir, candidate)))
            {
                return candidate;
            }
        }

        // 退而求其次：按加载器类型 + MC 版本模糊匹配
        var match = Directory.GetDirectories(versionsDir)
            .Select(Path.GetFileName)
            .FirstOrDefault(d => d != null && d.Contains(loader, StringComparison.OrdinalIgnoreCase) &&
                                 d.Contains(mcVersion, StringComparison.OrdinalIgnoreCase));

        return match ?? string.Empty;
    }

    /// <summary>按 mrpack 的 dependencies 找本地已有的加载器版本 id。</summary>
    private string ResolveLoaderVersionId(JsonElement deps, string mcVersion)
    {
        var (loader, loaderVersion) = ReadLoader(deps);
        return FindLoader(mcVersion, loader, loaderVersion);
    }

    /// <summary>从 dependencies 里读出加载器种类和版本号。</summary>
    private static (string Loader, string? Version) ReadLoader(JsonElement deps)
    {
        if (deps.TryGetProperty("fabric-loader", out var f) && f.GetString() is { Length: > 0 } fabric)
        {
            return ("fabric", fabric);
        }

        if (deps.TryGetProperty("quilt-loader", out var q) && q.GetString() is { Length: > 0 } quilt)
        {
            return ("quilt", quilt);
        }

        if (deps.TryGetProperty("neoforge", out var nf) && nf.GetString() is { Length: > 0 } neoForge)
        {
            return ("neoforge", neoForge);
        }

        if (deps.TryGetProperty("forge", out var fo) && fo.GetString() is { Length: > 0 } forge)
        {
            return ("forge", forge);
        }

        return (string.Empty, null);
    }

    private static string Sanitize(string name)
    {
        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(bad, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "Modpack" : name.Trim();
    }
}
