using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aurora.Core.Net;

/// <summary>MC 百科里的一条模组记录。</summary>
public sealed class McmodEntry
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// 用 MC 百科给模组「查中文」：搜到就直接拿它的中文名 + 中文简介，
/// 比机器翻译准，也能拿到对应的 /class/xxxx.html 详情页链接。
/// </summary>
public static class McmodLookup
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly ConcurrentDictionary<string, McmodEntry> Cache = LoadCache();
    private static readonly SemaphoreSlim Gate = new(2);
    private static readonly object CacheLock = new();
    private static int _writes;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
        return client;
    }

    private static string CachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MEMELauncher", "mcmod-cache.json");

    private static ConcurrentDictionary<string, McmodEntry> LoadCache()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MEMELauncher", "mcmod-cache.json");

            if (File.Exists(path))
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, McmodEntry>>(File.ReadAllText(path));
                if (map != null)
                {
                    return new ConcurrentDictionary<string, McmodEntry>(map);
                }
            }
        }
        catch
        {
            // 缓存坏了就当没有
        }

        return new ConcurrentDictionary<string, McmodEntry>();
    }

    private static void SaveCache()
    {
        try
        {
            var path = CachePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(Cache));
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>从「钠 (Sodium)」这种名字里抽出英文名，用来做中文搜索的回退。</summary>
    public static string? EnglishName(McmodEntry? entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
        {
            return null;
        }

        var match = Regex.Match(entry.Name, @"\(([^()]{2,60})\)");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>中文名反查英文名（用于用中文搜索 Modrinth 这类纯英文源）。</summary>
    public static async Task<string?> ToEnglishAsync(string chinese, CancellationToken ct = default)
    {
        var entry = await FindAsync(chinese, ct).ConfigureAwait(false);
        return EnglishName(entry);
    }
    public static McmodEntry? Cached(string title) =>
        !string.IsNullOrWhiteSpace(title) && Cache.TryGetValue(title.Trim(), out var hit) ? hit : null;

    /// <summary>按模组名去 MC 百科搜，拿中文名 / 中文简介 / 详情页链接。</summary>
    public static async Task<McmodEntry?> FindAsync(string title, CancellationToken ct = default)
    {
        var key = title?.Trim() ?? string.Empty;
        if (key.Length < 2)
        {
            return null;
        }

        if (Cache.TryGetValue(key, out var hit))
        {
            return hit;
        }

        var query = Regex.Replace(key, @"[\[\(（].*?[\]\)）]", string.Empty).Trim();
        if (query.Length == 0)
        {
            query = key;
        }

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                // 搜太密会被百科挡，逐次退避
                await Task.Delay(200 + attempt * 700, ct).ConfigureAwait(false);

                var html = await FetchAsync(query, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(html))
                {
                    continue;
                }

                var entry = Parse(html);
                if (entry == null)
                {
                    continue;
                }

                Cache[key] = entry;
                lock (CacheLock)
                {
                    SaveCache();   // 条目不多，每次存一下，下次秒开
                }

                return entry;
            }

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<string?> FetchAsync(string query, CancellationToken ct)
    {
        try
        {
            var url = "https://search.mcmod.cn/s?key=" + Uri.EscapeDataString(query);
            using var response = await Http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static McmodEntry? Parse(string html)
    {
        // 只看第一条搜索结果，别去匹配侧边栏推荐
        var index = html.IndexOf("result-item", StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var scope = html[index..];
        var match = Regex.Match(
            scope,
            @"href=""(?<url>https?://www\.mcmod\.cn/class/(?<id>\d+)\.html)""[^>]*>(?<name>.*?)</a>",
            RegexOptions.Singleline);

        if (!match.Success)
        {
            return null;
        }

        var bodyMatch = Regex.Match(scope, @"class=""body"">(?<body>.*?)</div>", RegexOptions.Singleline);
        var summary = bodyMatch.Success ? Clean(bodyMatch.Groups["body"].Value) : string.Empty;
        if (summary.Length > 200)
        {
            summary = summary[..200] + "…";
        }

        return new McmodEntry
        {
            Id = match.Groups["id"].Value,
            Url = match.Groups["url"].Value,
            Name = Clean(match.Groups["name"].Value),
            Summary = summary,
        };
    }

    /// <summary>去掉 HTML 标签、百科的 [h1=…] 标记和常见实体。</summary>
    private static string Clean(string html)
    {
        var text = Regex.Replace(html, "<.*?>", string.Empty);
        text = Regex.Replace(text, @"\[[^\[\]]{0,40}\]", string.Empty);
        text = text.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        text = text.Replace("&quot;", "\"");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
