using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aurora.Core.Net;

/// <summary>
/// 内容页的「人话」翻译：模组简介和分类标签。
/// 标签优先用内置词表（instant、不联网也准），句子走免费翻译接口，失败就保持原文。
/// </summary>
public static class Translator
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly ConcurrentDictionary<string, string> Cache = LoadCache();
    private static readonly object CacheLock = new();
    private static int _pendingWrites;

    private static string CachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MEMELauncher", "translate-cache.json");

    private static ConcurrentDictionary<string, string> LoadCache()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MEMELauncher", "translate-cache.json");

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (map != null)
                {
                    return new ConcurrentDictionary<string, string>(map);
                }
            }
        }
        catch
        {
            // 缓存坏了就当没有
        }

        return new ConcurrentDictionary<string, string>();
    }

    private static void SaveCache()
    {
        try
        {
            var path = CachePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(Cache);
            File.WriteAllText(path, json);
        }
        catch
        {
            // 写不进去就算了
        }
    }


    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/124.0 Safari/537.36");
        return client;
    }

    /// <summary>Modrinth / CurseForge 常见分类的中文名，查得到就直接用。</summary>
    private static readonly Dictionary<string, string> Tags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["adventure"] = "冒险",
        ["combat"] = "战斗",
        ["decoration"] = "装饰",
        ["decoration"] = "装饰",
        ["economy"] = "经济",
        ["equipment"] = "装备",
        ["food"] = "食物",
        ["game-mechanics"] = "机制",
        ["library"] = "前置库",
        ["magic"] = "魔法",
        ["management"] = "管理",
        ["minigame"] = "小游戏",
        ["mobs"] = "生物",
        ["optimization"] = "优化",
        ["redstone"] = "红石",
        ["storage"] = "存储",
        ["technology"] = "科技",
        ["transportation"] = "交通",
        ["utility"] = "实用",
        ["worldgen"] = "世界生成",
        ["miscellaneous"] = "杂项",
        ["cursed"] = "整活",
        ["social"] = "社交",
        ["challenging"] = "硬核",
        ["lightweight"] = "轻量",
        ["quests"] = "任务",
        ["multiplayer"] = "多人",
        ["kitchen-sink"] = "大杂烩",
        ["shaders"] = "光影",
        ["realistic"] = "写实",
        ["fantasy"] = "奇幻",
        ["medieval"] = "中世纪",
        ["modern"] = "现代",
        ["sci-fi"] = "科幻",
        ["simplistic"] = "简约",
        ["themed"] = "主题",
        ["tweaks"] = "微调",
        ["fonts"] = "字体",
        ["locale"] = "语言",
        ["modded"] = "模组",
        ["vanilla"] = "原版",
        ["fabric"] = "Fabric",
        ["forge"] = "Forge",
        ["neoforge"] = "NeoForge",
        ["quilt"] = "Quilt",
        ["datapack"] = "数据包",
        ["mod"] = "模组",
        ["modpack"] = "整合包",
        ["resourcepack"] = "资源包",
        ["shader"] = "光影",
    };

    /// <summary>分类标签：查词表，查不到就原样返回。</summary>
    public static string Tag(string raw) =>
        string.IsNullOrWhiteSpace(raw) ? raw :
        Tags.TryGetValue(raw.Trim(), out var zh) ? zh : raw;

    public static string TagsText(IEnumerable<string> tags) =>
        string.Join(" / ", tags.Select(Tag));

    /// <summary>翻译一段英文；已经有中文、或翻译失败时原样返回。</summary>
    public static async Task<string> TextAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text) || HasChinese(text))
        {
            return text;
        }

        var key = text.Trim();
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // 国内 Google 基本不通，MyMemory 反而稳，顺序反一下
        var translated = await ViaMyMemoryAsync(key, ct).ConfigureAwait(false)
                         ?? await ViaGoogleAsync(key, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(translated))
        {
            return text;
        }

        Cache[key] = translated;
        return translated;
    }

    /// <summary>一次翻好几段：并发跑，单条失败不影响其它。</summary>
    public static async Task<List<string>> TextBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var results = new List<string>(texts.Count);
        foreach (var text in texts)
        {
            results.Add(await TextAsync(text, ct).ConfigureAwait(false));
        }

        return results;
    }
    private static async Task<string?> ViaGoogleAsync(string text, CancellationToken ct)
    {
        try
        {
            var url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=zh-CN&dt=t&q=" +
                      Uri.EscapeDataString(text);
            var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                return null;
            }

            var builder = new System.Text.StringBuilder();
            foreach (var part in doc.RootElement[0].EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.Array && part.GetArrayLength() > 0 &&
                    part[0].ValueKind == JsonValueKind.String)
                {
                    builder.Append(part[0].GetString());
                }
            }

            var result = builder.ToString().Trim();
            return result.Length == 0 ? null : result;
        }
        catch
        {
            return null;
        }
    }

    // ---- 用户自己填的 OpenAI 兼容接口（有 key 就走它，质量最好、没有每日限额）----
    public static string ApiKey { get; private set; } = string.Empty;
    public static string BaseUrl { get; private set; } = "https://api.deepseek.com/v1";
    public static string Model { get; private set; } = "deepseek-chat";

    public static void Configure(string? apiKey, string? baseUrl, string? model)
    {
        ApiKey = apiKey?.Trim() ?? string.Empty;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.deepseek.com/v1" : baseUrl.Trim();
        Model = string.IsNullOrWhiteSpace(model) ? "deepseek-chat" : model.Trim();
    }

    private static async Task<string?> ViaOpenAiAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            return null;
        }

        try
        {
            using var request = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Post,
                BaseUrl.TrimEnd('/') + "/chat/completions");

            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + ApiKey);

            var payload = JsonSerializer.Serialize(new
            {
                model = Model,
                temperature = 0.2,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "你是 Minecraft 启动器的翻译。把用户给的英文模组简介翻成简体中文，只输出译文，不要解释、不要加引号。保留专有名词（如 Sodium、Fabric、Forge、NeoForge、OptiFine）。",
                    },
                    new { role = "user", content = text },
                },
            });

            request.Content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim();

            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch
        {
            return null;
        }
    }
    private static async Task<string?> ViaMyMemoryAsync(string text, CancellationToken ct)
    {
        try
        {
            var query = text.Length > 480 ? text[..480] : text;
            var url = "https://api.mymemory.translated.net/get?langpair=en|zh-CN&q=" + Uri.EscapeDataString(query);
            var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("responseData", out var data) ||
                !data.TryGetProperty("translatedText", out var value))
            {
                return null;
            }

            var result = value.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasChinese(string text) => Regex.IsMatch(text, "[\u4e00-\u9fa5]");

    /// <summary>给 UI 判断这段文字是不是已经中文了。</summary>
    public static bool HasChineseText(string text) => !string.IsNullOrWhiteSpace(text) && HasChinese(text);
}
