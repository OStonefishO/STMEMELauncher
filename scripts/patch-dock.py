import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")

# 找到主体 Grid 的结尾：在「状态栏/底栏」之前插入底部 dock
idx = None
for i, l in enumerate(lines):
    if 'x:Name="RailPanel"' in l:
        idx = i
        break
assert idx

# dock 插到主体 Grid 结束前：找 </Grid>\n            </Grid> 结构太脆，改成插在 RailPanel 之前（同一父级）
dock = '''                <!-- 底部浮动 dock（新版布局：侧栏换成底部胶囊条） -->
                <Border x:Name="BottomDock" Grid.Column="1" VerticalAlignment="Bottom" HorizontalAlignment="Center"
                        Margin="0,0,0,14" CornerRadius="22" Background="#CC0E0718"
                        BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Padding="10,6"
                        Visibility="Collapsed">
                    <Border.Effect>
                        <DropShadowEffect Color="#000000" BlurRadius="26" ShadowDepth="4" Opacity="0.6"/>
                    </Border.Effect>
                    <StackPanel x:Name="DockPanel" Orientation="Horizontal"/>
                </Border>
'''.split("\n")

# 在 RailPanel 所在的 StackPanel 之前插入（它前面是 <!-- 导航栏 --> 注释）
insert_at = idx
while insert_at > 0 and '<!-- 导航栏' not in lines[insert_at]:
    insert_at -= 1
lines[insert_at:insert_at] = dock

io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
import xml.etree.ElementTree as ET
ET.parse(p)
print("bottom dock inserted before rail comment at", insert_at)
