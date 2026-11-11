import io
p = r"D:\综合创作\MCLauncher\src\Aurora.Core\Net\Installer.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace('''            if (root.TryGetProperty("assetIndex", out var assetIndex) &&
                assetIndex.TryGetProperty("id", out var indexIdProp))
            {
                var indexId = indexIdProp.GetString();
                if (!string.IsNullOrEmpty(indexId))
                {
                    var indexPath = Path.Combine(AssetsDirectory, "indexes", indexId + ".json");
                    if (!File.Exists(indexPath))
                    {
                        progress?.Report(new InstallProgress { Stage = "资源索引", Message = "补下 assets index" });
                        await DownloadFileAsync(
                            new Uri($"https://launchermeta.mojang.com/v1/packages/{indexId}/{indexId}.json"),
                            indexPath, ct).ConfigureAwait(false);
                    }''','''            if (root.TryGetProperty("assetIndex", out var assetIndex) &&
                assetIndex.TryGetProperty("id", out var indexIdProp))
            {
                var indexId = indexIdProp.GetString();
                if (!string.IsNullOrEmpty(indexId))
                {
                    var indexPath = Path.Combine(AssetsDirectory, "indexes", indexId + ".json");
                    if (!File.Exists(indexPath))
                    {
                        var indexUrl = assetIndex.TryGetProperty("url", out var u) ? u.GetString() : null;
                        if (!string.IsNullOrEmpty(indexUrl))
                        {
                            progress?.Report(new InstallProgress { Stage = "资源索引", Message = "补下 assets index" });
                            await _client.DownloadFileAsync(indexUrl!, indexPath, ct: ct).ConfigureAwait(false);
                        }
                    }''', 1)

s = s.replace('''                    progress?.Report(new InstallProgress { Stage = "依赖库", Message = "补下 " + Path.GetFileName(rel) });
                    await DownloadFileAsync(new Uri(url), target, ct).ConfigureAwait(false);''','''                    progress?.Report(new InstallProgress { Stage = "依赖库", Message = "补下 " + Path.GetFileName(rel) });
                    await _client.DownloadFileAsync(url!, target, artifact.TryGetProperty("sha1", out var sha1Prop) ? sha1Prop.GetString() : null,
                        expected > 0 ? expected : null, ct).ConfigureAwait(false);''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("repair call sites fixed")
