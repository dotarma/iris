# Iris

**Making custom ROMs on Samsung devices easier.**

Iris is an open-source Windows toolkit designed to make working with ADB, Fastboot, drivers, and Samsung custom ROM workflows easier.

Iris provides a native Windows 11 interface for device discovery, common reboot actions, Android Platform-Tools management, restricted ADB/Fastboot command execution, driver guidance, diagnostics, updates, and carefully separated brokkR-flash integration.

## Highlights

- Native WinUI 3 desktop application with System, Light, and Dark themes
- ADB and Fastboot discovery with explicit device selection (no automatic first-device targeting)
- Restricted terminal that directly starts only resolved `adb.exe` or `fastboot.exe` paths, with ADB server/transport overrides rejected
- Official Google Platform-Tools download with license acknowledgement, optional checksum verification, safe extraction, progress, and cancellation
- Samsung and Android USB driver detection with official installation guidance
- Separate brokkR-flash process integration with mandatory target selection and destructive-operation warnings
- Structured local logs and exportable diagnostics
- Checksum- and process-architecture-verified ZIP updates from the `update` branch
- Separate Fluent WinUI installer for x86, x64, and ARM64

## Safety

Iris never invokes `cmd.exe`, PowerShell, Bash, or another shell for terminal commands. It does not silently install drivers, disable Driver Signature Enforcement, or bundle Samsung drivers. Only a small read-only command set runs without confirmation; all other terminal commands, reboots, and flashes require explicit confirmation showing the exact command/target. Development/CI packages are unsigned: verify repository, release, and checksum provenance before running elevated installers.

Firmware flashing is inherently risky. Back up important data, use firmware made for the exact device, and understand the device recovery procedure before continuing.

## Requirements

- Windows 10 version 2004 (build 19041) or later; Windows 11 recommended
- x86, x64, or ARM64 Windows
- .NET 8 SDK and Visual Studio 2022 with Windows application development tools for development
- A data-capable USB cable and the correct signed OEM driver
- A separately obtained brokkR-flash installation for Samsung/Odin workflows

Published packages are self-contained; end users do not need to install .NET or Windows App SDK separately.

## Build And Test

```powershell
dotnet restore Iris.sln
dotnet test tests/Iris.Tests/Iris.Tests.csproj -c Release
dotnet build src/Iris.App/Iris.App.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64
dotnet build src/Iris.Installer/Iris.Installer.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64
```

Create an installer package:

```powershell
./build/Package.ps1 -Architecture x64
```

Version defaults to `Directory.Build.props`. Use `x86`, `x64`, or `arm64`. Output and SHA-256 files are written under `artifacts/<architecture>/`.

## Repository Layout

```text
src/Iris.Core       Domain models and interfaces
src/Iris.Services   Process, device, download, tools, update, driver, logging services
src/Iris.App        Main WinUI 3 application and MVVM presentation
src/Iris.Installer  Separate WinUI 3 installer/uninstaller
tests/Iris.Tests    Unit and security tests
build               Packaging and update metadata scripts
docs                Architecture, security, build, release, and user documentation
.github/workflows   Multi-architecture CI and release automation
```

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Build](docs/BUILD.md)
- [Installer](docs/INSTALLER.md)
- [Updates](docs/UPDATES.md)
- [Release](docs/RELEASE.md)
- [Security](docs/SECURITY.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Third-party software](docs/THIRD-PARTY.md)

## License

Iris is licensed under the [MIT License](LICENSE). Third-party software and services retain their own licenses, terms, trademarks, and copyrights. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
