using System.Security.Cryptography;
using System.Text;
using Aurora.Core.Infrastructure;

namespace Aurora.Core.Accounts;

/// <summary>本地保存的一个账号。离线 / 外置 / 微软共用一种结构。</summary>
public sealed class SavedAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>offline / yggdrasil / microsoft</summary>
    public string Type { get; set; } = "offline";

    public string Name { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;

    /// <summary>外置 / 微软的 accessToken。</summary>
    public string AccessToken { get; set; } = string.Empty;

    public string ClientToken { get; set; } = string.Empty;

    /// <summary>外置登录的 API 根地址。</summary>
    public string ApiRoot { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public long LastUsed { get; set; }

    public string TypeLabel => Type switch
    {
        "yggdrasil" => "外置",
        "microsoft" => "正版",
        _ => "离线",
    };

    public string Subtitle => Type switch
    {
        "yggdrasil" => string.IsNullOrEmpty(ApiRoot) ? "外置登录" : ShortHost(ApiRoot),
        "microsoft" => "微软账号",
        _ => "离线账号",
    };

    public static string NewOffline(string name)
    {
        var clean = string.IsNullOrWhiteSpace(name) ? "Steve" : name.Trim();
        return TextUtil.OfflineUuid(clean);
    }

    private static string ShortHost(string url)
    {
        try
        {
            return new Uri(url).Host;
        }
        catch
        {
            return url;
        }
    }
}
