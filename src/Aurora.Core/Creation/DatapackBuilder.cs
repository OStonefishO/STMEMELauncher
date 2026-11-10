using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Aurora.Core.Creation;

public enum DatapackTemplate
{
    Empty,        // 只有 pack.mcmeta
    Hello,        // 一个 /hello 函数 + 自动提示
    Recipe,       // 示例合成配方
    Advancement,  // 示例进度
    TickLoad,     // load / tick 自动执行
}

public sealed class DatapackRequest
{
    public string OutputPath { get; set; } = string.Empty;
    public string Name { get; set; } = "我的数据包";
    public string Description { get; set; } = "用 MEME Launcher 做的数据包";
    public string Namespace { get; set; } = "meme";
    public string McVersion { get; set; } = "1.21.1";
    public int? PackFormatOverride { get; set; }
    public DatapackTemplate Template { get; set; } = DatapackTemplate.Hello;
    public string FunctionBody { get; set; } = string.Empty;
}

public sealed class DatapackResult
{
    public string Path { get; init; } = string.Empty;
    public int PackFormat { get; init; }
    public List<string> Entries { get; init; } = new();
}

/// <summary>
/// 数据包生成：按 MC 版本算 pack_format，套模板写出目录结构再打 zip。
/// 目录结构参考 wiki：data/&lt;namespace&gt;/function(s)/、data/minecraft/tags/function/。
/// </summary>
public static class DatapackBuilder
{
    /// <summary>常见版本的 pack_format（1.21 之后 function 目录从 functions 改成 function）。</summary>
    public static int PackFormatFor(string mcVersion)
    {
        var v = (mcVersion ?? string.Empty).Trim().TrimStart('v', 'V');
        if (v.Length == 0)
        {
            return 48;
        }

        // 只保留数字和点，方便比较
        var clean = new string(v.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        if (!Version.TryParse(clean.Length == 0 ? "1.21" : clean, out var parsed))
        {
            return 48;
        }

        var major = parsed.Major;
        var minor = parsed.Minor;
        var build = parsed.Build < 0 ? 0 : parsed.Build;

        if (major != 1)
        {
            return 48;
        }

        return (minor, build) switch
        {
            (13, _) => 4,
            (14, _) => 4,
            (15, _) => 5,
            (16, 0) => 5,
            (16, _) => 6,
            (17, _) => 7,
            (18, 0) => 8,
            (18, _) => 9,
            (19, 0) => 10,
            (19, 1) => 10,
            (19, 2) => 10,
            (19, 3) => 12,
            (19, _) => 13,
            (20, 0) => 15,
            (20, 1) => 15,
            (20, 2) => 18,
            (20, 3) => 26,
            (20, 4) => 26,
            (20, 5) => 41,
            (20, 6) => 41,
            (21, 0) => 48,
            (21, 1) => 48,
            (21, 2) => 57,
            (21, 3) => 57,
            (21, 4) => 61,
            (21, 5) => 71,
            (21, 6) => 80,
            _ => 48,
        };
    }

    /// <summary>1.21 起函数目录叫 function（单数）。</summary>
    public static bool UsesSingularFunctionFolder(string mcVersion)
    {
        var clean = new string((mcVersion ?? string.Empty).TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        if (!Version.TryParse(clean.Length == 0 ? "1.21" : clean, out var v))
        {
            return true;
        }

        return v.Major > 1 || v.Minor >= 21;
    }

    public static DatapackResult Build(DatapackRequest request)
    {
        var format = request.PackFormatOverride ?? PackFormatFor(request.McVersion);
        var ns = NormalizeNamespace(request.Namespace);
        var functionFolder = UsesSingularFunctionFolder(request.McVersion) ? "function" : "functions";
        var tagFolder = functionFolder; // tags 目录同名（1.21 后也是 tags/function）
        var entries = new List<string>();

        if (File.Exists(request.OutputPath))
        {
            File.Delete(request.OutputPath);
        }

        using var zip = ZipFile.Open(request.OutputPath, ZipArchiveMode.Create);

        void Add(string path, string content)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using var stream = entry.Open();
            var buffer = Encoding.UTF8.GetBytes(content);
            stream.Write(buffer, 0, buffer.Length);
            entries.Add(path);
        }

        var meta = JsonSerializer.Serialize(new
        {
            pack = new
            {
                pack_format = format,
                description = string.IsNullOrWhiteSpace(request.Description) ? request.Name : request.Description,
            },
        }, new JsonSerializerOptions { WriteIndented = true });
        Add("pack.mcmeta", meta);

        var body = request.FunctionBody?.Trim() ?? string.Empty;

        switch (request.Template)
        {
            case DatapackTemplate.Empty:
                break;

            case DatapackTemplate.Hello:
                if (body.Length == 0)
                {
                    body = "# 玩家进游戏时提示一句\nsay 欢迎来到 " + request.Name + "!";
                }

                Add($"data/{ns}/{functionFolder}/hello.mcfunction", body);
                Add($"data/minecraft/tags/{tagFolder}/load.json",
                    JsonSerializer.Serialize(new { values = new[] { $"{ns}:hello" } }, new JsonSerializerOptions { WriteIndented = true }));
                break;

            case DatapackTemplate.Recipe:
                if (body.Length == 0)
                {
                    body = "say 收到一个自定义合成配方";
                }

                Add($"data/{ns}/{functionFolder}/hello.mcfunction", body);
                // 示例配方：一个泥土换钻石（1.20.5+ 的 result 结构不同）
                var modernRecipe = UsesSingularFunctionFolder(request.McVersion);
                var recipe = modernRecipe
                    ? @"{
  ""type"": ""minecraft:crafting_shapeless"",
  ""category"": ""misc"",
  ""ingredients"": [ ""minecraft:dirt"" ],
  ""result"": { ""id"": ""minecraft:diamond"", ""count"": 1 }
}"
                    : @"{
  ""type"": ""minecraft:crafting_shapeless"",
  ""ingredients"": [ { ""item"": ""minecraft:dirt"" } ],
  ""result"": { ""item"": ""minecraft:diamond"", ""count"": 1 }
}";
                Add($"data/{ns}/recipe/dirt_to_diamond.json", recipe);
                break;

            case DatapackTemplate.Advancement:
                var adv = @"{
  ""display"": {
    ""icon"": { ""item"": ""minecraft:grass_block"" },
    ""title"": """ + request.Name + @""",
    ""description"": """ + (string.IsNullOrWhiteSpace(request.Description) ? "做出来的第一个进度" : request.Description) + @""",
    ""frame"": ""task"",
    ""show_toast"": true,
    ""announce_to_chat"": true
  },
  ""criteria"": {
    ""joined"": { ""trigger"": ""minecraft:tick"" }
  }
}";
                Add($"data/{ns}/advancement/hello.json", adv);
                break;

