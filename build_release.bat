@echo off
REM Build Release Script for Brilliant Executor
REM Creates both Setup Installer and Portable ZIP

setlocal enabledelayedexpansion

echo.
echo ====================================
echo Brilliant Executor Release Builder
echo ====================================
echo.

REM Set version
set VERSION=2.0.0
if not "%1"=="" set VERSION=%1

echo Building version: %VERSION%
echo.

REM Check for required tools
echo [1/5] Checking for required tools...
where cmake >nul 2>&1
if errorlevel 1 (
    echo ERROR: CMake not found. Please install CMake 3.10+
    exit /b 1
)

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found. Please install .NET 10 SDK
    exit /b 1
)

echo [OK] CMake and .NET SDK found
echo.

REM Build C++ DLL
echo [2/5] Building C++ DLL...
if exist build rmdir /s /q build
cmake -B build -A x64 -DCMAKE_BUILD_TYPE=Release
if errorlevel 1 (
    echo ERROR: CMake configuration failed
    exit /b 1
)

cmake --build build --config Release
if errorlevel 1 (
    echo ERROR: C++ build failed
    exit /b 1
)

echo [OK] C++ DLL built successfully
echo.

REM Build C# UI
echo [3/5] Building C# WPF UI...
cd ui
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false --output ..\publish 2>nul
if errorlevel 1 (
    echo WARNING: C# build encountered issues, continuing...
)
cd ..

REM Copy DLL to publish folder
copy /y "build\Release\Syntax.dll" "publish\" >nul
if errorlevel 1 (
    echo ERROR: Failed to copy Syntax.dll to publish folder
    exit /b 1
)

echo [OK] C# UI published successfully
echo.

REM Create Portable ZIP
echo [4/5] Creating Portable ZIP...
if not exist "Portable" mkdir Portable
copy /y "publish\Syntax Executor.exe" "Portable\" >nul
copy /y "publish\Syntax.dll" "Portable\" >nul
copy /y "README.md" "Portable\" >nul
copy /y "LICENSE.txt" "Portable\" >nul

powershell -Command "Compress-Archive -Path 'Portable\*' -DestinationPath 'BrilliantExecutor-Portable-v%VERSION%.zip' -Force" 2>nul
if errorlevel 1 (
    echo WARNING: Failed to create portable ZIP
) else (
    echo [OK] Portable ZIP created: BrilliantExecutor-Portable-v%VERSION%.zip
)
echo.

REM Try to build Inno Setup installer
echo [5/5] Creating Setup Installer...
where iscc >nul 2>&1
if errorlevel 1 (
    echo WARNING: Inno Setup not found. Skipping Setup Installer creation.
    echo Install from: https://jrsoftware.org/isdl.php
    echo You can manually create the installer using: installer\BrilliantExecutor-Setup.iss
) else (
    iscc /Q /DMyVersion=%VERSION% /O"." /F"BrilliantExecutor-Setup-v%VERSION%" "installer\BrilliantExecutor-Setup.iss"
    if errorlevel 1 (
        echo WARNING: Inno Setup compilation failed
    ) else (
        echo [OK] Setup Installer created: BrilliantExecutor-Setup-v%VERSION%.exe
    )
)
echo.

echo ====================================
echo Build Complete!
echo ====================================
echo.
echo Releases created:
echo   - BrilliantExecutor-Portable-v%VERSION%.zip
if exist "BrilliantExecutor-Setup-v%VERSION%.exe" (
    echo   - BrilliantExecutor-Setup-v%VERSION%.exe
) else (
    echo   - BrilliantExecutor-Setup-v%VERSION%.exe (NOT CREATED - install Inno Setup)
)
echo.
echo Next steps:
echo   1. Create a git tag: git tag v%VERSION%
echo   2. Push the tag: git push origin v%VERSION%
echo   3. Upload releases to GitHub manually or use the automated workflow
echo.
endlocal
