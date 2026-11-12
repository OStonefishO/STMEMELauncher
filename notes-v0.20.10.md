MEME Launcher v0.20.10

- 修「开始安装按钮变灰点不了」：按钮之前跟着 _busy 走，游戏启动/退出后 _busy 没及时复位就会一直灰着 → 现在安装按钮不再受启动状态影响，启动结束也会强制复位
- 启动前自动补齐加载器核心（0.20.9 起的）：Fabric 补库/natives/assets；NeoForge/Forge 缺 srg/extra/slim 或打补丁的 client jar 时**自动跑安装器**（BMCLAPI 镜像），首次启动会慢几分钟

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
