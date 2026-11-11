import io
p = r"D:\综合创作\MCLauncher\README.md"
s = io.open(p, encoding="utf-8", newline="").read()

start = s.index("## 界面：三种设计语言，一键切换")
end = s.index("## 亮点")

new = """## 界面：六套设计语言，一键切换

同一个启动器，六种完全不同的长相，标题栏右侧 **◐** 按钮循环切换，设置里下拉框直接选，选择会记住。

| 布局 | 长相 | 音效 |
|---|---|---|
| **新版（动感）** | 顶部胶囊导航 + 卡片仪表盘；超大渐变版本号 + 极光漂移；卡片悬停发光、按钮回弹；主题色相滑块 -160°~160° | 合成点击音（40ms 正弦） |
| **终端** | 近黑墨绿 + 荧光绿；Consolas 等宽版本号；1px 荧光细边、无渐变堆叠 | 方波机械 beep（32ms） |
| **纸质便签** | 米白纸面 + 深棕墨 + 陶土橙；Georgia 衬线字；浅色系、柔和阴影 | 低频木头「笃」声（50ms） |
| **像素 8bit** | 黑底 + NES 红/橙/荧光绿硬边色块；等宽字；边框亮绿 | 双音方波琶音（90ms） |
| **毛玻璃极光** | 蓝紫青柔和渐变；低对比大圆角；薄荷→蓝渐变按钮；极光缓慢呼吸 | 柔和正弦 + 短延迟（160ms） |
| **经典** | 原来的左侧图标栏 + 冷蓝配色，安静、信息密度高 | 无 |

### 预览

| 新版（动感） | 终端（荧光绿） | 经典（左栏） |
|---|---|---|
| ![](docs/screens/ui-vivid-home.png) | ![](docs/screens/ui-terminal-home.png) | ![](docs/screens/ui-classic-home.png) |

| 纸质便签（浅色） | 像素 8bit（NES） | 毛玻璃极光 |
|---|---|---|
| ![](docs/screens/ui-paper-home.png) | ![](docs/screens/ui-pixel-home.png) | ![](docs/screens/ui-aurora-home.png) |

另外两页：

| 设置（含布局与色相） | 终端设置页 | 联机页 |
|---|---|---|
| ![](docs/screens/ui-settings.png) | ![](docs/screens/ui-terminal-settings.png) | ![](docs/screens/ui-online.png) |

"""
s = s[:start] + new + s[end:]
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("README six layouts")
