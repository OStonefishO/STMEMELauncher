# 微软正版登录：现状与申请流程

## 实测结果（2026-11-09）

用我们自己的 Azure 应用（Client ID `52fb2aba-9fa1-4605-be0d-431776c0880d`，
账户类型「所有 Microsoft 帐户用户」，已开启公共客户端流）走完整条链路：

| 步骤 | 接口 | 结果 |
|---|---|---|
| ① 设备码 → MSA token | `login.microsoftonline.com/consumers/oauth2/v2.0/token` | ✅ 成功 |
| ② Xbox Live | `user.auth.xboxlive.com/user/authenticate` | ✅ 成功（拿到 uhs） |
| ③ XSTS | `xsts.auth.xboxlive.com/xsts/authorize` | ✅ 成功（RETAIL sandbox） |
| ④ Minecraft | `api.minecraftservices.com/authentication/login_with_xbox` | ❌ **403** |

第 ④ 步的原始返回：

```json
{
  "path": "/authentication/login_with_xbox",
  "errorMessage": "Invalid app registration, see https://aka.ms/AppRegInfo for more information"
}
```

**结论：** 不是配置问题，是 Minecraft 服务端有**应用白名单**——只有被 Mojang 批准过的
Client ID 才能换 Minecraft token。新注册的应用默认不在名单里。

## 解决办法：提交官方 AppID 登记表

Minecraft 官方提供一个表单，把 Client ID 报上去，Mojang Enforcement 审核通过后会把你
加入 allowlist（有多位第三方启动器开发者确认这条路可行）。

- 说明页：<https://help.minecraft.net/hc/en-us/articles/16254801392141>
- 申请表：<https://forms.cloud.microsoft/Pages/ResponsePage.aspx?id=v4j5cvGGr0GRqy180BHbR-ajEQ1td1ROpz00KtS8Gd5UNVpPTkVLNFVROVQxNkdRMEtXVjNQQjdXVC4u>

### 提交时要准备的信息

| 项目 | 我们的值 |
|---|---|
| 应用名称 | MEME Launcher |
| Application (client) ID | `52fb2aba-9fa1-4605-be0d-431776c0880d` |
| 启动器名称 | MEME Launcher |
| 项目地址 | https://github.com/OStonefishO/STMEMELauncher |
| 是否开源 | 是（公开仓库） |
| 用途 | Minecraft: Java Edition 第三方启动器，玩家用自己已购买的账号登录 |
| 联系方式 | 你的邮箱 |

### 英文提交说明（可直接粘）

> MEME Launcher is a free, open-source third-party launcher for Minecraft: Java Edition.
> Users sign in with their own Microsoft account that already owns the game. We do not
> resell, share, or proxy accounts, and we do not bypass Microsoft authentication.
>
> Our authentication flow is the standard one: Microsoft OAuth 2.0 device code →
> Xbox Live → XSTS → `api.minecraftservices.com/authentication/login_with_xbox`.
> Steps 1-3 succeed; step 4 returns HTTP 403 "Invalid app registration".
>
> We are requesting that our Application (client) ID be added to the Minecraft Services
> allowlist so that legitimate owners of the game can sign in with our launcher.
>
> - Launcher: MEME Launcher
> - Client ID: 52fb2aba-9fa1-4605-be0d-431776c0880d
> - Source: https://github.com/OStonefishO/STMEMELauncher
> - Distribution: free download from GitHub Releases
> - Platform: Windows

## 审核通过之后

不需要改代码：Client ID 已经内置在 `LauncherSettings.MicrosoftClientId` 的默认值里，
登录页的链路也已经就绪。重新登录一次即可。

## 审核期间怎么办

1. **外置登录**（LittleSkin 等 Yggdrasil 皮肤站）——已有，能拿皮肤
2. **导入官方启动器账号**——已有，读 `launcher_accounts.json` 复用 token，约 24 小时有效
3. 离线登录——兜底

## 参考

- [Microsoft Q&A：Invalid app registration 的解决过程](https://learn.microsoft.com/en-us/answers/questions/5984225/minecraft-services-http-403-invalid-app-registrati)
- [Microsoft Q&A：第三方启动器如何获得 XboxLive.signin 权限](https://learn.microsoft.com/en-us/answers/questions/5768276/how-to-get-xboxlive-signin-permission-for-azure-ap)
