MEME Launcher v0.15.63

- 修 Fabric / Quilt 启动崩溃：「Minecraft game provider couldn't locate the game」——加载器版本自己没有 client.jar，classpath 里少了被继承的原版 jar，现在自动补上
- 修 NeoForge / Forge「Your NeoForge installation is corrupted」：补 client-<MCP_VERSION>-srg.jar（从原版 client.jar 复制），并把安装器的 install_profile 留档，修复资源 / 启动时能自动重建
- 启动时缺依赖库改成自动补齐（以前只弹一句建议重装）
- 修复资源会先清掉下载到一半的 .tmp，避免卡在半截文件上
- 相关：安装 NeoForge 时如果 universal jar 只下到一半，现在修复资源能识别大小不对并重下

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
