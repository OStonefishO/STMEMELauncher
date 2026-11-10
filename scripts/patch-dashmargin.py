import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace('<Grid x:Name="DashboardGrid" Grid.Row="1" Margin="26,0,26,10" Visibility="Collapsed">',
              '<Grid x:Name="DashboardGrid" Grid.Row="1" Margin="26,0,26,64" Visibility="Collapsed">')
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("dashboard bottom margin")
