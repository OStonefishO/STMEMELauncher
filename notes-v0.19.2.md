MEME Launcher v0.19.2

- 版本页的「导出整合包」面板重做，参考 PCL2 的结构：
  - 整合包名称 + 整合包版本
  - 导出内容勾选：游戏设置(options.txt) / Mod / Mod 设置(config) / 单人存档 / 资源包 / 光影
  - **高级选项**（折叠）：打包资源文件(assets) / 只记录 Modrinth 下载地址 / 连原版 client.jar 一起打包，外加「读取配置 / 保存配置 / 制作指南」
  - 导出时写一份 meme-modpack.json（名称、版本、游戏版本、加载器、时间）
- 概览页恢复 **这个版本的 Mod 预览**（列出前 5 个 jar，隔离关闭时自动读游戏根目录的 mods）
- 概览页新增独立的 **导出整合包** 按钮，点了直接切到导出页签并带好名字

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
