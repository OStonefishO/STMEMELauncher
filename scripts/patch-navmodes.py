import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 导航形态：顶部 / 左侧 / 底部
s = s.replace('''        if (_vivid || _terminal)
        {
            BuildDock();
            BottomDock.Visibility = Visibility.Visible;
            RailPanel.Visibility = Visibility.Collapsed;
            RailColumn.Width = new GridLength(0);
            HeroGrid.Visibility = Visibility.Collapsed;
            DashboardGrid.Visibility = Visibility.Visible;
            SetPagesTopInset(72);
            InitDashboardScene();
            UpdateDock(_currentPage);
        }
        else
        {
            BottomDock.Visibility = Visibility.Collapsed;
            RailPanel.Visibility = Visibility.Visible;
            RailColumn.Width = new GridLength(74);
            HeroGrid.Visibility = Visibility.Visible;
            DashboardGrid.Visibility = Visibility.Collapsed;
            SetPagesTopInset(0);
        }''','''        // 六套布局各自的导航位置：顶部胶囊 / 左侧竖栏 / 底部状态条
        var useTop = _vivid && !_paper;              // vivid / terminal / aurora
        var useBottom = _pixel;                      // 像素：底部状态条
        var useLeft = !useTop && !useBottom;         // classic / paper

        BuildDock();
        BottomDock.Visibility = (useTop || useBottom) ? Visibility.Visible : Visibility.Collapsed;
        RailPanel.Visibility = useLeft ? Visibility.Visible : Visibility.Collapsed;
        RailColumn.Width = new GridLength(useLeft ? 74 : 0);

        // 顶部 → 内容留 72；底部 → 内容留 70；左侧 → 不留
        SetPagesTopInset(useTop ? 72 : 0);
        SetPagesBottomInset(useBottom ? 72 : 0);

        if (useBottom)
        {
            BottomDock.VerticalAlignment = VerticalAlignment.Bottom;
            BottomDock.HorizontalAlignment = HorizontalAlignment.Center;
            BottomDock.Margin = new Thickness(0, 0, 0, 16);
        }
        else
        {
            BottomDock.VerticalAlignment = VerticalAlignment.Top;
            BottomDock.HorizontalAlignment = HorizontalAlignment.Center;
            BottomDock.Margin = new Thickness(0, 10, 0, 0);
        }

        // 首页形态：仪表盘 / 活页清单 / 方块网格 / 经典大图
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
        }

        UpdateDock(_currentPage);''', 1)

# 底部内边距方法
s = s.replace('''    /// <summary>顶部胶囊条是浮层，给内容页让出空间。</summary>''','''    /// <summary>底部状态条是浮层，给内容页让出空间。</summary>
    private void SetPagesBottomInset(double bottom)
    {
        foreach (var page in new FrameworkElement[]
                 {
                     LaunchPage, InstallPage, ContentPage, AccountPage, SettingsPage,
                     UpdatesPage, OnlinePage, VersionsPage, FunPage, CreationPage,
                 })
        {
            if (page == null)
            {
                continue;
            }

            var m = page.Margin;
            page.Margin = new Thickness(m.Left, m.Top, m.Right, bottom);
        }
    }

    /// <summary>顶部胶囊条是浮层，给内容页让出空间。</summary>''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("nav modes wired")
