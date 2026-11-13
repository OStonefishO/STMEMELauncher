using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aurora.Core.Auth;

namespace Aurora.App;

/// <summary>微软登录四步诊断窗口：显示设备码，然后把每一步结果逐条打出来。</summary>
public partial class MsaDiagWindow : Window
{
    /// <summary>官方启动器（Xbox Live 公共客户端）的 AppID，历史上一直被允许登录 Minecraft。</summary>
    private const string OfficialClientId = "00000000402b5328";

    private string _clientId;
    private readonly Action<string>? _onClientIdChanged;
    private CancellationTokenSource? _cts;

    private readonly string? _shotPath;

    public MsaDiagWindow(string clientId, Action<string>? onClientIdChanged = null, string? shotPath = null)
    {
        InitializeComponent();
        _clientId = clientId;
        _onClientIdChanged = onClientIdChanged;
        _shotPath = shotPath;
        Loaded += async (_, _) =>
        {
            ClientIdText.Text = _clientId;
            if (!string.IsNullOrEmpty(_shotPath))
            {
                _ = Task.Delay(1500).ContinueWith(_ => Dispatcher.BeginInvoke(new Action(SaveShot)));
            }
            await RunAsync();
        };
    }

    /// <summary>调试：把窗口自己渲染成 png，验证布局用。</summary>
    private void SaveShot()
    {
        try
        {
            var width = (int)Math.Max(1, ActualWidth);
            var height = (int)Math.Max(1, ActualHeight);
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96,
                PixelFormats.Pbgra32);
            bmp.Render(this);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using var fs = System.IO.File.Create(_shotPath!);
            encoder.Save(fs);
        }
        catch
        {
            // 截图失败不影响诊断
        }
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

    /// <summary>换用官方启动器的 ClientId 再跑一遍（绕开自家 AppID 白名单未批的情况）。</summary>
    private async void OnUseOfficialClientId(object sender, RoutedEventArgs e)
    {
        _clientId = OfficialClientId;
        ClientIdText.Text = _clientId;
        _onClientIdChanged?.Invoke(_clientId);
        await RunAsync();
    }

    /// <summary>粘贴任意已登记的 ClientId（例如其它开源启动器公开的 AppID）。</summary>
    private async void OnPasteClientId(object sender, RoutedEventArgs e)
    {
        string? input;
        try
        {
            input = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : null;
        }
        catch
        {
            input = null;
        }

        if (string.IsNullOrWhiteSpace(input) || input.Length < 20)
        {
            StatusText.Text = "先把 ClientId 复制到剪贴板（形如 00000000402b5328 或 xxxxxxxx-xxxx-...），再点这个按钮";
            return;
        }

        _clientId = input;
        ClientIdText.Text = _clientId;
        _onClientIdChanged?.Invoke(_clientId);
        await RunAsync();
    }

    /// <summary>第 ④ 步过不去时的稳妥方案：改用外置登录（LittleSkin 等）。</summary>
    private void OnUseAuthlibInjector(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "外置登录不经过 Mojang 白名单：\n\n" +
            "1. 打开「账号」页 → 切换外置登录\n" +
            "2. 服务器地址填 https://littleskin.cn/api/yggdrasil\n" +
            "3. 用皮肤站账号密码登录，启动器会自动装 authlib-injector\n\n" +
            "这样正版皮肤和披风同样能显示，只是不校验微软账号。",
            "改用外置登录", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }
}
