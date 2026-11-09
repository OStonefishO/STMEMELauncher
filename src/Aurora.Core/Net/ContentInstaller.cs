using Aurora.Core.Models;

namespace Aurora.Core.Net;

/// <summary>把下载好的内容放进对应的游戏目录（mods / shaderpacks / resourcepacks / saves）。</summary>
public sealed class ContentInstaller
{
    private readonly DownloadClient _client;

    public ContentInstaller(DownloadClient client) => _client = client;

    public async Task<string> InstallFileAsync(ContentFile file, ContentKind kind, string gameDirectory,
        IProgress<InstallProgress>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            throw new InvalidOperationException("没有指定游戏目录");
        }

        var folder = Path.Combine(gameDirectory, ContentKindInfo.Folder(kind));
        Directory.CreateDirectory(folder);

        var name = SanitizeFileName(file.FileName);
        var target = Path.Combine(folder, name);

        progress?.Report(new InstallProgress { Stage = "内容", Message = "下载 " + name });
        await _client.DownloadFileAsync(file.Url, target, ct: ct).ConfigureAwait(false);
        progress?.Report(new InstallProgress
        {
            Stage = "内容",
            Done = 1,
            Total = 1,
            Message = $"已放入 {ContentKindInfo.Folder(kind)}\\{name}",
        });

        return target;
    }

    private static string SanitizeFileName(string name)
    {
        var cleaned = name;
        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(bad, '_');
        }

        return string.IsNullOrWhiteSpace(cleaned) ? "download.bin" : cleaned;
    }
}
