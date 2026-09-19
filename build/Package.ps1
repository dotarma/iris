param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("x86", "x64", "arm64")]
    [string]$Architecture,
    [string]$Version = "",
    [string]$DotNet = "dotnet"
)

if ([string]::IsNullOrWhiteSpace($Version)) {
    $props = Join-Path (Split-Path -Parent $PSScriptRoot) "Directory.Build.props"
    $xml = [xml](Get-Content $props)
    $Version = $xml.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($Version)) { throw "Could not determine version from Directory.Build.props." }
}

$ErrorActionPreference = "Stop"
$platform = if ($Architecture -eq "arm64") { "ARM64" } else { $Architecture }
$runtime = "win-$Architecture"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "artifacts/$Architecture"
$appOutput = Join-Path $output "app"
$packageRoot = Join-Path $output "package"
$installerOutput = Join-Path $packageRoot "installer"
$payloadOutput = Join-Path $installerOutput "payload/Iris.App"

function Get-SafeRelativePath([string]$BasePath, [string]$TargetPath) {
    $base = [IO.Path]::GetFullPath($BasePath).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $target = [IO.Path]::GetFullPath($TargetPath)
    if (-not $target.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the package root: $target"
    }
    return $target.Substring($base.Length)
}

function Copy-AppPri([string]$ProjectName, [string]$PublishOutput) {
    # Known WinUI unpackaged gap: `dotnet publish` does not emit the app's own
    # .pri (XAML resource index), so ms-appx LoadComponent fails with
    # XamlParseException and the window never appears. `dotnet build` does emit
    # it, so build first and copy the fresh PRI into the publish folder.
    # Without this file the packaged app crashes on startup; fail loudly here.
    $built = Get-ChildItem (Join-Path $root "src/$ProjectName/bin/$platform/Release") -Filter "$ProjectName.pri" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*$runtime*" } | Select-Object -First 1
    if (-not $built) { throw "Missing $ProjectName.pri in build output; cannot package a working app." }
    Copy-Item $built.FullName (Join-Path $PublishOutput "$ProjectName.pri") -Force
}

if (Test-Path $output) { Remove-Item $output -Recurse -Force }
New-Item -ItemType Directory -Path $appOutput, $installerOutput, $payloadOutput -Force | Out-Null

& $DotNet build (Join-Path $root "src/Iris.App/Iris.App.csproj") -c Release -p:Platform=$platform -p:RuntimeIdentifier=$runtime -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Iris.App build failed." }
& $DotNet publish (Join-Path $root "src/Iris.App/Iris.App.csproj") -c Release -r $runtime --self-contained true -p:Platform=$platform -p:Version=$Version -o $appOutput --no-restore
if ($LASTEXITCODE -ne 0) { throw "Iris.App publish failed." }
Copy-AppPri "Iris.App" $appOutput
& $DotNet build (Join-Path $root "src/Iris.Installer/Iris.Installer.csproj") -c Release -p:Platform=$platform -p:RuntimeIdentifier=$runtime -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Iris.Installer build failed." }
& $DotNet publish (Join-Path $root "src/Iris.Installer/Iris.Installer.csproj") -c Release -r $runtime --self-contained true -p:Platform=$platform -p:Version=$Version -o $installerOutput --no-restore
if ($LASTEXITCODE -ne 0) { throw "Iris.Installer publish failed." }
Copy-AppPri "Iris.Installer" $installerOutput

Copy-Item (Join-Path $appOutput "*") $payloadOutput -Recurse -Force
Copy-Item (Join-Path $root "LICENSE") (Join-Path $installerOutput "LICENSE.txt") -Force
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") (Join-Path $installerOutput "THIRD-PARTY-NOTICES.md") -Force
$payloadFiles = [ordered]@{}
Get-ChildItem $payloadOutput -File -Recurse | ForEach-Object {
    $relative = (Get-SafeRelativePath $payloadOutput $_.FullName).Replace("\", "/")
    $payloadFiles[$relative] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$payloadManifest = [ordered]@{ version = $Version; architecture = $Architecture; files = $payloadFiles }
$payloadManifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $payloadOutput "payload.manifest.json") -Encoding UTF8

$packageFiles = [ordered]@{}
Get-ChildItem $installerOutput -File -Recurse | ForEach-Object {
    $relative = (Get-SafeRelativePath $installerOutput $_.FullName).Replace("\", "/")
    $packageFiles[$relative] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$packageManifest = [ordered]@{ version = $Version; architecture = $Architecture; files = $packageFiles }
$packageManifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $installerOutput "package.manifest.json") -Encoding UTF8

$archive = Join-Path $output "Iris-$Version-$Architecture.zip"
Compress-Archive -Path (Join-Path $installerOutput "*") -DestinationPath $archive -CompressionLevel Optimal
$checksum = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$archive.sha256" -Value "$checksum  $(Split-Path $archive -Leaf)" -Encoding ASCII
Write-Output $archive
