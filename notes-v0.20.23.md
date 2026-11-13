MEME Launcher v0.20.23

- **修微软登录诊断窗口文字叠在一起**：ClientId 切换栏之前和设备码放在同一行（Grid.Row 都是 1），所以叠成一团乱码；现在单独占一行，顺序是 标题 → 设备码 → 步骤 → 状态 → 切换栏 → 按钮
- **首页大版本号只显示纯 MC 版本**（1.20.1），-forge-47.4.29 这类详细后缀留给副标题，长版本名不再撑破卡片
- 新增调试参数 --msa-test（打开诊断窗口）、--content-cf（以 CurseForge 源打开内容页）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
