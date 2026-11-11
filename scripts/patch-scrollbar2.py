import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")

# 1) 删掉我新加的重复隐式样式（从注释到对应 </Style>）
start = next(i for i,l in enumerate(lines) if '<!-- 全局滚动条：加粗到 14px' in l)
end = next(i for i in range(start, len(lines)) if lines[i].strip() == '</Style>')
del lines[start:end+1]

s = "\n".join(lines)

# 2) 直接加粗原有的 MemeScrollBar
s = s.replace('''        <Style x:Key="MemeScrollBar" TargetType="ScrollBar">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Width" Value="8"/>
            <Setter Property="MinWidth" Value="8"/>''','''        <Style x:Key="MemeScrollBar" TargetType="ScrollBar">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Width" Value="14"/>
            <Setter Property="MinWidth" Value="14"/>''', 1)
s = s.replace('<Border Background="#38FFFFFF" CornerRadius="3" Margin="1,2"/>',
              '<Border Background="#66FFFFFF" CornerRadius="5" Margin="3,2"/>', 1)
s = s.replace('''                                <Setter Property="Width" Value="Auto"/>
                                <Setter Property="Height" Value="8"/>''','''                                <Setter Property="Width" Value="Auto"/>
                                <Setter Property="Height" Value="14"/>''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("scrollbar merged into MemeScrollBar")
