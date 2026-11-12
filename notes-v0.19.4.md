MEME Launcher v0.19.4

- 概览页新增 **重装加载器** 按钮（仅 Forge / NeoForge 可用）：清掉 libraries 里的加载器核心 + client srg/extra/slim，然后重跑安装器，最后一步兜底
- 不会动 mods / 存档 / 配置，只重装核心库
- 说明：NeoForge 报「installation is corrupted」时，先点「修复资源」（补 extra/slim），还不行再点「重装加载器」

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
