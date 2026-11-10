import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace("""        if (sender is Button button && button.Tag is string tag && int.TryParse(tag, out var index))
        {
            SetPage(index);
        }""", """        if (sender is Button button && button.Tag is string tag && int.TryParse(tag, out var index))
        {
            PlayUiSound();
            SetPage(index);
        }""", 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("nav click sound added")
