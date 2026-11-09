using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Aurora.Core.Launch;
using Microsoft.Win32;

namespace Aurora.App;

/// <summary>崩溃说明窗口：讲清楚为什么崩、给建议、能看能导出报告。</summary>
public partial class CrashWindow : Window
{
    private readonly CrashInsight _insight;

    public CrashWindow(CrashInsight insight)
    {
        InitializeComponent();
        _insight = insight;
        Fill(insight);
    }

    private void Fill(CrashInsight insight)
    {
        CategoryText.Text = insight.Category;
        ConfidenceText.Text = insight.Confident ? "已确认" : "推测";
        ExitCodeText.Text = "退出码 " + insight.ExitCode;
        HeadlineText.Text = insight.Headline;
        VerdictText.Text = insight.Verdict;

        CauseList.ItemsSource = insight.Causes;
        SuggestionList.ItemsSource = insight.Suggestions;
        ReportBox.Text = insight.ReportText;

        OpenFolderButton.IsEnabled = !string.IsNullOrEmpty(insight.GameCrashReportPath);
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch
        {
            // 忽略
        }
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_insight.ToPlainText());
            CategoryText.Text = _insight.Category + " · 已复制";
        }
        catch
        {
            // 剪贴板被占用时忽略
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "导出崩溃报告",
                FileName = $"MEME崩溃报告-{DateTime.Now:yyyyMMdd-HHmm}.txt",
                Filter = "文本文件|*.txt",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, _insight.ToPlainText(), new UTF8Encoding(false));
            CategoryText.Text = _insight.Category + " · 已导出";
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{dialog.FileName}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：" + ex.Message, "MEME 启动器");
        }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _insight.GameCrashReportPath;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            else
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
                }
            }
        }
        catch
        {
            // 忽略
        }
    }

    private void OnToggleReport(object sender, RoutedEventArgs e)
    {
        var show = ReportHost.Visibility != Visibility.Visible;
        ReportHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ReportToggle.Content = (show ? "▾ " : "▸ ") + "原始报告 / 日志（发给作者用）";
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
