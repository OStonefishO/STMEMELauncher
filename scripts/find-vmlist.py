import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")
i = next(k for k,l in enumerate(lines) if 'x:Name="VmVersionList"' in l)
print(repr(lines[i-4:i+1]))
