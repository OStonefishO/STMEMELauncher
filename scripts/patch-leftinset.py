import io
# 1) 纸质左栏重叠：给左侧导航布局的页面加左内边距
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace('''        // 顶部 → 内容留 72；底部 → 内容留 70；左侧 → 不留
        SetPagesTopInset(useTop ? 72 : 0);
        SetPagesBottomInset(useBottom ? 72 : 0);''','''        // 顶部 → 内容留 72；底部 → 内容留 72；左侧 → 内容右移 84，避免和竖栏重叠
        SetPagesTopInset(useTop ? 72 : 0);
        SetPagesBottomInset(useBottom ? 72 : 0);
        SetPagesLeftInset(useLeft ? 84 : 0);''', 1)

s = s.replace('''    /// <summary>底部状态条是浮层，给内容页让出空间。</summary>''','''    /// <summary>左侧竖栏占位：内容页整体右移，避免压在导航上。</summary>
    private void SetPagesLeftInset(double left)
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
            page.Margin = new Thickness(left, m.Top, m.Right, m.Bottom);
        }
    }

    /// <summary>底部状态条是浮层，给内容页让出空间。</summary>''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("left inset added")
