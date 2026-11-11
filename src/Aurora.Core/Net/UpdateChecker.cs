using System.Text.Json;

namespace Aurora.Core.Net;

public sealed class ReleaseAsset
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public long Size { get; init; }
}

public sealed class ReleaseInfo
{
    public string Tag { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTimeOffset PublishedAt { get; init; }
    public string HtmlUrl { get; init; } = string.Empty;
    public List<ReleaseAsset> Assets { get; init; } = new();
    public string Source { get; init; } = string.Empty;

    public string VersionText => Tag.TrimStart('v', 'V');
}

/// <summary>启动器自身的更新检查：读 GitHub Releases 的 latest。</summary>
public sealed class UpdateChecker
{
    private readonly DownloadClient _client;
    private readonly string _repository;
    private readonly string _giteeRepository;

    public UpdateChecker(DownloadClient client, string repository = "OStonefishO/STMEMELauncher",
        string giteeRepository = "")
    {
        _client = client;
        _repository = repository;
        _giteeRepository = giteeRepository;
    }

    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct = default)
        => await GetLatestAsync(null, ct).ConfigureAwait(false);

    /// <summary>
    /// 先问 Gitee：有比当前版本更新的就直接用，不碰 GitHub（国内 GitHub API 经常超时）。
    /// Gitee 没新版本 / 不可达时，再问 GitHub，两个源都有就取新的那个。
    /// </summary>
    public async Task<ReleaseInfo?> GetLatestAsync(string? currentVersion, CancellationToken ct = default)
    {
        ReleaseInfo? gitee = null;
        if (!string.IsNullOrEmpty(_giteeRepository))
        {
            gitee = await GetFromGiteeAsync(ct).ConfigureAwait(false);
            if (gitee != null && !string.IsNullOrWhiteSpace(currentVersion) &&
                IsNewer(gitee.Tag, currentVersion!))
            {
                return gitee;
            }

            if (gitee != null && string.IsNullOrWhiteSpace(currentVersion))
            {
                return gitee;
            }
        }

        var github = await GetFromGithubAsync(ct).ConfigureAwait(false);

        if (gitee == null)
        {
            return github;
        }

        if (github == null)
        {
            return gitee;
        }

        return IsNewer(github.Tag, gitee.Tag) ? github : gitee;
    }

    /// <summary>GitHub 不通时用 Gitee 的镜像 Release 拿版本信息。</summary>
    private async Task<ReleaseInfo?> GetFromGiteeAsync(CancellationToken ct)
    {
        try
        {
            var json = await _client
                .GetStringAsync($"https://gitee.com/api/v5/repos/{_giteeRepository}/releases/latest", ct)
                .ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var assets = new List<ReleaseAsset>();
            if (root.TryGetProperty("assets", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    assets.Add(new ReleaseAsset
                    {
                        Name = Get(item, "name") ?? string.Empty,
                        Url = Get(item, "browser_download_url") ?? string.Empty,
                        Size = item.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                    });
                }
            }

            return new ReleaseInfo
            {
                Tag = Get(root, "tag_name") ?? string.Empty,
                Name = Get(root, "name") ?? string.Empty,
                Body = Get(root, "body") ?? string.Empty,
                PublishedAt = DateTimeOffset.TryParse(Get(root, "created_at"), out var dt) ? dt : default,
                HtmlUrl = Get(root, "html_url") ?? string.Empty,
                Assets = assets,
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<ReleaseInfo?> GetFromGithubAsync(CancellationToken ct)
    {
        try
        {
            var json = await _client
                .GetStringAsync($"https://api.github.com/repos/{_repository}/releases/latest", ct)
                .ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var assets = new List<ReleaseAsset>();
            if (root.TryGetProperty("assets", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    assets.Add(new ReleaseAsset
                    {
                        Name = Get(item, "name") ?? string.Empty,
                        Url = Get(item, "browser_download_url") ?? string.Empty,
                        Size = item.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                    });
                }
            }

            return new ReleaseInfo
            {
                Tag = Get(root, "tag_name") ?? string.Empty,
                Name = Get(root, "name") ?? string.Empty,
                Body = Get(root, "body") ?? string.Empty,
                PublishedAt = DateTimeOffset.TryParse(Get(root, "published_at"), out var dt) ? dt : default,
                HtmlUrl = Get(root, "html_url") ?? string.Empty,
                Assets = assets,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>比较 tag（例如 v0.5.1）和当前版本，返回是否有更新。</summary>
    public static bool IsNewer(string? tag, string current)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var remote = Parse(tag);
        var local = Parse(current);
        return remote != null && local != null && remote > local;
    }

    private static Version? Parse(string text)
    {
        var clean = text.Trim().TrimStart('v', 'V');
        var dash = clean.IndexOfAny(new[] { '-', '+' });
        if (dash > 0)
        {
            clean = clean[..dash];
        }

        return Version.TryParse(clean, out var version) ? version : null;
    }

    private static string? Get(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
