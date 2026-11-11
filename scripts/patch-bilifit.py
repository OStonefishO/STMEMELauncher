import io
# 1) 进娱乐页 / 换布局后重新算自适应缩放
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace('''        UpdateDock(_currentPage);''','''        UpdateDock(_currentPage);

        // 布局变化后重新算 B 站自适应缩放，避免被压成 86%
        if (BiliWeb != null)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_biliManualZoom && BiliWeb.ActualWidth > 100)
                {
                    ApplyBiliZoom(FitZoomFor(BiliWeb.ActualWidth), manual: false);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)

# 2) 进娱乐页时也重算
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s2 = io.open(p2, encoding="utf-8", newline="").read()
s2 = s2.replace('''        AnimatePageIn(target);
        AttachMotionHooks(target);''','''        AnimatePageIn(target);
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
        }''', 1)
io.open(p2, "w", encoding="utf-8", newline="").write(s2)

# 3) 缩放上限放宽到 1.4，宽屏能真正铺满
p3 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Entertainment.cs"
s3 = io.open(p3, encoding="utf-8", newline="").read()
s3 = s3.replace("return Math.Clamp(dipWidth / BiliTargetCssWidth, 0.5, 1.25);",
                "return Math.Clamp(dipWidth / BiliTargetCssWidth, 0.5, 1.45);", 1)
io.open(p3, "w", encoding="utf-8", newline="").write(s3)
print("bili refit wired")
