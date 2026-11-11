import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace('''                if (UpdateStatus != null)
                {
                    UpdateStatus.Text = $"已是最新版本（v{AppVersion}）";
                }''','''                if (UpdateStatus != null)
                {
                    var published = release.PublishedAt == default
                        ? string.Empty
                        : $" · 发布于 {release.PublishedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
                    UpdateStatus.Text = $"已是最新（当前 v{AppVersion} · 线上最新 {release.Tag}{published}）";
                }''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("update message enriched")
