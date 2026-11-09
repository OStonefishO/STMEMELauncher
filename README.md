# MEME Launcher

一个从零写的 Minecraft 启动器，参考 PCL2 的行为设计，**先把框架跑通，UI 后面再打磨**。

框架的目标很明确：把「扫描本地版本 → 解析继承链 → 挑 Java → 拼命令行 → 起进程 → 收日志 → 定位崩溃」这条链路做扎实、可测试、可复用，UI 只是最后一层壳。

---

## 现在能做什么

| 能力 | 状态 | 说明 |
|---|---|---|
| 扫描 `.minecraft/versions` | ✅ | 自动挑主 json、识别 Forge / NeoForge / Fabric / Quilt / OptiFine / 原版 |
| 继承链解析（`inheritsFrom`） | ✅ | 库、参数、主类、资源索引按子版本优先合并 |
| rules 求值（系统 / 架构 / features） | ✅ | 与官方启动器语义一致，默认拒绝 |
| Natives 解压 | ✅ | 支持 `${arch}` 分类器、`extract.exclude` |
| 参数占位符替换 | ✅ | 含 `${classpath}` `${library_directory}` 等；空值保留为空参数 |
| Java 探测与匹配 | ✅ | 读 `release` 文件，找不到再跑 `java -version`；按主版本择优 |
| 版本隔离（PCL2 行为） | ✅ | auto：版本目录里有 mods/config/saves 就单独用该目录做游戏目录 |
| 启动进程 + 日志 | ✅ | 逐行回调，log4j XML 自动转成可读文本 |
| 崩溃定位 | ✅ | 读 `crash-reports` 最新报告，提取 Description / 异常行 |
| 原版安装（下载） | ⚠️ 未实测 | 版本 json / client jar / 依赖库 / 资源对象，走 BMCLAPI 镜像 |
| 微软登录 | ❌ | 目前只有离线登录（UUID 用 `OfflinePlayer:` 算法） |
| Forge / Fabric 自动安装 | ❌ | 还没有跑安装器 |
| 正式 UI | ❌ | 只有占位壳，见下文 |

### 已实测

在本机 `D:\PCL2\.minecraft`（12 个版本）上验证过：

- `aurora list` / `doctor` / `info` / `plan` 全部输出正确（见下方输出样例）
- **真实启动成功**：`26.2-Fabric 0.19.3`，Java 25，进到游戏内并生成了世界；日志写入 `.minecraft/logs/latest.log`，含 `Setting user: Aurora` 与 `Loading Minecraft 26.2 with Fabric Loader 0.19.3`
- 1.8.9-Forge 这类旧版（`minecraftArguments` 字符串、无 JVM 参数段）能正确拼接并给出缺失库清单

---

## 目录结构

```
MCLauncher/
├─ src/
│  ├─ Aurora.Core/                 # 引擎（无 UI 依赖，可被任何前端复用）
│  │  ├─ Configuration/            # 设置读写、.minecraft 自动探测
│  │  ├─ Infrastructure/           # JSON、命令行拆分、离线 UUID、日志规范化
│  │  ├─ Java/                     # Java 探测与版本匹配
│  │  ├─ Launch/                   # LaunchPlan / 命令行构建 / 进程与崩溃分析
│  │  ├─ Models/                   # version.json 模型、ResolvedVersion
│  │  ├─ Net/                      # 下载器（镜像+重试+SHA1）、原版安装器
│  │  ├─ Versions/                 # 版本仓库、继承合并、rules、maven、版本隔离
│  │  └─ LauncherService.cs        # 门面：设置 + 仓库 + Java + 启动 + 安装
│  ├─ Aurora.Cli/                  # 无界面自检/调试入口（aurora.exe）
│  └─ Aurora.App/                  # WPF 界面（启动 / 安装 / 内容 / 设置 四页）
├─ MEME启动器.exe                # 自包含单文件，双击即用
├─ docs/ARCHITECTURE.md            # 设计说明与扩展点
├─ docs/screenshot.png             # 界面截图
├─ 启动 Aurora.cmd                 # 从源码编译并启动
├─ build.ps1                       # 一键构建/发布
└─ NuGet.config                    # nuget.org + 华为云镜像
```

## 直接打开（推荐）

双击 **`MEME启动器.exe`**（项目根目录有一份，桌面上也放了一份）。

它是**自包含单文件**（66 MB）：不用装 .NET、不用编译、不依赖仓库里的任何东西，双击就开。

![主界面](docs/screens/p0.png)

![安装](docs/screens/p1.png)

界面分四页（左侧导航）：

- **启动**：版本卡片列表（左侧色条按加载器区分）+ 右侧详情卡（Java / Mods / 内存 / 实际游戏目录）+ 大启动按钮
- **安装**：选 Minecraft 版本 → 选 Fabric / NeoForge / Forge → 一条链路装完（原版自动补齐）
- **内容**：Modrinth 搜索模组 / 整合包 / 光影 / 资源包，直接装进当前版本；CurseForge 支持粘贴链接
- **设置**：账号（微软登录）、游戏目录、内存、Java、版本隔离、镜像开关、自建镜像站地址

