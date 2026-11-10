import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace("""            HeroGrid.Visibility = Visibility.Collapsed;
            DashboardGrid.Visibility = Visibility.Visible;
            UpdateDock(_currentPage);""", """            HeroGrid.Visibility = Visibility.Collapsed;
            DashboardGrid.Visibility = Visibility.Visible;
            SetPagesTopInset(72);
            UpdateDock(_currentPage);""")

s = s.replace("""            HeroGrid.Visibility = Visibility.Visible;
            DashboardGrid.Visibility = Visibility.Collapsed;""", """            HeroGrid.Visibility = Visibility.Visible;
            DashboardGrid.Visibility = Visibility.Collapsed;
            SetPagesTopInset(0);""")

s = s.replace("    // ---------------- 音效 ----------------", '''    /// <summary>顶部胶囊条是浮层，给内容页让出空间。</summary>
    private void SetPagesTopInset(double top)
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
            page.Margin = new Thickness(m.Left, top, m.Right, m.Bottom);
        }
    }

    // ---------------- 音效 ----------------''')

io.open(p, "w", encoding="utf-8", newline="").write(s)
print("page inset wired")
