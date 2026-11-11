import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# vivid 分支（粉色主题）补一套深色下拉
s = s.replace('''            res["ComboItemActive"] = Frozen(Color.FromArgb(0x38, 0xFF, 0x5C, 0xC8));''','''            res["ComboItemActive"] = Frozen(Color.FromArgb(0x38, 0xFF, 0x5C, 0xC8));
            res["FieldBg"] = Frozen(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
            res["FieldLine"] = Frozen(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));''', 1)

# classic 分支补下拉配色（之前没设，会残留上一个主题的浅色弹层）
s = s.replace('''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["FontHead"] = new FontFamily("Microsoft YaHei UI Light");
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x12, 0x0A, 0x20));
        }

        // 色相偏移''','''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["FontHead"] = new FontFamily("Microsoft YaHei UI Light");
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x12, 0x0A, 0x20));
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x14, 0x18, 0x22));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x33, 0x5B, 0x8C, 0xFF));
            res["FieldBg"] = Frozen(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
            res["FieldLine"] = Frozen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        }

        // 色相偏移''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("classic + vivid combo colors set")
