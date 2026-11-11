import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()

# 新增主题化资源（下拉弹层 / 输入框底色）
s = s.replace('<SolidColorBrush x:Key="DockBg" Color="#F0120A20"/>',
'''<SolidColorBrush x:Key="DockBg" Color="#F0120A20"/>
        <SolidColorBrush x:Key="ComboPopupBg" Color="#FF141A26"/>
        <SolidColorBrush x:Key="ComboPopupLine" Color="#33FFFFFF"/>
        <SolidColorBrush x:Key="ComboItemHover" Color="#24FFFFFF"/>
        <SolidColorBrush x:Key="ComboItemActive" Color="#335B8CFF"/>
        <SolidColorBrush x:Key="FieldBg" Color="#12FFFFFF"/>
        <SolidColorBrush x:Key="FieldLine" Color="#26FFFFFF"/>''', 1)

s = s.replace('<Setter TargetName="b" Property="Background" Value="#24FFFFFF"/>', '<Setter TargetName="b" Property="Background" Value="{DynamicResource ComboItemHover}"/>', 1)
s = s.replace('<Setter TargetName="b" Property="Background" Value="#335B8CFF"/>', '<Setter TargetName="b" Property="Background" Value="{DynamicResource ComboItemActive}"/>', 1)
s = s.replace('''<Border x:Name="bd" Background="#12FFFFFF" BorderBrush="#26FFFFFF"''', '''<Border x:Name="bd" Background="{DynamicResource FieldBg}" BorderBrush="{DynamicResource FieldLine}"''', 1)
s = s.replace('<Setter TargetName="bd" Property="Background" Value="#1CFFFFFF"/>', '<Setter TargetName="bd" Property="Background" Value="{DynamicResource ComboItemHover}"/>', 1)
s = s.replace('<Border Background="#FF141A26" BorderBrush="#33FFFFFF" BorderThickness="1"', '<Border Background="{DynamicResource ComboPopupBg}" BorderBrush="{DynamicResource ComboPopupLine}" BorderThickness="1"', 1)
s = s.replace('<Path Grid.Column="1" Data="M0,0 L4.5,4.5 L9,0" Stroke="#8C94A8"', '<Path Grid.Column="1" Data="M0,0 L4.5,4.5 L9,0" Stroke="{DynamicResource InkDim}"', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("combo resources wired")
