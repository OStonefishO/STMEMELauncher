using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Aurora.App;

public partial class App : Application
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MEMELauncher");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log("AppDomain", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log("TaskScheduler", args.Exception);
            args.SetObserved();
        };
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log("Dispatcher", e.Exception);

        // 不因为一次 UI 异常直接退出，写盘后继续跑
        e.Handled = true;

        try
        {
            MessageBox.Show(
                "遇到一个错误，已写入日志：\n" + Path.Combine(LogDirectory, "error.log") + "\n\n" + e.Exception.Message,
                "MEME Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch
        {
            // 忽略
        }
    }

    private static void Log(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var path = Path.Combine(LogDirectory, "error.log");
            var builder = new StringBuilder();
            builder.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + source + "] ====");
            builder.AppendLine(ex?.ToString() ?? "(no exception object)");
            builder.AppendLine();
            File.AppendAllText(path, builder.ToString(), new UTF8Encoding(false));
        }
        catch
        {
            // 日志失败就算了
        }
    }
}
