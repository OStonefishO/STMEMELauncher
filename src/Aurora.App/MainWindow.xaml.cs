using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using Aurora.Core;
using Aurora.Core.Auth;
using Aurora.Core.Configuration;
using Aurora.Core.Launch;
using Aurora.Core.Models;
using Aurora.Core.Net;

namespace Aurora.App;

public partial class MainWindow : Window
{
    private LauncherService _service;
    private readonly LauncherSettings _settings;
    private bool _busy;
    private List<RemoteVersion> _mcVersions = new();
    private ContentItem? _selectedContent;
    private CancellationTokenSource? _loginCts;
    private bool _updatesLoaded;

    public MainWindow()
    {
        InitializeComponent();

        // 高分屏（150%）下 760 DIP 可能超过工作区，按工作区收缩
        var area = SystemParameters.WorkArea;
        Width = Math.Min(1180, area.Width - 48);
        Height = Math.Min(760, area.Height - 48);

        _settings = ConfigStore.Load();
        _service = new LauncherService(_settings);

        InitHero();
        Loaded += (_, __) => InitParticles();

        LoadSettingsToUi();
        LoadVersions();
        SetPage(0);

        // 支持 --page 1/2/3 直接打开某一页（截图和调试用）
        var argv = Environment.GetCommandLineArgs();
        var flag = Array.IndexOf(argv, "--page");
        if (flag >= 0 && flag + 1 < argv.Length && int.TryParse(argv[flag + 1], out var pageIndex))
        {
            SetPage(Math.Clamp(pageIndex, 0, 4));
        }
        Loaded += (_, __) => ApplyDarkTitleBar();
    }

