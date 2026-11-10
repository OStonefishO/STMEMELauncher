import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 顶部胶囊条：dock 从底部改到顶部
s = s.replace('''<Border x:Name="BottomDock" Grid.Column="1" VerticalAlignment="Bottom" HorizontalAlignment="Center"
                        Margin="0,0,0,14"''', '''<Border x:Name="BottomDock" Grid.Column="1" VerticalAlignment="Top" HorizontalAlignment="Center"
                        Margin="0,10,0,0"''')

# 2) 首页 hero 网格命名，便于切换
s = s.replace('''                        <Grid Grid.Row="1">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="1.05*"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>''', '''                        <Grid x:Name="HeroGrid" Grid.Row="1">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="1.05*"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>''')

io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("dock moved to top, hero named")
