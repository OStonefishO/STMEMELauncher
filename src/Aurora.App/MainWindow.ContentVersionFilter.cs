using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Aurora.App;

/// <summary>「内容 → 内容下载」的版本筛选：可以直接输版本号（比如 1.20.1），也支持折叠分组。</summary>
public partial class MainWindow
{
    /// <summary>筛选模式：跟随启动页选中的版本 / 不按版本过滤 / 指定版本号。</summary>
    private enum ContentVersionMode
    {
        Current,
        All,
        Specific,
    }

    private ContentVersionMode _contentVersionMode = ContentVersionMode.Current;
    private string? _contentVersionValue;

    private readonly List<string> _contentVersionSuggestions = new();
    private readonly HashSet<string> _contentVersionInstalled = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _contentVersionExpanded = new(StringComparer.OrdinalIgnoreCase);
    private TextBlock? _cvCountText;

    private Popup? _cvPopup;
    private Border? _cvAnchor;
    private Border? _cvCard;
    private TextBlock? _cvLabel;
    private TextBox? _cvSearch;
    private TextBlock? _cvSearchHint;
    private StackPanel? _cvRows;
    private ScrollViewer? _cvScroll;
    private Border? _cvGroupBorder;
    private TextBlock? _cvGroupText;

    private string _cvQuery = string.Empty;
    private bool _cvGrouped = true;
    private bool _cvManifestRequested;

    // ---------------- 结构 ----------------

    private void BuildContentVersionFilter()
    {
        if (ContentVersionHost == null || _cvPopup != null)
        {
            return;
        }

        var anchor = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = (Brush)FindResource("FieldBg"),
            BorderBrush = (Brush)FindResource("FieldLine"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Height = 36,
            ToolTip = "点开可以直接输版本号，比如 1.20.1",
        };

        var anchorGrid = new Grid { Margin = new Thickness(12, 0, 10, 0) };
        anchorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        anchorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _cvLabel = new TextBlock
        {
            Text = "跟随当前版本",
            FontSize = 11.5,
            Foreground = (Brush)FindResource("Ink"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        anchorGrid.Children.Add(_cvLabel);

        var caret = new TextBlock
        {
            Text = "▾",
            FontSize = 10,
            Foreground = (Brush)FindResource("InkDim"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };
        Grid.SetColumn(caret, 1);
        anchorGrid.Children.Add(caret);

        anchor.Child = anchorGrid;
        anchor.MouseEnter += (_, __) =>
        {
            anchor.BorderBrush = (Brush)FindResource("PopAccent");
        };
        anchor.MouseLeave += (_, __) =>
        {
            anchor.BorderBrush = (Brush)FindResource("FieldLine");
        };
        anchor.MouseLeftButtonUp += (_, __) => ToggleContentVersionPopup();
        _cvAnchor = anchor;

        ContentVersionHost.Children.Add(anchor);

        // 弹层
        _cvCard = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = (Brush)FindResource("PopBg"),
            BorderBrush = (Brush)FindResource("PopLine"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Width = 300,
            Margin = new Thickness(0, 6, 0, 0),
        };
        _cvCard.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 22,
            ShadowDepth = 6,
            Opacity = 0.45,
        };

        var root = new StackPanel();
        _cvCard.Child = root;

        // 搜索框
        var shell = new Border
        {
            CornerRadius = new CornerRadius(9),
            Background = (Brush)FindResource("PopField"),
            BorderBrush = (Brush)FindResource("PopLine"),
            BorderThickness = new Thickness(1),
            Height = 34,
            Margin = new Thickness(1, 0, 1, 8),
        };

        var grid = new Grid();
        shell.Child = grid;

        _cvSearch = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (Brush)FindResource("PopInk"),
            CaretBrush = (Brush)FindResource("PopAccent"),
            FontSize = 12,
            Padding = new Thickness(11, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null,
        };
        _cvSearch.TextChanged += OnContentVersionQueryChanged;
        _cvSearch.PreviewKeyDown += OnContentVersionQueryKeyDown;
        grid.Children.Add(_cvSearch);

        _cvSearchHint = new TextBlock
        {
            Text = "输入版本号，如 1.20.1",
            FontSize = 11.5,
            Foreground = (Brush)FindResource("PopFaint"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(11, 0, 10, 0),
            IsHitTestVisible = false,
        };
        grid.Children.Add(_cvSearchHint);
        root.Children.Add(shell);

        // 折叠开关
        var tools = new Grid { Margin = new Thickness(1, 0, 1, 2) };
        tools.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tools.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tip = new TextBlock
        {
            Text = "共 0 个版本",
            FontSize = 10,
            Foreground = (Brush)FindResource("PopFaint"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _cvCountText = tip;
        tools.Children.Add(tip);

        _cvGroupBorder = new Border
        {
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 3, 8, 3),
            Cursor = Cursors.Hand,
            ToolTip = "按大版本折叠 / 展开",
        };
        _cvGroupText = new TextBlock
        {
            Text = "按大版本折叠",
            FontSize = 10,
            Foreground = (Brush)FindResource("PopSub"),
        };
        _cvGroupBorder.Child = _cvGroupText;
        _cvGroupBorder.MouseLeftButtonUp += (_, __) =>
        {
            _cvGrouped = !_cvGrouped;
            PaintContentVersionChrome();
            RenderContentVersionRows();
            _cvSearch?.Focus();
        };
        Grid.SetColumn(_cvGroupBorder, 1);
        tools.Children.Add(_cvGroupBorder);

        root.Children.Add(tools);

        _cvRows = new StackPanel { Margin = new Thickness(0, 6, 0, 10) };
        _cvScroll = new ScrollViewer
        {
            Height = 300,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _cvRows,
        };

        var scrollHost = new Grid();
        scrollHost.Children.Add(_cvScroll);
        scrollHost.Children.Add(BuildPopFade());
        root.Children.Add(scrollHost);

        _cvPopup = new Popup
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = _cvCard,
        };
        _cvPopup.Opened += (_, __) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _cvSearch?.Focus();
                Keyboard.Focus(_cvSearch);
            }), DispatcherPriority.Input);
        };

