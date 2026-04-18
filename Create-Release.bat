@echo off
setlocal
echo =========================================
echo       KeyR Auto-Release Builder
echo =========================================
echo.
set /p tag="Enter the new version name (e.g., v0.4.0): "

IF "%tag%"=="" (
    echo Version cannot be empty.
    pause
    exit /b
)

echo.
echo Adding changes to Git...
git add .
git commit -m "Create Release %tag%"
git tag %tag%

echo.
echo Pushing code and tag to GitHub...
git push -u origin main
git push origin %tag%

echo.
echo =========================================
echo Done! GitHub Actions is now compiling %tag% online.
echo Check your GitHub repository's "Releases" tab in a minute!
echo =========================================
pause
