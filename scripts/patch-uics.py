import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) GridLengthAnimation 构造函数：To 是 GridLength?
s = s.replace("""    public GridLengthAnimation(DoubleAnimation inner)
    {
        To = inner.To;
        Duration = inner.Duration;
        EasingFunction = inner.EasingFunction;
    }""", """    public GridLengthAnimation(double to, Duration duration, IEasingFunction? easing)
    {
        To = new GridLength(to);
        Duration = duration;
        EasingFunction = easing;
    }""")

# 2) 调用处改成新签名
s = s.replace("""        var widthAnim = new DoubleAnimation(expanded ? 178 : 74, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        RailColumn.BeginAnimation(ColumnDefinition.WidthProperty, new GridLengthAnimation(widthAnim));""",
"""        var gridAnim = new GridLengthAnimation(
            expanded ? 178 : 74,
            TimeSpan.FromMilliseconds(180),
            new CubicEase { EasingMode = EasingMode.EaseOut });
        RailColumn.BeginAnimation(ColumnDefinition.WidthProperty, gridAnim);""")

# 3) ConfigStore 命名空间
s = s.replace("using System.Windows.Shapes;", "using System.Windows.Shapes;\nusing Aurora.Core.Configuration;")
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("ui.cs fixed")
