MEME Launcher v0.15.5

- 修「检查更新说是最新」：以前先问 Gitee 镜像，镜像没同步旧版本时会误判成已是最新；现在两个源都问，取更新的那个
- 主页/设置：扫描 Java、自动匹配、手动选 java.exe；缺 Java 时可一键下载安装（Temurin JRE）
- 启动前自动补齐缺失的库（Forge / NeoForge 的 bootstraplauncher 这类）
- 版本可重命名、写备注；安装可并发 + 取消；整合包点安装直接下载并导入
- 修 mods\mods 套娃、资源个别失败拖死安装、下载卡死、设置页多余字段

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
