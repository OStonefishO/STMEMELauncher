param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$GiteeToken,
    [string]$GiteeOwner = "stonefish8",
    [string]$GiteeRepo = "STMEMELauncher",
    [string]$NotesFile = "",
    [switch]$SkipGithub,
    [switch]$SkipGitee,
    [int]$KeepReleaseCount = 4
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

$notes = if ($NotesFile -and (Test-Path $NotesFile)) { Get-Content $NotesFile -Raw -Encoding UTF8 } else { "MEME Launcher $tag" }

if (-not $SkipGithub) {
    Write-Host "==> GitHub Release" -ForegroundColor Cyan
    $assets = @($asset) + @(Get-ChildItem "docs\screens\p*.png" | ForEach-Object { $_.FullName })
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
