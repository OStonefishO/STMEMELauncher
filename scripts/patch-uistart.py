import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace("""        ApplyUiLayout(layout);
        SaveSettings();""", """        ApplyUiLayout(layout);
        ConfigStore.Save(_settings);""")

anchor = "        _settings = ConfigStore.Load();"
if "ApplyUiLayout(_settings.UiLayout)" not in s:
    s = s.replace(anchor, anchor + """

        // 界面布局（新版 / 经典）：构造后立刻套用一次
        ApplyUiLayout(_settings.UiLayout);
        PreviewKeyDown += OnWindowKeyDown;""", 1)

io.open(p, "w", encoding="utf-8", newline="").write(s)
print("patched:", s.count("ApplyUiLayout(_settings.UiLayout)"))
