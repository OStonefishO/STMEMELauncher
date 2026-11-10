import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace('''                        BuildServerGuideDialog().Show();''','''                        var w = BuildServerGuideDialog();
                        w.Topmost = true;
                        w.Show();
                        w.Activate();''')
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("guide debug topmost")
