import io, re

# ---------- 1. XAML：概览页加「重装加载器」按钮 ----------
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
raw = io.open(p, encoding="utf-8", newline="").read()
nl = "\r\n" if "\r\n" in raw else "\n"
lines = raw.split(nl)

if any("OnVmReinstallLoader" in l for l in lines):
    print("button exists")
else:
    i = next(k for k, l in enumerate(lines) if 'x:Name="VmExportShortcut"' in l)
    # 在该按钮所在行之后插入
    indent = lines[i][:len(lines[i]) - len(lines[i].lstrip())]
    add = [
        f'{indent}<Button x:Name="VmReinstallLoaderButton" Content="重装加载器" Style="{{StaticResource GlassButton}}" Margin="0,0,8,8"',
        f'{indent}        Click="OnVmReinstallLoader" IsEnabled="False"',
        f'{indent}        ToolTip="清掉 libraries 里的加载器核心后重装（NeoForge 报 corrupted 时用）"/>',
    ]
    # 找到该按钮块的结束（以 /> 结尾）
    j = i
    while not lines[j].rstrip().endswith("/>"):
        j += 1
    lines[j + 1:j + 1] = add
    io.open(p, "w", encoding="utf-8", newline="").write(nl.join(lines))
    print("button inserted after", j)

# ---------- 2. 选中版本时启用按钮 ----------
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p2, encoding="utf-8", newline="").read()

if "VmReinstallLoaderButton" not in s:
    anchor = "        if (VmDeleteButton != null)\n        {\n            VmDeleteButton.IsEnabled = true;\n        }"
    if anchor in s:
        s = s.replace(anchor, anchor + '''

        if (VmReinstallLoaderButton != null)
        {
            VmReinstallLoaderButton.IsEnabled = _vmCurrent != null &&
                (_vmCurrent.Loader is "neoforge" or "forge");
        }''', 1)
        print("enable hook ok")
    else:
        print("enable anchor MISS")

# ---------- 3. 处理方法 ----------
if "OnVmReinstallLoader" not in s:
    helper = '''    /// <summary>清掉加载器的核心库后重装（NeoForge / Forge 报 corrupted 时的最后一招）。</summary>
    private async void OnVmReinstallLoader(object sender, RoutedEventArgs e)
    {
        if (_vmCurrent == null || _vmCurrent.Loader is not ("neoforge" or "forge"))
        {
            ShowToast("这个版本没有加载器", "只有 Forge / NeoForge 需要重装加载器", "⚠", 4000);
            return;
        }

        if (!Confirm("重装加载器",
                $"将清空 libraries 里 {_vmCurrent.Loader} 的核心文件，然后重新安装 {_vmCurrent.LoaderVersion}。\\n" +
                "不会动你的 mods / 存档 / 配置。继续吗？",
                "开始重装", "算了"))
        {
            return;
        }

        var root = GameRootFor(_vmCurrent.Directory) ?? _settings.GameDirectory;
        var libs = Path.Combine(root, "libraries", "net",
            _vmCurrent.Loader == "neoforge" ? "neoforged" : "minecraftforge");

        try
        {
            VmStatus.Text = "清理旧的核心库…";
            if (Directory.Exists(libs))
            {
                Directory.Delete(libs, recursive: true);
            }

            // srg / extra / slim 也一起清掉，让重装自己生成
            var clientLibs = Path.Combine(root, "libraries", "net", "minecraft", "client");
            if (Directory.Exists(clientLibs))
            {
                Directory.Delete(clientLibs, recursive: true);
            }

            VmStatus.Text = "重新安装 " + _vmCurrent.Loader + " " + _vmCurrent.LoaderVersion + "…";
            var installer = _service.CreateLoaderInstaller();
            var option = new Aurora.Core.Net.LoaderOption
            {
                Loader = _vmCurrent.Loader,
                Version = _vmCurrent.LoaderVersion ?? string.Empty,
                McVersion = _vmCurrent.ClientVersion,
            };

            var progress = new Progress<Aurora.Core.Models.InstallProgress>(p =>
            {
                VmStatus.Text = $"[{p.Stage}] {p.Message} · {p.Percent:0}%";
            });

            var id = await installer.InstallAsync(option, progress);
            VmStatus.Text = "重装完成：" + id;
            ShowToast("加载器已重装", id, "✓", 5000);
            LoadVersions();
        }
        catch (Exception ex)
        {
            VmStatus.Text = "重装失败: " + ex.Message;
            ShowToast("重装失败", ex.Message, "⚠", 7000);
        }
    }

'''
    m = re.search(r"(?m)^([ \t]*)private void OnVmExportPack\(", s)
    s = s[:m.start()] + helper + s[m.start():]
    print("handler inserted")

io.open(p2, "w", encoding="utf-8", newline="").write(s)
print("done")
