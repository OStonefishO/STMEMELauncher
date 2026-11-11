import io, re
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 音量事件改名，避免和娱乐页的 OnVolumeChanged 冲突
s = s.replace("private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)\n    {\n        if (_syncingHue)",
              "private void OnUiVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)\n    {\n        if (_syncingHue)", 1)

# 2) 删掉依赖 FileCombo / ContentInstallButton 的代码块
s = s.replace("        ContentInstallButton.IsEnabled = false;\n", "")
s = s.replace("        FileCombo.ItemsSource = null;\n", "")
s = s.replace("        FileCombo.ItemsSource = list;\n", "")
s = s.replace("        FileCombo.SelectedIndex = 0;\n", "")
s = s.replace("        ContentInstallButton.IsEnabled = list.Count > 0;\n", "")

# 3) OnFileSelected / OnInstallContent 整段移除
def drop_method(src, signature):
    i = src.find(signature)
    if i < 0:
        return src
    # 找到方法体结束（按大括号计数）
    brace = src.index("{", i)
    depth = 0
    j = brace
    while j < len(src):
        if src[j] == "{": depth += 1
        elif src[j] == "}":
            depth -= 1
            if depth == 0:
                j += 1
                break
        j += 1
    # 连同前后空行一起删
    start = src.rfind("\n", 0, i) + 1
    return src[:start] + src[j:].lstrip("\n")

s = drop_method(s, "private void OnFileSelected(object sender, SelectionChangedEventArgs e)")
s = drop_method(s, "private async void OnInstallContent(object sender, RoutedEventArgs e)")
io.open(p, "w", encoding="utf-8", newline="").write(s)

# 4) XAML 滑块事件改名
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s2 = io.open(p2, encoding="utf-8", newline="").read()
s2 = s2.replace('ValueChanged="OnVolumeChanged" IsSnapToTickEnabled="True" TickFrequency="5"',
                'ValueChanged="OnUiVolumeChanged" IsSnapToTickEnabled="True" TickFrequency="5"', 1)
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("volume handler renamed, install handlers dropped")
