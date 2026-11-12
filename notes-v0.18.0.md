MEME Launcher v0.18.0

- **界面音效**：按钮点击、启动成功、警告提示音（NAudio 合成，不带音频文件；音量跟设置里的「音效音量」走，高级选项里可以整体关掉）
- **WebP 图标**：Modrinth / CurseForge 的 .webp 图标现在能显示了（WPF 自带解码器不认，改用系统 WIC 兜底，Win10 1903+ / Win11 自带解码器）
- **LWJGL Unsafe Agent**：高级选项里可开关，开启后自动下载 lwjgl-unsafe-agent.jar 并挂 -javaagent（部分显卡/驱动下修 LWJGL 崩溃）
- 高级选项凑齐：JVM 参数、游戏参数、启动前命令、内存调优（默认 / G1GC / ZGC）、Java Launch Wrapper、LWJGL Unsafe Agent、界面音效
- 内容接口磁盘缓存（20 分钟）+ 免责声明入口（设置 → 其他 → 免责声明 / 许可）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
