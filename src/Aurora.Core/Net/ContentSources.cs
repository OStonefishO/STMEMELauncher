using System.Net.Http;
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

    /// <summary>CurseForge 的 classId。</summary>
    public static int CurseForgeClassId(ContentKind kind) => kind switch
    {
        ContentKind.Modpack => 4471,
        ContentKind.ResourcePack => 12,
        ContentKind.World => 17,
        ContentKind.Shader => 6552,
        _ => 6,
    };
}

public sealed class ContentTag
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public override string ToString() => Name;
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

public sealed class ContentDependency
{
    public string ProjectId { get; init; } = string.Empty;
    public string? VersionId { get; init; }
    public string Type { get; init; } = "required";
    public bool IsRequired => Type.Equals("required", StringComparison.OrdinalIgnoreCase);
}

/// <summary>一个具体版本（带适用 MC 版本、加载器、前置依赖）。</summary>
public sealed class ContentVersion
{
    public string Id { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Channel { get; init; } = "release";
    public List<string> GameVersions { get; init; } = new();
    public List<string> Loaders { get; init; } = new();
    public string Url { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTimeOffset Published { get; init; }
    public List<ContentDependency> Dependencies { get; init; } = new();

    public string LoaderText => Loaders.Count == 0 ? "任意" : string.Join(" / ", Loaders);
    public string SizeText => Size <= 0 ? "" : Size >= 1024 * 1024 ? $"{Size / 1024.0 / 1024.0:0.#} MB" : $"{Size / 1024.0:0.#} KB";
}

public sealed class ContentQuery
{
    public ContentKind Kind { get; init; }
    public string Query { get; init; } = string.Empty;
    public string? McVersion { get; init; }
    public string? Loader { get; init; }
    public string? Category { get; init; }
    public int Offset { get; init; }
    public int PageSize { get; init; } = 20;
}

public sealed class ContentResult
{
    public List<ContentItem> Items { get; init; } = new();
    public int Total { get; init; }
}

public interface IContentSource
{
    string Name { get; }
    ContentKind[] SupportedKinds { get; }
    bool IsAvailable { get; }

    Task<ContentResult> SearchAsync(ContentQuery query, CancellationToken ct = default);

    Task<List<ContentFile>> GetFilesAsync(ContentItem item, string? mcVersion, string? loader,
        CancellationToken ct = default);

    Task<List<ContentTag>> GetCategoriesAsync(ContentKind kind, CancellationToken ct = default);
}

/// <summary>Modrinth 官方 API：搜索 / 分类 / 版本 / 直链，全都不需要 key。</summary>
public sealed class ModrinthClient : IContentSource
{
    private const string Api = "https://api.modrinth.com/v2";

    private readonly DownloadClient _client;

    public ModrinthClient(DownloadClient client) => _client = client;

    public string Name => "Modrinth";

    public bool IsAvailable => true;

    public ContentKind[] SupportedKinds =>
        new[] { ContentKind.Mod, ContentKind.Modpack, ContentKind.Shader, ContentKind.ResourcePack };

    public async Task<ContentResult> SearchAsync(ContentQuery query, CancellationToken ct = default)
    {
        var facets = new List<string> { $"[\"project_type:{ContentKindInfo.ModrinthType(query.Kind)}\"]" };
        if (!string.IsNullOrWhiteSpace(query.McVersion))
        {
            facets.Add($"[\"versions:{query.McVersion}\"]");
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            facets.Add($"[\"categories:{query.Category}\"]");
        }
        else if (query.Kind is ContentKind.Mod or ContentKind.Modpack &&
                 !string.IsNullOrWhiteSpace(query.Loader) && query.Loader != "vanilla")
        {
            facets.Add($"[\"categories:{query.Loader}\"]");
        }

        var url = $"{Api}/search?query={Uri.EscapeDataString(query.Query ?? string.Empty)}" +
                  $"&facets={Uri.EscapeDataString("[" + string.Join(",", facets) + "]")}" +
                  $"&limit={Math.Clamp(query.PageSize, 1, 50)}&offset={Math.Max(0, query.Offset)}&index=relevance";

        var json = await _client.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var result = new ContentResult
        {
            Total = doc.RootElement.TryGetProperty("total_hits", out var total) ? total.GetInt32() : 0,
        };

        if (!doc.RootElement.TryGetProperty("hits", out var hits))
        {
            return result;
        }

        foreach (var hit in hits.EnumerateArray())
        {
            result.Items.Add(new ContentItem
            {
                Source = "Modrinth",
                Kind = query.Kind,
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

        return result;
    }

    public async Task<List<ContentTag>> GetCategoriesAsync(ContentKind kind, CancellationToken ct = default)
    {
        var type = ContentKindInfo.ModrinthType(kind);
        var json = await _client.GetStringAsync($"{Api}/tag/category", ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var tags = new List<ContentTag>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var projectType = GetString(item, "project_type");
            if (!string.Equals(projectType, type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = GetString(item, "name");
            if (!string.IsNullOrEmpty(name))
            {
                tags.Add(new ContentTag { Id = name!, Name = name! });
            }
        }

        return tags;
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

    /// <summary>拉取一个项目下所有版本（不过滤），用于「选择版本」界面。</summary>
    public async Task<List<ContentVersion>> GetVersionsAsync(ContentItem item, CancellationToken ct = default)
    {
        var id = string.IsNullOrEmpty(item.Id) ? item.Slug : item.Id;
        var json = await _client.GetStringAsync($"{Api}/project/{id}/version", ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var result = new List<ContentVersion>();
        foreach (var version in doc.RootElement.EnumerateArray())
        {
            string url = string.Empty, fileName = string.Empty;
            long size = 0;

            if (version.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
            {
                JsonElement? chosen = null;
                foreach (var file in files.EnumerateArray())
                {
                    var primary = file.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.True;
                    chosen ??= file;
                    if (primary)
                    {
                        chosen = file;
                        break;
                    }
                }

                if (chosen is { } f)
                {
                    url = GetString(f, "url") ?? string.Empty;
                    fileName = GetString(f, "filename") ?? string.Empty;
                    size = f.TryGetProperty("size", out var fs) ? fs.GetInt64() : 0;
                }
            }

            var deps = new List<ContentDependency>();
            if (version.TryGetProperty("dependencies", out var dependencyArray) &&
                dependencyArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var d in dependencyArray.EnumerateArray())
                {
                    var projectId = GetString(d, "project_id");
                    if (string.IsNullOrEmpty(projectId))
                    {
                        continue;
                    }

                    deps.Add(new ContentDependency
                    {
                        ProjectId = projectId!,
                        VersionId = GetString(d, "version_id"),
                        Type = GetString(d, "dependency_type") ?? "required",
                    });
                }
            }

            result.Add(new ContentVersion
            {
                Id = GetString(version, "id") ?? string.Empty,
                Number = GetString(version, "version_number") ?? string.Empty,
                Name = GetString(version, "name") ?? string.Empty,
                Channel = GetString(version, "version_type") ?? "release",
                GameVersions = StringList(version, "game_versions"),
                Loaders = StringList(version, "loaders"),
                Url = url,
                FileName = fileName,
                Size = size,
                Published = DateTimeOffset.TryParse(GetString(version, "date_published"), out var dt) ? dt : default,
                Dependencies = deps,
            });
        }

        return result.OrderByDescending(v => v.Published).ToList();
    }

    /// <summary>按 id / slug 取项目信息（用于解析前置模组的名字）。</summary>
    public async Task<ContentItem?> GetProjectAsync(string idOrSlug, CancellationToken ct = default)
    {
        try
        {
            var json = await _client.GetStringAsync($"{Api}/project/{idOrSlug}", ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new ContentItem
            {
                Source = "Modrinth",
                Kind = ContentKind.Mod,
                Id = GetString(root, "id") ?? idOrSlug,
                Slug = GetString(root, "slug") ?? string.Empty,
                Title = GetString(root, "title") ?? idOrSlug,
                Summary = GetString(root, "description") ?? string.Empty,
                IconUrl = GetString(root, "icon_url"),
                Downloads = root.TryGetProperty("downloads", out var d) ? d.GetInt64() : 0,
                Categories = StringList(root, "categories"),
            };
        }
        catch
        {
            return null;
        }
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
/// CurseForge 官方 API。需要 key（console.curseforge.com 免费申请），
/// 没有 key 时这个源会显示为「未配置」，其余功能照常。
/// </summary>
public sealed class CurseForgeClient : IContentSource
{
    private const string Api = "https://api.curseforge.com/v1";

    private readonly HttpClient _http;

    public CurseForgeClient(string? apiKey)
    {
        ApiKey = apiKey?.Trim() ?? string.Empty;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MemeLauncher/0.4");
        if (!string.IsNullOrEmpty(ApiKey))
        {
            _http.DefaultRequestHeaders.Add("x-api-key", ApiKey);
        }
    }

    public string ApiKey { get; }

    public string Name => "CurseForge";

    public bool IsAvailable => !string.IsNullOrEmpty(ApiKey);

    public ContentKind[] SupportedKinds =>
        new[]
        {
            ContentKind.Mod, ContentKind.Modpack, ContentKind.ResourcePack, ContentKind.Shader, ContentKind.World,
        };

    public async Task<ContentResult> SearchAsync(ContentQuery query, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            return new ContentResult();
        }

        var url = $"{Api}/mods/search?gameId=432&classId={ContentKindInfo.CurseForgeClassId(query.Kind)}" +
                  $"&index={Math.Max(0, query.Offset)}&pageSize={Math.Clamp(query.PageSize, 1, 50)}" +
                  "&sortField=2&sortOrder=desc";

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            url += "&searchFilter=" + Uri.EscapeDataString(query.Query);
        }

        if (!string.IsNullOrWhiteSpace(query.McVersion))
        {
            url += "&gameVersion=" + Uri.EscapeDataString(query.McVersion);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            url += "&categoryId=" + Uri.EscapeDataString(query.Category);
        }

        var json = await GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = new ContentResult();
        if (root.TryGetProperty("pagination", out var pagination) &&
            pagination.TryGetProperty("totalCount", out var total))
        {
            result = new ContentResult { Total = total.GetInt32() };
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var mod in data.EnumerateArray())
        {
            var logo = mod.TryGetProperty("logo", out var l) && l.ValueKind == JsonValueKind.Object
                ? ModrinthClient.GetString(l, "thumbnailUrl")
                : null;

            var categories = new List<string>();
            if (mod.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in cats.EnumerateArray())
                {
                    var name = ModrinthClient.GetString(c, "name");
                    if (!string.IsNullOrEmpty(name))
                    {
                        categories.Add(name!);
                    }
                }
            }

            var links = mod.TryGetProperty("links", out var lk) && lk.ValueKind == JsonValueKind.Object
                ? ModrinthClient.GetString(lk, "websiteUrl")
                : null;

            result.Items.Add(new ContentItem
            {
                Source = "CurseForge",
                Kind = query.Kind,
                Id = mod.TryGetProperty("id", out var id) ? id.GetInt64().ToString() : string.Empty,
                Slug = mod.TryGetProperty("slug", out var slug) ? slug.GetString() ?? string.Empty : string.Empty,
                Title = ModrinthClient.GetString(mod, "name") ?? string.Empty,
                Summary = ModrinthClient.GetString(mod, "summary") ?? string.Empty,
                Author = links ?? string.Empty,
                IconUrl = logo,
                Downloads = mod.TryGetProperty("downloadCount", out var dc) ? dc.GetInt64() : 0,
                Categories = categories,
            });
        }

        return result;
    }

    public async Task<List<ContentTag>> GetCategoriesAsync(ContentKind kind, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            return new List<ContentTag>();
        }

        var url = $"{Api}/categories?gameId=432&classId={ContentKindInfo.CurseForgeClassId(kind)}";
        var json = await GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var tags = new List<ContentTag>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return tags;
        }

        foreach (var item in data.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var i) ? i.GetInt64().ToString() : string.Empty;
            var name = ModrinthClient.GetString(item, "name");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
            {
                tags.Add(new ContentTag { Id = id, Name = name! });
            }
        }

        return tags;
    }

    public async Task<List<ContentFile>> GetFilesAsync(ContentItem item, string? mcVersion, string? loader,
        CancellationToken ct = default)
    {
        if (!IsAvailable || string.IsNullOrEmpty(item.Id))
        {
            return new List<ContentFile>();
        }

        var url = $"{Api}/mods/{item.Id}/files?pageSize=50";
        if (!string.IsNullOrWhiteSpace(mcVersion))
        {
            url += "&gameVersion=" + Uri.EscapeDataString(mcVersion);
        }

        var json = await GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var files = new List<ContentFile>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return files;
        }

        foreach (var file in data.EnumerateArray())
        {
            var fileName = ModrinthClient.GetString(file, "fileName");
            if (string.IsNullOrEmpty(fileName))
            {
                continue;
            }

            var id = file.TryGetProperty("id", out var fid) ? fid.GetInt64() : 0;
            var download = ModrinthClient.GetString(file, "downloadUrl");
            if (string.IsNullOrEmpty(download) && id > 0)
            {
                download = BuildCdnUrl(id, fileName!);
            }

            if (string.IsNullOrEmpty(download))
            {
                continue;
            }

            files.Add(new ContentFile
            {
                Id = id.ToString(),
                FileName = fileName!,
                DisplayName = ModrinthClient.GetString(file, "displayName") ?? string.Empty,
                Url = download!,
                Size = file.TryGetProperty("fileLength", out var len) ? len.GetInt64() : 0,
                GameVersions = ModrinthClient.StringList(file, "gameVersions"),
                IsPrimary = true,
            });
        }

        return files;
    }

    /// <summary>forgecdn 的稳定直链规则。</summary>
    private static string BuildCdnUrl(long fileId, string fileName) =>
        $"https://mediafilez.forgecdn.net/files/{fileId / 1000}/{fileId % 1000}/{fileName}";

    private async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"CurseForge 返回 {(int)response.StatusCode}: {Trim(body, 160)}");
        }

        return body;
    }

    private static string Trim(string text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max] + "…";
}
