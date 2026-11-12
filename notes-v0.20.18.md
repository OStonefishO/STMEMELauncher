MEME Launcher v0.20.18

- **双 minecraft 模块冲突彻底修掉**：Zombie Invade 100 Days 继承到 1.20.1-forge-47.4.29，再往下是原版 1.20.1；之前沿继承链找到了 1.20.1.jar 并把它放进 classpath，而 Forge 自己已经把打补丁的客户端放进了 -p → 两个 
et.minecraft.server 导出方
- 现在 classpath **不再添加原版 jar 到 Forge/NeoForge 版本**（它们只用 -p 里的补丁客户端），原版/Fabric 照旧
- 加上上一版的 
et/minecraft/client 库排除，Forge 的模块图里只剩一套 Minecraft

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
