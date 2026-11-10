import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()

# 色相滑块加到「界面布局」卡片里
anchor = '''                                            <ComboBoxItem Content="经典（旧版布局）" Tag="classic"/>
                                        </ComboBox>'''
addition = anchor + '''
                                        <Grid Margin="0,12,0,0">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="Auto"/>
                                                <ColumnDefinition Width="*"/>
                                                <ColumnDefinition Width="Auto"/>
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="主题色相" FontSize="11.5" Foreground="{DynamicResource InkDim}"
                                                       VerticalAlignment="Center"/>
                                            <Slider x:Name="HueSlider" Grid.Column="1" Minimum="-160" Maximum="160" Value="0"
                                                    Margin="12,0,12,0" VerticalAlignment="Center"
                                                    ValueChanged="OnHueChanged" IsSnapToTickEnabled="True" TickFrequency="5"/>
                                            <TextBlock x:Name="HueText" Grid.Column="2" Text="0°" FontSize="11.5"
                                                       Foreground="{DynamicResource InkDim}" VerticalAlignment="Center" Width="34"/>
                                        </Grid>'''
assert anchor in s
s = s.replace(anchor, addition, 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("hue slider added")
