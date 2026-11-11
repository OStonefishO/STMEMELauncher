import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")

start = next(i for i,l in enumerate(lines) if '<Grid Grid.Row="2" Margin="26,12,0,8">' in l)
end = next(i for i,l in enumerate(lines) if i > start and l.strip() == "</Grid>" and lines[i-1].strip().startswith("Foreground=\"{DynamicResource InkDim}\""))
# 上一行是 TextBlock 的续行，真正的结束在 end 这一行
replacement = [
    '                        <!-- 底部状态提示（安装条已移除：进详情页就能选版本安装） -->',
    '                        <TextBlock Grid.Row="2" x:Name="ContentStatus" Text="搜索后点一个条目进详情，选版本安装"',
    '                                   FontSize="11.5" Margin="26,8,0,10"',
    '                                   Foreground="{DynamicResource InkDim}" TextWrapping="Wrap"/>',
]
lines[start:end+1] = replacement
io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
import xml.etree.ElementTree as ET
ET.parse(p)
print("install bar removed lines", start, end)
