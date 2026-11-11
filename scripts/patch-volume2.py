import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 音量同步到滑块
s = s.replace('''        if (HueText != null)
        {
            HueText.Text = _hueShift.Text();
        }

        _syncingHue = false;''','''        if (HueText != null)
        {
            HueText.Text = _hueShift.Text();
        }

        _syncingHue = true;
        if (VolumeSlider != null)
        {
            VolumeSlider.Value = _settings.UiVolume;
        }

        if (VolumeText != null)
        {
            VolumeText.Text = _settings.UiVolume + "%";
        }

        _syncingHue = false;''', 1)

# 音量事件
s = s.replace('''    private void OnUiLayoutChanged(object sender, SelectionChangedEventArgs e)''','''    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingHue)
        {
            return;
        }

        _settings.UiVolume = (int)Math.Round(e.NewValue);
        if (VolumeText != null)
        {
            VolumeText.Text = _settings.UiVolume + "%";
        }

        ConfigStore.Save(_settings);
        PlayUiSound();
    }

    private void OnUiLayoutChanged(object sender, SelectionChangedEventArgs e)''', 1)

# 播放时应用音量 + 仪表盘按钮同步
s = s.replace('''            var player = _style switch''','''            var volume = Math.Clamp(_settings.UiVolume, 0, 100) / 100.0;
            if (volume <= 0.001)
            {
                return;
            }

            var player = _style switch''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)

# Ui.cs：播放前设 Volume；强制关闭按钮两处同步
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s2 = io.open(p2, encoding="utf-8", newline="").read()
s2 = s2.replace('''            if (strong)
            {
                player.Play();
            }
            else
            {
                player.Play();
            }''','''            player.Volume = (float)Math.Clamp(_settings.UiVolume / 100.0, 0, 1);
            player.Play();''', 1)
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("volume wired")
