import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")

i = next(k for k,l in enumerate(lines) if 'x:Name="HeroGrid"' in l)

dash = '''                        <!-- 仪表盘首页（新版布局） -->
                        <Grid x:Name="DashboardGrid" Grid.Row="1" Margin="26,0,26,10" Visibility="Collapsed">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="1.35*"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="*"/>
                                <RowDefinition Height="*"/>
                            </Grid.RowDefinitions>

                            <!-- 主卡：版本 + 启动 -->
                            <Border Grid.RowSpan="2" CornerRadius="22" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Padding="28,24"
                                    Margin="0,0,14,0">
                                <Grid>
                                    <StackPanel VerticalAlignment="Center">
                                        <TextBlock Text="CURRENT VERSION" FontSize="10.5" FontWeight="SemiBold"
                                                   Foreground="{DynamicResource InkFaint}"/>
                                        <Grid Margin="0,6,0,0">
                                            <TextBlock FontSize="104" FontWeight="Black" FontFamily="{StaticResource FontNum}"
                                                       Foreground="{DynamicResource AccentGrad}" Opacity="0.45"
                                                       Text="{Binding Text, ElementName=BigVersion}" IsHitTestVisible="False">
                                                <TextBlock.Effect><BlurEffect Radius="28"/></TextBlock.Effect>
                                            </TextBlock>
                                            <TextBlock FontSize="104" FontWeight="Black" FontFamily="{StaticResource FontNum}"
                                                       Foreground="{DynamicResource TitleGrad}"
                                                       Text="{Binding Text, ElementName=BigVersion}"/>
                                        </Grid>
                                        <TextBlock FontSize="18" FontWeight="SemiBold" Foreground="{DynamicResource Ink}"
                                                   Margin="0,2,0,0" TextTrimming="CharacterEllipsis"
                                                   Text="{Binding Text, ElementName=DetName}"/>
                                        <TextBlock FontSize="12" Foreground="{DynamicResource InkDim}" Margin="0,6,0,0"
                                                   TextWrapping="Wrap" Text="{Binding Text, ElementName=DetMeta}"/>
                                        <Button Content="▶  开始游戏" Style="{StaticResource HeroButton}" Height="58"
                                                FontSize="17" Margin="0,22,0,0" Click="OnLaunch"/>
                                        <StackPanel Orientation="Horizontal" Margin="0,12,0,0">
                                            <Button Content="打开目录" Style="{StaticResource GlassButton}" Click="OnOpenGameDir" Tag="noBounce"/>
                                            <Button Content="版本管理" Style="{StaticResource GlassButton}" Margin="8,0,0,0" Tag="2" Click="OnNavClick" Tag="noBounce"/>
                                        </StackPanel>
                                    </StackPanel>
                                </Grid>
                            </Border>

                            <!-- 右列小卡 -->
                            <Border Grid.Column="1" Grid.Row="0" CornerRadius="20" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Padding="20,16" Margin="0,0,0,8">
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock Text="内容 / 模组" FontSize="11.5" Foreground="{DynamicResource InkFaint}"/>
                                    <TextBlock Text="装模组、材质包、整合包" FontSize="15" FontWeight="SemiBold"
                                               Foreground="{DynamicResource Ink}" Margin="0,6,0,2"/>
                                    <TextBlock x:Name="DashContentText" Text="打开内容库挑东西" FontSize="11.5"
                                               Foreground="{DynamicResource InkDim}"/>
                                    <Button Content="进入内容页 →" Style="{StaticResource GlassButton}" Margin="0,12,0,0"
                                            HorizontalAlignment="Left" Tag="2" Click="OnNavClick"/>
                                </StackPanel>
                            </Border>

                            <Border Grid.Column="1" Grid.Row="1" CornerRadius="20" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Padding="20,16" Margin="0,0,0,0">
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock Text="快捷" FontSize="11.5" Foreground="{DynamicResource InkFaint}"/>
                                    <WrapPanel Margin="0,10,0,0">
                                        <Button Content="账户" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="3" Click="OnNavClick"/>
                                        <Button Content="联机" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="6" Click="OnNavClick"/>
                                        <Button Content="创作" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="9" Click="OnNavClick"/>
                                        <Button Content="设置" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="4" Click="OnNavClick"/>
                                    </WrapPanel>
                                </StackPanel>
                            </Border>
                        </Grid>
'''.split("\n")

lines[i:i] = dash
io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
import xml.etree.ElementTree as ET
ET.parse(p)
print("dashboard inserted at", i)
