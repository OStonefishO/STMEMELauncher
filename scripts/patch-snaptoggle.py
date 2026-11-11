import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")
i = next(k for k,l in enumerate(lines) if 'x:Name="VersionSearchHint"' in l)
# 在搜索框 Grid 结束后插入「含预览版」勾选
j = next(k for k in range(i, len(lines)) if lines[k].strip() == '</Grid>')
toggle = [
 '                                        <CheckBox x:Name="SnapshotToggle" Content="含预览版（快照 / 愚人节）" FontSize="11.5"\r',
 '                                                  Foreground="{DynamicResource InkDim}" Margin="2,8,0,0"\r',
 '                                                  Checked="OnSnapshotToggle" Unchecked="OnSnapshotToggle"/>\r',
]
lines[j+1:j+1] = toggle
io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
import xml.etree.ElementTree as ET
ET.parse(p)
print("snapshot toggle added")
