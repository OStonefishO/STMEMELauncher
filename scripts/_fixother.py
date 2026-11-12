import io, re

p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
raw = io.open(p, encoding="utf-8", newline="").read()
nl = "\r\n" if "\r\n" in raw else "\n"
lines = raw.split(nl)

# 1) 取走 OtherPage
start = next(i for i, l in enumerate(lines) if 'x:Name="OtherPage"' in l)
indent = lines[start][:len(lines[start]) - len(lines[start].lstrip())]
end = None
for i in range(start + 1, len(lines)):
    if lines[i].strip() == "</Grid>" and lines[i].startswith(indent) and not lines[i].startswith(indent + " "):
        end = i
        break
block = lines[start:end + 1]
del lines[start:end + 1]
print("removed OtherPage", start, end)

# 2) 用深度解析找页面容器的 </Grid>
container = next(i for i, l in enumerate(lines) if 'Grid.Column="1" Margin="10,0,22,0"' in l)
depth = 0
close = None
for i in range(container, len(lines)):
    line = lines[i]
    opens = len(re.findall(r"<Grid\b(?![^>]*/>)", line))
    closes = len(re.findall(r"</Grid>", line))
    depth += opens - closes
    if i > container and depth == 0:
        close = i
        break
if close is None:
    print("container close not found")
    raise SystemExit
print("container", container, "closes at", close, repr(lines[close]))

# 3) 插进去，缩进比容器深 4 格
cindent = lines[container][:len(lines[container]) - len(lines[container].lstrip())]
inner = cindent + "    "
fixed = []
for l in block:
    fixed.append((inner + l[len(indent):]) if l.startswith(indent) else l)

lines[close:close] = fixed
io.open(p, "w", encoding="utf-8", newline="").write(nl.join(lines))
print("inserted at", close)
