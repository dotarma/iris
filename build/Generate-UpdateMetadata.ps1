param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$ArtifactsDirectory,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$ReleaseNotes = "See the GitHub release for details."
)

$ErrorActionPreference = "Stop"
$architectures = [ordered]@{}
foreach ($architecture in @("x86", "x64", "arm64")) {
    $name = "Iris-$Version-$architecture.zip"
    $path = Get-ChildItem $ArtifactsDirectory -Filter $name -File -Recurse | Select-Object -First 1
    if (-not $path) { throw "Missing release package: $name" }
    $architectures[$architecture] = [ordered]@{
        url = "https://github.com/dotarma/iris/releases/download/v$Version/$name"
        sha256 = (Get-FileHash $path.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$metadata = [ordered]@{
    version = $Version
    releaseDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
    channel = "update"
    minimumVersion = "1.0.0"
    releaseNotes = $ReleaseNotes
    architectures = $architectures
}
$metadata | ConvertTo-Json -Depth 5 | Set-Content $OutputPath -Encoding UTF8
