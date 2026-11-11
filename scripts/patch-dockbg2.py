import io
# 1) 新增 DockBg 资源
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()
if 'x:Key="DockBg"' not in s:
    s = s.replace('<SolidColorBrush x:Key="Glass"', '<SolidColorBrush x:Key="DockBg" Color="#F0120A20"/>\n        <SolidColorBrush x:Key="Glass"', 1)
s = s.replace('CornerRadius="22" Background="#F0120A20"', 'CornerRadius="22" Background="{DynamicResource DockBg}"', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)

# 2) 每个风格设置 DockBg
p2 = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s2 = io.open(p2, encoding="utf-8", newline="").read()
s2 = s2.replace('''            res["FontNum"] = new FontFamily("Georgia");
            res["FontHead"] = new FontFamily("Georgia");''','''            res["FontNum"] = new FontFamily("Georgia");
            res["FontHead"] = new FontFamily("Georgia");
            res["DockBg"] = Frozen(Color.FromArgb(0xF5, 0xFD, 0xF9, 0xF0));''', 1)
s2 = s2.replace('''            res["FontNum"] = new FontFamily("Consolas");
            res["FontHead"] = new FontFamily("Consolas");''','''            res["FontNum"] = new FontFamily("Consolas");
            res["FontHead"] = new FontFamily("Consolas");
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x18, 0x18, 0x2C));''', 1)
s2 = s2.replace('''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["FontHead"] = new FontFamily("Microsoft YaHei UI Light");
        }
        else if (_terminal)''','''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["FontHead"] = new FontFamily("Microsoft YaHei UI Light");
            res["DockBg"] = Frozen(Color.FromArgb(0xC8, 0x1A, 0x2A, 0x4A));
        }
        else if (_terminal)''', 1)
s2 = s2.replace('''            res["FontNum"] = new FontFamily("Consolas");   // 终端风：等宽数字''','''            res["FontNum"] = new FontFamily("Consolas");   // 终端风：等宽数字
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x08, 0x0F, 0x0B));''', 1)
s2 = s2.replace('''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
        }
        else''','''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x12, 0x0A, 0x20));
        }
        else''', 1)
s2 = s2.replace('''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["FontHead"] = new FontFamily("Microsoft YaHei UI Light");
        }

        // 色相偏移''','''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["FontHead"] = new FontFamily("Microsoft YaHei UI Light");
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x12, 0x0A, 0x20));
        }

        // 色相偏移''', 1)
io.open(p2, "w", encoding="utf-8", newline="").write(s2)
print("dock bg wired")
