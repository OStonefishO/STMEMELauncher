using System.Text.Json;
using System.Text.Json.Serialization;
using Aurora.Core.Models;

namespace Aurora.Core.Infrastructure;

public static class Json
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        o.Converters.Add(new ArgumentEntryConverter());
        return o;
    }

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}

/// <summary>arguments 数组里的元素既可能是字符串，也可能是 { rules, value } 对象。</summary>
public sealed class ArgumentEntryConverter : JsonConverter<ArgumentEntry>
{
    public override ArgumentEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var entry = new ArgumentEntry();
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                entry.Values.Add(reader.GetString() ?? string.Empty);
                return entry;

            case JsonTokenType.StartObject:
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Array)
                    {
                        entry.Rules = JsonSerializer.Deserialize<List<Rule>>(rules.GetRawText(), options);
                    }

                    if (root.TryGetProperty("value", out var value))
                    {
                        if (value.ValueKind == JsonValueKind.String)
                        {
                            entry.Values.Add(value.GetString() ?? string.Empty);
                        }
                        else if (value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in value.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                {
                                    entry.Values.Add(item.GetString() ?? string.Empty);
                                }
                            }
                        }
                    }
                }
                return entry;

            default:
                return entry;
        }
    }

    public override void Write(Utf8JsonWriter writer, ArgumentEntry value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
