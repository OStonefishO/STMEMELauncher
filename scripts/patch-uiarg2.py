import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace("""        var uiFlag = Array.IndexOf(argv, "--ui");""", """        var uiFlag = Array.IndexOf(Environment.GetCommandLineArgs(), "--ui");""")
s = s.replace("""        if (uiFlag >= 0 && uiFlag + 1 < argv.Length)
        {
            _settings.UiLayout = argv[uiFlag + 1];
        }""", """        if (uiFlag >= 0 && uiFlag + 1 < Environment.GetCommandLineArgs().Length)
        {
            _settings.UiLayout = Environment.GetCommandLineArgs()[uiFlag + 1];
        }""")
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("fixed argv scope")
