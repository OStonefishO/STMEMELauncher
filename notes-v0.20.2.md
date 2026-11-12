MEME Launcher v0.20.2

- 修「删掉版本后版本页一片空白、点概览也切不回来」：加了兜底 —— 只要发现五个页签全被折叠，就自动把「概览」放回来；进版本页、删除版本后都会走一次
- 页签切换逻辑用无界面测试验证过：概览 ⇄ Mod 管理 ⇄ 导出 ⇄ 设置 ⇄ 修改 五个方向都正常
- （附）NeoForge 26.3 的四个 jar 已就位：srg / extra / slim + neoforge-26.3.0.102-client.jar

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
