using System.Diagnostics;
using System.Text;
using Aurora.Core;
using Aurora.Core.Configuration;
using Aurora.Core.Infrastructure;
using Aurora.Core.Java;
using Aurora.Core.Launch;
using Aurora.Core.Net;

namespace Aurora.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            return await RunAsync(args).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("错误: " + ex.Message);
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
        var rest = args.Skip(1).ToArray();
        var (flags, positional) = ParseFlags(rest);

        var settings = ConfigStore.Load();
        if (flags.TryGetValue("game", out var gameDir) && !string.IsNullOrWhiteSpace(gameDir))
        {
            settings.GameDirectory = Path.GetFullPath(gameDir);
        }

        var service = new LauncherService(settings);

        switch (cmd)
        {
            case "list":
                return ListVersions(service);
            case "info":
                return Info(service, positional.FirstOrDefault());
            case "plan":
                return Plan(service, positional.FirstOrDefault(), flags);
            case "launch":
                return await LaunchAsync(service, positional.FirstOrDefault(), flags).ConfigureAwait(false);
            case "java":
                return JavaReport(service);
            case "remote":
                return await RemoteAsync(service, flags).ConfigureAwait(false);
            case "install":
                return await InstallAsync(service, positional.FirstOrDefault(), flags).ConfigureAwait(false);
            case "doctor":
                return Doctor(service);
            case "loader":
                return await LoaderListAsync(service, positional).ConfigureAwait(false);
            case "loader-install":
                return await LoaderInstallAsync(service, positional).ConfigureAwait(false);
            case "search":
                return await SearchAsync(service, positional, flags).ConfigureAwait(false);
            case "pack":
                return await PackAsync(service, positional, flags).ConfigureAwait(false);
            case "skin":
                return await SkinAsync(service, positional, flags).ConfigureAwait(false);
            default:
                PrintHelp();
                return 0;
        }
    }

    private static int ListVersions(LauncherService service)
    {
        var versions = service.ListVersions();
        Console.WriteLine($"游戏目录: {service.Settings.GameDirectory}");
        Console.WriteLine($"共 {versions.Count} 个版本\n");
        Console.WriteLine($"{"版本",-32}{"MC",-10}{"加载器",-10}{"加载器版本",-18}{"Java",-6}{"Mods",-6}{"状态"}");
        Console.WriteLine(new string('-', 100));
        foreach (var v in versions)
        {
            var state = v.IsUsable ? (v.HasJar ? "OK" : "缺 jar") : ("异常: " + v.Error);
            Console.WriteLine($"{Trim(v.Id, 31),-32}{Trim(v.ClientVersion, 9),-10}{v.Loader,-10}{Trim(v.LoaderVersion ?? "-", 17),-18}{v.JavaMajor,-6}{v.ModCount,-6}{state}");
        }

        return 0;
    }

    private static int Info(LauncherService service, string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Console.Error.WriteLine("用法: aurora info <版本id>");
            return 1;
        }

        var resolved = service.Resolve(id);
        if (resolved == null)
        {
            Console.Error.WriteLine("找不到版本: " + id);
            return 1;
        }

        Console.WriteLine($"ID          : {resolved.Id}");
        Console.WriteLine($"目录        : {resolved.Directory}");
        Console.WriteLine($"Minecraft   : {resolved.ClientVersion}");
        Console.WriteLine($"加载器      : {resolved.Loader} {resolved.LoaderVersion}");
        Console.WriteLine($"主类        : {resolved.MainClass}");
        Console.WriteLine($"Java 要求   : {resolved.JavaMajor}");
        Console.WriteLine($"客户端 jar  : {resolved.JarPath ?? "(缺失)"}");
        Console.WriteLine($"资源索引    : {resolved.AssetIndex?.Id ?? "(无)"}");
        Console.WriteLine($"库数量      : {resolved.Libraries.Count}");
        Console.WriteLine($"游戏参数    : {resolved.GameArguments.Count} 组");
        Console.WriteLine($"JVM 参数    : {resolved.JvmArguments.Count} 组");

        var java = service.SelectJava(resolved.JavaMajor, out var all);
        Console.WriteLine($"选中 Java   : {(java == null ? "(未找到)" : java.JavaPath + "  [" + java.VersionText + "]")}");
        Console.WriteLine($"可用 Java   : {all.Count} 个");

        var plan = service.BuildPlan(id, out _, out var problems);
        if (plan != null)
        {
            Console.WriteLine($"classpath   : {plan.ClassPath.Count} 项");
            if (plan.Warnings.Count > 0)
            {
                Console.WriteLine("\n警告:");
                foreach (var w in plan.Warnings.Take(15))
                {
                    Console.WriteLine("  - " + w);
                }
            }
        }

        if (problems.Count > 0)
        {
            Console.WriteLine("\n问题:");
            foreach (var p in problems.Distinct().Take(15))
            {
                Console.WriteLine("  ! " + p);
            }
        }

        return 0;
    }

    private static int Plan(LauncherService service, string? id, Dictionary<string, string> flags)
    {
        if (string.IsNullOrEmpty(id))
        {
            Console.Error.WriteLine("用法: aurora plan <版本id> [--java 路径] [--name 玩家名]");
            return 1;
        }

        flags.TryGetValue("name", out var name);
        flags.TryGetValue("java", out var java);
        var plan = service.BuildPlan(id, out _, out var problems, name, java);
        if (plan == null)
        {
            Console.Error.WriteLine(string.Join("\n", problems));
            return 1;
        }

        Console.WriteLine("# Java: " + plan.JavaPath);
        Console.WriteLine("# 工作目录: " + plan.WorkingDirectory);
        Console.WriteLine("# natives: " + plan.NativesDirectory);
        Console.WriteLine("# classpath: " + plan.ClassPath.Count + " 项");
        if (plan.Warnings.Count > 0)
        {
            Console.WriteLine("# 警告: " + string.Join("; ", plan.Warnings.Distinct().Take(5)));
        }

        Console.WriteLine();
        Console.WriteLine(plan.CommandLinePreview);
        return 0;
    }

    private static async Task<int> LaunchAsync(LauncherService service, string? id, Dictionary<string, string> flags)
    {
        if (string.IsNullOrEmpty(id))
        {
            Console.Error.WriteLine("用法: aurora launch <版本id> [--name 玩家名] [--timeout 秒] [--dry]");
            return 1;
        }

        flags.TryGetValue("name", out var name);
        var plan = service.BuildPlan(id, out var resolved, out var problems, name);
        if (plan == null)
        {
            Console.Error.WriteLine(string.Join("\n", problems));
            return 1;
        }

        Console.WriteLine($"启动 {resolved!.Id} (MC {resolved.ClientVersion}, {resolved.Loader})");
        Console.WriteLine($"Java: {plan.JavaPath}");

        if (flags.ContainsKey("dry"))
        {
            Console.WriteLine(plan.CommandLinePreview);
            return 0;
        }

        var timeout = flags.TryGetValue("timeout", out var t) && int.TryParse(t, out var sec) ? sec : 0;
        using var cts = new CancellationTokenSource();
        if (timeout > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));
        }

        var launcher = new GameLauncher();
        launcher.OutputReceived += line => Console.WriteLine("  | " + line);
        var sw = Stopwatch.StartNew();
        var result = await launcher.LaunchAsync(plan, cts.Token).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"退出码: {result.ExitCode}  耗时: {result.Duration.TotalSeconds:0.0}s");
        if (!string.IsNullOrEmpty(result.Error))
        {
            Console.WriteLine("错误: " + result.Error);
        }

        if (!string.IsNullOrEmpty(result.CrashSummary))
        {
            Console.WriteLine("崩溃: " + result.CrashSummary);
            Console.WriteLine("报告: " + result.CrashReportPath);
        }

        return result.ExitCode == 0 ? 0 : 2;
    }

    private static int JavaReport(LauncherService service)
    {
        var all = service.ScanJava();
        Console.WriteLine($"发现 {all.Count} 个 Java:");
        foreach (var j in all)
        {
            Console.WriteLine($"  Java {j.Major,-3} {j.VersionText,-16} {j.JavaPath}");
        }

        if (all.Count == 0)
        {
            Console.WriteLine("  (无) 请把 JDK 目录加到配置的 ExtraJavaPaths");
        }

        return 0;
    }

    private static async Task<int> RemoteAsync(LauncherService service, Dictionary<string, string> flags)
    {
        using var client = new DownloadClient(service.Settings.UseMirror);
        var installer = new Installer(client, service.Settings.GameDirectory);
        var list = await installer.GetManifestAsync().ConfigureAwait(false);
        var installed = service.ListVersions().Select(v => v.ClientVersion).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var showAll = flags.ContainsKey("all");
        var items = showAll ? list : list.Where(v => v.Type == "release").Take(25).ToList();
        Console.WriteLine($"远程版本 {list.Count} 个，显示 {(showAll ? "全部" : "最近 25 个正式版")}:\n");
        foreach (var v in items)
        {
            var mark = installed.Contains(v.Id) ? "已安装" : string.Empty;
            Console.WriteLine($"  {v.Id,-16}{v.Type,-10}{v.ReleaseTime,-28}{mark}");
        }

        return 0;
    }

    private static async Task<int> InstallAsync(LauncherService service, string? id, Dictionary<string, string> flags)
    {
        if (string.IsNullOrEmpty(id))
        {
            Console.Error.WriteLine("用法: aurora install <版本id> [--no-assets]");
            return 1;
        }

        var downloadAssets = !flags.ContainsKey("no-assets");
        using var client = new DownloadClient(service.Settings.UseMirror);
        client.Progress = new Progress<(long Done, long Total)>(_ => { });
        var installer = new Installer(client, service.Settings.GameDirectory);
        var progress = new Progress<InstallProgress>(p =>
        {
            var percent = p.Total > 0 ? $" {p.Percent,5:0.0}%" : string.Empty;
            Console.WriteLine($"[{p.Stage}]{percent} {p.Message}");
        });

        Console.WriteLine($"安装 {id} -> {service.Settings.GameDirectory}");
        var json = await installer.InstallVanillaAsync(id, progress, downloadAssets).ConfigureAwait(false);
        Console.WriteLine("完成: " + json);
        return 0;
    }

    private static int Doctor(LauncherService service)
    {
        var s = service.Settings;
        Console.WriteLine("== 环境自检 ==");
        Console.WriteLine($"游戏目录     : {s.GameDirectory}  [{(Directory.Exists(s.GameDirectory) ? "存在" : "不存在")}]");
        Console.WriteLine($"versions     : {service.Versions.VersionsDirectory}");
        Console.WriteLine($"libraries    : {service.Versions.LibrariesDirectory}  [{(Directory.Exists(service.Versions.LibrariesDirectory) ? "存在" : "缺失")}]");
        Console.WriteLine($"assets       : {service.Versions.AssetsDirectory}  [{(Directory.Exists(service.Versions.AssetsDirectory) ? "存在" : "缺失")}]");
        var indexes = Path.Combine(service.Versions.AssetsDirectory, "indexes");
        Console.WriteLine($"asset index  : {(Directory.Exists(indexes) ? Directory.GetFiles(indexes, "*.json").Length + " 个" : "缺失")}");

        var versions = service.ListVersions();
        Console.WriteLine($"版本数量     : {versions.Count}（可用 {versions.Count(v => v.IsUsable)}）");
        Console.WriteLine($"配置文件     : {ConfigStore.ConfigPath}");

        var java = service.ScanJava();
        Console.WriteLine($"Java 数量    : {java.Count}");
        foreach (var j in java.GroupBy(x => x.Major).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  Java {j.Key}: {string.Join(", ", j.Select(x => x.VersionText).Distinct())}");
        }

        var missing = versions.Where(v => v.IsUsable && !v.HasJar).Select(v => v.Id).ToList();
        if (missing.Count > 0)
        {
            Console.WriteLine("缺少客户端 jar: " + string.Join(", ", missing));
        }

        Console.WriteLine("\n按 MC 版本所需的 Java:");
        foreach (var v in versions.Where(v => v.IsUsable))
        {
            var chosen = service.SelectJava(v.JavaMajor, out _);
            var ok = chosen != null && chosen.Major == v.JavaMajor ? "OK" : "缺";
            var chosenText = chosen == null ? "(无)" : chosen.VersionText;
            Console.WriteLine($"  {v.Id,-34} 需要 {v.JavaMajor,-3} -> {chosenText,-14} {ok}");
        }

        return 0;
    }

    private static async Task<int> LoaderListAsync(LauncherService service, List<string> positional)
    {
        var loader = positional.ElementAtOrDefault(0) ?? "fabric";
        var mc = positional.ElementAtOrDefault(1);
        if (string.IsNullOrEmpty(mc))
        {
            Console.Error.WriteLine("用法: aurora loader <fabric|neoforge|forge> <mc版本>");
            return 1;
        }

        var list = await service.CreateLoaderInstaller().ListAsync(loader, mc).ConfigureAwait(false);
        Console.WriteLine($"{loader} for {mc}: {list.Count} 个版本");
        foreach (var item in list.Take(15))
        {
            Console.WriteLine($"  {item.Version}{(item.IsStable ? string.Empty : "  (beta)")}");
        }

        return 0;
    }

    private static async Task<int> LoaderInstallAsync(LauncherService service, List<string> positional)
    {
        var loader = positional.ElementAtOrDefault(0) ?? "fabric";
        var mc = positional.ElementAtOrDefault(1);
        if (string.IsNullOrEmpty(mc))
        {
            Console.Error.WriteLine("用法: aurora loader-install <fabric|neoforge|forge> <mc版本> [加载器版本]");
            return 1;
        }

        var list = await service.CreateLoaderInstaller().ListAsync(loader, mc).ConfigureAwait(false);
        if (list.Count == 0)
        {
            Console.Error.WriteLine($"{loader} 没有支持 {mc} 的版本");
            return 1;
        }

        var wanted = positional.ElementAtOrDefault(2);
        var option = wanted == null ? list[0] : list.FirstOrDefault(o => o.Version == wanted);
        if (option == null)
        {
            Console.Error.WriteLine("找不到该加载器版本: " + wanted);
            return 1;
        }

        Console.WriteLine($"安装 {option.Loader} {option.Version} (MC {mc}) 到 {service.Settings.GameDirectory}");
        var progress = new Progress<InstallProgress>(p =>
            Console.WriteLine($"  [{p.Stage}] {p.Message}" + (p.Total > 0 ? $" ({p.Percent:0}%)" : string.Empty)));

        var id = await service.CreateLoaderInstaller().InstallAsync(option, progress).ConfigureAwait(false);
        Console.WriteLine("完成: " + id);
        return 0;
    }

    private static async Task<int> SearchAsync(LauncherService service, List<string> positional,
        Dictionary<string, string> flags)
    {
        var kindText = positional.ElementAtOrDefault(0) ?? "mod";
        var query = positional.ElementAtOrDefault(1) ?? string.Empty;
        var kind = kindText.ToLowerInvariant() switch
        {
            "modpack" => ContentKind.Modpack,
            "shader" => ContentKind.Shader,
            "resourcepack" => ContentKind.ResourcePack,
            _ => ContentKind.Mod,
        };

        var mc = flags.TryGetValue("mc", out var m) ? m : null;
        var loader = flags.TryGetValue("loader", out var l) ? l : null;
        IContentSource source = flags.ContainsKey("cf") ? service.CreateCurseForge() : service.CreateModrinth();

        var search = await source.SearchAsync(new ContentQuery
        {
            Kind = kind,
            Query = query,
            McVersion = mc,
            Loader = loader,
            PageSize = 10,
        }).ConfigureAwait(false);
        var items = search.Items;
        Console.WriteLine($"{source.Name} · {kind}: {items.Count} 个结果");
        foreach (var item in items)
        {
            Console.WriteLine($"  {item.Title}  [{item.DownloadText}]  {item.Slug}");
            Console.WriteLine($"     {item.Summary}");
        }

        if (items.Count > 0)
        {
            var files = await source.GetFilesAsync(items[0], mc, loader).ConfigureAwait(false);
            Console.WriteLine($"\n{items[0].Title} 的可用文件: {files.Count}");
            foreach (var f in files.Take(5))
            {
                Console.WriteLine($"  {f.DisplayName} · {f.FileName}");
                Console.WriteLine($"    {f.Url}");
            }
        }

        return 0;
    }

    private static async Task<int> PackAsync(LauncherService service, List<string> positional,
        Dictionary<string, string> flags)
    {
        var query = positional.FirstOrDefault() ?? string.Empty;
        var mc = flags.TryGetValue("mc", out var m) ? m : null;
        var loader = flags.TryGetValue("loader", out var l) ? l : null;

        var modrinth = service.CreateModrinth();
        var packed = await modrinth.SearchAsync(new ContentQuery
        {
            Kind = ContentKind.Modpack,
            Query = query,
            McVersion = mc,
            Loader = loader,
            PageSize = 10,
        }).ConfigureAwait(false);
        var items = packed.Items;
        if (items.Count == 0)
        {
            Console.Error.WriteLine("没有找到整合包");
            return 1;
        }

        foreach (var i in items.Take(5))
        {
            Console.WriteLine($"  {i.Title}  [{i.DownloadText}]  {i.Slug}");
        }

        var pick = items[0];
        var files = await modrinth.GetFilesAsync(pick, mc, loader).ConfigureAwait(false);
        if (files.Count == 0)
        {
            Console.Error.WriteLine("没有可用文件");
            return 1;
        }

        Console.WriteLine($"安装 {pick.Title} · {files[0].FileName}");
        var progress = new Progress<InstallProgress>(p =>
            Console.WriteLine($"  [{p.Stage}] {p.Message}"));
        var id = await service.CreateModpackInstaller().InstallAsync(files[0], progress).ConfigureAwait(false);
        Console.WriteLine("完成: " + id);
        return 0;
    }

    private static async Task<int> SkinAsync(LauncherService service, List<string> positional,
        Dictionary<string, string> flags)
    {
        var png = positional.ElementAtOrDefault(0);
        var name = positional.ElementAtOrDefault(1);
        if (string.IsNullOrEmpty(png) || string.IsNullOrEmpty(name))
        {
            Console.Error.WriteLine("用法: aurora skin <png> <账号名> --dir <目标目录> [--mc 26.2] [--loader fabric]");
            return 1;
        }

        var dir = flags.TryGetValue("dir", out var d) ? d : service.Settings.GameDirectory;
        var mc = flags.TryGetValue("mc", out var m) ? m : null;
        var loader = flags.TryGetValue("loader", out var l) ? l : "fabric";

        Console.WriteLine($"目标目录: {dir}");
        var progress = new Progress<InstallProgress>(p => Console.WriteLine($"  [{p.Stage}] {p.Message}"));
        var result = await service.CreateOfflineSkinService()
            .ApplyAsync(png, name, dir, mc, loader, progress).ConfigureAwait(false);

        Console.WriteLine("皮肤: " + result.SkinPath);
        Console.WriteLine("Mod : " + result.ModPath + (result.ModAlreadyExisted ? " (已存在)" : " (新装)"));
        Console.WriteLine("配置: " + result.ConfigPath);
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"Aurora Launcher CLI (框架/自检工具)

  aurora list                        列出本地版本
  aurora info <id>                   版本详情（继承链、库、Java）
  aurora plan <id>                   只生成命令行，不启动
  aurora launch <id> [--name 玩家] [--timeout 秒] [--dry]
  aurora java                        列出探测到的 Java
  aurora remote [--all]              查看远端版本清单
  aurora install <id> [--no-assets]  安装原版
  aurora doctor                      环境自检
  aurora loader <类型> <mc>           列出加载器版本（fabric/neoforge/forge）
  aurora loader-install <类型> <mc> [版本]
  aurora search <mod|modpack|shader|resourcepack> <关键词> [--mc 1.21.1] [--loader fabric] [--cf]
  aurora pack <关键词> [--mc 1.21.1] [--loader fabric]
  aurora skin <png> <账号名> --dir <目录> [--mc 26.2] [--loader fabric]

公共参数:
  --game <目录>                      临时指定 .minecraft 目录");
    }

    private static (Dictionary<string, string> Flags, List<string> Positional) ParseFlags(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positional = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(a);
                continue;
            }

            var key = a[2..];
            if (key.Contains('='))
            {
                var parts = key.Split('=', 2);
                result[parts[0]] = parts[1];
                continue;
            }

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                result[key] = args[++i];
            }
            else
            {
                result[key] = "true";
            }
        }

        return (result, positional);
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
