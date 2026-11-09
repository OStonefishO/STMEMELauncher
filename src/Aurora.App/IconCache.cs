using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace Aurora.App;

/// <summary>内容图标缓存：优先本地文件，没有就下载一次存起来。</summary>
internal static class IconCache
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly string Directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MEMELauncher", "icons");

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MemeLauncher/0.4");
        return client;
    }

    public static async Task<BitmapImage?> GetAsync(string url)
    {
        try
        {
            var path = Path.Combine(Directory, Hash(url) + ".img");
            if (!File.Exists(path))
            {
                var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
                System.IO.Directory.CreateDirectory(Directory);
                await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
            }

            return Load(path);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? Load(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static string Hash(string text)
    {
        using var sha = SHA1.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..20].ToLowerInvariant();
    }
}
