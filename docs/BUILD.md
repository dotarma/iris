# Build

## Prerequisites

- Windows 10 build 19041 or later
- .NET SDK 8.0.419 or a compatible later 8.0 feature band
- Visual Studio 2022 Windows application development tools are recommended for XAML debugging
- PowerShell 7 for packaging scripts; Windows PowerShell 5.1 also supports the scripts on current Windows

## Commands

```powershell
dotnet restore Iris.sln
dotnet test tests/Iris.Tests/Iris.Tests.csproj -c Release
```

Build one architecture:

```powershell
dotnet build src/Iris.App/Iris.App.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64
dotnet build src/Iris.Installer/Iris.Installer.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64
```

Architecture mapping:

| Package | MSBuild platform | Runtime identifier |
|---|---|---|
| x86 | x86 | win-x86 |
| x64 | x64 | win-x64 |
| ARM64 | ARM64 | win-arm64 |

`build/Package.ps1` builds each project, publishes .NET and Windows App SDK self-contained outputs, creates application and package manifests with SHA-256 hashes, and writes a ZIP plus checksum. Trimming, Native AOT, and single-file publishing are intentionally disabled because WinUI compatibility must take priority.

Known WinUI unpackaged gap: `dotnet publish` does not emit the app's own `.pri` (XAML resource index), so the packaged executable crashes in `InitializeComponent` with `XamlParseException` and no window appears. `Package.ps1` works around this by building first and copying the fresh `Iris.App.pri` / `Iris.Installer.pri` into the publish folders, failing loudly if absent. Never ship a package whose publish output lacks the app `.pri`.
