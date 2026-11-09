using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Aurora.Core.Models;

namespace Aurora.Core.Net;

public enum DownloadState
{
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Canceled,
}

/// <summary>一条内容下载任务（可暂停 / 继续 / 取消）。</summary>
public sealed class DownloadItem : INotifyPropertyChanged
{
    private DownloadState _state = DownloadState.Queued;
    private long _received;
    private long _total;
    private string _error = string.Empty;
    private double _speed;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public string KindLabel { get; init; } = string.Empty;

    public DownloadState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            Raise();
            Raise(nameof(StateText));
            Raise(nameof(DetailText));
            Raise(nameof(CanPause));
            Raise(nameof(CanResume));
            Raise(nameof(CanCancel));
        }
    }

    public long ReceivedBytes
    {
        get => _received;
        set
        {
            _received = value;
            Raise();
            Raise(nameof(Progress));
            Raise(nameof(SizeText));
            Raise(nameof(DetailText));
        }
    }

    public long TotalBytes
    {
        get => _total;
        set
        {
            _total = value;
            Raise();
            Raise(nameof(Progress));
            Raise(nameof(SizeText));
            Raise(nameof(DetailText));
        }
    }

    public double SpeedBytesPerSecond
    {
        get => _speed;
        set
        {
            _speed = value;
            Raise();
            Raise(nameof(SpeedText));
            Raise(nameof(DetailText));
        }
    }

    public string Error
    {
        get => _error;
        set
        {
            _error = value;
            Raise();
        }
    }

    public double Progress => TotalBytes > 0 ? Math.Min(100, ReceivedBytes * 100.0 / TotalBytes) : 0;

    public string StateText => State switch
    {
        DownloadState.Queued => "排队中",
        DownloadState.Running => "下载中",
        DownloadState.Paused => "已暂停",
        DownloadState.Completed => "已完成",
        DownloadState.Failed => "失败",
        DownloadState.Canceled => "已取消",
        _ => string.Empty,
    };

    public string SizeText => TotalBytes > 0
        ? $"{ReceivedBytes / 1024.0 / 1024.0:0.0} / {TotalBytes / 1024.0 / 1024.0:0.0} MB"
        : $"{ReceivedBytes / 1024.0 / 1024.0:0.0} MB";

    /// <summary>一行摘要（Run 绑定只读属性会抛异常，统一用这个）。</summary>
    public string DetailText => string.Join("  ·  ", new[]
    {
        KindLabel,
        StateText,
        SizeText,
        SpeedText,
    }.Where(v => !string.IsNullOrWhiteSpace(v)));

    public string SpeedText => State == DownloadState.Running && SpeedBytesPerSecond > 0
        ? $"{SpeedBytesPerSecond / 1024.0 / 1024.0:0.0} MB/s"
        : string.Empty;

    public bool CanPause => State == DownloadState.Running || State == DownloadState.Queued;
    public bool CanResume => State == DownloadState.Paused || State == DownloadState.Failed;
    public bool CanCancel => State is DownloadState.Running or DownloadState.Queued or DownloadState.Paused;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 下载队列：最多两个并发，支持暂停（保留 .part 断点）、继续（Range 续传）、取消、打开目录。
/// </summary>
public sealed class DownloadManager
{
    private const int MaxConcurrent = 2;

    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly Dictionary<string, CancellationTokenSource> _running = new();
    private readonly SynchronizationContext? _sync = SynchronizationContext.Current;

    /// <summary>把属性变更送回 UI 线程，WPF 绑定不能跨线程改。</summary>
    private void Ui(Action action)
    {
        if (_sync == null)
        {
            action();
            return;
        }

        _sync.Post(_ => action(), null);
    }

    public ObservableCollection<DownloadItem> Tasks { get; } = new();

    public DownloadItem Enqueue(ContentFile file, ContentKind kind, string gameDirectory)
    {
        var folder = Path.Combine(gameDirectory, ContentKindInfo.Folder(kind));
        var name = Sanitize(file.FileName);

        var item = new DownloadItem
        {
            Title = string.IsNullOrWhiteSpace(file.DisplayName) ? name : file.DisplayName,
            FileName = name,
            Url = file.Url,
            TargetPath = Path.Combine(folder, name),
            KindLabel = ContentKindInfo.Label(kind),
        };

        Ui(() => Tasks.Insert(0, item));
        _ = Task.Run(PumpAsync);
        return item;
    }

