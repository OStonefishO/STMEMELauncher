MEME Launcher v0.20.24

- **版本概览显示类型和版本**：标题下面多一行 Fabric 0.20.1 · MC 26.3 / 原版 26.3 / Forge 47.4.29 · MC 1.20.1 / NeoForge 21.11.48 · MC 1.21.11，不再只有一个名字
- **改名后版本选择器搜不到**：打开「版本 ▾」弹层前先按磁盘重扫一次，避免用改名前的旧快照搜索
- **修启动崩溃**：VersionRepository.FindJar 在版本目录里没有 json 时会空引用，导致启动直接弹错误框（上一版引入的 bug）
- 首页大版本号只显示纯 MC 版本（1.20.1），详细 id 放在副标题
- 微软登录诊断窗口的 ClientId 切换栏不再和设备码重叠（上一版布局 bug）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
