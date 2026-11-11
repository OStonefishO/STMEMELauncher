import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")
i = next(k for k,l in enumerate(lines) if 'x:Name="DashboardGrid"' in l)

# 纸质便签：单列「活页」清单
paper = '''                        <!-- 纸质便签首页：单列活页清单 -->
                        <Grid x:Name="PaperHome" Grid.Row="1" Margin="26,0,26,60" Visibility="Collapsed">
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="*"/>
                            </Grid.RowDefinitions>

                            <Border CornerRadius="6" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Padding="26,20" Margin="0,4,0,12">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>
                                    <StackPanel>
                                        <TextBlock Text="CURRENT VERSION" FontSize="10" FontWeight="SemiBold"
                                                   Foreground="{DynamicResource InkFaint}"/>
                                        <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
                                            <TextBlock FontSize="54" FontFamily="{DynamicResource FontNum}"
                                                       Foreground="{DynamicResource TitleGrad}"
                                                       Text="{Binding Text, ElementName=BigVersion}"/>
                                        </StackPanel>
                                        <TextBlock FontSize="15" FontWeight="SemiBold" Foreground="{DynamicResource Ink}"
                                                   Text="{Binding Text, ElementName=DetName}" Margin="0,2,0,0"/>
                                        <TextBlock FontSize="11.5" Foreground="{DynamicResource InkDim}" Margin="0,4,0,0"
                                                   TextWrapping="Wrap" Text="{Binding Text, ElementName=DetMeta}"/>
                                    </StackPanel>
                                    <StackPanel Grid.Column="1" VerticalAlignment="Center">
                                        <Button Content="▶  开始游戏" Style="{StaticResource HeroButton}" Width="210" Height="46"
                                                FontSize="15" Click="OnLaunch" Tag="noBounce"/>
                                        <Button x:Name="PaperKillButton" Content="强制关闭游戏" Style="{StaticResource GlassButton}"
                                                Margin="0,8,0,0" Click="OnForceKillGame" Visibility="Collapsed"/>
                                    </StackPanel>
                                </Grid>
                            </Border>

                            <StackPanel Grid.Row="1">
                                <Border CornerRadius="6" Background="{DynamicResource Glass}" BorderBrush="{DynamicResource GlassLine}"
                                        BorderThickness="1" Padding="20,14" Margin="0,0,0,8">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>
                                        <TextBlock Text="01" FontFamily="Georgia" FontSize="13" Foreground="{DynamicResource InkFaint}"
                                                   VerticalAlignment="Center" Width="34"/>
                                        <StackPanel Grid.Column="1" Margin="6,0,0,0">
                                            <TextBlock Text="内容库" FontSize="13.5" FontWeight="SemiBold" Foreground="{DynamicResource Ink}"/>
                                            <TextBlock Text="模组 / 材质包 / 光影 / 整合包，四个来源" FontSize="11" Foreground="{DynamicResource InkDim}"/>
                                        </StackPanel>
                                        <Button Grid.Column="2" Content="进入 →" Style="{StaticResource GlassButton}" Tag="2" Click="OnNavClick"/>
                                    </Grid>
                                </Border>
                                <Border CornerRadius="6" Background="{DynamicResource Glass}" BorderBrush="{DynamicResource GlassLine}"
                                        BorderThickness="1" Padding="20,14" Margin="0,0,0,8">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>
                                        <TextBlock Text="02" FontFamily="Georgia" FontSize="13" Foreground="{DynamicResource InkFaint}"
                                                   VerticalAlignment="Center" Width="34"/>
                                        <StackPanel Grid.Column="1" Margin="6,0,0,0">
                                            <TextBlock Text="版本" FontSize="13.5" FontWeight="SemiBold" Foreground="{DynamicResource Ink}"/>
                                            <TextBlock Text="安装、隔离、导入存档" FontSize="11" Foreground="{DynamicResource InkDim}"/>
                                        </StackPanel>
                                        <Button Grid.Column="2" Content="进入 →" Style="{StaticResource GlassButton}" Tag="7" Click="OnNavClick"/>
                                    </Grid>
                                </Border>
                                <Border CornerRadius="6" Background="{DynamicResource Glass}" BorderBrush="{DynamicResource GlassLine}"
                                        BorderThickness="1" Padding="20,14" Margin="0,0,0,8">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>
                                        <TextBlock Text="03" FontFamily="Georgia" FontSize="13" Foreground="{DynamicResource InkFaint}"
                                                   VerticalAlignment="Center" Width="34"/>
                                        <StackPanel Grid.Column="1" Margin="6,0,0,0">
                                            <TextBlock Text="联机" FontSize="13.5" FontWeight="SemiBold" Foreground="{DynamicResource Ink}"/>
                                            <TextBlock Text="一键开服 + 樱花穿透" FontSize="11" Foreground="{DynamicResource InkDim}"/>
                                        </StackPanel>
                                        <Button Grid.Column="2" Content="进入 →" Style="{StaticResource GlassButton}" Tag="6" Click="OnNavClick"/>
                                    </Grid>
                                </Border>
                                <Border CornerRadius="6" Background="{DynamicResource Glass}" BorderBrush="{DynamicResource GlassLine}"
                                        BorderThickness="1" Padding="20,14">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>
                                        <TextBlock Text="04" FontFamily="Georgia" FontSize="13" Foreground="{DynamicResource InkFaint}"
                                                   VerticalAlignment="Center" Width="34"/>
                                        <StackPanel Grid.Column="1" Margin="6,0,0,0">
                                            <TextBlock Text="账号 / 设置" FontSize="13.5" FontWeight="SemiBold" Foreground="{DynamicResource Ink}"/>
                                            <TextBlock Text="微软登录、内存、镜像、界面" FontSize="11" Foreground="{DynamicResource InkDim}"/>
                                        </StackPanel>
                                        <StackPanel Grid.Column="2" Orientation="Horizontal">
                                            <Button Content="账号" Style="{StaticResource GlassButton}" Tag="3" Click="OnNavClick"/>
                                            <Button Content="设置" Style="{StaticResource GlassButton}" Margin="8,0,0,0" Tag="4" Click="OnNavClick"/>
                                        </StackPanel>
                                    </Grid>
                                </Border>
                            </StackPanel>
                        </Grid>

'''
lines[i:i] = paper.split("\n")

