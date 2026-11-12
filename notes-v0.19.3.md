MEME Launcher v0.19.3

- **修 NeoForge 12「installation is corrupted」**：它要 client-<MCP_VERSION>-extra.jar，之前只在 srg 缺失时才生成，srg 在就跳过 → 现在安装和修复都会补齐 srg / extra / slim 三件套
- 实测：neoforge-26.3.0.102 的 libraries/net/minecraft/client/26.3-1 下已生成 extra(16.8MB) / slim(25.7MB) / srg(41.5MB)
- 顺带说明：NeoForge 的 maven 在国内被墙（实测 20s 超时），所以只能靠离线安装器 + 我们自己拆 jar，这条路现在补齐了

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
