using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Aurora.Core.Creation;
using Aurora.Core.Models;
using Aurora.Core.Versions;

namespace Aurora.App;

/// <summary>创作页：整合包导出 + 数据包生成。</summary>
public partial class MainWindow
{
    private readonly ObservableCollection<PackRow> _packRows = new();
    private bool _creationReady;

    /// <summary>整合包列表里的一行。</summary>
    public sealed class PackRow : INotifyPropertyChanged
    {
        private bool _isChecked;

        public PackRow(PackCandidate candidate)
        {
            Candidate = candidate;
            _isChecked = candidate.Suggested;
        }

        public PackCandidate Candidate { get; }

        public string Name => Candidate.Name;
        public string Hint => Candidate.Hint;
        public string Detail => Candidate.DetailText;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private void InitCreation()
    {
        if (_creationReady) return;
        _creationReady = true;

        PackCandidateList.ItemsSource = _packRows;

        // 数据包模板
        if (DataTemplateCombo.Items.Count == 0)
        {
            foreach (var (text, value) in new (string, DatapackTemplate)[]
                     {
                         ("空数据包（只生成 pack.mcmeta）", DatapackTemplate.Empty),
                         ("打招呼（进游戏自动执行一次）", DatapackTemplate.Hello),
                         ("示例合成配方", DatapackTemplate.Recipe),
                         ("示例进度 / 成就", DatapackTemplate.Advancement),
                         ("循环 tick（每秒执行）", DatapackTemplate.TickLoad),
                     })
            {
                DataTemplateCombo.Items.Add(new ComboBoxItem { Content = text, Tag = value });
            }

            DataTemplateCombo.SelectedIndex = 1;
        }

        // 窗口真正加载完（设置已就绪）再刷一次版本，避免启动早期扫不到
        Loaded += (_, _) =>
        {
            var list = RefreshCreationVersions();
            if (list.Count > 0 && PackVersionCombo.SelectedItem == null)
            {
                PackVersionCombo.SelectedIndex = 0;
            }
        };

        // 版本下拉（顺便拿第一项做默认）
        var versions = RefreshCreationVersions();

        if (versions.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(PackNameBox.Text))
            {
                PackNameBox.Text = versions[0].Id;
            }

            LoadPackCandidates(versions[0].Id);
        }
    }

    private List<VersionSummary> RefreshCreationVersions()
    {
        List<VersionSummary> versions;
        try
        {
            versions = _service.ListVersions().Where(v => v.IsUsable).ToList();
        }
        catch (Exception ex)
        {
            CreationStatus.Text = "读取版本列表失败: " + ex.Message;
            versions = new List<VersionSummary>();
        }

        if (versions.Count == 0)
        {
            PackHint.Text = "还没扫到版本，先确认「设置 → 游戏目录」选对了";
            return versions;
        }
        PackVersionCombo.ItemsSource = versions;

        var pick = _vmCurrent?.Id ?? versions.FirstOrDefault()?.Id;
        if (pick != null)
        {
            PackVersionCombo.SelectedItem = versions.FirstOrDefault(v => v.Id == pick);
        }

        return versions;
    }

    private void OnCreationTab(object sender, RoutedEventArgs e)
    {
        var pack = ReferenceEquals(sender, CreateTabPack);
        CreatePackPanel.Visibility = pack ? Visibility.Visible : Visibility.Collapsed;
        CreateDataPanel.Visibility = pack ? Visibility.Collapsed : Visibility.Visible;

        CreateTabPack.Style = (Style)FindResource(pack ? "HeroButton" : "GlassButton");
        CreateTabData.Style = (Style)FindResource(pack ? "GlassButton" : "HeroButton");

        CreationStatus.Text = pack ? "把当前版本打包成整合包，可自选内容" : "生成数据包：pack.mcmeta + 函数/配方/进度";
    }

