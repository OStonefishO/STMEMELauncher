import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace("    private bool _uiHooksReady;", "    private bool _uiHooksReady;\n    private bool _syncingHue;")

s = s.replace("""    private void OnHueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _hueShift = (int)e.NewValue;""", """    private void OnHueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingHue)
        {
            return;
        }

        _hueShift = (int)e.NewValue;""")

addition = '''    /// <summary>把当前主题的渐变整体旋转色相，实现「粉 / 青 / 绿 / 橙」自由选。</summary>
    private void HueShiftResources(int degrees)
    {
        var res = Application.Current.Resources;
        foreach (var key in new[] { "AccentGrad", "TitleGrad" })
        {
            if (res[key] is not LinearGradientBrush source)
            {
                continue;
            }

            var shifted = new LinearGradientBrush
            {
                StartPoint = source.StartPoint,
                EndPoint = source.EndPoint,
            };
            foreach (var stop in source.GradientStops)
            {
                shifted.GradientStops.Add(new GradientStop(ShiftHue(stop.Color, degrees), stop.Offset));
            }

            shifted.Freeze();
            res[key] = shifted;
        }

        if (res["GlassLine"] is SolidColorBrush line)
        {
            var c = ShiftHue(line.Color, degrees);
            res["GlassLine"] = new SolidColorBrush(Color.FromArgb(line.Color.A, c.R, c.G, c.B));
        }
    }

    private Color GetAccentColor()'''

s = s.replace("    private Color GetAccentColor()", addition, 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("hue resources added")
