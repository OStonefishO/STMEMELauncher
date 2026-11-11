import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")
i = next(k for k,l in enumerate(lines) if 'x:Name="PixelHome"' in l)
# 插到 PixelHome 前面
vivid = '''                        <!-- 新版（动感）首页：左侧信息 + 右侧整屏 3D 岛 -->
                        <Grid x:Name="VividHome" Grid.Row="1" Margin="26,0,26,60" Visibility="Collapsed">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="1.02*"/>
                                <ColumnDefinition Width="1.15*"/>
                            </Grid.ColumnDefinitions>

                            <StackPanel VerticalAlignment="Center" Margin="6,0,10,0">
                                <TextBlock Text="CURRENT VERSION" FontSize="10.5" FontWeight="SemiBold"
                                           Foreground="{DynamicResource InkFaint}"/>
                                <TextBlock FontSize="96" FontWeight="Black" Margin="0,4,0,0"
                                           FontFamily="{DynamicResource FontNum}"
                                           Foreground="{DynamicResource TitleGrad}"
                                           Text="{Binding Text, ElementName=BigVersion}"/>
                                <TextBlock FontSize="19" FontWeight="SemiBold" Foreground="{DynamicResource Ink}"
                                           Margin="0,2,0,0" Text="{Binding Text, ElementName=DetName}"/>
                                <TextBlock FontSize="12" Foreground="{DynamicResource InkDim}" Margin="0,6,0,0"
                                           TextWrapping="Wrap" Text="{Binding Text, ElementName=DetMeta}"/>
                                <Button Content="▶  开始游戏" Style="{StaticResource HeroButton}" Height="62"
                                        FontSize="18" Margin="0,24,0,0" Click="OnLaunch" Tag="noBounce"/>
                                <WrapPanel Margin="0,14,0,0">
                                    <Button Content="打开目录" Style="{StaticResource GlassButton}" Click="OnOpenFolder"/>
                                    <Button Content="内容库" Style="{StaticResource GlassButton}" Margin="8,0,0,0" Tag="2" Click="OnNavClick"/>
                                    <Button Content="版本管理" Style="{StaticResource GlassButton}" Margin="8,0,0,0" Tag="7" Click="OnNavClick"/>
                                    <Button x:Name="VividKillButton" Content="强制关闭" Style="{StaticResource DangerButton}"
                                            Margin="8,0,0,0" Click="OnForceKillGame" Visibility="Collapsed"/>
                                </WrapPanel>
                            </StackPanel>

                            <Grid Grid.Column="1" ClipToBounds="True">
                                <Viewport3D x:Name="VividViewport" IsHitTestVisible="True" Cursor="Hand"
                                            MouseLeftButtonDown="OnDashboardBiomeClick"/>
                                <TextBlock x:Name="VividBiomeText" Text="主世界 · 点一下换群系" FontSize="10.5"
                                           Foreground="{DynamicResource InkFaint}" HorizontalAlignment="Right"
                                           VerticalAlignment="Bottom" Margin="0,0,10,6" IsHitTestVisible="False"/>
                            </Grid>
                        </Grid>

'''
lines[i:i] = vivid.split("\n")

