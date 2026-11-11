import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# dock 高亮按风格
s = s.replace('''            if (on)
            {
                button.Background = _terminal
                    ? new SolidColorBrush(Color.FromArgb(0x2E, 0x35, 0xF0, 0x7A))
                    : (Brush)FindResource("AccentGrad");
            }''','''            if (on)
            {
                if (_terminal)
                {
                    button.Background = new SolidColorBrush(Color.FromArgb(0x2E, 0x35, 0xF0, 0x7A));
                }
                else if (_paper)
                {
                    button.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xC4, 0x55, 0x3A));
                }
                else if (_pixel)
                {
                    button.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xF8, 0x38, 0x58));
                }
                else if (_aurora)
                {
                    button.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xB6, 0xC8, 0xFF));
                }
                else
                {
                    button.Background = (Brush)FindResource("AccentGrad");
                }
            }''', 1)

# dock 文字颜色
s = s.replace('''                    text.Foreground = on
                        ? (_terminal ? (Brush)FindResource("Ink") : Brushes.White)
                        : (Brush)FindResource("InkDim");''','''                    text.Foreground = on
                        ? ((_terminal || _paper || _aurora) ? (Brush)FindResource("Ink") : Brushes.White)
                        : (Brush)FindResource("InkDim");''', 1)

# 音效：按风格选播放器
s = s.replace('''            var player = _terminal ? BeepPlayer() : ClickPlayer();''',
'''            var player = _style switch
            {
                "terminal" => BeepPlayer(),
                "paper" => PaperPlayer(),
                "pixel" => ChiptunePlayer(),
                "aurora" => SoftPlayer(),
                _ => ClickPlayer(),
            };''', 1)

# 三个新音源
extra = '''
    /// <summary>纸便签：低频木头"笃"声（150Hz 衰减 + 微弱噪声）。</summary>
    private static System.Media.SoundPlayer? _paperPlayer;

    private static System.Media.SoundPlayer PaperPlayer()
    {
        if (_paperPlayer != null)
        {
            return _paperPlayer;
        }

        const int rate = 44100;
        var samples = (int)(rate * 0.05);
        var data = BuildWav(rate, samples, (i) =>
        {
            var t = (double)i / rate;
            var env = Math.Exp(-t * 90);
            var tone = Math.Sin(2 * Math.PI * 150 * t) * 0.7 + Math.Sin(2 * Math.PI * 300 * t) * 0.3;
            var noise = ((i * 1103515245 + 12345) >> 16 & 0x7FFF) / 32768.0 - 0.5;
            return (tone + noise * 0.35) * env * 0.55;
        });
        _paperPlayer = MakePlayer(data);
        return _paperPlayer;
    }

    /// <summary>8bit：两个八度的方波琶音，NES 味道。</summary>
    private static System.Media.SoundPlayer? _chipPlayer;

    private static System.Media.SoundPlayer ChiptunePlayer()
    {
        if (_chipPlayer != null)
        {
            return _chipPlayer;
        }

        const int rate = 44100;
        var samples = (int)(rate * 0.09);
        double[] notes = { 880, 1320 };
        var data = BuildWav(rate, samples, (i) =>
        {
            var t = (double)i / rate;
            var step = (int)(t / 0.045);
            var freq = notes[Math.Min(step, notes.Length - 1)];
            var env = 1.0 - (t / 0.09);
            var phase = (2 * Math.PI * freq * t) % (2 * Math.PI);
            var square = phase < Math.PI ? 1.0 : -1.0;
            return square * env * 0.32;
        });
        _chipPlayer = MakePlayer(data);
        return _chipPlayer;
    }

    /// <summary>毛玻璃极光：柔和正弦，轻微混响感（两次延迟叠回）。</summary>
    private static System.Media.SoundPlayer? _softPlayer;

    private static System.Media.SoundPlayer SoftPlayer()
    {
        if (_softPlayer != null)
        {
            return _softPlayer;
        }

        const int rate = 44100;
        var samples = (int)(rate * 0.16);
        var buffer = new double[samples];
        for (var i = 0; i < samples; i++)
        {
            var t = (double)i / rate;
            var env = Math.Exp(-t * 22);
            buffer[i] = Math.Sin(2 * Math.PI * 660 * t) * env * 0.45;
        }

        var delay = (int)(rate * 0.045);
        for (var i = delay; i < samples; i++)
        {
            buffer[i] += buffer[i - delay] * 0.35;
        }

        var data = BuildWav(rate, samples, (i) => buffer[i]);
        _softPlayer = MakePlayer(data);
        return _softPlayer;
    }

    private static byte[] BuildWav(int rate, int samples, Func<int, double> sampleAt)
    {
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
            var value = (short)(Math.Clamp(sampleAt(i), -1, 1) * 11000);
            BitConverter.GetBytes(value).CopyTo(data, 44 + i * 2);
        }

        return data;
    }

    private static System.Media.SoundPlayer MakePlayer(byte[] data)
    {
        var stream = new System.IO.MemoryStream(data, false);
        var player = new System.Media.SoundPlayer(stream);
        player.Load();
        return player;
    }

    private void PlayUiSound(bool strong = false)'''

s = s.replace("    private void PlayUiSound(bool strong = false)", extra, 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("dock styling + 3 sounds added")
