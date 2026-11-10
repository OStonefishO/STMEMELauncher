# 安全策略 / Security Policy

MEME Launcher 会保存账号凭据、下载并执行 Java 程序与模组，所以安全问题我们当真处理。
本文件说明：怎么报、我们多久回、哪些算安全问题。

English summary is at the bottom.

---

## 支持范围

只有**最新发布版**接受安全修复。旧版本请先升级：
<https://github.com/OStonefishO/STMEMELauncher/releases/latest>

| 版本 | 是否维护 |
|---|---|
| 最新 Release | ✅ |
| 更早版本 | ❌（请升级后再验证是否仍存在） |

## 怎么报告漏洞

**请不要开公开 Issue。** 用下面任意一种私密渠道：

1. **GitHub 私密漏洞报告（推荐）**
   <https://github.com/OStonefishO/STMEMELauncher/security/advisories/new>
2. 如果上面的入口不可用，发邮件到仓库 README 里的联系方式，标题加 `[SECURITY]`。

报告里请尽量包含：

- 受影响的版本（启动器「设置 → 关于」里的版本号）
- 复现步骤或最小复现工程
- 影响判断（能读文件？能执行代码？能拿到令牌？）
- 你的联系方式（想署名的话）

## 我们怎么处理

| 阶段 | 目标时间 |
|---|---|
| 确认收到 | 3 天内 |
| 初步判断（是否成立、严重程度） | 7 天内 |
| 修复并发布 | 30 天内（严重问题会更快） |
| 公开致谢 | 修复发布后，除非你要求匿名 |

修复发布后会在 Release 说明和 GitHub Security Advisory 里写清楚影响范围与升级方式。
本项目**没有漏洞赏金**，但会在 Advisory 与 Release 里署名致谢。

## 哪些算安全问题

- 凭据泄露：微软 OAuth 令牌、Yggdrasil/外置登录令牌被写入日志、命令行参数、崩溃报告或上传到网络
- 下载链被投毒：镜像/资源地址可被替换、校验被跳过（Mojang 资源、libraries、模组、整合包、自更新 exe）
- 任意代码执行：解压路径穿越（zip slip）、恶意整合包/数据包逃逸出目标目录、启动参数注入
- 本地提权或越权：写到不该写的路径、绕过版本隔离读写别的实例
- 更新器：把玩家更新到非官方构建，或替换过程可被劫持

## 哪些不算（但仍欢迎反馈）

- 需要攻击者已经拥有本机管理员权限或已能改本机文件的问题
- 依赖库自身的已知问题（请报给上游；我们会跟进升级）
- 未签名的发布被 Windows 智能应用控制拦截——这是**已知且预期**的行为，不是漏洞，见
  [docs/SMART-APP-CONTROL.md](docs/SMART-APP-CONTROL.md)
- 启动器界面/体验类问题，请走普通 Issue

## 我们已经在做的

- 所有下载走 HTTPS，Mojang 资源与 libraries 校验 SHA-1/SHA-256
- 账号令牌尽量不落盘明文（Windows 上使用系统凭据存储），不写入日志
- 启动游戏用参数数组而不是拼接命令行字符串
- 整合包/数据包解压限制在目标目录内
- 自更新只从本仓库的 Release 资产下载，并校验文件名；替换由独立脚本等待进程退出后执行

## 已知限制（诚实说明）

发布产物**目前没有代码签名证书**（免费签名申请未通过），因此 Windows 11 智能应用控制会拦截。
这不是漏洞，但我们正在通过签名证书 / 商店分发解决；在此之前请从官方 Release 或 Gitee 镜像下载，
并按 [docs/SMART-APP-CONTROL.md](docs/SMART-APP-CONTROL.md) 处理。

---

## English summary

- **Supported versions:** latest release only.
- **Report privately:** use GitHub private vulnerability reporting
  (<https://github.com/OStonefishO/STMEMELauncher/security/advisories/new>); do not open a public issue.
- **Response:** acknowledgement within 3 days, triage within 7, fix within 30 for valid reports.
- **In scope:** credential leakage (Microsoft OAuth / Yggdrasil tokens), download or update tampering,
  arbitrary code execution via archive extraction or launch arguments, sandbox/isolation bypass.
- **Out of scope:** issues requiring existing admin/local access, upstream dependency bugs,
  and the expected Smart App Control block caused by our release **not being code signed**.
- **No bug bounty**; we credit reporters in the advisory and release notes unless asked otherwise.
