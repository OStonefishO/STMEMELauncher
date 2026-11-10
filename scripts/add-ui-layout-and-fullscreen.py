import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")

# 1) 设置页插入「界面布局」卡片
start = next(i for i,l in enumerate(lines) if 'x:Name="SettingsPage"' in l)
open_idx = next(i for i in range(start, len(lines)) if 'MaxWidth="660"' in lines[i])
card = '''                                <!-- 界面布局 -->
                                <Border CornerRadius="16" Background="{DynamicResource Glass}"
                                        BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Padding="20,16"
                                        Margin="0,0,0,12">
                                    <StackPanel>
                                        <TextBlock Text="界面布局" FontSize="14" FontWeight="SemiBold" Foreground="{DynamicResource Ink}"/>
                                        <TextBlock Text="新版更动感：大胆配色、切换有过渡、导航有高亮胶囊。想安静点就换回经典。"
                                                   FontSize="11.5" Foreground="{DynamicResource InkDim}" Margin="0,6,0,10" TextWrapping="Wrap"/>
                                        <ComboBox x:Name="UiLayoutCombo" Height="38" FontSize="12.5"
                                                  SelectionChanged="OnUiLayoutChanged">
                                            <ComboBoxItem Content="新版（动感 / 高对比）" Tag="vivid"/>
                                            <ComboBoxItem Content="经典（旧版布局）" Tag="classic"/>
                                        </ComboBox>
                                    </StackPanel>
                                </Border>
'''.split("\n")
lines[open_idx+1:open_idx+1] = card

# 2) 右上角加全屏按钮
idx = next(i for i,l in enumerate(lines) if 'Click="OnMinimize"' in l)
btn = '''                    <Button Style="{StaticResource ChromeButton}" Click="OnToggleFullscreen" Content="&#x26F6;" FontSize="12"
                            ToolTip="全屏 / 退出全屏（F11）"/>'''.split("\n")
lines[idx:idx] = btn

io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))

import xml.etree.ElementTree as ET
ET.parse(p)
print("card at", open_idx, "| fullscreen button at", idx)
