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
echo [1/6] Checking for required tools...
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

REM Clean build directory
echo [2/6] Cleaning previous builds...
if exist build rmdir /s /q build >nul 2>&1
if exist publish_ui rmdir /s /q publish_ui >nul 2>&1
if exist release_files rmdir /s /q release_files >nul 2>&1
echo [OK] Clean complete
echo.

REM Build C++ DLL
echo [3/6] Building C++ DLL (Syntax.dll)...
cmake -B build -A x64 -DCMAKE_BUILD_TYPE=Release >nul 2>&1
if errorlevel 1 (
    echo ERROR: CMake configuration failed
    exit /b 1
)

cmake --build build --config Release >nul 2>&1
if errorlevel 1 (
    echo ERROR: C++ build failed
    exit /b 1
)

if not exist "build\Release\Syntax.dll" (
    echo ERROR: Syntax.dll not found after build
    exit /b 1
)
echo [OK] Syntax.dll built successfully
echo.

REM Build C# UI
echo [4/6] Building C# UI (Syntax Executor.exe)...
cd ui
dotnet restore >nul 2>&1
if errorlevel 1 (
    echo ERROR: dotnet restore failed
    cd ..
    exit /b 1
)

dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o ..\publish_ui >nul 2>&1
if errorlevel 1 (
    echo ERROR: C# build failed
    cd ..
    exit /b 1
)

if not exist "..\publish_ui\Syntax Executor.exe" (
    echo ERROR: Syntax Executor.exe not found after build
    cd ..
    exit /b 1
)
cd ..
echo [OK] Syntax Executor.exe built successfully
echo.

REM Prepare release files
echo [5/6] Preparing release files...
if not exist release_files mkdir release_files

REM Copy UI executable
copy /y "publish_ui\Syntax Executor.exe" "release_files\" >nul 2>&1
if errorlevel 1 (
    echo ERROR: Failed to copy Syntax Executor.exe
    exit /b 1
)

REM Copy C++ DLL
copy /y "build\Release\Syntax.dll" "release_files\" >nul 2>&1
if errorlevel 1 (
    echo ERROR: Failed to copy Syntax.dll
    exit /b 1
)

REM Copy documentation
copy /y "README.md" "release_files\" >nul 2>&1
copy /y "LICENSE.txt" "release_files\" >nul 2>&1

echo [OK] All release files prepared
echo   - Syntax Executor.exe
echo   - Syntax.dll
echo   - README.md
echo   - LICENSE.txt
echo.

REM Create Portable ZIP
echo [6/6] Creating installers...

powershell -Command "Compress-Archive -Path 'release_files\*' -DestinationPath 'BrilliantExecutor-Portable-v%VERSION%.zip' -Force" 2>nul
if errorlevel 1 (
    echo WARNING: Failed to create portable ZIP
) else (
    echo [OK] Portable ZIP created: BrilliantExecutor-Portable-v%VERSION%.zip
)

REM Try to build Inno Setup installer
where iscc >nul 2>&1
if errorlevel 1 (
    echo [WARNING] Inno Setup not found. Skipping Setup Installer creation.
    echo [INFO] Install from: https://jrsoftware.org/isdl.php
    echo [INFO] Or manually create using: installer\BrilliantExecutor-Setup.iss
) else (
    iscc /Q /DMyVersion=%VERSION% /O"." /F"BrilliantExecutor-Setup-v%VERSION%" "installer\BrilliantExecutor-Setup.iss" >nul 2>&1
    if errorlevel 1 (
        echo [WARNING] Inno Setup compilation failed
    ) else (
        echo [OK] Setup Installer created: BrilliantExecutor-Setup-v%VERSION%.exe
    )
)
echo.

echo ====================================
echo Build Complete!
echo ====================================
echo.
echo Release files created:
if exist "BrilliantExecutor-Portable-v%VERSION%.zip" (
    echo   [OK] BrilliantExecutor-Portable-v%VERSION%.zip
) else (
    echo   [FAILED] BrilliantExecutor-Portable-v%VERSION%.zip
)
if exist "BrilliantExecutor-Setup-v%VERSION%.exe" (
    echo   [OK] BrilliantExecutor-Setup-v%VERSION%.exe
) else (
    echo   [FAILED] BrilliantExecutor-Setup-v%VERSION%.exe (install Inno Setup)
)
echo.
echo Next steps:
echo   1. Test the installers
echo   2. Create git tag: git tag v%VERSION%
echo   3. Push tag: git push origin v%VERSION%
echo   4. Upload to GitHub Releases
echo.
endlocal
