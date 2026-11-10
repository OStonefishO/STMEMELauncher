import io, xml.etree.ElementTree as ET
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")

start = next(i for i,l in enumerate(lines) if 'x:Name="SettingsPage"' in l)
sv = next(i for i in range(start, len(lines)) if lines[i].strip() == "</ScrollViewer>")
grid = next(i for i in range(sv, len(lines)) if lines[i].strip() == "</Grid>")

stage = '''                        <Border Grid.Row="1" Width="366" HorizontalAlignment="Right" VerticalAlignment="Stretch"
                                Margin="0,0,24,14" CornerRadius="18" Background="#0AFFFFFF"
                                BorderBrush="{StaticResource GlassLine}" BorderThickness="1" ClipToBounds="True">
                            <Grid Background="Transparent" Cursor="Hand"
                                  MouseLeftButtonDown="OnSettingsSceneClick"
                                  ToolTip="点一下换个角色：僵尸 / 猪灵 / 蜘蛛 / 苦力怕">
                                <Viewport3D x:Name="SettingsViewport"/>
                                <StackPanel VerticalAlignment="Bottom" HorizontalAlignment="Center" Margin="0,0,0,18"
                                            IsHitTestVisible="False">
                                    <TextBlock x:Name="SettingsMobText" Text="僵尸" FontSize="13.5"
                                               Foreground="{StaticResource Ink}" HorizontalAlignment="Center"/>
                                    <TextBlock x:Name="SettingsMobHint" Text="" FontSize="10.5"
                                               Foreground="{StaticResource InkFaint}" HorizontalAlignment="Center"
                                               Margin="0,4,0,0"/>
                                    <TextBlock Text="点一下换角色" FontSize="9.5" Foreground="#5F6E8A"
                                               HorizontalAlignment="Center" Margin="0,8,0,0"/>
                                </StackPanel>
                            </Grid>
                        </Border>'''.split("\n")

lines[grid:grid] = stage
io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
ET.parse(p)
print("inserted before line", grid, "XML OK")
