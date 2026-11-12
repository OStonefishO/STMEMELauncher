using System.Security.Cryptography;
using System.Text;

namespace Aurora.Core.Net;

/// <summary>
/// 内容接口（Modrinth / CurseForge）的 JSON 磁盘缓存。
/// 搜索页翻来翻去、重复打开详情时不用每次都打接口，镜像慢的时候也能顶一下。
/// </summary>
public static class JsonCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(20);

    private static string Directory =>
        Path.Combine(Aurora.Core.Configuration.ConfigStore.RootDirectory, "cache", "content");

    private static string PathOf(string url)
    {
        var key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        return Path.Combine(Directory, key + ".json");
    }

    public static bool TryGet(string url, out string json)
    {
        json = string.Empty;
        try
        {
            var path = PathOf(url);
            if (!File.Exists(path))
            {
                return false;
            }

            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > DefaultTtl)
            {
                return false;
            }

            json = File.ReadAllText(path);
            return json.Length > 2;
        }
        catch
        {
            return false;
        }
    }

    public static void Put(string url, string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length < 2)
            {
                return;
            }

            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(PathOf(url), json);
        }
        catch
        {
            // 缓存失败不影响功能
        }
    }

    /// <summary>清空内容缓存，返回释放的字节数。</summary>
    public static long Clear()
    {
        long freed = 0;
        try
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return 0;
            }

            foreach (var file in System.IO.Directory.GetFiles(Directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    freed += new FileInfo(file).Length;
                }
                catch
                {
                    // 忽略
                }
            }

            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch
        {
            // 忽略
        }

        return freed;
    }
}
