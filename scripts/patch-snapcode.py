import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace('''            _mcVersions = all.Where(v => v.Type == "release").Take(80).ToList();''','''            var includeSnapshots = SnapshotToggle?.IsChecked == true;
            _mcVersions = includeSnapshots
                ? all.Where(v => v.Type is "release" or "snapshot" or "old_beta" or "old_alpha").Take(160).ToList()
                : all.Where(v => v.Type == "release").Take(80).ToList();''', 1)

s = s.replace('''            InstallText.Text = $"共 {all.Count} 个版本，已列出最近 {_mcVersions.Count} 个正式版";''','''            InstallText.Text = includeSnapshots
                ? $"共 {all.Count} 个版本，已列出最近 {_mcVersions.Count} 个（含预览版）"
                : $"共 {all.Count} 个版本，已列出最近 {_mcVersions.Count} 个正式版";''', 1)

if "private void OnSnapshotToggle" not in s:
    s = s.replace('''    private async Task LoadMcVersionsAsync()''','''    private void OnSnapshotToggle(object sender, RoutedEventArgs e)
    {
        _ = LoadMcVersionsAsync();
    }

    private async Task LoadMcVersionsAsync()''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("snapshot loading wired")
