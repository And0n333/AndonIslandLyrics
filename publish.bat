@echo off
chcp 65001 >nul
echo ========================================
echo  AndonIslandLyrics — 发布打包
echo ========================================
echo.

dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -p:DebugSymbols=false

echo.
if %errorlevel% equ 0 (
    echo ✅ 打包成功！
    echo 输出文件：
    echo   bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\AndonIslandLyrics.exe
    echo.
    echo 双击 AndonIslandLyrics.exe 即可运行，无需安装 .NET SDK。
) else (
    echo ❌ 打包失败，请检查错误信息。
)

pause
