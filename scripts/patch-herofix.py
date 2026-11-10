import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace("""        if (HeroViewport != null)
        {
            RefreshHeroScene();
        }""", """        if (HeroViewport != null)
        {
            ApplyHeroBiome();
        }""")
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("fixed method name")
