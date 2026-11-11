import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()

old = '''                        <Grid Grid.Row="2" Margin="26,12,0,8">
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>
                            <Grid Grid.Row="0">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <ComboBox x:Name="FileCombo" Grid.Column="0" DisplayMemberPath="Label"
                                          SelectionChanged="OnFileSelected"/>
                                <Button x:Name="ContentInstallButton" Grid.Column="1" Content="安装到当前版本"
                                        Style="{StaticResource HeroButton}" Click="OnInstallContent"
                                        Margin="10,0,0,0" Padding="22,0" MinWidth="150" Height="38" IsEnabled="False"/>
                            </Grid>
                            <TextBlock Grid.Row="1" x:Name="ContentStatus" Text="搜索后选一个条目" FontSize="11.5" Margin="0,8,0,0"
                                       Foreground="{DynamicResource InkDim}" TextWrapping="Wrap"/>
                        </Grid>'''

new = '''                        <!-- 底部状态提示（安装条已移除：进详情页就能选版本安装） -->
                        <TextBlock Grid.Row="2" x:Name="ContentStatus" Text="搜索后点一个条目进详情，选版本安装"
                                   FontSize="11.5" Margin="26,8,0,10"
                                   Foreground="{DynamicResource InkDim}" TextWrapping="Wrap"/>'''

assert old in s
s = s.replace(old, new, 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("bottom install bar removed")
