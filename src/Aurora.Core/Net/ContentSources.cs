using System.Text.Json;

namespace Aurora.Core.Net;

public enum ContentKind
{
    Mod,
    Modpack,
    Shader,
    ResourcePack,
    World,
}

public static class ContentKindInfo
{
    public static string Folder(ContentKind kind) => kind switch
    {
        ContentKind.Mod => "mods",
        ContentKind.Shader => "shaderpacks",
        ContentKind.ResourcePack => "resourcepacks",
        ContentKind.World => "saves",
        _ => "modpacks",
    };

    public static string Label(ContentKind kind) => kind switch
    {
        ContentKind.Mod => "模组",
        ContentKind.Modpack => "整合包",
        ContentKind.Shader => "光影",
        ContentKind.ResourcePack => "资源包",
        _ => "地图",
    };

    public static string ModrinthType(ContentKind kind) => kind switch
    {
        ContentKind.Mod => "mod",
        ContentKind.Modpack => "modpack",
        ContentKind.Shader => "shader",
        ContentKind.ResourcePack => "resourcepack",
        _ => "mod",
    };
}

public sealed class ContentItem
{
    public string Source { get; init; } = string.Empty;
    public ContentKind Kind { get; init; }
    public string Id { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
    public long Downloads { get; init; }
    public List<string> Categories { get; init; } = new();
    public string? LoaderHint { get; init; }

    public string DownloadText => Downloads >= 1_000_000
        ? $"{Downloads / 1_000_000.0:0.#}M"
        : Downloads >= 1000
            ? $"{Downloads / 1000.0:0.#}k"
            : Downloads.ToString();
}

public sealed class ContentFile
{
    public string Id { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public long Size { get; init; }
    public List<string> GameVersions { get; init; } = new();
    public List<string> Loaders { get; init; } = new();
    public bool IsPrimary { get; init; }
}

public interface IContentSource
{
    string Name { get; }
    ContentKind[] SupportedKinds { get; }

    Task<List<ContentItem>> SearchAsync(ContentKind kind, string query, string? mcVersion, string? loader,
        int limit, CancellationToken ct = default);

    Task<List<ContentFile>> GetFilesAsync(ContentItem item, string? mcVersion, string? loader,
        CancellationToken ct = default);
}

/// <summary>Modrinth 官方 API：搜索 + 版本 + 直链下载，不需要 key。</summary>
public sealed class ModrinthClient : IContentSource
{
    private const string Api = "https://api.modrinth.com/v2";

    private readonly DownloadClient _client;

    public ModrinthClient(DownloadClient client) => _client = client;

    public string Name => "Modrinth";

    public ContentKind[] SupportedKinds =>
        new[] { ContentKind.Mod, ContentKind.Modpack, ContentKind.Shader, ContentKind.ResourcePack };

    public async Task<List<ContentItem>> SearchAsync(ContentKind kind, string query, string? mcVersion,
        string? loader, int limit, CancellationToken ct = default)
    {
        var facets = new List<string> { $"[\"project_type:{ContentKindInfo.ModrinthType(kind)}\"]" };
        if (!string.IsNullOrWhiteSpace(mcVersion))
        {
            facets.Add($"[\"versions:{mcVersion}\"]");
        }

        if (kind is ContentKind.Mod or ContentKind.Modpack && !string.IsNullOrWhiteSpace(loader) &&
            loader != "vanilla")
        {
            facets.Add($"[\"categories:{loader}\"]");
        }

        var url = $"{Api}/search?query={Uri.EscapeDataString(query ?? string.Empty)}" +
                  $"&facets={Uri.EscapeDataString("[" + string.Join(",", facets) + "]")}" +
                  $"&limit={Math.Clamp(limit, 1, 50)}&index=relevance";

        var json = await _client.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var items = new List<ContentItem>();
        if (!doc.RootElement.TryGetProperty("hits", out var hits))
        {
            return items;
        }

        foreach (var hit in hits.EnumerateArray())
        {
            items.Add(new ContentItem
            {
                Source = "Modrinth",
                Kind = kind,
                Id = GetString(hit, "project_id") ?? string.Empty,
                Slug = GetString(hit, "slug") ?? string.Empty,
                Title = GetString(hit, "title") ?? string.Empty,
                Summary = GetString(hit, "description") ?? string.Empty,
                Author = GetString(hit, "author") ?? string.Empty,
                IconUrl = GetString(hit, "icon_url"),
                Downloads = hit.TryGetProperty("downloads", out var d) ? d.GetInt64() : 0,
                Categories = StringList(hit, "display_categories"),
            });
        }

        return items;
    }

    public async Task<List<ContentFile>> GetFilesAsync(ContentItem item, string? mcVersion, string? loader,
        CancellationToken ct = default)
    {
        var id = string.IsNullOrEmpty(item.Id) ? item.Slug : item.Id;
        var url = $"{Api}/project/{id}/version";
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(mcVersion))
        {
            query.Add("game_versions=" + Uri.EscapeDataString($"[\"{mcVersion}\"]"));
        }

        if (item.Kind is ContentKind.Mod or ContentKind.Modpack && !string.IsNullOrWhiteSpace(loader) &&
            loader != "vanilla")
        {
            query.Add("loaders=" + Uri.EscapeDataString($"[\"{loader}\"]"));
        }

        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        var json = await _client.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var files = new List<ContentFile>();

