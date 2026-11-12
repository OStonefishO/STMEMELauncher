using System.Net;
using System.Security.Cryptography;

namespace Aurora.Core.Net;

/// <summary>带镜像改写、重试和 SHA1 校验的下载器。</summary>
public sealed class DownloadClient : IDisposable
{
    private readonly HttpClient _http;
    public const string Bmclapi = "https://bmclapi2.bangbang93.com";

    /// <summary>MCIM（mcmod-info-mirror）：Modrinth / CurseForge 的接口与文件镜像。</summary>
    public const string Mcim = "https://mod.mcimirror.top";

    // 同一个目标文件同时只允许一个下载任务写，避免并发抢锁
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> TargetLocks =
        new(StringComparer.OrdinalIgnoreCase);

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
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MEMELauncher/0.17 (+https://github.com/OStonefishO/STMEMELauncher)");
    }

    /// <summary>下载源：auto / official / mirror。</summary>
    public string SourceMode { get; set; } = "auto";

    /// <summary>模组内容来源：auto / official / mcim。</summary>
    public string ContentSourceMode { get; set; } = "auto";

    public bool UseMirror { get; set; }

    /// <summary>自建镜像站（http://127.0.0.1:8123 这种），有值时优先走它 —— 但连不上会自动跳过。</summary>
    public string MirrorBaseUrl { get; set; } = string.Empty;

    /// <summary>自建镜像站探测缓存（地址 → 结果），60 秒内不重复探。</summary>
    private static readonly Dictionary<string, (bool Ok, DateTime At)> MirrorProbeCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly object MirrorProbeLock = new();

    /// <summary>上次判定「自建镜像连不上、已回退 BMCLAPI」的地址，UI 拿它提示用户。</summary>
    public static string? LastDeadMirror { get; private set; }

    /// <summary>探测自建镜像端口能不能连（800ms 超时，结果缓存 60 秒）。</summary>
    public static bool MirrorReachable(string baseUrl, int timeoutMs = 800)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return false;
        }

        lock (MirrorProbeLock)
        {
            if (MirrorProbeCache.TryGetValue(baseUrl, out var hit) && (DateTime.UtcNow - hit.At).TotalSeconds < 60)
            {
                return hit.Ok;
            }
        }

        var ok = false;
        try
        {
            var uri = new Uri(baseUrl);
            using var tcp = new System.Net.Sockets.TcpClient();
            var connect = tcp.ConnectAsync(uri.Host, uri.Port <= 0 ? 80 : uri.Port);
            ok = connect.Wait(timeoutMs) && tcp.Connected;
        }
        catch
        {
            ok = false;
        }

        lock (MirrorProbeLock)
        {
            MirrorProbeCache[baseUrl] = (ok, DateTime.UtcNow);
        }

        if (!ok)
        {
            LastDeadMirror = baseUrl;
        }

        return ok;
    }

    /// <summary>给设置页用的异步探测（保存镜像地址时立刻给结论）。</summary>
    public static async Task<bool> ProbeMirrorAsync(string baseUrl, int timeoutMs = 1200)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return false;
        }

        try
        {
            var uri = new Uri(baseUrl);
            using var tcp = new System.Net.Sockets.TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await tcp.ConnectAsync(uri.Host, uri.Port <= 0 ? 80 : uri.Port, cts.Token).ConfigureAwait(false);
            return tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>进度回调（已下载字节 / 总字节，可能为 -1）。</summary>
    public IProgress<(long Done, long Total)>? Progress { get; set; }

    public string MapUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        if (string.Equals(SourceMode, "official", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var custom = MirrorBaseUrl?.Trim();
        if (!string.IsNullOrEmpty(custom) && MirrorReachable(custom))
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

        var mapped = url
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

        // MCIM（mcmod-info-mirror）：Modrinth / CurseForge 的接口与文件镜像
        if (!string.Equals(ContentSourceMode, "official", StringComparison.OrdinalIgnoreCase))
        {
            mapped = mapped
                .Replace("https://api.modrinth.com", Mcim + "/modrinth", StringComparison.Ordinal)
                .Replace("https://cdn.modrinth.com", Mcim, StringComparison.Ordinal)
                .Replace("https://api.curseforge.com", Mcim + "/curseforge", StringComparison.Ordinal)
                .Replace("https://edge.forgecdn.net", Mcim, StringComparison.Ordinal)
                .Replace("https://mediafilez.forgecdn.net", Mcim, StringComparison.Ordinal);
        }

        return mapped;
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
        var gate = TargetLocks.GetOrAdd(Path.GetFullPath(targetPath), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await DownloadFileCoreAsync(url, targetPath, sha1, size, ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task DownloadFileCoreAsync(string url, string targetPath, string? sha1, long? size,
        CancellationToken ct)
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

        // 先下到唯一的临时文件，再原子替换目标文件：
        // 同一个文件被多个任务/多个启动器实例同时下载时不会互相锁住（之前报
        // "The process cannot access the file" 就是这个原因）
        var tempPath = targetPath + ".meme-" + Guid.NewGuid().ToString("N") + ".tmp";
        foreach (var candidate in Candidates(url))
        {
            try
            {
                await DownloadOnceAsync(candidate, tempPath, ct).ConfigureAwait(false);
                if (sha1 != null && !await Sha1MatchesAsync(tempPath, sha1, ct).ConfigureAwait(false))
                {
                    last = new IOException("SHA1 校验失败: " + url);
                    TryDelete(tempPath);
                    continue;
                }

                MoveWithRetry(tempPath, targetPath);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                TryDelete(tempPath);
            }
        }

        TryDelete(tempPath);
        throw new IOException($"下载失败: {url} ({last?.Message})", last);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>把临时文件换成正式文件，被杀毒/占用时重试几次。</summary>
    private static void MoveWithRetry(string source, string target)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                File.Move(source, target, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(150 * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                Thread.Sleep(150 * (attempt + 1));
            }
        }

        File.Move(source, target, overwrite: true);
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
        if (string.Equals(SourceMode, "official", StringComparison.OrdinalIgnoreCase))
        {
            yield return url;
            yield break;
        }

        var mapped = MapUrl(url);
        yield return mapped;

        // 只用镜像时不回退官方（玩家明确要求）；自动模式镜像失败再试官方
        if (!string.Equals(SourceMode, "mirror", StringComparison.OrdinalIgnoreCase) && mapped != url)
        {
            yield return url;
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
