using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Aurora.App;

/// <summary>
/// 版本选择弹层：搜索框（直接输版本号）+ 筛选 chip + 按 MC 版本折叠分组。
/// 六套界面共用这一套结构，颜色全部走 Pop* 资源，所以每套主题都自动贴合自己的配色。
/// </summary>
public partial class MainWindow
{
    private readonly List<VersionCard> _pickerCards = new();
    private readonly List<VersionCard> _pickerVisible = new();
    private readonly List<Border> _pickerChipBorders = new();
    private readonly List<TextBlock> _pickerChipTexts = new();
    private readonly Dictionary<string, Border> _pickerRowViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pickerCollapsed = new(StringComparer.OrdinalIgnoreCase);

    private StackPanel? _pickerRows;
    private ScrollViewer? _pickerScroll;
    private TextBox? _pickerSearchBox;
    private TextBlock? _pickerSearchHint;
    private TextBlock? _pickerClear;
    private TextBlock? _pickerCount;
    private TextBlock? _pickerGroupText;
    private Border? _pickerGroupBorder;

    private string _pickerQuery = string.Empty;
    private int _pickerFilter;
    private bool _pickerGrouped = true;
    private string? _pickerHighlight;

    private static readonly string[] _pickerFilterNames = { "全部", "原版", "带加载器", "带模组" };

    // ---------------- 主题 ----------------

    /// <summary>按当前主题写一套弹层配色（每套界面都不一样，纸质是米白，终端是墨绿…）。</summary>
    private void ApplyPopTheme()
    {
        var res = Resources;

        // bg / field / line / hover / active / ink / sub / faint / accent
        var p = _style switch
        {
            // 纸质便签：米白纸面 + 深棕墨 + 陶土橙
            "paper" => (B: "#FFFBF3F0", F: "#142B2622", L: "#E6D8C0", H: "#14C4553A", A: "#26C4553A",
                        I: "#2B2622", S: "#6E6152", O: "#9A8C7A", C: "#C4553A"),
            // 像素 8bit：黑底 + NES 主色
            "pixel" => (B: "#FF101020", F: "#1AF8F8F8", L: "#FF58D854", H: "#2CF83858", A: "#3C58D854",
                        I: "#F8F8F8", S: "#9C9CB8", O: "#7A7A96", C: "#F83858"),
            // 毛玻璃极光：深蓝底 + 柔光紫
            "aurora" => (B: "#FF1A2A4A", F: "#1AB6C8FF", L: "#4DB6C8FF", H: "#1FB6C8FF", A: "#33B69CFF",
                         I: "#ECF3FF", S: "#A8BBDC", O: "#7488AC", C: "#B69CFF"),
            // 终端：近黑墨绿 + 荧光绿
            "terminal" => (B: "#FF07100C", F: "#1A35F07A", L: "#5535F07A", H: "#1E35F07A", A: "#3035F07A",
                           I: "#C9F7D2", S: "#77C28C", O: "#417A52", C: "#35F07A"),
            // 新版（动感）：紫黑 + 霓虹粉
            "vivid" => (B: "#FF170C28", F: "#16FFFFFF", L: "#45FF5CC8", H: "#1FFFFFFF", A: "#33FF5CC8",
                        I: "#F4F6FF", S: "#ABAECB", O: "#747796", C: "#FF5CC8"),
            // 经典：深蓝灰 + 蓝
            _ => (B: "#FF141822", F: "#14FFFFFF", L: "#33FFFFFF", H: "#1FFFFFFF", A: "#335B8CFF",
                  I: "#E6EAF2", S: "#99A2B5", O: "#6D7588", C: "#5B8CFF"),
        };

        res["PopBg"] = PopBrush(p.B);
        res["PopField"] = PopBrush(p.F);
        res["PopLine"] = PopBrush(p.L);
        res["PopHover"] = PopBrush(p.H);
        res["PopActive"] = PopBrush(p.A);
        res["PopInk"] = PopBrush(p.I);
        res["PopSub"] = PopBrush(p.S);
        res["PopFaint"] = PopBrush(p.O);
        res["PopAccent"] = PopBrush(p.C);

        // 危险按钮（删除版本这种）：浅色主题下用深一档的红，别糊成粉
        var danger = _style switch
        {
            "paper" => (Bg: "#1FC4553A", Hover: "#33C4553A", Line: "#C4553A", Ink: "#A33A24"),
            "pixel" => (Bg: "#2CF83858", Hover: "#40F83858", Line: "#F83858", Ink: "#FFD6DE"),
            "terminal" => (Bg: "#22FF5B5B", Hover: "#38FF5B5B", Line: "#FF8B94", Ink: "#FFC9C9"),
            "aurora" => (Bg: "#26FF7A9C", Hover: "#3CFF7A9C", Line: "#FF9AB8", Ink: "#FFD2DE"),
            "vivid" => (Bg: "#26FF5CC8", Hover: "#3CFF5CC8", Line: "#FF8AD8", Ink: "#FFD8EE"),
            _ => (Bg: "#2EFF5B5B", Hover: "#48FF5B5B", Line: "#7AFF6B5B", Ink: "#FFB4A8"),
        };

        res["DangerBg"] = PopBrush(danger.Bg);
        res["DangerBgHover"] = PopBrush(danger.Hover);
        res["DangerLine"] = PopBrush(danger.Line);
        res["DangerInk"] = PopBrush(danger.Ink);

        // 警示卡（前置模组那种）：浅色主题下要能看清字
        var warn = _style switch
        {
            "paper" => (Bg: "#1FC98A2B", Ink: "#8A5410"),
            "pixel" => (Bg: "#26FCA044", Ink: "#FFD9A0"),
            "terminal" => (Bg: "#1E9CFF6A", Ink: "#D8FFC8"),
            "aurora" => (Bg: "#22FFC46B", Ink: "#FFE0A8"),
            "vivid" => (Bg: "#24FFB44A", Ink: "#FFD79A"),
            _ => (Bg: "#1AFFB44A", Ink: "#FFC87A"),
        };

        res["WarnBg"] = PopBrush(warn.Bg);
        res["WarnInk"] = PopBrush(warn.Ink);
    }

