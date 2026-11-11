import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 导航形态：终端也用顶部胶囊条
s = s.replace("        if (_vivid)\n        {\n            BuildDock();", "        if (_vivid || _terminal)\n        {\n            BuildDock();", 1)

# 2) dock 高亮：终端用扁平荧光块
s = s.replace('''        foreach (var (key, button) in _dockButtons)
        {
            var on = key == active;
            button.Background = on ? (Brush)FindResource("AccentGrad") : Brushes.Transparent;''',
'''        foreach (var (key, button) in _dockButtons)
        {
            var on = key == active;
            if (on)
            {
                button.Background = _terminal
                    ? new SolidColorBrush(Color.FromArgb(0x2E, 0x35, 0xF0, 0x7A))
                    : (Brush)FindResource("AccentGrad");
            }
            else
            {
                button.Background = Brushes.Transparent;
            }''', 1)

# 3) dock 文字颜色：终端全用 ink 系
s = s.replace('''                    text.Foreground = on ? Brushes.White : (Brush)FindResource("InkDim");''',
'''                    text.Foreground = on
                        ? (_terminal ? (Brush)FindResource("Ink") : Brushes.White)
                        : (Brush)FindResource("InkDim");''', 1)

# 4) 极光：终端不显示
s = s.replace("        AuroraLayer.Visibility = _vivid ? Visibility.Visible : Visibility.Collapsed;\n        if (!_vivid)",
              "        AuroraLayer.Visibility = _vivid ? Visibility.Visible : Visibility.Collapsed;\n        if (!_vivid)", 1)

# 5) 音效：终端用方波机械声
s = s.replace('''    private static System.Media.SoundPlayer? _clickPlayer;''',
'''    private static System.Media.SoundPlayer? _clickPlayer;
    private static System.Media.SoundPlayer? _beepPlayer;''', 1)

s = s.replace('''    private void PlayUiSound(bool strong = false)
    {
        if (!_vivid)
        {
            return;
        }

        try
        {
            var player = ClickPlayer();''',
'''    /// <summary>终端风的机械短促 beep（方波 980Hz，12ms）。</summary>
    private static System.Media.SoundPlayer BeepPlayer()
    {
        if (_beepPlayer != null)
        {
            return _beepPlayer;
        }

        const int rate = 44100;
        var samples = (int)(rate * 0.032);
        var data = new byte[44 + samples * 2];
        void Write(int offset, string ascii) => System.Text.Encoding.ASCII.GetBytes(ascii).CopyTo(data, offset);
        Write(0, "RIFF");
        BitConverter.GetBytes(36 + samples * 2).CopyTo(data, 4);
        Write(8, "WAVEfmt ");
        BitConverter.GetBytes(16).CopyTo(data, 16);
        BitConverter.GetBytes((short)1).CopyTo(data, 20);
        BitConverter.GetBytes((short)1).CopyTo(data, 22);
        BitConverter.GetBytes(rate).CopyTo(data, 24);
        BitConverter.GetBytes(rate * 2).CopyTo(data, 28);
        BitConverter.GetBytes((short)2).CopyTo(data, 32);
        BitConverter.GetBytes((short)16).CopyTo(data, 34);
        Write(36, "data");
        BitConverter.GetBytes(samples * 2).CopyTo(data, 40);

        for (var i = 0; i < samples; i++)
        {
            var t = (double)i / rate;
            var env = Math.Exp(-t * 220);
            var phase = (2 * Math.PI * 980 * t) % (2 * Math.PI);
            var square = phase < Math.PI ? 1.0 : -0.55;   // 略偏置的方波，听起来更"机"
            var value = (short)(square * env * 7000);
            BitConverter.GetBytes(value).CopyTo(data, 44 + i * 2);
        }

        var stream = new System.IO.MemoryStream(data, false);
        _beepPlayer = new System.Media.SoundPlayer(stream);
        _beepPlayer.Load();
        return _beepPlayer;
    }

    private void PlayUiSound(bool strong = false)
    {
        if (!_vivid && !_terminal)
        {
            return;
        }

        try
        {
            var player = _terminal ? BeepPlayer() : ClickPlayer();''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("terminal dock + sound wired")
