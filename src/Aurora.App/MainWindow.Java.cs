using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aurora.Core.Java;

namespace Aurora.App;

public partial class MainWindow
{
    /// <summary>设置里那个 Java 下拉框的一项。</summary>
    private sealed class JavaOption
    {
        public string Text { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public int Major { get; init; }

        public override string ToString() => Text;
    }

    private List<JavaOption> _javaOptions = new();

    /// <summary>扫描本机 Java 并填进设置页的下拉框。</summary>
    private async Task RefreshJavaOptionsAsync(string? keepPath = null)
    {
        if (SettingsJavaCombo == null)
        {
            return;
        }

        var wanted = keepPath ?? _settings.JavaPath ?? string.Empty;
        if (JavaScanStatus != null)
        {
            JavaScanStatus.Text = "正在扫描…";
        }

        IReadOnlyList<JavaRuntime> found;
        try
        {
            found = await Task.Run(() => _service.ScanJava());
        }
        catch
        {
            found = Array.Empty<JavaRuntime>();
        }

        var options = new List<JavaOption>
        {
            new() { Text = "自动（按游戏版本挑合适的）", Path = string.Empty, Major = 0 },
        };

        foreach (var java in found)
        {
            options.Add(new JavaOption
            {
                Text = $"Java {java.Major} · {java.VersionText} · {ShortenPath(java.JavaPath)}",
                Path = java.JavaPath,
                Major = java.Major,
            });
        }

        _javaOptions = options;
        SettingsJavaCombo.ItemsSource = options;

        var index = options.FindIndex(o =>
            !string.IsNullOrEmpty(wanted) && string.Equals(o.Path, wanted, StringComparison.OrdinalIgnoreCase));
        SettingsJavaCombo.SelectedIndex = index >= 0 ? index : 0;

        if (JavaScanStatus != null)
        {
            JavaScanStatus.Text = found.Count == 0
                ? "一个都没找到，点「手动添加目录」指到 java.exe 所在的 bin 上一层"
                : $"找到 {found.Count} 个：" +
                  string.Join(" / ", found.Select(f => f.Major).Distinct().OrderBy(m => m).Select(m => "Java " + m));
        }
    }

    private static string ShortenPath(string path)
    {
        try
        {
            var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 3)
            {
                return path;
            }

            return "…\\" + string.Join("\\", parts.Skip(parts.Length - 3));
        }
        catch
        {
            return path;
        }
    }

    private async void OnScanJava(object sender, RoutedEventArgs e)
    {
        await RefreshJavaOptionsAsync();
        ShowToast("Java 扫描完成", JavaScanStatus?.Text ?? string.Empty, "✓", 3600);
    }

