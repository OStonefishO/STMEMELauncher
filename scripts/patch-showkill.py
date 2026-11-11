import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
if "--show-kill" not in s:
    s = s.replace('''            // 调试用：--guide 直接打开开服教程（截图用）''','''            // 调试用：--show-kill 强制显示「强制关闭」按钮（截图用）
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

            // 调试用：--guide 直接打开开服教程（截图用）''', 1)
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("show-kill arg added")
