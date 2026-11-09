using Aurora.Core.Infrastructure;
using Aurora.Core.Models;
using Aurora.Core.Versions;

namespace Aurora.Core.Net;

/// <summary>按 version.json 把依赖库下载到 .minecraft/libraries（支持镜像与 maven 坐标补全）。</summary>
public sealed class LibraryInstaller
{
    private readonly DownloadClient _client;
    private readonly string _gameDirectory;

    public LibraryInstaller(DownloadClient client, string gameDirectory)
    {
        _client = client;
        _gameDirectory = gameDirectory;
    }

    public string LibrariesDirectory => Path.Combine(_gameDirectory, "libraries");

    public async Task<int> InstallAsync(VersionJson json, IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var libraries = json.Libraries ?? new List<Library>();
        var tasks = new List<(string Url, string Path, string? Sha1, long? Size, string Name)>();

        foreach (var lib in libraries)
        {
            var coord = MavenHelper.Parse(lib.Name);
            var artifact = lib.Downloads?.Artifact;

            if (artifact?.Url != null)
            {
                var path = artifact.Path != null
                    ? Path.Combine(LibrariesDirectory, artifact.Path.Replace('/', Path.DirectorySeparatorChar))
                    : coord == null ? null : Path.Combine(LibrariesDirectory, coord.RelativePath);
                if (path != null)
                {
                    tasks.Add((artifact.Url, path, artifact.Sha1, artifact.Size, lib.Name));
                }
            }
            else if (coord != null && !string.IsNullOrEmpty(lib.Url))
            {
                var url = lib.Url!.TrimEnd('/') + "/" + coord.RelativePath.Replace(Path.DirectorySeparatorChar, '/');
                tasks.Add((url, Path.Combine(LibrariesDirectory, coord.RelativePath), null, null, lib.Name));
            }

            // natives 分类器
            if (lib.Natives != null && lib.Natives.TryGetValue(RuleEvaluator.CurrentOsName, out var template))
            {
                var classifier = template.Replace("${arch}", RuleEvaluator.ArchBits);
                var info = lib.Downloads?.Classifiers != null &&
                           lib.Downloads.Classifiers.TryGetValue(classifier, out var c)
                    ? c
                    : null;

                if (info?.Url != null)
                {
                    var path = info.Path != null
                        ? Path.Combine(LibrariesDirectory, info.Path.Replace('/', Path.DirectorySeparatorChar))
                        : null;
                    if (path != null)
                    {
                        tasks.Add((info.Url, path, info.Sha1, info.Size, lib.Name + ":" + classifier));
                    }
                }
                else if (coord != null && !string.IsNullOrEmpty(lib.Url))
                {
                    var withClassifier = new MavenHelper.Coordinate(coord.Group, coord.Artifact, coord.Version, classifier, "jar");
                    var url = lib.Url!.TrimEnd('/') + "/" + withClassifier.RelativePath.Replace(Path.DirectorySeparatorChar, '/');
                    tasks.Add((url, Path.Combine(LibrariesDirectory, withClassifier.RelativePath), null, null, lib.Name + ":" + classifier));
                }
            }
        }

        var done = 0;
        var failed = 0;
        using var gate = new SemaphoreSlim(8);
        var running = new List<Task>();

        foreach (var task in tasks)
        {
            ct.ThrowIfCancellationRequested();
            await gate.WaitAsync(ct).ConfigureAwait(false);
            running.Add(Task.Run(async () =>
            {
                try
                {
                    await _client.DownloadFileAsync(task.Url, task.Path, task.Sha1, task.Size, ct).ConfigureAwait(false);
                }
                catch
                {
                    Interlocked.Increment(ref failed);
                }
                finally
                {
                    gate.Release();
                    var current = Interlocked.Increment(ref done);
                    if (current % 5 == 0 || current == tasks.Count)
                    {
                        progress?.Report(new InstallProgress
                        {
                            Stage = "依赖库",
                            Done = current,
                            Total = tasks.Count,
                            Message = $"库 {current}/{tasks.Count}",
                        });
                    }
                }
            }, ct));
        }

        await Task.WhenAll(running).ConfigureAwait(false);
        if (failed > 0)
        {
            progress?.Report(new InstallProgress
            {
                Stage = "依赖库",
                Done = done,
                Total = tasks.Count,
                Message = $"{failed} 个库下载失败（可能是镜像缺失）",
            });
        }

        return tasks.Count - failed;
    }
}
