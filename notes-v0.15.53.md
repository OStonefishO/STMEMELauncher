MEME Launcher v0.15.53

- 版本页新增「删除版本」：要过三道关（先讲清会删掉什么 → 手动输入版本名 → 最后确认），删掉的整个版本文件夹进回收站，能还原
- 修掉浅色主题里的深色块：更新横幅、右下角提示、内容详情浮层、下载管理抽屉、模组列表底、控制台/皮肤预览框、输入框边框——全部改走主题色，六套布局统一
- 布局不再溢出：版本页的「备注」「内存 + 一排按钮」改成可换行，内容页的筛选行和搜索/翻页行改成自动换行，控件不再顶出卡片
- 更新检查改顺序：先问 Gitee，Gitee 有比当前版本新的就直接下载更新；Gitee 没新版或连不上才去问 GitHub（不再每次先等 GitHub 超时）

---

本项目使用 **SignPath Foundation** 提供的免费代码签名服务。
This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).

Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md
