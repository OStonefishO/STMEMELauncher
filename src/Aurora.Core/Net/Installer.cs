using System.Text.Json;
using Aurora.Core.Infrastructure;
using Aurora.Core.Models;

namespace Aurora.Core.Net;

public sealed class RemoteVersion
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ReleaseTime { get; set; }
}

public sealed class InstallProgress
{
    public string Stage { get; init; } = string.Empty;
    public int Done { get; init; }
    public int Total { get; init; }
    public string Message { get; init; } = string.Empty;

    public double Percent => Total <= 0 ? 0 : Math.Round(Done * 100.0 / Total, 1);
}

/// <summary>原版安装：版本 json、客户端 jar、依赖库、资源索引与资源对象。</summary>
public sealed class Installer
{
    private const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    private const string ObjectBase = "https://resources.download.minecraft.net";

    private readonly DownloadClient _client;

    public Installer(DownloadClient client, string gameDirectory)
    {
        _client = client;
        GameDirectory = Path.GetFullPath(gameDirectory);
    }

    public string GameDirectory { get; }

    public string VersionsDirectory => Path.Combine(GameDirectory, "versions");

    public string LibrariesDirectory => Path.Combine(GameDirectory, "libraries");

    public string AssetsDirectory => Path.Combine(GameDirectory, "assets");

    public async Task<List<RemoteVersion>> GetManifestAsync(CancellationToken ct = default)
    {
        var json = await _client.GetStringAsync(ManifestUrl, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var list = new List<RemoteVersion>();
        if (!doc.RootElement.TryGetProperty("versions", out var versions))
        {
            return list;
        }

        foreach (var item in versions.EnumerateArray())
        {
            list.Add(new RemoteVersion
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                Type = item.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                Url = item.GetProperty("url").GetString() ?? string.Empty,
                ReleaseTime = item.TryGetProperty("releaseTime", out var r) ? r.GetString() : null,
            });
        }

        return list;
    }

