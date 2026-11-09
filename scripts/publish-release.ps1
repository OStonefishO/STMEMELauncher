param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$GiteeToken,
    [string]$GiteeOwner = "stonefish8",
    [string]$GiteeRepo = "STMEMELauncher",
    [string]$NotesFile = "",
    [switch]$SkipGithub,
    [switch]$SkipGitee,
    [int]$KeepReleaseCount = 4,
    # 代码签名（可选）：给了 pfx 才签，没给就跳过，行为不变
    [string]$SignPfx = "",
    [string]$SignPassword = "",
    [string]$SignTimestamp = "http://timestamp.digicert.com"
)

# 一条命令发版：构建 → 发布单文件 exe → GitHub Release → Gitee Release
# 用法：
#   powershell -File scripts/publish-release.ps1 -Version 0.6.1 -GiteeToken <令牌> -NotesFile notes.md

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$tag = if ($Version.StartsWith("v")) { $Version } else { "v$Version" }
$plain = $tag.TrimStart('v')
$asset = "MEME-Launcher-$tag.exe"

Write-Host "==> 构建 $tag" -ForegroundColor Cyan
dotnet publish src/Aurora.App/Aurora.App.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist

Copy-Item "dist\MemeLauncher.exe" $asset -Force
Copy-Item "dist\MemeLauncher.exe" "MEME启动器.exe" -Force -ErrorAction SilentlyContinue

# ---------- 代码签名（可选）----------
if ($SignPfx) {
    $signtool = (Get-Command signtool.exe -ErrorAction SilentlyContinue).Source
    if (-not $signtool) {
        $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match 'x64' } | Select-Object -Last 1 -ExpandProperty FullName
    }

    if (-not $signtool) {
        throw "找不到 signtool.exe，装一下 Windows SDK（或者把 signtool 加到 PATH）"
    }

    Write-Host "==> 代码签名 $asset" -ForegroundColor Cyan
    & $signtool sign /fd SHA256 /f "$SignPfx" /p "$SignPassword" /tr "$SignTimestamp" /td SHA256 "$asset"
    if ($LASTEXITCODE -ne 0) { throw "签名失败" }
    Copy-Item $asset "MEME启动器.exe" -Force -ErrorAction SilentlyContinue
    & $signtool verify /pa "$asset"
}
else {
    Write-Host "==> 跳过签名（未给 -SignPfx）。Windows 11 的智能应用控制会拦未签名 exe，见 docs/SMART-APP-CONTROL.md" -ForegroundColor Yellow
}

# ---------- 校验值 + 使用说明（一起发出去，玩家少踩坑）----------
$sha = (Get-FileHash $asset -Algorithm SHA256).Hash.ToLower()
$checksum = "MEME-Launcher-$tag.sha256.txt"
Set-Content -Path $checksum -Value "$sha  $asset" -Encoding ASCII

$guide = "使用说明-$tag.txt"
$guideText = @"
MEME Launcher $tag
========================================

一、怎么用
  双击 MEME 启动器.exe 就能开，不用装 .NET，也不用先配 Java。

二、提示「智能应用控制已阻止可能不安全的应用」？
  这不是病毒，是 Windows 11 的 Smart App Control 不认识未签名的程序。
  解决办法（三选一）：
   1) 关掉它：Windows 安全中心 → 应用和浏览器控制 → 智能应用控制设置 → 关闭
      （注意：关掉以后要重装系统才能再打开，介意就用第 2、3 种）
   2) 换一台没开这个功能的电脑（Windows 10 一般没有这一项）
   3) 等微软审核通过（弹窗里的自动上报）
  详细说明：https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md

三、校验文件有没有下坏
  本文件旁边的 .sha256.txt 里是安装包的 SHA256，可以用：
    certutil -hashfile "MEME 启动器.exe" SHA256
  对比一下，一样就是完整的。

四、更新
  启动器里「设置 → 检查更新」会自动找新版本，国内走 Gitee 镜像。
"@
Set-Content -Path $guide -Value $guideText -Encoding UTF8

$notes = if ($NotesFile -and (Test-Path $NotesFile)) { Get-Content $NotesFile -Raw -Encoding UTF8 } else { "MEME Launcher $tag" }

if (-not $SkipGithub) {
    Write-Host "==> GitHub Release" -ForegroundColor Cyan
    $assets = @($asset, $checksum, $guide) + @(Get-ChildItem "docs\screens\p*.png" | ForEach-Object { $_.FullName })
    gh release create $tag @assets --title "MEME Launcher $tag" --notes $notes
}

if (-not $SkipGitee) {
    Write-Host "==> Gitee Release" -ForegroundColor Cyan
    & "$root\scripts\upload-gitee.ps1" -Token $GiteeToken -Owner $GiteeOwner -Repo $GiteeRepo `
        -Tag $tag -File "$root\$asset" -Name "MEME Launcher $tag" -KeepReleaseCount $KeepReleaseCount
}

Write-Host ""
Write-Host "完成：" -ForegroundColor Green
Write-Host "  GitHub: https://github.com/OStonefishO/STMEMELauncher/releases/tag/$tag"
Write-Host "  Gitee : https://gitee.com/$GiteeOwner/$GiteeRepo/releases/tag/$tag"
