MEME Launcher v0.15.2

- 主页 / 设置：扫描 Java、自动匹配、手动选 java.exe；Java 版本不对会先问你要不要照常启动
- 启动前自动补齐缺失的库文件（Forge / NeoForge 的 bootstraplauncher、securejarhandler 这类）
- 崩溃报告能认出「Forge 核心库没准备好」，不再甩锅给模组
- 版本可重命名、可写备注
- 安装可以同时进行多个、带取消按钮；整合包点安装就直接下载并导入
- 修 mods\mods 套娃、资源文件个别失败拖死安装、下载卡死、设置页多余的离线玩家名

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
