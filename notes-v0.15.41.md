MEME Launcher v0.15.41

- 新增「音效音量」滑块（0 = 静音，实时生效并记住），和主题色相滑块放在一起
- 修浅色主题（纸质便签）下拉框还是黑底的问题：下拉弹层、输入框底色、悬停/选中高亮都跟着主题走
- 恢复「强制关闭游戏」按钮：新版仪表盘首页会跟着启动状态出现/隐藏（之前只在经典布局里）
- 删掉内容页底部那条「安装到当前版本」栏（进详情页就能选版本安装，那条是多余的）
- 修启动崩溃：自绘按钮模板里 Setter 的目标名为空 + 音量滑块在初始化阶段回调拿到空设置
- 配置文件丢失/损坏时自动备份成 config.broken-时间戳.json 并重建默认配置
- 修创作页「重新扫描」：改为保留当前选中的版本，只重扫它的内容（之前会被换成同名的另一个版本）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
