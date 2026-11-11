import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")

start = next(i for i,l in enumerate(lines) if 'x:Name="VmSearchBox"' in l)
end = next(i for i in range(start, len(lines)) if '/>' in lines[i])
block = lines[start:end+1]
del lines[start:end+1]

new = [
 '                                    <Grid DockPanel.Dock="Top" Margin="8,0,8,8">\r',
 '                                        <TextBox x:Name="VmSearchBox" Height="32"\r',
 '                                                 FontSize="12" Background="{DynamicResource FieldBg}"\r',
 '                                                 BorderBrush="{DynamicResource FieldLine}" BorderThickness="1"\r',
 '                                                 Foreground="{DynamicResource Ink}" Padding="10,0"\r',
 '                                                 VerticalContentAlignment="Center"\r',
 '                                                 TextChanged="OnVmSearchChanged" KeyDown="OnVmSearchKeyDown"\r',
 '                                                 ToolTip="直接打字过滤，例如 1.12.2 / forge / fabric；回车选第一个"/>\r',
 '                                        <TextBlock x:Name="VmSearchHint" Text="输入版本号过滤，如 1.12.2"\r',
 '                                                   FontSize="12" Foreground="{DynamicResource InkFaint}"\r',
 '                                                   VerticalAlignment="Center" Margin="12,0,0,0"\r',
 '                                                   IsHitTestVisible="False"/>\r',
 '                                    </Grid>\r',
]
lines[start:start] = new
io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
import xml.etree.ElementTree as ET
ET.parse(p)
print("hint added")
