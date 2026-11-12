MEME Launcher v0.20.3

- **修 mod 下到一半失败（The process cannot access the file）**：下载先写唯一临时文件，再原子替换目标；同一个文件被多个任务/多个实例同时下时用单飞锁串行，等第一个写完后面直接判为已完成
- **修 mod 装错版本**：内容安装改用「首页当前选中的版本」来决定目录（之前误用了首页列表控件的选中项，取不到就落到根目录）
- 版本页页签（概览/Mod/导出/设置/修改）改为容错实现：切页失败只影响高亮，不再把整页留空

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
