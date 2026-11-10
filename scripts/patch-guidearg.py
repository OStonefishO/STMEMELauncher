import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
if "--guide" not in s:
    s = s.replace('''            // 调试用：--msa-diag 直接打开微软登录诊断窗口（截图用）''',
'''            // 调试用：--guide 直接打开开服教程（截图用）
            if (argv.Contains("--guide"))
            {
                Dispatcher.BeginInvoke(new Action(() => BuildServerGuideDialog().Show()), System.Windows.Threading.DispatcherPriority.Background);
            }

            // 调试用：--msa-diag 直接打开微软登录诊断窗口（截图用）''', 1)
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("guide arg added")
