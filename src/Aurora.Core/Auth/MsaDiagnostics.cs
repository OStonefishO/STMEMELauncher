using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Aurora.Core.Auth;

/// <summary>诊断里的一步。</summary>
public sealed class MsaStep
{
    public string Name { get; init; } = string.Empty;
    public bool Ok { get; init; }
    public string Detail { get; init; } = string.Empty;
    public bool Skipped { get; init; }
}

public sealed class MsaDiagnosticResult
{
    public List<MsaStep> Steps { get; init; } = new();
    public bool AllOk => Steps.Count > 0 && Steps.All(s => s.Ok);
    public string DeviceCode { get; init; } = string.Empty;
    public string VerificationUrl { get; init; } = "https://www.microsoft.com/link";
}

/// <summary>
/// 微软登录四步诊断：① MSA token → ② Xbox Live → ③ XSTS → ④ Minecraft Services。
/// 第 ④ 步 403 就是「AppID 还没进 Mojang 白名单」，其它步骤失败则是配置问题。
/// </summary>
public static class MsaDiagnostics
{
    private const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";

    public sealed class DeviceCodeInfo
    {
        public string Code { get; init; } = string.Empty;
        public string Url { get; init; } = "https://www.microsoft.com/link";
        public string DeviceCode { get; init; } = string.Empty;
        public int Interval { get; init; } = 5;
        public int ExpiresIn { get; init; } = 900;
    }

