using System.Text.Json;
using Aurora.Core.Infrastructure;

namespace Aurora.Core.Net;

public sealed class UpdateEntry
{
    public string Version { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset PublishedAt { get; init; }
    public List<string> Notes { get; init; } = new();
    public List<string> Images { get; init; } = new();

    public string DateText => PublishedAt == default ? string.Empty : PublishedAt.ToLocalTime().ToString("yyyy-MM-dd");
}

/// <summary>
/// 更新日志：直接读 GitHub Releases。
/// 每次发版把预览图作为 release 资产传上去，启动器这边就能拿到“带图更新日志”。
/// </summary>
public sealed class UpdateFeed
{
    private const string ApiBase = "https://api.github.com";

    private readonly DownloadClient _client;

    public UpdateFeed(DownloadClient client, string repository = "OStonefishO/STMEMELauncher")
    {
        _client = client;
        Repository = repository;
    }

    public string Repository { get; }

    public async Task<List<UpdateEntry>> FetchAsync(CancellationToken ct = default)
    {
        var url = $"{ApiBase}/repos/{Repository}/releases?per_page=20";
        var json = await _client.GetStringAsync(url, ct).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(json);
        var list = new List<UpdateEntry>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var release in doc.RootElement.EnumerateArray())
        {
            var tag = GetString(release, "tag_name") ?? string.Empty;
            var name = GetString(release, "name");
            var body = GetString(release, "body") ?? string.Empty;
            var published = GetString(release, "published_at");

            var images = new List<string>();
            if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = GetString(asset, "name") ?? string.Empty;
                    var download = GetString(asset, "browser_download_url");
                    if (!string.IsNullOrEmpty(download) &&
                        (assetName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                         assetName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                         assetName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)))
                    {
                        images.Add(download!);
                    }
                }
            }

            list.Add(new UpdateEntry
            {
                Version = tag,
                Title = string.IsNullOrWhiteSpace(name) ? tag : name!,
                PublishedAt = DateTimeOffset.TryParse(published, out var dt) ? dt : default,
                Notes = CleanNotes(body),
                Images = images,
            });
        }

        return list;
    }

    /// <summary>
    /// 下载 release 里的预览图。github.com 在部分网络下不可达，
    /// 这里按「直连 → gh-proxy.com → ghfast.top」的顺序回退。
    /// </summary>
    public async Task<byte[]?> DownloadImageAsync(string url, CancellationToken ct = default)
    {
        // github.com 在部分网络下是黑洞连接：先给 8 秒，连不上就立刻换代理，
        // 否则会一直卡到 HttpClient 的 5 分钟超时。
        var candidates = new (string Url, int TimeoutSeconds)[]
        {
            (url, 8),
            ("https://gh-proxy.com/" + url, 25),
            ("https://ghfast.top/" + url, 25),
        };

        foreach (var (candidate, seconds) in candidates)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
                return await _client.GetBytesAsync(candidate, timeout.Token).ConfigureAwait(false);
            }
            catch
            {
                // 换下一个通道
            }
        }

        return null;
    }

    /// <summary>把 markdown 粗加工成能直接显示的纯文本行。</summary>
    private static List<string> CleanNotes(string body)
    {
        var lines = new List<string>();
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line == "---")
            {
                continue;
            }

            line = line.TrimStart('#').Trim();
            line = line.Replace("**", string.Empty).Replace("`", string.Empty);
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                line = "· " + line[2..].Trim();
            }
            else if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                line = line[4..];
            }

            if (line.Length == 0)
            {
                continue;
            }

            lines.Add(line);
            if (lines.Count >= 40)
            {
                break;
            }
        }

        return lines;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
