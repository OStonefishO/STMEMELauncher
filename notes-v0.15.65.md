MEME Launcher v0.15.65

- 修「Fabric 装完 1 秒就说好了，但一启动就 Minecraft game provider couldn't locate the game」：根因是**被继承的原版版本没装好**（1.18.2 只有半截 .jar.tmp，28 个库也缺），而加载器安装只下了自己那 8 个小库，之前根本不检查基础版本
- classpath 现在会自动带上被继承的原版 client.jar（之前 merged.InheritsFrom 被合并时清空，这段兜底从没生效过）
- 「修复资源」现在会补 client.jar 本身（按版本 json 的 downloads.client 重下，先清掉半截 .tmp）
- 启动时缺库/缺 jar 自动补齐，装完再做完整性校验
- 实测：修完 1.18.2 后补了 3066 个文件（jar 20.2MB + 28 个库 + 1.18 资源索引），Fabric 启动计划里 jar ✓ / 缺库 0 / assets ✓

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
