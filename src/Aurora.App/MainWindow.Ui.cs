using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Aurora.Core.Configuration;

namespace Aurora.App;

/// <summary>
/// 新版布局的动效层：极光背景、卡片悬停发光、按钮回弹、侧栏悬停展开、主题色相偏移。
/// 全部只在「新版（动感）」下启用，经典布局保持安静。
/// </summary>
public partial class MainWindow
{
    private bool _auroraRunning;
    private bool _railExpanded;
    private int _hueShift;
    private bool _uiHooksReady;
    private bool _syncingHue;


    // ---------------- 底部 dock（激进版导航） ----------------

    private readonly Dictionary<int, Button> _dockButtons = new();

    private static readonly (int Page, string Icon, string Label)[] DockItems =
    {
        (0, "◈", "启动"),
        (2, "▦", "内容"),
        (7, "❖", "版本"),
        (6, "⇄", "联机"),
        (8, "♫", "娱乐"),
        (9, "✎", "创作"),
        (3, "👤", "账号"),
        (4, "⚙", "设置"),
    };

    private void BuildDock()
    {
        if (DockPanel == null || DockPanel.Children.Count > 0)
        {
            return;
        }

        foreach (var (page, icon, label) in DockItems)
        {
            var button = new Button
            {
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(2, 0, 2, 0),
                Tag = page,
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)FindResource("InkDim"),
            });
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Foreground = (Brush)FindResource("InkFaint"),
            });
            button.Content = stack;
            button.Click += (s, _) =>
            {
                if (s is Button b && b.Tag is int index)
                {
                    PlayUiSound();
                    SetPage(index);
                    UpdateDock(index);
                }
            };

            var hover = new ScaleTransform(1, 1);
            button.RenderTransform = hover;
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            button.MouseEnter += (_, _) => Animate(hover, ScaleTransform.ScaleXProperty, 1.12, 120);
            button.MouseEnter += (_, _) => Animate(hover, ScaleTransform.ScaleYProperty, 1.12, 120);
            button.MouseLeave += (_, _) => Animate(hover, ScaleTransform.ScaleXProperty, 1.0, 140);
            button.MouseLeave += (_, _) => Animate(hover, ScaleTransform.ScaleYProperty, 1.0, 140);

            _dockButtons[page] = button;
            DockPanel.Children.Add(button);
        }
    }

    private void UpdateDock(int page)
    {
        if (_dockButtons.Count == 0)
        {
            return;
        }

        // 「安装」并入 内容 页高亮
        var active = page == 1 ? 2 : page;
        foreach (var (key, button) in _dockButtons)
        {
            var on = key == active;
            button.Background = on ? (Brush)FindResource("AccentGrad") : Brushes.Transparent;
            if (button.Content is StackPanel stack)
            {
                foreach (var text in stack.Children.OfType<TextBlock>())
                {
                    text.Foreground = on ? Brushes.White : (Brush)FindResource("InkDim");
                }
            }
        }
    }

    /// <summary>新版布局：隐藏左侧栏、显示底部 dock；经典反过来。</summary>
    private void ApplyNavigationStyle()
    {
        if (BottomDock == null || RailPanel == null || RailColumn == null)
        {
            return;
        }

        if (_vivid)
        {
            BuildDock();
            BottomDock.Visibility = Visibility.Visible;
            RailPanel.Visibility = Visibility.Collapsed;
            RailColumn.Width = new GridLength(0);
            HeroGrid.Visibility = Visibility.Collapsed;
            DashboardGrid.Visibility = Visibility.Visible;
            SetPagesTopInset(72);
            InitDashboardScene();
            UpdateDock(_currentPage);
        }
        else
        {
            BottomDock.Visibility = Visibility.Collapsed;
            RailPanel.Visibility = Visibility.Visible;
            RailColumn.Width = new GridLength(74);
            HeroGrid.Visibility = Visibility.Visible;
            DashboardGrid.Visibility = Visibility.Collapsed;
            SetPagesTopInset(0);
        }
    }

    /// <summary>顶部胶囊条是浮层，给内容页让出空间。</summary>
    private void SetPagesTopInset(double top)
    {
        foreach (var page in new FrameworkElement[]
                 {
                     LaunchPage, InstallPage, ContentPage, AccountPage, SettingsPage,
                     UpdatesPage, OnlinePage, VersionsPage, FunPage, CreationPage,
                 })
        {
            if (page == null)
            {
                continue;
            }

            var m = page.Margin;
            page.Margin = new Thickness(m.Left, top, m.Right, m.Bottom);
        }
    }


    // ---------------- 仪表盘上的 3D 方块岛 ----------------

    private bool _dashSceneReady;

    private void InitDashboardScene()
    {
        if (_dashSceneReady || DashViewport == null)
        {
            return;
        }

        _dashSceneReady = true;
        var cam = HeroScene.Camera();
        cam.Width *= 1.45;   // 小卡片里把视野放宽一点，岛不会被裁掉
        DashViewport.Camera = cam;
        RefreshDashboardScene();
    }

    private void RefreshDashboardScene()
    {
        if (DashViewport == null)
        {
            return;
        }

        DashViewport.Children.Clear();
        DashViewport.Children.Add(new ModelVisual3D { Content = HeroScene.Lights(_heroBiome) });
        DashViewport.Children.Add(HeroScene.Build(_heroBiome));
        if (DashBiomeText != null)
        {
            DashBiomeText.Text = HeroScene.BiomeName(_heroBiome) + " · 点一下换群系";
        }
    }

    private void OnDashboardBiomeClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _heroBiome = _heroBiome switch
        {
            HeroBiome.Overworld => HeroBiome.Nether,
            HeroBiome.Nether => HeroBiome.End,
            _ => HeroBiome.Overworld,
        };

        RefreshDashboardScene();
        if (HeroViewport != null)
        {
            ApplyHeroBiome();
        }

        ShowToast(HeroScene.BiomeName(_heroBiome), HeroScene.BiomeHint(_heroBiome), "✦", 2200);
    }

    // ---------------- 音效 ----------------

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

    // ---------------- 极光 ----------------

    private void StartAurora()
    {
        if (AuroraLayer == null)
        {
            return;
        }

        AuroraLayer.Visibility = _vivid ? Visibility.Visible : Visibility.Collapsed;
        if (!_vivid)
        {
            return;
        }

        RecolorAurora();
        if (_auroraRunning)
        {
            return;
        }

        _auroraRunning = true;
        DriftEllipse(AuroraA, 0, 0, 26, 0.55, TimeSpan.FromSeconds(11));
        DriftEllipse(AuroraB, 0, 0, -34, 0.5, TimeSpan.FromSeconds(14));
        DriftEllipse(AuroraC, 0, 0, 22, 0.45, TimeSpan.FromSeconds(17));
    }

    /// <summary>极光颜色跟随主题色相。</summary>
    private void RecolorAurora()
    {
        var accent = GetAccentColor();
        PaintAurora(AuroraA, accent, 0x60);
        PaintAurora(AuroraB, ShiftHue(accent, 40), 0x4C);
        PaintAurora(AuroraC, ShiftHue(accent, -50), 0x3E);
    }

    private static void PaintAurora(Ellipse? shape, Color color, byte alpha)
    {
        if (shape == null)
        {
            return;
        }

        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, color.R, color.G, color.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        brush.Freeze();
        shape.Fill = brush;
    }

    /// <summary>缓慢漂移 + 呼吸，极光的感觉。</summary>
    private static void DriftEllipse(Ellipse? shape, double x, double y, double dx, double minOpacity, TimeSpan duration)
    {
        if (shape == null)
        {
            return;
        }

        var move = new TranslateTransform(x, y);
        shape.RenderTransform = move;

        var animX = new DoubleAnimation(0, dx, duration)
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        var animY = new DoubleAnimation(0, dx * 0.6, duration)
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        move.BeginAnimation(TranslateTransform.XProperty, animX);
        move.BeginAnimation(TranslateTransform.YProperty, animY);

        var glow = new DoubleAnimation(1, minOpacity, TimeSpan.FromSeconds(4.5))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        shape.BeginAnimation(UIElement.OpacityProperty, glow);
    }

    private void StopAurora()
    {
        if (AuroraLayer != null)
        {
            AuroraLayer.Visibility = Visibility.Collapsed;
        }
    }

    // ---------------- 卡片悬停 / 按钮回弹 ----------------

    /// <summary>给当前页所有卡片挂上悬停发光 + 微放大，给按钮挂上按下回弹。</summary>
    private void AttachMotionHooks(DependencyObject? root)
    {
        if (!_vivid || root == null)
        {
            return;
        }

        foreach (var element in Descendants(root).Take(600))
        {
            switch (element)
            {
                case Border border when IsCard(border):
                    AttachCardHover(border);
                    break;
                case Button button when button.Style != null && !Equals(button.Tag as string, "noBounce"):
                    AttachButtonBounce(button);
                    break;
            }
        }
    }

    private static bool IsCard(Border border) =>
        border.CornerRadius.TopLeft >= 10 &&
        border.BorderThickness.Left > 0 &&
        !Equals(border.Tag as string, "deps") &&
        border.RenderSize.Width > 120;

    private void AttachCardHover(Border border)
    {
        if (Equals(border.Tag as string, "hover"))
        {
            return;
        }

        border.Tag = "hover";
        border.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = border.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        border.RenderTransform = scale;

        border.MouseEnter += (_, _) =>
        {
            var accent = (Brush)FindResource("AccentGrad");
            border.Effect = new DropShadowEffect
            {
                Color = GetAccentColor(),
                BlurRadius = 26,
                ShadowDepth = 0,
                Opacity = 0.45,
            };
            Animate(scale, ScaleTransform.ScaleXProperty, 1.012, 140);
            Animate(scale, ScaleTransform.ScaleYProperty, 1.012, 140);
            _ = accent;
        };

        border.MouseLeave += (_, _) =>
        {
            border.Effect = null;
            Animate(scale, ScaleTransform.ScaleXProperty, 1.0, 160);
            Animate(scale, ScaleTransform.ScaleYProperty, 1.0, 160);
        };
    }

    private void AttachButtonBounce(Button button)
    {
        if (Equals(button.Tag as string, "bounce"))
        {
            return;
        }

        var previous = button.Tag;
        button.Tag = "bounce";
        button.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(1, 1);
        button.RenderTransform = scale;

        button.PreviewMouseLeftButtonDown += (_, _) =>
        {
            Animate(scale, ScaleTransform.ScaleXProperty, 0.965, 70);
            Animate(scale, ScaleTransform.ScaleYProperty, 0.965, 70);
        };

        button.PreviewMouseLeftButtonUp += (_, _) => BounceBack(scale);
        button.MouseLeave += (_, _) => BounceBack(scale);
        _ = previous;
    }

    private static void BounceBack(ScaleTransform scale)
    {
        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 };
        var x = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease };
        var y = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, x);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, y);
    }

    private static void Animate(Animatable target, DependencyProperty property, double to, int ms)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd,
        };
        target.BeginAnimation(property, anim);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    // ---------------- 侧栏悬停展开 ----------------

    private void OnRailEnter(object sender, System.Windows.Input.MouseEventArgs e) => SetRailExpanded(true);

    private void OnRailLeave(object sender, System.Windows.Input.MouseEventArgs e) => SetRailExpanded(false);

    private void SetRailExpanded(bool expanded)
    {
        if (!_vivid || RailColumn == null || RailPanel == null || _railExpanded == expanded)
        {
            return;
        }

        _railExpanded = expanded;
        var gridAnim = new GridLengthAnimation(
            expanded ? 178 : 74,
            TimeSpan.FromMilliseconds(180),
            new CubicEase { EasingMode = EasingMode.EaseOut });
        RailColumn.BeginAnimation(ColumnDefinition.WidthProperty, gridAnim);

        foreach (var button in NavButtons())
        {
            if (button.Content is not StackPanel stack || stack.Children.Count < 2)
            {
                continue;
            }

            var label = stack.Children.OfType<TextBlock>().LastOrDefault();
            if (label == null)
            {
                continue;
            }

            stack.Orientation = expanded ? Orientation.Horizontal : Orientation.Vertical;
            stack.Margin = expanded ? new Thickness(16, 0, 0, 0) : new Thickness(0);
            label.Margin = expanded ? new Thickness(9, 0, 0, 0) : new Thickness(0, 4, 0, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.FontSize = expanded ? 13 : 10.5;
            label.Opacity = expanded ? 0 : 1;
            if (expanded)
            {
                var fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { BeginTime = TimeSpan.FromMilliseconds(40) };
                label.BeginAnimation(UIElement.OpacityProperty, fade);
            }
        }
    }

    private IEnumerable<Button> NavButtons()
    {
        yield return NavLaunch;
        yield return NavContent;
        yield return NavVersions;
        yield return NavOnline;
        yield return NavFun;
        yield return NavCreation;
        yield return NavAccount;
        yield return NavSettings;
    }

    // ---------------- 主题色相 ----------------

    private void OnHueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingHue)
        {
            return;
        }

        _hueShift = (int)e.NewValue;
        if (HueText != null)
        {
            HueText.Text = _hueShift.Text();
        }

        if (!IsLoaded || !_vivid)
        {
            return;
        }

        _settings.UiHue = _hueShift;
        ApplyUiLayout(_settings.UiLayout);
        ConfigStore.Save(_settings);
    }

    /// <summary>把当前主题的渐变整体旋转色相，实现「粉 / 青 / 绿 / 橙」自由选。</summary>
    private void HueShiftResources(int degrees)
    {
        var res = Resources;
        foreach (var key in new[] { "AccentGrad", "TitleGrad" })
        {
            if (res[key] is not LinearGradientBrush source)
            {
                continue;
            }

            var shifted = new LinearGradientBrush
            {
                StartPoint = source.StartPoint,
                EndPoint = source.EndPoint,
            };
            foreach (var stop in source.GradientStops)
            {
                shifted.GradientStops.Add(new GradientStop(ShiftHue(stop.Color, degrees), stop.Offset));
            }

            shifted.Freeze();
            res[key] = shifted;
        }

        if (res["GlassLine"] is SolidColorBrush line)
        {
            var c = ShiftHue(line.Color, degrees);
            res["GlassLine"] = new SolidColorBrush(Color.FromArgb(line.Color.A, c.R, c.G, c.B));
        }
    }

    private Color GetAccentColor()
    {
        var brush = FindResource("AccentGrad") as LinearGradientBrush;
        var stop = brush?.GradientStops.FirstOrDefault();
        return ShiftHue(stop?.Color ?? Color.FromRgb(0xFF, 0x3D, 0x8B), _hueShift);
    }

    private static Color ShiftHue(Color color, int degrees)
    {
        if (degrees == 0)
        {
            return color;
        }

        var (h, s, l) = ToHsl(color);
        h = (h + degrees / 360.0 + 1.0) % 1.0;
        return FromHsl(h, s, l);
    }

    private static (double H, double S, double L) ToHsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (Math.Abs(max - min) < 1e-6)
        {
            return (0, 0, l);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h;
        if (max == r)
        {
            h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
        }
        else if (max == g)
        {
            h = ((b - r) / d + 2) / 6;
        }
        else
        {
            h = ((r - g) / d + 4) / 6;
        }

        return (h, s, l);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        if (s <= 0)
        {
            var v = (byte)Math.Round(l * 255);
            return Color.FromRgb(v, v, v);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        double R(double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        return Color.FromRgb(
            (byte)Math.Round(R(h + 1.0 / 3) * 255),
            (byte)Math.Round(R(h) * 255),
            (byte)Math.Round(R(h - 1.0 / 3) * 255));
    }

    // ---------------- 布局切换时统一刷新动效 ----------------

    private void RefreshMotionLayer()
    {
        if (_vivid)
        {
            StartAurora();
            AttachMotionHooks(CurrentPageElement());
        }
        else
        {
            StopAurora();
            SetRailExpanded(false);
        }

        if (!_uiHooksReady)
        {
            _uiHooksReady = true;
        }
    }

    private FrameworkElement? CurrentPageElement() => _currentPage switch
    {
        0 => LaunchPage,
        1 => InstallPage,
        2 => ContentPage,
        3 => AccountPage,
        4 => SettingsPage,
        5 => UpdatesPage,
        6 => OnlinePage,
        7 => VersionsPage,
        8 => FunPage,
        9 => CreationPage,
        _ => null,
    };
}

/// <summary>GridLength 的动画（WPF 没有内置），侧栏展开靠它。</summary>
public sealed class GridLengthAnimation : AnimationTimeline
{
    public GridLengthAnimation()
    {
    }

    public GridLengthAnimation(double to, Duration duration, IEasingFunction? easing)
    {
        To = new GridLength(to);
        Duration = duration;
        EasingFunction = easing;
    }

    public GridLength? From { get; set; }
    public GridLength? To { get; set; }
    public IEasingFunction? EasingFunction { get; set; }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        var progress = animationClock.CurrentProgress ?? 0;
        if (EasingFunction != null)
        {
            progress = EasingFunction.Ease(progress);
        }

        var from = From ?? (defaultOriginValue is GridLength gl ? gl : new GridLength(74));
        var to = To ?? (defaultDestinationValue is GridLength gt ? gt : new GridLength(74));
        return new GridLength(from.Value + (to.Value - from.Value) * progress);
    }
}

internal static class HueFormat
{
    public static string Text(this int degrees) => degrees == 0 ? "0°" : (degrees > 0 ? "+" : "") + degrees + "°";
}
