using System.Collections.Generic;
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

    // 同时最多下 4 个图标，避免一次性几十个请求把界面拖卡
    private static readonly SemaphoreSlim Gate = new(4, 4);
    private static readonly Dictionary<string, BitmapSource?> Memory = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string Directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MEMELauncher", "icons");

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MemeLauncher/0.4");
        return client;
    }

    public static async Task<BitmapSource?> GetAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (Memory.TryGetValue(url, out var cached))
        {
            return cached;
        }

        try
        {
            var path = Path.Combine(Directory, Hash(url) + ".img");
            if (!File.Exists(path))
            {
                await Gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (!File.Exists(path))
                    {
                        var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
                        System.IO.Directory.CreateDirectory(Directory);
                        await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Gate.Release();
                }
            }

            var bitmap = Load(path);
            Memory[url] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? Load(string path)
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
            // WPF 自带的解码器不认 webp，走一次系统 WIC（Win10 1903+ / Win11 自带 WebP 解码器）
            return LoadViaWic(path);
        }
    }

    /// <summary>用系统 WIC 解码（主要为了 .webp 图标）。</summary>
    private static BitmapSource? LoadViaWic(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return null;
            }

            var frame = decoder.Frames[0];
            var converted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            converted.Freeze();
            return converted;
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
