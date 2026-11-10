import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
if '"--hue"' not in s:
    s = s.replace('''        var uiFlag = Array.IndexOf(Environment.GetCommandLineArgs(), "--ui");''',
'''        var hueFlag = Array.IndexOf(Environment.GetCommandLineArgs(), "--hue");
        if (hueFlag >= 0 && hueFlag + 1 < Environment.GetCommandLineArgs().Length &&
            int.TryParse(Environment.GetCommandLineArgs()[hueFlag + 1], out var hueValue))
        {
            _settings.UiHue = hueValue;
        }

        var uiFlag = Array.IndexOf(Environment.GetCommandLineArgs(), "--ui");''', 1)
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("hue arg added")
