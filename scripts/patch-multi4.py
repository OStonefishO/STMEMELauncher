import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 纸张：去掉洋红，改成陶土→琥珀
s = s.replace('''            Set("AccentGrad", Color.FromRgb(0xC4, 0x55, 0x3A), Color.FromRgb(0xE0, 0x8A, 0x3C), Color.FromRgb(0xB4, 0x3B, 0x6B));
            Set("TitleGrad", Color.FromRgb(0x2B, 0x26, 0x22), Color.FromRgb(0x8A, 0x4B, 0x2F), Color.FromRgb(0xC4, 0x55, 0x3A));''',
'''            Set("AccentGrad", Color.FromRgb(0xC4, 0x55, 0x3A), Color.FromRgb(0xE0, 0x9C, 0x3C));
            Set("TitleGrad", Color.FromRgb(0x2B, 0x26, 0x22), Color.FromRgb(0xC4, 0x55, 0x3A), Color.FromRgb(0x7A, 0x3B, 0x22));''', 1)

# 像素：硬边渐变（8bit 阶梯）
s = s.replace('''            Set("AccentGrad", Color.FromRgb(0xF8, 0x38, 0x58), Color.FromRgb(0xFC, 0xA0, 0x44), Color.FromRgb(0x58, 0xD8, 0x54));
            Set("TitleGrad", Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xFC, 0xA0, 0x44), Color.FromRgb(0xF8, 0x38, 0x58));''',
'''            // 8bit：硬边两段色，不做平滑过渡
            res["AccentGrad"] = HardGradient(Color.FromRgb(0xF8, 0x38, 0x58), Color.FromRgb(0xFC, 0xA0, 0x44));
            res["TitleGrad"] = HardGradient(Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xF8, 0x38, 0x58));''', 1)

# 硬边渐变工厂
s = s.replace('''    private static SolidColorBrush Frozen(Color color)''','''    /// <summary>硬边渐变：同色重复两次，做出 8bit 的两段色阶。</summary>
    private static LinearGradientBrush HardGradient(Color a, Color b)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
        };
        brush.GradientStops.Add(new GradientStop(a, 0));
        brush.GradientStops.Add(new GradientStop(a, 0.5));
        brush.GradientStops.Add(new GradientStop(b, 0.5));
        brush.GradientStops.Add(new GradientStop(b, 1));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Frozen(Color color)''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("paper/pixel palettes tuned")
