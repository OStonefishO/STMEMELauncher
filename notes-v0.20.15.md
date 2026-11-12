MEME Launcher v0.20.15

- **修这次真正的崩溃原因**：日志里是 module.ResolutionException: Modules _1._20._1 and minecraft export package net.minecraft.server —— 说明 classpath 里同时出现了**两份 Minecraft**（原版 jar + 打补丁的 client jar）
- 起因是我上一版给 Forge/NeoForge 也补了「原版 jar 兜底」，结果和新补丁客户端撞了；现在**只对原版/Fabric 做原版 jar 兜底**，Forge/NeoForge 只用打补丁的 client jar
- 日志里那几行 missing mods.toml 是 Forge 自己对自身语言模块的提示，无害

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
