param(
    [string]$GameDir = $env:SEPHIRIA_DIR,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    throw "Set SEPHIRIA_DIR or pass -GameDir with the Sephiria installation path."
}

dotnet build "$PSScriptRoot\..\SephiriaTogether.csproj" `
    -c $Configuration `
    -p:GameDir="$GameDir"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}
