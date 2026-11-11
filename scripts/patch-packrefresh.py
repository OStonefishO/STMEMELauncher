import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Creation.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 重排版本列表时，优先保留「打包版本」下拉里当前选中的那个
s = s.replace('''        PackVersionCombo.ItemsSource = versions;

        var pick = _vmCurrent?.Id ?? versions.FirstOrDefault()?.Id;''','''        var previousPackId = (PackVersionCombo.SelectedItem as VersionSummary)?.Id;
        PackVersionCombo.ItemsSource = versions;

        var pick = previousPackId ?? _vmCurrent?.Id ?? versions.FirstOrDefault()?.Id;''', 1)

# 2) 刷新时按 id 重新选中，再按这个 id 读取候选内容
s = s.replace('''    private void OnPackRefresh(object sender, RoutedEventArgs e)
    {
        RefreshCreationVersions();
        if (PackVersionCombo.SelectedItem is VersionSummary version)
        {
            LoadPackCandidates(version.Id);
        }
    }''','''    private void OnPackRefresh(object sender, RoutedEventArgs e)
    {
        // 记住当前选的版本，刷新后按 id 找回来，避免被换成同名的另一个版本
        var selectedId = (PackVersionCombo.SelectedItem as VersionSummary)?.Id;
        var versions = RefreshCreationVersions();

        var target = selectedId == null
            ? null
            : versions.FirstOrDefault(v => v.Id == selectedId) ?? versions.FirstOrDefault();

        if (target != null)
        {
            PackVersionCombo.SelectedItem = target;
            LoadPackCandidates(target.Id);
            CreationStatus.Text = $"已重新扫描 {target.Id}：{_packRows.Count} 项内容";
        }
        else
        {
            CreationStatus.Text = "没有可用版本，先去「版本」页装一个";
        }
    }''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("pack refresh fixed")
