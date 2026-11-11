import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace('''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x12, 0x0A, 0x20));
        }
        else''','''            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x12, 0x0A, 0x20));
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x17, 0x0C, 0x28));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0x45, 0xFF, 0x5C, 0xC8));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x3A, 0xFF, 0x5C, 0xC8));
            res["FieldBg"] = Frozen(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
            res["FieldLine"] = Frozen(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));
        }
        else''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("vivid combo colors added")
