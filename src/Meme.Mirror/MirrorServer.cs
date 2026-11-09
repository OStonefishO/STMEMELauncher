using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Meme.Mirror;

/// <summary>
/// 极简镜像站：TcpListener 起 HTTP 服务，把上游（BNCLAPI / Mojang / Forge maven）缓存到本地磁盘，
/// 对外提供稳定的 /m/{源}/{路径} 地址，并带一个状态面板。
/// 不用 HttpListener 是为了免掉 Windows 的 urlacl 权限问题。
/// </summary>
public sealed class MirrorServer
{
    private readonly TcpListener _listener;
    private readonly HttpClient _http;
    private readonly string _cacheRoot;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.Now;
    private readonly ConcurrentDictionary<string, bool> _health = new();

    public MirrorServer(int port, string cacheRoot)
    {
        Port = port;
        _cacheRoot = cacheRoot;
        Directory.CreateDirectory(cacheRoot);

        _listener = new TcpListener(IPAddress.Loopback, port);
        _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MemeMirror/0.1");

        Sources = new List<MirrorSource>
        {
            new()
            {
                Key = "neoforge",
                Title = "NeoForge Maven",
                Upstream = "https://bmclapi2.bangbang93.com/maven",
                Description = "maven.neoforged.net 被污染，这里走 BMCLAPI 的 maven 镜像",
                ProbePath = "net/neoforged/bus/8.0.5/bus-8.0.5.jar",
            },
            new()
            {
                Key = "forge",
                Title = "Forge Maven",
                Upstream = "https://maven.minecraftforge.net",
                Description = "Forge 官方 maven（直连可用，缓存后更快）",
                ProbePath = "net/minecraftforge/forge/maven-metadata.xml",
            },
            new()
            {
                Key = "libraries",
                Title = "Mojang Libraries",
                Upstream = "https://bmclapi2.bangbang93.com/libraries",
                Description = "libraries.minecraft.net 的镜像",
                ProbePath = "com/mojang/brigadier/1.0.18/brigadier-1.0.18.jar",
            },
            new()
            {
                Key = "piston",
                Title = "Mojang 元数据",
                Upstream = "https://bmclapi2.bangbang93.com",
                Description = "piston-meta / launchermeta：版本清单、version.json",
                ProbePath = "mc/game/version_manifest_v2.json",
            },
            new()
            {
                Key = "fabric",
                Title = "Fabric Meta",
                Upstream = "https://bmclapi2.bangbang93.com/fabric-meta",
                Description = "Fabric 加载器版本与 profile",
                ProbePath = "v2/versions/loader",
            },
            new()
            {
                Key = "assets",
                Title = "游戏资源",
                Upstream = "https://bmclapi2.bangbang93.com/assets",
                Description = "resources.download.minecraft.net 的镜像（贴图/音效）",
                ProbePath = string.Empty,
            },
        };
    }

    public int Port { get; }

    public string CacheRoot => _cacheRoot;

