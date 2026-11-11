import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 每个主题设置下拉相关资源
def add_after(marker, snippet):
    global s
    s = s.replace(marker, marker + snippet, 1)

add_after('''            res["DockBg"] = Frozen(Color.FromArgb(0xF5, 0xFD, 0xF9, 0xF0));''', '''
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0xFD, 0xF9, 0xF0));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0xFF, 0xDD, 0xCF, 0xB8));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x22, 0xC4, 0x55, 0x3A));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x33, 0xC4, 0x55, 0x3A));
            res["FieldBg"] = Frozen(Color.FromArgb(0x10, 0x2B, 0x26, 0x22));
            res["FieldLine"] = Frozen(Color.FromArgb(0x30, 0x2B, 0x26, 0x22));''')

add_after('''            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x18, 0x18, 0x2C));''', '''
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x18, 0x18, 0x2C));
            res["ComboPopupLine"] = Frozen(Color.FromRgb(0x58, 0xD8, 0x54));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x30, 0xF8, 0x38, 0x58));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x50, 0x58, 0xD8, 0x54));
            res["FieldBg"] = Frozen(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
            res["FieldLine"] = Frozen(Color.FromArgb(0x66, 0x58, 0xD8, 0x54));''')

add_after('''            res["DockBg"] = Frozen(Color.FromArgb(0xC8, 0x1A, 0x2A, 0x4A));''', '''
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x1A, 0x2A, 0x4A));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0x60, 0xB6, 0xC8, 0xFF));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x2A, 0xB6, 0xC8, 0xFF));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x40, 0xB6, 0xC8, 0xFF));''')

add_after('''            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x08, 0x0F, 0x0B));''', '''
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x08, 0x0F, 0x0B));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0x80, 0x35, 0xF0, 0x7A));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x2E, 0x35, 0xF0, 0x7A));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x44, 0x35, 0xF0, 0x7A));''')

add_after('''            res["DockBg"] = Frozen(Color.FromArgb(0xF0, 0x12, 0x0A, 0x20));
        }
        else if (_vivid)''', '''
            res["ComboPopupBg"] = Frozen(Color.FromRgb(0x14, 0x0A, 0x24));
            res["ComboPopupLine"] = Frozen(Color.FromArgb(0x3C, 0xFF, 0x5C, 0xC8));
            res["ComboItemHover"] = Frozen(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
            res["ComboItemActive"] = Frozen(Color.FromArgb(0x38, 0xFF, 0x5C, 0xC8));''')
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("per-theme combo resources")
