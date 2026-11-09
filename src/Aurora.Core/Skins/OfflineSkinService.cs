using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aurora.Core.Skins;

public sealed class OfflineSkinResult
{
    public string SkinPath { get; init; } = string.Empty;
    public string ModPath { get; init; } = string.Empty;
    public string ConfigPath { get; init; } = string.Empty;
    public bool ModAlreadyExisted { get; init; }
}

/// <summary>
/// 离线皮肤：Minecraft 原版不会给离线账号下发皮肤，所以用 CustomSkinLoader 的
/// LocalSkin 机制——把 png 放进 CustomSkinLoader/LocalSkin/skins/名字.png，
/// 再保证 mod 和配置就位。
/// </summary>
public sealed class OfflineSkinService
{
    private const string ProjectSlug = "customskinloader";
    private const string Api = "https://api.modrinth.com/v2";

    private readonly Net.DownloadClient _client;

    public OfflineSkinService(Net.DownloadClient client) => _client = client;

    public async Task<OfflineSkinResult> ApplyAsync(string pngPath, string playerName, string gameDirectory,
        string? mcVersion, string? loader, IProgress<Net.InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(pngPath))
        {
            throw new FileNotFoundException("找不到皮肤文件", pngPath);
        }

        if (!pngPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("皮肤必须是 PNG（64x64 或 64x32）");
        }

        var name = Sanitize(playerName);
        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException("当前账号没有名字，无法生成皮肤文件");
        }

        Directory.CreateDirectory(gameDirectory);

        // 1. 保证 CustomSkinLoader 在 mods 里
        var modResult = await EnsureModAsync(gameDirectory, mcVersion, loader, progress, ct).ConfigureAwait(false);

        // 2. 放皮肤
        progress?.Report(new Net.InstallProgress { Stage = "皮肤", Message = "写入本地皮肤…" });
        var skinDir = Path.Combine(gameDirectory, "CustomSkinLoader", "LocalSkin", "skins");
        Directory.CreateDirectory(skinDir);
        var target = Path.Combine(skinDir, name + ".png");
        File.Copy(pngPath, target, overwrite: true);

        // 3. 保证配置里有 LocalSkin
        var configPath = EnsureConfig(gameDirectory);

        progress?.Report(new Net.InstallProgress
        {
            Stage = "皮肤",
            Done = 1,
            Total = 1,
            Message = $"已应用：{name}.png",
        });

        return new OfflineSkinResult
        {
            SkinPath = target,
            ModPath = modResult.Path,
            ConfigPath = configPath,
            ModAlreadyExisted = modResult.Existed,
        };
    }

    private async Task<(string Path, bool Existed)> EnsureModAsync(string gameDir, string? mcVersion, string? loader,
        IProgress<Net.InstallProgress>? progress, CancellationToken ct)
    {
        var modsDir = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(modsDir);

        var existing = Directory.GetFiles(modsDir, "*.jar")
            .FirstOrDefault(f => Path.GetFileName(f).Contains("CustomSkinLoader", StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return (existing, true);
        }

        progress?.Report(new Net.InstallProgress { Stage = "皮肤", Message = "下载 CustomSkinLoader…" });

        var loaderName = string.IsNullOrEmpty(loader) || loader == "vanilla" ? "fabric" : loader;
        var url = $"{Api}/project/{ProjectSlug}/version";
        var query = new List<string>();
        if (!string.IsNullOrEmpty(mcVersion))
        {
            query.Add("game_versions=" + Uri.EscapeDataString($"[\"{mcVersion}\"]"));
        }

        query.Add("loaders=" + Uri.EscapeDataString($"[\"{loaderName}\"]"));
        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        var json = await _client.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Modrinth 上没有适配 {mcVersion} / {loaderName} 的 CustomSkinLoader");
        }

        var version = doc.RootElement[0];
        var file = version.GetProperty("files")[0];
        var fileName = file.GetProperty("filename").GetString() ?? "CustomSkinLoader.jar";
        var download = file.GetProperty("url").GetString()
                       ?? throw new InvalidOperationException("拿不到下载地址");

        var target = Path.Combine(modsDir, fileName);
        await _client.DownloadFileAsync(download, target, ct: ct).ConfigureAwait(false);
        return (target, false);
    }

    /// <summary>确保 CustomSkinLoader.json 的 loadlist 里有 LocalSkin。</summary>
    private static string EnsureConfig(string gameDir)
    {
        var configDir = Path.Combine(gameDir, "CustomSkinLoader");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "CustomSkinLoader.json");

        JsonObject root;
        if (File.Exists(configPath))
        {
            try
            {
                root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        root["version"] ??= "14.19";
        root["enableDynamicSkull"] ??= true;
        root["enableTransparentSkin"] ??= true;
        root["enableCape"] ??= true;

        var loadlist = root["loadlist"] as JsonArray ?? new JsonArray();
        var hasLocal = loadlist.Any(node =>
            node is JsonObject o &&
            string.Equals(o["type"]?.GetValue<string>(), "LocalSkin", StringComparison.OrdinalIgnoreCase));

        if (!hasLocal)
        {
            loadlist.Add(new JsonObject
            {
                ["name"] = "LocalSkin",
                ["type"] = "LocalSkin",
                ["root"] = "CustomSkinLoader/LocalSkin",
            });
            root["loadlist"] = loadlist;
        }

        File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return configPath;
    }

    private static string Sanitize(string name)
    {
        var cleaned = name.Trim();
        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(bad, '_');
        }

        return cleaned;
    }
}
