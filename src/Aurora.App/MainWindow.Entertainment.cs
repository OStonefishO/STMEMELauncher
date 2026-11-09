using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace Aurora.App;

/// <summary>娱乐板块：内嵌 B 站 + 本地音乐播放器（含歌词）。</summary>
public partial class MainWindow
{
    // ---------------- 通用 ----------------
    private static readonly string[] MusicExtensions =
    {
        ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".wma", ".opus", ".aiff", ".ape",
    };

    private static HttpClient CreateMusicHttp()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(9) };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/124.0.0.0 Safari/537.36");
        return client;
    }

    private static readonly HttpClient MusicHttp = CreateMusicHttp();

    // ---------------- 娱乐页 / B 站 ----------------
    private bool _biliReady;
    private bool _biliInitStarted;

    private async void OnFunTab(object sender, RoutedEventArgs e)
    {
        var wantBili = sender == FunTabBili;
        SetFunTab(wantBili);
        if (wantBili)
        {
            await EnsureBiliAsync();
        }
        else
        {
            EnsureMusicInited();
        }
    }

    private void SetFunTab(bool bili)
    {
        if (BiliPane == null || MusicPane == null)
        {
            return;
        }

        BiliPane.Visibility = bili ? Visibility.Visible : Visibility.Collapsed;
        MusicPane.Visibility = bili ? Visibility.Collapsed : Visibility.Visible;

        var hero = (Style)FindResource("HeroButton");
        var glass = (Style)FindResource("GlassButton");
        FunTabBili.Style = bili ? hero : glass;
        FunTabMusic.Style = bili ? glass : hero;

        FunStatus.Text = bili
            ? "B 站是网页版，登录一次以后就一直有效"
            : "本地音乐，网易云 / QQ 音乐 / 酷狗的下载都能直接放";
    }

    /// <summary>进入娱乐页时调用：默认展示上次的子页。</summary>
    private async Task OnFunOpenedAsync()
    {
        if (MusicPane == null)
        {
            return;
        }

        var showBili = BiliPane.Visibility == Visibility.Visible;
        SetFunTab(showBili);
        if (showBili)
        {
            await EnsureBiliAsync();
        }
        else
        {
            EnsureMusicInited();
        }
    }

    private async Task EnsureBiliAsync()
    {
        if (_biliReady || _biliInitStarted || BiliWeb == null)
        {
            return;
        }

        _biliInitStarted = true;
        try
        {
            BiliHint.Text = "正在启动内置浏览器…";
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MEMELauncher", "BiliWeb");
            Directory.CreateDirectory(folder);

            var env = await CoreWebView2Environment.CreateAsync(null, folder);
            await BiliWeb.EnsureCoreWebView2Async(env);

            var core = BiliWeb.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;

            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                try
                {
                    core.Navigate(args.Uri);
                }
                catch
                {
                    // 忽略跳转失败
                }
            };

            core.NavigationCompleted += (_, __) =>
            {
                try
                {
                    BiliUrl.Text = core.Source;
                }
                catch
                {
                    // 忽略
                }
            };

            core.Navigate("https://www.bilibili.com/");
            _biliReady = true;
            BiliHint.Text = "整页自动铺满宽度；想看得更细就点 ＋ 放大，点「适应」恢复";

            // B 站桌面版按 1300px 左右排版，先按窗口宽度算一个合适的缩放，整页正好铺满
            ApplyBiliZoom(FitZoomFor(BiliWeb.ActualWidth), manual: false);
        }
        catch (Exception ex)
        {
            BiliHint.Text = "B 站没能加载：" + ex.Message;
        }
        finally
        {
            _biliInitStarted = false;
        }
    }

    private void OnBiliBack(object sender, RoutedEventArgs e) => SafeBili(c =>
    {
        if (c.CanGoBack)
        {
            c.GoBack();
        }
    });

    private void OnBiliForward(object sender, RoutedEventArgs e) => SafeBili(c => { if (c.CanGoForward) c.GoForward(); });

    private void OnBiliReload(object sender, RoutedEventArgs e) => SafeBili(c => c.Reload());

    private void OnBiliHome(object sender, RoutedEventArgs e) => SafeBili(c => c.Navigate("https://www.bilibili.com/"));

    // ---------------- B 站：缩放铺满 ----------------
    /// <summary>B 站桌面版按这个 CSS 宽度排版，缩放后整页正好铺满。</summary>
    private const double BiliTargetCssWidth = 1300;

    private bool _biliManualZoom;

    private double FitZoomFor(double dipWidth)
    {
        if (dipWidth <= 80)
        {
            return 1.0;
        }

        return Math.Clamp(dipWidth / BiliTargetCssWidth, 0.5, 1.25);
    }

    private void ApplyBiliZoom(double zoom, bool manual)
    {
        zoom = Math.Clamp(zoom, 0.5, 1.6);
        _biliManualZoom = manual;

        try
        {
            if (BiliWeb?.CoreWebView2 != null)
            {
                BiliWeb.ZoomFactor = zoom;
            }
        }
        catch
        {
            // 忽略
        }

        if (BiliZoomText != null)
        {
            BiliZoomText.Text = Math.Round(zoom * 100) + "%";
        }
    }

    private void OnBiliZoomIn(object sender, RoutedEventArgs e) => ApplyBiliZoom((BiliWeb?.ZoomFactor ?? 1) + 0.1, true);

    private void OnBiliZoomOut(object sender, RoutedEventArgs e) => ApplyBiliZoom((BiliWeb?.ZoomFactor ?? 1) - 0.1, true);

    private void OnBiliZoomFit(object sender, RoutedEventArgs e) => ApplyBiliZoom(FitZoomFor(BiliWeb?.ActualWidth ?? 0), false);

    private void OnBiliSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 手动缩放过就不动；否则窗口一变大变小都重新铺满
        if (_biliManualZoom || BiliWeb == null)
        {
            return;
        }

        var target = FitZoomFor(e.NewSize.Width);
        if (Math.Abs(target - BiliWeb.ZoomFactor) > 0.01)
        {
            ApplyBiliZoom(target, false);
        }
    }

    private void SafeBili(Action<CoreWebView2> action)
    {
        try
        {
            if (BiliWeb?.CoreWebView2 == null)
            {
                return;
            }

            action(BiliWeb.CoreWebView2);
        }
        catch
        {
            // 忽略
        }
    }

    // ---------------- 音乐：状态 ----------------
    public sealed class MusicTrack
    {
        public string Path { get; init; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Sub => string.IsNullOrWhiteSpace(Artist)
            ? System.IO.Path.GetFileName(Path)
            : Artist;
    }

    private sealed class LyricLine
    {
        public LyricLine(double time, string text)
        {
            Time = time;
            Text = text;
        }

        public double Time { get; }
        public string Text { get; }
    }

    private readonly ObservableCollection<MusicTrack> _tracks = new();
    private readonly MediaPlayer _music = new();
    private readonly List<LyricLine> _lyricLines = new();
    private DispatcherTimer? _musicTimer;
    private CancellationTokenSource? _lyricCts;
    private bool _musicInited;
    private bool _musicPlaying;
    private int _currentIndex = -1;
    private int _lyricIndex = -1;
    private bool _seekDragging;
    private bool _seekSuppress;
    private bool _suppressSelectPlay;

    private void EnsureMusicInited()
    {
        if (_musicInited)
        {
            return;
        }

        _musicInited = true;
        SongList.ItemsSource = _tracks;
        _music.Volume = Math.Max(0, Math.Min(1, VolumeBar.Value / 100.0));
        _music.MediaOpened += OnMusicOpened;
        _music.MediaEnded += (_, __) => PlayRelative(1);
        _music.MediaFailed += (_, args) =>
        {
            _musicPlaying = false;
            PlayButton.Content = "▶";
            FunStatus.Text = "这首歌放不出来：" + FirstLine(args.ErrorException?.Message);
        };

        _musicTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _musicTimer.Tick += (_, __) => OnMusicTick();
        _musicTimer.Start();

        ShowLyricMessage("选一首歌，歌词会显示在这里");
    }

    private static string FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "格式不支持";
        }

        var line = text.Split('\n')[0].Trim();
        return line.Length > 120 ? line[..120] : line;
    }

    // ---------------- 音乐：导入 ----------------
    private void OnMusicAdd(object sender, RoutedEventArgs e)
    {
        EnsureMusicInited();
        var dialog = new OpenFileDialog
        {
            Title = "选音乐（可以多选）",
            Filter = "音乐文件|*.mp3;*.flac;*.wav;*.m4a;*.aac;*.ogg;*.wma;*.opus;*.aiff;*.ape|所有文件|*.*",
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            AddMusicFiles(dialog.FileNames);
        }
    }

    private void OnMusicAddFolder(object sender, RoutedEventArgs e)
    {
        EnsureMusicInited();
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选一个放音乐的文件夹",
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            AddMusicFiles(new[] { dialog.SelectedPath });
        }
    }

    private void OnSongDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddMusicFiles(paths);
        }
    }

    private void OnSongDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void AddMusicFiles(IEnumerable<string> paths)
    {
        var added = 0;
        var files = new List<string>();

        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    files.AddRange(Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(IsMusicFile));
                }
                else if (File.Exists(path) && IsMusicFile(path))
                {
                    files.Add(path);
                }
            }
            catch
            {
                // 跳过没权限的目录
            }
        }

        foreach (var file in files)
        {
            if (_tracks.Any(t => string.Equals(t.Path, file, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _tracks.Add(MakeTrack(file));
            added++;
        }

        SongCountText.Text = _tracks.Count + " 首";
        PlaylistHint.Text = added > 0
            ? "刚加进来 " + added + " 首；双击列表就能放"
            : "没有新的音乐（支持 mp3 / flac / m4a / wav / ogg）";

        if (_currentIndex < 0 && _tracks.Count > 0)
        {
            _suppressSelectPlay = true;
            SongList.SelectedIndex = 0;
            _suppressSelectPlay = false;
            PlayTrack(0, autoplay: false);
        }
    }

    private static bool IsMusicFile(string path)
    {
        var ext = System.IO.Path.GetExtension(path);
        return MusicExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private static MusicTrack MakeTrack(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var title = name;
        var artist = string.Empty;

        // 常见命名："歌手 - 歌名" / "歌名 - 歌手"
        var sep = name.IndexOf(" - ", StringComparison.Ordinal);
        if (sep > 0)
        {
            artist = name[..sep].Trim();
            title = name[(sep + 3)..].Trim();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = name;
        }

        return new MusicTrack { Path = path, Title = title, Artist = artist };
    }

    private void OnMusicClear(object sender, RoutedEventArgs e)
    {
        StopMusic();
        _tracks.Clear();
        _currentIndex = -1;
        SongCountText.Text = "0 首";
        NowTitle.Text = "还没有选歌";
        NowArtist.Text = "左边挑一首，或者把音乐拖进来";
        PlaylistHint.Text = "也可以把音乐文件直接拖进列表";
        ClearLyrics("清空列表了");
    }

    // ---------------- 音乐：播放 ----------------
    private void OnSongSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectPlay)
        {
            return;
        }

        if (SongList.SelectedIndex >= 0 && SongList.SelectedIndex != _currentIndex)
        {
            PlayTrack(SongList.SelectedIndex);
        }
    }

    private void OnMusicPlayPause(object sender, RoutedEventArgs e)
    {
        EnsureMusicInited();
        if (_currentIndex < 0)
        {
            if (_tracks.Count > 0)
            {
                PlayTrack(0);
            }

            return;
        }

        if (_musicPlaying)
        {
            _music.Pause();
            _musicPlaying = false;
            PlayButton.Content = "▶";
        }
        else
        {
            _music.Play();
            _musicPlaying = true;
            PlayButton.Content = "Ⅱ";
        }
    }

    private void OnMusicPrev(object sender, RoutedEventArgs e) => PlayRelative(-1);

    private void OnMusicNext(object sender, RoutedEventArgs e) => PlayRelative(1);

    private void PlayRelative(int delta)
    {
        if (_tracks.Count == 0)
        {
            return;
        }

        var next = _currentIndex < 0 ? 0 : _currentIndex + delta;
        if (next < 0)
        {
            next = _tracks.Count - 1;
        }
        else if (next >= _tracks.Count)
        {
            next = 0;
        }

        PlayTrack(next);
    }

    private void PlayTrack(int index, bool autoplay = true)
    {
        EnsureMusicInited();
        if (index < 0 || index >= _tracks.Count)
        {
            return;
        }

        var track = _tracks[index];
        _currentIndex = index;
        if (SongList.SelectedIndex != index)
        {
            _suppressSelectPlay = true;
            SongList.SelectedIndex = index;
            _suppressSelectPlay = false;
        }

        try
        {
            _music.Stop();
            _music.Close();
            _music.Open(new Uri(track.Path));
            _music.Volume = Math.Max(0, Math.Min(1, VolumeBar.Value / 100.0));
            if (autoplay)
            {
                _music.Play();
                _musicPlaying = true;
                PlayButton.Content = "Ⅱ";
            }
            else
            {
                _musicPlaying = false;
                PlayButton.Content = "▶";
            }
        }
        catch (Exception ex)
        {
            FunStatus.Text = "打不开这个文件：" + FirstLine(ex.Message);
            return;
        }

        NowTitle.Text = track.Title;
        NowArtist.Text = string.IsNullOrWhiteSpace(track.Artist)
            ? System.IO.Path.GetFileName(track.Path)
            : track.Artist;
        TimeNow.Text = "0:00";
        TimeTotal.Text = "0:00";
        SeekBar.Value = 0;
        FunStatus.Text = autoplay ? "正在播放：" + track.Title : "已选好：" + track.Title + "，按播放开始";

        _ = LoadLyricsAsync(track);
    }

    private void StopMusic()
    {
        try
        {
            _music.Stop();
            _music.Close();
        }
        catch
        {
            // 忽略
        }

        _musicPlaying = false;
        PlayButton.Content = "▶";
    }

    private void OnMusicOpened(object? sender, EventArgs e)
    {
        try
        {
            if (!_music.NaturalDuration.HasTimeSpan)
            {
                return;
            }

        var total = _music.NaturalDuration.TimeSpan;
            SeekBar.Maximum = Math.Max(1, total.TotalSeconds);
            TimeTotal.Text = FormatTime(total);
            UpdateLyricHighlight(_music.Position.TotalSeconds);
        }
        catch
        {
            // 忽略
        }
    }

    private void OnMusicTick()
    {
        if (_currentIndex < 0 || !_musicPlaying)
        {
            return;
        }

        try
        {
            var pos = _music.Position;
            if (!_seekDragging)
            {
                _seekSuppress = true;
                SeekBar.Value = Math.Max(0, Math.Min(SeekBar.Maximum, pos.TotalSeconds));
                _seekSuppress = false;
            }

            TimeNow.Text = FormatTime(pos);
            UpdateLyricHighlight(pos.TotalSeconds);
        }
        catch
        {
            // 播放器偶尔会抛，忽略
        }
    }

    private void OnSeekDragStart(object sender, DragStartedEventArgs e) => _seekDragging = true;

    private void OnSeekDragEnd(object sender, DragCompletedEventArgs e)
    {
        _seekDragging = false;
        SeekTo(SeekBar.Value);
    }

    private void OnSeekChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_seekSuppress || _seekDragging || !_musicInited)
        {
            return;
        }

        // 点击轨道直接跳（IsMoveToPointEnabled 不会走 Drag 事件）
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            SeekTo(e.NewValue);
        }
    }

    private void SeekTo(double seconds)
    {
        try
        {
            if (_music.NaturalDuration.HasTimeSpan)
            {
                _music.Position = TimeSpan.FromSeconds(Math.Max(0, Math.Min(seconds, SeekBar.Maximum)));
            }
        }
        catch
        {
            // 忽略
        }
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolText == null)
        {
            return;
        }

        var percent = (int)Math.Round(e.NewValue);
        VolText.Text = percent + "%";
        try
        {
            _music.Volume = Math.Max(0, Math.Min(1, e.NewValue / 100.0));
        }
        catch
        {
            // 忽略
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}",
                (int)time.TotalHours, time.Minutes, time.Seconds);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", time.Minutes, time.Seconds);
    }

    // ---------------- 歌词：读取 / 显示 ----------------
    private async Task LoadLyricsAsync(MusicTrack track)
    {
        _lyricCts?.Cancel();
        _lyricCts = new CancellationTokenSource();
        var token = _lyricCts.Token;

        LyricSource.Text = string.Empty;

        var sidecar = FindSidecarLyric(track);
        if (sidecar != null)
        {
            try
            {
                var text = await File.ReadAllTextAsync(sidecar, token);
                ShowLyrics(text, "本地歌词");
                return;
            }
            catch
            {
                // 读不了就当没有
            }
        }

        ShowLyricMessage("正在找歌词…");
        LyricSource.Text = "匹配中";

        var query = string.IsNullOrWhiteSpace(track.Artist)
            ? track.Title
            : track.Artist + " " + track.Title;

        var remote = await TryFetchLyricAsync(query, token);
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (!string.IsNullOrEmpty(remote))
        {
            ShowLyrics(remote, "在线歌词");
            return;
        }

        ShowLyricMessage("没找到歌词。把同名的 .lrc 放到音乐旁边，或者用网易云 / QQ 音乐 / 酷狗下载的歌词文件");
        LyricSource.Text = string.Empty;
    }

    private static string? FindSidecarLyric(MusicTrack track)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(track.Path);
            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }

            var baseName = System.IO.Path.GetFileNameWithoutExtension(track.Path);
            var candidates = new[]
            {
                System.IO.Path.Combine(dir, baseName + ".lrc"),
                System.IO.Path.Combine(dir, track.Artist + " - " + track.Title + ".lrc"),
                System.IO.Path.Combine(dir, track.Title + ".lrc"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate) && new FileInfo(candidate).Length > 8)
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }

    private void ClearLyrics(string message)
    {
        _lyricLines.Clear();
        _lyricIndex = -1;
        _lyricCts?.Cancel();
        LyricPanel.Children.Clear();
        LyricSource.Text = string.Empty;
        if (!string.IsNullOrEmpty(message))
        {
            ShowLyricMessage(message);
        }
    }

    private void ShowLyricMessage(string message)
    {
        _lyricLines.Clear();
        _lyricIndex = -1;
        LyricPanel.Children.Clear();
        LyricPanel.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("InkFaint"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(20, 6, 20, 6),
        });
    }

    private void ShowLyrics(string lrcText, string source)
    {
        var lines = ParseLrc(lrcText);
        LyricPanel.Children.Clear();
        _lyricLines.Clear();
        _lyricIndex = -1;

        if (lines.Count == 0)
        {
            ShowLyricMessage("这份歌词没有时间轴，显示不了");
            LyricSource.Text = string.Empty;
            return;
        }

        _lyricLines.AddRange(lines);
        LyricSource.Text = source;

        var normal = (Brush)FindResource("InkDim");
        foreach (var line in lines)
        {
            LyricPanel.Children.Add(new TextBlock
            {
                Text = line.Text,
                FontSize = 13,
                Foreground = normal,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                LineHeight = 20,
                Margin = new Thickness(20, 6, 20, 6),
                Opacity = 0.75,
            });
        }

        double position = 0;
        try
        {
            position = _music.Position.TotalSeconds;
        }
        catch
        {
            // 还没开始播
        }

        UpdateLyricHighlight(position);
    }

    private void UpdateLyricHighlight(double seconds)
    {
        if (_lyricLines.Count == 0 || LyricPanel.Children.Count != _lyricLines.Count)
        {
            return;
        }

        var index = -1;
        for (var i = 0; i < _lyricLines.Count; i++)
        {
            if (_lyricLines[i].Time <= seconds + 0.15)
            {
                index = i;
            }
            else
            {
                break;
            }
        }

        if (index == _lyricIndex)
        {
            return;
        }

        _lyricIndex = index;
        var active = (Brush)FindResource("Ink");

        for (var i = 0; i < LyricPanel.Children.Count; i++)
        {
            if (LyricPanel.Children[i] is not TextBlock block)
            {
                continue;
            }

            var isActive = i == index;
            block.Foreground = isActive ? active : (Brush)FindResource("InkDim");
            block.FontSize = isActive ? 15 : 13;
            block.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
            block.Opacity = isActive ? 1 : (i < index ? 0.45 : 0.75);
        }

        if (index >= 0 && LyricPanel.Children[index] is TextBlock current)
        {
            try
            {
                var y = current.TranslatePoint(new Point(0, 0), LyricPanel).Y;
                var target = y - (LyricScroll.ViewportHeight * 0.42);
                LyricScroll.ScrollToVerticalOffset(Math.Max(0, target));
            }
            catch
            {
                // 忽略
            }
        }
    }

    private static readonly Regex LrcTag =
        new(@"\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);

    private static List<LyricLine> ParseLrc(string text)
    {
        var lines = new List<LyricLine>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return lines;
        }

        foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
        {
            var matches = LrcTag.Matches(raw);
            if (matches.Count == 0)
            {
                continue;
            }

            var content = LrcTag.Replace(raw, string.Empty).Trim();
            if (content.Length == 0)
            {
                continue;
            }

            foreach (Match match in matches)
            {
                var minute = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var second = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var fraction = match.Groups[3].Success ? match.Groups[3].Value : "0";
                var ms = fraction.Length switch
                {
                    1 => int.Parse(fraction, CultureInfo.InvariantCulture) * 100,
                    2 => int.Parse(fraction, CultureInfo.InvariantCulture) * 10,
                    _ => int.Parse(fraction, CultureInfo.InvariantCulture),
                };

                lines.Add(new LyricLine(minute * 60 + second + ms / 1000.0, content));
            }
        }

        lines.Sort((a, b) => a.Time.CompareTo(b.Time));
        return lines;
    }

    // ---------------- 歌词：在线匹配（网易云 → QQ 音乐 → 酷狗） ----------------
    private static async Task<string?> TryFetchLyricAsync(string query, CancellationToken token)
    {
        var providers = new (string Name, Func<string, CancellationToken, Task<string?>> Fetch)[]
        {
            ("网易云", FetchNeteaseLyricAsync),
            ("QQ音乐", FetchQqLyricAsync),
            ("酷狗", FetchKugouLyricAsync),
        };

        var queries = BuildQueries(query);

        foreach (var candidate in queries)
        {
            foreach (var provider in providers)
            {
                if (token.IsCancellationRequested)
                {
                    return null;
                }

                try
                {
                    var lyric = await provider.Fetch(candidate, token);
                    if (!string.IsNullOrWhiteSpace(lyric) && lyric.Contains("[00:"))
                    {
                        return lyric;
                    }
                }
                catch
                {
                    // 换下一个源
                }
            }
        }

        return null;
    }

    /// <summary>先拿「歌手 歌名」搜，搜不到就退化成只有歌名（去掉括号里的版本信息）。</summary>
    private static List<string> BuildQueries(string query)
    {
        var queries = new List<string> { query };

        var cleaned = Regex.Replace(query, @"[（(【\[].*?[）)】\]]", " ").Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        // query 是「歌手 歌名」，取最后一段当歌名
        var lastSpace = cleaned.LastIndexOf(' ');
        var titleOnly = lastSpace > 0 ? cleaned[(lastSpace + 1)..] : cleaned;
        if (titleOnly.Length >= 2 && !queries.Contains(titleOnly))
        {
            queries.Add(titleOnly);
        }

        if (!queries.Contains(cleaned) && cleaned.Length >= 2)
        {
            queries.Add(cleaned);
        }

        return queries;
    }

    private static async Task<string?> FetchNeteaseLyricAsync(string query, CancellationToken token)
    {
        var searchUrl = "https://music.163.com/api/search/get/web?s=" +
                        Uri.EscapeDataString(query) + "&type=1&limit=5&offset=0";
        using var searchReq = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        searchReq.Headers.Referrer = new Uri("https://music.163.com/");
        using var searchRes = await MusicHttp.SendAsync(searchReq, token);
        var searchJson = await searchRes.Content.ReadAsStringAsync(token);

        using var searchDoc = JsonDocument.Parse(searchJson);
        if (!TryPath(searchDoc.RootElement, out var songs, "result", "songs") ||
            songs.ValueKind != JsonValueKind.Array || songs.GetArrayLength() == 0)
        {
            return null;
        }

        var id = songs[0].GetProperty("id").GetInt64();
        var lyricUrl = $"https://music.163.com/api/song/lyric?id={id}&lv=1&kv=1&tv=-1";
        using var lyricReq = new HttpRequestMessage(HttpMethod.Get, lyricUrl);
        lyricReq.Headers.Referrer = new Uri("https://music.163.com/");
        using var lyricRes = await MusicHttp.SendAsync(lyricReq, token);
        var lyricJson = await lyricRes.Content.ReadAsStringAsync(token);

        using var lyricDoc = JsonDocument.Parse(lyricJson);
        return TryPath(lyricDoc.RootElement, out var lrc, "lrc", "lyric")
            ? lrc.GetString()
            : null;
    }

    private static async Task<string?> FetchQqLyricAsync(string query, CancellationToken token)
    {
        var searchUrl = "https://c.y.qq.com/soso/fcgi-bin/client_search_cp?w=" +
                        Uri.EscapeDataString(query) + "&format=json&n=5&p=1";
        using var searchReq = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        searchReq.Headers.Referrer = new Uri("https://y.qq.com/");
        using var searchRes = await MusicHttp.SendAsync(searchReq, token);
        var searchJson = await searchRes.Content.ReadAsStringAsync(token);

        using var searchDoc = JsonDocument.Parse(searchJson);
        if (!TryPath(searchDoc.RootElement, out var list, "data", "song", "list") ||
            list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
        {
            return null;
        }

        var mid = list[0].GetProperty("songmid").GetString();
        if (string.IsNullOrEmpty(mid))
        {
            return null;
        }

        var lyricUrl = "https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg?songmid=" +
                       Uri.EscapeDataString(mid) + "&format=json&nobase64=1&g_tk=5381";
        using var lyricReq = new HttpRequestMessage(HttpMethod.Get, lyricUrl);
        lyricReq.Headers.Referrer = new Uri("https://y.qq.com/portal/player.html");
        using var lyricRes = await MusicHttp.SendAsync(lyricReq, token);
        var lyricJson = await lyricRes.Content.ReadAsStringAsync(token);

        using var lyricDoc = JsonDocument.Parse(lyricJson);
        if (!TryPath(lyricDoc.RootElement, out var lyric))
        {
            return null;
        }

        return lyric.ValueKind == JsonValueKind.String ? UnescapeHtml(lyric.GetString() ?? string.Empty) : null;
    }

    private static async Task<string?> FetchKugouLyricAsync(string query, CancellationToken token)
    {
        var searchUrl = "https://mobilecdn.kugou.com/api/v3/search/song?format=json&keyword=" +
                        Uri.EscapeDataString(query) + "&page=1&pagesize=5&showtype=1";
        using var searchRes = await MusicHttp.GetAsync(searchUrl, token);
        var searchJson = await searchRes.Content.ReadAsStringAsync(token);

        using var searchDoc = JsonDocument.Parse(searchJson);
        if (!TryPath(searchDoc.RootElement, out var info, "data", "info") ||
            info.ValueKind != JsonValueKind.Array || info.GetArrayLength() == 0)
        {
            return null;
        }

        var first = info[0];
        var hash = first.TryGetProperty("hash", out var hashEl) ? hashEl.GetString() : null;
        var duration = first.TryGetProperty("duration", out var durEl) ? durEl.GetInt32() : 0;
        if (string.IsNullOrEmpty(hash))
        {
            return null;
        }

        var candidateUrl = "https://krcs.kugou.com/search?ver=1&man=yes&client=mobi&keyword=" +
                           Uri.EscapeDataString(query) + "&duration=" + duration + "&hash=" + hash;
        using var candidateRes = await MusicHttp.GetAsync(candidateUrl, token);
        var candidateJson = await candidateRes.Content.ReadAsStringAsync(token);

        using var candidateDoc = JsonDocument.Parse(candidateJson);
        if (!TryPath(candidateDoc.RootElement, out var candidates, "candidates") ||
            candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var pick = candidates[0];
        var id = pick.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var accessKey = pick.TryGetProperty("accesskey", out var keyEl) ? keyEl.GetString() : null;
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(accessKey))
        {
            return null;
        }

        var lyricUrl = "https://lyrics.kugou.com/download?ver=1&client=pc&id=" + id +
                       "&accesskey=" + accessKey + "&fmt=lrc&charset=utf8";
        using var lyricRes = await MusicHttp.GetAsync(lyricUrl, token);
        var lyricJson = await lyricRes.Content.ReadAsStringAsync(token);

        using var lyricDoc = JsonDocument.Parse(lyricJson);
        if (!TryPath(lyricDoc.RootElement, out var content, "content"))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(content.GetString() ?? string.Empty);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryPath(JsonElement root, out JsonElement result, params string[] path)
    {
        result = root;
        foreach (var key in path)
        {
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(key, out var next))
            {
                result = default;
                return false;
            }

            result = next;
        }

        return true;
    }

    private static string UnescapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('&'))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '&')
            {
                builder.Append(text[i]);
                continue;
            }

            var end = text.IndexOf(';', i);
            if (end < 0 || end - i > 10)
            {
                builder.Append(text[i]);
                continue;
            }

            var entity = text.Substring(i + 1, end - i - 1);
            switch (entity)
            {
                case "amp": builder.Append('&'); break;
                case "lt": builder.Append('<'); break;
                case "gt": builder.Append('>'); break;
                case "quot": builder.Append('"'); break;
                case "apos": builder.Append('\''); break;
                case "nbsp": builder.Append(' '); break;
                default:
                    if (entity.StartsWith("#x", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(entity[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                    {
                        builder.Append((char)hex);
                    }
                    else if (entity.StartsWith('#') &&
                             int.TryParse(entity[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var num))
                    {
                        builder.Append((char)num);
                    }
                    else
                    {
                        builder.Append('&').Append(entity).Append(';');
                    }

                    break;
            }

            i = end;
        }

        return builder.ToString();
    }
}
