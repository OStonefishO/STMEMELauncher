import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace('''            if (argv.Contains("--guide"))
            {
                Dispatcher.BeginInvoke(new Action(() => BuildServerGuideDialog().Show()), System.Windows.Threading.DispatcherPriority.Background);
            }''', '''            if (argv.Contains("--guide"))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        BuildServerGuideDialog().Show();
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.WriteAllText(@"D:\\综合创作\\_guideerr.txt", ex.ToString());
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("guide try/catch")