    private void OnPackVersionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PackVersionCombo.SelectedItem is not VersionSummary version)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PackNameBox.Text) || PackNameBox.Text == "我的整合包")
        {
            PackNameBox.Text = version.Id;
        }



        LoadPackCandidates(version.Id);
    }

    private void LoadPackCandidates(string versionId)
    {
        try
        {
            var resolved = _service.Resolve(versionId);
            var dir = resolved == null ? _settings.GameDirectory : _service.GameDirectoryFor(resolved);
            var candidates = PackExporter.Inspect(dir);

            // 这个版本没隔离 / 目录里没东西时，回退到游戏根目录再试一次
            var fallbackUsed = false;
            if (candidates.Count == 0 && !string.Equals(dir, _settings.GameDirectory, StringComparison.OrdinalIgnoreCase))
            {
                var rootCandidates = PackExporter.Inspect(_settings.GameDirectory);
                if (rootCandidates.Count > 0)
                {
                    candidates = rootCandidates;
                    dir = _settings.GameDirectory;
                    fallbackUsed = true;
                }
            }

            _packRows.Clear();
            foreach (var candidate in candidates)
            {
                _packRows.Add(new PackRow(candidate));
            }

            var total = candidates.Sum(c => c.SizeBytes);
            PackHint.Text = candidates.Count == 0
                ? "这个目录里没找到可打包的内容（mods / config / saves 等）"
                : (fallbackUsed ? "（用的是游戏根目录）" : string.Empty) +
                  $"共 {candidates.Count} 项 · 全选约 {PackSizeText(total)} · 勾选你要带上的";
        }
        catch (Exception ex)
        {
            PackHint.Text = "扫描失败: " + ex.Message;
        }
    }

    private static string PackSizeText(long bytes) => bytes switch
    {
        >= 1073741824 => (bytes / 1073741824.0).ToString("0.##") + " GB",
        >= 1048576 => (bytes / 1048576.0).ToString("0.#") + " MB",
        >= 1024 => (bytes / 1024.0).ToString("0") + " KB",
        _ => bytes + " B",
    };

    private void OnPackRefresh(object sender, RoutedEventArgs e)
    {
        RefreshCreationVersions();
        if (PackVersionCombo.SelectedItem is VersionSummary version)
        {
            LoadPackCandidates(version.Id);
        }
    }

    private void OnPackSelectAll(object sender, RoutedEventArgs e)
    {
        foreach (var row in _packRows) row.IsChecked = true;
    }

    private void OnPackSelectNone(object sender, RoutedEventArgs e)
    {
        foreach (var row in _packRows) row.IsChecked = false;
    }

    private void OnPackSelectSuggested(object sender, RoutedEventArgs e)
    {
        foreach (var row in _packRows) row.IsChecked = row.Candidate.Suggested;
    }

    private async void OnPackExport(object sender, RoutedEventArgs e)
    {
        if (PackVersionCombo.SelectedItem is not VersionSummary version)
        {
            PackResult.Text = "先选一个版本";
            return;
        }

        var includes = _packRows.Where(r => r.IsChecked).Select(r => r.Candidate.RelativePath).ToList();
        if (includes.Count == 0)
        {
            PackResult.Text = "至少勾选一项要打包的内容";
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出整合包",
            Filter = "整合包 zip|*.zip",
            FileName = SanitizeFileName(PackNameBox.Text) + ".zip",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var resolved = _service.Resolve(version.Id);
        var dir = resolved == null ? _settings.GameDirectory : _service.GameDirectoryFor(resolved);

        var options = new PackExportOptions
        {
            SourceDirectory = dir,
            OutputPath = dialog.FileName,
            Name = string.IsNullOrWhiteSpace(PackNameBox.Text) ? version.Id : PackNameBox.Text.Trim(),
            Description = PackDescBox.Text.Trim(),
            Version = string.IsNullOrWhiteSpace(PackVersionBox.Text) ? "1.0.0" : PackVersionBox.Text.Trim(),
            McVersion = resolved?.ClientVersion ?? string.Empty,
            Loader = resolved?.Loader ?? string.Empty,
            Includes = includes,
            Progress = new Progress<string>(text => CreationStatus.Text = text),
        };

        PackResult.Text = "正在打包…";
        try
        {
            var result = await Task.Run(() => PackExporter.Export(options));
            PackResult.Text = $"已导出 {result.FileCount} 个文件 · {PackSizeText(result.SizeBytes)}";
            CreationStatus.Text = result.Path;
            ShowToast("整合包已导出", Path.GetFileName(result.Path), "✓", 4200);
        }
        catch (Exception ex)
        {
            PackResult.Text = "导出失败: " + ex.Message;
        }
    }

    private static string SanitizeFileName(string? name)
    {
        var text = string.IsNullOrWhiteSpace(name) ? "modpack" : name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            text = text.Replace(c, '_');
        }

        return text;
    }

    // ---------------- 数据包 ----------------

    private void OnDataVersionChanged(object sender, TextChangedEventArgs e)
    {
        UpdateDataFormatPreview();
    }

    private void UpdateDataFormatPreview()
    {
        if (DataFormatText == null) return;
        var format = DatapackBuilder.PackFormatFor(DataVersionBox.Text);
        var folder = DatapackBuilder.UsesSingularFunctionFolder(DataVersionBox.Text) ? "function" : "functions";
        DataFormatText.Text = $"{format}（data/…/{folder}/）";
    }

    private void OnDataTemplateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataTemplateCombo.SelectedItem is not ComboBoxItem item || item.Tag is not DatapackTemplate template)
        {
            return;
        }

        DataTemplateHint.Text = DatapackBuilder.Describe(template);
        if (string.IsNullOrWhiteSpace(DataFunctionBox.Text))
        {
            DataFunctionBox.Text = template switch
            {
                DatapackTemplate.Hello => "say 欢迎来到我的世界！",
                DatapackTemplate.TickLoad => "# 每秒执行一次\nsay tick",
                DatapackTemplate.Recipe => "say 自定义配方已加载",
                _ => string.Empty,
            };
        }
    }

    private void OnDataExport(object sender, RoutedEventArgs e)
    {
        if (DataTemplateCombo.SelectedItem is not ComboBoxItem item || item.Tag is not DatapackTemplate template)
        {
            template = DatapackTemplate.Hello;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出数据包",
            Filter = "数据包 zip|*.zip",
            FileName = SanitizeFileName(DataNameBox.Text) + ".zip",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        int? overrideFormat = null;
        if (int.TryParse(DataFormatOverride.Text.Trim(), out var parsed) && parsed > 0)
        {
            overrideFormat = parsed;
        }

        var request = new DatapackRequest
        {
            OutputPath = dialog.FileName,
            Name = string.IsNullOrWhiteSpace(DataNameBox.Text) ? "我的数据包" : DataNameBox.Text.Trim(),
            Description = DataDescBox.Text.Trim(),
            Namespace = DatapackBuilder.NormalizeNamespace(DataNamespaceBox.Text),
            McVersion = DataVersionBox.Text.Trim(),
            PackFormatOverride = overrideFormat,
            Template = template,
            FunctionBody = DataFunctionBox.Text,
        };

        try
        {
            var result = DatapackBuilder.Build(request);
            DataResult.Text = $"已生成 {result.Entries.Count} 个文件 · pack_format {result.PackFormat}";
            CreationStatus.Text = result.Path;
            ShowToast("数据包已导出", "放进存档 datapacks 目录后 /reload", "✓", 5000);
        }
        catch (Exception ex)
        {
            DataResult.Text = "生成失败: " + ex.Message;
        }
    }
}
