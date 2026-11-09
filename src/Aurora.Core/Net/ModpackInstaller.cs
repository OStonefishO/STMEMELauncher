using System.IO.Compression;
using System.Text.Json;
using Aurora.Core.Infrastructure;

namespace Aurora.Core.Net;

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

    public async Task<string> InstallAsync(ContentFile file, IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"meme-pack-{Guid.NewGuid():N}.mrpack");
        try
        {
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
            var loaderId = string.Empty;
            if (root.TryGetProperty("dependencies", out var deps))
            {
                mcVersion = deps.TryGetProperty("minecraft", out var mc) ? mc.GetString() ?? "?" : "?";
                loaderId = ResolveLoaderVersionId(deps, mcVersion);
            }

            if (string.IsNullOrEmpty(loaderId))
            {
                throw new InvalidOperationException(
                    $"没找到与 {mcVersion} 匹配的已安装加载器版本，请先在「安装」页装好 Fabric/NeoForge/Forge");
            }

            var versionDir = Path.Combine(_gameDirectory, "versions", safeName);
            Directory.CreateDirectory(versionDir);

            var versionJson = JsonSerializer.Serialize(new
            {
                id = safeName,
                inheritsFrom = loaderId,
                type = "release",
                time = DateTimeOffset.Now,
            });
            File.WriteAllText(Path.Combine(versionDir, safeName + ".json"), versionJson);

            // 1. 逐个下载清单里的文件
            var entries = new List<(string Path, string Url)>();
            if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in files.EnumerateArray())
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
                        entries.Add((p.GetString()!, url!));
                    }
                }
            }

            var done = 0;
            foreach (var (path, url) in entries)
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
                if (done % 10 == 0 || done == entries.Count)
                {
                    progress?.Report(new InstallProgress
                    {
                        Stage = "整合包",
                        Done = done,
                        Total = entries.Count,
                        Message = $"文件 {done}/{entries.Count}",
                    });
                }
            }

            // 2. 解压 overrides / client-overrides
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

            progress?.Report(new InstallProgress
            {
                Stage = "整合包",
                Done = 1,
                Total = 1,
                Message = $"{name} 安装完成（基于 {loaderId}）",
            });

            return safeName;
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch
            {
                // 忽略
            }
        }
    }

    /// <summary>按 mrpack 的 dependencies 找本地已有的加载器版本 id。</summary>
    private string ResolveLoaderVersionId(JsonElement deps, string mcVersion)
    {
        var versionsDir = Path.Combine(_gameDirectory, "versions");
        if (!Directory.Exists(versionsDir))
        {
            return string.Empty;
        }

        string? fabric = deps.TryGetProperty("fabric-loader", out var f) ? f.GetString() : null;
        string? quilt = deps.TryGetProperty("quilt-loader", out var q) ? q.GetString() : null;
        string? forge = deps.TryGetProperty("forge", out var fo) ? fo.GetString() : null;
        string? neoForge = deps.TryGetProperty("neoforge", out var nf) ? nf.GetString() : null;

        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(fabric))
        {
            candidates.Add($"fabric-loader-{mcVersion}-{fabric}");
            candidates.Add($"fabric-loader-{fabric}-{mcVersion}");
        }

        if (!string.IsNullOrEmpty(quilt))
        {
            candidates.Add($"quilt-loader-{mcVersion}-{quilt}");
        }

        if (!string.IsNullOrEmpty(forge))
        {
            candidates.Add($"{mcVersion}-forge-{forge}");
        }

        if (!string.IsNullOrEmpty(neoForge))
        {
            candidates.Add($"neoforge-{neoForge}");
        }

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(Path.Combine(versionsDir, candidate)))
            {
                return candidate;
            }
        }

        // 退而求其次：按加载器类型 + MC 版本模糊匹配
        var kind = !string.IsNullOrEmpty(fabric) ? "fabric" : !string.IsNullOrEmpty(neoForge) ? "neoforge"
            : !string.IsNullOrEmpty(forge) ? "forge" : null;
        if (kind != null)
        {
            var match = Directory.GetDirectories(versionsDir)
                .Select(Path.GetFileName)
                .FirstOrDefault(d => d != null && d.Contains(kind, StringComparison.OrdinalIgnoreCase) &&
                                     d.Contains(mcVersion, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }
        }

        return string.Empty;
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
