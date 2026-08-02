@echo off
chcp 65001 >nul
echo ========================================
echo   Brilliant Executor - Certificate Generator
echo ========================================
echo.

REM Check if PowerShell is available
where powershell >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: PowerShell not found!
    pause
    exit /b 1
)

echo [✓] PowerShell found
echo.

REM Configuration
set "CERT_NAME=Brilliant Team"
set "CERT_PASSWORD=Brilliant2026!"
set "OUTPUT_PATH=brilliant_cert.pfx"
set "VALIDITY_YEARS=5"

echo Creating code signing certificate...
echo Publisher: %CERT_NAME%
echo Validity: %VALIDITY_YEARS% years
echo Output: %OUTPUT_PATH%
echo.

REM Create self-signed certificate for code signing
powershell -Command "$cert = New-SelfSignedCertificate -Type Custom -Subject 'CN=%CERT_NAME%' -KeyUsage DigitalSignature -KeyLength 2048 -HashAlgorithm SHA256 -NotAfter (Get-Date).AddYears(%VALIDITY_YEARS%) -CertStoreLocation 'Cert:\LocalMachine\My' -FriendlyName '%CERT_NAME% Code Signing' -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3'); $pwd = ConvertTo-SecureString -String '%CERT_PASSWORD%' -Force -AsPlainText; Export-PfxCertificate -Cert $cert -FilePath '%OUTPUT_PATH%' -Password $pwd; Write-Host 'Certificate created successfully!'"

if %errorlevel% equ 0 (
    echo.
    echo ========================================
    echo   Certificate Created Successfully!
    echo ========================================
    echo.
    echo Certificate Details:
    echo   Name: %CERT_NAME%
    echo   File: %OUTPUT_PATH%
    echo   Password: %CERT_PASSWORD%
    echo   Valid for: %VALIDITY_YEARS% years
    echo.
    echo IMPORTANT: Keep the certificate and password secure!
    echo.
    echo To sign the executable, run:
    echo   sign_executable.bat %OUTPUT_PATH% %CERT_PASSWORD% "publish\brilliant Executor.exe"
    echo.
    echo To install the certificate to Trusted Root (for testing):
    echo   powershell -Command "$cert = Import-PfxCertificate -FilePath '%OUTPUT_PATH%' -CertStoreLocation 'Cert:\LocalMachine\Root' -Password (ConvertTo-SecureString -String '%CERT_PASSWORD%' -AsPlainText -Force)"
    echo.
) else (
    echo.
    echo [✗] Certificate creation failed!
    echo.
    echo Make sure you're running as Administrator.
    echo.
)

pause