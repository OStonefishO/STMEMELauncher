import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# ApplyUiLayout 增加 fullRefresh 参数：滑块拖动时只换画刷，不重排页面/重启动效
s = s.replace("    private void ApplyUiLayout(string layout)\n    {",
              "    private void ApplyUiLayout(string layout, bool fullRefresh = true)\n    {", 1)

s = s.replace('''        // 让当前页重新高亮一次（胶囊只在 vivid 下出现）
        ApplyNavigationStyle();
        SetPage(_currentPage);
        RefreshMotionLayer();''','''        if (!fullRefresh)
        {
            return;   // 拖色相滑块时只换颜色，避免整页重排导致闪烁
        }

        // 让当前页重新高亮一次（胶囊只在 vivid 下出现）
        ApplyNavigationStyle();
        SetPage(_currentPage);
        RefreshMotionLayer();''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)

# OnHueChanged 改成节流预览
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s2 = io.open(p2, encoding="utf-8", newline="").read()
s2 = s2.replace('''        if (!IsLoaded || !_vivid)
        {
            return;
        }

        _settings.UiHue = _hueShift;
        ApplyUiLayout(_settings.UiLayout);
        ConfigStore.Save(_settings);''','''        if (!IsLoaded || _style == "classic")
        {
            return;
        }

        _settings.UiHue = _hueShift;

        // 节流：拖动时每 70ms 才重算一次画刷，松手后再完整刷新
        _hueTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(70),
        };
        _hueTimer.Stop();
        _hueTimer.Tick -= OnHueTimerTick;
        _hueTimer.Tick += OnHueTimerTick;
        _hueTimer.Start();''', 1)

s2 = s2.replace("    private bool _syncingHue;", '''    private bool _syncingHue;
    private System.Windows.Threading.DispatcherTimer? _hueTimer;

    private void OnHueTimerTick(object? sender, EventArgs e)
    {
        _hueTimer?.Stop();
        ApplyUiLayout(_settings.UiLayout, fullRefresh: false);
        ConfigStore.Save(_settings);
    }''', 1)
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("hue throttled")
