import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace("    private bool _vivid;", "    private bool _vivid;\n    private bool _terminal;", 1)

# 下拉框：三项
s = s.replace('''        // 同步下拉框选中项
        if (UiLayoutCombo != null)
        {
            UiLayoutCombo.SelectedIndex = _vivid ? 0 : 1;
        }''', '''        // 同步下拉框选中项（0 新版 / 1 终端 / 2 经典）
        if (UiLayoutCombo != null)
        {
            UiLayoutCombo.SelectedIndex = _terminal ? 1 : (_vivid ? 0 : 2);
        }''', 1)

# ◐ 按钮循环三种
s = s.replace('''        var next = _vivid ? "classic" : "vivid";''', '''        var next = _vivid ? "terminal" : (_terminal ? "classic" : "vivid");''', 1)
s = s.replace('''        ShowToast("界面布局", _vivid ? "新版（动感）" : "经典（旧版）", "◐", 1600);''',
'''        ShowToast("界面布局", _vivid ? "新版（动感）" : (_terminal ? "终端（荧光绿）" : "经典（旧版）"), "◐", 1600);''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)

# XAML 下拉项加 terminal
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s2 = io.open(p2, encoding="utf-8", newline="").read()
s2 = s2.replace('''                                            <ComboBoxItem Content="新版（动感 / 高对比）" Tag="vivid"/>
                                            <ComboBoxItem Content="经典（旧版布局）" Tag="classic"/>''',
'''                                            <ComboBoxItem Content="新版（动感 / 高对比）" Tag="vivid"/>
                                            <ComboBoxItem Content="终端（荧光绿 / 等宽感）" Tag="terminal"/>
                                            <ComboBoxItem Content="经典（旧版布局）" Tag="classic"/>''', 1)
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("three layouts wired")
