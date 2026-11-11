import io
# 1) 设置项 UiVolume
p = r"D:\综合创作\MCLauncher\src\Aurora.Core\Configuration\LauncherSettings.cs"
s = io.open(p, encoding="utf-8", newline="").read()
if "UiVolume" not in s:
    s = s.replace('    public int UiHue { get; set; }',
                  '    public int UiHue { get; set; }\n\n    /// <summary>界面音效音量（0..100），0 表示静音。</summary>\n    public int UiVolume { get; set; } = 55;', 1)
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("UiVolume added")

# 2) XAML：音量滑块 + 仪表盘强制关闭按钮
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s2 = io.open(p2, encoding="utf-8", newline="").read()
anchor = '''                                            <TextBlock x:Name="HueText" Grid.Column="2" Text="0°" FontSize="11.5"
                                                       Foreground="{DynamicResource InkDim}" VerticalAlignment="Center" Width="34"/>
                                        </Grid>'''
add = anchor + '''
                                        <Grid Margin="0,8,0,0">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="Auto"/>
                                                <ColumnDefinition Width="*"/>
                                                <ColumnDefinition Width="Auto"/>
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="音效音量" FontSize="11.5" Foreground="{DynamicResource InkDim}"
                                                       VerticalAlignment="Center"/>
                                            <Slider x:Name="VolumeSlider" Grid.Column="1" Minimum="0" Maximum="100" Value="55"
                                                    Margin="12,0,12,0" VerticalAlignment="Center"
                                                    ValueChanged="OnVolumeChanged" IsSnapToTickEnabled="True" TickFrequency="5"/>
                                            <TextBlock x:Name="VolumeText" Grid.Column="2" Text="55%" FontSize="11.5"
                                                       Foreground="{DynamicResource InkDim}" VerticalAlignment="Center" Width="34"/>
                                        </Grid>'''
assert anchor in s2
s2 = s2.replace(anchor, add, 1)

# 仪表盘加「强制关闭游戏」
kill = '''                                        <Button Content="版本管理" Style="{StaticResource GlassButton}" Margin="8,0,0,0" Tag="7" Click="OnNavClick"/>'''
kill_new = kill + '''
                                        <Button x:Name="DashKillButton" Content="强制关闭游戏" Style="{StaticResource GlassButton}"
                                                Margin="8,0,0,0" Click="OnForceKillGame" Visibility="Collapsed"/>'''
if kill in s2:
    s2 = s2.replace(kill, kill_new, 1)
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
import xml.etree.ElementTree as ET
ET.parse(p2)
print("volume slider + dash kill button added")
