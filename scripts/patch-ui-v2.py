import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")

def find(pred, start=0):
    for i in range(start, len(lines)):
        if pred(lines[i]):
            return i
    raise SystemExit("anchor not found")

# 1) 极光层：插到 LaunchPage 的 RowDefinitions 后面
i = find(lambda l: 'x:Name="LaunchPage"' in l)
j = find(lambda l: '</Grid.RowDefinitions>' in l, i)
aurora = '''                        <!-- 极光背景（仅新版布局显示） -->
                        <Canvas x:Name="AuroraLayer" Grid.RowSpan="3" IsHitTestVisible="False" ClipToBounds="True">
                            <Ellipse x:Name="AuroraA" Width="620" Height="620" Canvas.Left="-120" Canvas.Top="-160">
                                <Ellipse.Fill>
                                    <RadialGradientBrush>
                                        <GradientStop Color="#66FF3D8B" Offset="0"/>
                                        <GradientStop Color="#00FF3D8B" Offset="1"/>
                                    </RadialGradientBrush>
                                </Ellipse.Fill>
                                <Ellipse.Effect><BlurEffect Radius="90"/></Ellipse.Effect>
                            </Ellipse>
                            <Ellipse x:Name="AuroraB" Width="720" Height="720" Canvas.Left="360" Canvas.Top="-120">
                                <Ellipse.Fill>
                                    <RadialGradientBrush>
                                        <GradientStop Color="#557A3CFF" Offset="0"/>
                                        <GradientStop Color="#007A3CFF" Offset="1"/>
                                    </RadialGradientBrush>
                                </Ellipse.Fill>
                                <Ellipse.Effect><BlurEffect Radius="110"/></Ellipse.Effect>
                            </Ellipse>
                            <Ellipse x:Name="AuroraC" Width="560" Height="560" Canvas.Left="140" Canvas.Top="180">
                                <Ellipse.Fill>
                                    <RadialGradientBrush>
                                        <GradientStop Color="#4400E5FF" Offset="0"/>
                                        <GradientStop Color="#0000E5FF" Offset="1"/>
                                    </RadialGradientBrush>
                                </Ellipse.Fill>
                                <Ellipse.Effect><BlurEffect Radius="80"/></Ellipse.Effect>
                            </Ellipse>
                        </Canvas>'''.split("\n")
lines[j+1:j+1] = aurora

# 2) 大版本号：在 BigVersion 前加一个发光描边层
i = find(lambda l: 'x:Name="BigVersion"' in l)
glow = '''                                <Grid Margin="0,2,0,0">
                                    <TextBlock x:Name="BigVersionGlow" FontSize="96" FontFamily="{StaticResource FontNum}"
                                               FontWeight="Black" Foreground="#CCFF3D8B" Opacity="0.55"
                                               Text="{Binding Text, ElementName=BigVersion}" IsHitTestVisible="False">
                                        <TextBlock.Effect><BlurEffect Radius="26"/></TextBlock.Effect>
                                    </TextBlock>'''.split("\n")
lines[i:i] = glow
# 原来的 BigVersion 也要包在 Grid 里、字号放大：改成 96 并补一个结束标签
i2 = find(lambda l: 'x:Name="BigVersion"' in l)
lines[i2] = lines[i2].replace('FontSize="62"', 'FontSize="96"').replace('FontWeight="SemiBold"', 'FontWeight="Black"')
lines.insert(i2+2, '                                </Grid>')

# 3) 侧栏列命名 + 悬停展开
i = find(lambda l: '<ColumnDefinition Width="74"/>' in l)
lines[i] = lines[i].replace('<ColumnDefinition Width="74"/>', '<ColumnDefinition x:Name="RailColumn" Width="74"/>')
i = find(lambda l: '<!-- 导航栏 -->' in l)
lines[i+1] = lines[i+1].replace('<StackPanel Grid.Column="0" VerticalAlignment="Center" Margin="0,0,0,40">',
    '<StackPanel x:Name="RailPanel" Grid.Column="0" VerticalAlignment="Center" Margin="0,0,0,40"\n                            MouseEnter="OnRailEnter" MouseLeave="OnRailLeave">')

io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
import xml.etree.ElementTree as ET
ET.parse(p)
print("aurora + glow + rail hooks inserted")
