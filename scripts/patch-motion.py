import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
if "AttachMotionHooks(target)" not in s:
    s = s.replace("        AnimatePageIn(target);", "        AnimatePageIn(target);\n        AttachMotionHooks(target);", 1)
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("motion hooks attached per page")
else:
    print("already")
