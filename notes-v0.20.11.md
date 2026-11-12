MEME Launcher v0.20.11

- 下载管理里「暂停/继续」对安装类任务本来就不支持，之前按钮看着能点、点了没反应 → 现在不支持的任务这两个按钮直接置灰，并在说明里写清楚「安装类任务请用取消」
- 普通下载的暂停继续仍然可用（暂停保留进度状态，继续重新排队）
- 另外修了「开始安装按钮灰着点不了」：不再跟着启动状态走，启动结束强制复位

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
