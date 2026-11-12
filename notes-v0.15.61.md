MEME Launcher v0.15.61

- 毛玻璃极光（以及其它首页）的「强制关闭游戏」按钮统一显示：游戏启动后六套界面都能看到，退出后自动收起
- 启动时右下角弹提示：先「正在启动游戏 ⏳」，起来后「游戏启动中 ▶ pid」，结束时分别提示「游戏已正常退出 ✓」/「游戏异常退出 ⚠ 退出码」/「已强制关闭游戏 ■」，崩溃会额外提示崩溃原因
- 提示条加了序号保护：新的提示不会被上一条的延时关掉
- 下载管理：完成 / 已取消的任务停留 4 秒后自动消失（失败保留，方便看报错）
- 启动前自动体检 assets：缺资源自动补齐再启动（NoSuchFileException 那种崩溃）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
