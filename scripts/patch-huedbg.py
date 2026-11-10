import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()
if "_huedebug.txt" not in s:
    s = s.replace("""        // 同步下拉框选中项
        if (UiLayoutCombo != null)""", """        try
        {
            var dbg = (res["AccentGrad"] as LinearGradientBrush)?.GradientStops.FirstOrDefault()?.Color;
            System.IO.File.WriteAllText(@"D:\\综合创作\\_huedebug.txt",
                $"layout={layout} hue={_hueShift} accent0={dbg}");
        }
        catch { }

        // 同步下拉框选中项
        if (UiLayoutCombo != null)""", 1)
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("debug added")
