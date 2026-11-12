MEME Launcher v0.16.2

- 修 RenderSystem 崩溃（Fabric/原版 1.18.2 进游戏就挂）：依赖库去重时把同名的不同版本当成重复，1.18.2 的 lwjgl 3.2.1（只有 macos 原生库）把 3.2.2（windows/linux 原生库）顶掉了，导致 lwjgl-3.2.2-natives-windows.jar 根本没下 → 现在去重带上版本号
- 「修复资源 / 补齐配置」统一走同一条修复链路：jar / 依赖库 / natives / assets / srg 全查全补（之前按版本号那套只补 assets，natives 根本没管）
- Forge 安装器改成**联网优先**（maven.minecraftforge.net 国内可直连），processors 才会生成 client-<ver>-srg.jar / -extra.jar / -slim.jar 和 forge-<ver>-client.jar；失败再退回离线模式
- NeoForge 补 -extra.jar / -slim.jar（从原版 jar 拆分：extra=资源、slim=类），解决「installation is corrupted」里要 extra.jar 的情况
- 版本 json 丢了会明确提示「需要重装」，不再默默什么都不做

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
