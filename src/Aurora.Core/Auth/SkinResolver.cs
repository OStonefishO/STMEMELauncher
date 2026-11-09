using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Aurora.Core.Auth;

/// <summary>
/// 皮肤解析：外置走皮肤站的 sessionserver，微软走 Mojang sessionserver，
/// 离线读本地 CustomSkinLoader 的 LocalSkin。
/// </summary>
public static class SkinResolver
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>皮肤和披风的贴图地址（外置/微软都走 textures 属性）。</summary>
    public static async Task<(string? Skin, string? Cape)> GetTexturesAsync(string type, string uuid,
        string apiRoot, CancellationToken ct = default)
    {
        try
        {
            if (type == "offline" || string.IsNullOrWhiteSpace(uuid))
            {
                return (null, null);
            }

            string endpoint;
            if (type == "yggdrasil")
            {
                var root = string.IsNullOrWhiteSpace(apiRoot) ? "https://littleskin.cn/api/yggdrasil" : apiRoot;
                endpoint = root.TrimEnd('/') + "/sessionserver/session/minecraft/profile/" + uuid.Replace("-", string.Empty);
            }
            else
            {
                endpoint = "https://sessionserver.mojang.com/session/minecraft/profile/" + uuid.Replace("-", string.Empty);
            }

            var json = await Http.GetStringAsync(endpoint, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("properties", out var props) ||
                props.ValueKind != JsonValueKind.Array)
            {
                return (null, null);
            }

            foreach (var prop in props.EnumerateArray())
            {
                if (!prop.TryGetProperty("name", out var name) ||
                    !string.Equals(name.GetString(), "textures", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!prop.TryGetProperty("value", out var value) || value.GetString() is not { Length: > 0 } raw)
                {
                    continue;
                }

                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
                using var texDoc = JsonDocument.Parse(decoded);
                if (!texDoc.RootElement.TryGetProperty("textures", out var textures))
                {
                    return (null, null);
                }

                string? skin = null, cape = null;
                if (textures.TryGetProperty("SKIN", out var s2) && s2.TryGetProperty("url", out var su))
                {
                    skin = su.GetString();
                }

                if (textures.TryGetProperty("CAPE", out var c2) && c2.TryGetProperty("url", out var cu))
                {
                    cape = cu.GetString();
                }

                return (skin, cape);
            }
        }
        catch
        {
            // 忽略
        }

        return (null, null);
    }

    /// <summary>拿到皮肤的 PNG 字节。</summary>
    public static async Task<byte[]?> GetSkinBytesAsync(string type, string uuid, string apiRoot,
        string playerName, string gameDirectory, CancellationToken ct = default)
    {
        try
        {
            if (type == "offline")
            {
                var local = LocalSkinPath(gameDirectory, playerName);
                return local != null ? await File.ReadAllBytesAsync(local, ct).ConfigureAwait(false) : null;
            }

            var url = await ResolveTextureUrlAsync(type, uuid, apiRoot, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            return await Http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>在多个目录里找离线皮肤（版本隔离时皮肤在版本目录下）。</summary>
    public static string? FindLocalSkin(IEnumerable<string> directories, string playerName)
    {
        foreach (var dir in directories.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            var path = LocalSkinPath(dir, playerName);
            if (path != null)
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>本地离线皮肤文件（CustomSkinLoader/LocalSkin/skins/名字.png）。</summary>
    public static string? LocalSkinPath(string gameDirectory, string playerName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(playerName))
        {
            return null;
        }

        var path = Path.Combine(gameDirectory, "CustomSkinLoader", "LocalSkin", "skins", playerName + ".png");
        return File.Exists(path) ? path : null;
    }

    private static async Task<string?> ResolveTextureUrlAsync(string type, string uuid, string apiRoot,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(uuid))
        {
            return null;
        }

        string endpoint;
        if (type == "yggdrasil")
        {
            var root = string.IsNullOrWhiteSpace(apiRoot) ? "https://littleskin.cn/api/yggdrasil" : apiRoot;
            endpoint = root.TrimEnd('/') + "/sessionserver/session/minecraft/profile/" + uuid.Replace("-", string.Empty);
        }
        else
        {
            endpoint = "https://sessionserver.mojang.com/session/minecraft/profile/" + uuid.Replace("-", string.Empty);
        }

        var json = await Http.GetStringAsync(endpoint, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("properties", out var props) ||
            props.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var prop in props.EnumerateArray())
        {
            if (!prop.TryGetProperty("name", out var name) ||
                !string.Equals(name.GetString(), "textures", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!prop.TryGetProperty("value", out var value))
            {
                continue;
            }

            var raw = value.GetString();
            if (string.IsNullOrEmpty(raw))
            {
                continue;
            }

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
            using var texDoc = JsonDocument.Parse(decoded);
            if (texDoc.RootElement.TryGetProperty("textures", out var textures) &&
                textures.TryGetProperty("SKIN", out var skin) &&
                skin.TryGetProperty("url", out var skinUrl))
            {
                return skinUrl.GetString();
            }
        }

        return null;
    }
}
