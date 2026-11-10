import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace("        var res = Application.Current.Resources;", "        var res = Resources;   // 主题画刷定义在 Window.Resources 里")
io.open(p, "w", encoding="utf-8", newline="").write(s)

p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s2 = io.open(p2, encoding="utf-8", newline="").read()
s2 = s2.replace("        var res = Application.Current.Resources;", "        var res = Resources;")
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("resources target fixed")
