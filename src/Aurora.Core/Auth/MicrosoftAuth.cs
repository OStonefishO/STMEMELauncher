using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Aurora.Core.Auth;

public sealed class DeviceCodeFlow
{
    public string DeviceCode { get; init; } = string.Empty;
    public string UserCode { get; init; } = string.Empty;
    public string VerificationUri { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int IntervalSeconds { get; init; } = 5;
    public int ExpiresInSeconds { get; init; } = 900;
}

public sealed class AuthSession
{
    public string PlayerName { get; init; } = string.Empty;
    public string Uuid { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string? Xuid { get; init; }
    public string UserType => "msa";
    public DateTimeOffset ObtainedAt { get; init; } = DateTimeOffset.Now;

    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(Uuid);
}

/// <summary>
/// 微软正版登录（设备码流程）。需要一个 Azure 应用的 ClientId：
/// 在 portal.azure.com 注册应用 → 允许「移动和桌面应用」公共客户端 → 把 Application (client) ID 填进设置。
/// </summary>
public sealed class MicrosoftAuth
{
    private const string DeviceCodeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string XblUrl = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string McLoginUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string ProfileUrl = "https://api.minecraftservices.com/minecraft/profile";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public MicrosoftAuth(string clientId)
    {
        ClientId = clientId ?? string.Empty;
    }

    public string ClientId { get; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    public async Task<DeviceCodeFlow> StartAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("还没有配置微软 ClientId，先在设置里填写");
        }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["scope"] = "XboxLive.signin offline_access",
        };

        using var response = await _http.PostAsync(DeviceCodeUrl, new FormUrlEncodedContent(form), ct)
            .ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        EnsureSuccess(response, json, "获取设备码失败");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new DeviceCodeFlow
        {
            DeviceCode = root.GetProperty("device_code").GetString() ?? string.Empty,
            UserCode = root.GetProperty("user_code").GetString() ?? string.Empty,
            VerificationUri = root.GetProperty("verification_uri").GetString() ?? "https://microsoft.com/link",
            Message = root.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty,
            IntervalSeconds = root.TryGetProperty("interval", out var i) ? i.GetInt32() : 5,
            ExpiresInSeconds = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 900,
        };
    }

    /// <summary>轮询直到用户完成授权。</summary>
    public async Task<AuthSession> WaitForAuthorizationAsync(DeviceCodeFlow flow, CancellationToken ct = default)
    {
        var deadline = DateTimeOffset.Now.AddSeconds(flow.ExpiresInSeconds);
        var interval = Math.Max(2, flow.IntervalSeconds);

        while (DateTimeOffset.Now < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(interval), ct).ConfigureAwait(false);

            var form = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["device_code"] = flow.DeviceCode,
            };

            using var response = await _http.PostAsync(TokenUrl, new FormUrlEncodedContent(form), ct)
                .ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(json);
                var msaToken = doc.RootElement.GetProperty("access_token").GetString()
                               ?? throw new InvalidOperationException("没有拿到 access_token");
                return await ExchangeForMinecraftAsync(msaToken, ct).ConfigureAwait(false);
            }

            using var errDoc = JsonDocument.Parse(json);
            var error = errDoc.RootElement.TryGetProperty("error", out var err) ? err.GetString() : null;
            switch (error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += 5;
                    continue;
                case "authorization_declined":
                    throw new InvalidOperationException("用户在授权页面拒绝了登录");
                case "expired_token":
                    throw new InvalidOperationException("设备码已过期，请重新登录");
                default:
                    throw new InvalidOperationException("登录失败: " + error);
            }
        }

        throw new TimeoutException("登录超时");
    }

    /// <summary>MSA token → Xbox Live → XSTS → Minecraft token → profile。</summary>
    public async Task<AuthSession> ExchangeForMinecraftAsync(string msaAccessToken, CancellationToken ct = default)
    {
        // 1. Xbox Live
        var xblBody = JsonSerializer.Serialize(new
        {
            Properties = new Dictionary<string, object>
            {
                ["AuthMethod"] = "RPS",
                ["SiteName"] = "user.auth.xboxlive.com",
                ["RpsTicket"] = "d=" + msaAccessToken,
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT",
        });

        var (xblToken, uhs) = await PostXboxAsync(XblUrl, xblBody, ct).ConfigureAwait(false);

        // 2. XSTS
        var xstsBody = JsonSerializer.Serialize(new
        {
            Properties = new Dictionary<string, object>
            {
                ["SandboxId"] = "RETAIL",
                ["UserTokens"] = new[] { xblToken },
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT",
        });

        var (xstsToken, uhs2) = await PostXboxAsync(XstsUrl, xstsBody, ct).ConfigureAwait(false);

        // 3. 换 Minecraft token
        var mcBody = JsonSerializer.Serialize(new
        {
            identityToken = $"XBL3.0 x={uhs2};{xstsToken}",
        });

        using var mcResponse = await _http.PostAsync(McLoginUrl,
            new StringContent(mcBody, Encoding.UTF8, "application/json"), ct).ConfigureAwait(false);
        var mcJson = await mcResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        EnsureSuccess(mcResponse, mcJson, "Minecraft 登录失败");

        string mcToken;
        using (var mcDoc = JsonDocument.Parse(mcJson))
        {
            mcToken = mcDoc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        }

        // 4. 拿档案
        using var request = new HttpRequestMessage(HttpMethod.Get, ProfileUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mcToken);
        using var profileResponse = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var profileJson = await profileResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!profileResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("这个账号没有购买 Minecraft（或档案不可用）");
        }

        using var profileDoc = JsonDocument.Parse(profileJson);
        var profile = profileDoc.RootElement;
        return new AuthSession
        {
            PlayerName = profile.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
            Uuid = profile.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            AccessToken = mcToken,
            Xuid = uhs,
        };
    }

    private async Task<(string Token, string Uhs)> PostXboxAsync(string url, string body, CancellationToken ct)
    {
        using var response = await _http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct)
            .ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // 常见：XSTS 返回 401 + XErr 2148916233（没有 Xbox 账号）等
            if (json.Contains("2148916233", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("这个微软账号还没有 Xbox 档案，请先在 xbox.com 创建");
            }

            if (json.Contains("2148916238", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("未成年账号，需要家长批准");
            }

            throw new InvalidOperationException("Xbox 认证失败: " + Trim(json, 200));
        }

        using var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("Token").GetString() ?? string.Empty;
        var uhs = doc.RootElement.GetProperty("DisplayClaims").GetProperty("xui")[0]
            .GetProperty("uhs").GetString() ?? string.Empty;
        return (token, uhs);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body, string message)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{message}: {Trim(body, 300)}");
        }
    }

    private static string Trim(string text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max] + "…";
}