    public async Task<string> InstallVanillaAsync(string versionId, IProgress<InstallProgress>? progress = null,
        bool downloadAssets = true, CancellationToken ct = default)
    {
        progress?.Report(new InstallProgress { Stage = "版本信息", Message = "获取版本清单" });
        var manifest = await GetManifestAsync(ct).ConfigureAwait(false);
        var remote = manifest.FirstOrDefault(v => string.Equals(v.Id, versionId, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException("清单中不存在版本: " + versionId);

        var versionDir = Path.Combine(VersionsDirectory, remote.Id);
        Directory.CreateDirectory(versionDir);

        var jsonPath = Path.Combine(versionDir, remote.Id + ".json");
        progress?.Report(new InstallProgress { Stage = "版本信息", Message = "下载 version.json" });
        var jsonText = await _client.GetStringAsync(remote.Url, ct).ConfigureAwait(false);
        File.WriteAllText(jsonPath, jsonText);

        var versionJson = Json.Deserialize<VersionJson>(jsonText)
                          ?? throw new InvalidOperationException("version.json 解析失败");

        // 客户端 jar
        var clientUrl = ExtractClientUrl(jsonText);
        if (!string.IsNullOrEmpty(clientUrl))
        {
            progress?.Report(new InstallProgress { Stage = "客户端", Message = "下载 client.jar" });
            await _client.DownloadFileAsync(clientUrl, Path.Combine(versionDir, remote.Id + ".jar"), ct: ct)
                .ConfigureAwait(false);
        }

        // 依赖库
        var libraries = (versionJson.Libraries ?? new List<Library>())
            .Where(l => l.Downloads?.Artifact?.Url != null)
            .ToList();
        var libDone = 0;
        foreach (var lib in libraries)
        {
            ct.ThrowIfCancellationRequested();
            var artifact = lib.Downloads!.Artifact!;
            var target = Path.Combine(LibrariesDirectory, (artifact.Path ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
            progress?.Report(new InstallProgress
            {
                Stage = "依赖库",
                Done = libDone,
                Total = libraries.Count,
                Message = Path.GetFileName(target),
            });

            await _client.DownloadFileAsync(artifact.Url!, target, artifact.Sha1, artifact.Size, ct).ConfigureAwait(false);
            libDone++;
        }

        // 资源索引
        var assetIndexId = versionJson.AssetIndex?.Id;
        if (!string.IsNullOrEmpty(assetIndexId) && !string.IsNullOrEmpty(versionJson.AssetIndex?.Url))
        {
            var indexPath = Path.Combine(AssetsDirectory, "indexes", assetIndexId + ".json");
            progress?.Report(new InstallProgress { Stage = "资源索引", Message = "下载 assets index" });
            await _client.DownloadFileAsync(versionJson.AssetIndex!.Url!, indexPath, ct: ct).ConfigureAwait(false);

            if (downloadAssets)
            {
                await DownloadAssetObjectsAsync(indexPath, progress, ct).ConfigureAwait(false);
            }
        }

        // 装完必须验一遍：jar 半截、库缺失都要当场报错，不能"装完了但起不来"
        var jarPath = Path.Combine(versionDir, remote.Id + ".jar");
        if (!File.Exists(jarPath) || new FileInfo(jarPath).Length < 1024 * 1024)
        {
            throw new InvalidOperationException("客户端 jar 没下完整（" + remote.Id + ".jar），请重试安装");
        }

        var libDir = Path.Combine(GameDirectory, "libraries");
        var missingLibs = (versionJson.Libraries ?? new List<Library>())
            .Select(l => Aurora.Core.Versions.MavenHelper.ResolveArtifactPath(l, libDir))
            .Where(p2 => p2 != null && !File.Exists(p2))
            .Take(5)
            .ToList();
        if (missingLibs.Count > 0)
        {
            throw new InvalidOperationException(
                "安装不完整：还缺 " + missingLibs.Count + " 个依赖库（" +
                string.Join("、", missingLibs.Select(Path.GetFileName)) + "）");
        }

        progress?.Report(new InstallProgress { Stage = "完成", Done = 1, Total = 1, Message = remote.Id + " 安装完成（已校验）" });
        return jsonPath;
    }

    /// <summary>
    /// 启动前体检：把缺失或大小不对的 assets 对象 / 库文件补回来。
    /// 返回补下的文件数。0 表示本来就是完好的。
    /// </summary>
    /// <summary>数一下 assets 对象缺多少个（只做文件存在性检查，很快）。</summary>
    public int CountMissingAssets(ResolvedVersion resolved)
    {
        try
        {
            if (resolved.AssetIndex is not { Id: { Length: > 0 } id })
            {
                return 0;
            }

            var indexPath = Path.Combine(AssetsDirectory, "indexes", id + ".json");
            if (!File.Exists(indexPath))
            {
                return 1;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(indexPath));
            if (!doc.RootElement.TryGetProperty("objects", out var objects) ||
                objects.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            var missing = 0;
            foreach (var entry in objects.EnumerateObject())
            {
                if (!entry.Value.TryGetProperty("hash", out var hashProp))
                {
                    continue;
                }

                var hash = hashProp.GetString();
                if (string.IsNullOrEmpty(hash) || hash.Length < 2)
                {
                    continue;
                }

                var path = Path.Combine(AssetsDirectory, "objects", hash[..2], hash);
                if (!File.Exists(path))
                {
                    missing++;
                }
            }

            return missing;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 按解析好的版本（已合并继承链）修资源：assets 对象 + 依赖库。
    /// 直接读版本自己的 json 是不行的 —— Forge/NeoForge 的版本只写 inheritsFrom，assetIndex 和库都在父版本里。
    /// </summary>
    public async Task<int> RepairAsync(ResolvedVersion resolved, IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var repaired = 0;
        try
        {
            if (resolved.AssetIndex is { Id.Length: > 0 } index && !string.IsNullOrEmpty(index.Id))
            {
                var indexPath = Path.Combine(AssetsDirectory, "indexes", index.Id + ".json");
                if (!File.Exists(indexPath) && !string.IsNullOrEmpty(index.Url))
                {
                    progress?.Report(new InstallProgress { Stage = "资源索引", Message = "补下 assets index" });
                    await _client.DownloadFileAsync(index.Url!, indexPath, ct: ct).ConfigureAwait(false);
                }

                if (File.Exists(indexPath))
                {
                    var before = CountAssetObjectsOnDisk(indexPath);
                    await DownloadAssetObjectsAsync(indexPath, progress, ct).ConfigureAwait(false);
                    repaired += Math.Max(0, CountAssetObjectsOnDisk(indexPath) - before);
                }
            }

            foreach (var lib in resolved.Libraries)
            {
                ct.ThrowIfCancellationRequested();

                // natives（windows 分类器）也要补，否则启动时解压不出来，游戏照样起不来
                if (lib.Natives != null && lib.Downloads?.Classifiers != null)
                {
                    var nsKey = lib.Natives.FirstOrDefault(n => n.Key.StartsWith("windows", StringComparison.OrdinalIgnoreCase)).Value;
                    if (!string.IsNullOrEmpty(nsKey))
                    {
                        var classifier = nsKey!.Replace("${arch}", Environment.Is64BitProcess ? "64" : "32");
                        if (lib.Downloads.Classifiers.TryGetValue(classifier, out var nat) &&
                            !string.IsNullOrEmpty(nat.Path) && !string.IsNullOrEmpty(nat.Url))
                        {
                            var natTarget = Path.Combine(GameDirectory, "libraries",
                                nat.Path!.Replace('/', Path.DirectorySeparatorChar));
                            var natSize = nat.Size ?? -1;
                            if (!File.Exists(natTarget) || (natSize > 0 && new FileInfo(natTarget).Length != natSize))
                            {
                                try
                                {
                                    if (File.Exists(natTarget + ".tmp"))
                                    {
                                        File.Delete(natTarget + ".tmp");
                                    }
                                }
                                catch
                                {
                                    // 忽略
                                }

                                progress?.Report(new InstallProgress
                                {
                                    Stage = "依赖库",
                                    Message = "补下 " + Path.GetFileName(nat.Path),
                                });
                                await _client.DownloadFileAsync(nat.Url!, natTarget, nat.Sha1,
                                    natSize > 0 ? natSize : null, ct).ConfigureAwait(false);
                                repaired++;
                            }
                        }
                    }
                }

                var artifact = lib.Downloads?.Artifact;
                if (artifact == null || string.IsNullOrEmpty(artifact.Path) || string.IsNullOrEmpty(artifact.Url))
                {
                    continue;
                }

                var target = Path.Combine(GameDirectory, "libraries",
                    artifact.Path!.Replace('/', Path.DirectorySeparatorChar));

                var expected = artifact.Size ?? -1;
                if (File.Exists(target) && (expected <= 0 || new FileInfo(target).Length == expected))
                {
                    continue;
                }

                // 以前下到一半留下的 .tmp 会挡住重下（DownloadClient 会直接覆盖，但先清掉更稳）
                try
                {
                    if (File.Exists(target + ".tmp"))
                    {
                        File.Delete(target + ".tmp");
                    }
                }
                catch
                {
                    // 忽略
                }

                progress?.Report(new InstallProgress
                {
                    Stage = "依赖库",
                    Message = "补下 " + Path.GetFileName(artifact.Path),
                });

                await _client.DownloadFileAsync(artifact.Url!, target, artifact.Sha1,
                    expected > 0 ? expected : null, ct).ConfigureAwait(false);
                repaired++;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 体检失败不影响启动
        }

        // 客户端 jar 本身（自己或继承来的）—— 之前只补库不补 jar，jar 缺了游戏根本起不来
        try
        {
            var clientJar = resolved.JarPath;
            if (string.IsNullOrEmpty(clientJar) || !File.Exists(clientJar))
            {
                clientJar = Path.Combine(VersionsDirectory, resolved.ClientVersion, resolved.ClientVersion + ".jar");
            }

            if (!File.Exists(clientJar) || new FileInfo(clientJar).Length < 1024 * 1024)
            {
                var clientJsonPath = Path.Combine(VersionsDirectory, resolved.ClientVersion, resolved.ClientVersion + ".json");
                if (File.Exists(clientJsonPath))
                {
                    using var clientDoc = JsonDocument.Parse(File.ReadAllText(clientJsonPath));
                    if (clientDoc.RootElement.TryGetProperty("downloads", out var downloads) &&
                        downloads.TryGetProperty("client", out var client))
                    {
                        var url = client.TryGetProperty("url", out var u) ? u.GetString() : null;
                        var sha1 = client.TryGetProperty("sha1", out var h) ? h.GetString() : null;
                        var size = client.TryGetProperty("size", out var s) && s.TryGetInt64(out var parsed) ? parsed : (long?)null;
                        if (!string.IsNullOrEmpty(url))
                        {
                            progress?.Report(new InstallProgress { Stage = "客户端", Message = "补下 " + resolved.ClientVersion + ".jar" });
                            try
                            {
                                if (File.Exists(clientJar + ".tmp"))
                                {
                                    File.Delete(clientJar + ".tmp");
                                }
                            }
                            catch
                            {
                                // 忽略
                            }

                            await _client.DownloadFileAsync(url!, clientJar, sha1, size, ct).ConfigureAwait(false);
                            repaired++;
                            progress?.Report(new InstallProgress
                            {
                                Stage = "客户端",
                                Message = $"已补 {resolved.ClientVersion}.jar",
                            });
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 忽略
        }

        // NeoForge / Forge 要的 client-<MCP_VERSION>-srg.jar（安装器 processors 的产物，离线时常常缺）
        try
        {
            var profilePath = Path.Combine(GameDirectory, "versions", resolved.Id, ".meme-install-profile.json");
            if (File.Exists(profilePath))
            {
                var srgId = LoaderInstaller.ReadMcpVersion(File.ReadAllText(profilePath));
                if (!string.IsNullOrWhiteSpace(srgId))
                {
                    var srgDir = Path.Combine(GameDirectory, "libraries", "net", "minecraft", "client", srgId!);
                    var srgJar = Path.Combine(srgDir, $"client-{srgId}-srg.jar");
                    if (!File.Exists(srgJar))
                    {
                        var vanilla = Path.Combine(GameDirectory, "versions", resolved.ClientVersion,
                            resolved.ClientVersion + ".jar");
                        if (File.Exists(vanilla))
                        {
                            Directory.CreateDirectory(srgDir);
                            File.Copy(vanilla, srgJar, overwrite: true);
                            repaired++;
                            progress?.Report(new InstallProgress
                            {
                                Stage = "加载器",
                                Message = $"已补 client-{srgId}-srg.jar",
                            });
                        }
                    }
                }
            }
        }
        catch
        {
            // 忽略
        }

        return repaired;
    }

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
                        var indexUrl = assetIndex.TryGetProperty("url", out var u) ? u.GetString() : null;
                        if (!string.IsNullOrEmpty(indexUrl))
                        {
                            progress?.Report(new InstallProgress { Stage = "资源索引", Message = "补下 assets index" });
                            await _client.DownloadFileAsync(indexUrl!, indexPath, ct: ct).ConfigureAwait(false);
                        }
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
                    await _client.DownloadFileAsync(url!, target, artifact.TryGetProperty("sha1", out var sha1Prop) ? sha1Prop.GetString() : null,
                        expected > 0 ? expected : null, ct).ConfigureAwait(false);
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

    private async Task DownloadAssetObjectsAsync(string indexPath, IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        var text = await File.ReadAllTextAsync(indexPath, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("objects", out var objects))
        {
            return;
        }

        var entries = new List<(string Hash, long Size)>();
        foreach (var prop in objects.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("hash", out var hash))
            {
                var h = hash.GetString();
                if (!string.IsNullOrEmpty(h))
                {
                    var size = prop.Value.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt64(out var parsed)
                        ? parsed
                        : -1;
                    entries.Add((h, size));
                }
            }
        }

        var done = 0;
        var failed = new System.Collections.Concurrent.ConcurrentBag<string>();
        var gate = new SemaphoreSlim(16);
        var tasks = new List<Task>();
        object sync = new();

        foreach (var (hash, size) in entries)
        {
            ct.ThrowIfCancellationRequested();
            await gate.WaitAsync(ct).ConfigureAwait(false);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var relative = Path.Combine(hash[..2], hash);
                    var target = Path.Combine(AssetsDirectory, "objects", relative);
                    if (File.Exists(target))
                    {
                        var info = new FileInfo(target);
                        if (size <= 0 || info.Length == size)
                        {
                            return;   // 大小对得上才算真的下好了
                        }

                        try
                        {
                            info.Delete();
                        }
                        catch
                        {
                            // 删不掉就覆盖写
                        }
                    }

                    await DownloadAssetWithRetryAsync(hash, target, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // 单个资源坏了不该把整个安装拖死，记下来最后再补一次
                    failed.Add(hash);
                }
                finally
                {
                    gate.Release();
                    int current;
                    lock (sync)
                    {
                        current = ++done;
                    }

                    if (current % 25 == 0 || current == entries.Count)
                    {
                        progress?.Report(new InstallProgress
                        {
                            Stage = "资源文件",
                            Done = current,
                            Total = entries.Count,
                            Message = $"assets {current}/{entries.Count}",
                        });
                    }
                }
            }, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (failed.Count == 0)
        {
            return;
        }

        // 第二轮：串行把失败的补一遍（通常这时候是网络抖动，重试就过了）
        foreach (var hash in failed.ToList())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var relative = Path.Combine(hash[..2], hash);
                var target = Path.Combine(AssetsDirectory, "objects", relative);
                await DownloadAssetWithRetryAsync(hash, target, ct).ConfigureAwait(false);
                failed.TryTake(out _);
            }
            catch
            {
                // 还失败就先留着
            }
        }

        if (failed.Count > 0)
        {
            // 资源缺一点通常只是个别音效/语言文件没声，不值得让整个版本装不上
            progress?.Report(new InstallProgress
            {
                Stage = "资源文件",
                Done = entries.Count,
                Total = entries.Count,
                Message = $"有 {failed.Count} 个资源没下下来，先继续",
            });
        }
    }

    /// <summary>一个资源文件，最多试两次（第一次失败停 400ms 再来）。</summary>
    private async Task DownloadAssetWithRetryAsync(string hash, string target, CancellationToken ct)
    {
        var url = $"{ObjectBase}/{hash[..2]}/{hash}";
        try
        {
            await _client.DownloadFileAsync(url, target, hash, ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            await Task.Delay(400, ct).ConfigureAwait(false);
            await _client.DownloadFileAsync(url, target, hash, ct: ct).ConfigureAwait(false);
        }
    }

    private static string? ExtractClientUrl(string jsonText)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            if (doc.RootElement.TryGetProperty("downloads", out var downloads) &&
                downloads.TryGetProperty("client", out var client) &&
                client.TryGetProperty("url", out var url))
            {
                return url.GetString();
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }
}
