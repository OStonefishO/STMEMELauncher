import io
# 1) 设置项加 UiHue
p = r"D:\综合创作\MCLauncher\src\Aurora.Core\Configuration\LauncherSettings.cs"
s = io.open(p, encoding="utf-8", newline="").read()
if "UiHue" not in s:
    s = s.replace('    public string UiLayout { get; set; } = "vivid";',
                  '    public string UiLayout { get; set; } = "vivid";\n\n    /// <summary>新版布局的主题色相偏移（-160..160）。</summary>\n    public int UiHue { get; set; }')
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("UiHue added")

# 2) ApplyUiLayout：应用色相 + 刷新动效层
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s2 = io.open(p2, encoding="utf-8", newline="").read()

anchor = "        // 同步下拉框选中项\n        if (UiLayoutCombo != null)"
if "// 色相偏移" not in s2:
    s2 = s2.replace(anchor, """        // 色相偏移：在基础配色上整体旋转
        if (_hueShift != 0)
        {
            HueShiftResources(_hueShift);
        }

        // 同步下拉框选中项
        if (UiLayoutCombo != null)""", 1)

anchor2 = "        // 让当前页重新高亮一次（胶囊只在 vivid 下出现）\n        SetPage(_currentPage);"
if "RefreshMotionLayer()" not in s2:
    s2 = s2.replace(anchor2, """        // 让当前页重新高亮一次（胶囊只在 vivid 下出现）
        SetPage(_currentPage);
        RefreshMotionLayer();""", 1)

io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("layout hook updated")
