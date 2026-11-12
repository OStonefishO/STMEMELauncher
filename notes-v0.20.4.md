MEME Launcher v0.20.4

- **修「安装失败：基础版本 26.2 还没装好」**：加载器版本列表是按某个 MC 版本拉的，切换 MC 后可能还是旧对象 → 现在安装时强制用当前选中的 MC 版本，不会再装成上一个版本
- **装加载器前先修原版**：原版 json 已存在时也会跑一次修复（jar / 依赖库 / natives / assets），保证基础版本完整再装加载器
- 实测：26.3 + Fabric 0.20.1 一次装成功，abric-loader-0.20.1-26.3 的 jar ✓、缺库 0、lwjgl 3.4.3 natives 齐全

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
