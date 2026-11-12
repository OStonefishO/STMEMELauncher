MEME Launcher v0.17.0

- 设置页新增 **下载来源**：自动（镜像优先、失败回落官方）/ 官方源 / 镜像源（BMCLAPI + MCIM）—— 觉得镜像抽风就切官方源
- 接入 **MCIM（mcmod-info-mirror）**：Modrinth / CurseForge 的接口和文件走 mod.mcimirror.top，国内搜模组快很多（官方源模式则直连）
- 设置页新增 **其他**：检查更新、导出/导入设置、打开配置目录、打开日志目录、清空下载缓存（含半截 .tmp）、打开游戏目录
- 设置页新增 **高级选项**：JVM 参数、游戏参数、启动前命令、内存调优（默认 / G1GC / ZGC）、启用 Java Launch Wrapper（自动下载 JavaLaunchWrapper.jar，修 Windows 下 Java 参数编码问题）
- 上一批修复继续保留：依赖库去重带版本号（修 RenderSystem）、修复链路统一（jar/库/natives/assets/srg）、Forge 联网装、NeoForge 拆 extra/slim

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