想从源码重新编译时，用 `启动 Aurora.cmd`（先 build 再启动）。

## 镜像站

两个版本，路径规则一样（`/m/{源}/{路径}`）：

| 版本 | 位置 | 特点 |
|---|---|---|
| 本地版 | `启动镜像站.cmd` → `http://127.0.0.1:8123/` | 磁盘缓存，要开机 |
| 云端版 | `mirror-worker/worker.js` → Cloudflare Workers | 永久在线、边缘缓存，免费额度够用 |

镜像了 NeoForge / Forge maven、Mojang 库与元数据、Fabric meta、资源对象。详见 [docs/MIRROR.md](docs/MIRROR.md)。

## 账号

默认离线登录；微软正版登录走设备码流程（点「用微软账号登录」→ 自动复制验证码并打开浏览器 → 确认即可）。
需要一次性填 Azure 应用的 ClientId，作者内置好之后玩家看不到这一项。
## 构建与运行

需要 .NET SDK（本机已装在 `D:\综合创作\_env\dotnet`）：

```powershell
# CLI
& 'D:\综合创作\_env\dotnet\dotnet.exe' build .\src\Aurora.Cli -c Release
.\src\Aurora.Cli\bin\Release\net6.0\aurora.exe doctor

# WPF 壳
& 'D:\综合创作\_env\dotnet\dotnet.exe' build .\src\Aurora.App -c Release
.\src\Aurora.App\bin\Release\net6.0-windows\Aurora.App.exe
```

也可以用 `.\build.ps1`（构建 + 输出到 `dist\`）。

## CLI 用法

```
aurora list                        列出本地版本
aurora info <id>                   版本详情（继承链、库、Java）
aurora plan <id>                   只生成命令行，不启动
aurora launch <id> [--name 玩家] [--timeout 秒] [--dry]
aurora java                        列出探测到的 Java
aurora remote [--all]              查看远端版本清单
aurora install <id> [--no-assets]  安装原版
aurora doctor                      环境自检
--game <目录>                      临时指定 .minecraft 目录
```

`list` 的实际输出（本机）：

```
版本                              MC        加载器       加载器版本             Java  Mods  状态
26.3-NeoForge_26.3.0.101        26.3      neoforge  26.3.0.101        25    2     OK
26.2-Fabric 0.19.3              26.2      fabric    0.19.3            25    4     OK
All the Mods 11 - ATM11         26.1.2    neoforge  -                 25    258   OK
FTB StoneBlock 4                1.21.1    neoforge  -                 21    404   OK
1.8.9-Forge_11.15.1.2318        1.8.9     forge     11.15.1.2318-1.8.9 8    0     OK
```

## 配置文件

`%APPDATA%\AuroraLauncher\config.json`

| 字段 | 默认 | 说明 |
|---|---|---|
| `GameDirectory` | 自动挑版本最多的 `.minecraft` | 游戏根目录 |
| `PlayerName` | `Steve` | 离线登录名 |
| `JavaPath` | 空 = 自动 | 指定 java 可执行文件或 JDK 目录 |
| `ExtraJavaPaths` | `[]` | 额外 Java 搜索根目录（可填 JDK 所在父目录） |
| `MinMemoryMb` / `MaxMemoryMb` | 512 / 物理内存一半 | JVM 堆 |
| `WindowWidth` / `WindowHeight` | 0 | 0 表示不传，交给游戏 |
| `ExtraJvmArgs` / `ExtraGameArgs` | 空 | 追加参数（按命令行规则拆分） |
| `UseMirror` | true | 下载走 BMCLAPI |
| `VersionIsolation` | `auto` | `auto` / `on` / `off` |

## UI 现状

三个页面已经接好引擎，数据全部来自 `LauncherService`，不是假数据：

| 页面 | 数据来源 | 备注 |
|---|---|---|
| 启动 | `ListVersions()` / `SelectJava()` / `GameDirectoryFor()` | 卡片左侧色条按加载器区分：Forge 橙、NeoForge 紫、Fabric 青 |
| 下载 | `Installer.GetManifestAsync()` + `InstallVanillaAsync()` | 进度条吃 `InstallProgress` |
| 设置 | `LauncherSettings` ↔ `config.json` | 改游戏目录会重建 `LauncherService` |

还没做的（按优先级）：

1. 托盘 / 最小化到后台
2. 多账号 + 微软登录（`LaunchOptions` 里已经留好 `AccessToken` / `Uuid` / `UserType`）
3. 模组与整合包管理（导入 CurseForge/Modrinth 压缩包）
4. Forge / Fabric 自动安装（现在只能装原版）
5. 资源完整性校验、下载失败重试面板

设计细节和扩展点见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。
