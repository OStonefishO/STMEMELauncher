import io
p = r"D:\综合创作\MCLauncher\src\Aurora.Core\Net\Installer.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 收集 hash + size
s = s.replace('''        var entries = new List<string>();
        foreach (var prop in objects.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("hash", out var hash))
            {
                var h = hash.GetString();
                if (!string.IsNullOrEmpty(h))
                {
                    entries.Add(h);
                }
            }
        }''','''        var entries = new List<(string Hash, long Size)>();
        foreach (var prop in objects.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("hash", out var hash))
            {
                var h = hash.GetString();
                if (!string.IsNullOrEmpty(h))
                {
                    var size = prop.Value.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt64(out var parsed)
                        ? parsed
                        : -1;
                    entries.Add((h, size));
                }
            }
        }''', 1)

# 2) 校验 size：存在但大小不对就重下（之前 0 字节文件会被当成已存在，导致进游戏崩）
s = s.replace('''        foreach (var hash in entries)
        {
            ct.ThrowIfCancellationRequested();''','''        foreach (var (hash, size) in entries)
        {
            ct.ThrowIfCancellationRequested();''', 1)

s = s.replace('''                    var relative = Path.Combine(hash[..2], hash);
                    var target = Path.Combine(AssetsDirectory, "objects", relative);
                    if (File.Exists(target))
                    {
                        return;
                    }''','''                    var relative = Path.Combine(hash[..2], hash);
                    var target = Path.Combine(AssetsDirectory, "objects", relative);
                    if (File.Exists(target))
                    {
                        var info = new FileInfo(target);
                        if (size <= 0 || info.Length == size)
                        {
                            return;   // 大小对得上才算真的下好了
                        }

                        try
                        {
                            info.Delete();
                        }
                        catch
                        {
                            // 删不掉就覆盖写
                        }
                    }''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("asset size check added")
