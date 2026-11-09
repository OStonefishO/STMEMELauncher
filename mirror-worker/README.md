# MEME Mirror · Cloudflare Worker 部署（永久在线版）

本地版要开着电脑才有；这个版本部署到 Cloudflare 之后**一直在线**，免费额度对镜像站这种流量完全够用，而且自带全球边缘缓存。

## 三步部署（不用装任何工具）

1. 打开 <https://dash.cloudflare.com/> 注册/登录（免费，邮箱即可）
2. 左侧 **Workers & Pages** → **Create** → **Worker** → 起个名字（比如 `meme-mirror`）→ **Deploy**
3. 点 **Edit code**，把 [worker.js](worker.js) 的内容整个粘进去 → **Deploy**

完成后你会拿到一个地址，形如：

```
https://meme-mirror.<你的子域>.workers.dev
```

打开它就能看到和本地版一样的面板。

> 想要更好记的域名：Worker 的 **Settings → Triggers → Custom Domains** 绑定自己的域名（域名要托管在 Cloudflare）。

## 接到启动器

设置页 → **自建镜像站地址** → 填刚才那个 `https://...workers.dev`，保存。

之后所有下载（版本清单、依赖库、资源对象、Fabric / NeoForge / Forge 安装器）都会先走镜像，失败自动回退官方源。

## 接口

| 路径 | 作用 |
| --- | --- |
| `/` | 状态面板 |
| `/api/status` | JSON 源列表 |
| `/m/{源}/{路径}` | 代理 + 边缘缓存，`X-Meme-Cache: HIT/MISS` 能看到是否命中 |

源：`neoforge` `forge` `libraries` `piston` `fabric` `assets`

## 注意事项

- 免费版每天 10 万次请求，镜像站个人/小圈子用完全够
- 缓存按 URL 存，单个响应有大小上限；大 jar 走 Worker 也没问题（流式转发）
- 上游是 BMCLAPI 和 Forge 官方 maven；如果 BMCLAPI 挂了会直接透传错误，不会缓存错误响应
- 免费版 `workers.dev` 在国内偶尔被墙，**建议绑一个自己的域名**，或者先用着看效果

## 和本地版的关系

`src/Meme.Mirror` 是本地版（Windows 服务，带磁盘缓存，适合自己用）；
`mirror-worker/worker.js` 是云端版（边缘缓存，适合长期挂着给别人用）。
两者路径规则完全一致，启动器里改个地址就能切换。