# 像素 8bit 首页：2x2 方块网格
j = next(k for k,l in enumerate(lines) if 'x:Name="DashboardGrid"' in l)
pixel = '''                        <!-- 像素 8bit 首页：方块网格 -->
                        <Grid x:Name="PixelHome" Grid.Row="1" Margin="26,0,26,60" Visibility="Collapsed">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="1.25*"/>
                                <RowDefinition Height="*"/>
                            </Grid.RowDefinitions>

                            <Border Grid.RowSpan="2" CornerRadius="2" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="2" Padding="22" Margin="0,0,8,0">
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock Text="CURRENT VERSION" FontSize="10" FontFamily="{DynamicResource FontNum}"
                                               Foreground="{DynamicResource InkFaint}"/>
                                    <TextBlock FontSize="72" FontFamily="{DynamicResource FontNum}" FontWeight="Bold"
                                               Foreground="{DynamicResource TitleGrad}" Margin="0,8,0,0"
                                               Text="{Binding Text, ElementName=BigVersion}"/>
                                    <TextBlock FontSize="15" FontWeight="Bold" Foreground="{DynamicResource Ink}"
                                               Margin="0,6,0,0" Text="{Binding Text, ElementName=DetName}"/>
                                    <TextBlock FontSize="11" Foreground="{DynamicResource InkDim}" Margin="0,4,0,0"
                                               TextWrapping="Wrap" Text="{Binding Text, ElementName=DetMeta}"/>
                                    <Button Content="▶ START" Style="{StaticResource HeroButton}" Height="52" Margin="0,20,0,0"
                                            FontFamily="{DynamicResource FontNum}" FontSize="15" Click="OnLaunch" Tag="noBounce"/>
                                    <Button x:Name="PixelKillButton" Content="强制关闭" Style="{StaticResource GlassButton}"
                                            Margin="0,8,0,0" Click="OnForceKillGame" Visibility="Collapsed"/>
                                </StackPanel>
                            </Border>

                            <Border Grid.Column="1" Grid.Row="0" CornerRadius="2" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="2" Padding="18" Margin="8,0,0,8">
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock Text="CONTENT" FontSize="10" FontFamily="{DynamicResource FontNum}"
                                               Foreground="{DynamicResource InkFaint}"/>
                                    <TextBlock Text="内容库" FontSize="18" FontWeight="Bold" Foreground="{DynamicResource Ink}" Margin="0,6,0,0"/>
                                    <Button Content="进入 →" Style="{StaticResource GlassButton}" Margin="0,14,0,0"
                                            HorizontalAlignment="Left" Tag="2" Click="OnNavClick"/>
                                </StackPanel>
                            </Border>

                            <Border Grid.Column="1" Grid.Row="1" CornerRadius="2" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="2" Padding="18" Margin="8,8,0,0">
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock Text="MENU" FontSize="10" FontFamily="{DynamicResource FontNum}"
                                               Foreground="{DynamicResource InkFaint}"/>
                                    <WrapPanel Margin="0,10,0,0">
                                        <Button Content="版本" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="7" Click="OnNavClick"/>
                                        <Button Content="联机" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="6" Click="OnNavClick"/>
                                        <Button Content="账号" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="3" Click="OnNavClick"/>
                                        <Button Content="设置" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="4" Click="OnNavClick"/>
                                    </WrapPanel>
                                </StackPanel>
                            </Border>
                        </Grid>

'''
lines[j:j] = pixel.split("\n")

io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
import xml.etree.ElementTree as ET
ET.parse(p)
print("paper + pixel home panels added")