        PaintContentVersionChrome();
        RefreshContentVersionLabel();
    }

    /// <summary>换主题后重新上色（弹层颜色跟 Pop* 资源走，行是代码建的所以重建一遍）。</summary>
    private void RefreshContentVersionFilterTheme()
    {
        if (_cvCard == null || _cvPopup == null)
        {
            return;
        }

        _cvCard.Background = (Brush)FindResource("PopBg");
        _cvCard.BorderBrush = (Brush)FindResource("PopLine");

        var query = _cvSearch?.Text ?? string.Empty;
        ContentVersionHost?.Children.Clear();
        _cvPopup = null;
        _cvRows = null;
        BuildContentVersionFilter();
        if (_cvSearch != null)
        {
            _cvSearch.Text = query;
        }

        RenderContentVersionRows();
    }

    private void PaintContentVersionChrome()
    {
        if (_cvGroupBorder == null || _cvGroupText == null || _cvSearch == null)
        {
            return;
        }

        _cvGroupBorder.Background = _cvGrouped ? (Brush)FindResource("PopActive") : Brushes.Transparent;
        _cvGroupBorder.BorderBrush = _cvGrouped ? (Brush)FindResource("PopAccent") : (Brush)FindResource("PopLine");
        _cvGroupText.Foreground = _cvGrouped ? (Brush)FindResource("PopInk") : (Brush)FindResource("PopSub");

        _cvSearch.Foreground = (Brush)FindResource("PopInk");
        _cvSearch.CaretBrush = (Brush)FindResource("PopAccent");
        if (_cvSearchHint != null)
        {
            _cvSearchHint.Foreground = (Brush)FindResource("PopFaint");
        }
    }

    // ---------------- 数据 ----------------

    /// <summary>启动时先塞入本机已装的 MC 版本，等清单拉回来再补全。</summary>
    private void SeedContentVersionFilter(IEnumerable<string> installed)
    {
        foreach (var version in installed)
        {
            _contentVersionInstalled.Add(version);
            if (!_contentVersionSuggestions.Contains(version))
            {
                _contentVersionSuggestions.Add(version);
            }
        }

        _contentVersionSuggestions.Sort((x, y) => CompareContentVersion(y, x));
        RefreshContentVersionLabel();
    }

    /// <summary>拉一次 Mojang 清单，把「所有正式版」都放进下拉，玩家想下哪个版本就搜哪个。</summary>
    private async Task EnsureContentVersionsAsync()
    {
        if (_cvManifestRequested)
        {
            return;
        }

        _cvManifestRequested = true;
        try
        {
            var installer = _service.CreateInstaller();
            var all = await installer.GetManifestAsync();
            var releases = all
                .Where(v => string.Equals(v.Type, "release", StringComparison.OrdinalIgnoreCase))
                .Select(v => v.Id)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            foreach (var version in releases)
            {
                if (!_contentVersionSuggestions.Contains(version))
                {
                    _contentVersionSuggestions.Add(version);
                }
            }

            _contentVersionSuggestions.Sort((x, y) => CompareContentVersion(y, x));
            if (_cvPopup?.IsOpen == true)
            {
                RenderContentVersionRows();
            }
        }
        catch
        {
            // 离线也不影响手输版本号
            _cvManifestRequested = false;
        }
    }

    // ---------------- 打开 / 应用 ----------------

    private void ToggleContentVersionPopup()
    {
        if (_cvPopup == null)
        {
            return;
        }

        if (_cvPopup.IsOpen)
        {
            _cvPopup.IsOpen = false;
            return;
        }

        _cvQuery = string.Empty;
        if (_cvSearch != null)
        {
            _cvSearch.Text = string.Empty;
        }

        // 默认只展开当前用到的那个大版本，其余折叠着，九百多个版本也不卡
        _contentVersionExpanded.Clear();
        var current = _contentVersionValue ?? (VersionList.SelectedItem as VersionCard)?.Summary.ClientVersion;
        if (!string.IsNullOrWhiteSpace(current))
        {
            _contentVersionExpanded.Add(FamilyKey(current!));
        }

        RenderContentVersionRows();
        _cvPopup.IsOpen = true;
        _ = EnsureContentVersionsAsync();
    }

    private void ApplyContentVersionSelection(ContentVersionMode mode, string? value)
    {
        _contentVersionMode = mode;
        _contentVersionValue = value;
        RefreshContentVersionLabel();

        if (_cvPopup != null)
        {
            _cvPopup.IsOpen = false;
        }

        _contentOffset = 0;
        _ = LoadContentPageAsync();
    }

    private void RefreshContentVersionLabel()
    {
        if (_cvLabel == null)
        {
            return;
        }

        _cvLabel.Text = _contentVersionMode switch
        {
            ContentVersionMode.All => "全部版本",
            ContentVersionMode.Specific => _contentVersionValue ?? "全部版本",
            _ => (VersionList.SelectedItem as VersionCard) is { } card
                ? $"跟随当前 · {card.Summary.ClientVersion}"
                : "跟随当前版本",
        };
    }

    // ---------------- 行 ----------------

    private void RenderContentVersionRows()
    {
        if (_cvRows == null)
        {
            return;
        }

        _cvRows.Children.Clear();

        var query = _cvQuery.Trim();
        var exact = _contentVersionSuggestions
            .FirstOrDefault(v => string.Equals(v, query, StringComparison.OrdinalIgnoreCase));

        // 前两项：跟随当前 / 全部
        if (query.Length == 0)
        {
            AddContentVersionChoice("跟随当前版本", CurrentVersionCaption(), ContentVersionMode.Current, null);
            AddContentVersionChoice("全部版本", "不按版本过滤，所有版本的内容都列出来", ContentVersionMode.All, null);
            AddContentVersionDivider();
        }

        // 手输的版本（清单里没有也能用）
        if (query.Length > 0 && exact == null && LooksLikeVersion(query))
        {
            AddContentVersionChoice($"用「{Trim(query, 22)}」筛选",
                "不在清单里也照样按这个版本号去搜", ContentVersionMode.Specific, query, accent: true);
        }

        var items = _contentVersionSuggestions
            .Where(v => query.Length == 0
                        || v.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || FamilyKey(v).Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (items.Count == 0)
        {
            if (query.Length == 0)
            {
                AddContentVersionNote("正在获取版本清单…也可以直接输入版本号");
            }
            else if (!LooksLikeVersion(query))
            {
                AddContentVersionNote($"没有叫「{Trim(query, 18)}」的版本，试试 1.20.1 这样的写法");
            }

            return;
        }

        if (_cvCountText != null)
        {
            _cvCountText.Text = query.Length == 0
                ? $"共 {items.Count} 个版本"
                : $"匹配 {items.Count} 个";
        }
        // 分组：1.21 / 1.20 / 1.16 …（搜索时全展开）
        if (_cvGrouped && query.Length == 0)
        {
            foreach (var group in items.GroupBy(FamilyKey).OrderByDescending(g => g.Key, Comparer<string>.Create(CompareContentVersion)))
            {
                var collapsed = !_contentVersionExpanded.Contains(group.Key);
                AddContentVersionGroupHeader(group.Key, group.Count(), collapsed);
                if (collapsed)
                {
                    continue;
                }

                foreach (var version in group)
                {
                    AddContentVersionRow(version);
                }
            }
        }
        else
        {
            // 平铺时最多列 150 条，剩下的靠搜索，不然九百多个版本会卡
            foreach (var version in items.Take(150))
            {
                AddContentVersionRow(version);
            }

            if (items.Count > 150)
            {
                AddContentVersionNote($"只列了前 150 个，还有 {items.Count - 150} 个；直接输版本号更快");
            }
        }

        // 内容变了要叫弹层重新量一次，不然窗口高度会停在打开时的那一版
        NudgePopup(_cvPopup);
    }

    /// <summary>Popup 内容高度变了它不会自己重排，轻推一下偏移量逼它重新量。</summary>
    private static void NudgePopup(Popup? popup)
    {
        if (popup is not { IsOpen: true })
        {
            return;
        }

        var vertical = popup.VerticalOffset;
        popup.VerticalOffset = vertical + 0.4;
        popup.VerticalOffset = vertical;
    }

    private string CurrentVersionCaption()
    {
        var current = (VersionList.SelectedItem as VersionCard)?.Summary.ClientVersion;
        return string.IsNullOrWhiteSpace(current) ? "用启动页选中的那个版本" : $"用启动页选中的版本（MC {current}）";
    }

    private void AddContentVersionChoice(string title, string caption, ContentVersionMode mode, string? value, bool accent = false)
    {
        var selected = _contentVersionMode == mode
                       && (mode != ContentVersionMode.Specific
                           || string.Equals(_contentVersionValue, value, StringComparison.OrdinalIgnoreCase));

        var panel = new StackPanel { Margin = new Thickness(10, 6, 10, 6) };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = accent ? (Brush)FindResource("PopAccent") : (Brush)FindResource("PopInk"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        panel.Children.Add(new TextBlock
        {
            Text = caption,
            FontSize = 10,
            Foreground = (Brush)FindResource("PopSub"),
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = selected ? (Brush)FindResource("PopActive") : Brushes.Transparent,
            Child = panel,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 1, 0, 1),
        };
        border.MouseEnter += (_, __) =>
        {
            if (!selected)
            {
                border.Background = (Brush)FindResource("PopHover");
            }
        };
        border.MouseLeave += (_, __) =>
        {
            if (!selected)
            {
                border.Background = Brushes.Transparent;
            }
        };
        border.MouseLeftButtonUp += (_, __) => ApplyContentVersionSelection(mode, value);
        _cvRows!.Children.Add(border);
    }

    private void AddContentVersionRow(string version)
    {
        var selected = _contentVersionMode == ContentVersionMode.Specific
                       && string.Equals(_contentVersionValue, version, StringComparison.OrdinalIgnoreCase);

        var panel = new StackPanel { Margin = new Thickness(12, 5, 10, 5), Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = version,
            FontSize = 12,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = (Brush)FindResource("PopInk"),
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (_contentVersionInstalled.Contains(version))
        {
            var pill = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = (Brush)FindResource("PopActive"),
                Padding = new Thickness(5, 0, 5, 1),
                Margin = new Thickness(7, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            pill.Child = new TextBlock
            {
                Text = "已装",
                FontSize = 8.5,
                Foreground = (Brush)FindResource("PopAccent"),
            };
            panel.Children.Add(pill);
        }

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = selected ? (Brush)FindResource("PopActive") : Brushes.Transparent,
            Child = panel,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 1),
        };
        border.MouseEnter += (_, __) =>
        {
            if (!selected)
            {
                border.Background = (Brush)FindResource("PopHover");
            }
        };
        border.MouseLeave += (_, __) =>
        {
            if (!selected)
            {
                border.Background = Brushes.Transparent;
            }
        };
        border.MouseLeftButtonUp += (_, __) => ApplyContentVersionSelection(ContentVersionMode.Specific, version);

        _cvRows!.Children.Add(border);
    }

    private void AddContentVersionGroupHeader(string family, int count, bool collapsed)
    {
        var row = new Grid { Margin = new Thickness(6, 8, 6, 2), Cursor = Cursors.Hand, Background = Brushes.Transparent };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var caret = new TextBlock
        {
            Text = collapsed ? "▸" : "▾",
            FontSize = 9,
            Foreground = (Brush)FindResource("PopFaint"),
            Width = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(caret);

        var label = new TextBlock
        {
            Text = $"MC {family}",
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("PopSub"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        var line = new Border
        {
            Height = 1,
            Background = (Brush)FindResource("PopLine"),
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        };
        Grid.SetColumn(line, 2);
        row.Children.Add(line);

        row.MouseLeftButtonUp += (_, __) =>
        {
            if (!_contentVersionExpanded.Add(family))
            {
                _contentVersionExpanded.Remove(family);
            }

            RenderContentVersionRows();
        };
        row.MouseEnter += (_, __) => label.Foreground = (Brush)FindResource("PopInk");
        row.MouseLeave += (_, __) => label.Foreground = (Brush)FindResource("PopSub");

        _cvRows!.Children.Add(row);
    }

    private void AddContentVersionDivider()
    {
        _cvRows!.Children.Add(new Border
        {
            Height = 1,
            Background = (Brush)FindResource("PopLine"),
            Opacity = 0.7,
            Margin = new Thickness(6, 6, 6, 4),
        });
    }

    private void AddContentVersionNote(string text)
    {
        _cvRows!.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 10.5,
            Foreground = (Brush)FindResource("PopSub"),
            Margin = new Thickness(8, 10, 8, 12),
            TextWrapping = TextWrapping.Wrap,
        });
    }

    // ---------------- 输入 ----------------

    private void OnContentVersionQueryChanged(object sender, TextChangedEventArgs e)
    {
        _cvQuery = _cvSearch?.Text ?? string.Empty;
        if (_cvSearchHint != null)
        {
            _cvSearchHint.Visibility = _cvQuery.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        RenderContentVersionRows();
    }

    private void OnContentVersionQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var query = _cvQuery.Trim();
            if (query.Length > 0)
            {
                var exact = _contentVersionSuggestions
                    .FirstOrDefault(v => string.Equals(v, query, StringComparison.OrdinalIgnoreCase));
                ApplyContentVersionSelection(ContentVersionMode.Specific, exact ?? query);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_cvQuery.Length > 0 && _cvSearch != null)
            {
                _cvSearch.Text = string.Empty;
            }
            else if (_cvPopup != null)
            {
                _cvPopup.IsOpen = false;
            }

            e.Handled = true;
        }
    }

    private static bool LooksLikeVersion(string text) =>
        text.Length > 0 && Regex.IsMatch(text, @"^\d+([.\-_]\d+)*[a-zA-Z0-9.\-_]*$");

    /// <summary>1.21.11 → 1.21；26.3 → 26.3；1.7.10 → 1.7。</summary>
    private static string FamilyKey(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : parts[0];
    }

    private static int CompareContentVersion(string? a, string? b)
    {
        var left = Regex.Matches(a ?? string.Empty, @"\d+").Select(m => int.TryParse(m.Value, out var v) ? v : 0).ToList();
        var right = Regex.Matches(b ?? string.Empty, @"\d+").Select(m => int.TryParse(m.Value, out var v) ? v : 0).ToList();
        var length = Math.Max(left.Count, right.Count);

        for (var i = 0; i < length; i++)
        {
            var x = i < left.Count ? left[i] : 0;
            var y = i < right.Count ? right[i] : 0;
            if (x != y)
            {
                return x.CompareTo(y);   // 升序比较，展示时再倒过来
            }
        }

        return string.CompareOrdinal(a, b);
    }
}
