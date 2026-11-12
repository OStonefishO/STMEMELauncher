MEME Launcher v0.20.1

- **NeoForge 真正的修法**：安装器支持 --mirror，直接指向 BMCLAPI 的 maven 镜像（https://bmclapi2.bangbang93.com/maven/），processors 就能跑起来，一次生成 minecraft-client-patched-<ver>.jar
- 生成后再复制成 FML 12 要找的名字 
eoforge-<ver>-client.jar（libraries/net/neoforged/neoforge/<ver>/）
- 现在 NeoForge 26.3 需要的四件套齐了：client-26.3-1-srg.jar / -extra.jar / -slim.jar / 
eoforge-26.3.0.102-client.jar
- 你的 26.3 我已经手动跑过安装器并把 client jar 复制到位，直接点开始游戏试

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
