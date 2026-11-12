MEME Launcher v0.15.66

- 根除 Fabric / NeoForge 启动失败：以后不会再出现「装完 1 秒就说好了，一启动就崩」
- 启动前不再只查库：每次都完整校验 jar / 依赖库 / natives / assets / srg，缺什么当场补，补不上就明确告诉你缺哪个并中止启动
- 原版安装完立刻校验：client.jar 半截（.jar.tmp）或依赖库缺失 → 直接报「安装不完整」，不再报完成
- 加载器安装完也会检查基础版本：原版没装好会提示先装原版 / 点修复资源
- 「修复资源」新增：补 client.jar（按 downloads.client 重下）、补 natives（windows 分类器）
- 根因：1.18.2 基础版本只下到 1.18.2.jar.tmp（1.8MB）+ 缺 28 个库，而 Fabric 安装只下自己 8 个小库，所以 1 秒装好了

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
