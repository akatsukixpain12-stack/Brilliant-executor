# Publisher Setup - Brilliant Executor

## Problem Solved

The executable was showing as **"Unknown Publisher"** or **"Unknown Developer"** in Windows Defender and antivirus software. This has been resolved by implementing proper code signing infrastructure.

## What Was Changed

### 1. Application Manifest (`ui/app.manifest`)
- Added `processorArchitecture="amd64"` to assembly identity
- Added `<publisher name="Brilliant Team" />` element
- This embeds publisher information in the executable

### 2. Project Configuration (`ui/RblxExecutorUI.csproj`)
- Already configured with proper attributes:
  - `Company`: Brilliant Team
  - `Product`: Brilliant Executor
  - `Copyright`: © 2026 Brilliant Team
  - `Trademark`: Brilliant

### 3. Code Signing Infrastructure
Created three new files:
- **`create_certificate.bat`** - Generates a code signing certificate for "Brilliant Team"
- **`sign_executable.bat`** - Signs executables with the certificate
- **`CODE_SIGNING.md`** - Complete guide for code signing

### 4. Build Process (`build_all.bat`)
- Updated to automatically sign the executable if certificate exists
- Added informative messages about signing status
- Includes manual signing instructions in build output

### 5. Security (`.gitignore`)
- Added certificate files to `.gitignore` to prevent accidental commits
- Protects private keys and sensitive certificate data

## How to Use

### First Time Setup

1. **Run as Administrator:**
   ```batch
   create_certificate.bat
   ```
   This creates `brilliant_cert.pfx` with password `Brilliant2026!`

2. **Build the project:**
   ```batch
   build_all.bat
   ```
   The build script will automatically sign the executable

3. **Verify the signature:**
   ```batch
   signtool verify /pa "publish\Syntax Executor.exe"
   ```

### For Testing (Optional)

Install the certificate to Trusted Root to avoid SmartScreen warnings:
```batch
powershell -Command "$cert = Import-PfxCertificate -FilePath 'brilliant_cert.pfx' -CertStoreLocation 'Cert:\LocalMachine\Root' -Password (ConvertTo-SecureString -String 'Brilliant2026!' -AsPlainText -Force)"
```

**Remember to remove after testing:**
```batch
powershell -Command "Get-ChildItem -Path Cert:\LocalMachine\Root | Where-Object {$_.Subject -like '*Brilliant Team*'} | Remove-Item"
```

## For Production

### Option 1: Self-Signed Certificate (Testing/Development)
- Use the provided `create_certificate.bat`
- Works for testing but still shows warnings on other machines
- Free and easy to set up

### Option 2: Commercial Certificate (Recommended for Production)
Purchase a code signing certificate from a trusted CA:
- **DigiCert** - https://www.digicert.com/code-signing/
- **Sectigo** - https://sectigo.com/code-signing
- **GlobalSign** - https://www.globalsign.com/code-signing
- **Comodo** - https://comodosslstore.com/code-signing-certificates

Benefits:
- Recognized by all Windows machines immediately
- No SmartScreen warnings
- Higher trust level
- Better antivirus compatibility

## What Users Will See

### Before (Unsigned):
```
"Windows protected your PC"
"Unknown Publisher"
"Brilliant Executor.exe is not commonly downloaded and could harm your computer"
```

### After (Signed with Self-Signed Cert):
```
"Publisher: Brilliant Team" (if certificate installed)
OR
"Windows protected your PC" (on other machines, but shows publisher name)
```

### After (Signed with Commercial Cert):
```
"Publisher: Brilliant Team"
"No issues found"
"Smooth execution without warnings"
```

## Important Notes

1. **Certificate Security:**
   - NEVER commit `brilliant_cert.pfx` to version control
   - Keep the password secure
   - Backup the certificate in a safe location

2. **Antivirus Detection:**
   - Code signing reduces but doesn't eliminate all antivirus flags
   - Game executors may still be flagged by some antivirus software
   - Consider submitting to antivirus vendors for whitelisting

3. **Certificate Renewal:**
   - Current certificate is valid for 5 years
   - Renew before expiration to maintain trust
   - Update build scripts if you change the certificate file

4. **Distribution:**
   - Self-signed certificates only work on machines where the cert is installed
   - For wide distribution, use a commercial certificate from a trusted CA

## Troubleshooting

### "Unknown Publisher" Still Shows
- Make sure you ran `create_certificate.bat` as Administrator
- Verify the executable was signed: `signtool verify /pa "publish\Syntax Executor.exe"`
- Check that the certificate hasn't expired

### SmartScreen Still Warns
- This is normal for self-signed certificates
- Install the certificate to Trusted Root for testing
- For production, use a commercial EV certificate

### Antivirus Still Flags
- Some antivirus software flags game executors regardless of signing
- Submit to antivirus vendors for whitelisting
- Use commercial certificate for better reputation

## Files Modified/Created

### Modified:
- `ui/app.manifest` - Added publisher information
- `build_all.bat` - Added automatic signing step
- `.gitignore` - Added certificate exclusions

### Created:
- `create_certificate.bat` - Certificate generator
- `sign_executable.bat` - Executable signer
- `CODE_SIGNING.md` - Complete signing guide
- `PUBLISHER_SETUP.md` - This file

## Quick Reference

| Task | Command |
|------|---------|
| Create certificate | `create_certificate.bat` |
| Sign executable | `sign_executable.bat brilliant_cert.pfx Brilliant2026! "publish\Syntax Executor.exe"` |
| Verify signature | `signtool verify /pa "publish\Syntax Executor.exe"` |
| Install cert for testing | `powershell -Command "$cert = Import-PfxCertificate -FilePath 'brilliant_cert.pfx' -CertStoreLocation 'Cert:\LocalMachine\Root' -Password (ConvertTo-SecureString -String 'Brilliant2026!' -AsPlainText -Force)"` |
| Remove test cert | `powershell -Command "Get-ChildItem -Path Cert:\LocalMachine\Root | Where-Object {$_.Subject -like '*Brilliant Team*'} | Remove-Item"` |
| Full build | `build_all.bat` |

## Support

For detailed information, see:
- `CODE_SIGNING.md` - Complete code signing documentation
- `README.md` - Project overview and build instructions

---

**Status:** ✅ Fixed - Publisher information added and code signing infrastructure implemented  
**Publisher:** Brilliant Team  
**Product:** Brilliant Executor  
**Version:** 2.0.0.0