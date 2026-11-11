import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace("{StaticResource FontNum}", "{DynamicResource FontNum}")
s = s.replace("{StaticResource FontHead}", "{DynamicResource FontHead}")
io.open(p, "w", encoding="utf-8", newline="").write(s)
import xml.etree.ElementTree as ET
ET.parse(p)
print("fonts dynamic:", s.count("{DynamicResource FontNum}"))
