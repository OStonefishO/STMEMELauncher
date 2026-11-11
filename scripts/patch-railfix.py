import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 侧栏悬停展开只在「经典」布局启用；纸质便签也是左侧栏，展开会撑到内容上导致闪烁
s = s.replace('''    private void SetRailExpanded(bool expanded)
    {
        if (!_vivid || RailColumn == null || RailPanel == null || _railExpanded == expanded)
        {
            return;
        }''','''    private void SetRailExpanded(bool expanded)
    {
        // 只有经典布局的左侧图标栏才做「悬停展开」；其它布局展开会压到内容上，引发来回闪
        if (_style != "classic" || RailColumn == null || RailPanel == null || _railExpanded == expanded)
        {
            return;
        }''', 1)

# 2) 进入左侧栏布局时，把可能残留的展开动画停掉并复位宽度
s = s.replace('''        RailPanel.Visibility = useLeft ? Visibility.Visible : Visibility.Collapsed;
        RailColumn.Width = new GridLength(useLeft ? 74 : 0);''','''        RailPanel.Visibility = useLeft ? Visibility.Visible : Visibility.Collapsed;
        RailColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
        RailColumn.Width = new GridLength(useLeft ? 74 : 0);
        _railExpanded = false;''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("rail expand limited to classic")
