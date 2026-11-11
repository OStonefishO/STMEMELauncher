import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace('''        _terminal = string.Equals(layout, "terminal", StringComparison.OrdinalIgnoreCase);
        _vivid = !string.Equals(layout, "classic", StringComparison.OrdinalIgnoreCase) && !_terminal;''',
'''        _style = (layout ?? "vivid").Trim().ToLowerInvariant();
        _terminal = _style == "terminal";
        _paper = _style == "paper";
        _pixel = _style == "pixel";
        _aurora = _style == "aurora";
        _vivid = _style != "classic";
        if (_vivid)
        {
            _style = _style switch
            {
                "terminal" => "terminal",
                "paper" => "paper",
                "pixel" => "pixel",
                "aurora" => "aurora",
                _ => "vivid",
            };
        }''', 1)

palettes = '''        if (_paper)
        {
            // 纸质便签：米白纸面 + 深棕墨 + 陶土橙，浅色系
            Set("AppBg", Color.FromRgb(0xFA, 0xF4, 0xE8), Color.FromRgb(0xF3, 0xEA, 0xD9), Color.FromRgb(0xEE, 0xE2, 0xCC));
            Set("AccentGrad", Color.FromRgb(0xC4, 0x55, 0x3A), Color.FromRgb(0xE0, 0x8A, 0x3C), Color.FromRgb(0xB4, 0x3B, 0x6B));
            Set("TitleGrad", Color.FromRgb(0x2B, 0x26, 0x22), Color.FromRgb(0x8A, 0x4B, 0x2F), Color.FromRgb(0xC4, 0x55, 0x3A));
            res["Glass"] = Frozen(Color.FromArgb(0xF2, 0xFD, 0xF9, 0xF0));
            res["GlassLine"] = Frozen(Color.FromArgb(0xFF, 0xDD, 0xCF, 0xB8));
            res["Ink"] = Frozen(Color.FromRgb(0x2B, 0x26, 0x22));
            res["InkDim"] = Frozen(Color.FromRgb(0x6E, 0x61, 0x52));
            res["InkFaint"] = Frozen(Color.FromRgb(0x9A, 0x8C, 0x7A));
            res["FontNum"] = new FontFamily("Georgia");
            res["FontHead"] = new FontFamily("Georgia");
        }
        else if (_pixel)
        {
            // 像素 8bit：黑底 + NES 主色，纯色不渐变
            Set("AppBg", Color.FromRgb(0x08, 0x08, 0x10), Color.FromRgb(0x10, 0x10, 0x1C), Color.FromRgb(0x08, 0x08, 0x10));
            Set("AccentGrad", Color.FromRgb(0xF8, 0x38, 0x58), Color.FromRgb(0xFC, 0xA0, 0x44), Color.FromRgb(0x58, 0xD8, 0x54));
            Set("TitleGrad", Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xFC, 0xA0, 0x44), Color.FromRgb(0xF8, 0x38, 0x58));
            res["Glass"] = Frozen(Color.FromArgb(0xF0, 0x18, 0x18, 0x2C));
            res["GlassLine"] = Frozen(Color.FromRgb(0x58, 0xD8, 0x54));
            res["Ink"] = Frozen(Color.FromRgb(0xF8, 0xF8, 0xF8));
            res["InkDim"] = Frozen(Color.FromRgb(0x9C, 0x9C, 0xB8));
            res["InkFaint"] = Frozen(Color.FromRgb(0x60, 0x60, 0x80));
            res["FontNum"] = new FontFamily("Consolas");
            res["FontHead"] = new FontFamily("Consolas");
        }
        else if (_aurora)
        {
            // 毛玻璃极光：柔和蓝紫青渐变，低对比、大圆角感
            Set("AppBg", Color.FromRgb(0x0A, 0x14, 0x2E), Color.FromRgb(0x14, 0x22, 0x46), Color.FromRgb(0x1A, 0x2A, 0x52));
            Set("AccentGrad", Color.FromRgb(0x7F, 0xB8, 0xFF), Color.FromRgb(0xB6, 0x9C, 0xFF), Color.FromRgb(0x8A, 0xF0, 0xE0));
            Set("TitleGrad", Color.FromRgb(0xEA, 0xF3, 0xFF), Color.FromRgb(0xB6, 0x9C, 0xFF), Color.FromRgb(0x8A, 0xF0, 0xE0));
            res["Glass"] = Frozen(Color.FromArgb(0xB8, 0x1A, 0x2A, 0x4A));
            res["GlassLine"] = Frozen(Color.FromArgb(0x40, 0xB6, 0xC8, 0xFF));
            res["Ink"] = Frozen(Color.FromRgb(0xEC, 0xF3, 0xFF));
            res["InkDim"] = Frozen(Color.FromRgb(0xA8, 0xBB, 0xDC));
            res["InkFaint"] = Frozen(Color.FromRgb(0x74, 0x88, 0xAC));
            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["FontHead"] = new FontFamily("Microsoft YaHei UI Light");
        }
        else if (_terminal)'''

s = s.replace("        if (_terminal)", palettes, 1)

# classic 分支恢复字体
s = s.replace('''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
        }

        // 色相偏移''', '''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["FontHead"] = new FontFamily("Microsoft YaHei UI Light");
        }

        // 色相偏移''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("palettes added")
