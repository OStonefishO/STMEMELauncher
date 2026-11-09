param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipApp
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# 优先用仓库里自带的 SDK，找不到就退回系统 dotnet
$dotnet = 'D:\综合创作\_env\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw '找不到 dotnet，请安装 .NET SDK，或用 -Dotnet 指定路径'
    }
    $dotnet = $cmd.Source
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

function Build-Project([string]$project) {
    Write-Host "==> 构建 $project ($Configuration)" -ForegroundColor Cyan
    & $dotnet build $project -c $Configuration -v m --nologo -nowarn:NETSDK1138
    if ($LASTEXITCODE -ne 0) {
        throw "构建失败: $project"
    }
}

Build-Project (Join-Path $root 'src\Aurora.Core\Aurora.Core.csproj')
Build-Project (Join-Path $root 'src\Aurora.Cli\Aurora.Cli.csproj')
if (-not $SkipApp) {
    Build-Project (Join-Path $root 'src\Aurora.App\Aurora.App.csproj')
}

$dist = Join-Path $root 'dist'
if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
}
New-Item -ItemType Directory -Path $dist | Out-Null

$cliOut = Join-Path $root "src\Aurora.Cli\bin\$Configuration\net6.0"
Copy-Item $cliOut (Join-Path $dist 'cli') -Recurse

if (-not $SkipApp) {
    $appOut = Join-Path $root "src\Aurora.App\bin\$Configuration\net6.0-windows"
    Copy-Item $appOut (Join-Path $dist 'app') -Recurse
}

Write-Host ''
Write-Host '完成，输出在 dist\' -ForegroundColor Green
Write-Host ('  启动器 : ' + (Join-Path $dist 'app\Aurora.App.exe'))
Write-Host ('  CLI    : ' + (Join-Path $dist 'cli\aurora.exe'))
