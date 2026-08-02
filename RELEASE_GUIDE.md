# Release Guide - Brilliant Executor

This guide explains how to create releases with two installers (Setup and Portable) for Brilliant Executor.

## Quick Start

### Option 1: Automated GitHub Actions (Recommended)

1. **Create a git tag** for the new version:
```bash
git tag v2.0.1
git push origin v2.0.1
```

2. **GitHub Actions will automatically:**
   - Build the C++ DLL (Syntax.dll)
   - Build the C# WPF UI
   - Create both installers
   - Create a GitHub Release with both files

The workflow is defined in `.github/workflows/build-release.yml`

### Option 2: Manual Build on Local Machine

1. **Ensure you have prerequisites installed:**
   - CMake 3.10+
   - Visual Studio 2022 with C++ workload
   - .NET 10 SDK
   - Inno Setup 6.2+ (for Setup Installer)

2. **Run the release build script:**
```bash
build_release.bat 2.0.1
```

This will:
- Build the C++ DLL
- Build and publish the C# UI
- Create portable ZIP
- Create Setup Installer (if Inno Setup is installed)

3. **Upload the releases to GitHub:**
   - Go to Releases section
   - Create new release
   - Attach both files:
     - `BrilliantExecutor-Portable-v2.0.1.zip`
     - `BrilliantExecutor-Setup-v2.0.1.exe`

## Understanding the Two Installers

### Setup Installer (.exe)
- **Filename:** `BrilliantExecutor-Setup-v2.0.1.exe`
- **Requires:** Windows x64, .NET 10 Runtime
- **Installation:**
  - Creates Start Menu shortcuts
  - Optional Desktop shortcut
  - Adds uninstall option to Control Panel
  - Launches after installation (optional)
  - Full ~50MB disk space

### Portable (.zip)
- **Filename:** `BrilliantExecutor-Portable-v2.0.1.zip`
- **No installation required:**
  - Extract and run immediately
  - No system modifications
  - Portable to USB or network drive
  - ~40MB compressed size

## Installer Configuration

### Setup Installer Settings
Edit `installer/BrilliantExecutor-Setup.iss` to customize:
- Application name and version
- Installation directory
- Start Menu group name
- Icon and licensing
- Prerequisites checking

### .NET Runtime Check
The Setup Installer includes a check for .NET 10 Runtime:
- If not found, shows informational message
- Provides download link: https://dotnet.microsoft.com/download
- Installation continues regardless (warning-only)

## Troubleshooting

### Inno Setup Not Found
If you see: *"WARNING: Inno Setup not found"*

1. Download from: https://jrsoftware.org/isdl.php
2. Install to default location
3. Restart command prompt or run: `refreshenv`
4. Run `build_release.bat` again

### C++ Build Failed
1. Ensure Visual Studio 2022 is installed
2. Open Visual Studio Installer
3. Add "Desktop development with C++" workload
4. Rebuild: `cmake --build build --config Release`

### C# Build Failed
1. Install .NET 10 SDK: https://dotnet.microsoft.com/download
2. Verify installation: `dotnet --version`
3. Clean and rebuild: `cd ui && dotnet clean && dotnet build -c Release`

### Missing Syntax.dll
1. Verify C++ build completed
2. Check `build\Release\` folder
3. Manually copy: `copy build\Release\Syntax.dll publish\`

## Version Numbering

Follow Semantic Versioning: **MAJOR.MINOR.PATCH**

Examples:
- `v2.0.0` - Major release (breaking changes)
- `v2.0.1` - Patch release (bug fixes)
- `v2.1.0` - Minor release (new features)

## Release Checklist

Before creating a release:

- [ ] All issues closed
- [ ] Code reviewed
- [ ] Tests passing
- [ ] README.md updated
- [ ] VERSION updated in all files
- [ ] CHANGELOG.md created/updated
- [ ] Build successful locally
- [ ] Both installers created
- [ ] Upload to GitHub Releases
- [ ] Tag created: `git tag vX.Y.Z`

## GitHub Actions Workflow

The automated workflow (`.github/workflows/build-release.yml`):

1. **Trigger:** Push git tag starting with `v`
2. **Environment:** Windows Server (latest)
3. **Steps:**
   - Setup .NET 10
   - Install CMake
   - Setup MSVC
   - Build C++ DLL
   - Build C# UI
   - Create portable ZIP
   - Create GitHub Release with both artifacts

**Status:** Check in Actions tab → "Build and Create Release"

## Distribution

### Downloads Page
Users can download from the Releases page:
https://github.com/akatsukixpain12-stack/Brilliant-executor/releases

### Recommended Download Method
1. Check requirements (Windows 10/11 x64, .NET 10)
2. Choose installer:
   - **First time users:** Use Setup Installer
   - **Portable needs:** Use Portable ZIP
   - **No admin access:** Use Portable ZIP
3. Download and verify file integrity

## Support

For issues with installation, see:
- [README.md](README.md) - Installation section
- [GitHub Issues](https://github.com/akatsukixpain12-stack/Brilliant-executor/issues)
- [CODE_SIGNING.md](CODE_SIGNING.md) - For code signing details
