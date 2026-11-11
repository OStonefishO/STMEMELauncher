MEME Launcher v0.15.46

- 修「启动崩溃：assets 里某个对象文件不存在」：安装时按 index 里的 size 校验（0 字节残缺文件不再被当成已下好），启动前还会做一次资源体检并自动补下缺失的 assets / 库文件
- 同一套体检也覆盖 Forge / NeoForge / Fabric 的库文件，缺核心库会自己补回来
- 安装页新增「含预览版（快照 / 愚人节）」勾选：勾上后列表包含 snapshot、old_beta、old_alpha，能直接装官方最新预览版和愚人节版本
- 版本页加搜索框：直接打字过滤（版本号 / 加载器），回车选第一个，Esc 清空
- 滚动条加粗到 14px（圆角滑块），模组列表和设置页都更好拖
- 娱乐页 B 站自适应缩放会在换布局/进页面时重算，不再被压到 86%

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
