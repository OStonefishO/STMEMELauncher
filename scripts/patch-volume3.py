import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace('''            player.Volume = (float)Math.Clamp(_settings.UiVolume / 100.0, 0, 1);
            player.Play();''','''            player.Play();''', 1)

# 音量字段 + 变化时清缓存
s = s.replace("    private static System.Media.SoundPlayer? _clickPlayer;",
'''    private static double _soundVolume = 0.55;
    private static int _soundVolumeBucket = -1;
    private static System.Media.SoundPlayer? _clickPlayer;''', 1)

s = s.replace('''            var volume = Math.Clamp(_settings.UiVolume, 0, 100) / 100.0;
            if (volume <= 0.001)
            {
                return;
            }
''','''            var volume = Math.Clamp(_settings.UiVolume, 0, 100) / 100.0;
            if (volume <= 0.001)
            {
                return;
            }

            // 音量变了就丢掉旧采样，重新按新振幅合成
            var bucket = (int)Math.Round(volume * 20);
            if (bucket != _soundVolumeBucket)
            {
                _soundVolumeBucket = bucket;
                _soundVolume = volume;
                _clickPlayer = null;
                _beepPlayer = null;
                _paperPlayer = null;
                _chipPlayer = null;
                _softPlayer = null;
            }
''', 1)

# 合成时乘音量
s = s.replace('            var value = (short)(Math.Clamp(sampleAt(i), -1, 1) * 11000);',
              '            var value = (short)(Math.Clamp(sampleAt(i), -1, 1) * 11000 * _soundVolume);', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("volume via amplitude")
