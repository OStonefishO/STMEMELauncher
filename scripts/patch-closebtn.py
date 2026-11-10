import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace('''        var closeGlyph = new Button
        {
            Content = "✕",
            Width = 30,
            Height = 26,
            FontSize = 11,
            Style = (Style)FindResource("ChromeButton"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };''', '''        var closeGlyph = new Button
        {
            Content = "✕",
            Width = 32,
            Height = 28,
            FontSize = 12,
            Cursor = System.Windows.Input.Cursors.Hand,
            Foreground = inkDim,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Template = BuildFlatButtonTemplate(),
        };''')

# 加模板构造方法
if "BuildFlatButtonTemplate" not in s.split("private Window BuildServerGuideDialog")[0]:
    s = s.replace("    private Window BuildServerGuideDialog()", '''    /// <summary>一个扁平、无边框、悬停变亮的按钮模板（给对话框的 ✕ 用）。</summary>
    private static ControlTemplate BuildFlatButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), ""));
        template.Triggers.Add(hover);
        return template;
    }

    private Window BuildServerGuideDialog()''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("close button restyled")
