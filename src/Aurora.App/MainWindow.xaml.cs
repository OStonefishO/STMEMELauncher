using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aurora.Core;
using Aurora.Core.Accounts;
using Aurora.Core.Auth;
using Aurora.Core.Configuration;
using Aurora.Core.Launch;
using Aurora.Core.Models;
using Aurora.Core.Net;
using Aurora.Core.Network;
using Aurora.Core.Server;

namespace Aurora.App;

public partial class MainWindow : Window
{
    private LauncherService _service;
    private readonly LauncherSettings _settings;
    private bool _busy;
    private List<RemoteVersion> _mcVersions = new();
    private ContentItem? _selectedContent;
    private CancellationTokenSource? _loginCts;
    private const string AppVersion = "0.15.6";
    private bool _updatesLoaded;
    private ReleaseInfo? _pendingRelease;
    private readonly SakuraFrp _tunnel = new();
    private readonly UpnpClient _upnp = new();
    private ServerManager? _server;
    private string _serverDirectory = string.Empty;
    private bool _serverVersionsLoaded;
    private List<VersionSummary> _serverVersions = new();
    private bool _onlineLoaded;

    public MainWindow()
    {
        InitializeComponent();

        // 高分屏（150%）下 760 DIP 可能超过工作区，按工作区收缩
        var area = SystemParameters.WorkArea;
        Width = Math.Min(1180, area.Width - 48);
        Height = Math.Min(760, area.Height - 48);

        _settings = ConfigStore.Load();
        _service = new LauncherService(_settings);

        _service.MigrateAccounts();
        _service.RestoreYggdrasil();
        InitHero();
        InitDownloadPanel();
        Loaded += (_, __) => InitParticles();

        LoadSettingsToUi();
        LoadVersions();
        InitContentFilters();
        SetPage(0);

        // 支持 --page 1/2/3 直接打开某一页（截图和调试用）
        var argv = Environment.GetCommandLineArgs();

        // 调试用：--music <文件或文件夹>，先把歌放进播放器再截图
        var musicFlag = Array.IndexOf(argv, "--music");
        if (musicFlag >= 0 && musicFlag + 1 < argv.Length)
        {
            EnsureMusicInited();
            SetFunTab(false);
            AddMusicFiles(new[] { argv[musicFlag + 1] });
        }

        var flag = Array.IndexOf(argv, "--page");
        if (flag >= 0 && flag + 1 < argv.Length && int.TryParse(argv[flag + 1], out var pageIndex))
        {
            SetPage(Math.Clamp(pageIndex, 0, 8));
        }
        Loaded += (_, __) =>
        {
            ApplyDarkTitleBar();
            _ = SplashFadeOutAsync();
            _ = InitSkinWebAsync();
            _ = CheckUpdatesAsync(silent: true);

            // 调试用：--shot <png>，等界面稳定后自己存一张图然后退出
            var shotFlag = Array.IndexOf(argv, "--shot");
            if (shotFlag >= 0 && shotFlag + 1 < argv.Length)
            {
                _ = CaptureAndExitAsync(argv[shotFlag + 1]);
            }

            // 调试用：--demo-crash，拿一条假崩溃跑一遍崩溃窗口
            if (argv.Contains("--demo-crash"))
            {
                ShowCrashWindow(BuildDemoCrash(), modal: false);
            }

            // 调试用：--scroll-bottom，设置页直接滚到底（看水印）
            if (argv.Contains("--scroll-bottom"))
            {
                _ = Task.Delay(600).ContinueWith(_ => Dispatcher.BeginInvoke(() => ScrollSettingsToBottom()));
            }

            // 调试用：--demo-detail，自动打开内容页第一个项目的详情（看加载器分类）
            if (argv.Contains("--demo-detail"))
            {
                _ = OpenFirstContentDetailAsync();
            }
        };
    }

    // ---------------- 导航 ----------------

    /// <summary>调试：把当前窗口内容渲染成 PNG（不含 WebView2 的网页内容），然后退出。</summary>
    private async Task CaptureAndExitAsync(string path)
    {
        try
        {
            await Task.Delay(3800);

            // 有弹窗就截弹窗
            Window target = this;
            var extra = Application.Current.Windows.Cast<Window>()
                .LastOrDefault(w => w.IsVisible && !ReferenceEquals(w, this));
            if (extra != null)
            {
                target = extra;
            }

            var dpi = VisualTreeHelper.GetDpi(target);
            var width = (int)Math.Round(target.ActualWidth * dpi.DpiScaleX);
            var height = (int)Math.Round(target.ActualHeight * dpi.DpiScaleY);
            var bitmap = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY,
                PixelFormats.Pbgra32);
            bitmap.Render(target);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            await using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }
        catch
        {
            // 截图失败不影响正常使用
        }

