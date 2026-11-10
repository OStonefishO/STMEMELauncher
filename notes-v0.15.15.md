MEME Launcher v0.15.15

- 新增 MC 百科联动：搜到对应页面就用它的中文名 + 中文简介替换英文，详情页右上按钮变成「MC 百科：钠」并直达 https://www.mcmod.cn/class/2785.html 这种具体页面
- 内容列表会优先用 MC 百科的中文（比机翻准），查不到才退回翻译接口
- 实测命中：Sodium → 钠、Create → 机械动力、Dungeons Delight → 地牢乐事
- 说明：MC 百科是网页抓取，做了本地缓存 + 退避重试，第一次查稍慢，之后秒开

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
