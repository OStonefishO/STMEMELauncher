import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) ApplyNavigationStyle 同时切首页形态
s = s.replace("""            BuildDock();
            BottomDock.Visibility = Visibility.Visible;
            RailPanel.Visibility = Visibility.Collapsed;
            RailColumn.Width = new GridLength(0);
            UpdateDock(_currentPage);""", """            BuildDock();
            BottomDock.Visibility = Visibility.Visible;
            RailPanel.Visibility = Visibility.Collapsed;
            RailColumn.Width = new GridLength(0);
            HeroGrid.Visibility = Visibility.Collapsed;
            DashboardGrid.Visibility = Visibility.Visible;
            UpdateDock(_currentPage);""")

s = s.replace("""            BottomDock.Visibility = Visibility.Collapsed;
            RailPanel.Visibility = Visibility.Visible;
            RailColumn.Width = new GridLength(74);""", """            BottomDock.Visibility = Visibility.Collapsed;
            RailPanel.Visibility = Visibility.Visible;
            RailColumn.Width = new GridLength(74);
            HeroGrid.Visibility = Visibility.Visible;
            DashboardGrid.Visibility = Visibility.Collapsed;""")

# 2) 音效：合成一个短促的点击声，复用 SoundPlayer
s = s.replace("    // ---------------- 极光 ----------------", '''    // ---------------- 音效 ----------------

    private static System.Media.SoundPlayer? _clickPlayer;

    /// <summary>合成一段 40ms 的轻点击音（正弦 + 指数衰减），不依赖外部素材。</summary>
    private static System.Media.SoundPlayer ClickPlayer()
    {
        if (_clickPlayer != null)
        {
            return _clickPlayer;
        }

        const int rate = 44100;
        var samples = (int)(rate * 0.045);
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
            var env = Math.Exp(-t * 120);
            var freq = 1180 - t * 3200;
            var value = (short)(Math.Sin(2 * Math.PI * freq * t) * env * 9000);
            BitConverter.GetBytes(value).CopyTo(data, 44 + i * 2);
        }

        var stream = new System.IO.MemoryStream(data, false);
        _clickPlayer = new System.Media.SoundPlayer(stream);
        _clickPlayer.Load();
        return _clickPlayer;
    }

    private void PlayUiSound(bool strong = false)
    {
        if (!_vivid)
        {
            return;
        }

        try
        {
            var player = ClickPlayer();
            if (strong)
            {
                player.Play();
            }
            else
            {
                player.Play();
            }
        }
        catch
        {
            // 静音设备下忽略
        }
    }

    // ---------------- 极光 ----------------''')

# 3) dock 点击发声，页面切换发声
s = s.replace("""                if (s is Button b && b.Tag is int index)
                {
                    SetPage(index);
                    UpdateDock(index);
                }""", """                if (s is Button b && b.Tag is int index)
                {
                    PlayUiSound();
                    SetPage(index);
                    UpdateDock(index);
                }""")
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("dock sound + dashboard toggle wired")
