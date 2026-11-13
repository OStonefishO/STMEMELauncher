MEME Launcher v0.20.21

- **微软登录诊断窗口加了 ClientId 切换**：第 ④ 步 403（AppID 没进 Mojang 白名单）时，可以当场
  - 「用官方启动器 ID 重试」→ 换成 `00000000402b5328` 立刻重跑四步诊断
  - 「粘贴自定义 ClientId」→ 从剪贴板读取任意已登记的 AppID（例如其它开源启动器公开的那个）
  - 「改用外置登录」→ 提示改用 LittleSkin 等 Yggdrasil 登录（不经过 Mojang 白名单）
- 切换后会自动写进设置并保存，下次诊断继续用这个 ID

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