    private async void OnAddJavaDir(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选 Java 的安装目录（里面要有 bin\\java.exe）",
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var dir = dialog.SelectedPath;
        // 玩家可能直接选到 bin 目录，往上退一层
        if (string.Equals(Path.GetFileName(dir.TrimEnd('\\')), "bin", StringComparison.OrdinalIgnoreCase))
        {
            dir = Path.GetDirectoryName(dir.TrimEnd('\\')) ?? dir;
        }

        if (!_settings.ExtraJavaPaths.Contains(dir, StringComparer.OrdinalIgnoreCase))
        {
            _settings.ExtraJavaPaths.Add(dir);
        }

        Aurora.Core.Configuration.ConfigStore.Save(_settings);
        await RefreshJavaOptionsAsync();

        var match = _javaOptions.FirstOrDefault(o =>
            string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(o.Path) ?? string.Empty) ?? string.Empty,
                dir, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            SettingsJavaCombo.SelectedItem = match;
            ShowToast("已添加 Java", match.Text, "✓", 3600);
        }
        else
        {
            ShowToast("没认出这个目录", "这里没有 bin\\java.exe，换一个目录试试", "⚠", 4200);
        }
    }

    /// <summary>
    /// 启动前决定用哪个 Java：优先玩家手选的，其次按版本自动挑。
    /// 返回 (运行时, 需要提醒的话)；两个都为 null 表示一个 Java 都没有。
    /// </summary>
    private (JavaRuntime? Runtime, string? Warning) ResolveJavaForLaunch(Aurora.Core.Models.ResolvedVersion version)
    {
        var all = _service.ScanJava();

        JavaRuntime? chosen = null;
        if (!string.IsNullOrWhiteSpace(_settings.JavaPath))
        {
            chosen = all.FirstOrDefault(j =>
                string.Equals(j.JavaPath, _settings.JavaPath, StringComparison.OrdinalIgnoreCase));
        }

        chosen ??= _service.SelectJava(version.JavaMajor, out _);

        if (chosen == null)
        {
            return (null, null);
        }

        if (chosen.Major != version.JavaMajor)
        {
            var level = chosen.Major < version.JavaMajor ? "太旧" : "太新";
            return (chosen,
                $"这个版本需要 Java {version.JavaMajor}，你现在用的是 Java {chosen.Major}（{level}）。\n" +
                $"继续启动多半会直接崩掉，也可能是模组报错。\n\n" +
                $"要用的 Java：{chosen.JavaPath}\n\n" +
                "仍然用它启动吗？（设置里可以换一个 Java，或者重新扫描）");
        }

        return (chosen, null);
    }

    /// <summary>主页那几个 Java 控件：自动 / 手动、扫描、选 java.exe。</summary>
    private void UpdateHomeJavaUi()
    {
        if (JavaModeButton == null)
        {
            return;
        }

        var manual = !string.IsNullOrWhiteSpace(_settings.JavaPath);
        JavaModeButton.Content = manual ? "Java：手动" : "Java：自动";
        JavaModeButton.Style = (Style)FindResource(manual ? "HeroButton" : "GlassButton");
        JavaModeButton.Padding = new Thickness(12, 5, 12, 5);

        if (HomeJavaHint == null)
        {
            return;
        }

        var summary = (VersionList?.SelectedItem as VersionCard)?.Summary;
        if (summary == null)
        {
            HomeJavaHint.Text = string.Empty;
            return;
        }

        var needed = summary.JavaMajor;
        JavaRuntime? java;
        if (manual)
        {
            java = _service.ScanJava().FirstOrDefault(j =>
                string.Equals(j.JavaPath, _settings.JavaPath, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            java = _service.SelectJava(needed, out _);
        }

        if (java == null)
        {
            HomeJavaHint.Text = manual ? "指定的 java.exe 不在了" : $"没找到 Java {needed}";
            if (DetJava != null)
            {
                DetJava.Text = manual ? "路径无效" : $"缺少 Java {needed}";
            }

            return;
        }

        if (DetJava != null)
        {
            DetJava.Text = java.Major == needed
                ? $"Java {java.Major} · {java.VersionText}"
                : $"Java {java.Major}（需要 {needed}）";
        }

        HomeJavaHint.Text = java.Major == needed
            ? (manual ? "手动 · " + ShortenPath(java.JavaPath) : $"自动匹配到 Java {java.Major}")
            : $"这个版本要 Java {needed}，现在是 {java.Major}，启动前会问你";
    }

    private async void OnHomeScanJava(object sender, RoutedEventArgs e)
    {
        var found = await Task.Run(() => _service.ScanJava());
        await RefreshJavaOptionsAsync(_settings.JavaPath);
        UpdateHomeJavaUi();

        var text = found.Count == 0
            ? "没找到 Java，可以手动选 java.exe"
            : "找到 " + found.Count + " 个：" +
              string.Join(" / ", found.Select(f => f.Major).Distinct().OrderBy(m => m).Select(m => "Java " + m));
        ShowToast("Java 扫描完成", text, "✓", 3800);
    }

    private async void OnHomePickJava(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选一个 java.exe（一般在 jdk\\bin 或 jre\\bin 里）",
            Filter = "Java 可执行文件|java.exe;javaw.exe|所有文件|*.*",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _settings.JavaPath = dialog.FileName;
        Aurora.Core.Configuration.ConfigStore.Save(_settings);
        await RefreshJavaOptionsAsync(_settings.JavaPath);
        UpdateHomeJavaUi();
        ShowToast("已指定 Java", ShortenPath(dialog.FileName), "✓", 3200);
    }

    private void OnHomeToggleJavaMode(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_settings.JavaPath))
        {
            _settings.JavaPath = string.Empty;
        }
        else
        {
            var summary = (VersionList?.SelectedItem as VersionCard)?.Summary;
            var java = summary == null
                ? _service.ScanJava().FirstOrDefault()
                : _service.SelectJava(summary.JavaMajor, out _);

            if (java == null)
            {
                ShowToast("先扫描 Java", "一个都没找到，点「扫描 Java」或者直接选 java.exe", "⚠", 4000);
                return;
            }

            _settings.JavaPath = java.JavaPath;
        }

        Aurora.Core.Configuration.ConfigStore.Save(_settings);
        UpdateHomeJavaUi();
    }

    /// <summary>简单的两按钮确认框，和整体深色风格一致。</summary>
    private bool Confirm(string title, string message, string okText, string cancelText)
    {
        var window = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = (Brush)FindResource("Bg"),
            ShowInTaskbar = false,
        };

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Ink"),
        });
        panel.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = (Brush)FindResource("InkDim"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 18),
            LineHeight = 19,
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var cancel = new Button
        {
            Content = cancelText,
            Style = (Style)FindResource("GlassButton"),
            MinWidth = 96,
            Height = 36,
            IsCancel = true,
        };
        cancel.Click += (_, __) => window.DialogResult = false;

        var ok = new Button
        {
            Content = okText,
            Style = (Style)FindResource("HeroButton"),
            MinWidth = 96,
            Height = 36,
            Margin = new Thickness(8, 0, 0, 0),
        };
        ok.Click += (_, __) => window.DialogResult = true;

        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);

        window.Content = panel;
        return window.ShowDialog() == true;
    }
}
