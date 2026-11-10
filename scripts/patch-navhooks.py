import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# ApplyUiLayout 末尾调用导航形态切换
s = s.replace("        SetPage(_currentPage);\n        RefreshMotionLayer();",
              "        ApplyNavigationStyle();\n        SetPage(_currentPage);\n        RefreshMotionLayer();", 1)

# SetPage 里同步 dock 高亮 + 点击音
s = s.replace("        AnimatePageIn(target);\n        AttachMotionHooks(target);",
              "        AnimatePageIn(target);\n        AttachMotionHooks(target);\n        UpdateDock(index);", 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)

# OnNavClick 加音效
s2 = io.open(p, encoding="utf-8", newline="").read()
if "PlayUiSound();" not in s2.split("private void OnNavClick")[1][:400] if "private void OnNavClick" in s2 else True:
    pass
print("layout hooks updated")
