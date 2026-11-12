using System.Text.Json.Serialization;

namespace Aurora.Core.Models;

/// <summary>version.json 的原始结构（Mojang 格式）。字段全部可空，以容忍不同版本/加载器的差异。</summary>
public sealed class VersionJson
{
    public string? Id { get; set; }
    public string? InheritsFrom { get; set; }
    public string? Type { get; set; }
    public string? MainClass { get; set; }
    public string? Assets { get; set; }
    public AssetIndexInfo? AssetIndex { get; set; }
    public JavaVersionInfo? JavaVersion { get; set; }

    /// <summary>1.12 及更早使用的单行参数字符串。</summary>
    public string? MinecraftArguments { get; set; }

    /// <summary>1.13+ 使用的结构化参数。</summary>
    public ArgumentsSection? Arguments { get; set; }

    public List<Library>? Libraries { get; set; }
    public LoggingInfo? Logging { get; set; }
    public string? ClientVersion { get; set; }
    public int? ComplianceLevel { get; set; }
    public string? ReleaseTime { get; set; }
    public string? Time { get; set; }
    public int? MinimumLauncherVersion { get; set; }
    public string? Jar { get; set; }
}

public sealed class AssetIndexInfo
{
    public string? Id { get; set; }
    public string? Sha1 { get; set; }
    public long? Size { get; set; }
    public long? TotalSize { get; set; }
    public string? Url { get; set; }
}

public sealed class JavaVersionInfo
{
    public string? Component { get; set; }
    public int MajorVersion { get; set; }
}

public sealed class ArgumentsSection
{
    public List<ArgumentEntry>? Game { get; set; }
    public List<ArgumentEntry>? Jvm { get; set; }
}

/// <summary>一条参数：可以是纯字符串，也可以带 rules 的对象。</summary>
public sealed class ArgumentEntry
{
    public List<Rule>? Rules { get; set; }
    public List<string> Values { get; set; } = new();
}

public sealed class Rule
{
    public string? Action { get; set; }
    public OsRule? Os { get; set; }
    public Dictionary<string, bool>? Features { get; set; }
}

public sealed class OsRule
{
    public string? Name { get; set; }
    public string? Arch { get; set; }
    public string? Version { get; set; }
}

public sealed class Library
{
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public LibraryDownloads? Downloads { get; set; }
    public Dictionary<string, string>? Natives { get; set; }
    public List<Rule>? Rules { get; set; }
    public ExtractRule? Extract { get; set; }
}

public sealed class LibraryDownloads
{
    public ArtifactInfo? Artifact { get; set; }
    public Dictionary<string, ArtifactInfo>? Classifiers { get; set; }
}

public sealed class ArtifactInfo
{
    public string? Path { get; set; }
    public string? Sha1 { get; set; }
    public long? Size { get; set; }
    public string? Url { get; set; }
}

public sealed class ExtractRule
{
    public List<string>? Exclude { get; set; }
}

public sealed class LoggingInfo
{
    public LoggingClient? Client { get; set; }
}

public sealed class LoggingClient
{
    public string? Argument { get; set; }
    public LoggingFile? File { get; set; }
    public string? Type { get; set; }
}

public sealed class LoggingFile
{
    public string? Id { get; set; }
    public string? Sha1 { get; set; }
    public long? Size { get; set; }
    public string? Url { get; set; }
}

/// <summary>扫描磁盘上的一个版本目录得到的摘要，供 UI/CLI 列表使用。</summary>
public sealed class VersionSummary
{
    public string Id { get; init; } = string.Empty;
    public string Directory { get; init; } = string.Empty;
    public string? JsonPath { get; init; }
    public string? JarPath { get; set; }
    public string ClientVersion { get; init; } = string.Empty;
    public string Loader { get; init; } = "vanilla";
    public string? LoaderVersion { get; init; }
    public int JavaMajor { get; init; }
    public int ModCount { get; init; }
    public bool HasJar { get; init; }
    public bool HasJson { get; init; }
    public string? Error { get; init; }
    public bool IsUsable => HasJson && Error == null;
}

/// <summary>合并继承链之后的完整版本信息。</summary>
public sealed class ResolvedVersion
{
    public string Id { get; init; } = string.Empty;
    public string Directory { get; init; } = string.Empty;
    public string? JarPath { get; set; }
    public string MainClass { get; init; } = string.Empty;
    public string ClientVersion { get; init; } = string.Empty;
    public string Loader { get; init; } = "vanilla";
    public string? LoaderVersion { get; init; }
    public int JavaMajor { get; init; } = 8;
    public AssetIndexInfo? AssetIndex { get; init; }
    public string? AssetsRoot { get; init; }
    public List<Library> Libraries { get; init; } = new();
    public List<ArgumentEntry> JvmArguments { get; init; } = new();
    public List<ArgumentEntry> GameArguments { get; init; } = new();
    public LoggingInfo? Logging { get; init; }
    public string? Type { get; init; }
    public string JsonPath { get; init; } = string.Empty;
}
