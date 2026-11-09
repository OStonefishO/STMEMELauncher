namespace Meme.Mirror;

/// <summary>一个上游源：本地路径前缀 → 上游 base。</summary>
public sealed class MirrorSource
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Upstream { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>用来探活的相对路径（拿不到就算了）。</summary>
    public string ProbePath { get; init; } = string.Empty;

    public MirrorSourceStats Stats { get; } = new();
}

public sealed class MirrorSourceStats
{
    private long _hits;
    private long _misses;
    private long _bytes;
    private long _errors;

    public long Hits => Interlocked.Read(ref _hits);
    public long Misses => Interlocked.Read(ref _misses);
    public long Bytes => Interlocked.Read(ref _bytes);
    public long Errors => Interlocked.Read(ref _errors);
    public long BytesServed => Interlocked.Read(ref _bytes);

    public void Hit() => Interlocked.Increment(ref _hits);
    public void Miss() => Interlocked.Increment(ref _misses);
    public void Error() => Interlocked.Increment(ref _errors);
    public void AddBytes(long value) => Interlocked.Add(ref _bytes, value);
}