    // ---------------- 导航 ----------------

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag && int.TryParse(tag, out var index))
        {
            SetPage(index);
        }
    }

    private void SetPage(int index)
    {
        LaunchPage.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        InstallPage.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        ContentPage.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        UpdatesPage.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;

        var active = (Brush)FindResource("TextMain");
        var inactive = (Brush)FindResource("TextDim");
        NavLaunch.Foreground = index == 0 ? active : inactive;
        NavInstall.Foreground = index == 1 ? active : inactive;
        NavContent.Foreground = index == 2 ? active : inactive;
        NavSettings.Foreground = index == 3 ? active : inactive;
        NavUpdates.Foreground = index == 4 ? active : inactive;

        if (index == 1 && _mcVersions.Count == 0)
        {
            _ = LoadMcVersionsAsync();
        }

        if (index == 4 && !_updatesLoaded)
        {
            _ = LoadUpdatesAsync();
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
                VersionList.SelectedIndex = 0;
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
            : $"Java {java.Major} · {java.VersionText}\n{java.JavaPath}";

        var resolved = _service.Resolve(summary.Id);
        DetDir.Text = resolved == null ? "—" : _service.GameDirectoryFor(resolved);

        LaunchButton.IsEnabled = !_busy;
        StatusText.Text = java == null
            ? $"警告：没有找到 Java {summary.JavaMajor}"
            : $"{summary.Id} · 已识别 {all.Count} 个 Java";
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
            var plan = _service.BuildPlan(card.Summary.Id, out var resolved, out var problems);
            if (plan == null || resolved == null)
            {
                SetStatus("启动失败：" + string.Join("；", problems.Take(3)));
                return;
            }

            SetStatus($"{resolved.Id} · 运行中（{plan.JavaPath}）");

            var launcher = new GameLauncher();
            launcher.OutputReceived += line =>
                Dispatcher.BeginInvoke(() => SetStatus(Trim(line, 140)));

            var result = await Task.Run(() => launcher.LaunchAsync(plan));

            if (!string.IsNullOrEmpty(result.CrashSummary))
            {
                SetStatus("崩溃：" + result.CrashSummary);
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

    private void OnMcVersionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (McVersionList.SelectedItem is RemoteVersion v)
        {
            InstallMcText.Text = "Minecraft " + v.Id;
            InstallButton.IsEnabled = !_busy;
            InstallText.Text = "可以选加载器了";
        }
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

        try
        {
            LoaderHint.Text = $"正在拉取 {loader} 版本列表…";
            var list = await _service.CreateLoaderInstaller().ListAsync(loader, mc.Id);
            LoaderVersionCombo.ItemsSource = list;
            LoaderVersionCombo.IsEnabled = list.Count > 0;
            if (list.Count > 0)
            {
                LoaderVersionCombo.SelectedIndex = 0;
            }

            LoaderHint.Text = list.Count == 0
                ? $"{loader} 没有支持 {mc.Id} 的版本"
                : $"共 {list.Count} 个，最新 {list[0].Version}";
        }
        catch (Exception ex)
        {
            LoaderHint.Text = "拉取失败: " + ex.Message;
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
        if (_busy || McVersionList.SelectedItem is not RemoteVersion mc)
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

        _busy = true;
        InstallButton.IsEnabled = false;
        InstallBar.Value = 0;

        try
        {
            var progress = new Progress<InstallProgress>(p =>
            {
                InstallBar.Value = p.Percent;
                InstallText.Text = p.Total > 0
                    ? $"[{p.Stage}] {p.Message} · {p.Percent:0}%"
                    : $"[{p.Stage}] {p.Message}";
            });

            var installer = _service.CreateInstaller();
            var baseJson = Path.Combine(_settings.GameDirectory, "versions", mc.Id, mc.Id + ".json");
            if (!File.Exists(baseJson))
            {
                InstallText.Text = "先补齐原版 " + mc.Id + " …";
                await installer.InstallVanillaAsync(mc.Id, progress, downloadAssets: true);
            }

            if (option != null)
            {
                InstallText.Text = $"安装 {option.Loader} {option.Version} …";
                var id = await _service.CreateLoaderInstaller().InstallAsync(option, progress);
                InstallText.Text = "安装完成: " + id;
            }
            else
            {
                InstallText.Text = "原版安装完成: " + mc.Id;
            }

            LoadVersions();
            InstallBar.Value = 100;
        }
        catch (Exception ex)
        {
            InstallText.Text = "安装失败: " + ex.Message;
        }
        finally
        {
            _busy = false;
            InstallButton.IsEnabled = true;
        }
    }

    // ---------------- 内容页 ----------------

    private ContentKind SelectedKind() => KindCombo.SelectedIndex switch
    {
        1 => ContentKind.Modpack,
        2 => ContentKind.Shader,
        3 => ContentKind.ResourcePack,
        _ => ContentKind.Mod,
    };

    private IContentSource SelectedSource() =>
        SourceCombo.SelectedIndex == 1 ? _service.CreateCurseForge() : _service.CreateModrinth();

    private async void OnSearch(object sender, RoutedEventArgs e)
    {
        var kind = SelectedKind();
        var source = SelectedSource();
        var query = QueryBox.Text?.Trim() ?? string.Empty;

        if (source is CurseForgeClient && string.IsNullOrEmpty(query))
        {
            ContentStatus.Text = "CurseForge 不支持关键词搜索，请粘贴项目链接或 slug";
            return;
        }

        try
        {
            ContentStatus.Text = "搜索中…";
            ContentList.ItemsSource = null;
            FileCombo.ItemsSource = null;

            var mc = (VersionList.SelectedItem as VersionCard)?.Summary.ClientVersion;
            var loader = (VersionList.SelectedItem as VersionCard)?.Summary.Loader;
            var items = await source.SearchAsync(kind, query, mc, loader, 30);

            var cards = items.Select(i => new ContentCard(i)).ToList();
            ContentList.ItemsSource = cards;
            ContentStatus.Text = cards.Count == 0
                ? "没有结果（换个关键词，或确认当前版本/加载器）"
                : $"找到 {cards.Count} 个结果 · {source.Name}";
        }
        catch (Exception ex)
        {
            ContentStatus.Text = "搜索失败: " + ex.Message;
        }
    }

    private async void OnContentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ContentList.SelectedItem is not ContentCard card)
        {
            return;
        }

        _selectedContent = card.Item;
        ContentInstallButton.IsEnabled = false;
        FileCombo.ItemsSource = null;

        try
        {
            ContentStatus.Text = "读取版本列表…";
            var mc = (VersionList.SelectedItem as VersionCard)?.Summary.ClientVersion;
            var loader = (VersionList.SelectedItem as VersionCard)?.Summary.Loader;
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
                ? "这个版本没有可用文件"
                : $"{list.Count} 个文件 · 选中后点安装";
        }
        catch (Exception ex)
        {
            ContentStatus.Text = "读取文件失败: " + ex.Message;
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
        if (_busy || FileCombo.SelectedItem is not FileEntry entry || _selectedContent == null)
        {
            return;
        }

        var target = (VersionList.SelectedItem as VersionCard);
        var resolved = target == null ? null : _service.Resolve(target.Summary.Id);
        var gameDir = resolved == null ? _settings.GameDirectory : _service.GameDirectoryFor(resolved);

        _busy = true;
        ContentInstallButton.IsEnabled = false;

        try
        {
            var progress = new Progress<InstallProgress>(p => ContentStatus.Text = $" {p.Stage} · {p.Message}");
            var path = await _service.CreateContentInstaller()
                .InstallFileAsync(entry.File, _selectedContent.Kind, gameDir, progress);
            ContentStatus.Text = "已安装: " + path;
        }
        catch (Exception ex)
        {
            ContentStatus.Text = "安装失败: " + ex.Message;
        }
        finally
        {
            _busy = false;
            ContentInstallButton.IsEnabled = true;
        }
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
            _service.Account = session;
            UpdateAccountChip();
            AccountStatus.Text = $"已登录：{session.PlayerName}（{session.Uuid}）";
        }
        catch (Exception ex)
        {
            dialog.Close();
            AccountStatus.Text = "登录失败: " + ex.Message;
        }
    }

    private void OnLogout(object sender, RoutedEventArgs e)
    {
        _service.Account = null;
        UpdateAccountChip();
        AccountStatus.Text = "已退出，使用离线账号";
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
        var account = _service.Account;
        var isPremium = account is { IsValid: true };
        AccountChip.Text = isPremium ? "正版：" + account!.PlayerName : "离线：" + _settings.PlayerName;
        AccountStatus.Text = isPremium
            ? $"已登录微软账号：{account!.PlayerName}（{account.Uuid}）"
            : $"当前：离线账号 {_settings.PlayerName} · 想用正版皮肤就登录微软账号";
    }

    // ---------------- 设置 ----------------

    private void LoadSettingsToUi()
    {
        SettingsGameDir.Text = _settings.GameDirectory;
        SettingsPlayer.Text = _settings.PlayerName;
        SettingsMemory.Text = _settings.MaxMemoryMb.ToString();
        SettingsJava.Text = _settings.JavaPath;
        SettingsMirror.IsChecked = _settings.UseMirror;
        ClientIdBox.Text = _settings.MicrosoftClientId;
        SettingsMirrorUrl.Text = _settings.MirrorBaseUrl;
        SettingsIsolation.SelectedIndex = _settings.VersionIsolation?.ToLowerInvariant() switch
        {
            "on" => 1,
            "off" => 2,
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
            _settings.PlayerName = string.IsNullOrWhiteSpace(SettingsPlayer.Text) ? "Steve" : SettingsPlayer.Text.Trim();
            if (int.TryParse(SettingsMemory.Text, out var memory) && memory >= 512)
            {
                _settings.MaxMemoryMb = memory;
            }

            _settings.JavaPath = SettingsJava.Text.Trim();
            _settings.UseMirror = SettingsMirror.IsChecked == true;
            _settings.MicrosoftClientId = ClientIdBox.Text?.Trim() ?? string.Empty;
            _settings.MirrorBaseUrl = SettingsMirrorUrl.Text?.Trim() ?? string.Empty;
            _settings.VersionIsolation = SettingsIsolation.SelectedIndex switch
            {
                1 => "on",
                2 => "off",
                _ => "auto",
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

    private void OnCloseWindow(object sender, RoutedEventArgs e) => Close();

    /// <summary>版本轮播用滚轮横向滚动（横向滚动条已经藏起来了）。</summary>
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

    private sealed class ContentCard
    {
        public ContentCard(ContentItem item)
        {
            Item = item;
            Meta = $"{item.Source} · {item.DownloadText} 次下载" +
                   (item.Categories.Count > 0 ? " · " + string.Join(" / ", item.Categories.Take(3)) : string.Empty);
        }

        public ContentItem Item { get; }

        public string Title => Item.Title;

        public string Summary => Item.Summary;

        public string Meta { get; }
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
