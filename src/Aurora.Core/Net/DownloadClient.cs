using System.Net;
using System.Security.Cryptography;

namespace Aurora.Core.Net;

/// <summary>带镜像改写、重试和 SHA1 校验的下载器。</summary>
public sealed class DownloadClient : IDisposable
{
    private readonly HttpClient _http;
    public const string Bmclapi = "https://bmclapi2.bangbang93.com";

    /// <summary>单个地址一次尝试最多等多久，卡住就换下一个通道。</summary>
    private static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(90);

    /// <summary>下载文件时，多久没有新数据就判定卡死。</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(45);

    public DownloadClient(bool useMirror = true)
    {
        UseMirror = useMirror;
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            MaxConnectionsPerServer = 16,
        };
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Memelauncher/0.2 (+https://localhost)");
    }

    public bool UseMirror { get; set; }

    /// <summary>自建镜像站（http://127.0.0.1:8123 这种），有值时优先走它。</summary>
    public string MirrorBaseUrl { get; set; } = string.Empty;

    /// <summary>进度回调（已下载字节 / 总字节，可能为 -1）。</summary>
    public IProgress<(long Done, long Total)>? Progress { get; set; }

    public string MapUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        var custom = MirrorBaseUrl?.Trim();
        if (!string.IsNullOrEmpty(custom))
        {
            var b = custom.TrimEnd('/');
            return url
                .Replace("https://piston-meta.mojang.com", b + "/m/piston", StringComparison.Ordinal)
                .Replace("https://piston-data.mojang.com", b + "/m/piston", StringComparison.Ordinal)
                .Replace("https://launchermeta.mojang.com", b + "/m/piston", StringComparison.Ordinal)
                .Replace("https://launcher.mojang.com", b + "/m/piston", StringComparison.Ordinal)
                .Replace("https://libraries.minecraft.net", b + "/m/libraries", StringComparison.Ordinal)
                .Replace("https://resources.download.minecraft.net", b + "/m/assets", StringComparison.Ordinal)
                .Replace("https://meta.fabricmc.net", b + "/m/fabric", StringComparison.Ordinal)
                .Replace("https://maven.neoforged.net/releases", b + "/m/neoforge", StringComparison.Ordinal)
                .Replace("https://maven.neoforged.net", b + "/m/neoforge", StringComparison.Ordinal)
                .Replace("https://maven.minecraftforge.net", b + "/m/forge", StringComparison.Ordinal);
        }

        if (!UseMirror)
        {
            return url;
        }

        return url
            .Replace("https://piston-meta.mojang.com", Bmclapi, StringComparison.Ordinal)
            .Replace("https://piston-data.mojang.com", Bmclapi, StringComparison.Ordinal)
            .Replace("https://launchermeta.mojang.com", Bmclapi, StringComparison.Ordinal)
            .Replace("https://launcher.mojang.com", Bmclapi, StringComparison.Ordinal)
            .Replace("https://libraries.minecraft.net", Bmclapi + "/libraries", StringComparison.Ordinal)
            .Replace("https://resources.download.minecraft.net", Bmclapi + "/assets", StringComparison.Ordinal)
            .Replace("https://meta.fabricmc.net", Bmclapi + "/fabric-meta", StringComparison.Ordinal)
            .Replace("https://maven.neoforged.net/releases", Bmclapi + "/maven", StringComparison.Ordinal)
            .Replace("https://maven.neoforged.net", Bmclapi + "/maven", StringComparison.Ordinal)
            .Replace("https://files.minecraftforge.net/maven", "https://maven.aliyun.com/repository/public", StringComparison.Ordinal);
    }

    public async Task<string> GetStringAsync(string url, CancellationToken ct = default)
    {
        var bytes = await GetBytesAsync(url, ct).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public async Task<byte[]> GetBytesAsync(string url, CancellationToken ct = default)
    {
        Exception? last = null;
        foreach (var candidate in Candidates(url))
        {
            try
            {
                // 单个地址最多等 90 秒，卡住的连接直接放弃换下一个，别让玩家对着 100% 干等
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attempt.CancelAfter(PerAttemptTimeout);

                using var response = await _http.GetAsync(candidate, HttpCompletionOption.ResponseHeadersRead, attempt.Token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(attempt.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new IOException($"下载失败: {url} ({last?.Message})", last);
    }

    public async Task DownloadFileAsync(string url, string targetPath, string? sha1 = null, long? size = null,
        CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(targetPath))
        {
            var length = new FileInfo(targetPath).Length;
            var valid = sha1 != null
                ? await Sha1MatchesAsync(targetPath, sha1, ct).ConfigureAwait(false)
                : size.HasValue
                    ? length == size.Value
                    : length > 0;

            if (valid)
            {
                return;
            }

            File.Delete(targetPath);
        }

        Exception? last = null;
        foreach (var candidate in Candidates(url))
        {
            try
            {
                await DownloadOnceAsync(candidate, targetPath, ct).ConfigureAwait(false);
                if (sha1 != null && !await Sha1MatchesAsync(targetPath, sha1, ct).ConfigureAwait(false))
                {
                    last = new IOException("SHA1 校验失败: " + url);
                    File.Delete(targetPath);
                    continue;
                }

                return;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new IOException($"下载失败: {url} ({last?.Message})", last);
    }

    private async Task DownloadOnceAsync(string url, string targetPath, CancellationToken ct)
    {
        // 45 秒没收到任何数据就判定卡死，换下一个通道（有数据就一直续命）
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stall.CancelAfter(StallTimeout);
        var token = stall.Token;

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? -1;

        var temp = targetPath + ".tmp";
        await using (var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
        await using (var target = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                done += read;
                stall.CancelAfter(StallTimeout);
                Progress?.Report((done, total));
            }
        }

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        File.Move(temp, targetPath);
    }

    private IEnumerable<string> Candidates(string url)
    {
        yield return MapUrl(url);
        if (UseMirror)
        {
            yield return url; // 镜像失败时回退官方源
        }
    }

    public static Task<bool> Sha1MatchesAsync(string path, string expected, CancellationToken ct = default)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA1.Create();
            var hash = sha.ComputeHash(stream);
            var actual = Convert.ToHexString(hash).ToLowerInvariant();
            var ok = string.Equals(actual, expected.Trim().ToLowerInvariant(), StringComparison.Ordinal);
            return Task.FromResult(ok);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public void Dispose() => _http.Dispose();
}
