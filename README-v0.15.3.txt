MEME Launcher v0.15.3

- 缺 Java 时可以直接在启动器里装：检测到没有能跑这个版本的 Java，会弹「要自动下载安装吗」，装 Eclipse Temurin JRE 到启动器自己的目录（约 45 MB，不动你已有的 Java）
- 启动前自动补齐缺失的库文件（Forge / NeoForge 的 bootstraplauncher、securejarhandler 这类），崩溃报告也能认出这种情况
- 主页 / 设置：扫描 Java、自动匹配、手动选 java.exe；Java 版本不对会先问你要不要照常启动
- 版本可重命名、可写备注；安装可并发 + 取消；整合包点安装直接下载并导入
- 修 mods\mods 套娃、资源个别失败拖死安装、下载卡死、设置页多余的离线玩家名

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
