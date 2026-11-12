MEME Launcher v0.15.64

- 修 Fabric / Quilt 启动崩溃「Minecraft game provider couldn't locate the game」：加载器版本自己没有 client.jar，启动时自动把被继承的原版 jar 加进 classpath
- 修 NeoForge / Forge「Your NeoForge installation is corrupted」：补 client-<MCP_VERSION>-srg.jar（从原版 client.jar 复制），安装时和「修复资源」时都会补，并把 install_profile 留档
- 加载器安装后做完整性校验：版本 json 在不在、依赖库齐不齐；缺了直接报「安装不完整」并指出缺哪几个，不再装完就报完成
- 启动时缺依赖库改成自动补齐（以前只提示建议重装）
- 「修复资源」会先清掉下载到一半的 .tmp 文件

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