        Application.Current.Shutdown();
    }

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag && int.TryParse(tag, out var index))
        {
            SetPage(index);
        }
    }

    private void AnimateIn(UIElement element)
    {
        try
        {
            element.Opacity = 0;
            var transform = new TranslateTransform(0, 14);
            element.RenderTransform = transform;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            element.BeginAnimation(OpacityProperty, fade);

            var slide = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            transform.BeginAnimation(TranslateTransform.YProperty, slide);
        }
        catch
        {
            element.Opacity = 1;
        }
    }

    private void SetPage(int index)
    {
        LaunchPage.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        InstallPage.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        ContentPage.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        OnlinePage.Visibility = index == 6 ? Visibility.Visible : Visibility.Collapsed;
        VersionsPage.Visibility = index == 7 ? Visibility.Visible : Visibility.Collapsed;
        AccountPage.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
        UpdatesPage.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
        FunPage.Visibility = index == 8 ? Visibility.Visible : Visibility.Collapsed;

        var active = (Brush)FindResource("TextMain");
        var inactive = (Brush)FindResource("TextDim");
        NavLaunch.Foreground = index == 0 ? active : inactive;
        NavContent.Foreground = index is 1 or 2 ? active : inactive;
        NavOnline.Foreground = index == 6 ? active : inactive;
        NavVersions.Foreground = index == 7 ? active : inactive;
        NavAccount.Foreground = index == 3 ? active : inactive;
        NavFun.Foreground = index == 8 ? active : inactive;
        NavSettings.Foreground = index is 4 or 5 ? active : inactive;

        if (index == 1 && _mcVersions.Count == 0)
        {
            _ = LoadMcVersionsAsync();
        }

        if (index == 7)
        {
            InitVersionManager();
        }

        if (index == 6 && !_onlineLoaded)
        {
            _onlineLoaded = true;
            _tunnel.Output += line => Dispatcher.BeginInvoke(() => AppendTunnelLog(line));
            InitServerPanel();
            RefreshAddresses();
        }

        if (index == 3)
        {
            RefreshAccounts();
            RefreshCurrentSkinPreview();
        }

        if (index == 8)
        {
            _ = OnFunOpenedAsync();
        }

        try
        {
            // 3D 场景只在启动页渲染，其它页面隐藏省资源
            if (HeroViewport != null)
            {
                HeroViewport.Visibility = index == 0 ? Visibility.Visible : Visibility.Hidden;
            }
        }
        catch
        {
            // 忽略
        }

        if (index == 5 && !_updatesLoaded)
        {
            _ = LoadUpdatesAsync();
        }

        if (index == 2 && ContentList.ItemsSource == null)
        {
            _ = LoadContentPageAsync();
        }
    }

    // ---------------- 启动页 ----------------

    private void LoadVersions()
    {
        try
        {
            var cards = _service.ListVersions().Select(v => new VersionCard(v)).ToList();
            VersionList.ItemsSource = cards;
            if (cards.Count > 0)
            {
                var saved = cards.FirstOrDefault(c =>
                    string.Equals(c.Summary.Id, _settings.SelectedVersionId, StringComparison.OrdinalIgnoreCase));
                VersionList.SelectedItem = saved ?? cards[0];
                StatusText.Text = $"找到 {cards.Count} 个版本 · {_settings.GameDirectory}";
            }
            else
            {
                StatusText.Text = "没有找到版本，去「安装」页装一个";
                LaunchButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "读取版本失败: " + ex.Message;
        }
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        LoadVersions();
        StatusText.Text = "已刷新";
    }

    private void OnVersionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (VersionList.SelectedItem is VersionCard selected)
        {
            _settings.SelectedVersionId = selected.Summary.Id;
            ConfigStore.Save(_settings);
        }

        UpdateVersionPicker();
        if (VersionList.SelectedItem is not VersionCard card)
        {
            return;
        }

        var summary = card.Summary;
        BigVersion.Text = summary.ClientVersion;
        DetName.Text = summary.Id;
        DetMeta.Text = $"MC {summary.ClientVersion} · {summary.Loader}" +
                       (string.IsNullOrEmpty(summary.LoaderVersion) ? string.Empty : " " + summary.LoaderVersion);
        DetMods.Text = summary.ModCount + " 个";
        DetMemory.Text = $"{_settings.MinMemoryMb} - {_settings.MaxMemoryMb} MB";

        var java = _service.SelectJava(summary.JavaMajor, out var all);
        DetJava.Text = java == null
            ? $"缺少 Java {summary.JavaMajor}"
            : $"Java {java.Major} · {java.VersionText}";

        var resolved = _service.Resolve(summary.Id);
        DetDir.Text = resolved == null ? "—" : _service.GameDirectoryFor(resolved);

        LaunchButton.IsEnabled = !_busy;
        StatusText.Text = java == null
            ? $"警告：没有找到 Java {summary.JavaMajor}"
            : $"{summary.Id} · 已识别 {all.Count} 个 Java";

        UpdateHomeJavaUi();
    }

    private async void OnLaunch(object sender, RoutedEventArgs e)
    {
        if (_busy || VersionList.SelectedItem is not VersionCard card)
        {
            return;
        }

        _busy = true;
        LaunchButton.IsEnabled = false;
        SetStatus("正在准备启动…");

        try
        {
            // 外置登录：先把 token 续期、注入器备好，失败就别启动（否则进游戏没皮肤、联机也会被踢）
            if (_service.ActiveAccount is { Type: "yggdrasil" })
            {
                SetStatus("准备外置登录…");
                var prepared = await _service.PrepareYggdrasilAsync(
                    new Progress<string>(t => SetStatus(t)));
                if (prepared != null)
                {
                    SetStatus(prepared);
                    ShowToast("外置登录有问题", prepared, "⚠", 6000);
                    return;
                }
            }

            var versionInfo = _service.Resolve(card.Summary.Id);
            if (versionInfo == null)
            {
                SetStatus("找不到这个版本：" + card.Summary.Id);
                return;
            }

            // 先把缺的库补齐（Forge/NeoForge 最容易缺 bootstraplauncher 这类核心库）
            SetStatus("检查版本文件…");
            var missingLibs = await _service.EnsureLibrariesAsync(versionInfo,
                new Progress<string>(t => SetStatus(t)));
            if (missingLibs.Count > 0)
            {
                SetStatus($"缺 {missingLibs.Count} 个库文件，启动多半会失败");
                ShowToast("库文件不全", "建议在「内容 → 安装版本」里重装这个版本", "⚠", 5200);
            }

            // Java 先挑一遍：一台都没有、或者没有正好的版本，就先问要不要自动装
            var needed = versionInfo.JavaMajor;
            var hasExact = _service.ScanJava().Any(j => j.Major == needed);
            if (!hasExact)
            {
                var installed = await OfferJavaInstallAsync(needed);
                if (installed == null)
                {
                    var (javaFallback, warningFallback) = ResolveJavaForLaunch(versionInfo);
                    if (javaFallback == null)
                    {
                        SetStatus("一个 Java 都没找到，去「设置 → 启动游戏用的 Java」点重新扫描");
                        ShowToast("缺少 Java", "去设置里扫描 Java，或者手动添加目录", "⚠", 6000);
                        return;
                    }

                    if (warningFallback != null && !Confirm("Java 版本不对", warningFallback, "仍然启动", "先不启动"))
                    {
                        SetStatus("已取消启动，去设置里换一个 Java 再试");
                        return;
                    }
                }
            }

            var (javaForLaunch, javaWarning) = ResolveJavaForLaunch(versionInfo);
            if (javaForLaunch == null)
            {
                SetStatus("一个 Java 都没找到，去「设置 → 启动游戏用的 Java」点重新扫描");
                ShowToast("缺少 Java", "去设置里扫描 Java，或者手动添加目录", "⚠", 6000);
                return;
            }

            if (javaWarning != null && !Confirm("Java 版本不对", javaWarning, "仍然启动", "先不启动"))
            {
                SetStatus("已取消启动，去设置里换一个 Java 再试");
                return;
            }

            var plan = _service.BuildPlan(card.Summary.Id, out var resolved, out var problems,
                javaPath: javaForLaunch.JavaPath);
            if (plan == null || resolved == null)
            {
                SetStatus("启动失败：" + string.Join("；", problems.Take(3)));
                return;
            }

            SetStatus($"{resolved.Id} · 运行中（{plan.JavaPath}）");

            var launcher = new GameLauncher();
            _gameLauncher = launcher;
            launcher.Started += pid => Dispatcher.BeginInvoke(() =>
            {
                if (ForceKillButton != null)
                {
                    ForceKillButton.Visibility = Visibility.Visible;
                }

                SetStatus($"游戏已启动（pid {pid}），点「强制关闭游戏」可以强杀");
            });
            launcher.OutputReceived += line =>
                Dispatcher.BeginInvoke(() => SetStatus(Trim(line, 140)));

            var result = await Task.Run(() => launcher.LaunchAsync(plan));

            var killedByUser = _killedByUser;
            _killedByUser = false;

            if (!string.IsNullOrEmpty(result.CrashSummary))
            {
                SetStatus("崩溃：" + result.CrashSummary);
                if (result.Insight != null && !killedByUser)
                {
                    ShowCrashWindow(result.Insight);
                }
            }
            else
            {
                SetStatus(result.ExitCode == 0
                    ? $"游戏已退出（{result.Duration.TotalSeconds:0}s）"
                    : $"异常退出，退出码 {result.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            SetStatus("启动异常: " + ex.Message);
        }
        finally
        {
            _busy = false;
            LaunchButton.IsEnabled = true;
        }
    }

    private void OnOpenBlockedGuide(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                "https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            SetStatus("打不开浏览器：" + ex.Message);
        }
    }

    private void OnOpenBiliSpace(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://space.bilibili.com/3546629445519895")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            SetStatus("打不开浏览器：" + ex.Message);
        }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Directory.Exists(_settings.GameDirectory))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_settings.GameDirectory}\"")
                {
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception ex)
        {
            SetStatus("打开目录失败: " + ex.Message);
        }
    }

    // ---------------- 安装页 ----------------

    private async Task LoadMcVersionsAsync()
    {
        try
        {
            InstallText.Text = "正在获取 Minecraft 版本清单…";
            var installer = _service.CreateInstaller();
            var all = await installer.GetManifestAsync();
            _mcVersions = all.Where(v => v.Type == "release").Take(80).ToList();
            McVersionList.ItemsSource = _mcVersions;
            if (_mcVersions.Count > 0)
            {
                McVersionList.SelectedIndex = 0;
            }

            InstallText.Text = $"共 {all.Count} 个版本，已列出最近 {_mcVersions.Count} 个正式版";
        }
        catch (Exception ex)
        {
            InstallText.Text = "获取清单失败: " + ex.Message;
        }
    }

    private async void OnMcVersionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (McVersionList.SelectedItem is not RemoteVersion v)
        {
            return;
        }

        InstallMcText.Text = "Minecraft " + v.Id;
        InstallButton.IsEnabled = !_busy;
        InstallText.Text = "可以选加载器了";

        // 切版本必须重拉加载器版本，否则右边还停在上一个 MC 的列表
        LoaderVersionCombo.ItemsSource = null;
        LoaderVersionCombo.IsEnabled = false;

        if (SelectedLoader() == "vanilla")
        {
            LoaderHint.Text = "原版：直接装 Minecraft 本身";
            return;
        }

        await ReloadLoaderVersionsAsync(v.Id, SelectedLoader());
    }

    private async void OnLoaderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LoaderVersionCombo == null || LoaderCombo == null)
        {
            return;
        }

        var loader = SelectedLoader();
        LoaderVersionCombo.ItemsSource = null;
        LoaderVersionCombo.IsEnabled = false;
        LoaderHint.Text = string.Empty;

        if (loader == "vanilla" || McVersionList.SelectedItem is not RemoteVersion mc)
        {
            LoaderHint.Text = loader == "vanilla" ? "原版：直接装 Minecraft 本身" : "先在左边选版本";
            return;
        }

        await ReloadLoaderVersionsAsync(mc.Id, loader);
    }

    private async Task ReloadLoaderVersionsAsync(string mcId, string loader)
    {
        try
        {
            LoaderHint.Text = $"正在拉取 {loader} 版本列表…";
            var list = await _service.CreateLoaderInstaller().ListAsync(loader, mcId);
            LoaderVersionCombo.ItemsSource = list;
            LoaderVersionCombo.IsEnabled = list.Count > 0;
            if (list.Count > 0)
            {
                LoaderVersionCombo.SelectedIndex = 0;
            }

            LoaderHint.Text = list.Count == 0
                ? $"{loader} 没有支持 {mcId} 的版本"
                : $"共 {list.Count} 个，最新 {list[0].Version}";
        }
        catch (Exception ex)
        {
            LoaderHint.Text = "拉取失败: " + ex.Message;
        }
    }

    // ---------------- 内容页的两个分段 ----------------

    private void OnBackToSettings(object sender, RoutedEventArgs e) => SetPage(4);

    private async Task SplashFadeOutAsync()
    {
        try
        {
            await Task.Delay(650);
            if (SplashOverlay == null)
            {
                return;
            }

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            };
            fade.Completed += (_, __) => SplashOverlay.Visibility = Visibility.Collapsed;
            SplashOverlay.BeginAnimation(OpacityProperty, fade);
        }
        catch
        {
            if (SplashOverlay != null)
            {
                SplashOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void OnTabInstall(object sender, RoutedEventArgs e) => SetPage(1);

    private void OnTabContent(object sender, RoutedEventArgs e) => SetPage(2);

    private void OnQueryTextChanged(object sender, TextChangedEventArgs e)
    {
        if (QueryHint != null && QueryBox != null)
        {
            QueryHint.Visibility = string.IsNullOrEmpty(QueryBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void OnVersionSearch(object sender, TextChangedEventArgs e)
    {
        if (VersionSearchHint != null)
        {
            VersionSearchHint.Visibility = string.IsNullOrEmpty(VersionSearchBox?.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (McVersionList == null || _mcVersions == null)
        {
            return;
        }

        var keyword = VersionSearchBox?.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(keyword)
            ? _mcVersions
            : _mcVersions.Where(v => v.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        McVersionList.ItemsSource = filtered;
        if (filtered.Count > 0)
        {
            McVersionList.SelectedIndex = 0;
        }
    }

    private string SelectedLoader() => LoaderCombo.SelectedIndex switch
    {
        1 => "fabric",
        2 => "neoforge",
        3 => "forge",
        _ => "vanilla",
    };

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if (McVersionList.SelectedItem is not RemoteVersion mc)
        {
            return;
        }

        var loader = SelectedLoader();
        LoaderOption? option = null;
        if (loader != "vanilla")
        {
            option = LoaderVersionCombo.SelectedItem as LoaderOption;
            if (option == null)
            {
                InstallText.Text = "先选一个加载器版本";
                return;
            }
        }

        // 不再锁 _busy：一个版本还在装的时候，可以继续排别的版本
        InstallBar.Value = 0;
        CancelInstallButton.Visibility = Visibility.Visible;

        var manager = _service.CreateDownloadManager();
        var loaderLabel = loader == "vanilla" ? "原版" : loader;
        var job = manager.EnqueueJob("安装 Minecraft " + mc.Id + "（" + loaderLabel + "）", "版本");
        var cts = new CancellationTokenSource();
        _installCts[job.Id] = cts;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var progress = new Progress<InstallProgress>(p =>
            {
                // 在「下载管理」里点了取消，也要让安装停下来
                if (job.State == DownloadState.Canceled && !cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }

                InstallBar.Value = p.Percent;
                InstallText.Text = p.Total > 0
                    ? $"[{p.Stage}] {p.Message} · {p.Percent:0}% · {stopwatch.Elapsed.TotalSeconds:0}s"
                    : $"[{p.Stage}] {p.Message} · {stopwatch.Elapsed.TotalSeconds:0}s";

                manager.UpdateJob(job, p.Percent, $"[{p.Stage}] {p.Message}");
            });

            var installer = _service.CreateInstaller();
            var baseJson = Path.Combine(_settings.GameDirectory, "versions", mc.Id, mc.Id + ".json");
            if (!File.Exists(baseJson))
            {
                InstallText.Text = "先补齐原版 " + mc.Id + " …";
                await installer.InstallVanillaAsync(mc.Id, progress, downloadAssets: true, cts.Token);
            }

            if (option != null)
            {
                InstallText.Text = $"安装 {option.Loader} {option.Version} …";
                var id = await _service.CreateLoaderInstaller().InstallAsync(option, progress, cts.Token);
                InstallText.Text = "安装完成: " + id;
            }
            else
            {
                InstallText.Text = "原版安装完成: " + mc.Id;
            }

            LoadVersions();
            InstallBar.Value = 100;
            manager.CompleteJob(job, true);
            ShowToast("安装完成", "Minecraft " + mc.Id + "（" + loaderLabel + "）", "✓", 4000);
        }
        catch (OperationCanceledException)
        {
            InstallText.Text = "已取消安装: " + mc.Id;
            manager.CompleteJob(job, false, "已取消");
        }
        catch (Exception ex)
        {
            InstallText.Text = "安装失败: " + ex.Message;
            manager.CompleteJob(job, false, ex.Message);
        }
        finally
        {
            _installCts.Remove(job.Id);
            cts.Dispose();

            if (_installCts.Count == 0)
            {
                CancelInstallButton.Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>取消正在跑的安装（可以一次取消全部）。</summary>
    private void OnCancelInstall(object sender, RoutedEventArgs e)
    {
        if (_installCts.Count == 0)
        {
            return;
        }

        foreach (var cts in _installCts.Values.ToList())
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
                // 忽略
            }
        }

        InstallText.Text = "正在取消…";
    }

    // ---------------- 内容页 ----------------

    private const int ContentPageSize = 20;
    private int _contentOffset;
    private int _contentTotal;

    private ContentKind SelectedKind() => KindCombo.SelectedIndex switch
    {
        1 => ContentKind.Modpack,
        2 => ContentKind.Shader,
        3 => ContentKind.ResourcePack,
        _ => ContentKind.Mod,
    };

    private IContentSource SelectedSource() =>
        SourceCombo.SelectedIndex == 1 ? _service.CreateCurseForge() : _service.CreateModrinth();

    private void InitContentFilters()
    {
        var versions = _service.ListVersions()
            .Select(v => v.ClientVersion)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .ToList();

        var items = new List<string> { "跟随当前版本", "全部版本" };
        items.AddRange(versions);
        VersionCombo.ItemsSource = items;
        VersionCombo.SelectedIndex = 0;
        ContentLoaderCombo.SelectedIndex = 0;
        _ = ReloadCategoriesAsync();
    }

    private string? EffectiveVersion()
    {
        if (VersionCombo.SelectedIndex <= 0)
        {
            return (VersionList.SelectedItem as VersionCard)?.Summary.ClientVersion;
        }

        return VersionCombo.SelectedIndex == 1 ? null : VersionCombo.SelectedItem as string;
    }

    private string? EffectiveLoader()
    {
        if (ContentLoaderCombo.SelectedIndex <= 0)
        {
            return (VersionList.SelectedItem as VersionCard)?.Summary.Loader;
        }

        return (ContentLoaderCombo.SelectedItem as ComboBoxItem)?.Content as string;
    }

    private string? EffectiveCategory() =>
        CategoryCombo.SelectedItem is ContentTag tag && !string.IsNullOrEmpty(tag.Id) ? tag.Id : null;

    private async void OnKindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await ReloadCategoriesAsync();
    }

    private async void OnSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await ReloadCategoriesAsync();
    }

    private async Task ReloadCategoriesAsync()
    {
        try
        {
            var source = SelectedSource();
            var tags = await source.GetCategoriesAsync(SelectedKind());
            var list = new List<ContentTag> { new() { Id = string.Empty, Name = "全部分类" } };
            list.AddRange(tags);
            CategoryCombo.ItemsSource = list;
            CategoryCombo.SelectedIndex = 0;
        }
        catch
        {
            CategoryCombo.ItemsSource = new List<ContentTag> { new() { Id = string.Empty, Name = "全部分类" } };
            CategoryCombo.SelectedIndex = 0;
        }
    }

    private async void OnSearch(object sender, RoutedEventArgs e)
    {
        _contentOffset = 0;
        await LoadContentPageAsync();
    }

    private async Task LoadContentPageAsync()
    {
        var source = SelectedSource();
        if (!source.IsAvailable)
        {
            ContentStatus.Text = "CurseForge 需要 API key：设置 → 下载与镜像 → CurseForge API key（免费申请）";
            ContentList.ItemsSource = null;
            _contentTotal = 0;
            UpdatePager();
            return;
        }

        try
        {
            ContentStatus.Text = "搜索中…";
            var query = new ContentQuery
            {
                Kind = SelectedKind(),
                Query = QueryBox.Text?.Trim() ?? string.Empty,
                McVersion = EffectiveVersion(),
                Loader = EffectiveLoader(),
                Category = EffectiveCategory(),
                Offset = _contentOffset,
                PageSize = ContentPageSize,
            };

            var result = await source.SearchAsync(query);
            var relaxedNote = false;

            // 版本筛出来是 0 个，多半是这个版本号对方站点没有（比如版本号解析错、或太新的版本）。
            // 自动放宽到「不限版本」再搜一次，别让玩家对着空白页发呆。
            if (result.Total == 0 && !string.IsNullOrWhiteSpace(query.McVersion))
            {
                var relaxed = new ContentQuery
                {
                    Kind = query.Kind,
                    Query = query.Query,
                    McVersion = null,
                    Loader = query.Loader,
                    Category = query.Category,
                    Offset = query.Offset,
                    PageSize = query.PageSize,
                };

                var again = await source.SearchAsync(relaxed);
                if (again.Total > 0)
                {
                    result = again;
                    relaxedNote = true;
                }
            }

            _contentTotal = result.Total;

            var cards = result.Items.Select(i => new ContentCard(i)).ToList();
            ContentList.ItemsSource = cards;
            UpdatePager();

            ContentStatus.Text = cards.Count == 0
                ? "没有结果，换个关键词或放宽筛选"
                : relaxedNote
                    ? $"「{query.McVersion}」没有匹配的，已放宽到全部版本 · 共 {_contentTotal} 个 · {source.Name}"
                    : $"共 {_contentTotal} 个 · 第 {_contentOffset / ContentPageSize + 1} 页 · {source.Name}";
        }
        catch (Exception ex)
        {
            ContentStatus.Text = "搜索失败: " + ex.Message;
        }
    }

    private void UpdatePager()
    {
        var pages = Math.Max(1, (int)Math.Ceiling(_contentTotal / (double)ContentPageSize));
        var page = _contentOffset / ContentPageSize + 1;
        PageText.Text = _contentTotal == 0 ? string.Empty : $"第 {page} / {pages} 页";
        PrevButton.IsEnabled = _contentOffset > 0;
        NextButton.IsEnabled = _contentOffset + ContentPageSize < _contentTotal;
    }

    private async void OnPrevPage(object sender, RoutedEventArgs e)
    {
        if (_contentOffset >= ContentPageSize)
        {
            _contentOffset -= ContentPageSize;
            await LoadContentPageAsync();
        }
    }

    private async void OnNextPage(object sender, RoutedEventArgs e)
    {
        if (_contentOffset + ContentPageSize < _contentTotal)
        {
            _contentOffset += ContentPageSize;
            await LoadContentPageAsync();
        }
    }

    private async void OnContentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ContentList.SelectedItem is not ContentCard card)
        {
            return;
        }

        _selectedContent = card.Item;
        _ = ShowContentDetailAsync(card.Item);
        ContentInstallButton.IsEnabled = false;
        FileCombo.ItemsSource = null;

        try
        {
            ContentStatus.Text = "读取版本列表…";
            var mc = EffectiveVersion();
            var loader = EffectiveLoader();
            var source = card.Item.Source == "CurseForge" ? (IContentSource)_service.CreateCurseForge() : _service.CreateModrinth();
            var files = await source.GetFilesAsync(card.Item, mc, loader);

            var list = files.Select(f => new FileEntry(f)).ToList();
            FileCombo.ItemsSource = list;
            if (list.Count > 0)
            {
                FileCombo.SelectedIndex = 0;
            }

            ContentInstallButton.IsEnabled = list.Count > 0;
            ContentStatus.Text = list.Count == 0
                ? "这个筛选下没有可用文件（换个版本或加载器试试）"
                : $"{list.Count} 个文件 · 选中后点安装";
        }
        catch (Exception ex)
        {
            ContentStatus.Text = "读取文件失败: " + ex.Message;
        }
    }

    private void OnForceKillGame(object sender, RoutedEventArgs e)
    {
        _killedByUser = true;
        try
        {
            if (_gameLauncher?.Kill() == true)
            {
                SetStatus("已强制结束游戏进程");
            }
            else
            {
                SetStatus("游戏没有在运行");
            }
        }
        catch (Exception ex)
        {
            SetStatus("强制关闭失败: " + ex.Message);
        }

        ForceKillButton.Visibility = Visibility.Collapsed;
    }

    // ---------------- 版本管理 ----------------

    private sealed class VmVersionItem
    {
        public string Id { get; init; } = string.Empty;
        public string RawId { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
    }

    private VersionSummary? _vmCurrent;
    private bool _vmLoaded;

    private void InitVersionManager()
    {
        if (VmVersionList == null)
        {
            return;
        }

        RefreshVmDirs();

        if (_vmLoaded)
        {
            ReloadVmVersions();
            return;
        }

        _vmLoaded = true;
        var items = _service.ListVersions()
            .Where(v => v.IsUsable)
            .OrderByDescending(v => v.ClientVersion, StringComparer.OrdinalIgnoreCase)
            .Select(v => new VmVersionItem
            {
                Id = v.Id,
                RawId = v.Id,
                Subtitle = $"{v.Loader} · {v.ModCount} mods",
            })
            .ToList();

        VmVersionList.ItemsSource = items;
        if (items.Count > 0)
        {
            VmVersionList.SelectedIndex = 0;
        }
    }

    private void RefreshVmDirs()
    {
        if (VmDirCombo == null)
        {
            return;
        }

        var dirs = new List<string>();
        void Add(string? dir)
        {
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && !dirs.Contains(dir))
            {
                dirs.Add(dir);
            }
        }

        Add(_settings.GameDirectory);
        foreach (var dir in _settings.KnownGameDirs)
        {
            Add(dir);
        }

        var selected = _settings.GameDirectory;
        VmDirCombo.ItemsSource = dirs;
        VmDirCombo.SelectedItem = dirs.FirstOrDefault(d => string.Equals(d, selected, StringComparison.OrdinalIgnoreCase))
                                  ?? dirs.FirstOrDefault();
    }

    private void OnVmAddDir(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择一个 .minecraft 文件夹（里面要有 versions）",
            UseDescriptionForTitle = true,
            SelectedPath = _settings.GameDirectory,
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var dir = dialog.SelectedPath;
        if (!Directory.Exists(Path.Combine(dir, "versions")))
        {
            VmStatus.Text = "这个文件夹里没有 versions 子目录，可能不是 .minecraft";
        }

        if (!_settings.KnownGameDirs.Contains(dir))
        {
            _settings.KnownGameDirs.Add(dir);
            ConfigStore.Save(_settings);
        }

        RefreshVmDirs();
        VmDirCombo.SelectedItem = dir;
    }

    private void OnVmDirChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VmDirCombo?.SelectedItem is not string dir || string.IsNullOrEmpty(dir))
        {
            return;
        }

        if (string.Equals(dir, _settings.GameDirectory, StringComparison.OrdinalIgnoreCase))
        {
            ReloadVmVersions();
            return;
        }

        try
        {
            _service.SetGameDirectory(dir);
            _mcVersions.Clear();
            ReloadVmVersions();
            RefreshVersionCards();
            VmStatus.Text = "已切换到 " + dir;
        }
        catch (Exception ex)
        {
            VmStatus.Text = "切换目录失败: " + ex.Message;
        }
    }

    private void ReloadVmVersions()
    {
        if (VmVersionList == null)
        {
            return;
        }

        var items = _service.ListVersions()
            .Where(v => v.IsUsable)
            .OrderByDescending(v => v.ClientVersion, StringComparer.OrdinalIgnoreCase)
            .Select(v => new VmVersionItem
            {
                Id = v.Id,
                RawId = v.Id,
                Subtitle = $"{v.Loader} · {v.ModCount} mods",
            })
            .ToList();

        VmVersionList.ItemsSource = items;
        if (items.Count > 0)
        {
            VmVersionList.SelectedIndex = 0;
        }
        else
        {
            VmTitle.Text = "这个目录里没有可用版本";
            VmModList.Children.Clear();
        }
    }

    private void RefreshVersionCards()
    {
        try
        {
            var versions = _service.ListVersions().Where(v => v.IsUsable).ToList();
            VersionList.ItemsSource = versions.Select(v => new VersionCard(v)).ToList();
            if (VersionList.Items.Count > 0)
            {
                VersionList.SelectedIndex = 0;
            }
        }
        catch
        {
            // 忽略
        }
    }

    private void OnOpenVersionManager(object sender, RoutedEventArgs e) => SetPage(7);

    private void OnImportPackHome(object sender, RoutedEventArgs e)
    {
        SetPage(7);
        OnVmImportPack(sender, e);
    }

    private void OnVmVersionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (VmVersionList.SelectedItem is not VmVersionItem item)
        {
            return;
        }

        _vmCurrent = _service.ListVersions().FirstOrDefault(v => v.Id == item.RawId);
        if (_vmCurrent == null)
        {
            VmTitle.Text = "版本不存在";
            return;
        }

        var resolved = _service.Resolve(_vmCurrent.Id);
        var dir = resolved == null ? _settings.GameDirectory : _service.GameDirectoryFor(resolved);

        VmTitle.Text = _vmCurrent.Id;
        VmPath.Text = dir;
        VmMemoryBox.Text = _settings.MaxMemoryMb.ToString();

        RefreshIsolationUi(resolved);
        RefreshModList(dir);
        UpdateModCountLine(dir);

        if (VmNoteBox != null)
        {
            VmNoteBox.Text = _service.VersionNote(_vmCurrent.Id) ?? string.Empty;
        }

        if (VmRenameButton != null)
        {
            VmRenameButton.IsEnabled = true;
        }
    }

    private void OnVmSaveNote(object sender, RoutedEventArgs e)
    {
        if (_vmCurrent == null)
        {
            return;
        }

        _service.SetVersionNote(_vmCurrent.Id, VmNoteBox.Text);
        VmStatus.Text = string.IsNullOrWhiteSpace(VmNoteBox.Text) ? "备注已清空" : "备注已保存";
        ReloadVmVersions();
    }

    private async void OnVmRename(object sender, RoutedEventArgs e)
    {
        if (_vmCurrent == null)
        {
            return;
        }

        var resolved = _service.Resolve(_vmCurrent.Id);
        if (resolved == null)
        {
            return;
        }

        var newName = PromptForText("重命名版本", "新的版本名（文件夹和版本号会一起改）", _vmCurrent.Id);
        if (string.IsNullOrWhiteSpace(newName) || newName.Trim() == _vmCurrent.Id)
        {
            return;
        }

        var outcome = _service.RenameVersion(resolved, newName.Trim());
        if (!outcome.Ok || outcome.NewId == null)
        {
            VmStatus.Text = outcome.Message;
            ShowToast("改名失败", outcome.Message, "⚠", 5000);
            return;
        }

        var newId = outcome.NewId;
        ReloadVmVersions();

        var target = VmVersionList.Items.OfType<VmVersionItem>().FirstOrDefault(v => v.RawId == newId);
        if (target != null)
        {
            VmVersionList.SelectedItem = target;
        }

        VmStatus.Text = outcome.Message;
        ShowToast("已改名", outcome.Message, "✓", 4600);
        await Task.CompletedTask;
    }

    /// <summary>一个简单的输入框（重命名、备注之类的用）。</summary>
    private string? PromptForText(string title, string label, string initial)
    {
        var window = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = (Brush)FindResource("Bg"),
        };

        var panel = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("InkDim"),
        });

        var box = new TextBox { Text = initial, Margin = new Thickness(0, 8, 0, 16) };
        panel.Children.Add(box);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var cancel = new Button
        {
            Content = "取消",
            Style = (Style)FindResource("GlassButton"),
            Width = 84,
            Height = 34,
            IsCancel = true,
        };
        var ok = new Button
        {
            Content = "确定",
            Style = (Style)FindResource("HeroButton"),
            Width = 84,
            Height = 34,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true,
        };
        ok.Click += (_, __) => window.DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);

        window.Content = panel;
        box.Focus();
        box.SelectAll();

        return window.ShowDialog() == true ? box.Text?.Trim() : null;
    }

    /// <summary>mod 数量按“这个版本实际读哪个 mods 文件夹”来数，别用扫描时的旧值。</summary>
    private void UpdateModCountLine(string gameDir)
    {
        var count = 0;
        try
        {
            var modsDir = Path.Combine(gameDir, "mods");
            if (Directory.Exists(modsDir))
            {
                count = Directory.GetFiles(modsDir).Count(f =>
                    f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase));
            }
        }
        catch
        {
            // 忽略
        }

        if (VmStatus != null)
        {
            VmStatus.Text = "mod 数量：" + count;
        }
    }

    /// <summary>版本页那个「版本隔离」按钮的状态。</summary>
    private void RefreshIsolationUi(ResolvedVersion? resolved)
    {
        if (IsolationButton == null || IsolationHint == null)
        {
            return;
        }

        if (resolved == null)
        {
            IsolationButton.IsEnabled = false;
            IsolationButton.Content = "版本隔离：—";
            IsolationHint.Text = string.Empty;
            return;
        }

        IsolationButton.IsEnabled = true;
        var isolated = _service.IsIsolated(resolved);
        var forced = _service.IsolationOverride(resolved.Id);

        IsolationButton.Content = isolated ? "版本隔离：开" : "版本隔离：关";
        IsolationButton.Style = (Style)FindResource(isolated ? "HeroButton" : "GlassButton");
        IsolationButton.Padding = new Thickness(16, 6, 16, 6);

        IsolationHint.Text = forced == null
            ? (_settings.VersionIsolation == "on"
                ? "全局设置是「总是隔离」，这个版本就在自己的目录里；点按钮可以单独改成共用。"
                : "现在跟随「设置 → 版本隔离」的全局设置，点按钮让这个版本单独决定。")
            : isolated
                ? "已隔离：这个版本的 mods / config / saves 在 " + resolved.Directory + " 里，不跟别的版本共用。"
                : "已设成共用：mod 从游戏目录的 mods 读，和别的版本一起用。";
    }

    private void OnToggleIsolation(object sender, RoutedEventArgs e)
    {
        if (_vmCurrent == null)
        {
            return;
        }

        var resolved = _service.Resolve(_vmCurrent.Id);
        if (resolved == null)
        {
            return;
        }

        var isolated = !_service.IsIsolated(resolved);
        _service.SetIsolation(resolved, isolated);

        var dir = _service.GameDirectoryFor(resolved);
        VmPath.Text = dir;
        RefreshIsolationUi(resolved);
        RefreshModList(dir);
        UpdateModCountLine(dir);

        ShowToast(isolated ? "已开启版本隔离" : "已改成共用目录",
            isolated
                ? "现在的 mod 放在 " + Path.Combine(resolved.Directory, "mods")
                : "mod 从 " + Path.Combine(_settings.GameDirectory, "mods") + " 读",
            "✓", 3600);
    }

    private string? CurrentVmGameDir()
    {
        if (_vmCurrent == null)
        {
            return null;
        }

        var resolved = _service.Resolve(_vmCurrent.Id);
        return resolved == null ? _settings.GameDirectory : _service.GameDirectoryFor(resolved);
    }

    private void OnVmModSearch(object sender, TextChangedEventArgs e)
    {
        if (VmModSearchHint != null)
        {
            VmModSearchHint.Visibility = string.IsNullOrEmpty(VmModSearch?.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        var dir = CurrentVmGameDir();
        if (dir != null)
        {
            RefreshModList(dir);
        }
    }

    private void RefreshModList(string gameDir)
    {
        VmModList.Children.Clear();
        var modsDir = Path.Combine(gameDir, "mods");
        if (!Directory.Exists(modsDir))
        {
            VmModList.Children.Add(new TextBlock
            {
                Text = "还没有 mods 文件夹（启动一次游戏会自动创建）",
                FontSize = 11.5,
                Foreground = (Brush)FindResource("InkFaint"),
            });
            return;
        }

        var keyword = VmModSearch?.Text?.Trim() ?? string.Empty;
        var files = Directory.GetFiles(modsDir)
            .Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
            .Where(f => string.IsNullOrEmpty(keyword) ||
                        Path.GetFileName(f).Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        if (files.Count == 0)
        {
            VmModList.Children.Add(new TextBlock
            {
                Text = "这个版本还没有 mod，可以把 jar 拖进来",
                FontSize = 11.5,
                Foreground = (Brush)FindResource("InkFaint"),
            });
            return;
        }

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var enabled = name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);

            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            label.Children.Add(new TextBlock
            {
                Text = name.Replace(".disabled", string.Empty),
                FontSize = 12,
                Foreground = (Brush)FindResource(enabled ? "Ink" : "InkFaint"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            label.Children.Add(new TextBlock
            {
                Text = enabled ? "已启用" : "已禁用",
                FontSize = 10,
                Foreground = (Brush)FindResource("InkFaint"),
                Margin = new Thickness(0, 2, 0, 0),
            });
            row.Children.Add(label);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var toggle = new Button
            {
                Content = enabled ? "禁用" : "启用",
                Style = (Style)FindResource("GlassButton"),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
            };
            toggle.Click += (_, __) =>
            {
                try
                {
                    var target = enabled ? file + ".disabled" : file[..^".disabled".Length];
                    File.Move(file, target, overwrite: true);
                    RefreshModList(gameDir);
                    VmStatus.Text = (enabled ? "已禁用 " : "已启用 ") + name;
                }
                catch (Exception ex)
                {
                    VmStatus.Text = "操作失败: " + ex.Message;
                }
            };
            buttons.Children.Add(toggle);

            var remove = new Button
            {
                Content = "删除",
                Style = (Style)FindResource("GlassButton"),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
                Margin = new Thickness(6, 0, 0, 0),
            };
            remove.Click += (_, __) =>
            {
                try
                {
                    File.Delete(file);
                    RefreshModList(gameDir);
                    VmStatus.Text = "已删除 " + name;
                }
                catch (Exception ex)
                {
                    VmStatus.Text = "删除失败: " + ex.Message;
                }
            };
            buttons.Children.Add(remove);

            Grid.SetColumn(buttons, 1);
            row.Children.Add(buttons);

            VmModList.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
                Padding = new Thickness(12, 8, 10, 8),
                Child = row,
            });
        }
    }

    private void OnVmRefreshMods(object sender, RoutedEventArgs e)
    {
        var dir = CurrentVmGameDir();
        if (dir != null)
        {
            RefreshModList(dir);
        }
    }

    private void OnVmSaveMemory(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(VmMemoryBox.Text, out var memory) || memory < 512)
        {
            VmStatus.Text = "内存要填大于 512 的数字";
            return;
        }

        _settings.MaxMemoryMb = memory;
        ConfigStore.Save(_settings);
        VmStatus.Text = "内存已设为 " + memory + " MB";
    }

    private void OnVmSetAsTarget(object sender, RoutedEventArgs e)
    {
        if (_vmCurrent == null)
        {
            return;
        }

        if (VersionList.ItemsSource is IEnumerable<VersionCard> cards)
        {
            var target = cards.FirstOrDefault(c =>
                string.Equals(c.Summary.Id, _vmCurrent.Id, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                VersionList.SelectedItem = target;
                VmStatus.Text = "已设为安装目标：" + _vmCurrent.Id;
                return;
            }
        }

        _settings.SelectedVersionId = _vmCurrent.Id;
        ConfigStore.Save(_settings);
        VmStatus.Text = "已记住这个版本为安装目标（启动页列表里还没刷新）";
    }

    private void OnVmOpenVersionDir(object sender, RoutedEventArgs e)
    {
        var dir = CurrentVmGameDir();
        if (dir == null)
        {
            return;
        }

        OpenFolder(dir);
    }

    private void OnVmOpenFolder(object sender, RoutedEventArgs e)
    {
        var dir = CurrentVmGameDir();
        if (dir == null || sender is not Button { Tag: string folder })
        {
            return;
        }

        var target = Path.Combine(dir, folder);
        Directory.CreateDirectory(target);
        OpenFolder(target);
    }

    private void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            VmStatus.Text = "打开文件夹失败: " + ex.Message;
        }
    }

    private void OnVmAddMods(object sender, RoutedEventArgs e)
    {
        var dir = CurrentVmGameDir();
        if (dir == null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 mod（可多选）",
            Filter = "Mod jar|*.jar",
            Multiselect = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        CopyMods(dialog.FileNames, dir);
    }

    private void OnVmModsDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnVmModsDrop(object sender, DragEventArgs e)
    {
        var dir = CurrentVmGameDir();
        if (dir == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            CopyMods(files, dir);
        }
    }

    private void CopyMods(IEnumerable<string> files, string gameDir)
    {
        var modsDir = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(modsDir);

        var copied = 0;
        foreach (var file in files.Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var target = Path.Combine(modsDir, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
                copied++;
            }
            catch (Exception ex)
            {
                VmStatus.Text = "复制失败: " + ex.Message;
            }
        }

        RefreshModList(gameDir);
        VmStatus.Text = $"已添加 {copied} 个 mod";
    }

    private void OnVmExportPack(object sender, RoutedEventArgs e)
    {
        var dir = CurrentVmGameDir();
        if (dir == null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出整合包",
            Filter = "整合包 zip|*.zip",
            FileName = (_vmCurrent?.Id ?? "modpack") + ".zip",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (File.Exists(dialog.FileName))
            {
                File.Delete(dialog.FileName);
            }

            using var zip = System.IO.Compression.ZipFile.Open(dialog.FileName,
                System.IO.Compression.ZipArchiveMode.Create);

            void AddFolder(string folder)
            {
                var full = Path.Combine(dir, folder);
                if (!Directory.Exists(full))
                {
                    return;
                }

                foreach (var file in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
                {
                    var entryName = Path.GetRelativePath(dir, file).Replace('\\', '/');
                    zip.CreateEntryFromFile(file, entryName);
                }
            }

            AddFolder("mods");
            AddFolder("config");

            var options = Path.Combine(dir, "options.txt");
            if (File.Exists(options))
            {
                zip.CreateEntryFromFile(options, "options.txt");
            }

            VmStatus.Text = "已导出: " + dialog.FileName;
        }
        catch (Exception ex)
        {
            VmStatus.Text = "导出失败: " + ex.Message;
        }
    }

    private void OnVmImportPack(object sender, RoutedEventArgs e)
    {
        var dir = CurrentVmGameDir();
        if (dir == null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入整合包",
            Filter = "整合包 zip|*.zip",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(dialog.FileName, dir, overwriteFiles: true);
            RefreshModList(dir);
            VmStatus.Text = "已导入到 " + dir;
        }
        catch (Exception ex)
        {
            VmStatus.Text = "导入失败: " + ex.Message;
        }
    }

    // ---------------- 内容点击 / 提示 ----------------

    private DateTime _lastDetailClick = DateTime.MinValue;

    private void OnContentListClick(object sender, MouseButtonEventArgs e) => TryOpenSelectedContent();

    private void OnContentItemClick(object sender, MouseButtonEventArgs e) => TryOpenSelectedContent();

    private void TryOpenSelectedContent()
    {
        if (ContentList.SelectedItem is not ContentCard card)
        {
            return;
        }

        // 同一个条目可能同时触发 ListBox 和 ListBoxItem 两个事件，防抖一下
        if ((DateTime.Now - _lastDetailClick).TotalMilliseconds < 250)
        {
            return;
        }

        _lastDetailClick = DateTime.Now;
        _ = ShowContentDetailAsync(card.Item);
    }

    private async void ShowToast(string title, string detail, string icon = "⇩", int milliseconds = 3200)
    {
        try
        {
            if (ToastHost == null)
            {
                return;
            }

            ToastIcon.Text = icon;
            ToastTitle.Text = title;
            ToastDetail.Text = detail;

            ToastHost.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
            ToastHost.BeginAnimation(OpacityProperty, fadeIn);

            await Task.Delay(milliseconds);

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260));
            fadeOut.Completed += (_, __) => ToastHost.Visibility = Visibility.Collapsed;
            ToastHost.BeginAnimation(OpacityProperty, fadeOut);
        }
        catch
        {
            // 提示失败不影响主流程
        }
    }

    // ---------------- 下载管理 ----------------

    private void InitDownloadPanel()
    {
        if (DownloadList == null)
        {
            return;
        }

        var manager = _service.CreateDownloadManager();
        DownloadList.ItemsSource = manager.Tasks;
        manager.Tasks.CollectionChanged += (_, e) =>
        {
            UpdateDownloadHint();
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add &&
                e.NewItems is { Count: > 0 } && e.NewItems[0] is DownloadItem added)
            {
                var verb = added.KindLabel == "版本" ? "开始安装" : "开始下载";
                ShowToast(verb, added.FileName, "⇩", 2400);
            }
        };

        manager.TaskCompleted += item => Dispatcher.BeginInvoke(() =>
        {
            UpdateDownloadHint();
            ShowToast("下载完成", item.FileName, "✓", 3600);
        });
        manager.TaskFailed += item => Dispatcher.BeginInvoke(() =>
        {
            UpdateDownloadHint();
            ShowToast("下载失败", item.FileName + " · " + item.Error, "⚠", 5000);
        });

        UpdateDownloadHint();
    }

    private void UpdateDownloadHint()
    {
        if (DownloadEmptyHint == null)
        {
            return;
        }

        var manager = _service.CreateDownloadManager();
        var active = manager.Tasks.Count(t => t.State is DownloadState.Queued or DownloadState.Running);
        DownloadEmptyHint.Text = manager.Tasks.Count == 0
            ? "还没有下载任务"
            : active > 0 ? $"{active} 个任务进行中" : "全部结束";

        if (DownloadsButton != null)
        {
            DownloadsButton.Content = active > 0 ? $"下载管理 ({active})" : "下载管理";
        }
    }

    private void OnToggleDownloads(object sender, RoutedEventArgs e)
    {
        if (DownloadPanel == null)
        {
            return;
        }

        DownloadPanel.Visibility = DownloadPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        UpdateDownloadHint();
    }

    private void OnPauseDownload(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DownloadItem item })
        {
            _service.CreateDownloadManager().Pause(item);
            UpdateDownloadHint();
        }
    }

    private void OnResumeDownload(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DownloadItem item })
        {
            _service.CreateDownloadManager().Resume(item);
            UpdateDownloadHint();
        }
    }

    private void OnCancelDownload(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DownloadItem item })
        {
            // 安装类任务（KindLabel = 版本 / 整合包）走自己的取消开关
            if (_installCts.TryGetValue(item.Id, out var cts))
            {
                try
                {
                    cts.Cancel();
                }
                catch
                {
                    // 忽略
                }
            }

            _service.CreateDownloadManager().Cancel(item);
            UpdateDownloadHint();
        }
    }

    private void OnClearDownloads(object sender, RoutedEventArgs e)
    {
        _service.CreateDownloadManager().ClearFinished();
        UpdateDownloadHint();
    }

    private void OnOpenDownloadFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var target = VersionList.SelectedItem as VersionCard;
            var resolved = target == null ? null : _service.Resolve(target.Summary.Id);
            var dir = resolved == null ? _settings.GameDirectory : _service.GameDirectoryFor(resolved);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch
        {
            // 忽略
        }
    }

    // ---------------- 内容详情 ----------------

    private async void OnCloseContentDetail(object sender, RoutedEventArgs e)
    {
        if (_detailStack.Count > 0)
        {
            var previous = _detailStack.Pop();
            _detailCurrent = null;
            await ShowContentDetailAsync(previous, pushHistory: false);
            return;
        }

        _detailCurrent = null;
        ContentDetailPanel.Visibility = Visibility.Collapsed;
    }

    private async Task ShowContentDetailAsync(ContentItem item, bool pushHistory = true)
    {
        if (pushHistory && _detailCurrent != null && _detailCurrent.Id != item.Id)
        {
            _detailStack.Push(_detailCurrent);
        }

        _detailCurrent = item;
        ContentDetailPanel.Visibility = Visibility.Visible;
        DetailTitle.Text = item.Title;
        DetailSummary.Text = item.Summary;
        DetailStatus.Text = "读取版本列表…";
        DetailBody.Children.Clear();
        expanderCount = 0;

        DetailIcon.Source = null;
        if (!string.IsNullOrEmpty(item.IconUrl))
        {
            _ = IconCache.GetAsync(item.IconUrl!).ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion && t.Result != null)
                {
                    Dispatcher.BeginInvoke(() => DetailIcon.Source = t.Result);
                }
            });
        }

        try
        {
            List<ContentVersion> versions;
            if (item.Source == "CurseForge")
            {
                var cf = _service.CreateCurseForge();
                if (!cf.IsAvailable)
                {
                    DetailStatus.Text = "CurseForge 需要 API Key（在设置里填，免费且不会过期）";
                    return;
                }

                versions = await cf.GetVersionsAsync(item);
            }
            else
            {
                versions = await _service.CreateModrinth().GetVersionsAsync(item);
            }

            DetailStatus.Text = versions.Count == 0
                ? "这个项目没有可用版本"
                : $"共 {versions.Count} 个版本，按适用 Minecraft 版本分组";

            _detailVersions = versions;
            _detailLoader = string.Empty;
            RenderDetail(item);
            await AddDependenciesAsync(item, versions);
        }
        catch (Exception ex)
        {
            DetailStatus.Text = "读取失败: " + ex.Message;
        }
    }

    /// <summary>重新画详情页：加载器小分类 + 按 MC 版本分组。</summary>
    private void RenderDetail(ContentItem item)
    {
        DetailBody.Children.Clear();
        expanderCount = 0;
        DetailBody.Children.Add(BuildLoaderFilter(item));

        var filtered = string.IsNullOrEmpty(_detailLoader)
            ? _detailVersions
            : _detailVersions
                .Where(v => v.Loaders.Contains(_detailLoader, StringComparer.OrdinalIgnoreCase))
                .ToList();

        if (filtered.Count == 0)
        {
            DetailBody.Children.Add(new TextBlock
            {
                Text = "这个加载器下没有版本，点「全部」看看",
                FontSize = 11.5,
                Foreground = (Brush)FindResource("InkFaint"),
                Margin = new Thickness(2, 10, 0, 0),
            });
            return;
        }

        BuildVersionGroups(item, filtered);
    }

    /// <summary>mod 详情页顶上的「全部 / Fabric / NeoForge / Forge …」小按钮。</summary>
    private WrapPanel BuildLoaderFilter(ContentItem item)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };

        var loaders = new List<string> { string.Empty };
        foreach (var name in new[] { "fabric", "neoforge", "forge", "quilt", "optifine" })
        {
            if (_detailVersions.Any(v => v.Loaders.Contains(name, StringComparer.OrdinalIgnoreCase)))
            {
                loaders.Add(name);
            }
        }

        foreach (var extra in _detailVersions.SelectMany(v => v.Loaders).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!loaders.Contains(extra, StringComparer.OrdinalIgnoreCase))
            {
                loaders.Add(extra);
            }
        }

        foreach (var loader in loaders)
        {
            var count = string.IsNullOrEmpty(loader)
                ? _detailVersions.Count
                : _detailVersions.Count(v => v.Loaders.Contains(loader, StringComparer.OrdinalIgnoreCase));

            var active = string.Equals(_detailLoader, loader, StringComparison.OrdinalIgnoreCase);
            var label = string.IsNullOrEmpty(loader) ? "全部" : LoaderLabel(loader);

            var button = new Button
            {
                Content = label + " · " + count,
                Style = (Style)FindResource(active ? "HeroButton" : "GlassButton"),
                Padding = new Thickness(12, 5, 12, 5),
                FontSize = 10.5,
                Margin = new Thickness(0, 0, 6, 6),
                Tag = loader,
            };
            button.Click += (_, __) =>
            {
                if (_detailCurrent == null)
                {
                    return;
                }

                _detailLoader = loader;
                RenderDetail(item);
                _ = AddDependenciesAsync(item, _detailVersions);
            };

            panel.Children.Add(button);
        }

        return panel;
    }

    private static string LoaderLabel(string loader) => loader.ToLowerInvariant() switch
    {
        "fabric" => "Fabric",
        "forge" => "Forge",
        "neoforge" => "NeoForge",
        "quilt" => "Quilt",
        "optifine" => "OptiFine",
        "iris" => "Iris",
        "vanilla" => "原版",
        _ => loader,
    };

    private void BuildVersionGroups(ContentItem item, List<ContentVersion> versions)
    {
        var currentMc = EffectiveVersion();
        var currentLoader = EffectiveLoader();

        // 按 MC 版本分组；当前版本排最前
        var groups = versions
            .SelectMany(v => (v.GameVersions.Count == 0 ? new List<string> { "未知" } : v.GameVersions)
                .Select(mc => new { Mc = mc, Version = v }))
            .GroupBy(x => x.Mc)
            .OrderByDescending(g => g.Key == currentMc)
            .ThenByDescending(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groups)
        {
            var items = group.Select(x => x.Version)
                .GroupBy(v => v.Id)
                .Select(g => g.First())
                .OrderByDescending(v => v.Published)
                .ToList();

            var expander = new Expander
            {
                Header = $"Minecraft {group.Key} · {items.Count} 个版本",
                Foreground = (Brush)FindResource("Ink"),
                Margin = new Thickness(0, 2, 0, 6),
                IsExpanded = group.Key == currentMc || (currentMc == null && expanderCount == 0),
            };

            var list = new StackPanel { Margin = new Thickness(6, 6, 0, 0) };
            foreach (var version in items.Take(30))
            {
                list.Children.Add(BuildVersionRow(item, version, currentLoader));
            }

            expander.Content = list;
            DetailBody.Children.Add(expander);
            expanderCount++;
        }
    }

    /// <summary>把「前置模组」放在最上面：按当前 MC 版本挑一个最合适的版本，列它需要的依赖。</summary>
    private async Task AddDependenciesAsync(ContentItem item, List<ContentVersion> versions)
    {
        var currentMc = EffectiveVersion();
        var currentLoader = EffectiveLoader();

        var candidate = versions
            .Where(v => currentMc == null || v.GameVersions.Contains(currentMc))
            .Where(v => currentLoader == null || v.Loaders.Count == 0 ||
                        v.Loaders.Contains(currentLoader, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(v => v.Published)
            .FirstOrDefault()
            ?? versions.FirstOrDefault(v => string.IsNullOrEmpty(currentMc) || v.GameVersions.Contains(currentMc));

        if (candidate == null || candidate.Dependencies.Count == 0)
        {
            return;
        }

        var required = candidate.Dependencies
            .Where(d => d.IsRequired)
            .GroupBy(d => d.ProjectId)
            .Select(g => g.First())
            .ToList();

        if (required.Count == 0)
        {
            return;
        }

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xB4, 0x4A)),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 8, 12),
        };

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = $"前置模组（{required.Count} 个，不装会启动失败）",
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x7A)),
        });

        foreach (var dependency in required)
        {
            var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = "读取中…",
                FontSize = 12,
                Foreground = (Brush)FindResource("Ink"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            row.Children.Add(name);

            var install = new Button
            {
                Content = "查看 / 安装",
                Style = (Style)FindResource("GlassButton"),
                Padding = new Thickness(12, 5, 12, 5),
                FontSize = 11.5,
                Margin = new Thickness(8, 0, 0, 0),
            };
            Grid.SetColumn(install, 1);
            row.Children.Add(install);

            panel.Children.Add(row);

            var projectId = dependency.ProjectId;
            ContentItem? depItem = null;
            if (item.Source == "CurseForge")
            {
                name.Text = "CurseForge 项目 #" + projectId;
            }
            else
            {
                depItem = await _service.CreateModrinth().GetProjectAsync(projectId);
                name.Text = depItem?.Title ?? projectId;
            }

            install.Click += async (_, __) =>
            {
                install.IsEnabled = false;
                try
                {
                    // 跳到前置模组自己的详情页，让玩家自己挑版本
                    var target = depItem;
                    if (target == null && item.Source == "CurseForge")
                    {
                        target = await _service.CreateCurseForge().GetProjectAsync(projectId);
                    }

                    target ??= new ContentItem
                    {
                        Id = projectId,
                        Kind = ContentKind.Mod,
                        Source = item.Source,
                        Title = item.Source == "CurseForge" ? "CurseForge #" + projectId : projectId,
                    };

                    await ShowContentDetailAsync(target);
                }
                catch (Exception ex)
                {
                    DetailStatus.Text = "打开前置失败: " + ex.Message;
                }
                finally
                {
                    install.IsEnabled = true;
                }
            };
        }

        card.Child = panel;
        DetailBody.Children.Insert(0, card);
    }

    private int expanderCount;
    private readonly Stack<ContentItem> _detailStack = new();
    private List<ContentVersion> _detailVersions = new();
    private string _detailLoader = string.Empty;
    private GameLauncher? _gameLauncher;
    private bool _killedByUser;

    /// <summary>正在跑的安装任务（job id → 取消开关），支持同时装好几个版本。</summary>
    private readonly Dictionary<string, CancellationTokenSource> _installCts = new();

    /// <summary>崩溃窗口：看完原因、导出报告。</summary>
    private void ShowCrashWindow(CrashInsight insight, bool modal = true)
    {
        try
        {
            var window = new CrashWindow(insight) { Owner = this };
            if (modal)
            {
                window.ShowDialog();
            }
            else
            {
                window.Show();
            }
        }
        catch (Exception ex)
        {
            SetStatus("崩溃窗口打不开：" + ex.Message);
        }
    }
    private ContentItem? _detailCurrent;

    private Border BuildVersionRow(ContentItem item, ContentVersion version, string? currentLoader)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(version.Number) ? version.Name : version.Number,
            FontSize = 12.5,
            Foreground = (Brush)FindResource("Ink"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var bits = new List<string>();
        if (version.Loaders.Count > 0)
        {
            bits.Add(version.LoaderText);
        }

        if (version.Size > 0)
        {
            bits.Add(version.SizeText);
        }

        if (version.Published != default)
        {
            bits.Add(version.Published.ToLocalTime().ToString("yyyy-MM-dd"));
        }

        if (version.Channel != "release")
        {
            bits.Add(version.Channel);
        }

        text.Children.Add(new TextBlock
        {
            Text = string.Join(" · ", bits),
            FontSize = 10.5,
            Foreground = (Brush)FindResource("InkFaint"),
            Margin = new Thickness(0, 2, 0, 0),
        });
        grid.Children.Add(text);

        var install = new Button
        {
            Content = "安装",
            Style = (Style)FindResource("GlassButton"),
            Padding = new Thickness(14, 5, 14, 5),
            FontSize = 11.5,
        };
        install.Click += async (_, __) =>
        {
            install.IsEnabled = false;
            await InstallVersionAsync(item, version, install);
        };
        Grid.SetColumn(install, 1);
        grid.Children.Add(install);

        return new Border
        {
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF)),
            Padding = new Thickness(12, 8, 10, 8),
            Margin = new Thickness(0, 0, 8, 0),
            Child = grid,
        };
    }

    /// <summary>按设置询问安装目录；返回 null 表示用户取消。</summary>
    private string? AskInstallTarget(string fallback)
    {
        if (!_settings.AskInstallFolder)
        {
            return fallback;
        }

        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择要装进哪个游戏目录（会写入它的 mods / resourcepacks 等文件夹）",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_settings.LastInstallFolder) ? _settings.LastInstallFolder : fallback,
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return null;
        }

        _settings.LastInstallFolder = dialog.SelectedPath;
        ConfigStore.Save(_settings);
        return dialog.SelectedPath;
    }

    private async Task InstallVersionAsync(ContentItem item, ContentVersion version, Button? button)
    {
        try
        {
            DetailStatus.Text = $"安装 {version.Number}…";
            var target = VersionList.SelectedItem as VersionCard;
            var resolved = target == null ? null : _service.Resolve(target.Summary.Id);
            var gameDir = resolved == null ? _settings.GameDirectory : _service.GameDirectoryFor(resolved);

            var chosen = AskInstallTarget(gameDir);
            if (string.IsNullOrEmpty(chosen))
            {
                DetailStatus.Text = "已取消安装";
                if (button != null)
                {
                    button.IsEnabled = true;
                }
                return;
            }

            gameDir = chosen;

            var file = new ContentFile
            {
                Id = version.Id,
                FileName = version.FileName,
                DisplayName = version.Number,
                Url = version.Url,
                Size = version.Size,
                GameVersions = version.GameVersions,
                Loaders = version.Loaders,
            };

            var task = _service.CreateDownloadManager().Enqueue(file, item.Kind, gameDir);
            DetailStatus.Text = "已加入下载管理: " + task.FileName;
            UpdateDownloadHint();
            if (button != null)
            {
                button.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            DetailStatus.Text = "安装失败: " + ex.Message;
            if (button != null)
            {
                button.IsEnabled = true;
            }
        }
    }

    private void OnFileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (FileCombo.SelectedItem is FileEntry entry)
        {
            ContentStatus.Text = $"{entry.File.FileName} · {FormatSize(entry.File.Size)}";
        }
    }

    private async void OnInstallContent(object sender, RoutedEventArgs e)
    {
        if (FileCombo.SelectedItem is not FileEntry entry || _selectedContent == null)
        {
            return;
        }

        // 整合包：下完直接导入成版本，别只丢一个 mrpack 在下载目录里让玩家自己找
        if (_selectedContent.Kind == ContentKind.Modpack)
        {
            await InstallContentModpackAsync(entry.File);
            return;
        }

        var target = VersionList.SelectedItem as VersionCard;
        var resolved = target == null ? null : _service.Resolve(target.Summary.Id);
        var gameDir = resolved == null ? _settings.GameDirectory : _service.GameDirectoryFor(resolved);

        try
        {
            var task = _service.CreateDownloadManager().Enqueue(entry.File, _selectedContent.Kind, gameDir);
            ContentStatus.Text = "已加入下载管理: " + task.FileName;
            UpdateDownloadHint();
        }
        catch (Exception ex)
        {
            ContentStatus.Text = "安装失败: " + ex.Message;
        }
    }

    /// <summary>整合包：下载 + 解包 + 建版本，一条龙，进度走下载管理。</summary>
    private async Task InstallContentModpackAsync(ContentFile file)
    {
        var manager = _service.CreateDownloadManager();
        var job = manager.EnqueueJob("整合包 " + file.FileName, "整合包");
        var cts = new CancellationTokenSource();
        _installCts[job.Id] = cts;
        CancelInstallButton.Visibility = Visibility.Visible;
        ContentStatus.Text = "正在下载并导入整合包…";

        try
        {
            var progress = new Progress<InstallProgress>(p =>
            {
                if (job.State == DownloadState.Canceled && !cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }

                manager.UpdateJob(job, p.Percent, $"[{p.Stage}] {p.Message}");
                ContentStatus.Text = $"[{p.Stage}] {p.Message} · {p.Percent:0}%";
            });

            var id = await _service.CreateModpackInstaller().InstallAsync(file, progress, cts.Token);
            manager.CompleteJob(job, true);
            LoadVersions();
            ReloadVmVersions();

            ContentStatus.Text = "整合包已导入成版本：" + id + "（已按设置做版本隔离）";
            ShowToast("整合包已导入", id, "✓", 5000);
        }
        catch (OperationCanceledException)
        {
            manager.CompleteJob(job, false, "已取消");
            ContentStatus.Text = "已取消导入整合包";
        }
        catch (Exception ex)
        {
            manager.CompleteJob(job, false, ex.Message);
            ContentStatus.Text = "整合包导入失败: " + ex.Message;
        }
        finally
        {
            _installCts.Remove(job.Id);
            cts.Dispose();
            if (_installCts.Count == 0)
            {
                CancelInstallButton.Visibility = Visibility.Collapsed;
            }
        }
    }

    // ---------------- 更新检查 ----------------

    private async Task CheckUpdatesAsync(bool silent)
    {
        try
        {
            if (!silent && UpdateStatus != null)
            {
                UpdateStatus.Text = "正在检查…";
            }

            var checker = _service.CreateUpdateChecker();
            var release = await checker.GetLatestAsync();
            if (release == null)
            {
                if (!silent && UpdateStatus != null)
                {
                    UpdateStatus.Text = "检查失败（网络或 GitHub API 不可达）";
                }
                return;
            }

            _pendingRelease = release;
            if (UpdateChecker.IsNewer(release.Tag, AppVersion))
            {
                if (UpdateBanner != null)
                {
                    UpdateBanner.Visibility = Visibility.Visible;
                    UpdateBannerText.Text = $"发现新版本 {release.Tag}（当前 v{AppVersion}）";
                }

                if (UpdateStatus != null)
                {
                    UpdateStatus.Text = $"有新版本 {release.Tag}";
                }
            }
            else
            {
                if (UpdateBanner != null)
                {
                    UpdateBanner.Visibility = Visibility.Collapsed;
                }

                if (UpdateStatus != null)
                {
                    UpdateStatus.Text = $"已是最新版本（v{AppVersion}）";
                }
            }
        }
        catch (Exception ex)
        {
            if (!silent && UpdateStatus != null)
            {
                UpdateStatus.Text = "检查失败: " + ex.Message;
            }
        }
    }

    private async void OnCheckUpdates(object sender, RoutedEventArgs e) => await CheckUpdatesAsync(silent: false);

    private void OnOpenUpdatesPage(object sender, RoutedEventArgs e) => SetPage(5);

    private void OnOpenUpdate(object sender, RoutedEventArgs e)
    {
        if (_pendingRelease == null)
        {
            return;
        }

        BuildUpdateDialog(_pendingRelease).ShowDialog();
    }

    private Window BuildUpdateDialog(ReleaseInfo release)
    {
        var panel = new StackPanel { Margin = new Thickness(26) };

        panel.Children.Add(new TextBlock
        {
            Text = release.Tag + " · " + release.PublishedAt.ToLocalTime().ToString("yyyy-MM-dd"),
            FontSize = 12,
            Foreground = (Brush)FindResource("InkDim"),
        });

        panel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(release.Name) ? release.Tag : release.Name,
            FontSize = 20,
            FontFamily = (FontFamily)FindResource("FontHead"),
            Foreground = (Brush)FindResource("Ink"),
            Margin = new Thickness(0, 8, 0, 12),
            TextWrapping = TextWrapping.Wrap,
        });

        var notes = new TextBox
        {
            Text = release.Body ?? string.Empty,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 240,
            Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0E, 0x13)),
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA6, 0xBA)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 12,
        };
        panel.Children.Add(notes);

        var status = new TextBlock
        {
            Text = string.Empty,
            FontSize = 11,
            Foreground = (Brush)FindResource("InkFaint"),
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        panel.Children.Add(status);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };

        var window = new Window
        {
            Owner = this,
            Title = "检查更新",
            Width = 560,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)FindResource("Bg"),
            Content = panel,
        };

        var apply = new Button
        {
            Content = "立即更新",
            Style = (Style)FindResource("HeroButton"),
            Width = 140,
            Height = 38,
        };

        var page = new Button
        {
            Content = "打开下载页",
            Style = (Style)FindResource("GlassButton"),
            Width = 120,
            Margin = new Thickness(8, 0, 0, 0),
        };

        var later = new Button
        {
            Content = "稍后",
            Style = (Style)FindResource("GlassButton"),
            Width = 80,
            Margin = new Thickness(8, 0, 0, 0),
        };

        apply.Click += async (_, __) =>
        {
            if (SelfUpdater.PickAsset(release) == null)
            {
                status.Text = "这个版本没附带 exe，请用「打开下载页」手动下载";
                return;
            }

            apply.IsEnabled = false;
            page.IsEnabled = false;
            later.IsEnabled = false;
            status.Text = "开始下载…";

            try
            {
                var updater = _service.CreateSelfUpdater();
                var progress = new Progress<double>(p =>
                    status.Text = $"下载中 {p * 100:0}%");

                var file = await updater.DownloadAsync(release, progress, default,
                    source => Dispatcher.BeginInvoke(() => status.Text = "正在从 " + source + " 下载…"));
                status.Text = "下载完成，正在重启并替换…";

                SelfUpdater.ApplyAndRestart(file);
                await Task.Delay(300);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                status.Text = "更新失败：" + ex.Message;
                apply.IsEnabled = true;
                page.IsEnabled = true;
                later.IsEnabled = true;
            }
        };

        page.Click += (_, __) =>
        {
            var url = string.IsNullOrEmpty(release.HtmlUrl)
                ? "https://github.com/OStonefishO/STMEMELauncher/releases"
                : release.HtmlUrl;
            OpenBrowser(url);
        };

        later.Click += (_, __) => window.Close();

        buttons.Children.Add(apply);
        buttons.Children.Add(page);
        buttons.Children.Add(later);
        panel.Children.Add(buttons);

        return window;
    }

    // ---------------- 联机 ----------------

    private int Port => int.TryParse(PortBox?.Text, out var p) ? p : 25565;

    private void OnRefreshAddresses(object sender, RoutedEventArgs e) => RefreshAddresses();

    private void RefreshAddresses()
    {
        if (AddressList == null)
        {
            return;
        }

        AddressList.Children.Clear();
        var addresses = NetworkProbe.GetAddresses();
        if (addresses.Count == 0)
        {
            AddressList.Children.Add(new TextBlock
            {
                Text = "没有检测到可用地址（可能只有内网 IPv4）",
                FontSize = 11.5,
                Foreground = (Brush)FindResource("InkFaint"),
            });
            return;
        }

        foreach (var address in addresses)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 2, 7, 2),
                Background = new SolidColorBrush(address.Family == "IPv6"
                    ? Color.FromArgb(0x33, 0x4A, 0xDE, 0x80)
                    : Color.FromArgb(0x22, 0x8C, 0x94, 0xA8)),
                Child = new TextBlock
                {
                    Text = address.Family,
                    FontSize = 10,
                    Foreground = (Brush)FindResource("InkDim"),
                },
            };
            row.Children.Add(badge);

            var text = address.WithPort(Port);
            var label = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Margin = new Thickness(10, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("Ink"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            var copy = new Button
            {
                Content = "复制",
                Style = (Style)FindResource("GlassButton"),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
            };
            copy.Click += (_, __) =>
            {
                try
                {
                    Clipboard.SetText(text);
                    TunnelStatus.Text = "已复制：" + text;
                }
                catch
                {
                    // 忽略
                }
            };
            Grid.SetColumn(copy, 2);
            row.Children.Add(copy);

            AddressList.Children.Add(row);
        }

        var has6 = addresses.Any(a => a.Family == "IPv6");
        TunnelStatus.Text = has6
            ? "本机有公网 IPv6，朋友可以直接用 [IPv6]:端口 连进来"
            : "只有内网 IPv4，外网联机需要下面的樱花穿透或 UPnP";
    }

    private async void OnUpnpMap(object sender, RoutedEventArgs e)
    {
        var port = Port;
        UpnpStatus.Text = "正在找路由器（SSDP 发现）…";

        try
        {
            var discovered = await _upnp.DiscoverAsync();
            if (!discovered)
            {
                UpnpStatus.Text = "路由器没有响应 UPnP 发现。可能关了这个功能，或者你在校园网/公司网里（这种网络一般不给 UPnP）。";
                return;
            }

            UpnpStatus.Text = "正在添加端口映射…";
            var result = await _upnp.MapPortAsync(port, "MEME Launcher");
            if (!result.Success)
            {
                UpnpStatus.Text = "映射失败：" + result.Message;
                return;
            }

            UpnpStatus.Text = string.IsNullOrEmpty(result.ExternalIp)
                ? $"映射成功：{result.LocalIp}:{port} → 外网 {port}（没拿到公网 IP，去 ip.sb 看一下）"
                : $"映射成功！朋友可以连 {result.ExternalIp}:{port}";
        }
        catch (Exception ex)
        {
            UpnpStatus.Text = "UPnP 出错：" + ex.Message;
        }
    }

    private async void OnUpnpUnmap(object sender, RoutedEventArgs e)
    {
        try
        {
            var ok = await _upnp.UnmapPortAsync(Port);
            UpnpStatus.Text = ok ? "已删除端口映射" : "删除失败（可能本来就没有映射）";
        }
        catch (Exception ex)
        {
            UpnpStatus.Text = "删除出错：" + ex.Message;
        }
    }

    private async void OnEnsureFrpc(object sender, RoutedEventArgs e)
    {
        try
        {
            TunnelStatus.Text = "准备 frpc…";
            await _tunnel.EnsureAsync(new Progress<string>(t => TunnelStatus.Text = t));
            TunnelStatus.Text = "frpc 已就绪：" + SakuraFrp.ExePath;
        }
        catch (Exception ex)
        {
            TunnelStatus.Text = "下载失败: " + ex.Message;
        }
    }

    private async void OnStartTunnel(object sender, RoutedEventArgs e)
    {
        var token = TunnelTokenBox.Text?.Trim() ?? string.Empty;
        var ids = TunnelIdBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(ids))
        {
            TunnelStatus.Text = "访问密钥和隧道 ID 都要填";
            return;
        }

        try
        {
            TunnelStartButton.IsEnabled = false;
            TunnelStatus.Text = "准备 frpc…";
            await _tunnel.EnsureAsync(new Progress<string>(t => TunnelStatus.Text = t));

            _tunnel.Start(token, ids);
            TunnelStopButton.IsEnabled = true;
            TunnelStatus.Text = "隧道启动中，看下面的日志确认连接地址";
        }
        catch (Exception ex)
        {
            TunnelStatus.Text = "启动失败: " + ex.Message;
            TunnelStartButton.IsEnabled = true;
        }
    }

    private void OnStopTunnel(object sender, RoutedEventArgs e)
    {
        _tunnel.Stop();
        TunnelStopButton.IsEnabled = false;
        TunnelStartButton.IsEnabled = true;
        TunnelStatus.Text = "已停止";
    }

    private void AppendTunnelLog(string line)
    {
        if (TunnelLog == null)
        {
            return;
        }

        TunnelLog.AppendText(line + Environment.NewLine);
        TunnelLog.ScrollToEnd();

        if (line.Contains("启动成功", StringComparison.Ordinal) ||
            line.Contains("start proxy success", StringComparison.OrdinalIgnoreCase))
        {
            TunnelStatus.Text = "隧道已连通";
        }
        else if (line.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            TunnelStatus.Text = "frpc 报错，看日志";
        }
    }

    // ---------------- 一键开服 ----------------

    private void InitServerPanel()
    {
        _server ??= new ServerManager(_service.SharedDownloader, _settings.GameDirectory);
        _server.Output -= OnServerOutput;
        _server.Output += OnServerOutput;

        if (_serverVersionsLoaded)
        {
            return;
        }

        _serverVersions = _service.ListVersions()
            .Where(v => v.IsUsable)
            .OrderByDescending(v => v.ClientVersion, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ServerVersionCombo.ItemsSource = _serverVersions
            .Select(v => $"{v.Id}  ·  MC {v.ClientVersion}  ·  {v.Loader}")
            .ToList();
        if (_serverVersions.Count > 0)
        {
            ServerVersionCombo.SelectedIndex = 0;
        }

        _serverVersionsLoaded = true;
    }

    private void OnServerOutput(string line) => Dispatcher.BeginInvoke(() => AppendServerLog(line));

    private void AppendServerLog(string line)
    {
        if (ServerLog == null)
        {
            return;
        }

        ServerLog.AppendText(line + Environment.NewLine);
        ServerLog.ScrollToEnd();

        if (line.Contains("Done (", StringComparison.Ordinal) || line.Contains("For help, type", StringComparison.Ordinal))
        {
            ServerStatus.Text = "服务器已就绪，可以把地址发给朋友了";
        }
    }

    private void OnOpenServerGuide(object sender, RoutedEventArgs e) => BuildServerGuideDialog().ShowDialog();

    private Window BuildServerGuideDialog()
    {
        var panel = new StackPanel { Margin = new Thickness(26) };

        panel.Children.Add(new TextBlock
        {
            Text = "开服教程",
            FontSize = 22,
            FontFamily = (FontFamily)FindResource("FontHead"),
            Foreground = (Brush)FindResource("Ink"),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "照着做，五分钟能让朋友进你的世界",
            FontSize = 11.5,
            Foreground = (Brush)FindResource("InkDim"),
            Margin = new Thickness(0, 6, 0, 14),
        });

        void Section(string title, params string[] lines)
        {
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Ink"),
                Margin = new Thickness(0, 14, 0, 6),
            });

            foreach (var line in lines)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = line,
                    FontSize = 12,
                    Foreground = (Brush)FindResource("InkDim"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 3),
                    LineHeight = 19,
                });
            }
        }

        Section("① 开服",
            "在「版本」里选一个已安装的版本 → 填端口（默认 25565）→ 填内存（2048 够 5 个人）",
            "需要正版服就勾「开启正版验证」；离线玩家一起玩就别勾",
            "勾选「我已阅读并同意 Minecraft EULA」，点「开服」",
            "看到控制台出现 Done (x.xxxs)! 就是起来了");

        Section("② 让朋友进来（三选一）",
            "IPv6 直连：本机地址里有 IPv6 的话，直接发 [你的IPv6]:端口，最省事",
            "UPnP：点「UPnP 开端口」，成功会给你一个外网 IP:端口",
            "樱花穿透：natfrp.com 建 TCP 隧道指向 127.0.0.1:端口，填访问密钥和隧道 ID，点「启动隧道」");

        Section("③ 朋友怎么连",
            "游戏里「多人游戏」→「添加服务器」→ 把地址粘进去 → 加入",
            "局域网里的朋友也可以直接用 内网IP:端口");

        Section("常见问题",
            "连不上：先看服务端控制台有没有 Connection 记录，没有就是网络没通（防火墙/端口/映射）",
            "Windows 防火墙：第一次开服会弹窗，记得点「允许访问」，专用和公用都勾上",
            "装了 mod 的服：朋友的 mod 必须和服务端一致，否则会报错退回去",
            "内存不够：看到 Java heap space 就把内存调大，但别超过物理内存的一半",
            "正版验证：离线账号进正版服会报 Invalid session，要么关掉正版验证，要么让朋友用正版");

        var close = new Button
        {
            Content = "知道了",
            Style = (Style)FindResource("HeroButton"),
            Width = 120,
            Height = 38,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        panel.Children.Add(close);

        var scroll = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 560,
        };

        var window = new Window
        {
            Owner = this,
            Title = "开服教程",
            Width = 600,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)FindResource("Bg"),
            Content = scroll,
        };

        close.Click += (_, __) => window.Close();
        return window;
    }

    private async void OnStartServer(object sender, RoutedEventArgs e)
    {
        if (ServerVersionCombo.SelectedIndex < 0 || ServerVersionCombo.SelectedIndex >= _serverVersions.Count)
        {
            ServerStatus.Text = "先在「启动」页装一个版本";
            return;
        }

        var summary = _serverVersions[ServerVersionCombo.SelectedIndex];
        if (summary == null)
        {
            ServerStatus.Text = "先在「启动」页装一个版本";
            return;
        }

        if (ServerEula.IsChecked != true)
        {
            ServerStatus.Text = "需要先勾选同意 Minecraft EULA";
            return;
        }

        var isForgeFamily = summary.Loader is "forge" or "neoforge";
        var loader = isForgeFamily
            ? summary.Loader
            : summary.Loader.Equals("fabric", StringComparison.OrdinalIgnoreCase) ? "fabric" : "vanilla";

        try
        {
            ServerStartButton.IsEnabled = false;
            ServerStatus.Text = "准备服务端…";

            _server ??= new ServerManager(_service.SharedDownloader, _settings.GameDirectory);
            _serverDirectory = Path.Combine(ServerManager.ServersRoot(_settings.GameDirectory), SafeName(summary.Id));
            Directory.CreateDirectory(_serverDirectory);

            var progress = new Progress<InstallProgress>(p => ServerStatus.Text = $"{p.Stage} · {p.Message}");

            var port = int.TryParse(ServerPortBox.Text, out var p1) ? p1 : 25565;
            var memory = int.TryParse(ServerMemoryBox.Text, out var m1) ? Math.Max(1024, m1) : 2048;
            var java = _service.SelectJava(summary.JavaMajor, out _)?.JavaPath;
            if (string.IsNullOrEmpty(java))
            {
                ServerStatus.Text = $"没有找到 Java {summary.JavaMajor}";
                ServerStartButton.IsEnabled = true;
                return;
            }

            var options = new ServerLaunchOptions
            {
                Directory = _serverDirectory,
                Port = port,
                MemoryMb = memory,
                OnlineMode = ServerOnlineMode.IsChecked == true,
                AcceptEula = true,
                JavaPath = java,
                Motd = "MEME Launcher · 一键开服",
            };

            if (isForgeFamily)
            {
                if (string.IsNullOrEmpty(summary.LoaderVersion))
                {
                    ServerStatus.Text = "这个版本没记录 Forge/NeoForge 版本号，没法装服务端";
                    ServerStartButton.IsEnabled = true;
                    return;
                }

                var layout = await _server.InstallForgeServerAsync(
                    summary.ClientVersion, loader, summary.LoaderVersion, _serverDirectory, java, progress);

                options = new ServerLaunchOptions
                {
                    Directory = _serverDirectory,
                    Port = port,
                    MemoryMb = memory,
                    OnlineMode = ServerOnlineMode.IsChecked == true,
                    AcceptEula = true,
                    JavaPath = java,
                    Motd = "MEME Launcher · 一键开服",
                    Mode = layout.Mode,
                    JarPath = layout.JarPath,
                    ArgsFile = layout.ArgsFile,
                    UserJvmArgsFile = layout.UserJvmArgsFile,
                };
            }
            else
            {
                var jar = await _server.EnsureServerJarAsync(summary.ClientVersion, loader, _serverDirectory, progress);
                options = new ServerLaunchOptions
                {
                    Directory = _serverDirectory,
                    JarPath = jar,
                    Port = port,
                    MemoryMb = memory,
                    OnlineMode = ServerOnlineMode.IsChecked == true,
                    AcceptEula = true,
                    JavaPath = java,
                    Motd = "MEME Launcher · 一键开服",
                };
            }

            _server.Configure(options);
            _server.Start(options);

            ServerStopButton.IsEnabled = true;
            ServerStatus.Text = $"服务端已启动，端口 {port}（目录：{_serverDirectory}）";
        }
        catch (Exception ex)
        {
            ServerStatus.Text = "开服失败: " + ex.Message;
            ServerStartButton.IsEnabled = true;
        }
    }

    private void OnStopServer(object sender, RoutedEventArgs e)
    {
        try
        {
            _server?.Stop();
        }
        catch
        {
            // 忽略
        }

        ServerStopButton.IsEnabled = false;
        ServerStartButton.IsEnabled = true;
        ServerStatus.Text = "已停止";
    }

    private void OnOpenServerFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = string.IsNullOrEmpty(_serverDirectory)
                ? ServerManager.ServersRoot(_settings.GameDirectory)
                : _serverDirectory;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch
        {
            // 忽略
        }
    }

    private static string SafeName(string name)
    {
        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(bad, '_');
        }

        return name.Replace(' ', '_');
    }

    // ---------------- 账号 ----------------

    private async void OnLogin(object sender, RoutedEventArgs e)
    {
        var clientId = ClientIdBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(clientId))
        {
            AccountStatus.Text = "还没配置 ClientId：展开下面的「高级」，填一次就好";
            return;
        }

        _settings.MicrosoftClientId = clientId;
        ConfigStore.Save(_settings);

        var auth = _service.CreateAuth();
        DeviceCodeFlow flow;
        try
        {
            flow = await auth.StartAsync();
        }
        catch (Exception ex)
        {
            AccountStatus.Text = "获取设备码失败: " + ex.Message;
            return;
        }

        _loginCts = new CancellationTokenSource();
        var dialog = BuildLoginDialog(flow);
        var poll = Task.Run(() => auth.WaitForAuthorizationAsync(flow, _loginCts.Token));
        dialog.Closed += (_, __) =>
        {
            if (!poll.IsCompleted)
            {
                _loginCts.Cancel();
            }
        };

        dialog.Show();

        try
        {
            var session = await poll;
            dialog.Close();
            var account = new SavedAccount
            {
                Type = "microsoft",
                Name = session.PlayerName,
                Uuid = session.Uuid,
                AccessToken = session.AccessToken,
            };
            _service.UpsertAccount(account);
            RefreshAccounts();
            AccountPageStatus.Text = $"微软登录成功：{session.PlayerName}";
        }
        catch (Exception ex)
        {
            dialog.Close();
            AccountStatus.Text = "登录失败: " + ex.Message;
        }
    }

    private void OnLogout(object sender, RoutedEventArgs e)
    {
        var offline = _service.Settings.Accounts.FirstOrDefault(a => a.Type == "offline");
        if (offline != null)
        {
            _service.SwitchAccount(offline.Id);
            RefreshAccounts();
            AccountPageStatus.Text = "已切回离线账号";
        }
    }

    // ---------------- 外置登录 / 导入 ----------------

    private async void OnYggdrasilLogin(object sender, RoutedEventArgs e)
    {
        var dialog = BuildYggdrasilDialog(out var apiBox, out var emailBox, out var passBox);
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var apiRoot = apiBox.Text?.Trim() ?? string.Empty;
        var email = emailBox.Text?.Trim() ?? string.Empty;
        var password = passBox.Password;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            AccountStatus.Text = "邮箱和密码都要填";
            return;
        }

        try
        {
            AccountStatus.Text = "正在登录外置账号…";
            var auth = _service.CreateYggdrasil(apiRoot);
            var session = await auth.AuthenticateAsync(email, password);

            AccountStatus.Text = "下载 authlib-injector…";
            var installer = _service.CreateInjectorInstaller();
            await installer.EnsureAsync(_settings.GameDirectory, new Progress<string>(t => AccountStatus.Text = t));

            var account = new SavedAccount
            {
                Type = "yggdrasil",
                Name = session.PlayerName,
                Uuid = session.Uuid,
                AccessToken = session.AccessToken,
                ClientToken = session.ClientToken,
                ApiRoot = session.ApiRoot,
                Email = email,
            };
            _service.SaveYggdrasil(session);
            _service.UpsertAccount(account);
            _settings.YggdrasilEmail = email;
            ConfigStore.Save(_settings);
            RefreshAccounts();
            AccountPageStatus.Text = $"外置登录成功：{session.PlayerName}";
        }
        catch (Exception ex)
        {
            AccountStatus.Text = "外置登录失败: " + ex.Message;
        }
    }

    private Window BuildYggdrasilDialog(out TextBox apiBox, out TextBox emailBox, out PasswordBox passBox)
    {
        var panel = new StackPanel { Margin = new Thickness(26) };

        panel.Children.Add(new TextBlock
        {
            Text = "服务器地址",
            FontSize = 12,
            Foreground = (Brush)FindResource("InkDim"),
        });
        apiBox = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(_settings.YggdrasilApiRoot)
                ? "https://littleskin.cn/api/yggdrasil"
                : _settings.YggdrasilApiRoot,
            Margin = new Thickness(0, 6, 0, 14),
        };
        panel.Children.Add(apiBox);

        panel.Children.Add(new TextBlock { Text = "邮箱", FontSize = 12, Foreground = (Brush)FindResource("InkDim") });
        emailBox = new TextBox { Text = _settings.YggdrasilEmail, Margin = new Thickness(0, 6, 0, 14) };
        panel.Children.Add(emailBox);

        panel.Children.Add(new TextBlock { Text = "密码", FontSize = 12, Foreground = (Brush)FindResource("InkDim") });
        passBox = new PasswordBox { Margin = new Thickness(0, 6, 0, 16), Padding = new Thickness(9, 7, 9, 7) };
        panel.Children.Add(passBox);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "登录", Style = (Style)FindResource("HeroButton"), Width = 96, Height = 36, IsDefault = true };
        var cancel = new Button { Content = "取消", Style = (Style)FindResource("GlassButton"), Width = 80, Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        var window = new Window
        {
            Owner = this,
            Title = "外置登录",
            Width = 430,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)FindResource("Bg"),
            Content = panel,
        };

        ok.Click += (_, __) => window.DialogResult = true;
        return window;
    }

    private Window BuildLoginDialog(DeviceCodeFlow flow)
    {
        var panel = new StackPanel { Margin = new Thickness(26) };

        panel.Children.Add(new TextBlock
        {
            Text = "把这个验证码输入浏览器（已自动复制）",
            FontSize = 13,
            Foreground = (Brush)FindResource("TextDim"),
            Margin = new Thickness(0, 0, 0, 12),
        });

        panel.Children.Add(new TextBlock
        {
            Text = flow.UserCode,
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Accent"),
            Margin = new Thickness(0, 0, 0, 14),
        });

        panel.Children.Add(new TextBlock
        {
            Text = flow.VerificationUri,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextFaint"),
            Margin = new Thickness(0, 0, 0, 16),
        });

        var status = new TextBlock
        {
            Text = "等待授权…",
            FontSize = 12,
            Foreground = (Brush)FindResource("TextDim"),
            Margin = new Thickness(0, 14, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var open = new Button
        {
            Content = "重新打开浏览器",
            Style = (Style)FindResource("GhostButton"),
        };
        open.Click += (_, __) => OpenBrowser(flow.VerificationUri);

        var copy = new Button
        {
            Content = "复制验证码",
            Style = (Style)FindResource("GhostButton"),
            Margin = new Thickness(8, 0, 0, 0),
        };
        copy.Click += (_, __) =>
        {
            try
            {
                Clipboard.SetText(flow.UserCode);
                status.Text = "验证码已复制";
            }
            catch
            {
                // 忽略剪贴板占用
            }
        };

        row.Children.Add(open);
        row.Children.Add(copy);
        panel.Children.Add(row);
        panel.Children.Add(status);

        var window = new Window
        {
            Owner = this,
            Title = "微软登录",
            Width = 430,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)FindResource("Bg"),
            Content = panel,
        };

        // 打开就自动复制验证码并拉起浏览器，玩家只需要在网页里确认
        window.Loaded += (_, __) =>
        {
            try
            {
                Clipboard.SetText(flow.UserCode);
            }
            catch
            {
                // 忽略
            }

            OpenBrowser(flow.VerificationUri);
        };

        return window;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 打不开就手动复制
        }
    }
    private void UpdateAccountChip()
    {
        var account = _service.ActiveAccount;
        if (account == null)
        {
            AccountChip.Text = "离线：" + _settings.PlayerName;
            AccountStatus.Text = "还没有账号，去「账号」页建一个";
            return;
        }

        AccountChip.Text = account.TypeLabel + "：" + account.Name;
        AccountStatus.Text = $"当前账号：{account.Name}（{account.TypeLabel}）";
        if (AccountPageStatus != null)
        {
            AccountPageStatus.Text = $"共 {_service.Settings.Accounts.Count} 个账号，点卡片切换";
        }
    }

    private void OnOpenAccounts(object sender, RoutedEventArgs e) => SetPage(3);

    private void RefreshAccounts()
    {
        if (AccountList == null)
        {
            return;
        }

        AccountList.Children.Clear();
        var activeId = _service.ActiveAccount?.Id;
        foreach (var account in _service.Settings.Accounts.OrderByDescending(a => a.LastUsed))
        {
            AccountList.Children.Add(BuildAccountCard(account, account.Id == activeId));
        }

        if (_service.Settings.Accounts.Count == 0)
        {
            AccountList.Children.Add(new TextBlock
            {
                Text = "还没有保存的账号，右边新建一个离线账号吧",
                FontSize = 12.5,
                Foreground = (Brush)FindResource("InkFaint"),
                Margin = new Thickness(6, 12, 0, 0),
            });
        }

        UpdateAccountChip();
    }

    private Border BuildAccountCard(SavedAccount account, bool isActive)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var initial = string.IsNullOrEmpty(account.Name) ? "?" : account.Name[..1].ToUpperInvariant();
        var headImage = new Image { Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
        var initialText = new TextBlock
        {
            Text = initial,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Ink"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var avatar = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x5B, 0x8C, 0xFF)),
            Child = new Grid
            {
                Children = { initialText, headImage },
                Clip = new RectangleGeometry(new Rect(0, 0, 40, 40), 20, 20),
            },
        };

        _ = LoadAccountHeadAsync(account, headImage, initialText);
        grid.Children.Add(avatar);

        var text = new StackPanel { Margin = new Thickness(14, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = account.Name,
            FontSize = 14.5,
            FontWeight = FontWeights.Medium,
            Foreground = (Brush)FindResource("Ink"),
        });
        text.Children.Add(new TextBlock
        {
            Text = account.TypeLabel + " · " + account.Subtitle,
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = (Brush)FindResource("InkFaint"),
        });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (isActive)
        {
            right.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0x4A, 0xDE, 0x80)),
                Padding = new Thickness(9, 3, 9, 3),
                Child = new TextBlock
                {
                    Text = "当前",
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC)),
                },
            });
        }
        else
        {
            var use = new Button
            {
                Content = "使用",
                Style = (Style)FindResource("GlassButton"),
                Padding = new Thickness(12, 5, 12, 5),
                FontSize = 11.5,
            };
            use.Click += (_, __) =>
            {
                _service.SwitchAccount(account.Id);
                RefreshAccounts();
            };
            right.Children.Add(use);
        }

        var skinButton = new Button
        {
            Content = "皮肤",
            Style = (Style)FindResource("GlassButton"),
            Padding = new Thickness(9, 4, 9, 4),
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 11,
            Tag = account,
        };
        skinButton.Click += OnChangeSkinForAccount;
        right.Children.Add(skinButton);

        var remove = new Button
        {
            Content = "✕",
            Style = (Style)FindResource("GlassButton"),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 11,
        };
        remove.Click += (_, __) =>
        {
            _service.RemoveAccount(account.Id);
            RefreshAccounts();
        };
        right.Children.Add(remove);

        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        var card = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = (Brush)FindResource("Glass"),
            BorderBrush = (Brush)FindResource("GlassLine"),
            BorderThickness = new Thickness(isActive ? 1.6 : 1),
            Padding = new Thickness(16, 13, 14, 13),
            Margin = new Thickness(0, 0, 0, 10),
            Child = grid,
            Cursor = System.Windows.Input.Cursors.Hand,
        };

        card.MouseLeftButtonUp += (_, __) =>
        {
            if (!isActive)
            {
                _service.SwitchAccount(account.Id);
                RefreshAccounts();
            }
        };

        return card;
    }

    private void OnChangeSkinForCurrent(object sender, RoutedEventArgs e)
    {
        var account = _service.ActiveAccount;
        if (account == null)
        {
            return;
        }

        // 外置 / 微软账号的皮肤归服务器管，本地写了也没用，直接把人送去换皮肤的地方
        if (account.Type is "yggdrasil" or "microsoft")
        {
            OpenSkinSite(account.Type);
            return;
        }

        ImportSkinFor(account);
    }

    private void OpenSkinSite(string accountType)
    {
        var url = accountType == "microsoft"
            ? "https://www.minecraft.net/msaprofile/mygames/editskin"
            : "https://littleskin.cn/user/player";

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            AccountPageStatus.Text = accountType == "microsoft"
                ? "皮肤要在 Minecraft 官网换，换完进游戏就是新的"
                : "在 LittleSkin 的「我的角色」里上传皮肤，换完重新启动游戏即可";
        }
        catch (Exception ex)
        {
            AccountPageStatus.Text = "打不开浏览器：" + ex.Message;
        }
    }

    private void OnChangeSkinForAccount(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SavedAccount account })
        {
            ImportSkinFor(account);
        }
    }

    private async void OnImportSkin(object sender, RoutedEventArgs e)
    {
        var account = _service.ActiveAccount;
        if (account == null)
        {
            AccountPageStatus.Text = "先选一个账号，皮肤会按账号名写进去";
            return;
        }

        ImportSkinFor(account);
    }

    private async void ImportSkinFor(SavedAccount account)
    {
        if (account.Type != "offline")
        {
            AccountPageStatus.Text = account.Type == "yggdrasil"
                ? "LittleSkin 账号的皮肤要在皮肤站上传，点「去 LittleSkin 换皮肤」"
                : "微软账号的皮肤要在 Minecraft 官网换，点「去官网换皮肤」";
            ShowToast("皮肤由服务器决定", "点下面的按钮去对应的网站换，换完重新启动游戏", "⚠", 4200);
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择皮肤 PNG（64x64 或 64x32）",
            Filter = "PNG 图片|*.png",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var target = VersionList.SelectedItem as VersionCard;
        var resolved = target == null ? null : _service.Resolve(target.Summary.Id);
        if (resolved == null)
        {
            AccountPageStatus.Text = "先在「启动」页选一个版本，皮肤要装进那个版本";
            return;
        }

        try
        {
            AccountPageStatus.Text = "正在安装皮肤…";
            var gameDir = _service.GameDirectoryFor(resolved);
            var progress = new Progress<InstallProgress>(p => AccountPageStatus.Text = $"{p.Stage} · {p.Message}");

            var result = await _service.CreateOfflineSkinService().ApplyAsync(
                dialog.FileName, account.Name, gameDir, resolved.ClientVersion, resolved.Loader, progress);

            AccountPageStatus.Text = result.ModAlreadyExisted
                ? $"皮肤已应用：{account.Name}.png（CustomSkinLoader 已在）"
                : $"皮肤已应用，并装好了 CustomSkinLoader：{account.Name}.png";
            RefreshAccounts();
            RefreshCurrentSkinPreview();
            ShowToast("皮肤已更新", $"{account.Name} 的皮肤换成 {Path.GetFileName(dialog.FileName)}", "✓", 4000);
        }
        catch (Exception ex)
        {
            AccountPageStatus.Text = "导入皮肤失败: " + ex.Message;
        }
    }

    /// <summary>找皮肤时要看的目录：全局游戏目录 + 当前版本目录（版本隔离）。</summary>
    private List<string> SkinSearchDirs()
    {
        var dirs = new List<string> { _settings.GameDirectory };
        try
        {
            if (VersionList?.SelectedItem is VersionCard card)
            {
                var resolved = _service.Resolve(card.Summary.Id);
                if (resolved != null)
                {
                    dirs.Add(_service.GameDirectoryFor(resolved));
                }
            }
        }
        catch
        {
            // 忽略
        }

        return dirs;
    }

    private async Task LoadAccountHeadAsync(SavedAccount account, Image target, TextBlock fallback)
    {
        try
        {
            var bytes = await SkinResolver.GetSkinBytesAsync(
                account.Type, account.Uuid, account.ApiRoot, account.Name, _settings.GameDirectory);

            if (bytes == null && account.Type == "offline")
            {
                var local = SkinResolver.FindLocalSkin(SkinSearchDirs(), account.Name);
                if (local != null)
                {
                    bytes = await File.ReadAllBytesAsync(local);
                }
            }

            if (bytes == null)
            {
                return;
            }

            var head = SkinImage.Head(bytes, 40);
            if (head != null)
            {
                target.Source = head;
                target.Visibility = Visibility.Visible;
                fallback.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            // 没皮肤就用首字母
        }
    }

    private BitmapSource? _skinBitmap;
    private bool _skinSlim;

    private void OnSkinModelToggled(object sender, RoutedEventArgs e)
    {
        _skinSlim = sender is FrameworkElement { Tag: "true" };
        var model = _skinSlim ? "slim" : "classic";

        try
        {
            AccountSkinWeb?.CoreWebView2?.ExecuteScriptAsync($"setModel('{model}')");
            if (AccountSkinStatus != null)
            {
                AccountSkinStatus.Text = (_skinBitmap == null ? "还没有皮肤" : "已加载皮肤") +
                                         "（" + (_skinSlim ? "Alex 细胳膊" : "Steve 粗胳膊") + "）";
            }
        }
        catch
        {
            // 忽略
        }
    }

    private AxisAngleRotation3D? _skinRotation;
    private Point _dragStart;
    private bool _dragging;









    private bool _skinWebReady;
    private byte[]? _pendingSkinBytes;
    private byte[]? _pendingCapeBytes;

    private async Task InitSkinWebAsync()
    {
        try
        {
            if (AccountSkinWeb == null)
            {
                return;
            }

            // WebView2 的缓存放到 LOCALAPPDATA，别污染启动器目录
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MEMELauncher", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment
                .CreateAsync(null, userDataFolder);
            await AccountSkinWeb.EnsureCoreWebView2Async(env);
            AccountSkinWeb.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            AccountSkinWeb.CoreWebView2.Settings.AreDevToolsEnabled = false;
            AccountSkinWeb.CoreWebView2.Settings.IsStatusBarEnabled = false;

            AccountSkinWeb.NavigationCompleted += async (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    return;
                }

                AccountSkinWeb.Visibility = Visibility.Visible;
                if (_pendingSkinBytes != null || _pendingCapeBytes != null)
                {
                    await ShowSkinInWeb(_pendingSkinBytes, _pendingCapeBytes);
                }
            };

            var page = EnsureSkinViewerAssets();
            AccountSkinWeb.Source = new Uri(page);
            _skinWebReady = true;

            // 兜底：就算 NavigationCompleted 没触发，1.5 秒后也把控件露出来
            await Task.Delay(1500);
            AccountSkinWeb.Visibility = Visibility.Visible;

            if (_pendingSkinBytes != null)
            {
                await ShowSkinInWeb(_pendingSkinBytes);
            }
        }
        catch (Exception ex)
        {
            // WebView2 不可用时静默降级（只显示头像）
            AccountSkinStatus.Text = "3D 预览不可用: " + ex.Message;
        }
    }

    /// <summary>把内嵌的 viewer.html / bundle 释放到本地目录，单文件 exe 也能用。</summary>
    private static string EnsureSkinViewerAssets()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MEMELauncher", "skinview");
        Directory.CreateDirectory(dir);

        foreach (var name in new[] { "viewer.html", "skinview3d.bundle.js" })
        {
            var target = Path.Combine(dir, name);
            if (File.Exists(target) && new FileInfo(target).Length > 0)
            {
                continue;
            }

            var resource = "Aurora.App.Assets.skinview." + name;
            using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resource);
            if (stream == null)
            {
                continue;
            }

            using var file = File.Create(target);
            stream.CopyTo(file);
        }

        return Path.Combine(dir, "viewer.html");
    }

    private async Task ShowSkinInWeb(byte[]? bytes, byte[]? capeBytes = null)
    {
        _pendingSkinBytes = bytes;
        _pendingCapeBytes = capeBytes;

        if (!_skinWebReady || AccountSkinWeb?.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            var skinUrl = "'data:image/png;base64," + Convert.ToBase64String(bytes) + "'";
            var capeUrl = capeBytes is { Length: > 0 }
                ? "'data:image/png;base64," + Convert.ToBase64String(capeBytes) + "'"
                : "null";

            await AccountSkinWeb.CoreWebView2.ExecuteScriptAsync($"setSkin({skinUrl}, {capeUrl})");
        }
        catch
        {
            // 忽略
        }
    }

    private async void RefreshCurrentSkinPreview()
    {
        var account = _service.ActiveAccount;
        if (account == null || AccountSkinImage == null)
        {
            return;
        }

        AccountSkinName.Text = account.Name + "（" + account.TypeLabel + "）";
        AccountSkinStatus.Text = "读取皮肤…";
        _skinBitmap = null;

        if (SkinActionButton != null)
        {
            SkinActionButton.Content = account.Type switch
            {
                "yggdrasil" => "去 LittleSkin 换皮肤",
                "microsoft" => "去官网换皮肤",
                _ => "更换这个账号的皮肤",
            };
        }

        try
        {
            var bytes = await SkinResolver.GetSkinBytesAsync(
                account.Type, account.Uuid, account.ApiRoot, account.Name, _settings.GameDirectory);

            if (bytes == null && account.Type == "offline")
            {
                var local = SkinResolver.FindLocalSkin(SkinSearchDirs(), account.Name);
                if (local != null)
                {
                    bytes = await File.ReadAllBytesAsync(local);
                }
            }

            if (bytes == null)
            {
                AccountSkinStatus.Text = account.Type switch
                {
                    "offline" => "还没有皮肤，可以点下面「导入离线皮肤」",
                    "yggdrasil" => "LittleSkin 上这个角色还没有皮肤，点下面按钮去上传",
                    "microsoft" => "Mojang 服务器上没有这个账号的皮肤，点下面按钮去官网设置",
                    _ => "这个账号没有设置皮肤",
                };
                return;
            }

            _skinBitmap = SkinImage.FromBytes(bytes);

            byte[]? capeBytes = null;
            try
            {
                var textures = await SkinResolver.GetTexturesAsync(
                    account.Type, account.Uuid, account.ApiRoot);
                if (!string.IsNullOrEmpty(textures.Cape))
                {
                    using var http = new System.Net.Http.HttpClient();
                    capeBytes = await http.GetByteArrayAsync(textures.Cape);
                }
            }
            catch
            {
                // 没披风就算了
            }

            _ = ShowSkinInWeb(bytes, capeBytes);
            if (AccountSkinImage != null)
            {
                AccountSkinImage.Source = SkinImage.Full(bytes, 256);
            }

            // 外置登录：皮肤能不能进游戏，取决于 authlib-injector 有没有装上
            var injectorReady = account.Type != "yggdrasil" ||
                                File.Exists(Path.Combine(_settings.GameDirectory, "authlib-injector.jar"));
            AccountSkinStatus.Text = account.Type switch
            {
                "yggdrasil" => injectorReady
                    ? "皮肤来自 LittleSkin · 已加载"
                    : "皮肤来自 LittleSkin · 但 authlib-injector 没装好，启动时会自动补（失败就进不了游戏）",
                "microsoft" => "皮肤来自 Mojang · 已加载",
                _ => "已加载皮肤",
            };
        }
        catch (Exception ex)
        {
            AccountSkinStatus.Text = "读取失败: " + ex.Message;
        }
    }

    private void OnAddOfflineAccount(object sender, RoutedEventArgs e)
    {
        var name = OfflineNameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            AccountPageStatus.Text = "先填一个名字";
            return;
        }

        var account = new SavedAccount
        {
            Type = "offline",
            Name = name,
        };
        account.Uuid = SavedAccount.NewOffline(name);

        _service.UpsertAccount(account);
        OfflineNameBox.Text = string.Empty;
        RefreshAccounts();
        AccountPageStatus.Text = $"已添加离线账号 {name}";
    }

    // ---------------- 设置 ----------------

    private void LoadSettingsToUi()
    {
        if (VersionText != null)
        {
            VersionText.Text = $"当前版本 v{AppVersion}";
        }

        SettingsGameDir.Text = _settings.GameDirectory;
        // 离线玩家名统一在「账号」页管理，这里不再单独设置
        SettingsMemory.Text = _settings.MaxMemoryMb.ToString();
        _ = RefreshJavaOptionsAsync(_settings.JavaPath);
        SettingsMirror.IsChecked = _settings.UseMirror;
        if (SettingsAskInstall != null)
        {
            SettingsAskInstall.IsChecked = _settings.AskInstallFolder;
        }
        ClientIdBox.Text = _settings.MicrosoftClientId;
        SettingsMirrorUrl.Text = _settings.MirrorBaseUrl;
        SettingsCfKey.Text = _settings.CurseForgeApiKey;
        SettingsIsolation.SelectedIndex = _settings.VersionIsolation?.ToLowerInvariant() switch
        {
            "off" => 2,
            "auto" => 1,
            _ => 0,
        };

        UpdateAccountChip();
    }

    private void OnSaveSettings(object sender, RoutedEventArgs e)
    {
        try
        {
            var dirChanged = !string.Equals(_settings.GameDirectory, SettingsGameDir.Text.Trim(),
                StringComparison.OrdinalIgnoreCase);

            _settings.GameDirectory = SettingsGameDir.Text.Trim();
            // 离线玩家名跟随当前账号，不在这里覆盖
            if (int.TryParse(SettingsMemory.Text, out var memory) && memory >= 512)
            {
                _settings.MaxMemoryMb = memory;
            }

            _settings.JavaPath = (SettingsJavaCombo.SelectedItem as JavaOption)?.Path ?? string.Empty;
            _settings.UseMirror = SettingsMirror.IsChecked == true;
            _settings.AskInstallFolder = SettingsAskInstall?.IsChecked != false;
            _settings.MicrosoftClientId = ClientIdBox.Text?.Trim() ?? string.Empty;
            _settings.MirrorBaseUrl = SettingsMirrorUrl.Text?.Trim() ?? string.Empty;
            _settings.CurseForgeApiKey = SettingsCfKey.Text?.Trim() ?? string.Empty;
            _settings.VersionIsolation = SettingsIsolation.SelectedIndex switch
            {
                2 => "off",
                1 => "auto",
                _ => "on",
            };

            ConfigStore.Save(_settings);

            if (dirChanged)
            {
                _service = new LauncherService(_settings);
                LoadVersions();
            }

            UpdateAccountChip();
            SaveStatus.Text = "已保存 · " + ConfigStore.ConfigPath;
            SetStatus("设置已保存");
        }
        catch (Exception ex)
        {
            SaveStatus.Text = "保存失败: " + ex.Message;
        }
    }

    // ---------------- 工具 ----------------

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private void ApplyDarkTitleBar()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var on = 1;
            if (DwmSetWindowAttribute(handle, 20, ref on, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, 19, ref on, sizeof(int));
            }
        }
        catch
        {
            // 老系统忽略
        }
    }

    // ---------------- 更新日志 ----------------

    private async Task LoadUpdatesAsync()
    {
        try
        {
            UpdateSubtitle.Text = "正在从 GitHub 拉取更新日志…";
            UpdateList.Children.Clear();

            var feed = _service.CreateUpdateFeed();
            var entries = await feed.FetchAsync();

            if (entries.Count == 0)
            {
                UpdateSubtitle.Text = "还没有发布记录";
                return;
            }

            foreach (var entry in entries)
            {
                UpdateList.Children.Add(BuildUpdateCard(entry, feed));
            }

            UpdateSubtitle.Text = $"共 {entries.Count} 个版本 · 数据来自 github.com/{feed.Repository}";
            _updatesLoaded = true;
        }
        catch (Exception ex)
        {
            UpdateSubtitle.Text = "拉取失败: " + ex.Message;
        }
    }

    private Border BuildUpdateCard(UpdateEntry entry, UpdateFeed feed)
    {
        var panel = new StackPanel();

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var badge = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x5B, 0x8C, 0xFF)),
            Padding = new Thickness(10, 4, 10, 4),
            Child = new TextBlock
            {
                Text = entry.Version,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB9, 0xC8, 0xFF)),
            },
        };
        head.Children.Add(badge);
        head.Children.Add(new TextBlock
        {
            Text = entry.DateText,
            FontSize = 11.5,
            Margin = new Thickness(10, 2, 0, 0),
            Foreground = (Brush)FindResource("InkFaint"),
        });
        panel.Children.Add(head);

        panel.Children.Add(new TextBlock
        {
            Text = entry.Title,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Ink"),
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap,
        });

        if (entry.Notes.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = string.Join("\n", entry.Notes),
                FontSize = 12,
                LineHeight = 21,
                Foreground = (Brush)FindResource("InkDim"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14),
            });
        }

        if (entry.Images.Count > 0)
        {
            var wrap = new WrapPanel();
            foreach (var url in entry.Images.Take(4))
            {
                var image = new Image
                {
                    Width = 356,
                    Height = 229,
                    Stretch = Stretch.UniformToFill,
                    Tag = url,
                };

                var frame = new Border
                {
                    Width = 356,
                    Height = 229,
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                    Margin = new Thickness(0, 0, 12, 12),
                    ClipToBounds = true,
                    Child = image,
                };

                frame.Child = image;
                wrap.Children.Add(frame);
                _ = LoadUpdateImageAsync(image, url, feed);
            }

            panel.Children.Add(wrap);
        }

        return new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = (Brush)FindResource("Glass"),
            BorderBrush = (Brush)FindResource("GlassLine"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(22, 18, 22, 12),
            Margin = new Thickness(0, 0, 0, 16),
            Child = panel,
        };
    }

    private async Task LoadUpdateImageAsync(Image target, string url, UpdateFeed feed)
    {
        try
        {
            var bytes = await feed.DownloadImageAsync(url);
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.EndInit();
            bitmap.Freeze();

            target.Source = bitmap;
        }
        catch
        {
            // 图片失败不影响文字
        }
    }

    // ---------------- 窗口与主界面 ----------------

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // 忽略拖拽异常
            }
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        _tunnel.Dispose();
        Close();
    }

    /// <summary>版本轮播用滚轮横向滚动（横向滚动条已经藏起来了）。</summary>
    private void UpdateVersionPicker()
    {
        if (VersionPickerText == null)
        {
            return;
        }

        VersionPickerText.Text = VersionList.SelectedItem is VersionCard card
            ? $"{card.Summary.Id}"
            : "未选择";
    }

    private void OnPickVersion(object sender, RoutedEventArgs e)
    {
        if (VersionList.ItemsSource is not IEnumerable<VersionCard> cards)
        {
            return;
        }

        var list = cards.ToList();
        if (list.Count == 0)
        {
            SetStatus("还没有可用版本，先去「内容 → 安装版本」装一个");
            return;
        }

        BuildVersionPopover(list);
        VersionPopover.IsOpen = !VersionPopover.IsOpen;
    }

    private void BuildVersionPopover(List<VersionCard> versions)
    {
        if (VersionPopoverList == null)
        {
            return;
        }

        VersionPopoverList.Children.Clear();
        var current = (VersionList.SelectedItem as VersionCard)?.Summary.Id;

        foreach (var card in versions)
        {
            var isCurrent = string.Equals(card.Summary.Id, current, StringComparison.OrdinalIgnoreCase);

            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new StackPanel { Margin = new Thickness(12, 8, 8, 8) };
            text.Children.Add(new TextBlock
            {
                Text = card.Summary.Id,
                FontSize = 12.5,
                FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = (Brush)FindResource(isCurrent ? "Ink" : "InkDim"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            text.Children.Add(new TextBlock
            {
                Text = $"MC {card.Summary.ClientVersion} · {card.Summary.Loader} · {card.Summary.ModCount} mods",
                FontSize = 10,
                Foreground = (Brush)FindResource("InkFaint"),
                Margin = new Thickness(0, 3, 0, 0),
            });
            row.Children.Add(text);

            if (isCurrent)
            {
                var badge = new TextBlock
                {
                    Text = "✓",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                };
                Grid.SetColumn(badge, 1);
                row.Children.Add(badge);
            }

            var border = new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = isCurrent
                    ? new SolidColorBrush(Color.FromArgb(0x33, 0x5B, 0x8C, 0xFF))
                    : Brushes.Transparent,
                Child = row,
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            border.MouseEnter += (_, __) =>
            {
                if (!isCurrent)
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
                }
            };
            border.MouseLeave += (_, __) =>
            {
                if (!isCurrent)
                {
                    border.Background = Brushes.Transparent;
                }
            };
            border.MouseLeftButtonUp += (_, __) =>
            {
                VersionList.SelectedItem = card;
                VersionPopover.IsOpen = false;
            };

            VersionPopoverList.Children.Add(border);
        }
    }

    private void OnVersionWheel(object sender, MouseWheelEventArgs e)
    {
        var viewer = FindScrollViewer(VersionList);
        if (viewer != null)
        {
            viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer)
        {
            return viewer;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void InitHero()
    {
        try
        {
            HeroViewport.Camera = HeroScene.Camera();
            HeroViewport.Children.Add(new ModelVisual3D { Content = HeroScene.Lights() });
            HeroViewport.Children.Add(HeroScene.Build());
        }
        catch (Exception ex)
        {
            SetStatus("3D 场景初始化失败: " + ex.Message);
        }
    }

    /// <summary>背景粒子：缓慢上浮的小光点，给静态界面一点生命感。</summary>
    private void InitParticles()
    {
        if (ParticleCanvas.Children.Count > 0)
        {
            return;
        }

        var width = ParticleCanvas.ActualWidth;
        var height = ParticleCanvas.ActualHeight;
        if (width < 10 || height < 10)
        {
            return;
        }

        var random = new Random(20261109);
        for (var i = 0; i < 42; i++)
        {
            var size = 1.5 + random.NextDouble() * 3.2;
            var alpha = (byte)(18 + random.Next(46));
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(alpha, 0x9B, 0xC4, 0xFF)),
                IsHitTestVisible = false,
            };

            Canvas.SetLeft(dot, random.NextDouble() * width);
            Canvas.SetTop(dot, height * 0.4 + random.NextDouble() * height * 0.7);

            var transform = new TranslateTransform();
            dot.RenderTransform = transform;

            var duration = TimeSpan.FromSeconds(14 + random.NextDouble() * 18);
            var animation = new System.Windows.Media.Animation.DoubleAnimation(0, -(height * 0.9 + 60), duration)
            {
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(random.NextDouble() * 12),
            };
            transform.BeginAnimation(TranslateTransform.YProperty, animation);

            ParticleCanvas.Children.Add(dot);
        }
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private static string Trim(string text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..(max - 1)] + "…";

    private static string FormatSize(long bytes) =>
        bytes >= 1 << 20 ? $"{bytes / 1024.0 / 1024.0:0.#} MB" : $"{bytes / 1024.0:0} KB";

    private sealed class VersionCard
    {
        public VersionCard(VersionSummary summary)
        {
            Summary = summary;
            LoaderColor = MakeBrush(summary.Loader);
            Subtitle = $"MC {summary.ClientVersion} · {summary.Loader}" +
                       (string.IsNullOrEmpty(summary.LoaderVersion) ? string.Empty : " " + summary.LoaderVersion) +
                       $" · Java {summary.JavaMajor} · Mods {summary.ModCount}";
        }

        public VersionSummary Summary { get; }

        public string Id => Summary.Id;

        public string ShortName => Summary.Id.Length > 20 ? Summary.Id[..19] + "…" : Summary.Id;

        public string Subtitle { get; }

        public Brush LoaderColor { get; }

        private static Brush MakeBrush(string loader) => loader switch
        {
            "forge" => NewBrush("#FFE0A040"),
            "neoforge" => NewBrush("#FFB07CFF"),
            "fabric" => NewBrush("#FF57C1A8"),
            "quilt" => NewBrush("#FFD06BD0"),
            "optifine" => NewBrush("#FF6FA8FF"),
            _ => NewBrush("#FF7A8290"),
        };

        private static Brush NewBrush(string hex) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    private sealed class ContentCard : System.ComponentModel.INotifyPropertyChanged
    {
        public ContentCard(ContentItem item)
        {
            Item = item;
            Meta = $"{item.Source} · {item.DownloadText} 次下载" +
                   (item.Categories.Count > 0 ? " · " + string.Join(" / ", item.Categories.Take(3)) : string.Empty);
            _ = LoadIconAsync();
        }

        public ContentItem Item { get; }

        public string Title => Item.Title;

        public string Summary => Item.Summary;

        public string Meta { get; }

        private ImageSource? _icon;

        public ImageSource? Icon
        {
            get => _icon;
            private set
            {
                _icon = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Icon)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private async Task LoadIconAsync()
        {
            if (string.IsNullOrWhiteSpace(Item.IconUrl))
            {
                return;
            }

            var bitmap = await IconCache.GetAsync(Item.IconUrl!).ConfigureAwait(true);
            if (bitmap != null)
            {
                Icon = bitmap;
            }
        }
    }

    private sealed class FileEntry
    {
        public FileEntry(ContentFile file) => File = file;

        public ContentFile File { get; }

        public string Label => string.IsNullOrEmpty(File.DisplayName)
            ? File.FileName
            : $"{File.DisplayName} · {File.FileName}";
    }
}
