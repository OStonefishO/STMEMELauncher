import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")

# 在仪表盘「内容 / 模组」卡片之前插入 3D 卡片，并把内容卡下移到 Row1/2
i = next(k for k,l in enumerate(lines) if 'x:Name="DashboardGrid"' in l)
j = next(k for k in range(i, len(lines)) if 'Content / 模组' in lines[k] or '内容 / 模组' in lines[k])

card = '''                            <!-- 3D 方块岛（新版首页保留的招牌画面） -->
                            <Border Grid.Column="1" Grid.Row="0" CornerRadius="20" Background="#0AFFFFFF"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" ClipToBounds="True"
                                    Margin="0,0,0,8">
                                <Grid Background="Transparent" Cursor="Hand" MouseLeftButtonDown="OnDashboardBiomeClick">
                                    <Viewport3D x:Name="DashViewport"/>
                                    <TextBlock x:Name="DashBiomeText" Text="主世界" FontSize="10.5"
                                               Foreground="{DynamicResource InkFaint}" HorizontalAlignment="Right"
                                               VerticalAlignment="Bottom" Margin="0,0,12,10" IsHitTestVisible="False"/>
                                </Grid>
                            </Border>
'''.split("\n")

# 把原「内容 / 模组」卡片从 Row0 挪到 Row1，快捷卡挪到 Row2：需要三行，改 Grid.RowDefinitions
lines[j-3:j-3] = card

io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
print("dash 3d card inserted near", j)
