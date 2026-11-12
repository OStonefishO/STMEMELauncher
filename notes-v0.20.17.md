MEME Launcher v0.20.17

- **修 module.ResolutionException 的真正源头**：Forge/NeoForge 的 
et.minecraft:client:*（srg / extra / slim）本来只该出现在 -p 模块路径里，之前又被我们的 classpath 收了一遍，于是出现两个 minecraft 模块（_1._20._1 + minecraft）
- 现在构建 classpath 时**排除所有 
et/minecraft/client/ 下的库**，原版/Fabric 不受影响（它们本来就没有这些条目）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