j = next(k for k,l in enumerate(lines) if 'x:Name="PixelHome"' in l)
term = '''                        <!-- 终端首页：命令行 + 信息表 -->
                        <Grid x:Name="TerminalHome" Grid.Row="1" Margin="26,0,26,60" Visibility="Collapsed">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="1.45*"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>

                            <Border CornerRadius="8" Background="#FF070C09" BorderBrush="{DynamicResource GlassLine}"
                                    BorderThickness="1" Margin="0,4,8,0" Padding="0">
                                <Grid>
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height="Auto"/>
                                        <RowDefinition Height="*"/>
                                    </Grid.RowDefinitions>
                                    <Border CornerRadius="7,7,0,0" Background="#FF0E1712" Padding="12,7">
                                        <StackPanel Orientation="Horizontal">
                                            <Ellipse Width="8" Height="8" Fill="#FF35F07A" Margin="0,0,8,0" VerticalAlignment="Center"/>
                                            <TextBlock Text="meme@launcher: ~" FontFamily="Consolas" FontSize="11.5"
                                                       Foreground="{DynamicResource InkDim}"/>
                                        </StackPanel>
                                    </Border>
                                    <StackPanel Grid.Row="1" Margin="16,14,16,16">
                                        <TextBlock Text="meme-launcher --status" FontFamily="Consolas" FontSize="12"
                                                   Foreground="{DynamicResource InkFaint}"/>
                                        <TextBlock FontFamily="Consolas" FontSize="13" Foreground="{DynamicResource Ink}"
                                                   Margin="0,10,0,0" TextWrapping="Wrap">
                                            <Run Text="version  : "/><Run Text="{Binding Text, ElementName=BigVersion}" Foreground="#FF35F07A"/>
                                        </TextBlock>
                                        <TextBlock FontFamily="Consolas" FontSize="12.5" Foreground="{DynamicResource InkDim}"
                                                   Margin="0,4,0,0" TextWrapping="Wrap"
                                                   Text="{Binding Text, ElementName=DetName}"/>
                                        <TextBlock FontFamily="Consolas" FontSize="11.5" Foreground="{DynamicResource InkFaint}"
                                                   Margin="0,2,0,0" TextWrapping="Wrap"
                                                   Text="{Binding Text, ElementName=DetMeta}"/>
                                        <TextBlock Text="> 选择操作：" FontFamily="Consolas" FontSize="12"
                                                   Foreground="#FF35F07A" Margin="0,16,0,0"/>
                                        <Button Content="&gt; start game" HorizontalAlignment="Left" Margin="0,8,0,0"
                                                Style="{StaticResource HeroButton}" Height="46" Padding="26,0"
                                                FontFamily="Consolas" Click="OnLaunch" Tag="noBounce"/>
                                        <WrapPanel Margin="0,10,0,0">
                                            <Button Content="&gt; contents" Style="{StaticResource GlassButton}" Margin="0,0,8,8"
                                                    FontFamily="Consolas" Tag="2" Click="OnNavClick"/>
                                            <Button Content="&gt; versions" Style="{StaticResource GlassButton}" Margin="0,0,8,8"
                                                    FontFamily="Consolas" Tag="7" Click="OnNavClick"/>
                                            <Button x:Name="TermKillButton" Content="&gt; kill" Style="{StaticResource DangerButton}"
                                                    Margin="0,0,8,8" FontFamily="Consolas" Click="OnForceKillGame" Visibility="Collapsed"/>
                                        </WrapPanel>
                                    </StackPanel>
                                </Grid>
                            </Border>

                            <Border Grid.Column="1" CornerRadius="8" Background="{DynamicResource Glass}"
                                    BorderBrush="{DynamicResource GlassLine}" BorderThickness="1" Margin="8,4,0,0" Padding="16,14">
                                <StackPanel>
                                    <TextBlock Text="SYSTEM" FontFamily="Consolas" FontSize="10.5"
                                               Foreground="{DynamicResource InkFaint}"/>
                                    <TextBlock Text="-------------------" FontFamily="Consolas" FontSize="11"
                                               Foreground="{DynamicResource InkFaint}" Margin="0,6,0,0"/>
                                    <TextBlock Text="user   : 1134 (offline)" FontFamily="Consolas" FontSize="11.5"
                                               Foreground="{DynamicResource InkDim}" Margin="0,6,0,0"/>
                                    <TextBlock Text="dir    : 见设置" FontFamily="Consolas" FontSize="11.5"
                                               Foreground="{DynamicResource InkDim}" Margin="0,4,0,0"/>
                                    <TextBlock Text="ui     : terminal" FontFamily="Consolas" FontSize="11.5"
                                               Foreground="{DynamicResource InkDim}" Margin="0,4,0,0"/>
                                    <TextBlock Text="-------------------" FontFamily="Consolas" FontSize="11"
                                               Foreground="{DynamicResource InkFaint}" Margin="0,10,0,0"/>
                                    <WrapPanel Margin="0,10,0,0">
                                        <Button Content="账户" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="3" Click="OnNavClick"/>
                                        <Button Content="联机" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="6" Click="OnNavClick"/>
                                        <Button Content="设置" Style="{StaticResource GlassButton}" Margin="0,0,8,8" Tag="4" Click="OnNavClick"/>
                                    </WrapPanel>
                                </StackPanel>
                            </Border>
                        </Grid>

'''
lines[j:j] = term.split("\n")

k = next(x for x,l in enumerate(lines) if 'x:Name="PixelHome"' in l)
aurora = '''                        <!-- 极光首页：全屏 3D 背景 + 居中玻璃卡 -->
                        <Grid x:Name="AuroraHome" Grid.Row="1" Margin="26,0,26,60" Visibility="Collapsed">
                            <Border MaxWidth="760" CornerRadius="28" Background="#40080F20"
                                    BorderBrush="#66B6C8FF" BorderThickness="1" Padding="34,28"
                                    HorizontalAlignment="Center" VerticalAlignment="Center">
                                <Border.Effect>
                                    <DropShadowEffect Color="#9952C7FF" BlurRadius="60" ShadowDepth="0" Opacity="0.5"/>
                                </Border.Effect>
                                <StackPanel>
                                    <TextBlock Text="CURRENT VERSION" FontSize="10.5" Foreground="{DynamicResource InkFaint}"
                                               HorizontalAlignment="Center"/>
                                    <TextBlock FontSize="86" FontWeight="Black" FontFamily="{DynamicResource FontNum}"
                                               HorizontalAlignment="Center" Margin="0,2,0,0"
                                               Foreground="{DynamicResource TitleGrad}"
                                               Text="{Binding Text, ElementName=BigVersion}"/>
                                    <TextBlock FontSize="17" FontWeight="SemiBold" Foreground="{DynamicResource Ink}"
                                               HorizontalAlignment="Center" Margin="0,4,0,0"
                                               Text="{Binding Text, ElementName=DetName}"/>
                                    <TextBlock FontSize="11.5" Foreground="{DynamicResource InkDim}" HorizontalAlignment="Center"
                                               Margin="0,6,0,0" Text="{Binding Text, ElementName=DetMeta}"/>
                                    <Button Content="▶  开始游戏" Style="{StaticResource HeroButton}" Height="52" Width="260"
                                            HorizontalAlignment="Center" Margin="0,20,0,0" Click="OnLaunch" Tag="noBounce"/>
                                    <WrapPanel HorizontalAlignment="Center" Margin="0,14,0,0">
                                        <Button Content="打开目录" Style="{StaticResource GlassButton}" Click="OnOpenFolder"/>
                                        <Button Content="内容库" Style="{StaticResource GlassButton}" Margin="8,0,0,0" Tag="2" Click="OnNavClick"/>
                                        <Button Content="版本" Style="{StaticResource GlassButton}" Margin="8,0,0,0" Tag="7" Click="OnNavClick"/>
                                        <Button x:Name="AuroraKillButton" Content="强制关闭" Style="{StaticResource DangerButton}"
                                                Margin="8,0,0,0" Click="OnForceKillGame" Visibility="Collapsed"/>
                                    </WrapPanel>
                                </StackPanel>
                            </Border>
                        </Grid>

'''
lines[k:k] = aurora.split("\n")

io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
import xml.etree.ElementTree as ET
ET.parse(p)
print("three new home panels added")
