import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

s = s.replace('''            res["InkFaint"] = Frozen(Color.FromRgb(0x41, 0x7A, 0x52));
        }
        else if (_vivid)''', '''            res["InkFaint"] = Frozen(Color.FromRgb(0x41, 0x7A, 0x52));
            res["FontNum"] = new FontFamily("Consolas");   // 终端风：等宽数字
        }
        else if (_vivid)''', 1)

s = s.replace('''            res["InkFaint"] = Frozen(Color.FromRgb(0x74, 0x77, 0x96));
        }''', '''            res["InkFaint"] = Frozen(Color.FromRgb(0x74, 0x77, 0x96));
            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
        }''', 1)

s = s.replace('''            res["InkFaint"] = Frozen(Color.FromRgb(0x6D, 0x75, 0x88));
        }''', '''            res["InkFaint"] = Frozen(Color.FromRgb(0x6D, 0x75, 0x88));
            res["FontNum"] = new FontFamily("Segoe UI Variable Display");
        }''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("font swap wired")
