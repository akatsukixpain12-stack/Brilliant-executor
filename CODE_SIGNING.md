# Code Signing Guide - Brilliant Executor

This guide explains how to sign the Brilliant Executor executable to eliminate the "Unknown Publisher" warning from Windows Defender and antivirus software.

## Why Code Signing is Important

When Windows shows "Unknown Publisher" or "Windows protected your PC", it's because the executable lacks a digital signature. Code signing:
- Verifies the publisher identity (Brilliant Team)
- Ensures the executable hasn't been tampered with
- Reduces false positives from antivirus software
- Improves user trust and security

## Quick Start

### Step 1: Create a Certificate

Run the certificate generation script (requires Administrator privileges):

```batch
create_certificate.bat
```

This will:
- Create a self-signed code signing certificate for "Brilliant Team"
- Save it as `brilliant_cert.pfx` in the project root
- Set a password (default: `Brilliant2026!`)
- Make it valid for 5 years

**Note:** For production use, obtain a certificate from a trusted Certificate Authority (CA) like:
- DigiCert
- Sectigo
- GlobalSign
- Comodo

### Step 2: Sign the Executable

After building the project with `build_all.bat`, sign the executable:

```batch
sign_executable.bat brilliant_cert.pfx Brilliant2026! "publish\Syntax Executor.exe"
```

### Step 3: Verify the Signature

Verify the signature was applied correctly:

```batch
signtool verify /pa "publish\Syntax Executor.exe"
```

## Automated Signing During Build

The `build_all.bat` script automatically signs the executable if `brilliant_cert.pfx` exists in the project root. No manual intervention needed!

## Certificate Management

### Installing the Certificate for Testing

To test without SmartScreen warnings, install the certificate to the Trusted Root store:

```batch
powershell -Command "$cert = Import-PfxCertificate -FilePath 'brilliant_cert.pfx' -CertStoreLocation 'Cert:\LocalMachine\Root' -Password (ConvertTo-SecureString -String 'Brilliant2026!' -AsPlainText -Force)"
```

**Warning:** Only install self-signed certificates to Trusted Root for testing. Remove them after testing.

### Exporting the Certificate

To backup or transfer the certificate:

```batch
powershell -Command "$cert = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object {$_.Subject -like '*Brilliant Team*'}; Export-PfxCertificate -Cert $cert -FilePath 'backup_cert.pfx' -Password (ConvertTo-SecureString -String 'Brilliant2026!' -AsPlainText -Force)"
```

### Removing the Certificate

To remove the certificate from the system:

```batch
powershell -Command "Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object {$_.Subject -like '*Brilliant Team*'} | Remove-Item"
```

## Using a Commercial Certificate

For production releases, use a certificate from a trusted CA:

1. Purchase a code signing certificate from a CA
2. Export it as a `.pfx` file with your private key
3. Rename it to `brilliant_cert.pfx` or update the build scripts
4. Update the password in the scripts
5. Build and sign as normal

## Troubleshooting

### "signtool.exe not found"

Install Windows SDK or Visual Studio Build Tools:
- Download from: https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/
- Or install Visual Studio with "Desktop development with C++" workload

### "Certificate doesn't have code signing EKU"

The certificate must have the Code Signing Enhanced Key Usage (EKU). The `create_certificate.bat` script handles this automatically.

### "Invalid certificate or password"

- Verify the `.pfx` file path is correct
- Check the password matches (default: `Brilliant2026!`)
- Ensure the certificate hasn't expired

### Antivirus Still Flagging

Even with signing, some antivirus software may flag the executable because:
- It's a game executor/hack tool
- It uses memory manipulation techniques
- It's not widely distributed yet

Solutions:
- Submit the executable to antivirus vendors for whitelisting
- Use a commercial EV (Extended Validation) certificate
- Build reputation over time with more users

## Security Best Practices

1. **Keep the certificate secure:** Never commit `brilliant_cert.pfx` to version control
2. **Use strong passwords:** The default password is for testing only
3. **Rotate certificates:** Renew before expiration (currently set to 5 years)
4. **Use timestamping:** The signing script uses timestamp servers to validate signatures after certificate expiration
5. **Backup certificates:** Store backups in secure locations

## Files in This Guide

- `create_certificate.bat` - Generates a self-signed code signing certificate
- `sign_executable.bat` - Signs an executable with a certificate
- `build_all.bat` - Main build script (includes automatic signing)
- `app.manifest` - Application manifest with publisher information
- `RblxExecutorUI.csproj` - Project file with assembly attributes

## Additional Resources

- [Microsoft Code Signing Documentation](https://docs.microsoft.com/en-us/windows-hardware/drivers/install/code-signing)
- [Windows SDK Documentation](https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- [Inno Setup Documentation](https://jrsoftware.org/isinfo.php)

## Support

For issues with code signing:
1. Check this guide first
2. Verify all prerequisites are installed
3. Run scripts as Administrator
4. Check Windows Event Viewer for detailed error messages

---

**Publisher:** Brilliant Team  
**Product:** Brilliant Executor  
**Version:** 2.0.0.0