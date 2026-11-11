import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()

# 极光背景相机拉远，避免糊满屏
s = s.replace('<PerspectiveCamera Position="9,7,11" LookDirection="-9,-4.5,-11" UpDirection="0,1,0" FieldOfView="45"/>',
              '<PerspectiveCamera Position="17,13,21" LookDirection="-17,-7.5,-21" UpDirection="0,1,0" FieldOfView="42"/>', 1)
s = s.replace('<Viewport3D x:Name="AuroraBackdrop" IsHitTestVisible="False" Opacity="0.55" Visibility="Collapsed">',
              '<Viewport3D x:Name="AuroraBackdrop" IsHitTestVisible="False" Opacity="0.42" Visibility="Collapsed">', 1)

# 纸质便签：手写贴纸
anchor = '''                            <StackPanel Grid.Row="1">'''
note = '''                            <StackPanel Grid.Row="1">
                                <Grid>
                                    <Border HorizontalAlignment="Right" VerticalAlignment="Top" Margin="0,-6,4,0"
                                            Background="#FFF4D27A" CornerRadius="3" Padding="14,8"
                                            RenderTransformOrigin="0.5,0.5" Panel.ZIndex="2">
                                        <Border.RenderTransform>
                                            <RotateTransform Angle="3.2"/>
                                        </Border.RenderTransform>
                                        <Border.Effect>
                                            <DropShadowEffect Color="#552B2622" BlurRadius="10" ShadowDepth="2" Opacity="0.5"/>
                                        </Border.Effect>
                                        <StackPanel>
                                            <TextBlock Text="记得先备份存档 ✍" FontFamily="Segoe Script" FontSize="13"
                                                       Foreground="#FF6B4A2E"/>
                                            <TextBlock Text="装模组前把 saves 复制一份" FontFamily="Segoe Script" FontSize="10.5"
                                                       Foreground="#FF8A6A4A" Margin="0,2,0,0"/>
                                        </StackPanel>
                                    </Border>
                                </Grid>'''
assert anchor in s
s = s.replace(anchor, note, 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("aurora camera + paper sticker")
