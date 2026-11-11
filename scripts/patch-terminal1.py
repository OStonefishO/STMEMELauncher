import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 三种布局：vivid / terminal / classic
s = s.replace('''        _vivid = !string.Equals(layout, "classic", StringComparison.OrdinalIgnoreCase);
''', '''        _terminal = string.Equals(layout, "terminal", StringComparison.OrdinalIgnoreCase);
        _vivid = !string.Equals(layout, "classic", StringComparison.OrdinalIgnoreCase) && !_terminal;
''', 1)

# 2) terminal 调色板（插在 if (_vivid) 之前）
s = s.replace('''        if (_vivid)
        {
            Set("AppBg", Color.FromRgb(0x07, 0x03, 0x10)''', '''        if (_terminal)
        {
            // 终端风：近黑的墨绿底 + 荧光绿主色，等宽感、无渐变堆叠
            Set("AppBg", Color.FromRgb(0x04, 0x07, 0x06), Color.FromRgb(0x06, 0x0B, 0x09), Color.FromRgb(0x08, 0x12, 0x0D));
            Set("AccentGrad", Color.FromRgb(0x35, 0xF0, 0x7A), Color.FromRgb(0x00, 0xC8, 0xA0), Color.FromRgb(0x9C, 0xFF, 0x6A));
            Set("TitleGrad", Color.FromRgb(0xE9, 0xFF, 0xF0), Color.FromRgb(0x35, 0xF0, 0x7A), Color.FromRgb(0x00, 0xC8, 0xA0));
            res["Glass"] = Frozen(Color.FromArgb(0xE6, 0x08, 0x0F, 0x0B));
            res["GlassLine"] = Frozen(Color.FromArgb(0x66, 0x35, 0xF0, 0x7A));
            res["Ink"] = Frozen(Color.FromRgb(0xC9, 0xF7, 0xD2));
            res["InkDim"] = Frozen(Color.FromRgb(0x77, 0xC2, 0x8C));
            res["InkFaint"] = Frozen(Color.FromRgb(0x41, 0x7A, 0x52));
        }
        else if (_vivid)
        {
            Set("AppBg", Color.FromRgb(0x07, 0x03, 0x10)''', 1)

io.open(p, "w", encoding="utf-8", newline="").write(s)
print("terminal palette added")
