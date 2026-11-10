import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace('CornerRadius="22" Background="#CC0E0718"', 'CornerRadius="22" Background="#F0120A20"')
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("dock bg opaque")
