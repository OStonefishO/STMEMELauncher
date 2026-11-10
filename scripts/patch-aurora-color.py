import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace("""        AuroraLayer.Visibility = _vivid ? Visibility.Visible : Visibility.Collapsed;
        if (!_vivid || _auroraRunning)""", """        AuroraLayer.Visibility = _vivid ? Visibility.Visible : Visibility.Collapsed;
        if (!_vivid)
        {
            return;
        }

        RecolorAurora();
        if (_auroraRunning)""", 1)

s = s.replace("""    /// <summary>缓慢漂移 + 呼吸，极光的感觉。</summary>""", """    /// <summary>极光颜色跟随主题色相。</summary>
    private void RecolorAurora()
    {
        var accent = GetAccentColor();
        PaintAurora(AuroraA, accent, 0x60);
        PaintAurora(AuroraB, ShiftHue(accent, 40), 0x4C);
        PaintAurora(AuroraC, ShiftHue(accent, -50), 0x3E);
    }

    private static void PaintAurora(Ellipse? shape, Color color, byte alpha)
    {
        if (shape == null)
        {
            return;
        }

        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, color.R, color.G, color.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        brush.Freeze();
        shape.Fill = brush;
    }

    /// <summary>缓慢漂移 + 呼吸，极光的感觉。</summary>""", 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("aurora recolors with accent")

# 去掉调试写文件
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s2 = io.open(p2, encoding="utf-8", newline="").read()
import re
s2 = re.sub(r"        try\r?\n        \{\r?\n            var dbg = .*?\r?\n        \}\r?\n        catch \{ \}\r?\n\r?\n", "", s2, flags=re.S)
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("debug removed:", "_huedebug" not in s2)
