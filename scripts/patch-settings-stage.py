import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8").read()

anchor = "                        </ScrollViewer>\r\n                    </Grid>\r\n                    <!-- ========== 更新日志 ========== -->"
assert anchor in s, "anchor not found"

stage = '''                        </ScrollViewer>

                        <!-- 右侧 3D 小舞台 -->
                        <Border Grid.Row="1" Width="366" HorizontalAlignment="Right" VerticalAlignment="Stretch"
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
                        </Border>
                    </Grid>
                    <!-- ========== 更新日志 ========== -->'''

s = s.replace(anchor, stage.replace("\n", "\r\n"), 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)

import xml.etree.ElementTree as ET
ET.parse(p)
print("stage inserted, XML OK")
