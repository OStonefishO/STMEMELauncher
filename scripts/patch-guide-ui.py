import io, re
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

start = s.index("    private Window BuildServerGuideDialog()")
end = s.index("    private async void OnStartServer(object sender, RoutedEventArgs e)")

new = r'''    private Window BuildServerGuideDialog()
    {
        var ink = (Brush)FindResource("Ink");
        var inkDim = (Brush)FindResource("InkDim");
        var inkFaint = (Brush)FindResource("InkFaint");
        var glass = (Brush)FindResource("Glass");
        var glassLine = (Brush)FindResource("GlassLine");
        var accent = (Brush)FindResource("AccentGrad");

        var body = new StackPanel { Margin = new Thickness(2, 0, 2, 4) };

        // 一节的卡片：左边序号徽标 + 标题 + 要点
        void Section(string index, string title, params string[] lines)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF)),
                BorderBrush = glassLine,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 13, 16, 13),
                Margin = new Thickness(0, 0, 0, 10),
            };

            var inner = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            head.Children.Add(new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(7),
                Background = accent,
                Child = new TextBlock
                {
                    Text = index,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });
            head.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = ink,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(9, 0, 0, 0),
            });
            inner.Children.Add(head);

            foreach (var line in lines)
            {
                inner.Children.Add(new TextBlock
                {
                    Text = "·  " + line,
                    FontSize = 12,
                    Foreground = inkDim,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 4),
                    LineHeight = 19,
                });
            }

            card.Child = inner;
            body.Children.Add(card);
        }

        Section("1", "开服",
            "在「版本」里选一个已安装的版本 → 填端口（默认 25565）→ 填内存（2048 够 5 个人）",
            "需要正版服就勾「开启正版验证」；离线玩家一起玩就别勾",
            "勾选「我已阅读并同意 Minecraft EULA」，点「开服」",
            "看到控制台出现 Done (x.xxxs)! 就是起来了");

        Section("2", "让朋友进来（三选一）",
            "IPv6 直连：本机地址里有 IPv6 的话，直接发 [你的IPv6]:端口，最省事",
            "UPnP：点「UPnP 开端口」，成功会给你一个外网 IP:端口",
            "樱花穿透：natfrp.com 建 TCP 隧道指向 127.0.0.1:端口，填访问密钥和隧道 ID，点「启动隧道」");

        Section("3", "朋友怎么连",
            "游戏里「多人游戏」→「添加服务器」→ 把地址粘进去 → 加入",
            "局域网里的朋友也可以直接用 内网IP:端口");

        Section("!", "常见问题",
            "连不上：先看服务端控制台有没有 Connection 记录，没有就是网络没通（防火墙 / 端口 / 映射）",
            "Windows 防火墙：第一次开服会弹窗，记得点「允许访问」，专用和公用都勾上",
            "装了 mod 的服：朋友的 mod 必须和服务端一致，否则会报错退回去",
            "内存不够：看到 Java heap space 就把内存调大，但别超过物理内存的一半",
            "正版验证：离线账号进正版服会报 Invalid session，要么关掉正版验证，要么让朋友用正版");

        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 520,
            Margin = new Thickness(0, 4, 0, 0),
        };

        // 头部：标题 + 关闭
        var title = new TextBlock
        {
            Text = "开服教程",
            FontSize = 20,
            FontFamily = (FontFamily)FindResource("FontHead"),
            Foreground = ink,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var sub = new TextBlock
        {
            Text = "照着做，五分钟能让朋友进你的世界",
            FontSize = 11.5,
            Foreground = inkFaint,
            Margin = new Thickness(0, 5, 0, 0),
        };

        var closeGlyph = new Button
        {
            Content = "✕",
            Width = 30,
            Height = 26,
            FontSize = 11,
            Style = (Style)FindResource("ChromeButton"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var headerGrid = new Grid();
        headerGrid.Children.Add(closeGlyph);
        var headerText = new StackPanel { Margin = new Thickness(0, 0, 34, 0) };
        headerText.Children.Add(title);
        headerText.Children.Add(sub);
        headerGrid.Children.Add(headerText);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = "右上角 ✕ 或点下面按钮关闭",
            FontSize = 10.5,
            Foreground = inkFaint,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var ok = new Button
        {
            Content = "知道了",
            Style = (Style)FindResource("HeroButton"),
            Width = 118,
            Height = 38,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(ok, 1);
        footer.Children.Add(ok);

        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
        root.Children.Add(headerGrid);
        root.Children.Add(scroll);
        root.Children.Add(footer);

        var shell = new Border
        {
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x0B, 0x10, 0x1C)),
            BorderBrush = glassLine,
            BorderThickness = new Thickness(1),
            Child = root,
        };
        shell.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 40,
            ShadowDepth = 0,
            Opacity = 0.6,
        };

        var window = new Window
        {
            Owner = this,
            Title = "开服教程",
            Width = 640,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 860,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            FontFamily = FontFamily,
            Content = new Border { Margin = new Thickness(14), Child = shell },
        };

        // 拖窗 + 关闭
        headerGrid.MouseLeftButtonDown += (s2, e2) =>
        {
            if (e2.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                window.DragMove();
            }
        };
        closeGlyph.Click += (_, _) => window.Close();
        ok.Click += (_, _) => window.Close();
        shell.MouseLeftButtonDown += (_, e2) => { if (e2.ClickCount == 1) window.Activate(); };

        return window;
    }

'''

s = s[:start] + new + s[end:]
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("guide dialog restyled")
