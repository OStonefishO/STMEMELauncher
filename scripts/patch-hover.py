import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

old_start = s.index("    private void AttachCardHover(Border border)")
old_end = s.index("    private void AttachButtonBounce(Button button)")
new = '''    private void AttachCardHover(Border border)
    {
        if (Equals(border.Tag as string, "hover"))
        {
            return;
        }

        border.Tag = "hover";
        border.RenderTransformOrigin = new Point(0.5, 0.5);

        // 每套布局的悬停手感不一样
        if (_paper)
        {
            var tilt = border.RenderTransform as RotateTransform ?? new RotateTransform(0);
            border.RenderTransform = tilt;
            border.MouseEnter += (_, _) => AnimateRotate(tilt, 0.7, 180);
            border.MouseLeave += (_, _) => AnimateRotate(tilt, 0, 200);
            return;
        }

        if (_pixel)
        {
            var shift = border.RenderTransform as TranslateTransform ?? new TranslateTransform(0, 0);
            border.RenderTransform = shift;
            border.MouseEnter += (_, _) => Animate(shift, TranslateTransform.YProperty, -3, 90);
            border.MouseLeave += (_, _) => Animate(shift, TranslateTransform.YProperty, 0, 110);
            return;
        }

        if (_terminal)
        {
            border.MouseEnter += (_, _) =>
            {
                border.Effect = new DropShadowEffect { Color = Color.FromRgb(0x35, 0xF0, 0x7A), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.55 };
            };
            border.MouseLeave += (_, _) => border.Effect = null;
            return;
        }

        var scale = border.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        border.RenderTransform = scale;

        border.MouseEnter += (_, _) =>
        {
            border.Effect = new DropShadowEffect
            {
                Color = GetAccentColor(),
                BlurRadius = _aurora ? 34 : 26,
                ShadowDepth = 0,
                Opacity = _aurora ? 0.35 : 0.45,
            };
            var target = _aurora ? 1.006 : 1.012;
            Animate(scale, ScaleTransform.ScaleXProperty, target, 150);
            Animate(scale, ScaleTransform.ScaleYProperty, target, 150);
        };

        border.MouseLeave += (_, _) =>
        {
            border.Effect = null;
            Animate(scale, ScaleTransform.ScaleXProperty, 1.0, 170);
            Animate(scale, ScaleTransform.ScaleYProperty, 1.0, 170);
        };
    }

    private static void AnimateRotate(RotateTransform rotate, double to, int ms)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd,
        };
        rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

'''
s = s[:old_start] + new + s[old_end:]
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("hover variants added")
