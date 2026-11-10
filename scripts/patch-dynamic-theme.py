import io, re
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
s = io.open(p, encoding="utf-8", newline="").read()

keys = ["AppBg","AccentGrad","TitleGrad","Glass","GlassLine","Ink","InkDim","InkFaint","TextMain","TextDim"]
for k in keys:
    s = s.replace("{StaticResource %s}" % k, "{DynamicResource %s}" % k)

print("dynamic resources:", s.count("{DynamicResource"))
io.open(p, "w", encoding="utf-8", newline="").write(s)

import xml.etree.ElementTree as ET
ET.parse(p)
print("XML OK")
