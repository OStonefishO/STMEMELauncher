import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml"
lines = io.open(p, encoding="utf-8", newline="").read().split("\n")

# 定位底部安装条的 <Grid Grid.Row="2" ...> 到它的结束 </Grid>
start = next(i for i,l in enumerate(lines) if 'Grid.Row="2" Margin="26,12,0,8"' in l)
# 记录缩进层级，用简单计数找到匹配的 </Grid>
depth = 0
end = None
for i in range(start, len(lines)):
    depth += lines[i].count('<Grid') - lines[i].count('</Grid>')
    if depth == 0 and i > start:
        end = i
        break
print('block', start, end)
for i in range(start, end+1):
    print(repr(lines[i]))
