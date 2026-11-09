@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion
title Aurora Launcher

set "ROOT=%~dp0"
set "DOTNET=%ROOT%..\_env\dotnet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"
set "EXE=%ROOT%src\Aurora.App\bin\Release\net6.0-windows\Aurora.App.exe"

echo.
echo   Aurora Launcher
echo   ---------------------------------------------
echo   正在编译（第一次大约 5 秒，之后更快）...
echo.

"%DOTNET%" build "%ROOT%src\Aurora.App\Aurora.App.csproj" -c Release -v q --nologo -nowarn:NETSDK1138

if errorlevel 1 goto buildfail
if not exist "%EXE%" goto noexe

echo   编译完成，正在打开启动器...
start "" "%EXE%"
exit /b 0

:noexe
echo   编译似乎成功了，但没找到：
echo   %EXE%
echo   请把这句话发给我。
pause
exit /b 1

:buildfail
echo.
echo   × 编译失败（错误信息在上面）
echo.
if exist "%EXE%" (
    echo   先打开上一次编译好的版本，你可以先看看界面。
    start "" "%EXE%"
)
echo   把上面的报错发给我，我来修。
pause
exit /b 1
