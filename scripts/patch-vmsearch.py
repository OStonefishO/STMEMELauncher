import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()

old = '''                                    <TextBlock DockPanel.Dock="Top" Text="已安装版本" FontSize="11.5"
                                               Foreground="{DynamicResource InkFaint}" Margin="12,10,10,8"/>
                                    <ListBox x:Name="VmVersionList" Background="Transparent" BorderThickness="0"'''
new = '''                                    <TextBlock DockPanel.Dock="Top" Text="已安装版本" FontSize="11.5"
                                               Foreground="{DynamicResource InkFaint}" Margin="12,10,10,8"/>
                                    <TextBox x:Name="VmSearchBox" DockPanel.Dock="Top" Margin="8,0,8,8" Height="32"
                                             FontSize="12" Background="{DynamicResource FieldBg}"
                                             BorderBrush="{DynamicResource FieldLine}" BorderThickness="1"
                                             Foreground="{DynamicResource Ink}" Padding="10,0"
                                             VerticalContentAlignment="Center"
                                             TextChanged="OnVmSearchChanged" KeyDown="OnVmSearchKeyDown"
                                             ToolTip="直接打字过滤，例如 1.12.2 / forge / fabric；回车选第一个"/>
                                    <ListBox x:Name="VmVersionList" Background="Transparent" BorderThickness="0"'''
assert old in s
s = s.replace(old, new, 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("version search box added")
