param(
    [Parameter(Mandatory = $true)][string]$Token,
    [Parameter(Mandatory = $true)][string]$Owner,
    [Parameter(Mandatory = $true)][string]$Repo,
    [Parameter(Mandatory = $true)][string]$Tag,
    [Parameter(Mandatory = $true)][string]$File,
    [string]$Name = "",
    [string]$Body = "MEME Launcher 国内镜像",
    [string]$Branch = "master"
)

# 把构建产物传到 Gitee Releases，作为国内备用下载源。
# 用法：
#   powershell -File scripts/upload-gitee.ps1 -Token <你的令牌> -Owner <用户名> -Repo STMEMELauncher `
#     -Tag v0.5.7 -File .\MEME-Launcher-v0.5.7.exe
# 令牌在 https://gitee.com/profile/personal_access_tokens 生成，勾选 projects 权限即可。

if (-not (Test-Path $File)) {
    Write-Error "找不到文件: $File"
    exit 1
}

if ([string]::IsNullOrWhiteSpace($Name)) {
    $Name = "MEME Launcher $Tag"
}

$api = "https://gitee.com/api/v5/repos/$Owner/$Repo"

Write-Host "创建 Release $Tag ..."
$bodyFile = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($bodyFile, $Body, (New-Object System.Text.UTF8Encoding($false)))

$create = & curl.exe -s -X POST "$api/releases" `
    -d "access_token=$Token" `
    -d "tag_name=$Tag" `
    -d "name=$Name" `
    -d "target_commitish=$Branch" `
    --data-urlencode "body@$bodyFile"

$release = $create | ConvertFrom-Json
if (-not $release.id) {
    Write-Error "创建 Release 失败: $create"
    exit 1
}

Write-Host "Release id = $($release.id)，开始上传附件 ..."
$upload = & curl.exe -s -X POST "$api/releases/$($release.id)/attach_files" `
    -F "access_token=$Token" `
    -F "file=@$File"

Write-Host $upload

$info = $upload | ConvertFrom-Json
if ($info.browser_download_url) {
    Write-Host ""
    Write-Host "上传成功，国内下载地址：" -ForegroundColor Green
    Write-Host $info.browser_download_url
} else {
    Write-Error "上传失败"
    exit 1
}
