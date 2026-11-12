MEME Launcher v0.20.9

- **启动时自动补齐加载器核心**：Fabric / NeoForge / Forge 都在启动前检查一遍
  - Fabric：补版本 json、依赖库、natives、assets
  - Forge / NeoForge：额外检查 client-<id>-srg/extra/slim.jar 和打补丁的 client jar；缺了**自动跑一次安装器**（走 BMCLAPI 镜像），首次启动会慢几分钟
- 补不上会明确提示，不会再让你进到一个必崩的窗口里

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
