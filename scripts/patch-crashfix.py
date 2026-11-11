import io
# A. 修 flat 按钮模板的空 TargetName
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace('''        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));''','''        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(FrameworkElement.NameProperty, "bd");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));''', 1)
s = s.replace('''        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), ""));''',
'''        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), "bd"));''', 1)

# B. 音量回调空值保护（InitializeComponent 阶段就会触发）
s = s.replace('''    private void OnUiVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingHue)''','''    private void OnUiVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_settings == null || _syncingHue)''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)

# C. 配置损坏时备份 + 自动重建
p2 = r"D:\综合创作\MCLauncher\src\Aurora.Core\Configuration\LauncherSettings.cs"
s2 = io.open(p2, encoding="utf-8", newline="").read()
s2 = s2.replace('''        catch
        {
            // 配置损坏时退回默认值
        }''','''        catch
        {
            // 配置损坏：备份一份再重建，避免玩家丢设置后无从排查
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var backup = Path.Combine(RootDirectory, $"config.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                    File.Copy(ConfigPath, backup, overwrite: true);
                }
            }
            catch
            {
                // 备份失败也不影响启动
            }
        }''', 1)
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("fixes applied")
