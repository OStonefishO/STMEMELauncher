import io
# 1) XAML: OnOpenGameDir -> OnOpenFolder
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace('Click="OnOpenGameDir"', 'Click="OnOpenFolder"')
io.open(p, "w", encoding="utf-8", newline="").write(s)

# 2) Ui.cs: 去掉 Button.CornerRadius
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s2 = io.open(p2, encoding="utf-8", newline="").read()
s2 = s2.replace("            button.Background = on ? (Brush)FindResource(\"AccentGrad\") : Brushes.Transparent;\n            button.CornerRadius = new CornerRadius(16);",
                "            button.Background = on ? (Brush)FindResource(\"AccentGrad\") : Brushes.Transparent;")
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("fixed")
