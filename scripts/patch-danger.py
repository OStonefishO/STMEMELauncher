import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 新增醒目的「危险按钮」样式（基于 GlassButton 的模板思路，红橙强调 + 更大）
style = '''
        <!-- 强制关闭：比普通按钮明显大一号，红橙强调 -->
        <Style x:Key="DangerButton" TargetType="Button">
            <Setter Property="Foreground" Value="#FFFFB4A8"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Height" Value="42"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Padding" Value="18,0"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="b" CornerRadius="12" Background="#2EFF5B5B"
                                BorderBrush="#7AFF6B5B" BorderThickness="1.5"
                                Padding="{TemplateBinding Padding}">
                            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center">
                                <TextBlock Text="✕" FontSize="12" Margin="0,1,8,0" VerticalAlignment="Center"
                                           Foreground="{TemplateBinding Foreground}"/>
                                <ContentPresenter VerticalAlignment="Center"/>
                            </StackPanel>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="b" Property="Background" Value="#48FF5B5B"/>
                                <Setter TargetName="b" Property="BorderBrush" Value="#B3FF8B7B"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="b" Property="Background" Value="#66FF5B5B"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
'''
s = s.replace('        <Style x:Key="GlassButton" TargetType="Button">', style + '\n        <Style x:Key="GlassButton" TargetType="Button">', 1)

# 2) 四个强制关闭按钮改用新样式
for name in ["PaperKillButton", "PixelKillButton", "DashKillButton", "ForceKillButton"]:
    marker = 'x:Name="' + name + '"'
    i = s.index(marker)
    seg_end = s.index('/>', i)
    seg = s[i:seg_end]
    seg = seg.replace('Style="{StaticResource GlassButton}"', 'Style="{StaticResource DangerButton}"')
    s = s[:i] + seg + s[seg_end:]
io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("danger style applied")
