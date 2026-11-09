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

        progress?.Report(new InstallProgress { Stage = "完成", Done = 1, Total = 1, Message = remote.Id + " 安装完成" });
        return jsonPath;
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

        var entries = new List<string>();
        foreach (var prop in objects.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("hash", out var hash))
            {
                var h = hash.GetString();
                if (!string.IsNullOrEmpty(h))
                {
                    entries.Add(h);
                }
            }
        }

        var done = 0;
        var gate = new SemaphoreSlim(16);
        var tasks = new List<Task>();
        object sync = new();

        foreach (var hash in entries)
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
                        return;
                    }

                    var url = $"{ObjectBase}/{hash[..2]}/{hash}";
                    await _client.DownloadFileAsync(url, target, hash, ct: ct).ConfigureAwait(false);
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
