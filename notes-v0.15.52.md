MEME Launcher v0.15.52

- 整合包：点「安装」不再弹目录选择，直接装成独立版本；原版 / Fabric / Forge / NeoForge 没装会自动补齐，装完自动选中新版本，点「开始游戏」就能玩（不用再走一遍导入）
- 自建镜像连不上会自动跳过，不再白等 90 秒才回退：填了地址但端口没开时，直接用 BMCLAPI
- 保存设置或启动时探测一次自建镜像，挂了会直接提示「已自动改用 BMCLAPI」
- 下载源：清掉了指向本机 8123 的旧配置，现在默认走 BMCLAPI（实测比官方源快约 3 倍）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
