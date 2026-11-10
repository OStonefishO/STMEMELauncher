import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

addition = '''
    // ---------------- 仪表盘上的 3D 方块岛 ----------------

    private bool _dashSceneReady;

    private void InitDashboardScene()
    {
        if (_dashSceneReady || DashViewport == null)
        {
            return;
        }

        _dashSceneReady = true;
        DashViewport.Camera = HeroScene.Camera();
        RefreshDashboardScene();
    }

    private void RefreshDashboardScene()
    {
        if (DashViewport == null)
        {
            return;
        }

        DashViewport.Children.Clear();
        DashViewport.Children.Add(new ModelVisual3D { Content = HeroScene.Lights(_heroBiome) });
        DashViewport.Children.Add(HeroScene.Build(_heroBiome));
        if (DashBiomeText != null)
        {
            DashBiomeText.Text = HeroScene.BiomeName(_heroBiome) + " · 点一下换群系";
        }
    }

    private void OnDashboardBiomeClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _heroBiome = _heroBiome switch
        {
            HeroBiome.Overworld => HeroBiome.Nether,
            HeroBiome.Nether => HeroBiome.End,
            _ => HeroBiome.Overworld,
        };

        RefreshDashboardScene();
        if (HeroViewport != null)
        {
            RefreshHeroScene();
        }

        ShowToast(HeroScene.BiomeName(_heroBiome), HeroScene.BiomeHint(_heroBiome), "✦", 2200);
    }
'''

anchor = "    // ---------------- 音效 ----------------"
if "InitDashboardScene" not in s:
    s = s.replace(anchor, addition + "\n" + anchor, 1)

# vivid 时初始化
s = s.replace("            SetPagesTopInset(72);\n            UpdateDock(_currentPage);",
              "            SetPagesTopInset(72);\n            InitDashboardScene();\n            UpdateDock(_currentPage);")
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("dashboard 3d wired")
