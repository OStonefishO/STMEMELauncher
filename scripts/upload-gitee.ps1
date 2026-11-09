param(
    [Parameter(Mandatory = $true)][string]$Token,
    [Parameter(Mandatory = $true)][string]$Owner,
    [Parameter(Mandatory = $true)][string]$Repo,
    [Parameter(Mandatory = $true)][string]$Tag,
    [Parameter(Mandatory = $true)][string]$File,
    [string]$Name = "",
    [string]$Body = "MEME Launcher 国内镜像",
    [string]$Branch = "",
    [switch]$CreateRepo
)

# 把构建产物传到 Gitee Releases，作为国内备用下载源。
#
# 令牌：https://gitee.com/profile/personal_access_tokens  勾选 projects 权限
#
# 用法：
#   powershell -File scripts/upload-gitee.ps1 -Token <令牌> -Owner stonefish8 -Repo STMEMELauncher `
#     -Tag v0.5.7 -File .\MEME-Launcher-v0.5.7.exe -CreateRepo

if (-not (Test-Path $File)) {
    Write-Error "找不到文件: $File"
    exit 1
}

$fileSize = (Get-Item $File).Length
if ($fileSize -gt 100MB) {
    Write-Error ("文件 {0:N1} MB 超过 Gitee 附件 100MB 限制" -f ($fileSize / 1MB))
    exit 1
}

if ([string]::IsNullOrWhiteSpace($Name)) {
    $Name = "MEME Launcher $Tag"
}

$root = "https://gitee.com/api/v5"
$api = "$root/repos/$Owner/$Repo"

# 1. 仓库不存在就先建（带初始提交，否则建不了 Release）
$check = & curl.exe -s --max-time 20 "$api" -G -d "access_token=$Token"
$repoInfo = $null
try { $repoInfo = $check | ConvertFrom-Json } catch { }

if (-not $repoInfo.full_name) {
    if (-not $CreateRepo) {
        Write-Error "仓库 $Owner/$Repo 不存在或不公开。加 -CreateRepo 让我自动建一个。"
        exit 1
    }

    Write-Host "仓库不存在，创建中 ..."
    $created = & curl.exe -s -X POST "$root/user/repos" `
        -d "access_token=$Token" `
        -d "name=$Repo" `
        -d "description=MEME Launcher 国内下载镜像" `
        -d "private=false" `
        -d "auto_init=true"
    $repoInfo = $created | ConvertFrom-Json
    if (-not $repoInfo.full_name) {
        Write-Error "创建仓库失败: $created"
        exit 1
    }

    Write-Host "仓库已创建: $($repoInfo.full_name)"
    Start-Sleep -Seconds 3
}

if ([string]::IsNullOrWhiteSpace($Branch)) {
    $Branch = if ($repoInfo.default_branch) { $repoInfo.default_branch } else { "master" }
}

# 2. 建 Release
Write-Host "创建 Release $Tag（分支 $Branch）..."
$bodyFile = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($bodyFile, $Body, (New-Object System.Text.UTF8Encoding($false)))

$create = & curl.exe -s -X POST "$api/releases" `
    -d "access_token=$Token" `
    -d "tag_name=$Tag" `
    -d "name=$Name" `
    -d "target_commitish=$Branch" `
    --data-urlencode "body@$bodyFile"

$release = $null
try { $release = $create | ConvertFrom-Json } catch { }

if (-not $release.id) {
    # 可能已经存在同名 Release，找出来复用
    $list = & curl.exe -s --max-time 20 "$api/releases" -G -d "access_token=$Token"
    $existing = ($list | ConvertFrom-Json) | Where-Object { $_.tag_name -eq $Tag } | Select-Object -First 1
    if ($existing) {
        Write-Host "Release 已存在，复用 id=$($existing.id)"
        $release = $existing
    } else {
        Write-Error "创建 Release 失败: $create"
        exit 1
    }
}

# 3. 上传附件
Write-Host "上传附件（$([math]::Round($fileSize/1MB,1)) MB）..."
$upload = & curl.exe -s -X POST "$api/releases/$($release.id)/attach_files" `
    -F "access_token=$Token" `
    -F "file=@$File"

$info = $null
try { $info = $upload | ConvertFrom-Json } catch { }

if ($info.browser_download_url) {
    Write-Host ""
    Write-Host "上传成功，国内下载地址：" -ForegroundColor Green
    Write-Host $info.browser_download_url
    $info.browser_download_url | Set-Content -Path "$env:TEMP\gitee_download_url.txt" -Encoding UTF8
} else {
    Write-Error "上传失败: $upload"
    exit 1
}
