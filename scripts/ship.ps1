param(
    [Parameter(Mandatory = $true)][string]$Notes,
    [string]$Version = "",
    # 给了就顺手传到 Gitee 镜像（令牌：https://gitee.com/profile/personal_access_tokens ，勾 projects）
    [string]$GiteeToken = "",
    [string]$GiteeOwner = "stonefish8",
    [string]$GiteeRepo = "STMEMELauncher",
    [switch]$NoPush
)

# 一条命令发一版：改版本号 → 构建 → 打包校验值 → 提交打 tag → 推送 → 发 GitHub Release
# 用法： .\scripts\ship.ps1 -Notes "这版改了什么…"
#        .\scripts\ship.ps1 -Version 0.16.0 -Notes "…"

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$dotnet = 'D:\综合创作\_env\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}

$appFile = 'src/Aurora.App/MainWindow.xaml.cs'
$text = [System.IO.File]::ReadAllText($appFile)
$match = [regex]::Match($text, 'private const string AppVersion = "([\d\.]+)";')
if (-not $match.Success) { throw '没找到 AppVersion' }

$current = $match.Groups[1].Value
if (-not $Version) {
    $parts = $current.Split('.')
    $parts[$parts.Length - 1] = ([int]$parts[$parts.Length - 1] + 1).ToString()
    $Version = $parts -join '.'
}

Write-Host "==> 发版 v$Version（上一个版本 v$current）" -ForegroundColor Cyan
$text = $text.Replace("private const string AppVersion = `"$current`";", "private const string AppVersion = `"$Version`";")
[System.IO.File]::WriteAllText($appFile, $text)

Write-Host '==> 构建单文件 exe' -ForegroundColor Cyan
& $dotnet publish src/Aurora.App/Aurora.App.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist | Out-Host
if ($LASTEXITCODE -ne 0) { throw '构建失败' }

$asset = "MEME-Launcher-v$Version.exe"
Copy-Item 'dist/MemeLauncher.exe' $asset -Force
try { Copy-Item 'dist/MemeLauncher.exe' 'MEME启动器.exe' -Force -ErrorAction Stop }
catch { Write-Warning '启动器正在运行，跳过覆盖 MEME启动器.exe（关掉再跑一次就好）' }

$sha = (Get-FileHash $asset -Algorithm SHA256).Hash.ToLower()
Set-Content -Path "$asset.sha256.txt" -Value "$sha  $asset" -Encoding ASCII

$notesFile = "notes-v$Version.md"
$noteLines = @(
    "MEME Launcher v$Version",
    "",
    $Notes,
    "",
    "---",
    "",
    "本项目使用 **SignPath Foundation** 提供的免费代码签名服务。",
    "This project uses the free code signing provided by [SignPath Foundation](https://signpath.org/).",
    "",
    "Windows 11 智能应用控制会拦未签名 exe：见 https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md"
)
Set-Content -Path $notesFile -Value $noteLines -Encoding UTF8

if (-not $NoPush) {
    Write-Host '==> 提交 + 打 tag' -ForegroundColor Cyan
    git add -A
    git commit -m "release: v$Version" | Out-Host
    git tag "v$Version"

    Write-Host '==> 推送' -ForegroundColor Cyan
    git push origin main 2>&1 | Out-Host
    git push origin "v$Version" 2>&1 | Out-Host

    Write-Host '==> 发 GitHub Release' -ForegroundColor Cyan
    gh release create "v$Version" $asset "$asset.sha256.txt" --title "MEME Launcher v$Version" --notes-file $notesFile | Out-Host
}

if ($GiteeToken) {
    Write-Host '==> 上传到 Gitee 镜像' -ForegroundColor Cyan
    $guide = $Version -replace '\.', '\\.'
    & (Join-Path $root 'scripts\upload-gitee.ps1') `
        -Token $GiteeToken -Owner $GiteeOwner -Repo $GiteeRepo `
        -Tag "v$Version" -File (Join-Path $root $asset) `
        -Name "MEME Launcher v$Version" -Body $Notes | Out-Host
}
elseif (-not $NoPush) {
    Write-Host '==> 跳过 Gitee（没给 -GiteeToken）' -ForegroundColor Yellow
}

Write-Host ''
Write-Host "完成：v$Version" -ForegroundColor Green
Write-Host "  exe : $asset"
Write-Host "  sha : $sha"
if ($GiteeToken) {
    Write-Host "  gitee: https://gitee.com/$GiteeOwner/$GiteeRepo/releases/tag/v$Version"
}