    public IReadOnlyList<MirrorSource> Sources { get; }

    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public void Start()
    {
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
        _ = Task.Run(HealthLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(client));
        }
    }

    private async Task HealthLoopAsync()
    {
        while (true)
        {
            foreach (var source in Sources)
            {
                if (string.IsNullOrEmpty(source.ProbePath))
                {
                    _health[source.Key] = true;
                    continue;
                }

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head,
                        source.Upstream.TrimEnd('/') + "/" + source.ProbePath);
                    using var response = await _http.SendAsync(request).ConfigureAwait(false);
                    _health[source.Key] = response.IsSuccessStatusCode ||
                                          (int)response.StatusCode is >= 200 and < 400;
                }
                catch
                {
                    _health[source.Key] = false;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.NoDelay = true;
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 8192, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    return;
                }

                var parts = requestLine.Split(' ');
                if (parts.Length < 2)
                {
                    return;
                }

                var method = parts[0].ToUpperInvariant();
                var rawPath = parts[1];
                var queryIndex = rawPath.IndexOf('?');
                var path = Uri.UnescapeDataString(queryIndex >= 0 ? rawPath[..queryIndex] : rawPath);

                // 读完请求头
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false)))
                {
                    // 只需要把头部消费掉
                }

                if (method != "GET" && method != "HEAD")
                {
                    await WriteTextAsync(stream, method, 405, "Method Not Allowed", "只支持 GET/HEAD").ConfigureAwait(false);
                    return;
                }

                if (path is "/" or "/index.html")
                {
                    await WriteTextAsync(stream, method, 200, "text/html; charset=utf-8", BuildDashboard()).ConfigureAwait(false);
                    return;
                }

                if (path == "/api/status")
                {
                    await WriteTextAsync(stream, method, 200, "application/json; charset=utf-8", BuildStatusJson()).ConfigureAwait(false);
                    return;
                }

                if (path == "/favicon.ico")
                {
                    await WriteEmptyAsync(stream, method, 204).ConfigureAwait(false);
                    return;
                }

                if (path.StartsWith("/m/", StringComparison.Ordinal))
                {
                    await ProxyAsync(stream, method, path[3..]).ConfigureAwait(false);
                    return;
                }

                await WriteTextAsync(stream, method, 404, "text/plain; charset=utf-8", "404").ConfigureAwait(false);
            }
            catch
            {
                // 单个连接出错不影响服务
            }
        }
    }

    private async Task ProxyAsync(Stream stream, string method, string relative)
    {
        var slash = relative.IndexOf('/');
        if (slash <= 0)
        {
            await WriteTextAsync(stream, method, 400, "text/plain; charset=utf-8", "用法: /m/{源}/{路径}").ConfigureAwait(false);
            return;
        }

        var key = relative[..slash];
        var subPath = relative[(slash + 1)..];

        var source = Sources.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
        if (source == null)
        {
            await WriteTextAsync(stream, method, 404, "text/plain; charset=utf-8", "未知源: " + key).ConfigureAwait(false);
            return;
        }

        var cachePath = Path.Combine(_cacheRoot, source.Key, subPath.Replace('/', Path.DirectorySeparatorChar));
        var contentType = GuessContentType(subPath);

        if (File.Exists(cachePath))
        {
            source.Stats.Hit();
            await WriteFileAsync(stream, method, cachePath, contentType).ConfigureAwait(false);
            return;
        }

        var url = source.Upstream.TrimEnd('/') + "/" + subPath;
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                source.Stats.Error();
                await WriteTextAsync(stream, method, 502, "text/plain; charset=utf-8",
                    $"上游 {url} 返回 {(int)response.StatusCode}").ConfigureAwait(false);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            var temp = cachePath + ".tmp";
            await using (var sourceStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                await sourceStream.CopyToAsync(file).ConfigureAwait(false);
            }

            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }

            File.Move(temp, cachePath);
            source.Stats.Miss();

            var type = response.Content.Headers.ContentType?.MediaType ?? contentType;
            await WriteFileAsync(stream, method, cachePath, type).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            source.Stats.Error();
            await WriteTextAsync(stream, method, 502, "text/plain; charset=utf-8", "拉取失败: " + ex.Message).ConfigureAwait(false);
        }
    }

    private static async Task WriteFileAsync(Stream stream, string method, string path, string contentType)
    {
        var info = new FileInfo(path);
        var header = new StringBuilder();
        header.Append("HTTP/1.1 200 OK\r\n");
        header.Append("Content-Type: ").Append(contentType).Append("\r\n");
        header.Append("Content-Length: ").Append(info.Length).Append("\r\n");
        header.Append("Cache-Control: public, max-age=86400\r\n");
        header.Append("Access-Control-Allow-Origin: *\r\n");
        header.Append("Connection: close\r\n\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        await stream.WriteAsync(headerBytes).ConfigureAwait(false);
        if (method == "HEAD")
        {
            return;
        }

        await using var file = File.OpenRead(path);
        await file.CopyToAsync(stream, 81920).ConfigureAwait(false);
    }

    private static async Task WriteTextAsync(Stream stream, string method, int status, string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var reason = status switch
        {
            200 => "OK",
            204 => "No Content",
            400 => "Bad Request",
            404 => "Not Found",
            405 => "Method Not Allowed",
            502 => "Bad Gateway",
            _ => "OK",
        };

        var header = new StringBuilder();
        header.Append($"HTTP/1.1 {status} {reason}\r\n");
        header.Append("Content-Type: ").Append(contentType).Append("\r\n");
        header.Append("Content-Length: ").Append(bytes.Length).Append("\r\n");
        header.Append("Access-Control-Allow-Origin: *\r\n");
        header.Append("Connection: close\r\n\r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes(header.ToString())).ConfigureAwait(false);
        if (method != "HEAD")
        {
            await stream.WriteAsync(bytes).ConfigureAwait(false);
        }
    }

    private static async Task WriteEmptyAsync(Stream stream, string method, int status)
    {
        var header = $"HTTP/1.1 {status} No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header)).ConfigureAwait(false);
    }

    private static string GuessContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".jar" => "application/java-archive",
        ".zip" => "application/zip",
        ".txt" => "text/plain",
        _ => "application/octet-stream",
    };

    // ---------------- 面板 ----------------

    private string BuildStatusJson()
    {
        var payload = new
        {
            @base = BaseUrl,
            uptime = (DateTimeOffset.Now - _startedAt).ToString(@"hh\:mm\:ss"),
            cache = _cacheRoot,
            cacheSize = DirSize(_cacheRoot),
            sources = Sources.Select(s => new
            {
                key = s.Key,
                title = s.Title,
                upstream = s.Upstream,
                healthy = _health.TryGetValue(s.Key, out var ok) && ok,
                hits = s.Stats.Hits,
                misses = s.Stats.Misses,
                errors = s.Stats.Errors,
                cachedBytes = DirSize(Path.Combine(_cacheRoot, s.Key)),
                path = $"{BaseUrl}/m/{s.Key}/",
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private string BuildDashboard()
    {
        var rows = new StringBuilder();
        foreach (var source in Sources)
        {
            var healthy = _health.TryGetValue(source.Key, out var ok) && ok;
            var size = DirSize(Path.Combine(_cacheRoot, source.Key));
            rows.Append($@"
        <tr>
          <td><b>{source.Title}</b><div class=""sub"">{source.Description}</div></td>
          <td class=""mono"">{source.Upstream}</td>
          <td><span class=""pill {(healthy ? "ok" : "bad")}"">{(healthy ? "可用" : "不通")}</span></td>
          <td class=""num"">{source.Stats.Hits}</td>
          <td class=""num"">{source.Stats.Misses}</td>
          <td class=""num"">{Human(size)}</td>
        </tr>");
        }

        var total = Human(DirSize(_cacheRoot));
        var uptime = (DateTimeOffset.Now - _startedAt).ToString(@"hh\:mm\:ss");

        return $@"<!DOCTYPE html>
<html lang=""zh-CN""><head><meta charset=""utf-8"">
<title>MEME Mirror · 我的世界镜像站</title>
<style>
*{{box-sizing:border-box}}
body{{margin:0;background:#0f1116;color:#e8ebf1;font-family:""Microsoft YaHei UI"",system-ui,sans-serif}}
.wrap{{max-width:1180px;margin:0 auto;padding:36px 28px 60px}}
h1{{font-size:26px;margin:0 0 6px}}
.lead{{color:#8b93a3;font-size:13px;margin:0 0 26px}}
.cards{{display:flex;gap:14px;margin-bottom:26px}}
.card{{flex:1;background:#1a1e26;border:1px solid #262c37;border-radius:12px;padding:16px 18px}}
.card .k{{font-size:12px;color:#8b93a3}}
.card .v{{font-size:22px;font-weight:700;margin-top:6px}}
table{{width:100%;border-collapse:collapse;background:#1a1e26;border-radius:12px;overflow:hidden}}
th,td{{padding:12px 14px;text-align:left;font-size:13px;border-bottom:1px solid #242a34;vertical-align:top}}
th{{background:#151922;color:#8b93a3;font-weight:500;font-size:12px}}
tr:last-child td{{border-bottom:0}}
.sub{{color:#6f7787;font-size:11.5px;margin-top:3px;max-width:380px}}
.mono{{font-family:Consolas,monospace;font-size:11.5px;color:#9aa3b2;word-break:break-all}}
.num{{text-align:right;font-variant-numeric:tabular-nums}}
.pill{{display:inline-block;padding:2px 9px;border-radius:99px;font-size:11px}}
.pill.ok{{background:#123a2b;color:#4ade80}}
.pill.bad{{background:#3a1b1b;color:#f87171}}
.usage{{margin-top:26px;background:#151922;border:1px solid #242a34;border-radius:12px;padding:18px}}
.usage h2{{margin:0 0 10px;font-size:14px}}
code{{background:#0b0e13;padding:2px 6px;border-radius:5px;font-family:Consolas,monospace;font-size:12px;color:#8ab4ff}}
pre{{background:#0b0e13;padding:14px;border-radius:9px;overflow:auto;font-size:12px;color:#a9b4c6;line-height:1.6}}
.foot{{margin-top:26px;color:#5f6675;font-size:11.5px}}
</style></head><body><div class=""wrap"">
<h1>MEME Mirror</h1>
<p class=""lead"">本地镜像站 · 把被墙的 maven 和 Mojang 资源缓存到本地，给 MEME Launcher 用</p>

<div class=""cards"">
  <div class=""card""><div class=""k"">服务地址</div><div class=""v mono"">{BaseUrl}</div></div>
  <div class=""card""><div class=""k"">缓存占用</div><div class=""v"">{total}</div></div>
  <div class=""card""><div class=""k"">运行时长</div><div class=""v"">{uptime}</div></div>
  <div class=""card""><div class=""k"">缓存目录</div><div class=""v mono"" style=""font-size:11px"">{_cacheRoot}</div></div>
</div>

<table>
  <tr><th>源</th><th>上游</th><th>状态</th><th class=""num"">命中</th><th class=""num"">回源</th><th class=""num"">缓存</th></tr>
  {rows}
</table>

<div class=""usage"">
  <h2>怎么用</h2>
  <p style=""color:#8b93a3;font-size:12.5px;margin:0 0 10px"">把原来的地址换成 <code>{BaseUrl}/m/{{源}}/</code> 即可，例如：</p>
  <pre>NeoForge 库   https://maven.neoforged.net/releases/net/neoforged/bus/8.0.5/bus-8.0.5.jar
        →  {BaseUrl}/m/neoforge/net/neoforged/bus/8.0.5/bus-8.0.5.jar

Fabric meta   https://meta.fabricmc.net/v2/versions/loader
        →  {BaseUrl}/m/fabric/v2/versions/loader

Mojang 元数据  https://piston-meta.mojang.com/mc/game/version_manifest_v2.json
        →  {BaseUrl}/m/piston/mc/game/version_manifest_v2.json

资源文件       https://resources.download.minecraft.net/ab/cdef…
        →  {BaseUrl}/m/assets/ab/cdef…</pre>
  <p style=""color:#8b93a3;font-size:12.5px;margin:12px 0 0"">
    第一次请求会回源并写入缓存，之后再请求就是本地直出。状态接口：<code>/api/status</code>
  </p>
</div>

<p class=""foot"">MEME Mirror v0.1 · 单文件本地服务，缓存放在上面的目录里，删掉就是清空缓存。</p>
</div></body></html>";
    }

    private static long DirSize(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // 忽略
                }
            }
        }
        catch
        {
            // 忽略
        }

        return total;
    }

    private static string Human(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
