MEME Launcher v0.20.12

- 修「在『其他』页时导航按钮有个白块、点起来发滞」：导航按钮（顶部 dock 和左侧 rail）不再抢键盘焦点，也不画焦点虚线框 —— 之前点击后残留的白色焦点框看起来像坏掉的按钮
- 修「修复资源后仍说缺 client.jar」：补完文件会**重新解析版本**，刚下好的 jar 立即生效（之前用的是补之前的快照，所以永远判缺）
- 修首页大版本号超格：1.20.1-forge-47.4.29 这类长名字按长度自动缩小字号

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
