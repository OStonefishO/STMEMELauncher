MEME Launcher v0.15.67

- 每个界面（纸质/像素/终端/极光/动感/经典）首页都有一行「Java：自动」按钮，点开能选：自动匹配、列表里挑某个已扫到的 Java、或者直接选 java.exe
- 版本页加「Java 跟随全局 / 改 Java」：只给这个版本指定 java.exe，别的版本不受影响；「自动」= 按版本要求挑
- 版本号修正：加载器版本以前会显示成「Java 8」（自己 json 里没有 javaVersion），现在顺继承链找，26.3 的 fabric 正确显示 Java 25
- 「补齐配置」现在是真的能救回来：配置 + client.jar + 依赖库 + natives + assets + srg 一次全查全补
- 启动前完整校验（jar/库/natives/assets/srg）、安装后校验（原版 + 加载器）继续保留

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
