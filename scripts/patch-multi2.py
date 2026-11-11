import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace("    private bool _vivid;\n    private bool _terminal;",
              "    private bool _vivid;\n    private bool _terminal;\n    private bool _paper;\n    private bool _pixel;\n    private bool _aurora;\n    private string _style = \"vivid\";", 1)

# 下拉索引：0 vivid / 1 terminal / 2 paper / 3 pixel / 4 aurora / 5 classic
s = s.replace('''            UiLayoutCombo.SelectedIndex = _terminal ? 1 : (_vivid ? 0 : 2);''',
'''            UiLayoutCombo.SelectedIndex = _style switch
            {
                "terminal" => 1,
                "paper" => 2,
                "pixel" => 3,
                "aurora" => 4,
                "classic" => 5,
                _ => 0,
            };''', 1)

# ◐ 循环
s = s.replace('''        var next = _vivid ? "terminal" : (_terminal ? "classic" : "vivid");''',
'''        var next = _style switch
        {
            "vivid" => "terminal",
            "terminal" => "paper",
            "paper" => "pixel",
            "pixel" => "aurora",
            "aurora" => "classic",
            _ => "vivid",
        };''', 1)
s = s.replace('''        ShowToast("界面布局", _vivid ? "新版（动感）" : (_terminal ? "终端（荧光绿）" : "经典（旧版）"), "◐", 1600);''',
'''        var label = _style switch
        {
            "terminal" => "终端（荧光绿）",
            "paper" => "纸质便签（浅色）",
            "pixel" => "像素 8bit",
            "aurora" => "毛玻璃极光",
            "classic" => "经典（旧版）",
            _ => "新版（动感）",
        };
        ShowToast("界面布局", label, "◐", 1600);''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)

# XAML 下拉 6 项
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s2 = io.open(p2, encoding="utf-8", newline="").read()
s2 = s2.replace('''                                            <ComboBoxItem Content="终端（荧光绿 / 等宽感）" Tag="terminal"/>''',
'''                                            <ComboBoxItem Content="终端（荧光绿 / 等宽感）" Tag="terminal"/>
                                            <ComboBoxItem Content="纸质便签（浅色 / 衬线）" Tag="paper"/>
                                            <ComboBoxItem Content="像素 8bit（NES 配色）" Tag="pixel"/>
                                            <ComboBoxItem Content="毛玻璃极光（柔和渐变）" Tag="aurora"/>''', 1)
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("six layouts wired")
