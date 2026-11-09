# MEME Mirror · 自建镜像站

本地跑的一个小镜像站：把被墙/不稳定的上游缓存到本地磁盘，给 MEME Launcher 和手动下载用。

## 启动

双击 **`启动镜像站.cmd`**，或者：

```powershell
.\src\Meme.Mirror\bin\Release\net6.0\MemeMirror.exe --port 8123
```

会自动打开 `http://127.0.0.1:8123/`。

参数：

| 参数 | 默认 | 说明 |
|---|---|---|
| `--port` | 8123 | 监听端口 |
| `--cache` | `%LOCALAPPDATA%\MEMEMirror\cache` | 缓存目录，删掉就是清空 |
| `--no-open` | 关 | 不自动开浏览器 |

## 镜像了哪些源

| 本地前缀 | 上游 | 为什么 |
|---|---|---|
| `/m/neoforge/` | bmclapi2 `maven` | `maven.neoforged.net` 在这台机器上被 DNS 污染，直连超时 |
| `/m/forge/` | `maven.minecraftforge.net` | 直连可用，缓存后更快 |
| `/m/libraries/` | bmclapi2 `libraries` | Mojang 依赖库 |
| `/m/piston/` | bmclapi2 | 版本清单、version.json |
| `/m/fabric/` | bmclapi2 `fabric-meta` | Fabric 加载器列表 / profile |
| `/m/assets/` | bmclapi2 `assets` | 贴图音效等资源对象 |

## 怎么用

把原来的地址域名换成 `http://127.0.0.1:8123/m/{源}/`，路径不变：

```
https://maven.neoforged.net/releases/net/neoforged/bus/8.0.5/bus-8.0.5.jar
→ http://127.0.0.1:8123/m/neoforge/net/neoforged/bus/8.0.5/bus-8.0.5.jar

https://meta.fabricmc.net/v2/versions/loader
→ http://127.0.0.1:8123/m/fabric/v2/versions/loader
```

第一次请求会回源并落盘，第二次起就是本地直出（实测 0.42s → 0.0015s）。

启动器里：设置页 → **自建镜像站地址** 填 `http://127.0.0.1:8123`，之后所有下载（版本、库、资源、Fabric/NeoForge/Forge 安装器）都会先走镜像，失败自动回退官方/BMCLAPI。

## 接口

- `GET /` 状态面板
- `GET /api/status` JSON：各源健康状态、命中/回源次数、缓存占用
- `GET|HEAD /m/{源}/{路径}` 代理 + 缓存

## 实测

- NeoForge 26.3.0.101 全量安装（30 个依赖）走镜像完成，缓存 13.2 MB
- 第二次请求命中缓存，耗时 1.5 ms

## 已知限制

- 只做 GET/HEAD，不代理上传；不支持 Range 断点续传
- 缓存无上限，不会自动清理（删目录即可）
- 目前是本机自用形态；要对外提供服务，得换真实 Web 服务器 + 反代
