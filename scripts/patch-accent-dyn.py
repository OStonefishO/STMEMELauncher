import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()
before = s.count("{StaticResource AccentGrad}")
s = s.replace("{StaticResource AccentGrad}", "{DynamicResource AccentGrad}")
s = s.replace('Foreground="#CCFF3D8B"', 'Foreground="{DynamicResource AccentGrad}"')
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("accent static -> dynamic:", before)
import xml.etree.ElementTree as ET
ET.parse(p)
print("XML OK")
