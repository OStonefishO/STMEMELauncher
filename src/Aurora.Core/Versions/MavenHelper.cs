using Aurora.Core.Models;

namespace Aurora.Core.Versions;

/// <summary>把 maven 坐标（group:artifact:version[:classifier][@ext]）转成 libraries 下的文件路径。</summary>
public static class MavenHelper
{
    public sealed record Coordinate(string Group, string Artifact, string Version, string? Classifier, string Extension)
    {
        public string Key => $"{Group}:{Artifact}" + (Classifier != null ? ":" + Classifier : string.Empty);

        public string RelativePath
        {
            get
            {
                var file = Artifact + "-" + Version + (Classifier != null ? "-" + Classifier : string.Empty) + "." + Extension;
                return Path.Combine(Group.Replace('.', Path.DirectorySeparatorChar), Artifact, Version, file);
            }
        }
    }

    public static Coordinate? Parse(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var ext = "jar";
        var at = name.IndexOf('@');
        if (at >= 0)
        {
            ext = name[(at + 1)..];
            name = name[..at];
        }

        var parts = name.Split(':');
        if (parts.Length < 3)
        {
            return null;
        }

        var group = parts[0];
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length >= 4 && !string.IsNullOrEmpty(parts[3]) ? parts[3] : null;
        return new Coordinate(group, artifact, version, classifier, ext);
    }

    /// <summary>给一个库算出它在磁盘上的 jar 路径；拿不到坐标时返回 null。</summary>
    public static string? ResolveArtifactPath(Library library, string librariesDirectory)
    {
        var artifact = library.Downloads?.Artifact;
        if (!string.IsNullOrEmpty(artifact?.Path))
        {
            return Path.Combine(librariesDirectory, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
        }

        var coord = Parse(library.Name);
        return coord == null ? null : Path.Combine(librariesDirectory, coord.RelativePath);
    }

    /// <summary>库名去掉版本后的归一键，用于继承时去重（子版本优先）。</summary>
    public static string DedupKey(Library library)
    {
        var coord = Parse(library.Name);
        if (coord == null)
        {
            return library.Name;
        }

        return $"{coord.Group}:{coord.Artifact}:{coord.Classifier}";
    }
}
