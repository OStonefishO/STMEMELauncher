import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 字体：Window 字体资源化
s = s.replace('FontFamily="Microsoft YaHei UI" FontSize="13"', 'FontFamily="{DynamicResource FontBody}" FontSize="13"', 1)
s = s.replace('<FontFamily x:Key="FontHead">Microsoft YaHei UI Light</FontFamily>',
'''<FontFamily x:Key="FontHead">Microsoft YaHei UI Light</FontFamily>
        <FontFamily x:Key="FontBody">Microsoft YaHei UI</FontFamily>''', 1)

# 2) 扫描线覆盖层 + 极光整屏 3D 背景（插到背景 Border 之后）
anchor = '<Border Background="{DynamicResource AppBg}"/>'
addition = anchor + '''
        <!-- 极光版：整屏 3D 方块岛背景 -->
        <Viewport3D x:Name="AuroraBackdrop" IsHitTestVisible="False" Opacity="0.55" Visibility="Collapsed">
            <Viewport3D.Camera>
                <PerspectiveCamera Position="9,7,11" LookDirection="-9,-4.5,-11" UpDirection="0,1,0" FieldOfView="45"/>
            </Viewport3D.Camera>
        </Viewport3D>

        <!-- 扫描线（像素 / 终端） -->
        <Rectangle x:Name="ScanlineOverlay" IsHitTestVisible="False" Visibility="Collapsed" Opacity="0.16">
            <Rectangle.Fill>
                <DrawingBrush TileMode="Tile" Viewport="0,0,4,4" ViewportUnits="Absolute">
                    <DrawingBrush.Drawing>
                        <GeometryDrawing Brush="#22000000">
                            <GeometryDrawing.Geometry>
                                <RectangleGeometry Rect="0,0,4,1"/>
                            </GeometryDrawing.Geometry>
                        </GeometryDrawing>
                    </DrawingBrush.Drawing>
                </DrawingBrush>
            </Rectangle.Fill>
        </Rectangle>
'''
assert anchor in s
s = s.replace(anchor, addition, 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("overlays added")
