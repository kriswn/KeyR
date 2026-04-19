@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   KeyR Automation: Publish New Version
echo ========================================
echo.

set /p VERSION="Enter Version Number: "

if "%VERSION%"=="" (
    echo Error: No version entered.
    pause
    exit /b
)

echo.
echo [1/4] Closing any running KeyR instances...
taskkill /F /IM KeyR.exe /T >nul 2>&1

echo [2/4] Updating Project Metadata (v%VERSION%)...
:: Update .csproj versioning
powershell -Command "$v = '%VERSION%'; $path = 'src/KeyR.csproj'; $xml = [xml](Get-Content $path); $xml.Project.PropertyGroup.AssemblyVersion = $v + '.0'; $xml.Project.PropertyGroup.FileVersion = $v + '.0'; $xml.Save($path);"

:: Update MainWindow.xaml floating version label
:: This looks for the vX.X.X pattern in the Text attribute
powershell -Command "$v = 'v%VERSION%'; $path = 'src/MainWindow.xaml'; $content = Get-Content $path; $content -replace 'Text=\"v[0-9\.]+\"', ('Text=\"' + $v + '\"') | Set-Content $path;"

echo [3/4] Compiling and Publishing Binary...
dotnet publish src/KeyR.csproj -c Release -p:PublishSingleFile=true -p:SelfContained=false -r win-x64 --output website/downloads/temp_build >nul

if not exist "website\downloads\temp_build\KeyR.exe" (
    echo.
    echo ERROR: Build failed. Check your XAML or code for errors.
    pause
    exit /b
)

echo [4/4] Moving Binary to Downloads folder...
set FINAL_NAME=KeyR_v%VERSION%_win_x64.exe
if exist "website\downloads\!FINAL_NAME!" del /f /q "website\downloads\!FINAL_NAME!"
move /y "website\downloads\temp_build\KeyR.exe" "website\downloads\!FINAL_NAME!" >nul

echo.
echo ========================================
echo   SUCCESS: Published as !FINAL_NAME!
echo ========================================
echo.

:: Cleanup
rd /s /q "website\downloads\temp_build"

pause
