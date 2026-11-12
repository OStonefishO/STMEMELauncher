using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace Aurora.App;

/// <summary>
/// 界面音效：启动/成功/警告/点击。音频用代码合成，不额外带音频文件。
/// 音量跟随设置里的「音效音量」，关掉开关就完全不发声。
/// </summary>
internal static class SoundService
{
    private const int SampleRate = 44100;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, byte[]> Cache = new();
    private static readonly List<WaveOutEvent> Playing = new();

    public static bool Enabled { get; set; } = true;

    public static int Volume { get; set; } = 55;

    public static void Play(string kind)
    {
        if (!Enabled || Volume <= 0)
        {
            return;
        }

        try
        {
            var key = kind + ":" + Volume;
            byte[] pcm;
            lock (Gate)
            {
                if (!Cache.TryGetValue(key, out pcm!))
                {
                    pcm = Build(kind, Volume / 100f);
                    Cache[key] = pcm;
                }
            }

            var stream = new RawSourceWaveStream(new MemoryStream(pcm), new WaveFormat(SampleRate, 16, 1));
            var output = new WaveOutEvent();
            output.Init(stream);
            output.PlaybackStopped += (_, __) =>
            {
                try
                {
                    output.Dispose();
                    stream.Dispose();
                }
                catch
                {
                    // 忽略
                }

                lock (Gate)
                {
                    Playing.Remove(output);
                }
            };

            lock (Gate)
            {
                Playing.Add(output);
            }

            output.Play();
        }
        catch
        {
            // 没有音频设备也不影响启动器
        }
    }

    /// <summary>合成一小段声音（16bit 单声道 PCM）。</summary>
    private static byte[] Build(string kind, float volume)
    {
        double[] freqs;
        double seconds;
        double amp;
        switch (kind)
        {
            case "ok":
                freqs = new[] { 660.0, 990.0 };
                seconds = 0.14;
                amp = 0.30;
                break;
            case "warn":
                freqs = new[] { 330.0, 262.0 };
                seconds = 0.22;
                amp = 0.34;
                break;
            case "start":
                freqs = new[] { 523.0, 659.0, 784.0 };
                seconds = 0.28;
                amp = 0.34;
                break;
            default: // click
                freqs = new[] { 880.0 };
                seconds = 0.05;
                amp = 0.22;
                break;
        }

        var total = (int)(SampleRate * seconds);
        var perTone = Math.Max(1, total / freqs.Length);
        var data = new short[total];
        for (var i = 0; i < total; i++)
        {
            var toneIndex = Math.Min(freqs.Length - 1, i / perTone);
            var local = i - toneIndex * perTone;
            var progress = (double)local / perTone;
            var envelope = Math.Pow(1 - progress, 2.2);
            var value = Math.Sin(2 * Math.PI * freqs[toneIndex] * i / SampleRate) * amp * envelope;
            // 起音淡入，避免爆音
            var attack = Math.Min(1, i / 120.0);
            data[i] = (short)(Math.Clamp(value * attack * volume, -1, 1) * short.MaxValue);
        }

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        var byteCount = data.Length * 2;
        writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
        writer.Write(36 + byteCount);
        writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
        writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
        writer.Write(byteCount);
        foreach (var sample in data)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return ms.ToArray();
    }
}