            case DatapackTemplate.TickLoad:
                if (body.Length == 0)
                {
                    body = "# 每秒执行一次（20 tick）\n# 可以用 execute / scoreboard 等命令\nsay tick";
                }

                Add($"data/{ns}/{functionFolder}/tick.mcfunction", body);
                Add($"data/minecraft/tags/{tagFolder}/tick.json",
                    JsonSerializer.Serialize(new { values = new[] { $"{ns}:tick" } }, new JsonSerializerOptions { WriteIndented = true }));
                break;
        }

        Add("README.txt",
            $"{request.Name}\r\n========================\r\n" +
            (string.IsNullOrWhiteSpace(request.Description) ? string.Empty : request.Description + "\r\n\r\n") +
            $"Minecraft: {request.McVersion}\r\npack_format: {format}\r\n命名空间: {ns}\r\n\r\n" +
            "安装：把整个 zip 放进存档的 datapacks 目录，或在服务器 world/datapacks 里；\r\n" +
            "进游戏后执行 /reload 即可生效。\r\n\r\n导出自 MEME Launcher · STONEFISH8\r\n");

        return new DatapackResult
        {
            Path = request.OutputPath,
            PackFormat = format,
            Entries = entries,
        };
    }

    public static string NormalizeNamespace(string raw)
    {
        var text = (raw ?? string.Empty).Trim().ToLowerInvariant();
        var builder = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
            {
                builder.Append(c);
            }
        }

        var result = builder.ToString();
        return result.Length == 0 ? "meme" : result;
    }

    /// <summary>模板说明，界面上给个提示。</summary>
    public static string Describe(DatapackTemplate template) => template switch
    {
        DatapackTemplate.Empty => "只有 pack.mcmeta，之后你自己往里加内容",
        DatapackTemplate.Hello => "自带一个 /function meme:hello，进游戏时自动执行一次",
        DatapackTemplate.Recipe => "示例：一个泥土换一个钻石（改 JSON 就能做自己的配方）",
        DatapackTemplate.Advancement => "示例：一个「进游戏就完成」的进度，可当成就系统起点",
        DatapackTemplate.TickLoad => "自带 tick 函数，每秒执行一次，适合做计时/循环逻辑",
        _ => string.Empty,
    };
}
