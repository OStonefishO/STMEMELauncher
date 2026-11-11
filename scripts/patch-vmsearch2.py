import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")
i = next(k for k,l in enumerate(lines) if 'x:Name="VmVersionList"' in l)
box = [
 '                                    <TextBox x:Name="VmSearchBox" DockPanel.Dock="Top" Margin="8,0,8,8" Height="32"\r',
 '                                             FontSize="12" Background="{DynamicResource FieldBg}"\r',
 '                                             BorderBrush="{DynamicResource FieldLine}" BorderThickness="1"\r',
 '                                             Foreground="{DynamicResource Ink}" Padding="10,0"\r',
 '                                             VerticalContentAlignment="Center"\r',
 '                                             TextChanged="OnVmSearchChanged" KeyDown="OnVmSearchKeyDown"\r',
 '                                             ToolTip="直接打字过滤，例如 1.12.2 / forge / fabric；回车选第一个"/>\r',
]
lines[i:i] = box
io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
import xml.etree.ElementTree as ET
ET.parse(p)
print("search box inserted at", i)
