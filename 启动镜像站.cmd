@echo off
chcp 65001 >nul
title MEME Mirror

cd /d "%~dp0"

set "EXE=src\Meme.Mirror\bin\Release\net6.0\MemeMirror.exe"
set "DOTNET=D:\综合创作\_env\dotnet\dotnet.exe"

if not exist "%EXE%" (
    echo   还没编译过镜像站，先编译...
    if exist "%DOTNET%" (
        "%DOTNET%" build src\Meme.Mirror\Meme.Mirror.csproj -c Release -v q --nologo
    ) else (
        dotnet build src\Meme.Mirror\Meme.Mirror.csproj -c Release -v q --nologo
    )
)

if not exist "%EXE%" (
    echo   编译失败，检查一下上面的报错。
    pause
    exit /b 1
)

echo.
echo   正在启动 MEME Mirror（默认 8123 端口，会自动打开浏览器）
echo   关掉这个窗口就等于关掉镜像站。
echo.

"%EXE%" --port 8123
pause
