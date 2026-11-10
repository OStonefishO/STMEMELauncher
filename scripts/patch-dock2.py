import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

addition = '''
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
            button.CornerRadius = new CornerRadius(16);
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
            UpdateDock(_currentPage);
        }
        else
        {
            BottomDock.Visibility = Visibility.Collapsed;
            RailPanel.Visibility = Visibility.Visible;
            RailColumn.Width = new GridLength(74);
        }
    }
'''

anchor = "    // ---------------- 极光 ----------------"
if "BuildDock()" not in s:
    s = s.replace(anchor, addition + "\n" + anchor, 1)
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("dock code added")
else:
    print("already")
