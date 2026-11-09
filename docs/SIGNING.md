# 代码签名与自动发版

未签名的 exe 在开了 **Windows 11 智能应用控制** 的电脑上会被直接拦掉（弹窗里连「仍要运行」都没有）。
这个问题只有「给 exe 一个受信任的数字签名」能彻底解决，没有技术绕过。

本仓库已经准备好两条路，任选其一：

| 路线 | 花费 | 说明 |
|---|---|---|
| **A. SignPath（推荐，免费）** | 0 | 面向开源项目的免费签名服务，签出来的是受信任证书；过审后 CI 全自动签名发版 |
| **B. 自己买证书** | 约 ¥200–2000/年 | Certum 开源证书最便宜（约 €30–70/年），OV/EV 更贵；本地脚本签名 |

---

## A. SignPath（GitHub Actions 自动签名）

`.github/workflows/release.yml` 已经写好了：打 tag → 构建 → 送签 → 发 Release。

### 1. 申请

1. 打开 <https://signpath.org/apply> 提交开源项目申请
   - 需要：**公开 GitHub 仓库**、OSI 认可的开源许可证（本仓库用 MIT 就行）、项目简介
   - 审核通常几天到两周
2. 通过后会收到 `app.signpath.io` 的组织邀请

### 2. 在 SignPath 里配置

登录 <https://app.signpath.io>：

| 步骤 | 位置 | 填什么 |
|---|---|---|
| 连接 GitHub | Organization → **GitHub** | 授权 SignPath GitHub App，允许访问 `OStonefishO/STMEMELauncher` |
| 建项目 | Projects → **New project** | slug：`meme-launcher`（记住它） |
| 制品配置 | 项目里 Artifact configurations → New | slug：`meme-launcher-exe`；类型 **Zip**；文档根填 `./`；要签的文件 `MEME-Launcher-*.exe` |
| 签名策略 | 项目里 Signing policies → New | slug：`release-signing`；类型 **Release signing**；可以打开「需要人工审批」 |

### 3. 拿三个值

| 值 | 在哪 |
|---|---|
| Organization ID | Organization 设置页顶部（UUID） |
| API Token | 右上角 User → API tokens → New，权限选 `submit signing requests` |
| 三个 slug | 就是上面自己填的 `meme-launcher` / `release-signing` / `meme-launcher-exe` |

### 4. 填进 GitHub 仓库

仓库 → Settings → Secrets and variables → **Actions**：

**Secrets**（加密）

```
SIGNPATH_API_TOKEN        = <你的 token>
SIGNPATH_ORGANIZATION_ID  = <组织 UUID>
GITEE_TOKEN               = <可选，顺手传到 Gitee 镜像>
```

**Variables**（明文也行）

```
SIGNPATH_PROJECT_SLUG         = meme-launcher
SIGNPATH_SIGNING_POLICY_SLUG  = release-signing
SIGNPATH_ARTIFACT_CONFIG_SLUG = meme-launcher-exe
```

### 5. 发版

```powershell
# 先把 APP_VERSION 改掉（src/Aurora.App/MainWindow.xaml.cs 里的 AppVersion）
git commit -am "release: 0.14.0"
git tag v0.14.0
git push origin main --tags
```

push tag 后 Actions 会自动：
1. `dotnet publish` 出单文件 exe
2. 上传成 artifact，交给 SignPath 签名（等结果）
3. 把签名后的 exe + `SHA256` + `使用说明` 发到 GitHub Release
4. 配了 `GITEE_TOKEN` 的话同时传到 Gitee

也可以在 Actions 页面手动 `Run workflow`，填版本号试跑；勾 `skip_signing` 只构建不签。

> 想先看流程跑通再申请签名：把 `sign` 任务的 `if` 临时改成 `false`，就能出未签名版。

---

## B. 自己买证书（本地 / 部分 CI）

### 推荐的便宜选项

- **Certum Open Source Code Signing**：约 €30–70/年，开源项目可申请（要证明仓库开源）
- **Azure Trusted Signing**：约 $9.99/月，微软自家，需要身份验证
- **OV / EV 证书**：DigiCert、Sectigo 等，¥1000–3000+/年

拿到 `.pfx` 后：

```powershell
.\scripts\publish-release.ps1 -Version 0.14.0 -GiteeToken <token> `
    -SignPfx "D:\cert\meme.pfx" -SignPassword "<密码>"
```

脚本会调用 `signtool` 签 `MEME-Launcher-vX.exe` 并验签，然后照常发版。
`signtool` 来自 Windows SDK（`Windows Kits\10\bin\...\x64\signtool.exe`），脚本会自动找。

也可以手动签：

```powershell
signtool sign /fd SHA256 /f meme.pfx /p <密码> /tr http://timestamp.digicert.com /td SHA256 MEME-Launcher-v0.14.0.exe
signtool verify /pa MEME-Launcher-v0.14.0.exe
```

---

## 签名之后会怎样

| 场景 | 签名前 | 签名后 |
|---|---|---|
| Windows 11 智能应用控制 | **直接拦死** | 放行 |
| SmartScreen「未知发布者」 | 提示，需「更多信息 → 仍要运行」 | 有证书后不再提示（EV 立即，OV 需要一点信誉积累） |
| 杀软误报 | 偶发 | 明显减少 |

---

## 常见问题

**Q：自签名证书行不行？**
不行。SAC 只认「受信任的根证书」签发的证书，自签名等于没签，一样被拦。

**Q：换个打包方式（zip / 安装包 / 改后缀）能绕过吗？**
不能。SAC 拦的是「执行一个未签名的可执行文件」这件事本身，改包装不改变内核。

**Q：能不能用一个已签名的程序去加载我们的代码？**
理论上有（比如用微软签名的 `dotnet.exe` 跑我们的 dll），但：
- 需要玩家机器装 .NET 运行时（又是一次安装）
- 属于钻空子，随时可能被收紧
- 体积从 70MB 变成 200MB 文件夹

不推荐当正式方案，只适合临时救急。

**Q：SignPath 审核没通过怎么办？**
先用 Certum 的开源证书（一年几百块），或者像现在这样附带 `使用说明` 让玩家手动关 SAC。
