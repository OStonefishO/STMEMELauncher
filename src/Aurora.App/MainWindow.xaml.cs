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
    private const string AppVersion = "0.15.55";
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

        // 调试用：--ui classic / vivid 强制指定布局（截图用）
        var cvFlag = Array.IndexOf(Environment.GetCommandLineArgs(), "--cv-value");
        if (cvFlag >= 0 && cvFlag + 1 < Environment.GetCommandLineArgs().Length)
        {
            _contentVersionMode = ContentVersionMode.Specific;
            _contentVersionValue = Environment.GetCommandLineArgs()[cvFlag + 1];
        }

        var hueFlag = Array.IndexOf(Environment.GetCommandLineArgs(), "--hue");
        if (hueFlag >= 0 && hueFlag + 1 < Environment.GetCommandLineArgs().Length &&
            int.TryParse(Environment.GetCommandLineArgs()[hueFlag + 1], out var hueValue))
        {
            _settings.UiHue = hueValue;
        }

        var uiFlag = Array.IndexOf(Environment.GetCommandLineArgs(), "--ui");
        if (uiFlag >= 0 && uiFlag + 1 < Environment.GetCommandLineArgs().Length)
        {
            _settings.UiLayout = Environment.GetCommandLineArgs()[uiFlag + 1];
        }

        // 界面布局（新版 / 经典）：构造后立刻套用一次
        ApplyUiLayout(_settings.UiLayout);
        BuildContentVersionFilter();
        PreviewKeyDown += OnWindowKeyDown;
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
            SetPage(Math.Clamp(pageIndex, 0, 9));
        }
        Loaded += (_, __) =>
        {
            ApplyDarkTitleBar();
            _ = SplashFadeOutAsync();
            _ = InitSkinWebAsync();
            _ = CheckUpdatesAsync(silent: true);
            _ = ProbeMirrorNoticeAsync();

            // 调试用：--shot <png>，等界面稳定后自己存一张图然后退出
            var shotFlag = Array.IndexOf(argv, "--shot");
            if (shotFlag >= 0 && shotFlag + 1 < argv.Length)
            {
                var waitFlag = Array.IndexOf(argv, "--shot-wait");
                var waitMs = waitFlag >= 0 && waitFlag + 1 < argv.Length && int.TryParse(argv[waitFlag + 1], out var ms)
                    ? ms
                    : 0;
                _ = CaptureAndExitAsync(argv[shotFlag + 1], waitMs);
            }

            // 调试用：--fake-downloads 往下载管理里塞两条假任务（截图看布局用）
            if (argv.Contains("--fake-downloads"))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var manager = _service.CreateDownloadManager();
                    var running = manager.EnqueueJob("安装 Minecraft 26.3（forge）", "版本");
                    manager.UpdateJob(running, 37, "[加载器] 下载安装器");

                    var failed = manager.Enqueue("modmenu-21.0.0.jar" != null
                        ? new ContentFile { FileName = "modmenu-21.0.0.jar", Url = "https://example.invalid/x.jar", Size = 837000 }
                        : new ContentFile(), ContentKind.Mod, _settings.GameDirectory);
                    manager.CompleteJob(failed, false, "已取消");

                    if (DownloadPanel != null)
                    {
                        DownloadPanel.Visibility = Visibility.Visible;
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }

            // 调试用：--banner 强制显示更新横幅（截图看配色用）
            if (argv.Contains("--banner") && UpdateBanner != null)
            {
                _ = Task.Delay(3000).ContinueWith(_ => Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateBannerText.Text = "发现新版本 v0.15.53 · 修复浅色主题下的深色块";
                    UpdateBanner.Visibility = Visibility.Visible;
                })));
            }

            // 调试用：--picker 直接展开版本选择弹层（截图用）
            if (argv.Contains("--picker"))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (VersionList.ItemsSource is IEnumerable<VersionCard> pickerCards)
                    {
                        OpenVersionPicker(pickerCards.ToList());
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }

            // 调试用：--shot-picker <png>，把窗口 + 版本弹层合成一张图（弹层是独立窗口，普通 --shot 截不到）
            var shotPickerFlag = Array.IndexOf(argv, "--shot-picker");
            if (shotPickerFlag >= 0 && shotPickerFlag + 1 < argv.Length)
            {
                _ = CapturePickerAsync(argv[shotPickerFlag + 1]);
            }

            // 调试用：--cv 直接展开「内容 → 内容下载」的版本筛选（截图用）
            var cvQueryFlag = Array.IndexOf(argv, "--cv-query");
            var cvQueryValue = cvQueryFlag >= 0 && cvQueryFlag + 1 < argv.Length ? argv[cvQueryFlag + 1] : null;
            if (argv.Contains("--cv") || cvQueryValue != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetPage(2);
                    ToggleContentVersionPopup();
                    if (cvQueryValue != null && _cvSearch != null)
                    {
                        _cvSearch.Text = cvQueryValue;
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
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

            // 调试用：--settings-mob 0..3 指定设置页角色（截图用）
            var mobFlag = Array.IndexOf(argv, "--settings-mob");
            if (mobFlag >= 0 && mobFlag + 1 < argv.Length && int.TryParse(argv[mobFlag + 1], out var mobIndex))
            {
                _settingsMob = (SettingsMob)Math.Clamp(mobIndex, 0, 3);
                InitSettingsScene();
                ApplySettingsMob();
            }
            // 调试用：--show-kill 强制显示「强制关闭」按钮（截图用）
            if (argv.Contains("--show-kill"))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (var b in new[] { ForceKillButton, DashKillButton, PaperKillButton, PixelKillButton })
                    {
                        if (b != null)
                        {
                            b.Visibility = Visibility.Visible;
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }

            // 调试用：--guide 直接打开开服教程（截图用）
            if (argv.Contains("--guide"))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var w = BuildServerGuideDialog();
                        w.Topmost = true;
                        w.Show();
                        w.Activate();
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.WriteAllText(@"D:\综合创作\_guideerr.txt", ex.ToString());
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }

            // 调试用：--msa-diag 直接打开微软登录诊断窗口（截图用）
            if (argv.Contains("--msa-diag"))
            {
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    new MsaDiagWindow(_settings.MicrosoftClientId) { Owner = this }.Show();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            // 调试用：--biome 0/1/2，直接切到某个群系（截图用）
            var biomeFlag = Array.IndexOf(argv, "--biome");
            if (biomeFlag >= 0 && biomeFlag + 1 < argv.Length &&
                int.TryParse(argv[biomeFlag + 1], out var biomeIndex))
            {
                _heroBiome = (HeroBiome)Math.Clamp(biomeIndex, 0, 2);
                ApplyHeroBiome();
            }

            // Windows 11 智能应用控制开着的话，未签名的 exe 会被拦，先提醒一次
            try
            {
                var sac = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\CI\Policy",
                    "VerifiedAndReputablePolicyState", 0);
                if (sac is int state && state == 1)
                {
                    ShowToast("智能应用控制开着", "未签名的 exe 可能被拦；设置里有处理办法", "⚠", 8000);
                }
            }
            catch
            {
                // 读不到就算了
            }
            // 调试用：--query "钠"，预填搜索词（验证中文搜索）
            var queryFlag = Array.IndexOf(argv, "--query");
            if (queryFlag >= 0 && queryFlag + 1 < argv.Length && QueryBox != null)
            {
                QueryBox.Text = argv[queryFlag + 1];
            }
            // 调试用：--kind 0..4，内容页预选分类（截图用）
            var kindFlag = Array.IndexOf(argv, "--kind");
            if (kindFlag >= 0 && kindFlag + 1 < argv.Length &&
                int.TryParse(argv[kindFlag + 1], out var kindIndex))
            {
                KindCombo.SelectedIndex = Math.Clamp(kindIndex, 0, 4);
            }
            // 调试用：--demo-detail，自动打开内容页第一个项目的详情（看加载器分类）
            _demoTranslate = argv.Contains("--demo-translate");
            if (argv.Contains("--demo-detail"))
            {
                _ = OpenFirstContentDetailAsync();
            }
        };
    }

    // ---------------- 导航 ----------------

    /// <summary>调试：把当前窗口内容渲染成 PNG（不含 WebView2 的网页内容），然后退出。</summary>
    private async Task CaptureAndExitAsync(string path, int extraWaitMs = 0)
    {
        try
        {
            await Task.Delay(3800 + extraWaitMs);

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
            PlayUiSound();
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

    private int _currentPage;

    private void SetPage(int index)
    {
        _currentPage = index;
        LaunchPage.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        InstallPage.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        ContentPage.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        OnlinePage.Visibility = index == 6 ? Visibility.Visible : Visibility.Collapsed;
        VersionsPage.Visibility = index == 7 ? Visibility.Visible : Visibility.Collapsed;
        AccountPage.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
        UpdatesPage.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
        FunPage.Visibility = index == 8 ? Visibility.Visible : Visibility.Collapsed;
        CreationPage.Visibility = index == 9 ? Visibility.Visible : Visibility.Collapsed;

        var active = (Brush)FindResource("TextMain");
        var inactive = (Brush)FindResource("TextDim");
        MarkNav(NavLaunch, index == 0, active, inactive);
        MarkNav(NavContent, index is 1 or 2, active, inactive);
        MarkNav(NavOnline, index == 6, active, inactive);
        MarkNav(NavVersions, index == 7, active, inactive);
        MarkNav(NavAccount, index == 3, active, inactive);
        MarkNav(NavFun, index == 8, active, inactive);
        MarkNav(NavCreation, index == 9, active, inactive);
        if (index == 4)
        {
            InitSettingsScene();
        }

        if (index == 9)
        {
            InitCreation();

            // 版本列表可能在窗口 Loaded 之后才扫完，延后补一次
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var list = RefreshCreationVersions();
                if (list.Count > 0 && PackVersionCombo.SelectedItem == null)
                {
                    PackVersionCombo.SelectedIndex = 0;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        MarkNav(NavSettings, index is 4 or 5, active, inactive);

        // 换页动画（仅新版布局）：淡入 + 轻微左滑
        FrameworkElement? target = index switch
        {
            0 => LaunchPage,
            1 => InstallPage,
            2 => ContentPage,
            3 => AccountPage,
            4 => SettingsPage,
            5 => UpdatesPage,
            6 => OnlinePage,
            7 => VersionsPage,
            8 => FunPage,
            9 => CreationPage,
            _ => null,
        };
        AnimatePageIn(target);
        AttachMotionHooks(target);

        if (index == 8 && BiliWeb != null)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_biliManualZoom && BiliWeb.ActualWidth > 100)
                {
                    ApplyBiliZoom(FitZoomFor(BiliWeb.ActualWidth), manual: false);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        UpdateDock(index);

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

                if (DashKillButton != null)
                {
                    DashKillButton.Visibility = Visibility.Visible;
                }

                SetStatus($"游戏已启动（pid {pid}），点「强制关闭游戏」可以强杀");
            });
            launcher.OutputReceived += line =>
                Dispatcher.BeginInvoke(() => SetStatus(Trim(line, 140)));

            var result = await Task.Run(() => launcher.LaunchAsync(plan));

            var killedByUser = _killedByUser;
            _killedByUser = false;

            // 每次启动都把日志落一份，玩家发过来就能查
            var logPath = SaveLaunchLog(result, launcher.RecentOutput, plan);

            if (!string.IsNullOrEmpty(result.CrashSummary))
            {
                SetStatus("崩溃：" + result.CrashSummary);
                if (result.Insight != null && !killedByUser)
                {
                    ShowCrashWindow(result.Insight);
                }
            }
            else if (!killedByUser && result.ExitCode == 0 && result.Duration.TotalSeconds < 3)
            {
                // 启动即退出：多半是 Java 不对 / 版本文件不全，给玩家一个能看懂的结论
                var insight = CrashInspector.Analyze(
                    plan.WorkingDirectory, 1, launcher.RecentOutput, DateTimeOffset.Now.AddSeconds(-result.Duration.TotalSeconds));

                var quick = new CrashInsight
                {
                    Headline = "游戏刚启动就退出了（0 秒，还没到主界面）",
                    Verdict = insight.Causes.Count > 0
                        ? "看起来是启动阶段就断了，不是进游戏后崩的。"
                        : "进程启动后立刻正常退出，没有留下报错。常见是 Java 版本不对、版本文件没下全，或者被安全软件拦了。",
                    Category = insight.Category == "未知" ? "启动" : insight.Category,
                    Confident = false,
                    Causes = insight.Causes,
                    Suggestions = insight.Suggestions.Count > 0
                        ? insight.Suggestions
                        : new[]
                        {
                            "先看「设置 → 启动游戏用的 Java」，换一个和版本匹配的 Java 再试。",
                            "在「内容 → 安装版本」里对同一个版本重装一次，补齐文件。",
                            "把这份报告导出来发给我，日志里有 JVM 的原始输出。",
                        },
                    ExitCode = result.ExitCode,
                    ReportText = insight.ReportText + "\n\n启动器日志：" + logPath,
                    GameCrashReportPath = insight.GameCrashReportPath,
                };

                SetStatus("游戏刚启动就退出了（0s），已生成诊断报告");
                ShowCrashWindow(quick);
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

    /// <summary>打开宣传站（GitHub Pages）。</summary>
    /// <summary>账号页的「微软登录诊断」：四步跑一遍，第 4 步就是 Mojang 白名单检查点。</summary>
    private void OnMsaDiagnostics(object sender, RoutedEventArgs e)
    {
        try
        {
            var clientId = string.IsNullOrWhiteSpace(_settings.MicrosoftClientId)
                ? "52fb2aba-9fa1-4605-be0d-431776c0880d"
                : _settings.MicrosoftClientId;

            new MsaDiagWindow(clientId) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            SetStatus("诊断窗口打不开: " + ex.Message);
        }
    }
    private void OnOpenWebsite(object sender, RoutedEventArgs e) => OpenUrl("https://ostonefisho.github.io/STMEMELauncher/", "宣传站");

    private void OnOpenGitHub(object sender, RoutedEventArgs e) => OpenUrl("https://github.com/OStonefishO/STMEMELauncher", "GitHub 项目");

    private void OnOpenGitee(object sender, RoutedEventArgs e) => OpenUrl("https://gitee.com/stonefish8/STMEMELauncher", "Gitee 镜像");

    private void OnOpenAfdian(object sender, RoutedEventArgs e) => OpenUrl("https://afdian.com/a/8STONEFISH8", "爱发电");

    private void OpenUrl(string url, string name)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            SetStatus("已在浏览器打开" + name);
        }
        catch (Exception ex)
        {
            SetStatus("打不开浏览器: " + ex.Message);
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

    private void OnSnapshotToggle(object sender, RoutedEventArgs e)
    {
        _ = LoadMcVersionsAsync();
    }

    private async Task LoadMcVersionsAsync()
    {
        try
        {
            InstallText.Text = "正在获取 Minecraft 版本清单…";
            var installer = _service.CreateInstaller();
            var all = await installer.GetManifestAsync();
            var includeSnapshots = SnapshotToggle?.IsChecked == true;
            _mcVersions = includeSnapshots
                ? all.Where(v => v.Type is "release" or "snapshot" or "old_beta" or "old_alpha").Take(160).ToList()
                : all.Where(v => v.Type == "release").Take(80).ToList();
            McVersionList.ItemsSource = _mcVersions;
            if (_mcVersions.Count > 0)
            {
                McVersionList.SelectedIndex = 0;
            }

            InstallText.Text = includeSnapshots
                ? $"共 {all.Count} 个版本，已列出最近 {_mcVersions.Count} 个（含预览版）"
                : $"共 {all.Count} 个版本，已列出最近 {_mcVersions.Count} 个正式版";
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
        4 => ContentKind.World,
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

        SeedContentVersionFilter(versions);
        ContentLoaderCombo.SelectedIndex = 0;
        _ = ReloadCategoriesAsync();
    }

    private string? EffectiveVersion()
    {
        return _contentVersionMode switch
        {
            ContentVersionMode.All => null,
            ContentVersionMode.Specific => _contentVersionValue,
            _ => (VersionList.SelectedItem as VersionCard)?.Summary.ClientVersion,
        };
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
        await LoadContentPageAsync();
    }

    private async void OnSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await ReloadCategoriesAsync();
        await LoadContentPageAsync();
    }

    private async Task ReloadCategoriesAsync()
    {
        try
        {
            var source = SelectedSource();
            var tags = await source.GetCategoriesAsync(SelectedKind());
            var list = new List<ContentTag> { new() { Id = string.Empty, Name = "全部分类" } };
            foreach (var tag in tags)
            {
                // 分类也走中文词表（ContentTag.Name 是 init-only，只能新建一个）
                list.Add(new ContentTag { Id = tag.Id, Name = Aurora.Core.Net.Translator.Tag(tag.Name) });
            }
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

    /// <summary>站外搜索：把当前关键词带到对应站点（这些站没有公开 API，直接跳转最稳）。</summary>
    private void OnSearchSite(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string template } || string.IsNullOrEmpty(template))
        {
            return;
        }

        var query = QueryBox?.Text?.Trim() ?? string.Empty;
        var url = template.Replace("{q}", Uri.EscapeDataString(query));

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            ContentStatus.Text = query.Length == 0
                ? "已打开站点首页，输入关键词再搜一次更准"
                : "已在浏览器搜索：" + query;
        }
        catch (Exception ex)
        {
            ContentStatus.Text = "打不开浏览器：" + ex.Message;
        }
    }

    /// <summary>详情页打开后去 MC 百科查一下：查到就用它的中文名和中文简介，按钮也变成直达链接。</summary>
    private async Task ApplyMcmodToDetailAsync(string title)
    {
        if (DetailMcmodButton == null || string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        try
        {
            var entry = Aurora.Core.Net.McmodLookup.Cached(title)
                        ?? await Aurora.Core.Net.McmodLookup.FindAsync(title);

            if (entry == null || DetailMcmodButton == null)
            {
                return;
            }

            DetailMcmodButton.Tag = entry.Url;
            DetailMcmodButton.Content = "MC 百科：" + entry.Name;

            if (_settings.TranslateContent && !string.IsNullOrWhiteSpace(entry.Summary) &&
                !string.IsNullOrWhiteSpace(entry.Name) && entry.Name != title)
            {
                DetailTitle.Text = entry.Name + "  ·  " + title;
                DetailSummary.Text = entry.Summary;
            }
        }
        catch
        {
            // 查不到就保持原样
        }
    }
    private bool _demoTranslate;

    private bool _detailShowingZh;
    private string _detailOriginalTitle = string.Empty;
    private string _detailOriginalSummary = string.Empty;

    /// <summary>详情页「翻译为中文 / 显示原文」开关：优先用 MC 百科的中文，查不到再机翻。</summary>
    private void OnDetailTranslate(object sender, RoutedEventArgs e)
    {
        if (_detailCurrent == null || DetailTranslateButton == null)
        {
            return;
        }

        if (_detailShowingZh)
        {
            DetailTitle.Text = _detailOriginalTitle;
            DetailSummary.Text = _detailOriginalSummary;
            DetailTranslateButton.Content = "翻译为中文";
            _detailShowingZh = false;
            DetailStatus.Text = "已切回原文";
            return;
        }

        _ = TranslateDetailAsync(_detailCurrent.Title, _detailOriginalSummary);
    }

    private async Task TranslateDetailAsync(string title, string summary)
    {
        DetailTranslateButton.IsEnabled = false;
        DetailTranslateButton.Content = "翻译中…";
        DetailStatus.Text = "正在识别并翻译…";

        try
        {
            var entry = Aurora.Core.Net.McmodLookup.Cached(title)
                        ?? await Aurora.Core.Net.McmodLookup.FindAsync(title);

            if (entry != null && !string.IsNullOrWhiteSpace(entry.Summary))
            {
                DetailMcmodButton.Tag = entry.Url;
                DetailMcmodButton.Content = "MC 百科：" + entry.Name;
                DetailTitle.Text = entry.Name + "  ·  " + title;
                DetailSummary.Text = entry.Summary;
                DetailStatus.Text = "已用 MC 百科的中文资料";
            }
            else
            {
                var zh = await Aurora.Core.Net.Translator.TextAsync(summary);
                if (!string.IsNullOrWhiteSpace(zh) && zh != summary)
                {
                    DetailSummary.Text = zh;
                    DetailStatus.Text = "已机器翻译（MC 百科没收录这个模组）";
                }
                else
                {
                    DetailStatus.Text = "翻译失败：今天免费额度用完了，去设置里填自己的 Key 更稳";
                    DetailTranslateButton.Content = "翻译为中文";
                    DetailTranslateButton.IsEnabled = true;
                    return;
                }
            }

            DetailTranslateButton.Content = "显示原文";
            _detailShowingZh = true;
        }
        catch (Exception ex)
        {
            DetailStatus.Text = "翻译失败: " + ex.Message;
            DetailTranslateButton.Content = "翻译为中文";
        }
        finally
        {
            if (DetailTranslateButton != null)
            {
                DetailTranslateButton.IsEnabled = true;
            }
        }
    }
    /// <summary>模组详情里跳 MC 百科（中文资料最全，查前置/兼容性很方便）。</summary>
    private void OnOpenMcmodForDetail(object sender, RoutedEventArgs e)
    {
        var title = _detailCurrent?.Title ?? DetailTitle?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        try
        {
            var url = "https://search.mcmod.cn/s?key=" + Uri.EscapeDataString(title);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            DetailStatus.Text = "已在浏览器打开 MC 百科：" + title;
        }
        catch (Exception ex)
        {
            DetailStatus.Text = "打不开浏览器：" + ex.Message;
        }
    }

    private async Task LoadContentPageAsync()
    {
        var source = SelectedSource();

        // Modrinth 不收录地图：有 CurseForge key 就自动切过去，没有就说清楚怎么办
        if (SelectedKind() == ContentKind.World && SourceCombo.SelectedIndex == 0)
        {
            if (!string.IsNullOrWhiteSpace(_settings.CurseForgeApiKey))
            {
                SourceCombo.SelectedIndex = 1; // 触发 OnSourceChanged 重新加载
                return;
            }

            ContentList.ItemsSource = new List<ContentCard>();
            _contentTotal = 0;
            ContentStatus.Text = "Modrinth 没有地图。地图得走 CurseForge：设置里填免费 API Key（console.curseforge.com），或者点下面「Minecraft Maps」站外下载";
            UpdatePager();
            return;
        }

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
            var rawQuery = QueryBox.Text?.Trim() ?? string.Empty;

            // 中文搜索：先问 MC 百科「钠 → Sodium」，再用英文去搜 Modrinth / CurseForge
            if (rawQuery.Length > 0 && System.Text.RegularExpressions.Regex.IsMatch(rawQuery, "[\u4e00-\u9fa5]"))
            {
                try
                {
                    ContentStatus.Text = "识别中文名…";
                    var english = await Aurora.Core.Net.McmodLookup.ToEnglishAsync(rawQuery);
                    if (!string.IsNullOrWhiteSpace(english))
                    {
                        rawQuery = english;
                    }
                }
                catch
                {
                    // 识别失败就用原文搜
                }
            }

            var query = new ContentQuery
            {
                Kind = SelectedKind(),
                Query = rawQuery,
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

            // MC 百科有记录的，默认就显示中文（缓存命中立即换，没命中后台补）
            foreach (var card in cards)
            {
                var hit = Aurora.Core.Net.McmodLookup.Cached(card.Item.Title);
                if (hit != null)
                {
                    card.ApplyMcmod(hit);
                }
            }

            ContentList.ItemsSource = cards;
            UpdatePager();
            _ = FillMcmodNamesAsync(cards);

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

    /// <summary>列表里没有被百科缓存过的前 10 个，后台慢慢补中文名（不阻塞界面）。</summary>
    private async Task FillMcmodNamesAsync(List<ContentCard> cards)
    {
        foreach (var card in cards.Take(10))
        {
            if (Aurora.Core.Net.McmodLookup.Cached(card.Item.Title) != null)
            {
                continue;
            }

            try
            {
                var entry = await Aurora.Core.Net.McmodLookup.FindAsync(card.Item.Title);
                if (entry != null)
                {
                    card.ApplyMcmod(entry);
                }
            }
            catch
            {
                // 忽略
            }
        }
    }

    private async Task TranslateCardsAsync(List<ContentCard> cards)
    {
        // 先问 MC 百科（有中文名就直接用，比机翻准），没有再退回翻译接口
        var gate = new SemaphoreSlim(2);

        foreach (var card in cards.Take(12))
        {
            var title = card.Item.Title;
            var summary = card.Item.Summary;

            _ = Task.Run(async () =>
            {
                await gate.WaitAsync();
                try
                {
                    var entry = Aurora.Core.Net.McmodLookup.Cached(title)
                                ?? await Aurora.Core.Net.McmodLookup.FindAsync(title);

                    if (entry != null)
                    {
                        card.SetTitle(entry.Name);
                        if (!string.IsNullOrWhiteSpace(entry.Summary))
                        {
                            card.SetSummary(entry.Summary);
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(summary))
                    {
                        var zh = await Aurora.Core.Net.Translator.TextAsync(summary);
                        if (!string.IsNullOrWhiteSpace(zh) && zh != summary)
                        {
                            card.SetSummary(zh);
                        }
                    }
                }
                catch
                {
                    // 忽略
                }
                finally
                {
                    gate.Release();
                }
            });
        }

        await Task.Delay(200);
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

        try
        {
            ContentStatus.Text = "读取版本列表…";
            var mc = EffectiveVersion();
            var loader = EffectiveLoader();
            var source = card.Item.Source == "CurseForge" ? (IContentSource)_service.CreateCurseForge() : _service.CreateModrinth();
            var files = await source.GetFilesAsync(card.Item, mc, loader);

            var list = files.Select(f => new FileEntry(f)).ToList();
                if (list.Count > 0)
            {
                    }

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
        if (DashKillButton != null)
        {
            DashKillButton.Visibility = Visibility.Collapsed;
        }
    }

    // ---------------- 版本管理 ----------------

    private sealed class VmVersionItem
    {
        public string Id { get; init; } = string.Empty;
        public string RawId { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
    }

    private List<VmVersionItem> _vmAllItems = new();
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

        _vmAllItems = items;
        ApplyVmFilter(VmSearchBox?.Text ?? string.Empty, keepSelection: false);
    }

    /// <summary>版本列表按输入内容过滤（id / 加载器 / MC 版本都参与匹配）。</summary>
    private void ApplyVmFilter(string query, bool keepSelection = true)
    {
        if (VmVersionList == null)
        {
            return;
        }

        if (VmSearchHint != null)
        {
            VmSearchHint.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
        }

        var previous = (VmVersionList.SelectedItem as VmVersionItem)?.Id;
        var q = (query ?? string.Empty).Trim();

        var filtered = q.Length == 0
            ? _vmAllItems
            : _vmAllItems
                .Where(v => v.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || v.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();

        VmVersionList.ItemsSource = filtered;

        if (filtered.Count == 0)
        {
            return;
        }

        var target = keepSelection && previous != null
            ? filtered.FirstOrDefault(v => v.Id == previous) ?? filtered[0]
            : filtered[0];

        VmVersionList.SelectedItem = target;
    }

    private void OnVmSearchChanged(object sender, TextChangedEventArgs e)
        => ApplyVmFilter((sender as TextBox)?.Text ?? string.Empty);

    private void OnVmSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyVmFilter((sender as TextBox)?.Text ?? string.Empty, keepSelection: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (sender is TextBox box)
            {
                box.Text = string.Empty;
            }

            e.Handled = true;
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

    /// <summary>
    /// 导入存档：从别处复制来的存档目录或 zip，直接放进这个版本实际读的 saves。
    /// （版本隔离开着时，游戏读的是 versions\版本名\saves，不是根目录的 saves，所以不能只丢文件）
    /// </summary>
    private void OnVmImportSave(object sender, RoutedEventArgs e)
    {
        var gameDir = CurrentVmGameDir();
        if (gameDir == null)
        {
            VmStatus.Text = "先在左边选一个版本";
            return;
        }

        var saves = Path.Combine(gameDir, "saves");
        Directory.CreateDirectory(saves);

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择存档 zip（文件夹请用下面的按钮）",
            Filter = "存档压缩包|*.zip|所有文件|*.*",
        };

        // 先问要不要选文件夹，取消就当成选 zip
        var useFolder = MessageBox.Show(this,
            "要导入的是文件夹形式的存档吗？\n\n选「是」然后挑存档文件夹；选「否」去挑 zip 压缩包。",
            "导入存档", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        try
        {
            if (useFolder == MessageBoxResult.Yes)
            {
                using var picker = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选存档文件夹（里面应该有 level.dat）",
                    UseDescriptionForTitle = true,
                };

                if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                var source = picker.SelectedPath;
                if (!File.Exists(Path.Combine(source, "level.dat")))
                {
                    var nested = Directory.GetDirectories(source)
                        .Where(d => File.Exists(Path.Combine(d, "level.dat"))).ToList();
                    if (nested.Count == 0)
                    {
                        VmStatus.Text = "这个文件夹里没有 level.dat，不像存档（如果是多个存档，请选到具体某个存档）";
                        return;
                    }

                    var target = Path.Combine(saves, Path.GetFileName(source));
                    CopyDirectory(source, target);
                    FinishImport(target);
                    return;
                }

                var dest = Path.Combine(saves, Path.GetFileName(source.TrimEnd('\\')));
                CopyDirectory(source, dest);
                FinishImport(dest);
            }
            else if (useFolder != MessageBoxResult.Cancel && dialog.ShowDialog() == true)
            {
                var name = Path.GetFileNameWithoutExtension(dialog.FileName);
                var dest = Path.Combine(saves, name);
                if (Directory.Exists(dest))
                {
                    dest += "-" + DateTime.Now.ToString("HHmmss");
                }

                System.IO.Compression.ZipFile.ExtractToDirectory(dialog.FileName, dest);
                // zip 里裹了一层同名文件夹的话挪出来
                var inner = Path.Combine(dest, name);
                if (Directory.Exists(inner) && !File.Exists(Path.Combine(dest, "level.dat")))
                {
                    CopyDirectory(inner, Path.Combine(saves, name));
                    Directory.Delete(dest, true);
                    dest = Path.Combine(saves, name);
                }

                FinishImport(dest);
            }
        }
        catch (Exception ex)
        {
            VmStatus.Text = "导入存档失败: " + ex.Message;
        }

        void FinishImport(string target)
        {
            VmStatus.Text = "存档已放进 " + target;
            ShowToast("存档已导入", Path.GetFileName(target), "✓", 4200);
            RefreshModList(gameDir);

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{saves}\"") { UseShellExecute = true });
            }
            catch
            {
                // 忽略
            }
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
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

        if (VmDeleteButton != null)
        {
            VmDeleteButton.IsEnabled = true;
        }
    }

    /// <summary>删除版本：三次确认（讲清后果 → 手打版本名 → 最后确认），然后整个文件夹进回收站。</summary>
    private void OnVmDeleteVersion(object sender, RoutedEventArgs e)
    {
        if (_vmCurrent == null)
        {
            return;
        }

        var id = _vmCurrent.Id;
        var dir = _vmCurrent.Directory;

        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            VmStatus.Text = "找不到这个版本的目录（可能已经被删了）";
            ReloadVmVersions();
            return;
        }

        // 第 1 次：讲清楚会删掉什么
        var first = MessageBox.Show(this,
            $"要删除版本「{id}」吗？\n\n" +
            $"目录：{dir}\n\n" +
            "整个文件夹都会没：mods、saves（存档）、config、截图全在里面，\n" +
            "删掉后启动器里也不再列出这个版本。",
            "删除版本（1/3）", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (first != MessageBoxResult.Yes)
        {
            VmStatus.Text = "已取消删除";
            return;
        }

        // 第 2 次：手打版本名，防止连点
        var typed = PromptForText("确认删除（2/3）", $"把版本名原样输入一次才继续：{id}", string.Empty);
        if (string.IsNullOrWhiteSpace(typed) || !string.Equals(typed.Trim(), id, StringComparison.OrdinalIgnoreCase))
        {
            VmStatus.Text = "版本名对不上，已取消删除";
            return;
        }

        // 第 3 次：最后一道
        var last = MessageBox.Show(this,
            $"最后确认：删除「{id}」？\n\n版本会连同 mods 和存档一起进回收站。",
            "删除版本（3/3）", MessageBoxButton.YesNo, MessageBoxImage.Stop, MessageBoxResult.No);
        if (last != MessageBoxResult.Yes)
        {
            VmStatus.Text = "已取消删除";
            return;
        }

        try
        {
            MoveToRecycleBin(dir);

            if (string.Equals((VersionList.SelectedItem as VersionCard)?.Summary.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                VersionList.SelectedItem = null;
            }

            _vmCurrent = null;
            LoadVersions();
            ReloadVmVersions();

            VmTitle.Text = "先在左边选一个版本";
            VmPath.Text = string.Empty;
            VmModList.Children.Clear();
            if (VmRenameButton != null)
            {
                VmRenameButton.IsEnabled = false;
            }

            if (VmDeleteButton != null)
            {
                VmDeleteButton.IsEnabled = false;
            }

            VmStatus.Text = $"已删除 {id}（在回收站里，需要可以还原）";
            ShowToast("版本已删除", id + " · 已放进回收站", "✓", 5000);
        }
        catch (Exception ex)
        {
            VmStatus.Text = "删除失败: " + ex.Message;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

    private const uint FO_DELETE = 3;
    private const ushort FOF_ALLOWUNDO = 0x40;
    private const ushort FOF_NOCONFIRMATION = 0x10;
    private const ushort FOF_SILENT = 0x4;
    private const ushort FOF_NOERRORUI = 0x400;

    /// <summary>删到回收站，而不是直接抹掉 —— 手滑还能捞回来。</summary>
    private static void MoveToRecycleBin(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0",
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI),
        };

        var code = SHFileOperation(ref op);
        if (code != 0)
        {
            throw new IOException($"回收站操作失败（代码 {code}）");
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
                Background = Brushes.Transparent,
                BorderBrush = (Brush)FindResource("GlassLine"),
                BorderThickness = new Thickness(0, 0, 0, 1),
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

    /// <summary>删掉某一条下载任务（正在下的先取消，再把它从列表里移除）。</summary>
    private void OnRemoveDownload(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DownloadItem item })
        {
            return;
        }

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

            _installCts.Remove(item.Id);
        }

        _service.CreateDownloadManager().Remove(item);
        UpdateDownloadHint();
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
        DetailMcmodButton.Tag = null;
        DetailMcmodButton.Content = "MC 百科";
        _ = ApplyMcmodToDetailAsync(item.Title);
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
        var token = ++_depsToken;
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
            Tag = "deps",
            CornerRadius = new CornerRadius(12),
            Background = (Brush)FindResource("WarnBg"),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 8, 12),
        };

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = $"前置模组（{required.Count} 个，不装会启动失败）",
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("WarnInk"),
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

        if (token != _depsToken)
        {
            return;
        }

        // 双保险：先摘掉可能已经存在的旧卡片，再插新的
        foreach (var old in DetailBody.Children.OfType<Border>()
                     .Where(b => string.Equals(b.Tag as string, "deps", StringComparison.Ordinal))
                     .ToList())
        {
            DetailBody.Children.Remove(old);
        }

        card.Child = panel;
        DetailBody.Children.Insert(0, card);
    }

    private int expanderCount;

    /// <summary>每次画详情页 / 切加载器都自增；旧的「前置模组」任务回来后自己作废，避免叠两张卡。</summary>
    private int _depsToken;
    private readonly Stack<ContentItem> _detailStack = new();
    private List<ContentVersion> _detailVersions = new();
    private string _detailLoader = string.Empty;
    private GameLauncher? _gameLauncher;
    private bool _killedByUser;

    /// <summary>正在跑的安装任务（job id → 取消开关），支持同时装好几个版本。</summary>
    private readonly Dictionary<string, CancellationTokenSource> _installCts = new();

    /// <summary>把这次启动的完整日志写一份到 %APPDATA%\MEMELauncher\logs，方便玩家发过来排查。</summary>
    private static string SaveLaunchLog(LaunchResult result, IReadOnlyList<string> lines, LaunchPlan plan)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MEMELauncher", "logs");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, $"launch-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("MEME Launcher 启动日志");
            sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("版本：" + plan.MainClass + "  ·  " + plan.WorkingDirectory);
            sb.AppendLine("Java：" + plan.JavaPath);
            sb.AppendLine($"退出码：{result.ExitCode}  用时：{result.Duration.TotalSeconds:0.0}s");
            sb.AppendLine(new string('-', 60));
            foreach (var line in lines)
            {
                sb.AppendLine(line);
            }

            File.WriteAllText(file, sb.ToString(), new System.Text.UTF8Encoding(false));

            // 只留最近 20 份
            try
            {
                foreach (var old in Directory.GetFiles(dir, "launch-*.log")
                             .OrderByDescending(File.GetLastWriteTime).Skip(20))
                {
                    File.Delete(old);
                }
            }
            catch
            {
                // 忽略
            }

            return file;
        }
        catch
        {
            return "(日志写入失败)";
        }
    }

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
            Background = (Brush)FindResource("FieldBg"),
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

            // 整合包：不问目录，直接装成独立版本（缺的原版 / 加载器会自动补齐）
            if (item.Kind == ContentKind.Modpack)
            {
                DetailStatus.Text = "整合包：自动补齐原版和加载器，然后导入…";
                if (button != null)
                {
                    button.IsEnabled = false;
                }

                await InstallContentModpackAsync(file);

                if (button != null)
                {
                    button.IsEnabled = true;
                }

                return;
            }

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

    /// <summary>地图：下 zip，解到 saves\地图名，然后把目录打开给玩家看。</summary>
    private async Task InstallWorldAsync(ContentFile file, string gameDir)
    {
        var saves = Path.Combine(gameDir, "saves");
        Directory.CreateDirectory(saves);

        var name = System.IO.Path.GetFileNameWithoutExtension(
            string.IsNullOrWhiteSpace(file.DisplayName) ? file.FileName : file.DisplayName);
        foreach (var bad in System.IO.Path.GetInvalidFileNameChars())
        {
            name = name.Replace(bad, '_');
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "map-" + DateTime.Now.ToString("MMdd-HHmmss");
        }

        var target = Path.Combine(saves, name);
        var temp = Path.Combine(System.IO.Path.GetTempPath(), "meme-map-" + Guid.NewGuid().ToString("N") + ".zip");

        try
        {
            ContentStatus.Text = "正在下载地图 " + file.FileName + "…";
            await _service.SharedDownloader.DownloadFileAsync(file.Url, temp);

            if (Directory.Exists(target))
            {
                target += "-" + DateTime.Now.ToString("HHmmss");
            }

            var staging = temp + ".dir";
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(temp, staging);

            // 有些地图压缩包里还裹了一层同名文件夹，摊平
            var entries = Directory.GetFileSystemEntries(staging);
            var root = staging;
            if (entries.Length == 1 && Directory.Exists(entries[0]) &&
                !File.Exists(Path.Combine(staging, "level.dat")))
            {
                root = entries[0];
            }

            CopyDirectory(root, target);
            Directory.Delete(staging, true);

            ContentStatus.Text = "地图已装到 " + target;
            ShowToast("地图已安装", name, "✓", 4200);
            RefreshModList(gameDir);

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{saves}\"") { UseShellExecute = true });
            }
            catch
            {
                // 忽略
            }
        }
        catch (Exception ex)
        {
            ContentStatus.Text = "地图安装失败: " + ex.Message;
        }
        finally
        {
            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch
            {
                // 忽略
            }
        }
    }
    /// <summary>整合包：下载 → 自动补原版/加载器 → 解包成独立版本，一条龙，进度走下载管理。</summary>
    private async Task InstallContentModpackAsync(ContentFile file)
    {
        var manager = _service.CreateDownloadManager();
        var job = manager.EnqueueJob("整合包 " + file.FileName, "整合包");
        var cts = new CancellationTokenSource();
        _installCts[job.Id] = cts;
        CancelInstallButton.Visibility = Visibility.Visible;
        ContentStatus.Text = "正在下载整合包…";

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

            var installer = _service.CreateModpackInstaller();
            var plan = await installer.PrepareAsync(file, progress, cts.Token);

            // 缺原版 / 加载器就自动补，玩家不用自己去安装页折腾
            if (!string.IsNullOrEmpty(plan.Loader) &&
                string.IsNullOrEmpty(installer.FindLoader(plan.McVersion, plan.Loader, plan.LoaderVersion)))
            {
                await EnsureModpackBaseAsync(plan, progress, cts.Token, manager, job);
                plan.LoaderId = installer.FindLoader(plan.McVersion, plan.Loader, plan.LoaderVersion);
                LoadVersions();
            }

            var id = await installer.InstallPreparedAsync(plan, progress, cts.Token);
            manager.CompleteJob(job, true);
            LoadVersions();
            ReloadVmVersions();

            // 装完直接切到新版本，玩家点「开始游戏」就能玩，不用再手动导入
            SelectVersionById(id);
            ContentStatus.Text = "整合包已装好：" + id + "（已按设置做版本隔离）";
            ShowToast("整合包装好了", $"{id} · 直接点开始游戏", "✓", 6000);
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

    /// <summary>整合包要的原版 / 加载器没装时自动补上（先原版，再挑一个匹配的加载器版本）。</summary>
    private async Task EnsureModpackBaseAsync(ModpackPlan plan, IProgress<InstallProgress> progress,
        CancellationToken ct, DownloadManager manager, DownloadItem job)
    {
        var mcJson = Path.Combine(_settings.GameDirectory, "versions", plan.McVersion, plan.McVersion + ".json");
        if (!File.Exists(mcJson))
        {
            manager.UpdateJob(job, 2, "先补齐原版 " + plan.McVersion);
            await _service.CreateInstaller().InstallVanillaAsync(plan.McVersion, progress, downloadAssets: true, ct);
        }

        manager.UpdateJob(job, 6, $"安装 {plan.Loader} {plan.LoaderVersion}");
        var options = await _service.CreateLoaderInstaller().ListAsync(plan.Loader, plan.McVersion, ct);
        if (options.Count == 0)
        {
            throw new InvalidOperationException($"{plan.Loader} 没有适配 {plan.McVersion} 的版本");
        }

        var option = options.FirstOrDefault(o => !string.IsNullOrEmpty(plan.LoaderVersion) &&
                                                 (o.Version.Contains(plan.LoaderVersion!) ||
                                                  plan.LoaderVersion!.Contains(o.Version)))
                     ?? options[0];

        await _service.CreateLoaderInstaller().InstallAsync(option, progress, ct);
    }

    /// <summary>按版本 id 在启动页选中它（装完整合包用）。</summary>
    private void SelectVersionById(string id)
    {
        if (VersionList?.ItemsSource is not IEnumerable<VersionCard> cards)
        {
            return;
        }

        var card = cards.FirstOrDefault(c => string.Equals(c.Summary.Id, id, StringComparison.OrdinalIgnoreCase));
        if (card != null)
        {
            VersionList.SelectedItem = card;
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
            var release = await checker.GetLatestAsync(AppVersion);
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
                    var published = release.PublishedAt == default
                        ? string.Empty
                        : $" · 发布于 {release.PublishedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
                    UpdateStatus.Text = $"已是最新（当前 v{AppVersion} · 线上最新 {release.Tag}{published}）";
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

    /// <summary>横幅上的「立即更新」：一键下载 + 替换 + 重启，不用再点第二个对话框。</summary>
    private async void OnApplyUpdateNow(object sender, RoutedEventArgs e)
    {
        if (_pendingRelease == null || UpdateBannerButton == null)
        {
            return;
        }

        if (Aurora.Core.Net.SelfUpdater.PickAsset(_pendingRelease) == null)
        {
            UpdateBannerText.Text = "这个版本没附带 exe，请到下载页手动下";
            OpenBrowser(_pendingRelease.HtmlUrl);
            return;
        }

        UpdateBannerButton.IsEnabled = false;
        UpdateBannerButton.Content = "下载中…";

        try
        {
            var updater = _service.CreateSelfUpdater();
            var progress = new Progress<double>(p =>
            {
                if (UpdateBannerButton != null)
                {
                    UpdateBannerButton.Content = $"下载中 {p * 100:0}%";
                }
            });

            var file = await updater.DownloadAsync(_pendingRelease, progress, default,
                source => Dispatcher.BeginInvoke(() =>
                {
                    if (UpdateBannerButton != null) UpdateBannerButton.Content = "连接 " + source + "…";
                }));

            UpdateBannerText.Text = "下载完成，正在重启并替换…";
            Aurora.Core.Net.SelfUpdater.ApplyAndRestart(file);
            await Task.Delay(400);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            if (UpdateBannerText != null)
            {
                UpdateBannerText.Text = "更新失败：" + ex.Message + "（可以到设置里点「检查更新」走手动流程）";
            }

            if (UpdateBannerButton != null)
            {
                UpdateBannerButton.IsEnabled = true;
                UpdateBannerButton.Content = "重试更新";
            }
        }
    }
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
            Background = (Brush)FindResource("FieldBg"),
            Foreground = (Brush)FindResource("InkDim"),
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

    /// <summary>一个扁平、无边框、悬停变亮的按钮模板（给对话框的 ✕ 用）。</summary>
    private static ControlTemplate BuildFlatButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(FrameworkElement.NameProperty, "bd");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x24, 0x80, 0x80, 0x80)), "bd"));
        template.Triggers.Add(hover);
        return template;
    }

    private Window BuildServerGuideDialog()
    {
        var ink = (Brush)FindResource("Ink");
        var inkDim = (Brush)FindResource("InkDim");
        var inkFaint = (Brush)FindResource("InkFaint");
        var glass = (Brush)FindResource("Glass");
        var glassLine = (Brush)FindResource("GlassLine");
        var accent = (Brush)FindResource("AccentGrad");

        var body = new StackPanel { Margin = new Thickness(2, 0, 2, 4) };

        // 一节的卡片：左边序号徽标 + 标题 + 要点
        void Section(string index, string title, params string[] lines)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = (Brush)FindResource("FieldBg"),
                BorderBrush = glassLine,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 13, 16, 13),
                Margin = new Thickness(0, 0, 0, 10),
            };

            var inner = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            head.Children.Add(new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(7),
                Background = accent,
                Child = new TextBlock
                {
                    Text = index,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });
            head.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = ink,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(9, 0, 0, 0),
            });
            inner.Children.Add(head);

            foreach (var line in lines)
            {
                inner.Children.Add(new TextBlock
                {
                    Text = "·  " + line,
                    FontSize = 12,
                    Foreground = inkDim,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 4),
                    LineHeight = 19,
                });
            }

            card.Child = inner;
            body.Children.Add(card);
        }

        Section("1", "开服",
            "在「版本」里选一个已安装的版本 → 填端口（默认 25565）→ 填内存（2048 够 5 个人）",
            "需要正版服就勾「开启正版验证」；离线玩家一起玩就别勾",
            "勾选「我已阅读并同意 Minecraft EULA」，点「开服」",
            "看到控制台出现 Done (x.xxxs)! 就是起来了");

        Section("2", "让朋友进来（三选一）",
            "IPv6 直连：本机地址里有 IPv6 的话，直接发 [你的IPv6]:端口，最省事",
            "UPnP：点「UPnP 开端口」，成功会给你一个外网 IP:端口",
            "樱花穿透：natfrp.com 建 TCP 隧道指向 127.0.0.1:端口，填访问密钥和隧道 ID，点「启动隧道」");

        Section("3", "朋友怎么连",
            "游戏里「多人游戏」→「添加服务器」→ 把地址粘进去 → 加入",
            "局域网里的朋友也可以直接用 内网IP:端口");

        Section("!", "常见问题",
            "连不上：先看服务端控制台有没有 Connection 记录，没有就是网络没通（防火墙 / 端口 / 映射）",
            "Windows 防火墙：第一次开服会弹窗，记得点「允许访问」，专用和公用都勾上",
            "装了 mod 的服：朋友的 mod 必须和服务端一致，否则会报错退回去",
            "内存不够：看到 Java heap space 就把内存调大，但别超过物理内存的一半",
            "正版验证：离线账号进正版服会报 Invalid session，要么关掉正版验证，要么让朋友用正版");

        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 520,
            Margin = new Thickness(0, 4, 0, 0),
        };

        // 头部：标题 + 关闭
        var title = new TextBlock
        {
            Text = "开服教程",
            FontSize = 20,
            FontFamily = (FontFamily)FindResource("FontHead"),
            Foreground = ink,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var sub = new TextBlock
        {
            Text = "照着做，五分钟能让朋友进你的世界",
            FontSize = 11.5,
            Foreground = inkFaint,
            Margin = new Thickness(0, 5, 0, 0),
        };

        var closeGlyph = new Button
        {
            Content = "✕",
            Width = 32,
            Height = 28,
            FontSize = 12,
            Cursor = System.Windows.Input.Cursors.Hand,
            Foreground = inkDim,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Template = BuildFlatButtonTemplate(),
        };

        var headerGrid = new Grid();
        headerGrid.Children.Add(closeGlyph);
        var headerText = new StackPanel { Margin = new Thickness(0, 0, 34, 0) };
        headerText.Children.Add(title);
        headerText.Children.Add(sub);
        headerGrid.Children.Add(headerText);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = "右上角 ✕ 或点下面按钮关闭",
            FontSize = 10.5,
            Foreground = inkFaint,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var ok = new Button
        {
            Content = "知道了",
            Style = (Style)FindResource("HeroButton"),
            Width = 118,
            Height = 38,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(ok, 1);
        footer.Children.Add(ok);

        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
        root.Children.Add(headerGrid);
        root.Children.Add(scroll);
        root.Children.Add(footer);

        var shell = new Border
        {
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x0B, 0x10, 0x1C)),
            BorderBrush = glassLine,
            BorderThickness = new Thickness(1),
            Child = root,
        };
        shell.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 40,
            ShadowDepth = 0,
            Opacity = 0.6,
        };

        var window = new Window
        {
            Owner = this,
            Title = "开服教程",
            Width = 640,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 860,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            FontFamily = FontFamily,
            Content = new Border { Margin = new Thickness(14), Child = shell },
        };

        // 拖窗 + 关闭
        headerGrid.MouseLeftButtonDown += (s2, e2) =>
        {
            if (e2.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                window.DragMove();
            }
        };
        closeGlyph.Click += (_, _) => window.Close();
        ok.Click += (_, _) => window.Close();
        shell.MouseLeftButtonDown += (_, e2) => { if (e2.ClickCount == 1) window.Activate(); };

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

    private SettingsMob _settingsMob;
    private bool _settingsSceneReady;

    /// <summary>设置页右侧的小舞台：底座 + 方块生物，慢转。</summary>
    private void InitSettingsScene()
    {
        if (_settingsSceneReady || SettingsViewport == null)
        {
            return;
        }

        _settingsSceneReady = true;
        SettingsViewport.Camera = SettingsScene.Camera();
        ApplySettingsMob();
    }

    private void ApplySettingsMob()
    {
        try
        {
            SettingsViewport.Children.Clear();
            SettingsViewport.Children.Add(new ModelVisual3D { Content = SettingsScene.Lights() });
            SettingsViewport.Children.Add(SettingsScene.Build(_settingsMob));

            SettingsMobText.Text = SettingsScene.Name(_settingsMob);
            SettingsMobHint.Text = SettingsScene.Hint(_settingsMob);
        }
        catch (Exception ex)
        {
            SetStatus("3D 小场景初始化失败: " + ex.Message);
        }
    }

    private void OnSettingsSceneClick(object sender, MouseButtonEventArgs e)
    {
        _settingsMob = SettingsScene.Next(_settingsMob);
        ApplySettingsMob();
        ShowToast(SettingsScene.Name(_settingsMob), SettingsScene.Hint(_settingsMob), "✦", 2200);
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
        SettingsTranslateKey.Text = _settings.TranslateApiKey;
        SettingsTranslateBaseUrl.Text = _settings.TranslateBaseUrl;
        SettingsTranslateModel.Text = _settings.TranslateModel;
        Aurora.Core.Net.Translator.Configure(_settings.TranslateApiKey, _settings.TranslateBaseUrl, _settings.TranslateModel);
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
            Background = (Brush)FindResource("PopActive"),
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
        SettingsLaunchMode.SelectedIndex = _settings.LaunchMode switch
        {
            "fullscreen" => 2,
            "borderless" => 1,
            _ => 0,
        };

        SettingsIsolation.SelectedIndex = _settings.VersionIsolation?.ToLowerInvariant() switch
        {
            "off" => 2,
            "auto" => 1,
            _ => 0,
        };

        UpdateAccountChip();
        SettingsTranslateKey.Text = _settings.TranslateApiKey;
        SettingsTranslateBaseUrl.Text = _settings.TranslateBaseUrl;
        SettingsTranslateModel.Text = _settings.TranslateModel;
        Aurora.Core.Net.Translator.Configure(_settings.TranslateApiKey, _settings.TranslateBaseUrl, _settings.TranslateModel);
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
            _settings.TranslateApiKey = SettingsTranslateKey.Text.Trim();
            _settings.TranslateBaseUrl = string.IsNullOrWhiteSpace(SettingsTranslateBaseUrl.Text)
                ? "https://api.deepseek.com/v1" : SettingsTranslateBaseUrl.Text.Trim();
            _settings.TranslateModel = string.IsNullOrWhiteSpace(SettingsTranslateModel.Text)
                ? "deepseek-chat" : SettingsTranslateModel.Text.Trim();
            Aurora.Core.Net.Translator.Configure(_settings.TranslateApiKey, _settings.TranslateBaseUrl, _settings.TranslateModel);

            _settings.LaunchMode = SettingsLaunchMode.SelectedIndex switch
            {
                2 => "fullscreen",
                1 => "borderless",
                _ => "window",
            };

            if (_settings.LaunchMode == "borderless")
            {
                _settings.WindowWidth = (int)SystemParameters.PrimaryScreenWidth;
                _settings.WindowHeight = (int)SystemParameters.PrimaryScreenHeight;
            }

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
        SettingsTranslateKey.Text = _settings.TranslateApiKey;
        SettingsTranslateBaseUrl.Text = _settings.TranslateBaseUrl;
        SettingsTranslateModel.Text = _settings.TranslateModel;
        Aurora.Core.Net.Translator.Configure(_settings.TranslateApiKey, _settings.TranslateBaseUrl, _settings.TranslateModel);
            SaveStatus.Text = "已保存 · " + ConfigStore.ConfigPath;
            SetStatus("设置已保存");

            // 自建镜像填了就连一下：连不上立刻说清楚，别让玩家以为是网慢
            if (!string.IsNullOrWhiteSpace(_settings.MirrorBaseUrl))
            {
                _ = ProbeMirrorAfterSaveAsync(_settings.MirrorBaseUrl);
            }
        }
        catch (Exception ex)
        {
            SaveStatus.Text = "保存失败: " + ex.Message;
        }
    }

    private async Task ProbeMirrorAfterSaveAsync(string mirror)
    {
        try
        {
            var ok = await Aurora.Core.Net.DownloadClient.ProbeMirrorAsync(mirror);
            if (ok)
            {
                ShowToast("自建镜像可用", mirror, "✓", 4000);
                return;
            }

            SaveStatus.Text = $"自建镜像 {mirror} 连不上，下载会自动改用 BMCLAPI";
            SetStatus("自建镜像连不上，已自动改用 BMCLAPI");
            ShowToast("自建镜像连不上", "下载会自动改用 BMCLAPI", "⚠", 6000);
        }
        catch
        {
            // 探测失败不影响保存
        }
    }

    /// <summary>启动时看一眼自建镜像还在不在，挂了就提前说，别等下载卡住才发现。</summary>
    private async Task ProbeMirrorNoticeAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.MirrorBaseUrl))
        {
            return;
        }

        try
        {
            var ok = await Aurora.Core.Net.DownloadClient.ProbeMirrorAsync(_settings.MirrorBaseUrl);
            if (ok)
            {
                return;
            }

            ShowToast("自建镜像连不上", $"{_settings.MirrorBaseUrl} 没响应，下载已自动改用 BMCLAPI", "⚠", 8000);
        }
        catch
        {
            // 忽略
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
            Background = (Brush)FindResource("PopActive"),
            Padding = new Thickness(10, 4, 10, 4),
            Child = new TextBlock
            {
                Text = entry.Version,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("PopInk"),
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
                    Background = (Brush)FindResource("FieldBg"),
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

    // ---------------- 界面布局（新版 / 经典） ----------------

    private bool _vivid;
    private bool _terminal;
    private bool _paper;
    private bool _pixel;
    private bool _aurora;
    private string _style = "vivid";
    private bool _fullscreen;
    private Rect _fullscreenRestore = Rect.Empty;
    private WindowState _fullscreenRestoreState = WindowState.Normal;

    /// <summary>真正用代码换主题：新版大胆配色 + 动感过渡，经典回到原来的冷蓝。</summary>
    private void ApplyUiLayout(string layout, bool fullRefresh = true)
    {
        _style = (layout ?? "vivid").Trim().ToLowerInvariant();
        _terminal = _style == "terminal";
        _paper = _style == "paper";
        _pixel = _style == "pixel";
        _aurora = _style == "aurora";
        _vivid = _style != "classic";
        if (_vivid)
        {
            _style = _style switch
            {
                "terminal" => "terminal",
                "paper" => "paper",
                "pixel" => "pixel",
                "aurora" => "aurora",
                _ => "vivid",
            };
        }

        _hueShift = _settings.UiHue;
        _syncingHue = true;
        if (HueSlider != null)
        {
            HueSlider.Value = _hueShift;
        }

        if (HueText != null)
        {
            HueText.Text = _hueShift.Text();
        }

        _syncingHue = true;
        if (VolumeSlider != null)
        {
            VolumeSlider.Value = _settings.UiVolume;
        }

        if (VolumeText != null)
        {
            VolumeText.Text = _settings.UiVolume + "%";
        }

        _syncingHue = false;

        var res = Resources;   // 主题画刷定义在 Window.Resources 里
        void Set(string key, Color from, Color to, Color? mid = null)
            => res[key] = MakeGradient(from, to, mid);

        if (_paper)
        {
            // 纸质便签：米白纸面 + 深棕墨 + 陶土橙，浅色系
            Set("AppBg", Color.FromRgb(0xFA, 0xF4, 0xE8), Color.FromRgb(0xF3, 0xEA, 0xD9), Color.FromRgb(0xEE, 0xE2, 0xCC));
            Set("AccentGrad", Color.FromRgb(0xC4, 0x55, 0x3A), Color.FromRgb(0xE0, 0x9C, 0x3C));
            Set("TitleGrad", Color.FromRgb(0x2B, 0x26, 0x22), Color.FromRgb(0xC4, 0x55, 0x3A), Color.FromRgb(0x7A, 0x3B, 0x22));
            res["Glass"] = Frozen(Color.FromArgb(0xF2, 0xFD, 0xF9, 0xF0));
            res["GlassLine"] = Frozen(Color.FromArgb(0xFF, 0xDD, 0xCF, 0xB8));
            res["Ink"] = Frozen(Color.FromRgb(0x2B, 0x26, 0x22));
            res["InkDim"] = Frozen(Color.FromRgb(0x6E, 0x61, 0x52));
            res["InkFaint"] = Frozen(Color.FromRgb(0x9A, 0x8C, 0x7A));
            res["FontNum"] = new FontFamily("Georgia");
            res["FontHead"] = new FontFamily("Georgia");
            res["DockBg"] = Frozen(Color.FromArgb(0xF5, 0xFD, 0xF9, 0xF0));
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0xFD, 0xF9, 0xF0));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0xFF, 0xDD, 0xCF, 0xB8));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x22, 0xC4, 0x55, 0x3A));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x33, 0xC4, 0x55, 0x3A));
            res["FieldBg"] = Frozen(Color.FromArgb(0x10, 0x2B, 0x26, 0x22));
            res["FieldLine"] = Frozen(Color.FromArgb(0x30, 0x2B, 0x26, 0x22));
        }
        else if (_pixel)
        {
            // 像素 8bit：黑底 + NES 主色，纯色不渐变
            Set("AppBg", Color.FromRgb(0x08, 0x08, 0x10), Color.FromRgb(0x10, 0x10, 0x1C), Color.FromRgb(0x08, 0x08, 0x10));
            // 8bit：硬边两段色，不做平滑过渡
            res["AccentGrad"] = HardGradient(Color.FromRgb(0xF8, 0x38, 0x58), Color.FromRgb(0xFC, 0xA0, 0x44));
            res["TitleGrad"] = HardGradient(Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xF8, 0x38, 0x58));
            res["Glass"] = Frozen(Color.FromArgb(0xF0, 0x18, 0x18, 0x2C));
            res["GlassLine"] = Frozen(Color.FromRgb(0x58, 0xD8, 0x54));
            res["Ink"] = Frozen(Color.FromRgb(0xF8, 0xF8, 0xF8));
            res["InkDim"] = Frozen(Color.FromRgb(0x9C, 0x9C, 0xB8));
            res["InkFaint"] = Frozen(Color.FromRgb(0x60, 0x60, 0x80));
            res["FontNum"] = new FontFamily("Consolas");
            res["FontHead"] = new FontFamily("Consolas");
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x18, 0x18, 0x2C));
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x18, 0x18, 0x2C));
            res["ComboPopupLine"] = Frozen(Color.FromRgb(0x58, 0xD8, 0x54));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x30, 0xF8, 0x38, 0x58));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x50, 0x58, 0xD8, 0x54));
            res["FieldBg"] = Frozen(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
            res["FieldLine"] = Frozen(Color.FromArgb(0x66, 0x58, 0xD8, 0x54));
        }
        else if (_aurora)
        {
            // 毛玻璃极光：柔和蓝紫青渐变，低对比、大圆角感
            Set("AppBg", Color.FromRgb(0x0A, 0x14, 0x2E), Color.FromRgb(0x14, 0x22, 0x46), Color.FromRgb(0x1A, 0x2A, 0x52));
            Set("AccentGrad", Color.FromRgb(0x7F, 0xB8, 0xFF), Color.FromRgb(0xB6, 0x9C, 0xFF), Color.FromRgb(0x8A, 0xF0, 0xE0));
            Set("TitleGrad", Color.FromRgb(0xEA, 0xF3, 0xFF), Color.FromRgb(0xB6, 0x9C, 0xFF), Color.FromRgb(0x8A, 0xF0, 0xE0));
            res["Glass"] = Frozen(Color.FromArgb(0xB8, 0x1A, 0x2A, 0x4A));
            res["GlassLine"] = Frozen(Color.FromArgb(0x40, 0xB6, 0xC8, 0xFF));
            res["Ink"] = Frozen(Color.FromRgb(0xEC, 0xF3, 0xFF));
            res["InkDim"] = Frozen(Color.FromRgb(0xA8, 0xBB, 0xDC));
            res["InkFaint"] = Frozen(Color.FromRgb(0x74, 0x88, 0xAC));
            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["FontHead"] = new FontFamily("Microsoft YaHei UI Light");
            res["DockBg"] = Frozen(Color.FromArgb(0xC8, 0x1A, 0x2A, 0x4A));
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x1A, 0x2A, 0x4A));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0x60, 0xB6, 0xC8, 0xFF));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x2A, 0xB6, 0xC8, 0xFF));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x40, 0xB6, 0xC8, 0xFF));
        }
        else if (_terminal)
        {
            // 终端风：近黑的墨绿底 + 荧光绿主色，等宽感、无渐变堆叠
            Set("AppBg", Color.FromRgb(0x04, 0x07, 0x06), Color.FromRgb(0x06, 0x0B, 0x09), Color.FromRgb(0x08, 0x12, 0x0D));
            Set("AccentGrad", Color.FromRgb(0x35, 0xF0, 0x7A), Color.FromRgb(0x00, 0xC8, 0xA0), Color.FromRgb(0x9C, 0xFF, 0x6A));
            Set("TitleGrad", Color.FromRgb(0xE9, 0xFF, 0xF0), Color.FromRgb(0x35, 0xF0, 0x7A), Color.FromRgb(0x00, 0xC8, 0xA0));
            res["Glass"] = Frozen(Color.FromArgb(0xE6, 0x08, 0x0F, 0x0B));
            res["GlassLine"] = Frozen(Color.FromArgb(0x66, 0x35, 0xF0, 0x7A));
            res["Ink"] = Frozen(Color.FromRgb(0xC9, 0xF7, 0xD2));
            res["InkDim"] = Frozen(Color.FromRgb(0x77, 0xC2, 0x8C));
            res["InkFaint"] = Frozen(Color.FromRgb(0x41, 0x7A, 0x52));
            res["FontNum"] = new FontFamily("Consolas");   // 终端风：等宽数字
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x08, 0x0F, 0x0B));
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x08, 0x0F, 0x0B));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0x80, 0x35, 0xF0, 0x7A));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x2E, 0x35, 0xF0, 0x7A));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x44, 0x35, 0xF0, 0x7A));
        }
        else if (_vivid)
        {
            Set("AppBg", Color.FromRgb(0x07, 0x03, 0x10), Color.FromRgb(0x04, 0x06, 0x0C), Color.FromRgb(0x1A, 0x08, 0x33));
            Set("AccentGrad", Color.FromRgb(0xFF, 0x3D, 0x8B), Color.FromRgb(0x00, 0xE5, 0xFF), Color.FromRgb(0x7A, 0x3C, 0xFF));
            Set("TitleGrad", Colors.White, Color.FromRgb(0x00, 0xE5, 0xFF), Color.FromRgb(0xFF, 0x7A, 0xC8));
            res["Glass"] = Frozen(Color.FromArgb(0xD8, 0x14, 0x0A, 0x24));
            res["GlassLine"] = Frozen(Color.FromArgb(0x3C, 0xFF, 0x5C, 0xC8));
            res["Ink"] = Frozen(Color.FromRgb(0xF4, 0xF6, 0xFF));
            res["InkDim"] = Frozen(Color.FromRgb(0xAB, 0xAE, 0xCB));
            res["InkFaint"] = Frozen(Color.FromRgb(0x74, 0x77, 0x96));
            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x12, 0x0A, 0x20));
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x17, 0x0C, 0x28));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0x45, 0xFF, 0x5C, 0xC8));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x3A, 0xFF, 0x5C, 0xC8));
            res["FieldBg"] = Frozen(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
            res["FieldLine"] = Frozen(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));
        }
        else
        {
            Set("AppBg", Color.FromRgb(0x05, 0x07, 0x0D), Color.FromRgb(0x07, 0x0A, 0x12), Color.FromRgb(0x0A, 0x10, 0x20));
            Set("AccentGrad", Color.FromRgb(0x5B, 0x8C, 0xFF), Color.FromRgb(0x22, 0xD3, 0xEE), Color.FromRgb(0x7C, 0x5C, 0xFF));
            Set("TitleGrad", Colors.White, Color.FromRgb(0x7C, 0x5C, 0xFF), Color.FromRgb(0xB9, 0xC8, 0xFF));
            res["Glass"] = Frozen(Color.FromArgb(0xB3, 0x12, 0x16, 0x20));
            res["GlassLine"] = Frozen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
            res["Ink"] = Frozen(Color.FromRgb(0xE6, 0xEA, 0xF2));
            res["InkDim"] = Frozen(Color.FromRgb(0x99, 0xA2, 0xB5));
            res["InkFaint"] = Frozen(Color.FromRgb(0x6D, 0x75, 0x88));
            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["FontHead"] = new FontFamily("Microsoft YaHei UI Light");
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x12, 0x0A, 0x20));
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x14, 0x18, 0x22));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x33, 0x5B, 0x8C, 0xFF));
            res["FieldBg"] = Frozen(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
            res["FieldLine"] = Frozen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        }

        // 色相偏移：在基础配色上整体旋转
        if (_hueShift != 0)
        {
            HueShiftResources(_hueShift);
        }

        // 版本选择弹层：每套界面一套自己的配色
        ApplyPopTheme();
        RefreshVersionPickerTheme();
        RefreshContentVersionFilterTheme();

        // 同步下拉框选中项（0 新版 / 1 终端 / 2 经典）
        if (UiLayoutCombo != null)
        {
            UiLayoutCombo.SelectedIndex = _style switch
            {
                "terminal" => 1,
                "paper" => 2,
                "pixel" => 3,
                "aurora" => 4,
                "classic" => 5,
                _ => 0,
            };
        }

        if (!fullRefresh)
        {
            return;   // 拖色相滑块时只换颜色，避免整页重排导致闪烁
        }

        // 让当前页重新高亮一次（胶囊只在 vivid 下出现）
        ApplyNavigationStyle();
        SetPage(_currentPage);
        RefreshMotionLayer();
    }

    /// <summary>硬边渐变：同色重复两次，做出 8bit 的两段色阶。</summary>
    private static LinearGradientBrush HardGradient(Color a, Color b)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
        };
        brush.GradientStops.Add(new GradientStop(a, 0));
        brush.GradientStops.Add(new GradientStop(a, 0.5));
        brush.GradientStops.Add(new GradientStop(b, 0.5));
        brush.GradientStops.Add(new GradientStop(b, 1));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush MakeGradient(Color from, Color to, Color? mid)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
        };
        brush.GradientStops.Add(new GradientStop(from, 0));
        if (mid.HasValue)
        {
            brush.GradientStops.Add(new GradientStop(mid.Value, 0.55));
        }

        brush.GradientStops.Add(new GradientStop(to, 1));
        brush.Freeze();
        return brush;
    }

    private void OnUiVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_settings == null || _syncingHue)
        {
            return;
        }

        _settings.UiVolume = (int)Math.Round(e.NewValue);
        if (VolumeText != null)
        {
            VolumeText.Text = _settings.UiVolume + "%";
        }

        ConfigStore.Save(_settings);
        PlayUiSound();
    }

    private void OnUiLayoutChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || UiLayoutCombo?.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var layout = item.Tag as string ?? "vivid";
        _settings.UiLayout = layout;
        ApplyUiLayout(layout);
        ConfigStore.Save(_settings);
        ShowToast("界面布局", _vivid ? "新版：动感配色 + 过渡动画" : "经典：回到旧版冷蓝", "✦", 2000);
    }

    /// <summary>新版布局下给当前页做一次滑入 + 淡入，换页更有动感。</summary>
    private void AnimatePageIn(FrameworkElement page)
    {
        if (!_vivid || page == null)
        {
            return;
        }

        var offset = new TranslateTransform(-18, 0);
        page.RenderTransform = offset;
        page.Opacity = 0;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        var slide = new DoubleAnimation(-18, 0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        page.BeginAnimation(UIElement.OpacityProperty, fade);
        offset.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    /// <summary>导航高亮：新版是一枚渐变胶囊，经典是纯文字变色。</summary>
    private void MarkNav(Button button, bool active, Brush activeBrush, Brush inactiveBrush)
    {
        button.Foreground = active ? activeBrush : inactiveBrush;
        button.Background = active && _vivid
            ? (Brush)FindResource("AccentGrad")
            : Brushes.Transparent;
    }

    // ---------------- 窗口化全屏 ----------------

    private void OnToggleFullscreen(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _fullscreenRestoreState = WindowState;
            _fullscreenRestore = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : Rect.Empty;

            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
            _fullscreen = true;
            ShowToast("窗口化全屏", "再点一次或按 F11 退出", "⛶", 1800);
        }
        else
        {
            WindowState = WindowState.Normal;
            if (_fullscreenRestore != Rect.Empty)
            {
                Left = _fullscreenRestore.Left;
                Top = _fullscreenRestore.Top;
                Width = _fullscreenRestore.Width;
                Height = _fullscreenRestore.Height;
            }

            WindowState = _fullscreenRestoreState;
            _fullscreen = false;
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }
    /// <summary>标题栏右侧的小圆钮：一键在新版 / 经典之间切换。</summary>
    private void OnToggleLayout(object sender, RoutedEventArgs e)
    {
        var next = _style switch
        {
            "vivid" => "terminal",
            "terminal" => "paper",
            "paper" => "pixel",
            "pixel" => "aurora",
            "aurora" => "classic",
            _ => "vivid",
        };
        _settings.UiLayout = next;
        ApplyUiLayout(next);
        ConfigStore.Save(_settings);
        var label = _style switch
        {
            "terminal" => "终端（荧光绿）",
            "paper" => "纸质便签（浅色）",
            "pixel" => "像素 8bit",
            "aurora" => "毛玻璃极光",
            "classic" => "经典（旧版）",
            _ => "新版（动感）",
        };
        ShowToast("界面布局", label, "◐", 1600);
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

        RefreshContentVersionLabel();
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

        OpenVersionPicker(list);
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

    private HeroBiome _heroBiome;

    private void InitHero()
    {
        try
        {
            _heroBiome = (HeroBiome)Math.Clamp(_settings.HeroBiome, 0, 2);
            HeroViewport.Camera = HeroScene.Camera();
            ApplyHeroBiome();
        }
        catch (Exception ex)
        {
            SetStatus("3D 场景初始化失败: " + ex.Message);
        }
    }

    /// <summary>按当前群系重建方块岛（点一下轮换主世界 / 下界 / 末地）。</summary>
    private void ApplyHeroBiome()
    {
        try
        {
            HeroViewport.Children.Clear();
            HeroViewport.Children.Add(new ModelVisual3D { Content = HeroScene.Lights(_heroBiome) });
            HeroViewport.Children.Add(HeroScene.Build(_heroBiome));

            if (HeroBiomeText != null)
            {
                HeroBiomeText.Text = HeroScene.BiomeName(_heroBiome) + " · 点一下换群系";
            }

            if (HeroBiomeHint != null)
            {
                HeroBiomeHint.Text = HeroScene.BiomeHint(_heroBiome);
            }
        }
        catch (Exception ex)
        {
            SetStatus("切换群系失败: " + ex.Message);
        }
    }

    private void OnHeroClicked(object sender, MouseButtonEventArgs e)
    {
        _heroBiome = (HeroBiome)(((int)_heroBiome + 1) % 3);
        _settings.HeroBiome = (int)_heroBiome;
        ConfigStore.Save(_settings);
        ApplyHeroBiome();

        ShowToast(HeroScene.BiomeName(_heroBiome), HeroScene.BiomeHint(_heroBiome), "✦", 2600);
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
            _summary = item.Summary;
            Meta = $"{item.Source} · {item.DownloadText} 次下载" +
                   (item.Categories.Count > 0 ? " · " + Aurora.Core.Net.Translator.TagsText(item.Categories.Take(3)) : string.Empty);
            _ = LoadIconAsync();
        }

        public ContentItem Item { get; }

        public string Title => string.IsNullOrEmpty(_title) ? Item.Title : _title;

        private string _title = string.Empty;

        /// <summary>套用 MC 百科的中文名 + 中文简介。</summary>
        public void ApplyMcmod(Aurora.Core.Net.McmodEntry entry)
        {
            if (entry == null) return;
            SetTitle(entry.Name);
            if (!string.IsNullOrWhiteSpace(entry.Summary))
            {
                SetSummary(entry.Summary);
            }
        }

        public void SetTitle(string value)
        {
            if (string.IsNullOrEmpty(value) || value == _title) return;
            _title = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Title)));
        }

        private string _summary = string.Empty;
        public string Summary => _summary;
        public void SetSummary(string value)
        {
            if (string.IsNullOrEmpty(value) || value == _summary) return;
            _summary = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Summary)));
        }

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
        public override string ToString() => Label;
        public FileEntry(ContentFile file) => File = file;

        public ContentFile File { get; }

        public string Label => string.IsNullOrEmpty(File.DisplayName)
            ? File.FileName
            : $"{File.DisplayName} · {File.FileName}";
    }
}