        foreach (var version in doc.RootElement.EnumerateArray())
        {
            var versionName = GetString(version, "version_number") ?? GetString(version, "name") ?? string.Empty;
            if (!version.TryGetProperty("files", out var fileArray))
            {
                continue;
            }

            foreach (var file in fileArray.EnumerateArray())
            {
                var primary = file.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.True;
                var fileName = GetString(file, "filename") ?? string.Empty;
                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                files.Add(new ContentFile
                {
                    Id = GetString(file, "url") ?? fileName,
                    FileName = fileName,
                    DisplayName = versionName,
                    Url = GetString(file, "url") ?? string.Empty,
                    Size = file.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                    GameVersions = StringList(version, "game_versions"),
                    Loaders = StringList(version, "loaders"),
                    IsPrimary = primary,
                });
            }
        }

        return files.OrderByDescending(f => f.IsPrimary).ToList();
    }

    internal static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    internal static List<string> StringList(JsonElement element, string name)
    {
        var result = new List<string>();
        if (element.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    result.Add(item.GetString()!);
                }
            }
        }

        return result;
    }
}

/// <summary>
/// CurseForge：官方 API 需要 key，这里走公开的 cfwidget 元数据 + forgecdn 直链。
/// 不支持关键词搜索，按「slug / 链接」精确导入。
/// </summary>
public sealed class CurseForgeClient : IContentSource
{
    private readonly DownloadClient _client;

    public CurseForgeClient(DownloadClient client) => _client = client;

    public string Name => "CurseForge";

    public ContentKind[] SupportedKinds =>
        new[] { ContentKind.Mod, ContentKind.Modpack, ContentKind.ResourcePack, ContentKind.World };

    public async Task<List<ContentItem>> SearchAsync(ContentKind kind, string query, string? mcVersion,
        string? loader, int limit, CancellationToken ct = default)
    {
        // 只接受 slug 或 curseforge 链接
        var slug = ExtractSlug(query);
        if (string.IsNullOrEmpty(slug))
        {
            return new List<ContentItem>();
        }

        var item = await FetchAsync(kind, slug!, ct).ConfigureAwait(false);
        return item == null ? new List<ContentItem>() : new List<ContentItem> { item };
    }

    public async Task<List<ContentFile>> GetFilesAsync(ContentItem item, string? mcVersion, string? loader,
        CancellationToken ct = default)
    {
        var payload = await FetchRawAsync(item.Kind, item.Slug, ct).ConfigureAwait(false);
        if (payload == null)
        {
            return new List<ContentFile>();
        }

        var files = new List<ContentFile>();
        if (!payload.Value.TryGetProperty("files", out var arr))
        {
            return files;
        }

        foreach (var file in arr.EnumerateArray())
        {
            var name = ModrinthClient.GetString(file, "name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var versions = ModrinthClient.StringList(file, "versions");
            if (!string.IsNullOrWhiteSpace(mcVersion) && versions.Count > 0 &&
                !versions.Contains(mcVersion, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = file.TryGetProperty("id", out var idEl) ? idEl.GetInt64() : 0;
            var url = id > 0 ? BuildCdnUrl(id, name!) : null;
            if (url == null)
            {
                continue;
            }

            files.Add(new ContentFile
            {
                Id = id.ToString(),
                FileName = name!,
                DisplayName = ModrinthClient.GetString(file, "display") ?? string.Empty,
                Url = url,
                Size = file.TryGetProperty("filesize", out var fs) ? fs.GetInt64() : 0,
                GameVersions = versions,
                Loaders = versions.Where(v => v is "Forge" or "Fabric" or "NeoForge" or "Quilt").ToList(),
                IsPrimary = true,
            });
        }

        return files;
    }

    /// <summary>forgecdn 的稳定直链规则：files/{id/1000}/{id%1000}/{filename}</summary>
    private static string BuildCdnUrl(long fileId, string fileName)
    {
        var folder = fileId / 1000;
        var remainder = fileId % 1000;
        return $"https://mediafilez.forgecdn.net/files/{folder}/{remainder}/{fileName}";
    }

    private async Task<ContentItem?> FetchAsync(ContentKind kind, string slug, CancellationToken ct)
    {
        var payload = await FetchRawAsync(kind, slug, ct).ConfigureAwait(false);
        if (payload == null)
        {
            return null;
        }

        var root = payload.Value;
        return new ContentItem
        {
            Source = "CurseForge",
            Kind = kind,
            Id = root.TryGetProperty("id", out var id) ? id.GetInt64().ToString() : slug,
            Slug = slug,
            Title = ModrinthClient.GetString(root, "title") ?? slug,
            Summary = ModrinthClient.GetString(root, "summary") ?? string.Empty,
            Author = string.Empty,
            IconUrl = ModrinthClient.GetString(root, "thumbnail"),
            Downloads = root.TryGetProperty("downloads", out var d) && d.TryGetInt64(out var dval) ? dval : 0,
        };
    }

    private async Task<JsonElement?> FetchRawAsync(ContentKind kind, string slug, CancellationToken ct)
    {
        var type = kind switch
        {
            ContentKind.ResourcePack => "texture-packs",
            ContentKind.World => "worlds",
            _ => "mc-mods",
        };

        try
        {
            var json = await _client.GetStringAsync($"https://api.cfwidget.com/minecraft/{type}/{slug}", ct)
                .ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    internal static string? ExtractSlug(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var text = query.Trim();
        var index = text.IndexOf("curseforge.com/", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var rest = text[(index + "curseforge.com/".Length)..].Trim('/');
            var parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return parts[1];
            }
        }

        return text.Contains(' ') || text.Contains('/') ? null : text;
    }
}
