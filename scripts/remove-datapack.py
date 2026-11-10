import io, os, re

root = r"D:\综合创作\MCLauncher"
xaml = os.path.join(root, r"src\Aurora.App\MainWindow.xaml")
cs = os.path.join(root, r"src\Aurora.App\MainWindow.Creation.cs")
main = os.path.join(root, r"src\Aurora.App\MainWindow.xaml.cs")
dp = os.path.join(root, r"src\Aurora.Core\Creation\DatapackBuilder.cs")

# ---------- XAML ----------
t = io.open(xaml, encoding="utf-8").read()

# 1) 删掉两个 tab 按钮，换一行说明
tab_start = t.find('<Button x:Name="CreateTabPack"')
tab_end = t.find('</StackPanel>', tab_start)
if tab_start > 0 and tab_end > tab_start:
    before = t.rfind('<StackPanel Orientation="Horizontal">', 0, tab_start)
    block_end = tab_end + len('</StackPanel>')
    t = t[:before] + '<TextBlock Text="把某个版本打包成整合包：自选要带上的内容，元数据会写进 README 和 meme-modpack.json"\n                                       FontSize="11.5" Foreground="{StaticResource InkFaint}"\n                                       VerticalAlignment="Center"/>' + t[block_end:]
    print("tabs removed")
else:
    print("tab block NOT found")

# 2) 删掉整个数据包面板
marker = '<!-- ===== 数据包 ===== -->'
mi = t.find(marker)
if mi > 0:
    end_marker = '</ScrollViewer>\r\n                        </Grid>\r\n                    </Grid>'
    ei = t.find(end_marker, mi)
    if ei < 0:
        end_marker = '</ScrollViewer>\n                        </Grid>\n                    </Grid>'
        ei = t.find(end_marker, mi)
    if ei > 0:
        cut_to = ei + len('</ScrollViewer>')
        t = t[:mi] + t[cut_to:]
        # 清理 CreatePackPanel 上的空行
        t = t.replace('\r\n\r\n\r\n                        </Grid>', '\r\n                        </Grid>')
        print("data panel removed")
    else:
        print("panel end NOT found")
else:
    print("panel marker NOT found")

io.open(xaml, "w", encoding="utf-8", newline="").write(t)

# ---------- MainWindow.Creation.cs ----------
c = io.open(cs, encoding="utf-8").read()

# 删掉模板初始化块
start = c.find('        // 数据包模板')
if start > 0:
    end = c.find('        // 版本下拉', start)
    if end > start:
        c = c[:start] + c[end:]
        print("template init removed")

# 删掉数据包相关方法（从 OnDataVersionChanged 到文件末尾的类结束）
s = c.find('    // ---------------- 数据包 ----------------')
if s > 0:
    e = c.rfind('}')
    # 保留类最后的 }
    c = c[:s] + '}\n'
    print("datapack methods removed")

io.open(cs, "w", encoding="utf-8", newline="").write(c)

# ---------- MainWindow.xaml.cs：去掉 --create 参数 ----------
m = io.open(main, encoding="utf-8").read()
s = m.find('            // 调试用：--create data')
if s > 0:
    e = m.find('            // 调试用：--query', s)
    if e > s:
        m = m[:s] + m[e:]
        print("--create arg removed")
io.open(main, "w", encoding="utf-8", newline="").write(m)

# ---------- 删除 DatapackBuilder.cs ----------
if os.path.exists(dp):
    os.remove(dp)
    print("DatapackBuilder.cs deleted")

print("done")
