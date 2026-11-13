MEME Launcher v0.20.26

- **改名后搜不到的问题真正修好**：打开「版本 ▾」前先按磁盘重扫一次（改名/删除/新装的版本立即生效），实测输 114514 能匹配到 1/25
- **Java 版本按继承链取最大值**：整包版本 json 里残留的 javaVersion: 8 不再覆盖真实需求（26.3 现在统一显示 Java 25）
- 新增调试参数 --picker-q <文字>、--dump-versions（验证搜索与解析用）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
