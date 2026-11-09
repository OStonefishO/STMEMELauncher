# CurseForge API Key 申请：逐项填写

申请页：<https://console.curseforge.com/> → **API Applications**（monday workforms）

## 1. Your full Discord username

填你的完整 Discord 用户名，格式 `用户名#1234`（新版 Discord 是 `@用户名`，两个都写上更保险）。

**没有 Discord 账号就先注册一个**（discord.com），这一项是必填且会被核实。

## 2. Describe the project goal and scope

> MEME Launcher is a free, open-source third-party launcher for Minecraft: Java Edition
> (Windows). It helps players manage game versions, install mod loaders, and browse/download
> mods, modpacks, shader packs and resource packs from CurseForge and Modrinth.
>
> Scope: the launcher only reads project metadata and file listings, then downloads the
> files the user explicitly selects, directly from the CurseForge CDN to the user's own
> machine. It does not re-host, mirror, bundle, or redistribute any CurseForge file, and it
> does not expose the API to third parties. API usage is low-volume and per-user: each client
> only queries when the user searches or opens a project.

## 3. Why are you building this project?

> Because existing launchers are either closed-source, hard to use for Chinese players, or
> lack proper mod management. I want a lightweight, open-source launcher that gives players a
> clean way to browse CurseForge content without leaving the launcher, while respecting
> authors' rights: every download points to the official CDN and every project links back to
> its CurseForge page.

## 4. Supported games

选 **Minecraft**。

## 5. Website URL

`https://github.com/OStonefishO/STMEMELauncher`

## 6. Git URL

`https://github.com/OStonefishO/STMEMELauncher.git`

## 7. 勾选框：file distribution subject to mod author's approval

**勾上**（我们的行为符合：只从官方 CDN 下载，不转存、不二次分发）。

## 8. Are you looking to monetize the project?

选 **No**（免费开源，无广告、无订阅、无捐赠强制）。

## 9. If you have a business model please describe it

> No business model. The project is free and open source (MIT-style, public GitHub
> repository). There are no ads, no paid tiers, no donations required, and no resale of
> CurseForge content.

## 10. How would you contribute to the mod authors?

> - Downloads always go through the official CurseForge CDN, with the author's share of
>   download revenue intact (no re-hosting or mirroring).
> - Every project page in the launcher shows the author name and links back to the original
>   CurseForge page, so users can support, follow and donate to authors directly.
> - No modpack or mod files are bundled with the launcher; nothing is redistributed.
> - If a mod author opts out of third-party distribution, we remove it from the launcher on
>   request.

## 11. 勾选框：above certain volumes I might be required to reduce API calls or pay bandwidth

**勾上**（表示你理解并接受）。

## 12. 勾选框：I have read and agree to the API's TOS

**勾上**。

## 13. Additional notes（可选）

> Launcher name: MEME Launcher
> Platform: Windows (WPF, .NET 6)
> Repo: https://github.com/OStonefishO/STMEMELauncher
> Current stage: open source, small user base (early development)
> We only need read-only endpoints: /v1/mods/search, /v1/mods/{id}/files, /v1/mods/{id}

## 提交之后

1. 审核通常几天到两周，结果发到 CurseForge 账号邮箱
2. 通过后在 console 里看到 **API Key**，复制进启动器「设置 → 下载与镜像 → CurseForge API Key」
3. 没通过时表单会说明原因，常见是「项目描述不够具体」或「Discord 无法核实」

## 常见坑

- 一个 CurseForge 账号只能有一个 API 应用，重复提交会被合并处理
- Discord 用户名写错会直接被打回（他们要加你核实项目）
- 描述里**不要写**「我会把 mod 打包进启动器」之类的话，那属于分发，需要额外授权
