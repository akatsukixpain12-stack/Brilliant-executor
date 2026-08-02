@echo off
REM ============================================================================
REM Brilliant Executor - Full Build Script
REM ============================================================================
setlocal enabledelayedexpansion

echo ============================================================
echo   Brilliant Executor Build Script v2.0
echo ============================================================
echo.

REM Check for CMake
where cmake >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] CMake not found! Install it from: https://cmake.org/download/
    exit /b 1
)

REM Check for dotnet
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK not found! Install it from: https://dotnet.microsoft.com/download
    exit /b 1
)

REM ============================================================================
REM Step 1: Build the C++ native DLL
REM ============================================================================
echo.
echo [1/4] Building C++ native DLL (Syntax.dll)...
echo.

if not exist "build" (
    echo   Configuring CMake...
    cmake -B build -A x64
    if !errorlevel! neq 0 (
        echo [ERROR] CMake configuration failed!
        exit /b 1
    )
)

echo   Building with CMake...
cmake --build build --config Release --parallel
if %errorlevel% neq 0 (
    echo [ERROR] C++ build failed!
    exit /b 1
)

REM Check if DLL was built
if exist "build\Release\Syntax.dll" (
    echo   [+] Syntax.dll built successfully!
) else (
    if exist "build\Release\SyntaxAPI.dll" (
        echo   [+] SyntaxAPI.dll found, renaming to Syntax.dll...
        copy /y "build\Release\SyntaxAPI.dll" "build\Release\Syntax.dll" >nul
    ) else (
        echo [ERROR] Syntax.dll was not built!
        exit /b 1
    )
)

REM ============================================================================
REM Step 2: Build the C# UI
REM ============================================================================
echo.
echo [2/4] Building C# UI...
echo.

pushd ui
echo   Restoring packages...
dotnet restore
if %errorlevel% neq 0 (
    echo [ERROR] dotnet restore failed!
    popd
    exit /b 1
)

echo   Building UI...
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
if %errorlevel% neq 0 (
    echo [ERROR] UI build failed!
    popd
    exit /b 1
)
popd

REM ============================================================================
REM Step 3: Sign the executable (if certificate exists)
REM ============================================================================
echo.
echo [3/4] Code signing...
echo.

if exist "brilliant_cert.pfx" (
    echo   [+] Certificate found, signing executable...
    call sign_executable.bat "brilliant_cert.pfx" "Brilliant2026!" "ui\bin\Release\net10.0-windows\win-x64\publish\Syntax Executor.exe" >nul 2>&1
    if exist "ui\bin\Release\net10.0-windows\win-x64\publish\Syntax Executor.exe" (
        call sign_executable.bat "brilliant_cert.pfx" "Brilliant2026!" "ui\bin\Release\net10.0-windows\win-x64\publish\Syntax Executor.exe" >nul 2>&1
    )
    if exist "ui\bin\x64\Release\net10.0-windows\Syntax Executor.exe" (
        call sign_executable.bat "brilliant_cert.pfx" "Brilliant2026!" "ui\bin\x64\Release\net10.0-windows\Syntax Executor.exe" >nul 2>&1
    )
    echo   [+] Executable signed with Brilliant Team certificate!
) else (
    echo   [INFO] No certificate found (brilliant_cert.pfx)
    echo   [INFO] To sign the executable, run create_certificate.bat first
    echo   [INFO] The executable will show as 'Unknown Publisher' without signing
)

REM ============================================================================
REM Step 4: Prepare publish folder
REM ============================================================================
echo.
echo [4/4] Preparing publish folder...
echo.

if not exist "publish" mkdir "publish"
if not exist "publish\Scripts" mkdir "publish\Scripts"

REM Copy the UI executable
if exist "ui\bin\Release\net10.0-windows\win-x64\publish\Syntax Executor.exe" (
    copy /y "ui\bin\Release\net10.0-windows\win-x64\publish\Syntax Executor.exe" "publish\" >nul
) else (
    if exist "ui\bin\x64\Release\net10.0-windows\Syntax Executor.exe" (
        copy /y "ui\bin\x64\Release\net10.0-windows\Syntax Executor.exe" "publish\" >nul
    ) else (
        echo [WARNING] Could not find UI executable, checking for existing...
        if not exist "publish\Syntax Executor.exe" (
            echo [ERROR] UI executable not found!
            exit /b 1
        )
    )
)

REM Copy the native DLL
copy /y "build\Release\Syntax.dll" "publish\Syntax.dll" >nul 2>&1

REM Copy Lua.xshd
if exist "ui\Lua.xshd" copy /y "ui\Lua.xshd" "publish\Lua.xshd" >nul 2>&1

REM Copy example script
if not exist "publish\Scripts\Example.lua" (
    echo -- Brilliant Executor Example Script > "publish\Scripts\Example.lua"
    echo print("Hello from Brilliant Executor!") >> "publish\Scripts\Example.lua"
)

echo   [+] Publish folder ready!

REM ============================================================================
REM Step 5: Build installer (if Inno Setup is available)
REM ============================================================================
echo.
echo [5/5] Building installer...
echo.

where iscc >nul 2>&1
if %errorlevel% neq 0 (
    echo   [SKIP] Inno Setup not found. Install from: https://jrsoftware.org/isinfo.php
    echo   Installer script is at: installer\BrilliantExecutor.iss
) else (
    echo   Building installer...
    iscc installer\BrilliantExecutor.iss
    if %errorlevel% neq 0 (
        echo   [WARNING] Installer build failed
    ) else (
        echo   [+] Installer built successfully!
    )
)

echo.
echo ============================================================
echo   Build Complete!
echo   UI:  publish\Syntax Executor.exe
echo   DLL: publish\Syntax.dll
echo.
echo To sign the executable manually:
echo   1. Run: create_certificate.bat
echo   2. Run: sign_executable.bat brilliant_cert.pfx Brilliant2026! "publish\Syntax Executor.exe"
echo ============================================================
echo.
pause