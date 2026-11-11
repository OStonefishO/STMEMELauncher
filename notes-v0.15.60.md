MEME Launcher v0.15.60

- 下载管理：完成 / 已取消的任务在列表里停留 4 秒后自动消失，不用再手动点「清空已结束」；失败的任务保留，方便看报错
- 启动前自动体检 assets：进游戏前查资源是否齐全（只做文件存在性检查，很快），缺了自动补齐再启动
- 这解决的就是 NoSuchFileException 那种崩溃：客户端加载贴图时找不到 assets 对象直接崩，现在启动前就补上
- assets 检查按继承链解析（Forge/NeoForge 的 assetIndex 在父版本里）
- 版本页「修复资源」用同一套逻辑，可随时手动跑

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
