param(
    [string]$GameDir = $env:SEPHIRIA_DIR,
    [string]$Version = "3.3.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path "$PSScriptRoot\..").Path
$artifacts = Join-Path $projectRoot "artifacts"
$staging = Join-Path $artifacts "SephiriaTogether-$Version"
$pluginDir = Join-Path $staging "BepInEx\plugins"

& "$PSScriptRoot\build.ps1" -GameDir $GameDir -Configuration Release

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}

New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot\bin\Release\netstandard2.1\SephiriaTogether.dll" -Destination $pluginDir
Copy-Item -LiteralPath "$projectRoot\README.md" -Destination $staging
Copy-Item -LiteralPath "$projectRoot\LICENSE" -Destination $staging
Copy-Item -LiteralPath "$projectRoot\CHANGELOG.md" -Destination $staging

$zip = Join-Path $artifacts "SephiriaTogether-$Version.zip"
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

Compress-Archive -Path "$staging\*" -DestinationPath $zip -CompressionLevel Optimal
Copy-Item -LiteralPath "$projectRoot\bin\Release\netstandard2.1\SephiriaTogether.dll" `
    -Destination (Join-Path $artifacts "SephiriaTogether.dll")

Write-Output "Created: $zip"
