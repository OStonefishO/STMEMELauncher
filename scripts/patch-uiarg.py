import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
anchor = "        // 界面布局（新版 / 经典）：构造后立刻套用一次"
addition = """        // 调试用：--ui classic / vivid 强制指定布局（截图用）
        var uiFlag = Array.IndexOf(argv, "--ui");
        if (uiFlag >= 0 && uiFlag + 1 < argv.Length)
        {
            _settings.UiLayout = argv[uiFlag + 1];
        }

"""
if "--ui" not in s:
    s = s.replace(anchor, addition + anchor, 1)
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("ui arg added")
else:
    print("already")
