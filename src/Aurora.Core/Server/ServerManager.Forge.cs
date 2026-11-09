using System.Diagnostics;
using System.Text;

namespace Aurora.Core.Server;

/// <summary>Forge / NeoForge 服务端的准备结果：要么一个 jar，要么一组 @args 文件。</summary>
public sealed class ForgeServerLayout
{
    public string Mode { get; init; } = "jar";

    /// <summary>Mode == jar 时的服务端 jar。</summary>
    public string JarPath { get; init; } = string.Empty;

    /// <summary>Mode == args 时的参数文件（win_args.txt / unix_args.txt）。</summary>
    public string ArgsFile { get; init; } = string.Empty;

    public string UserJvmArgsFile { get; init; } = string.Empty;
}

public sealed partial class ServerManager
{
    /// <summary>
    /// 下载官方安装器并跑 `--installServer`，然后找出启动方式。
    /// 安装器会自己去 maven 拉依赖，国内可能慢；安装器本身优先走 BMCLAPI 镜像。
    /// </summary>
    public async Task<ForgeServerLayout> InstallForgeServerAsync(string mcVersion, string loader,
        string loaderVersion, string targetDirectory, string javaPath,
        IProgress<Net.InstallProgress>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(targetDirectory);
        var isNeo = loader.Equals("neoforge", StringComparison.OrdinalIgnoreCase);

        var artifact = isNeo
            ? $"net/neoforged/neoforge/{loaderVersion}/neoforge-{loaderVersion}-installer.jar"
            : $"net/minecraftforge/forge/{mcVersion}-{loaderVersion}/forge-{mcVersion}-{loaderVersion}-installer.jar";

        var fileName = isNeo
            ? $"neoforge-{loaderVersion}-installer.jar"
            : $"forge-{mcVersion}-{loaderVersion}-installer.jar";

        var installerPath = Path.Combine(targetDirectory, fileName);
        if (!File.Exists(installerPath) || new FileInfo(installerPath).Length < 100_000)
        {
            progress?.Report(new Net.InstallProgress { Stage = "安装器", Message = "下载安装器（优先国内镜像）…" });

            var urls = new[]
            {
                "https://bmclapi2.bangbang93.com/maven/" + artifact,
                isNeo
                    ? "https://maven.neoforged.net/releases/" + artifact
                    : "https://maven.minecraftforge.net/" + artifact,
            };

            Exception? last = null;
            foreach (var url in urls)
            {
                try
                {
                    await _client.DownloadFileAsync(url, installerPath, ct: ct).ConfigureAwait(false);
                    last = null;
                    break;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            if (last != null)
            {
                throw new InvalidOperationException("安装器下载失败：" + last.Message);
            }
        }

        progress?.Report(new Net.InstallProgress { Stage = "安装器", Message = "运行 --installServer（可能要几分钟）…" });

        var psi = new ProcessStartInfo
        {
            FileName = javaPath,
            WorkingDirectory = targetDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(installerPath);
        psi.ArgumentList.Add("--installServer");

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => Emit(e.Data);
        process.ErrorDataReceived += (_, e) => Emit(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var finished = await Task.Run(() => process.WaitForExit(15 * 60 * 1000), ct).ConfigureAwait(false);
        if (!finished)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 忽略
            }

            throw new InvalidOperationException("安装器超过 15 分钟没结束，已中止");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"安装器退出码 {process.ExitCode}，看控制台日志");
        }

        progress?.Report(new Net.InstallProgress { Stage = "安装器", Message = "安装完成，查找启动方式…" });
        return LocateLayout(targetDirectory);
    }

    private static ForgeServerLayout LocateLayout(string directory)
    {
        var libraries = Path.Combine(directory, "libraries");
        var argsFile = Directory.Exists(libraries)
            ? Directory.GetFiles(libraries, "win_args.txt", SearchOption.AllDirectories).FirstOrDefault()
            : null;

        var userJvm = Path.Combine(directory, "user_jvm_args.txt");

        if (argsFile != null)
        {
            if (!File.Exists(userJvm))
            {
                File.WriteAllText(userJvm, "# 由 MEME Launcher 生成\n-Xmx2048M\n", new UTF8Encoding(false));
            }

            return new ForgeServerLayout
            {
                Mode = "args",
                ArgsFile = argsFile,
                UserJvmArgsFile = userJvm,
            };
        }

        var universal = Directory.GetFiles(directory, "*universal*.jar").FirstOrDefault()
                        ?? Directory.GetFiles(directory, "forge-*.jar")
                            .FirstOrDefault(f => !f.Contains("installer", StringComparison.OrdinalIgnoreCase));

        if (universal != null)
        {
            return new ForgeServerLayout { Mode = "jar", JarPath = universal };
        }

        throw new InvalidOperationException("安装完了但没找到可以启动的东西（win_args.txt / universal jar 都没有）");
    }

    /// <summary>把内存写进 user_jvm_args.txt（Forge/NeoForge 的启动器参数文件）。</summary>
    public static void SetJvmMemory(string userJvmArgsFile, int memoryMb)
    {
        var lines = File.Exists(userJvmArgsFile)
            ? File.ReadAllLines(userJvmArgsFile).Where(l => !l.TrimStart().StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase)).ToList()
            : new List<string>();

        lines.Add($"-Xmx{memoryMb}M");
        File.WriteAllText(userJvmArgsFile, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    }
}
