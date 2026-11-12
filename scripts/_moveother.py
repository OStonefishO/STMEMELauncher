import io

p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
raw = io.open(p, encoding="utf-8", newline="").read()
nl = "\r\n" if "\r\n" in raw else "\n"
lines = raw.split(nl)

# 1) 取出 OtherPage 整块
start = next(i for i, l in enumerate(lines) if 'x:Name="OtherPage"' in l)
indent = lines[start][:len(lines[start]) - len(lines[start].lstrip())]
end = None
for i in range(start + 1, len(lines)):
    if lines[i].strip() == "</Grid>" and lines[i].startswith(indent) and not lines[i].startswith(indent + " "):
        end = i
        break
if end is None:
    print("OtherPage end missing")
    raise SystemExit

block = lines[start:end + 1]
del lines[start:end + 1]

# 2) 找到页面容器（`<Grid Grid.Column="1" Margin="10,0,22,0">`）的结束 </Grid>
container = next(i for i, l in enumerate(lines) if 'Grid.Column="1" Margin="10,0,22,0"' in l)
cindent = lines[container][:len(lines[container]) - len(lines[container].lstrip())]
close = None
for i in range(container + 1, len(lines)):
    if lines[i].strip() == "</Grid>" and lines[i].startswith(cindent) and not lines[i].startswith(cindent + " "):
        close = i
        break
if close is None:
    print("pages container close missing")
    raise SystemExit

# 3) 插到容器闭合标签之前
lines[close:close] = block
io.open(p, "w", encoding="utf-8", newline="").write(nl.join(lines))
print("moved OtherPage into pages grid at", close)
