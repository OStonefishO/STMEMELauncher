import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")
i = next(k for k,l in enumerate(lines) if 'Click="OnToggleFullscreen"' in l)
btn = '''                    <Button Style="{StaticResource ChromeButton}" Click="OnToggleLayout" Content="◐" FontSize="13"
                            ToolTip="切换新版 / 经典界面"/>'''.split("\n")
lines[i:i] = btn
io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))

import xml.etree.ElementTree as ET
ET.parse(p)

# 处理函数
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p2, encoding="utf-8", newline="").read()
if "OnToggleLayout" not in s:
    s = s.replace("    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;",
'''    /// <summary>标题栏右侧的小圆钮：一键在新版 / 经典之间切换。</summary>
    private void OnToggleLayout(object sender, RoutedEventArgs e)
    {
        var next = _vivid ? "classic" : "vivid";
        _settings.UiLayout = next;
        ApplyUiLayout(next);
        ConfigStore.Save(_settings);
        ShowToast("界面布局", _vivid ? "新版（动感）" : "经典（旧版）", "◐", 1600);
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;''', 1)
    io.open(p2, "w", encoding="utf-8", newline="").write(s)
    print("toggle button + handler added")
