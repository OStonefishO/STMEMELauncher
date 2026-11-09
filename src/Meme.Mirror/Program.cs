using System.Diagnostics;
using Meme.Mirror;

var port = 8123;
var cache = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MEMEMirror", "cache");
var open = true;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port" when i + 1 < args.Length:
            int.TryParse(args[++i], out port);
            break;
        case "--cache" when i + 1 < args.Length:
            cache = args[++i];
            break;
        case "--no-open":
            open = false;
            break;
    }
}

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("MEME Mirror v0.1");
Console.WriteLine($"  地址: http://127.0.0.1:{port}/");
Console.WriteLine($"  缓存: {cache}");

var server = new MirrorServer(port, cache);
try
{
    server.Start();
}
catch (Exception ex)
{
    Console.Error.WriteLine("启动失败: " + ex.Message);
    Console.Error.WriteLine("端口可能被占用，换一个：--port 8124");
    return 1;
}

Console.WriteLine();
foreach (var source in server.Sources)
{
    Console.WriteLine($"  /m/{source.Key,-9} -> {source.Upstream}");
}

Console.WriteLine();
Console.WriteLine("按 Ctrl+C 停止。");

if (open)
{
    try
    {
        Process.Start(new ProcessStartInfo($"http://127.0.0.1:{port}/") { UseShellExecute = true });
    }
    catch
    {
        // 打不开浏览器就算了
    }
}

await Task.Delay(Timeout.Infinite);
return 0;
