import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace('''        var previous = (VmVersionList.SelectedItem as VmVersionItem)?.Id;
        var q = (query ?? string.Empty).Trim();''','''        if (VmSearchHint != null)
        {
            VmSearchHint.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
        }

        var previous = (VmVersionList.SelectedItem as VmVersionItem)?.Id;
        var q = (query ?? string.Empty).Trim();''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("hint visibility wired")