    /// <summary>第 0 步：拿设备码，界面显示给用户去登录。</summary>
    public static async Task<DeviceCodeInfo> StartAsync(string clientId, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["scope"] = "XboxLive.signin offline_access",
        });

        using var response = await http.PostAsync("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode", form, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("获取设备码失败: " + Short(body));
        }

        using var doc = JsonDocument.Parse(body);
        return new DeviceCodeInfo
        {
            Code = doc.RootElement.GetProperty("user_code").GetString() ?? string.Empty,
            Url = doc.RootElement.TryGetProperty("verification_uri", out var u) ? u.GetString() ?? string.Empty : string.Empty,
            DeviceCode = doc.RootElement.GetProperty("device_code").GetString() ?? string.Empty,
            Interval = doc.RootElement.TryGetProperty("interval", out var i) ? i.GetInt32() : 5,
            ExpiresIn = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 900,
        };
    }

    /// <summary>第 ①~④ 步，逐条回调给界面。</summary>
    public static async Task<MsaDiagnosticResult> RunAsync(
        string clientId,
        DeviceCodeInfo device,
        Action<MsaStep> onStep,
        Action<string>? onStatus = null,
        CancellationToken ct = default)
    {
        var result = new MsaDiagnosticResult { DeviceCode = device.Code, VerificationUrl = device.Url };
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        void Report(MsaStep step)
        {
            result.Steps.Add(step);
            onStep(step);
        }

        // ---------- ① MSA token ----------
        onStatus?.Invoke("等待你在浏览器完成登录…");
        string? accessToken = null;
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(60, device.ExpiresIn));
        var refreshed = DateTime.UtcNow.AddSeconds(device.ExpiresIn);

        while (DateTime.UtcNow < deadline && accessToken == null)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await Task.Delay(Math.Max(3, device.Interval) * 1000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            string body;
            try
            {
                using var poll = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = clientId,
                    ["device_code"] = device.DeviceCode,
                }), ct);
                body = await poll.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                onStatus?.Invoke("网络抖动，继续等待登录…");
                continue;
            }

            if (body.Contains("access_token", StringComparison.Ordinal))
            {
                using var doc = JsonDocument.Parse(body);
                accessToken = doc.RootElement.GetProperty("access_token").GetString();
                Report(new MsaStep { Name = "① 微软账号登录（MSA token）", Ok = true, Detail = "已拿到 access_token" });
                break;
            }

            if (body.Contains("authorization_declined", StringComparison.Ordinal))
            {
                Report(new MsaStep { Name = "① 微软账号登录（MSA token）", Ok = false, Detail = "你在浏览器里取消了授权" });
                return result;
            }

            if (body.Contains("expired_token", StringComparison.Ordinal) || DateTime.UtcNow > refreshed)
            {
                Report(new MsaStep { Name = "① 微软账号登录（MSA token）", Ok = false, Detail = "设备码已过期，请重新开始" });
                return result;
            }

            if (!body.Contains("authorization_pending", StringComparison.Ordinal))
            {
                Report(new MsaStep { Name = "① 微软账号登录（MSA token）", Ok = false, Detail = Short(body) });
                return result;
            }
        }

        if (accessToken == null)
        {
            if (result.Steps.Count == 0)
            {
                Report(new MsaStep { Name = "① 微软账号登录（MSA token）", Ok = false, Detail = "超时没有完成登录" });
            }

            return result;
        }

        // ---------- ② Xbox Live ----------
        onStatus?.Invoke("正在请求 Xbox Live…");
        string? xblToken = null;
        string? uhs = null;
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                Properties = new { AuthMethod = "RPS", SiteName = "user.auth.xboxlive.com", RpsTicket = "d=" + accessToken },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT",
            });

            using var response = await http.PostAsync("https://user.auth.xboxlive.com/user/authenticate",
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                Report(new MsaStep
                {
                    Name = "② Xbox Live 授权",
                    Ok = false,
                    Detail = $"HTTP {(int)response.StatusCode}：{Short(body)}",
                });
                return result;
            }

            using var doc = JsonDocument.Parse(body);
            xblToken = doc.RootElement.GetProperty("Token").GetString();
            uhs = doc.RootElement.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString();
            Report(new MsaStep { Name = "② Xbox Live 授权", Ok = true, Detail = "已拿到 XBL token（uhs " + uhs + "）" });
        }
        catch (Exception ex)
        {
            Report(new MsaStep { Name = "② Xbox Live 授权", Ok = false, Detail = ex.Message });
            return result;
        }

        // ---------- ③ XSTS ----------
        onStatus?.Invoke("正在换取 XSTS 令牌…");
        string? xstsToken = null;
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                Properties = new { UserTokens = new[] { xblToken }, SandboxId = "RETAIL" },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT",
            });

            using var response = await http.PostAsync("https://xsts.auth.xboxlive.com/xsts/authorize",
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                Report(new MsaStep
                {
                    Name = "③ XSTS 令牌",
                    Ok = false,
                    Detail = $"HTTP {(int)response.StatusCode}：{ExplainXstsError(body)}",
                });
                return result;
            }

            using var doc = JsonDocument.Parse(body);
            xstsToken = doc.RootElement.GetProperty("Token").GetString();
            Report(new MsaStep { Name = "③ XSTS 令牌", Ok = true, Detail = "RETAIL 沙箱通过" });
        }
        catch (Exception ex)
        {
            Report(new MsaStep { Name = "③ XSTS 令牌", Ok = false, Detail = ex.Message });
            return result;
        }

        // ---------- ④ Minecraft Services（白名单） ----------
        onStatus?.Invoke("正在请求 Minecraft Services（这一步检查白名单）…");
        try
        {
            var payload = JsonSerializer.Serialize(new { identityToken = $"XBL3.0 x={uhs};{xstsToken}" });
            using var response = await http.PostAsync("https://api.minecraftservices.com/authentication/login_with_xbox",
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var code = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                Report(new MsaStep
                {
                    Name = "④ Minecraft Services",
                    Ok = true,
                    Detail = "已拿到 Minecraft access token —— AppID 在白名单里，正版登录可用",
                });
            }
            else if (code == 403 && body.Contains("Invalid app registration", StringComparison.OrdinalIgnoreCase))
            {
                Report(new MsaStep
                {
                    Name = "④ Minecraft Services",
                    Ok = false,
                    Detail = "403 Invalid app registration —— AppID 还没进 Mojang 白名单（前 3 步都正常，只差审批）",
                });
            }
            else if (code == 401)
            {
                Report(new MsaStep
                {
                    Name = "④ Minecraft Services",
                    Ok = false,
                    Detail = "401：XSTS 令牌被拒，通常是账号没买 Minecraft 或被 Xbox 限制",
                });
            }
            else
            {
                Report(new MsaStep
                {
                    Name = "④ Minecraft Services",
                    Ok = false,
                    Detail = $"HTTP {code}：{Short(body)}",
                });
            }
        }
        catch (Exception ex)
        {
            Report(new MsaStep { Name = "④ Minecraft Services", Ok = false, Detail = ex.Message });
        }

        return result;
    }

    /// <summary>把 XSTS 的常见错误码翻译成人话。</summary>
    public static string ExplainXstsError(string body)
    {
        if (body.Contains("2148916233", StringComparison.Ordinal))
        {
            return "这个微软账号还没有 Xbox 档案，先去 xbox.com 登录一次创建";
        }

        if (body.Contains("2148916235", StringComparison.Ordinal))
        {
            return "账号所在地区不允许 Xbox Live";
        }

        if (body.Contains("2148916238", StringComparison.Ordinal))
        {
            return "未成年账号需要家长同意（加入家庭组）";
        }

        return Short(body);
    }

    private static string Short(string text)
    {
        var flat = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return flat.Length > 220 ? flat[..220] + "…" : flat;
    }
}
