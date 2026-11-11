import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace('''        // 首页形态：仪表盘 / 活页清单 / 方块网格 / 经典大图
        HeroGrid.Visibility = Visibility.Collapsed;
        DashboardGrid.Visibility = Visibility.Collapsed;
        PaperHome.Visibility = Visibility.Collapsed;
        PixelHome.Visibility = Visibility.Collapsed;

        if (_paper)
        {
            PaperHome.Visibility = Visibility.Visible;
        }
        else if (_pixel)
        {
            PixelHome.Visibility = Visibility.Visible;
        }
        else if (_vivid)
        {
            DashboardGrid.Visibility = Visibility.Visible;
            InitDashboardScene();
        }
        else
        {
            HeroGrid.Visibility = Visibility.Visible;
        }''','''        // 首页形态：每套布局一个独立面板
        HeroGrid.Visibility = Visibility.Collapsed;
        DashboardGrid.Visibility = Visibility.Collapsed;
        PaperHome.Visibility = Visibility.Collapsed;
        PixelHome.Visibility = Visibility.Collapsed;
        VividHome.Visibility = Visibility.Collapsed;
        TerminalHome.Visibility = Visibility.Collapsed;
        AuroraHome.Visibility = Visibility.Collapsed;

        if (_paper)
        {
            PaperHome.Visibility = Visibility.Visible;
        }
        else if (_pixel)
        {
            PixelHome.Visibility = Visibility.Visible;
        }
        else if (_terminal)
        {
            TerminalHome.Visibility = Visibility.Visible;
        }
        else if (_aurora)
        {
            AuroraHome.Visibility = Visibility.Visible;
        }
        else if (_vivid)
        {
            VividHome.Visibility = Visibility.Visible;
            InitVividScene();
        }
        else
        {
            HeroGrid.Visibility = Visibility.Visible;
        }''', 1)

# 新版首页的 3D 岛
s = s.replace('''    private void InitAuroraBackdrop()''','''    private bool _vividSceneReady;

    private void InitVividScene()
    {
        if (_vividSceneReady || VividViewport == null)
        {
            return;
        }

        _vividSceneReady = true;
        var cam = HeroScene.Camera();
        cam.Width *= 1.2;
        VividViewport.Camera = cam;
        RefreshVividScene();
    }

    private void RefreshVividScene()
    {
        if (VividViewport == null)
        {
            return;
        }

        VividViewport.Children.Clear();
        VividViewport.Children.Add(new ModelVisual3D { Content = HeroScene.Lights(_heroBiome) });
        VividViewport.Children.Add(HeroScene.Build(_heroBiome));
        if (VividBiomeText != null)
        {
            VividBiomeText.Text = HeroScene.BiomeName(_heroBiome) + " · 点一下换群系";
        }
    }

    private void InitAuroraBackdrop()''', 1)

# 换群系时同步刷新新版首页
s = s.replace('''        RefreshDashboardScene();
        if (HeroViewport != null)
        {
            ApplyHeroBiome();
        }''','''        RefreshDashboardScene();
        RefreshVividScene();
        RefreshAuroraBackdrop();
        if (HeroViewport != null)
        {
            ApplyHeroBiome();
        }''', 1)

# 极光背景重建（换群系要重画）
s = s.replace('''    private void InitAuroraBackdrop()
    {
        if (AuroraBackdrop == null || AuroraBackdrop.Children.Count > 0)
        {
            return;
        }''','''    private void RefreshAuroraBackdrop()
    {
        if (AuroraBackdrop == null || AuroraBackdrop.Children.Count == 0)
        {
            return;
        }

        AuroraBackdrop.Children.Clear();
        InitAuroraBackdrop();
    }

    private void InitAuroraBackdrop()
    {
        if (AuroraBackdrop == null || AuroraBackdrop.Children.Count > 0)
        {
            return;
        }''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("home switching updated")
