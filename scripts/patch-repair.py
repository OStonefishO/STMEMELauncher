import io
p = r"D:\综合创作\MCLauncher\src\Aurora.Core\Net\Installer.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 公开一个「开服/启动前体检」：补齐缺失或大小不对的 assets 与 libraries
if "public async Task<int> RepairAsync" not in s:
    anchor = "    private async Task DownloadAssetObjectsAsync("
    method = '''    /// <summary>
    /// 启动前体检：把缺失或大小不对的 assets 对象 / 库文件补回来。
    /// 返回补下的文件数。0 表示本来就是完好的。
    /// </summary>
    public async Task<int> RepairAsync(string versionId, IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var repaired = 0;
        try
        {
            var versionJsonPath = Path.Combine(GameDirectory, "versions", versionId, versionId + ".json");
            if (!File.Exists(versionJsonPath))
            {
                return 0;
            }

            using var versionDoc = JsonDocument.Parse(await File.ReadAllTextAsync(versionJsonPath, ct).ConfigureAwait(false));
            var root = versionDoc.RootElement;

            // ---- assets ----
            if (root.TryGetProperty("assetIndex", out var assetIndex) &&
                assetIndex.TryGetProperty("id", out var indexIdProp))
            {
                var indexId = indexIdProp.GetString();
                if (!string.IsNullOrEmpty(indexId))
                {
                    var indexPath = Path.Combine(AssetsDirectory, "indexes", indexId + ".json");
                    if (!File.Exists(indexPath))
                    {
                        progress?.Report(new InstallProgress { Stage = "资源索引", Message = "补下 assets index" });
                        await DownloadFileAsync(
                            new Uri($"https://launchermeta.mojang.com/v1/packages/{indexId}/{indexId}.json"),
                            indexPath, ct).ConfigureAwait(false);
                    }

                    if (File.Exists(indexPath))
                    {
                        var before = CountAssetObjectsOnDisk(indexPath);
                        await DownloadAssetObjectsAsync(indexPath, progress, ct).ConfigureAwait(false);
                        repaired += Math.Max(0, CountAssetObjectsOnDisk(indexPath) - before);
                    }
                }
            }

            // ---- libraries ----
            if (root.TryGetProperty("libraries", out var libraries))
            {
                foreach (var lib in libraries.EnumerateArray())
                {
                    ct.ThrowIfCancellationRequested();
                    if (!lib.TryGetProperty("downloads", out var downloads) ||
                        !downloads.TryGetProperty("artifact", out var artifact) ||
                        !artifact.TryGetProperty("path", out var pathProp) ||
                        !artifact.TryGetProperty("url", out var urlProp))
                    {
                        continue;
                    }

                    var rel = pathProp.GetString();
                    var url = urlProp.GetString();
                    if (string.IsNullOrEmpty(rel) || string.IsNullOrEmpty(url))
                    {
                        continue;
                    }

                    var target = Path.Combine(GameDirectory, "libraries", rel.Replace('/', Path.DirectorySeparatorChar));
                    var expected = artifact.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt64(out var parsed)
                        ? parsed
                        : -1;
                    if (File.Exists(target) && (expected <= 0 || new FileInfo(target).Length == expected))
                    {
                        continue;
                    }

                    progress?.Report(new InstallProgress { Stage = "依赖库", Message = "补下 " + Path.GetFileName(rel) });
                    await DownloadFileAsync(new Uri(url), target, ct).ConfigureAwait(false);
                    repaired++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 体检失败不影响启动，游戏自己会报
        }

        return repaired;
    }

    private int CountAssetObjectsOnDisk(string indexPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(indexPath));
            if (!doc.RootElement.TryGetProperty("objects", out var objects))
            {
                return 0;
            }

            var count = 0;
            foreach (var prop in objects.EnumerateObject())
            {
                if (!prop.Value.TryGetProperty("hash", out var hash))
                {
                    continue;
                }

                var h = hash.GetString();
                if (string.IsNullOrEmpty(h))
                {
                    continue;
                }

                var file = Path.Combine(AssetsDirectory, "objects", h[..2], h);
                if (File.Exists(file))
                {
                    count++;
                }
            }

            return count;
        }
        catch
        {
            return 0;
        }
    }

'''
    s = s.replace(anchor, method + anchor, 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("repair method added")
