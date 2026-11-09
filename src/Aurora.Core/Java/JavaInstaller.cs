using System.IO.Compression;
using System.Text.Json;
using Aurora.Core.Net;

namespace Aurora.Core.Java;

/// <summary>缺 Java 时自动装一个（Eclipse Temurin JRE），装到启动器自己的目录里。</summary>
public sealed class JavaInstaller
{
    private readonly DownloadClient _client;
    private readonly string _root;

    public JavaInstaller(DownloadClient client, string root)
    {
        _client = client;
        _root = root;
    }

    public string Root => _root;

    /// <summary>已经装过就直接用，没装就下载解压；失败返回 null。</summary>
    public async Task<JavaRuntime?> EnsureAsync(int major, IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var target = Path.Combine(_root, "java-" + major);
        var existing = FindJavaHome(target);
        if (existing != null)
        {
            return new JavaRuntime
            {
                JavaPath = Path.Combine(existing, "bin", "javaw.exe"),
                Home = existing,
                Major = major,
                VersionText = "已安装 Java " + major,
                Vendor = "Temurin",
                Source = "启动器自带",
            };
        }

        Directory.CreateDirectory(_root);
        var zipPath = Path.Combine(Path.GetTempPath(), $"meme-jre-{major}.zip");

        try
        {
            progress?.Report(new InstallProgress { Stage = "Java", Message = $"下载 Java {major}…" });
            var urls = await GetDownloadUrlsAsync(major, ct).ConfigureAwait(false);

            Exception? last = null;
            foreach (var url in urls)
            {
                try
                {
                    progress?.Report(new InstallProgress { Stage = "Java", Message = $"下载 Java {major}…" });
                    await _client.DownloadFileAsync(url, zipPath, ct: ct).ConfigureAwait(false);
                    last = null;
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            if (last != null || !File.Exists(zipPath))
            {
                throw last ?? new IOException("下载失败");
            }

            progress?.Report(new InstallProgress { Stage = "Java", Message = "解压 Java…" });
            var extract = Path.Combine(_root, "java-" + major + "-tmp");
            if (Directory.Exists(extract))
            {
                Directory.Delete(extract, true);
            }

            ZipFile.ExtractToDirectory(zipPath, extract);
            var home = FindJavaHome(extract);
            if (home == null)
            {
                throw new IOException("解压出来的目录里没有 bin\\java.exe");
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }

            Directory.Move(home, target);

            // 把解压剩下的空目录清掉
            try
            {
                if (Directory.Exists(extract))
                {
                    Directory.Delete(extract, true);
                }
            }
            catch
            {
                // 忽略
            }

            progress?.Report(new InstallProgress { Stage = "Java", Message = "Java 装好了" });
            return new JavaRuntime
            {
                JavaPath = Path.Combine(target, "bin", "javaw.exe"),
                Home = target,
                Major = major,
                VersionText = "刚安装的 Java " + major,
                Vendor = "Temurin",
                Source = "启动器自带",
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }
            catch
            {
                // 忽略
            }
        }
    }

    /// <summary>先拿精确的 GitHub 下载地址，再退回官方直链；每个都配一个 gh-proxy 版本。</summary>
    private static async Task<List<string>> GetDownloadUrlsAsync(int major, CancellationToken ct)
    {
        var urls = new List<string>();

        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var api = $"https://api.adoptium.net/v3/assets/latest/{major}/hotspot?os=windows&architecture=x64&image_type=jre";
            var json = await http.GetStringAsync(api, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var link = doc.RootElement[0].GetProperty("binary").GetProperty("package").GetProperty("link").GetString();
                if (!string.IsNullOrEmpty(link))
                {
                    urls.Add(link);
                    urls.Add("https://gh-proxy.com/" + link);
                }
            }
        }
        catch
        {
            // 拿不到就只用直链
        }

        var direct = $"https://api.adoptium.net/v3/binary/latest/{major}/ga/windows/x64/jre/hotspot/normal/eclipse";
        if (!urls.Contains(direct))
        {
            urls.Add(direct);
        }

        return urls;
    }

    /// <summary>在目录（含子目录）里找 bin\java.exe 所在的那一层。</summary>
    private static string? FindJavaHome(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            return null;
        }

        if (File.Exists(Path.Combine(dir, "bin", "javaw.exe")) || File.Exists(Path.Combine(dir, "bin", "java.exe")))
        {
            return dir;
        }

        try
        {
            foreach (var sub in Directory.GetDirectories(dir))
            {
                var found = FindJavaHome(sub);
                if (found != null)
                {
                    return found;
                }
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }
}
