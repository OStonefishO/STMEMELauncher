MEME Launcher v0.20.27

- **修「系统 JAVA_TOOL_OPTIONS 把第三方 javaagent 塞进游戏」**：启动子进程前自动清空 JAVA_TOOL_OPTIONS / _JAVA_OPTIONS / JDK_JAVA_OPTIONS（AstralAgent 这类 agent 自带 asm 包，会和 ModLauncher 的模块图打架）
- **崩溃结论更准**：遇到 Modules X and Y export package ... / Ignoring duplicate module / -javaagent: 时，结论改成「系统里给 Java 塞了额外的 agent」，并给出 setx JAVA_TOOL_OPTIONS "

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
