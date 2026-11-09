# 架构说明

## 分层

```
┌──────────────────────────────┐
│ Aurora.App (WPF 壳)          │  只做展示与交互
├──────────────────────────────┤
│ Aurora.Cli (aurora.exe)      │  无界面自检 / 排障 / CI
├──────────────────────────────┤
│ Aurora.Core (LauncherService)│  门面
│  ├ VersionRepository         │  扫描 / 继承合并
│  ├ RuleEvaluator             │  rules 求值
│  ├ JavaLocator               │  JDK 探测与匹配
│  ├ LaunchPlanner             │  命令行构建 + natives
│  ├ GameLauncher              │  进程 / 日志 / 崩溃
│  └ Installer + DownloadClient│  下载与安装
└──────────────────────────────┘
```

Core 不引用 WPF，任何前端（WPF、WinUI、Avalonia、Web）都能直接复用。

## 启动流水线

```
settings.json
   │
   ├─ ConfigStore.Load()            自动探测 .minecraft（按版本数量打分）
   │
   ├─ VersionRepository.Scan()      遍历 versions/*，挑主 json，识别加载器
   │
   ├─ VersionRepository.Resolve(id) 递归合并 inheritsFrom
   │      libraries: 子在前，按 group:artifact:classifier 去重
   │      arguments: 子在前，逐条追加（保留 rules）
   │      其余字段: 子优先，空则回落父
   │
   ├─ JavaLocator.Select(major)     精确主版本 → 1.8 只认 8 → 否则取最近的更高版本
   │
   ├─ LaunchPlanner.Build()         natives 解压 → classpath → 占位符替换 → 参数
   │
   └─ GameLauncher.LaunchAsync()    起进程 → 读 stdout/stderr → 规范化 → 退出码/崩溃
```

## 关键实现点

### 1. rules 求值（`RuleEvaluator`）

- 没有 rules → 允许
- 有 rules → **默认拒绝**，按顺序匹配覆盖（`allow` / `disallow`）
- 匹配维度：`os.name`、`os.arch`、`os.version`（正则）、`features.*`
- features 只支持官方定义的那几个（demo、自定义分辨率、quick play），未知一律 false

### 2. 继承合并（`VersionRepository.Merge`）

子版本覆盖父版本，但 `libraries` 与 `arguments` 是**拼接**而不是覆盖：

- 库按 `group:artifact:classifier` 去重，子版本优先（Forge 覆盖原版库版本）
- 参数保留原始 `ArgumentEntry`（含 rules），执行时再求值，避免提前丢失条件
- `minecraftArguments`（1.12 及更早）和 `arguments`（1.13+）两条路径都支持

### 3. classpath 顺序

`libraries` 按 json 顺序 → 最后才是客户端 jar，与官方启动器一致。

带 `natives` 且没有 `downloads.artifact` 的库视为「纯 natives」，不加入 classpath，只解压。

### 4. Natives

- 解压到 `versions/<id>/natives`，每次启动前清空重建
- 支持 `${arch}` → 32/64
- 支持 `extract.exclude`（跳过 `META-INF` 等）
- 现代版本（1.19+）没有 natives 分类器，此时不报「缺少 natives」；只有版本确实声明了却一个都没解出来才警告

### 5. 占位符替换

`${classpath}`、`${natives_directory}`、`${library_directory}`、`${assets_index_name}`、`${auth_uuid}` 等一次性映射表替换；未知占位符清空。

**空值必须保留为空参数**：`--clientId` 后面跟 `${clientId}`，如果直接把空串丢掉，`--xuid` 就会被当成 clientId 的值，游戏会解析错参数。

### 6. 版本隔离（PCL2 的关键行为）

同一个 `.minecraft` 下，整合包的 `mods/`、`config/`、`saves/` 就在版本目录里。如果游戏目录错用 `.minecraft` 根，**mod 一个都不会加载**（实测过：Fabric 只加载了 loader 和 minecraft 本身）。

`VersionIsolation.ShouldIsolate`：

- `on` / `off` 直接听设置
- `auto`：版本目录里有 `mods/*.jar`、`config/`、`saves/` 或 `options.txt` 就隔离

### 7. 日志规范化（`LogNormalizer`）

MC 26.x 的 log4j 配置会把 XML 事件直接写到 stdout：

```xml
<log4j:Event logger="..." level="INFO" thread="Render thread">
  <log4j:Message><![CDATA[Setting user: Aurora]]></log4j:Message>
</log4j:Event>
```

`LogNormalizer` 用状态机把这些块拼起来，还原成 `[Render thread/INFO]: Setting user: Aurora`，普通文本原样透传；进程退出时 `Flush()` 防止半截块丢失。

### 8. 下载与镜像

`DownloadClient.MapUrl` 把 Mojang 域名映射到 BMCLAPI：

| 官方 | 镜像 |
|---|---|
| `piston-meta.mojang.com` | `bmclapi2.bangbang93.com` |
| `libraries.minecraft.net` | `bmclapi2.bangbang93.com/libraries` |
| `resources.download.minecraft.net` | `bmclapi2.bangbang93.com/assets` |
| `meta.fabricmc.net` | `bmclapi2.bangbang93.com/fabric-meta` |

镜像失败会自动回退官方源；文件已存在时先按 SHA1/大小校验，合格就跳过。

## 踩过的坑（都已在代码里修掉）

| 现象 | 原因 | 处理 |
|---|---|---|
| 现代版本报「没有解压出 natives」 | 1.19+ 本来就没有 natives 分类器 | 只在版本确实声明了 natives 时才警告 |
| `--clientId --xuid` 连在一起 | 空占位符被过滤 | 空值保留为空参数 |
| 整合包 mod 全不加载 | 游戏目录用了 `.minecraft` 根 | 版本隔离 |
| 1.8.9 Forge 版本号显示成 `11.15.1.2318-1.8.9` | maven 版本带 MC 前缀 | 按 `clientVersion` 前缀裁剪 |
| WPF 窗口内容被裁掉 | 自定义 manifest 声明 permonitorv2，WPF 不认 | 去掉自定义 manifest，交给 WPF 自己处理 DPI |

## 扩展点

### 加一个加载器支持（如 Quilt）

1. `VersionRepository.DetectLoader` 增加识别分支
2. 版本 json 本身如果已经是标准格式，`LaunchPlanner` 不用改
3. 需要额外安装步骤的（Forge installer / Fabric meta），在 `Net/` 下加一个 `XxxInstaller`

### 加微软登录

`LaunchOptions` 里已经有 `AccessToken` / `Uuid` / `UserType` / `ClientId` 的位置：

1. 新增 `Auth/` 目录，实现 device code 或授权码流程
2. 拿到 `access_token` + `uuid` 后填进 `LaunchOptions`，`UserType` 设 `msa`
3. 把 `--clientId` 占位符指向真实 clientId（`LaunchPlanner` 的替换表里已预留）

### UI 层

`LauncherService` 是唯一入口，建议：

- `MainViewModel` 持有 `LauncherService`
- 版本列表绑定 `List<VersionSummary>`
- 启动用 `GameLauncher.StartDetached` + `OutputReceived` 事件驱动日志面板（不要轮询）
- 下载页用 `IProgress<InstallProgress>`，已带 Stage/Done/Total/Percent

## 已知短板

- 安装器只覆盖原版；Forge/Fabric 需要跑官方 installer 或 meta API
- 没有资源完整性校验（只校验下载时带 SHA1 的文件）
- 没有多账号、没有皮肤、没有模组管理
- 崩溃分析只取报告头部，不做堆栈归类
