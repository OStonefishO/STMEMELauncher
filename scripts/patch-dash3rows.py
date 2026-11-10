import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()

# 三行布局
s = s.replace('''                            <Grid.RowDefinitions>
                                <RowDefinition Height="*"/>
                                <RowDefinition Height="*"/>
                            </Grid.RowDefinitions>

                            <Border Grid.RowSpan="2" CornerRadius="22"''', '''                            <Grid.RowDefinitions>
                                <RowDefinition Height="1.05*"/>
                                <RowDefinition Height="0.95*"/>
                                <RowDefinition Height="0.9*"/>
                            </Grid.RowDefinitions>

                            <Border Grid.RowSpan="3" CornerRadius="22"''')

# 内容卡挪到 Row1
s = s.replace('''<Border Grid.Column="1" Grid.Row="0" CornerRadius="20" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Padding="20,16" Margin="0,0,0,8">
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock Text="内容 / 模组"''', '''<Border Grid.Column="1" Grid.Row="1" CornerRadius="20" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Padding="20,16" Margin="0,0,0,8">
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock Text="内容 / 模组"''')

# 快捷卡挪到 Row2
s = s.replace('''<Border Grid.Column="1" Grid.Row="1" CornerRadius="20" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Padding="20,16">
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock Text="快捷入口"''', '''<Border Grid.Column="1" Grid.Row="2" CornerRadius="20" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Padding="20,16">
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock Text="快捷入口"''')

io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("dashboard 3 rows ok")
