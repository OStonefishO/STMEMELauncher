MEME Launcher v0.20.22

- **CurseForge 现在能用了**：API Key 已写入配置；官方 api.curseforge.com 在你的网络出口被 CloudFront 拦（403、空 body），启动器会自动**改走 MCIM 的 CurseForge 反向代理**并把 key 一起带上，实测能正常搜到 GeckoLib / JEI / Cloth Config 等
- 修 **内容站版本号带加载器尾巴导致查不到东西**：1.20.1-forge-47.4.29 现在只取 1.20.1 再发给 Modrinth/CurseForge
- CurseForge 实在不可用时会**自动切回 Modrinth**并提示原因，不再对着报错干瞪眼
- 新增调试参数 --content-cf（直接以 CurseForge 源打开内容页，验证用）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
