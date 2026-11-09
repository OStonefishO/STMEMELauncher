using System.IO.Compression;
using System.Text.Json;

namespace Aurora.Core.Server;

public sealed partial class ServerManager
{
    /// <summary>把安装器需要的依赖按 maven 目录结构从 BMCLAPI 预下载到 libraries/。</summary>
    private async Task<int> PrefetchInstallerLibrariesAsync(string installerJar, string targetDirectory,
        IProgress<Net.InstallProgress>? progress, CancellationToken ct)
    {
        var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // path -> url

        try
        {
            using var zip = ZipFile.OpenRead(installerJar);
            foreach (var name in new[] { "version.json", "install_profile.json" })
            {
                var entry = zip.GetEntry(name);
                if (entry == null)
                {
                    continue;
                }

                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                CollectLibraries(json, wanted);
            }
        }
        catch
        {
            return 0;
        }

        if (wanted.Count == 0)
        {
            return 0;
        }

        var librariesDir = Path.Combine(targetDirectory, "libraries");
        var done = 0;
        var total = wanted.Count;

        foreach (var pair in wanted)
        {
            ct.ThrowIfCancellationRequested();

            var path = pair.Key.Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.Combine(librariesDir, path);
            if (File.Exists(destination) && new FileInfo(destination).Length > 0)
            {
                done++;
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            var urls = new List<string>();
            if (!string.IsNullOrEmpty(pair.Value))
            {
                urls.Add(pair.Value);
            }

            urls.Add("https://bmclapi2.bangbang93.com/maven/" + pair.Key);

            var ok = false;
            foreach (var url in urls)
            {
                try
                {
                    await _client.DownloadFileAsync(url, destination, ct: ct).ConfigureAwait(false);
                    ok = true;
                    break;
                }
                catch
                {
                    // 换下一个源
                }
            }

            done++;
            if (ok)
            {
                progress?.Report(new Net.InstallProgress
                {
                    Stage = "依赖",
                    Done = done,
                    Total = total,
                    Message = $"预下载依赖 {done}/{total}",
                });
            }
        }

        return wanted.Count;
    }

    private static void CollectLibraries(string json, Dictionary<string, string> wanted)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("libraries", out var libraries) ||
            libraries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var lib in libraries.EnumerateArray())
        {
            string? path = null;
            string? url = null;

            if (lib.TryGetProperty("downloads", out var downloads) &&
                downloads.TryGetProperty("artifact", out var artifact))
            {
                if (artifact.TryGetProperty("path", out var p))
                {
                    path = p.GetString();
                }

                if (artifact.TryGetProperty("url", out var u))
                {
                    url = u.GetString();
                }
            }

            if (string.IsNullOrEmpty(path) && lib.TryGetProperty("name", out var nameEl))
            {
                path = MavenPathFromName(nameEl.GetString());
            }

            if (!string.IsNullOrEmpty(path))
            {
                wanted[path!] = url ?? string.Empty;
            }
        }
    }

    /// <summary>group:artifact:version[:classifier][@ext] → maven 相对路径。</summary>
    private static string? MavenPathFromName(string? coordinate)
    {
        if (string.IsNullOrWhiteSpace(coordinate))
        {
            return null;
        }

        var extension = "jar";
        var at = coordinate.IndexOf('@');
        if (at > 0)
        {
            extension = coordinate[(at + 1)..];
            coordinate = coordinate[..at];
        }

        var parts = coordinate.Split(':');
        if (parts.Length < 3)
        {
            return null;
        }

        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length > 3 ? "-" + parts[3] : string.Empty;

        return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.{extension}";
    }
}