    private static SolidColorBrush PopBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private Brush PopRes(string key) => (Brush)FindResource(key);

    /// <summary>列表底部的淡出遮罩，免得最后一行被切成半截。</summary>
    private Border BuildPopFade()
    {
        var color = ((SolidColorBrush)PopRes("PopBg")).Color;
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, color.R, color.G, color.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, color.R, color.G, color.B), 1));
        brush.Freeze();

        return new Border
        {
            Height = 18,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Background = brush,
        };
    }

    /// <summary>换主题后把弹层重新上色（行都是代码生成的画刷，得重建一遍）。</summary>
    private void RefreshVersionPickerTheme()
    {
        if (VersionPopoverCard == null)
        {
            return;
        }

        VersionPopoverCard.Background = PopRes("PopBg");
        VersionPopoverCard.BorderBrush = PopRes("PopLine");
        if (VersionPopoverCard.Effect is DropShadowEffect shadow)
        {
            shadow.Color = _paper ? Color.FromRgb(0x2B, 0x26, 0x22) : Colors.Black;
            shadow.Opacity = _paper ? 0.26 : 0.5;
        }

        if (_pickerRows == null)
        {
            return;
        }

        var query = _pickerSearchBox?.Text ?? string.Empty;
        RebuildVersionPickerChrome();
        if (_pickerSearchBox != null)
        {
            _pickerSearchBox.Text = query;
        }

        RenderVersionPickerRows();
    }

    // ---------------- 打开 ----------------

    private void OnVersionPopoverOpened(object sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_pickerSearchBox == null)
            {
                return;
            }

            _pickerSearchBox.Focus();
            Keyboard.Focus(_pickerSearchBox);
        }), DispatcherPriority.Input);
    }

    /// <summary>点「版本 ▾」：带上版本列表，重建弹层内容。</summary>
    private void OpenVersionPicker(IEnumerable<VersionCard> cards)
    {
        _pickerCards.Clear();
        _pickerCards.AddRange(cards);
        _pickerQuery = string.Empty;
        _pickerFilter = 0;
        _pickerHighlight = null;
        _pickerCollapsed.Clear();

        EnsureVersionPickerChrome();
        RenderVersionPickerRows();
        UpdateVersionPickerChips();

        VersionPopover.IsOpen = !VersionPopover.IsOpen;
    }

    // ---------------- 结构 ----------------

    private void EnsureVersionPickerChrome()
    {
        if (VersionPopoverRoot == null || _pickerRows != null)
        {
            return;
        }

        var root = VersionPopoverRoot;
        root.Children.Clear();
        _pickerRowViews.Clear();
        _pickerChipBorders.Clear();
        _pickerChipTexts.Clear();

        // 标题行：选择版本 / 计数
        var header = new Grid { Margin = new Thickness(6, 2, 6, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "选择版本",
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = PopRes("PopInk"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(title);

        _pickerCount = new TextBlock
        {
            Text = string.Empty,
            FontSize = 10.5,
            Foreground = PopRes("PopFaint"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 2, 0, 0),
        };
        Grid.SetColumn(_pickerCount, 1);
        header.Children.Add(_pickerCount);
        root.Children.Add(header);

        // 搜索框
        var searchShell = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = PopRes("PopField"),
            BorderBrush = PopRes("PopLine"),
            BorderThickness = new Thickness(1),
            Height = 38,
            Margin = new Thickness(2, 0, 2, 8),
        };

        var searchGrid = new Grid();
        searchShell.Child = searchGrid;

        var glyph = new Grid
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(13, 0, 0, 0),
            IsHitTestVisible = false,
        };
        glyph.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 9,
            Height = 9,
            Stroke = PopRes("PopFaint"),
            StrokeThickness = 1.4,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0),
        });
        glyph.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 9,9 L 13,13"),
            Stroke = PopRes("PopFaint"),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        });
        searchGrid.Children.Add(glyph);

        _pickerSearchBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = PopRes("PopInk"),
            CaretBrush = PopRes("PopAccent"),
            FontSize = 12.5,
            Padding = new Thickness(34, 0, 30, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null,
        };
        _pickerSearchBox.TextChanged += OnPickerQueryChanged;
        _pickerSearchBox.PreviewKeyDown += OnPickerQueryKeyDown;
        searchGrid.Children.Add(_pickerSearchBox);

        _pickerSearchHint = new TextBlock
        {
            Text = "输入版本号，如 1.21.11 / 26.3 / neoforge",
            FontSize = 12,
            Foreground = PopRes("PopFaint"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(34, 0, 30, 0),
            IsHitTestVisible = false,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        searchGrid.Children.Add(_pickerSearchHint);

        _pickerClear = new TextBlock
        {
            Text = "✕",
            FontSize = 11,
            Foreground = PopRes("PopFaint"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 13, 0),
            Cursor = Cursors.Hand,
            Visibility = Visibility.Collapsed,
            ToolTip = "清空搜索",
        };
        _pickerClear.MouseLeftButtonUp += (_, __) =>
        {
            if (_pickerSearchBox != null)
            {
                _pickerSearchBox.Text = string.Empty;
                _pickerSearchBox.Focus();
            }
        };
        searchGrid.Children.Add(_pickerClear);

        root.Children.Add(searchShell);

        // 筛选 chip + 分组开关
        var chipsRow = new Grid { Margin = new Thickness(2, 0, 2, 2) };
        chipsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        chipsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var chips = new WrapPanel { Orientation = Orientation.Horizontal };
        chipsRow.Children.Add(chips);

        for (var i = 0; i < _pickerFilterNames.Length; i++)
        {
            var index = i;
            var border = new Border
            {
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 4, 9, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
            };
            var text = new TextBlock
            {
                Text = _pickerFilterNames[i],
                FontSize = 10.5,
                Foreground = PopRes("PopSub"),
            };
            border.Child = text;
            border.MouseLeftButtonUp += (_, __) =>
            {
                _pickerFilter = index;
                UpdateVersionPickerChips();
                RenderVersionPickerRows();
                _pickerSearchBox?.Focus();
            };
            border.MouseEnter += (_, __) => border.Opacity = 0.82;
            border.MouseLeave += (_, __) => border.Opacity = 1;

            _pickerChipBorders.Add(border);
            _pickerChipTexts.Add(text);
            chips.Children.Add(border);
        }

        _pickerGroupBorder = new Border
        {
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(9, 4, 9, 4),
            Cursor = Cursors.Hand,
            ToolTip = "按 MC 版本折叠 / 展开",
        };
        _pickerGroupText = new TextBlock
        {
            Text = "按 MC 分组",
            FontSize = 10.5,
            Foreground = PopRes("PopSub"),
        };
        _pickerGroupBorder.Child = _pickerGroupText;
        _pickerGroupBorder.MouseLeftButtonUp += (_, __) =>
        {
            _pickerGrouped = !_pickerGrouped;
            _pickerCollapsed.Clear();
            UpdateVersionPickerChips();
            RenderVersionPickerRows();
            _pickerSearchBox?.Focus();
        };
        Grid.SetColumn(_pickerGroupBorder, 1);
        chipsRow.Children.Add(_pickerGroupBorder);

        root.Children.Add(chipsRow);

        // 列表
        _pickerRows = new StackPanel { Margin = new Thickness(0, 6, 0, 8) };
        _pickerScroll = new ScrollViewer
        {
            Height = 296,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _pickerRows,
            Padding = new Thickness(0, 0, 2, 0),
        };

        var pickerScrollHost = new Grid();
        pickerScrollHost.Children.Add(_pickerScroll);
        pickerScrollHost.Children.Add(BuildPopFade());
        root.Children.Add(pickerScrollHost);
    }

    private void RebuildVersionPickerChrome()
    {
        _pickerRows = null;
        _pickerSearchBox = null;
        _pickerSearchHint = null;
        _pickerClear = null;
        EnsureVersionPickerChrome();
    }

    private void UpdateVersionPickerChips()
    {
        for (var i = 0; i < _pickerChipBorders.Count; i++)
        {
            var on = i == _pickerFilter;
            _pickerChipBorders[i].Background = on ? PopRes("PopActive") : Brushes.Transparent;
            _pickerChipBorders[i].BorderBrush = on ? PopRes("PopAccent") : PopRes("PopLine");
            _pickerChipTexts[i].Foreground = on ? PopRes("PopInk") : PopRes("PopSub");
            _pickerChipTexts[i].FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
        }

        if (_pickerGroupBorder != null && _pickerGroupText != null)
        {
            _pickerGroupBorder.Background = _pickerGrouped ? PopRes("PopActive") : Brushes.Transparent;
            _pickerGroupBorder.BorderBrush = _pickerGrouped ? PopRes("PopAccent") : PopRes("PopLine");
            _pickerGroupText.Foreground = _pickerGrouped ? PopRes("PopInk") : PopRes("PopSub");
            _pickerGroupText.FontWeight = _pickerGrouped ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    // ---------------- 行 ----------------

    private void RenderVersionPickerRows()
    {
        if (_pickerRows == null)
        {
            return;
        }

        _pickerRows.Children.Clear();
        _pickerVisible.Clear();
        _pickerRowViews.Clear();

        var query = _pickerQuery.Trim();
        var currentId = (VersionList.SelectedItem as VersionCard)?.Summary.Id;

        var items = _pickerCards
            .Where(PassesPickerFilter)
            .Where(c => MatchesPickerQuery(c, query))
            .ToList();

        if (_pickerCount != null)
        {
            _pickerCount.Text = items.Count == _pickerCards.Count
                ? $"共 {items.Count} 个"
                : $"{items.Count} / {_pickerCards.Count} 个";
        }

        if (items.Count == 0)
        {
            AddPickerEmptyState(query);
            _pickerHighlight = null;
            return;
        }

        // 搜索时永远平铺展开，免得结果藏在折叠组里
        if (_pickerGrouped && query.Length == 0)
        {
            var groups = items
                .GroupBy(c => string.IsNullOrWhiteSpace(c.Summary.ClientVersion) ? "未知" : c.Summary.ClientVersion)
                .OrderByDescending(g => g.Key, Comparer<string>.Create(CompareMcVersion))
                .ToList();

            foreach (var group in groups)
            {
                var collapsed = _pickerCollapsed.Contains(group.Key);
                AddPickerGroupHeader(group.Key, group.Count(), collapsed);

                if (collapsed)
                {
                    continue;
                }

                foreach (var card in OrderForPicker(group, currentId))
                {
                    AddPickerRow(card, currentId);
                }
            }
        }
        else
        {
            foreach (var card in OrderForPicker(items, currentId))
            {
                AddPickerRow(card, currentId);
            }
        }

        // 高亮：优先保留原来的，其次当前版本，最后第一条
        if (_pickerHighlight == null || _pickerVisible.All(c => !SameVersion(c, _pickerHighlight!)))
        {
            _pickerHighlight = _pickerVisible.FirstOrDefault(c => SameVersion(c, currentId!))?.Summary.Id
                                ?? _pickerVisible[0].Summary.Id;
        }

        PaintPickerHighlight();
        NudgePopup(VersionPopover);
    }

    private static IEnumerable<VersionCard> OrderForPicker(IEnumerable<VersionCard> cards, string? currentId) =>
        cards.OrderByDescending(c => SameVersion(c, currentId!))
             .ThenBy(c => c.Summary.Loader == "vanilla" ? 0 : 1)
             .ThenByDescending(c => c.Summary.ModCount)
             .ThenBy(c => c.Summary.Id, StringComparer.OrdinalIgnoreCase);

    private static bool SameVersion(VersionCard card, string? id) =>
        id != null && string.Equals(card.Summary.Id, id, StringComparison.OrdinalIgnoreCase);

    private bool PassesPickerFilter(VersionCard card) => _pickerFilter switch
    {
        1 => string.Equals(card.Summary.Loader, "vanilla", StringComparison.OrdinalIgnoreCase),
        2 => !string.Equals(card.Summary.Loader, "vanilla", StringComparison.OrdinalIgnoreCase),
        3 => card.Summary.ModCount > 0,
        _ => true,
    };

    private static bool MatchesPickerQuery(VersionCard card, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        var haystack = string.Join(' ',
            card.Summary.Id,
            card.Summary.ClientVersion,
            card.Summary.Loader,
            card.Summary.LoaderVersion,
            card.Subtitle);

        foreach (var token in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (haystack.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private void AddPickerEmptyState(string query)
    {
        var panel = new StackPanel { Margin = new Thickness(8, 18, 8, 20) };
        panel.Children.Add(new TextBlock
        {
            Text = "没有匹配的版本",
            FontSize = 12.5,
            Foreground = PopRes("PopInk"),
        });
        panel.Children.Add(new TextBlock
        {
            Text = query.Length > 0
                ? $"「{Trim(query, 24)}」没找到；试试只输 1.21 或 26.3，或者用上面的筛选项"
                : "先切回「全部」，或者去「版本管理」装一个新版本",
            FontSize = 10.5,
            Foreground = PopRes("PopSub"),
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
        _pickerRows!.Children.Add(panel);
    }

    private void AddPickerGroupHeader(string name, int count, bool collapsed)
    {
        var row = new Grid { Margin = new Thickness(2, 10, 2, 2), Cursor = Cursors.Hand, Background = Brushes.Transparent };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var caret = new TextBlock
        {
            Text = collapsed ? "▸" : "▾",
            FontSize = 9,
            Foreground = PopRes("PopFaint"),
            VerticalAlignment = VerticalAlignment.Center,
            Width = 13,
        };
        row.Children.Add(caret);

        var label = new TextBlock
        {
            Text = $"MC {name}",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = PopRes("PopSub"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        var badge = new TextBlock
        {
            Text = count.ToString(),
            FontSize = 9.5,
            Foreground = PopRes("PopFaint"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(7, 1, 0, 0),
        };
        Grid.SetColumn(badge, 2);
        row.Children.Add(badge);

        var line = new Border
        {
            Height = 1,
            Background = PopRes("PopLine"),
            Opacity = 0.55,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        };
        Grid.SetColumn(line, 3);
        row.Children.Add(line);

        row.MouseLeftButtonUp += (_, __) =>
        {
            if (!_pickerCollapsed.Add(name))
            {
                _pickerCollapsed.Remove(name);
            }

            RenderVersionPickerRows();
        };
        row.MouseEnter += (_, __) => label.Foreground = PopRes("PopInk");
        row.MouseLeave += (_, __) => label.Foreground = PopRes("PopSub");

        _pickerRows!.Children.Add(row);
    }

    private void AddPickerRow(VersionCard card, string? currentId)
    {
        var isCurrent = SameVersion(card, currentId);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var bar = new Border
        {
            CornerRadius = new CornerRadius(2),
            Background = card.LoaderColor,
            Margin = new Thickness(0, 8, 0, 8),
            Opacity = isCurrent ? 1 : 0.75,
        };
        row.Children.Add(bar);

        var text = new StackPanel { Margin = new Thickness(10, 7, 8, 7) };

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock
        {
            Text = card.Summary.Id,
            FontSize = 12.5,
            FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = PopRes("PopInk"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 300,
        });

        if (isCurrent)
        {
            var pill = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = PopRes("PopActive"),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            pill.Child = new TextBlock
            {
                Text = "当前",
                FontSize = 9,
                Foreground = PopRes("PopAccent"),
            };
            titleRow.Children.Add(pill);
        }

        text.Children.Add(titleRow);

        var parts = new List<string> { $"MC {card.Summary.ClientVersion}" };
        parts.Add(card.Summary.Loader + (string.IsNullOrEmpty(card.Summary.LoaderVersion)
            ? string.Empty
            : " " + card.Summary.LoaderVersion));
        parts.Add($"{card.Summary.ModCount} mods");
        parts.Add($"Java {card.Summary.JavaMajor}");

        text.Children.Add(new TextBlock
        {
            Text = string.Join(" · ", parts),
            FontSize = 10,
            Foreground = PopRes("PopSub"),
            Margin = new Thickness(0, 3, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 320,
        });

        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        if (isCurrent)
        {
            var check = new TextBlock
            {
                Text = "✓",
                FontSize = 13,
                Foreground = PopRes("PopAccent"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };
            Grid.SetColumn(check, 2);
            row.Children.Add(check);
        }

        var border = new Border
        {
            CornerRadius = new CornerRadius(9),
            Background = isCurrent ? PopRes("PopActive") : Brushes.Transparent,
            Child = row,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 1, 0, 1),
        };

        var id = card.Summary.Id;
        border.MouseEnter += (_, __) =>
        {
            if (!isCurrent)
            {
                border.Background = PopRes("PopHover");
            }

            _pickerHighlight = id;
            PaintPickerHighlight();
        };
        border.MouseLeave += (_, __) =>
        {
            if (!isCurrent)
            {
                border.Background = Brushes.Transparent;
            }
        };
        border.MouseLeftButtonUp += (_, __) =>
        {
            VersionList.SelectedItem = card;
            VersionPopover.IsOpen = false;
        };

        _pickerRowViews[id] = border;
        _pickerVisible.Add(card);
        _pickerRows!.Children.Add(border);
    }

    /// <summary>键盘高亮：给当前高亮行换底色，其余恢复。</summary>
    private void PaintPickerHighlight()
    {
        foreach (var pair in _pickerRowViews)
        {
            var isCurrent = VersionList.SelectedItem is VersionCard current
                            && string.Equals(current.Summary.Id, pair.Key, StringComparison.OrdinalIgnoreCase);
            var isHighlight = string.Equals(pair.Key, _pickerHighlight, StringComparison.OrdinalIgnoreCase);
            pair.Value.Background = isCurrent
                ? PopRes("PopActive")
                : isHighlight ? PopRes("PopHover") : Brushes.Transparent;
        }
    }

    // ---------------- 交互 ----------------

    private void OnPickerQueryChanged(object sender, TextChangedEventArgs e)
    {
        _pickerQuery = _pickerSearchBox?.Text ?? string.Empty;
        if (_pickerSearchHint != null)
        {
            _pickerSearchHint.Visibility = _pickerQuery.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_pickerClear != null)
        {
            _pickerClear.Visibility = _pickerQuery.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        RenderVersionPickerRows();
    }

    private void OnPickerQueryKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CommitPickerSelection();
                e.Handled = true;
                break;

            case Key.Down:
                MovePickerHighlight(1);
                e.Handled = true;
                break;

            case Key.Up:
                MovePickerHighlight(-1);
                e.Handled = true;
                break;

            case Key.Escape:
                if (_pickerQuery.Length > 0)
                {
                    if (_pickerSearchBox != null)
                    {
                        _pickerSearchBox.Text = string.Empty;
                    }
                }
                else
                {
                    VersionPopover.IsOpen = false;
                }

                e.Handled = true;
                break;
        }
    }

    private void CommitPickerSelection()
    {
        var target = _pickerVisible.FirstOrDefault(c => SameVersion(c, _pickerHighlight!))
                     ?? _pickerVisible.FirstOrDefault();
        if (target == null)
        {
            return;
        }

        VersionList.SelectedItem = target;
        VersionPopover.IsOpen = false;
    }

    private void MovePickerHighlight(int delta)
    {
        if (_pickerVisible.Count == 0)
        {
            return;
        }

        var index = _pickerVisible.FindIndex(c => SameVersion(c, _pickerHighlight!));
        index = index < 0 ? 0 : Math.Clamp(index + delta, 0, _pickerVisible.Count - 1);
        _pickerHighlight = _pickerVisible[index].Summary.Id;
        PaintPickerHighlight();

        if (_pickerRowViews.TryGetValue(_pickerHighlight, out var view))
        {
            view.BringIntoView();
        }
    }

    /// <summary>调试：把窗口和版本弹层（独立 Popup 窗口）合成一张 PNG，方便检查配色。</summary>
    private async Task CapturePickerAsync(string path)
    {
        await Task.Delay(1600);
        try
        {
            UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(this);
            var width = (int)Math.Round(ActualWidth * dpi.DpiScaleX);
            var height = (int)Math.Round(ActualHeight * dpi.DpiScaleY);
            var window = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            window.Render(this);

            RenderTargetBitmap? popup = null;
            FrameworkElement? popupChild = null;
            var offset = new Point(0, 0);
FrameworkElement? child = null;
            if (_cvPopup is { IsOpen: true } && _cvPopup.Child is FrameworkElement cvChild)
            {
                child = cvChild;
            }
            else if (VersionPopover is { IsOpen: true } && VersionPopover.Child is FrameworkElement vpChild)
            {
                child = vpChild;
            }

            if (child != null)
            {
                popupChild = child;
                child.UpdateLayout();
                var scale = dpi.DpiScaleX;
                var pw = (int)Math.Ceiling(child.ActualWidth * scale);
                var ph = (int)Math.Ceiling(child.ActualHeight * scale);


                if (pw > 4 && ph > 4)
                {
                    popup = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    popup.Render(child);

                    var popOrigin = child.PointToScreen(new Point(0, 0));
                    var winOrigin = PointToScreen(new Point(0, 0));
                    // PointToScreen 给的是设备像素，位图也是按像素画的，不用再乘缩放
                    offset = new Point(popOrigin.X - winOrigin.X, popOrigin.Y - winOrigin.Y);

                }
            }



            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(window, new Rect(0, 0, width, height));
                if (popup != null && popupChild != null)
                {
                    dc.DrawImage(popup, new Rect(offset.X, offset.Y, popup.PixelWidth, popup.PixelHeight));
                }
            }

            var final = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            final.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(final));
            await using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(@"D:\综合创作\_pickererr.txt", ex.ToString());
        }

        Application.Current.Shutdown();
    }

    /// <summary>自然序比较版本号：1.9 &lt; 1.10 &lt; 26.3。</summary>
    private static int CompareMcVersion(string? a, string? b)
    {
        var left = Numbers(a);
        var right = Numbers(b);
        var length = Math.Max(left.Count, right.Count);

        for (var i = 0; i < length; i++)
        {
            var x = i < left.Count ? left[i] : 0;
            var y = i < right.Count ? right[i] : 0;
            if (x != y)
            {
                return x.CompareTo(y);
            }
        }

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static List<int> Numbers(string? text) =>
        Regex.Matches(text ?? string.Empty, @"\d+")
            .Select(m => int.TryParse(m.Value, out var value) ? value : 0)
            .ToList();
}
