import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace('''                if (ForceKillButton != null)
                {
                    ForceKillButton.Visibility = Visibility.Visible;
                }''','''                if (ForceKillButton != null)
                {
                    ForceKillButton.Visibility = Visibility.Visible;
                }

                if (DashKillButton != null)
                {
                    DashKillButton.Visibility = Visibility.Visible;
                }''', 1)

s = s.replace('''        ForceKillButton.Visibility = Visibility.Collapsed;
    }

    // ---------------- 版本管理 ----------------''','''        ForceKillButton.Visibility = Visibility.Collapsed;
        if (DashKillButton != null)
        {
            DashKillButton.Visibility = Visibility.Collapsed;
        }
    }

    // ---------------- 版本管理 ----------------''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("dash kill visibility wired")