    public void Pause(DownloadItem item)
    {
        if (!item.CanPause)
        {
            return;
        }

        if (_running.TryGetValue(item.Id, out var cts))
        {
            Ui(() => item.State = DownloadState.Paused);
            cts.Cancel();
        }
        else
        {
            Ui(() => item.State = DownloadState.Paused);
        }
    }

    public void Resume(DownloadItem item)
    {
        if (!item.CanResume)
        {
            return;
        }

        Ui(() =>
        {
            item.State = DownloadState.Queued;
            item.Error = string.Empty;
        });
        _ = Task.Run(PumpAsync);
    }

    public void Cancel(DownloadItem item)
    {
        if (_running.TryGetValue(item.Id, out var cts))
        {
            cts.Cancel();
        }

        Ui(() => item.State = DownloadState.Canceled);
        TryDelete(item.TargetPath + ".part");
    }

    public void ClearFinished()
    {
        for (var i = Tasks.Count - 1; i >= 0; i--)
        {
            if (Tasks[i].State is DownloadState.Completed or DownloadState.Canceled)
            {
                Tasks.RemoveAt(i);
            }
        }
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            if (Tasks.Count(t => t.State == DownloadState.Running) >= MaxConcurrent)
            {
                return;
            }

            var next = Tasks.LastOrDefault(t => t.State == DownloadState.Queued);
            if (next == null)
            {
                return;
            }

            _ = RunAsync(next);
            await Task.Delay(30).ConfigureAwait(false);
        }
    }

    private async Task RunAsync(DownloadItem item)
    {
        var cts = new CancellationTokenSource();
        _running[item.Id] = cts;
        Ui(() => item.State = DownloadState.Running);

        var part = item.TargetPath + ".part";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(item.TargetPath)!);

            long existing = File.Exists(part) ? new FileInfo(part).Length : 0;
            using var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
            if (existing > 0)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existing, null);
            }

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? 0;
            var startTotal = existing + total;
            var received = existing;
            Ui(() =>
            {
                item.TotalBytes = startTotal;
                item.ReceivedBytes = existing;
            });

            await using var source = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            await using var destination = new FileStream(part, FileMode.Append, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            var watch = Stopwatch.StartNew();
            var lastTick = 0L;
            var lastBytes = item.ReceivedBytes;

            int read;
            while ((read = await source.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cts.Token).ConfigureAwait(false);
                received += read;

                // 最多每 250ms 刷一次 UI，避免每个数据块都触发绑定
                if (watch.ElapsedMilliseconds - lastTick > 250)
                {
                    var elapsed = Math.Max(0.001, (watch.ElapsedMilliseconds - lastTick) / 1000.0);
                    var speed = (received - lastBytes) / elapsed;
                    lastTick = watch.ElapsedMilliseconds;
                    lastBytes = received;

                    var snapshot = received;
                    Ui(() =>
                    {
                        item.ReceivedBytes = snapshot;
                        item.SpeedBytesPerSecond = speed;
                    });
                }
            }

            destination.Close();
            if (File.Exists(item.TargetPath))
            {
                File.Delete(item.TargetPath);
            }

            File.Move(part, item.TargetPath);
            Ui(() =>
            {
                item.ReceivedBytes = Math.Max(item.ReceivedBytes, received);
                item.State = DownloadState.Completed;
                item.SpeedBytesPerSecond = 0;
            });
        }
        catch (OperationCanceledException)
        {
            // 暂停或取消：状态已经被设置过了
        }
        catch (Exception ex)
        {
            Ui(() =>
            {
                item.Error = ex.Message;
                item.State = DownloadState.Failed;
            });
        }
        finally
        {
            _running.Remove(item.Id);
            _ = Task.Run(PumpAsync);
        }
    }

    private static string Sanitize(string name)
    {
        var cleaned = name;
        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(bad, '_');
        }

        return string.IsNullOrWhiteSpace(cleaned) ? "download.bin" : cleaned;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 忽略
        }
    }
}
