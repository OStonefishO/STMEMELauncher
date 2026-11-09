using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Aurora.Core.Auth;

/// <summary>外置登录（Yggdrasil / authlib-injector）会话。</summary>
public sealed class YggdrasilSession
{
    public string ApiRoot { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string ClientToken { get; init; } = string.Empty;
    public string Uuid { get; init; } = string.Empty;
    public string PlayerName { get; init; } = string.Empty;

    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(Uuid);
}

/// <summary>
/// 第三方皮肤站登录（LittleSkin 等）。完全绕开微软，玩家用邮箱账号登录，
/// 启动时通过 authlib-injector 把认证地址替换成皮肤站。
/// </summary>
public sealed class YggdrasilAuth
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public string ApiRoot { get; }

    public YggdrasilAuth(string apiRoot)
    {
        ApiRoot = (apiRoot ?? string.Empty).TrimEnd('/');
    }

    public async Task<YggdrasilSession> AuthenticateAsync(string email, string password,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ApiRoot))
        {
            throw new InvalidOperationException("没有填写外置登录服务器地址");
        }

        var body = JsonSerializer.Serialize(new
        {
            agent = new { name = "Minecraft", version = 1 },
            username = email,
            password,
            requestUser = true,
        });

        var json = await PostAsync($"{ApiRoot}/authserver/authenticate", body, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var profile = root.TryGetProperty("selectedProfile", out var sp) && sp.ValueKind == JsonValueKind.Object
            ? sp
            : default;

        if (profile.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("这个账号还没有创建角色，先去皮肤站建一个");
        }

        return new YggdrasilSession
        {
            ApiRoot = ApiRoot,
            AccessToken = GetString(root, "accessToken") ?? string.Empty,
            ClientToken = GetString(root, "clientToken") ?? string.Empty,
            Uuid = GetString(profile, "id") ?? string.Empty,
            PlayerName = GetString(profile, "name") ?? string.Empty,
        };
    }

    /// <summary>校验 token 是否还有效。</summary>
    public async Task<bool> ValidateAsync(YggdrasilSession session, CancellationToken ct = default)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { accessToken = session.AccessToken });
            var json = await PostAsync($"{session.ApiRoot}/authserver/validate", body, ct).ConfigureAwait(false);
            return json.Trim() == "{}" || json.Contains("valid", StringComparison.OrdinalIgnoreCase) ||
                   string.IsNullOrWhiteSpace(json);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> PostAsync(string url, string body, CancellationToken ct)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var message = TryReadError(text) ?? $"HTTP {(int)response.StatusCode}";
            throw new InvalidOperationException("登录失败：" + message);
        }

        return text;
    }

    private static string? TryReadError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return GetString(doc.RootElement, "errorMessage") ?? GetString(doc.RootElement, "error");
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>
/// authlib-injector：外置登录的注入器。
/// 从官方 meta 拉最新版，失败走 gh-proxy（GitHub 在部分网络下不可达）。
/// </summary>
public sealed class AuthlibInjectorInstaller
{
    private const string MetaUrl = "https://authlib-injector.yushi.moe/artifact/latest.json";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public string JarPath(string gameDirectory) => Path.Combine(gameDirectory, "authlib-injector.jar");

    public async Task<string> EnsureAsync(string gameDirectory, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var target = JarPath(gameDirectory);
        if (File.Exists(target) && new FileInfo(target).Length > 100_000)
        {
            return target;
        }

        Directory.CreateDirectory(gameDirectory);
        progress?.Report("获取 authlib-injector 版本…");

        string downloadUrl;
        try
        {
            var meta = await _http.GetStringAsync(MetaUrl, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(meta);
            downloadUrl = doc.RootElement.TryGetProperty("download_url", out var d)
                ? d.GetString() ?? string.Empty
                : string.Empty;
        }
        catch
        {
            downloadUrl = string.Empty;
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException("拿不到 authlib-injector 的下载地址");
        }

        foreach (var candidate in new[] { downloadUrl, "https://gh-proxy.com/" + downloadUrl })
        {
            try
            {
                progress?.Report("下载 authlib-injector…");
                using var response = await _http.GetAsync(candidate, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                await File.WriteAllBytesAsync(target, bytes, ct).ConfigureAwait(false);
                return target;
            }
            catch
            {
                // 换下一个通道
            }
        }

        throw new InvalidOperationException("authlib-injector 下载失败");
    }
}

/// <summary>从官方启动器的 launcher_accounts.json 里导入已登录的正版账号。</summary>
public sealed class OfficialLauncherImport
{
    public sealed class ImportedAccount
    {
        public string PlayerName { get; init; } = string.Empty;
        public string Uuid { get; init; } = string.Empty;
        public string AccessToken { get; init; } = string.Empty;
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "launcher_accounts.json");

    public static List<ImportedAccount> Read(string? path = null)
    {
        var result = new List<ImportedAccount>();
        var file = path ?? DefaultPath;
        if (!File.Exists(file))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (!doc.RootElement.TryGetProperty("accounts", out var accounts) ||
                accounts.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var entry in accounts.EnumerateObject())
            {
                var value = entry.Value;
                var token = Get(value, "accessToken");
                var profile = value.TryGetProperty("profile", out var p) && p.ValueKind == JsonValueKind.Object
                    ? p
                    : default;

                var name = profile.ValueKind == JsonValueKind.Object ? Get(profile, "name") : null;
                var uuid = profile.ValueKind == JsonValueKind.Object ? Get(profile, "id") : null;

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(uuid))
                {
                    continue;
                }

                result.Add(new ImportedAccount { PlayerName = name!, Uuid = uuid!, AccessToken = token! });
            }
        }
        catch
        {
            // 文件格式变化就当作没有
        }

        return result;
    }

    private static string? Get(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
