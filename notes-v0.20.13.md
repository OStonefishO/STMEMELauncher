MEME Launcher v0.20.13

- 修「FPS Modpack 这类整包版本永远说缺 client.jar」：版本继承链是 FPS Modpack → fabric-loader-0.20.1-26.3 → 26.3，中间是加载器目录（没有 jar），之前的查找只看上一层 → 现在**沿继承链一路找 jar**，并且启动前解析不到时直接按原版目录兜底
- 启动前的自动补齐也会把这种情况算进去：补完重新解析、jar 立刻生效

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
