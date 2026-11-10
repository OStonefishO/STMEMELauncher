using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Aurora.Core.Auth;

namespace Aurora.App;

/// <summary>微软登录四步诊断窗口：显示设备码，然后把每一步结果逐条打出来。</summary>
public partial class MsaDiagWindow : Window
{
    private readonly string _clientId;
    private CancellationTokenSource? _cts;

    public MsaDiagWindow(string clientId)
    {
        InitializeComponent();
        _clientId = clientId;
        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        StepPanel.Children.Clear();
        StatusText.Text = "正在申请设备码…";
        CodeText.Text = "----";
        RestartButton.IsEnabled = false;

        try
        {
            var device = await MsaDiagnostics.StartAsync(_clientId, ct);
            CodeText.Text = device.Code;
            UrlText.Text = device.Url;
            StatusText.Text = "等你登录中…（这个窗口别关，登录完会自动往下跑）";

            var result = await MsaDiagnostics.RunAsync(
                _clientId,
                device,
                step => Dispatcher.BeginInvoke(new Action(() => AddStep(step))),
                text => Dispatcher.BeginInvoke(new Action(() => StatusText.Text = text)),
                ct);

            StatusText.Text = result.AllOk
                ? "四步全过：正版登录可用 ✅"
                : "有步骤没过，看上面哪一条是红的；第 ④ 步失败通常就是白名单还没批。";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "已取消";
        }
        catch (Exception ex)
        {
            StatusText.Text = "诊断失败：" + ex.Message;
        }
        finally
        {
            RestartButton.IsEnabled = true;
        }
    }

    private void AddStep(MsaStep step)
    {
        var row = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(step.Ok ? "#1422C55E" : "#14FF5B5B")),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(step.Ok ? "#3322C55E" : "#33FF5B5B")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 8),
        };

        var stack = new StackPanel();
        var title = new TextBlock
        {
            Text = (step.Ok ? "✅ " : "❌ ") + step.Name,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Ink"),
        };
        stack.Children.Add(title);

        if (!string.IsNullOrWhiteSpace(step.Detail))
        {
            stack.Children.Add(new TextBlock
            {
                Text = step.Detail,
                FontSize = 11.5,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("InkDim"),
            });
        }

        row.Child = stack;
        StepPanel.Children.Add(row);
    }

    private void OnCopyCode(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(CodeText.Text);
            StatusText.Text = "代码已复制：" + CodeText.Text;
        }
        catch
        {
            StatusText.Text = "复制失败，请手动选中上面的代码";
        }
    }

    private void OnOpenLink(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                string.IsNullOrWhiteSpace(UrlText.Text) ? "https://www.microsoft.com/link" : UrlText.Text)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // 忽略
        }
    }

    private async void OnRestart(object sender, RoutedEventArgs e) => await RunAsync();

    private void OnClose(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }
}
