MEME Launcher v0.20.14

- **启动前自动补齐又加一类检查**：Forge/NeoForge 的 -p（module path）里引用的库如果缺（崩在 BootstrapLauncher / securejarhandler 那种），现在会自动重跑安装器补齐，然后再启动
- 现在的启动前自检顺序：补配置 → 修资源（jar/库/natives/assets）→ 检查 srg/extra/slim 与 client jar → 检查 -p 模块路径 → 都齐了才启动
- 任何一步补不上都会明确告诉你缺什么，不会让你进到一个必崩的窗口

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
