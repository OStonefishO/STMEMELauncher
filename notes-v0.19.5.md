MEME Launcher v0.19.5

- **修 Fabric 一直起不来的根因**：1.18.2 的版本 json 里同一个 artifact 有两份（lwjgl 3.2.1 带 macos natives、3.2.2 带 windows natives），之前两份 jar 都塞进 classpath → LWJGL 版本混用 → NoClassDefFoundError: Could not initialize class com.mojang.blaze3d.systems.RenderSystem
- 现在合并继承链时同一个 group:artifact:classifier 只留 **版本最高的那份**，并把两份的 natives / classifiers 字典合并 —— classpath 只剩 lwjgl 3.2.2，natives（windows）也齐了
- 顺带对 Forge / NeoForge 同样有效（它们也依赖 lwjgl）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
