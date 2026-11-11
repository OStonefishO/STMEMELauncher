import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 保存完整列表
s = s.replace("    private VersionSummary? _vmCurrent;", "    private List<VmVersionItem> _vmAllItems = new();\n    private VersionSummary? _vmCurrent;", 1)

s = s.replace('''        VmVersionList.ItemsSource = items;
        if (items.Count > 0)
        {
            VmVersionList.SelectedIndex = 0;
        }''','''        _vmAllItems = items;
        ApplyVmFilter(VmSearchBox?.Text ?? string.Empty, keepSelection: false);''', 1)

# 2) 过滤 + 键盘
s = s.replace('''    private void RefreshVmDirs()''','''    /// <summary>版本列表按输入内容过滤（id / 加载器 / MC 版本都参与匹配）。</summary>
    private void ApplyVmFilter(string query, bool keepSelection = true)
    {
        if (VmVersionList == null)
        {
            return;
        }

        var previous = (VmVersionList.SelectedItem as VmVersionItem)?.Id;
        var q = (query ?? string.Empty).Trim();

        var filtered = q.Length == 0
            ? _vmAllItems
            : _vmAllItems
                .Where(v => v.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || v.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();

        VmVersionList.ItemsSource = filtered;

        if (filtered.Count == 0)
        {
            return;
        }

        var target = keepSelection && previous != null
            ? filtered.FirstOrDefault(v => v.Id == previous) ?? filtered[0]
            : filtered[0];

        VmVersionList.SelectedItem = target;
    }

    private void OnVmSearchChanged(object sender, TextChangedEventArgs e)
        => ApplyVmFilter((sender as TextBox)?.Text ?? string.Empty);

    private void OnVmSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyVmFilter((sender as TextBox)?.Text ?? string.Empty, keepSelection: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (sender is TextBox box)
            {
                box.Text = string.Empty;
            }

            e.Handled = true;
        }
    }

    private void RefreshVmDirs()''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("version filter wired")
