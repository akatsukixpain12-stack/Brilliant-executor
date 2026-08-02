@echo off
chcp 65001 >nul
echo ========================================
echo   Brilliant Executor - Code Signing Tool
echo ========================================
echo.

REM Check if signtool is available
where signtool >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: signtool.exe not found!
    echo.
    echo Please install Windows SDK or Visual Studio Build Tools
    echo Download from: https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/
    echo.
    pause
    exit /b 1
)

echo [✓] signtool.exe found
echo.

REM Configuration
set "CERT_PATH=%~1"
set "CERT_PASSWORD=%~2"
set "EXE_PATH=%~3"

if "%~1"=="" (
    echo Usage: sign_executable.bat ^<certificate.pfx^> ^<password^> ^<executable.exe^>
    echo.
    echo Example: sign_executable.bat brilliant_cert.pfx MyPassword "publish\brilliant Executor.exe"
    echo.
    echo To create a self-signed certificate (for testing):
    echo   powershell -Command "New-SelfSignedCertificate -Type Custom -Subject 'CN=Brilliant Team' -KeyUsage DigitalSignature -FriendlyName 'Brilliant Team' -CertStoreLocation 'Cert:\LocalMachine\My'"
    echo.
    pause
    exit /b 1
)

if not exist "%EXE_PATH%" (
    echo ERROR: Executable not found: %EXE_PATH%
    pause
    exit /b 1
)

echo Signing executable: %EXE_PATH%
echo Publisher: Brilliant Team
echo.

REM Sign the executable
signtool sign /f "%CERT_PATH%" /p "%CERT_PASSWORD%" /tr http://timestamp.digicert.com /td sha256 /fd sha256 "%EXE_PATH%"

if %errorlevel% equ 0 (
    echo.
    echo [✓] Successfully signed: %EXE_PATH%
    echo.
    echo Verifying signature...
    signtool verify /pa "%EXE_PATH%"
    echo.
    echo ========================================
    echo   Signing Complete!
    echo ========================================
) else (
    echo.
    echo [✗] Signing failed!
    echo.
    echo Common issues:
    echo   - Invalid certificate or password
    echo   - Certificate doesn't have code signing EKU
    echo   - Certificate has expired
    echo.
)

pause