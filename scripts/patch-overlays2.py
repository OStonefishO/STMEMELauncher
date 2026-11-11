import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 扫描线 + 字体 + 极光背景 一起在导航形态里处理
s = s.replace('''        // 首页形态：仪表盘 / 活页清单 / 方块网格 / 经典大图''','''        // 扫描线：像素与终端
        if (ScanlineOverlay != null)
        {
            ScanlineOverlay.Visibility = (_pixel || _terminal) ? Visibility.Visible : Visibility.Collapsed;
            ScanlineOverlay.Opacity = _terminal ? 0.10 : 0.18;
        }

        // 极光：整屏 3D 岛背景
        if (AuroraBackdrop != null)
        {
            AuroraBackdrop.Visibility = _aurora ? Visibility.Visible : Visibility.Collapsed;
        }

        // 首页形态：仪表盘 / 活页清单 / 方块网格 / 经典大图''', 1)

# dock 文本：终端加 > 前缀，像素加大写
s = s.replace('''            button.Content = stack;''','''            if (_terminal)
            {
                stack.Children.Clear();
                stack.Children.Add(new TextBlock
                {
                    Text = "> " + label,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = (Brush)FindResource("InkDim"),
                });
            }

            button.Content = stack;''', 1)

# 极光背景初始化
s = s.replace('''    private void InitDashboardScene()''','''    private void InitAuroraBackdrop()
    {
        if (AuroraBackdrop == null || AuroraBackdrop.Children.Count > 0)
        {
            return;
        }

        AuroraBackdrop.Children.Add(new ModelVisual3D { Content = HeroScene.Lights(_heroBiome) });
        AuroraBackdrop.Children.Add(HeroScene.Build(_heroBiome));
    }

    private void InitDashboardScene()''', 1)
s = s.replace('''        if (AuroraBackdrop != null)
        {
            AuroraBackdrop.Visibility = _aurora ? Visibility.Visible : Visibility.Collapsed;
        }''','''        if (AuroraBackdrop != null)
        {
            AuroraBackdrop.Visibility = _aurora ? Visibility.Visible : Visibility.Collapsed;
            if (_aurora)
            {
                InitAuroraBackdrop();
            }
        }''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("scanline/terminal/aurora wired")
