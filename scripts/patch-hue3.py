import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# ApplyUiLayout 开头：从设置读取色相并同步滑块（带防递归）
s = s.replace("""    private void ApplyUiLayout(string layout)
    {
        _vivid = !string.Equals(layout, "classic", StringComparison.OrdinalIgnoreCase);""",
"""    private void ApplyUiLayout(string layout)
    {
        _vivid = !string.Equals(layout, "classic", StringComparison.OrdinalIgnoreCase);

        _hueShift = _settings.UiHue;
        _syncingHue = true;
        if (HueSlider != null)
        {
            HueSlider.Value = _hueShift;
        }

        if (HueText != null)
        {
            HueText.Text = _hueShift.Text();
        }

        _syncingHue = false;""", 1)

io.open(p, "w", encoding="utf-8", newline="").write(s)
print("apply-ui hue sync added")
