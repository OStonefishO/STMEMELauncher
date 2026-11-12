MEME Launcher v0.20.0

- **新开「其他」页**（侧栏最后一个），参考 PCL2 把杂项集中过去：下载来源、更新/备份/目录/缓存、免责声明、高级选项
- **设置页精简**：只留 界面布局 + 游戏（目录、内存、启动方式、Java、版本隔离）
- **来源开关拆成两组**：
  - 版本 / 核心：自动（镜像优先、官方兜底）/ 官方（Mojang 直连）/ 镜像（BMCLAPI）
  - 模组内容：自动（MCIM 镜像优先）/ 官方（Modrinth、CurseForge 直连）/ 镜像（只用 MCIM）
  - 选中官方源时，下载器完全不走镜像；MCIM 只影响 mod 接口与文件，不影响版本/核心
- 高级选项保持在「其他」页：JVM 参数、游戏参数、启动前命令、内存调优（默认/G1GC/ZGC）、Java Launch Wrapper、LWJGL Unsafe Agent、界面音效

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
