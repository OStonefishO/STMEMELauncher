MEME Launcher v0.20.6

- **修「点了其他就点不动任何东西 / 其他页超格」**：其他页之前插在了页面容器外面（盖住了导航），现在正确嵌进页面容器，位置和设置页一致
- **修版本号显示错误（1.21.11 显示成 21.11）**：版本摘要优先用 json 的 inheritsFrom 判断 MC 版本，猜版本时也优先以 1. 开头的三段式
- **修 NeoForge 21.11.48 也报 corrupted**：修复流程现在会为任意 NeoForge 版本补 
eoforge-<ver>-client.jar（从安装器产物复制），你机器上 21.11.48 的四件套已就位
- 安装器空选项保护

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
