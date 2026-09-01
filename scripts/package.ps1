param(
    [string]$GameDir = $env:SEPHIRIA_DIR,
    [string]$Version = "3.9.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path "$PSScriptRoot\..").Path
$artifacts = Join-Path $projectRoot "artifacts"
$staging = Join-Path $artifacts "SephiriaTogether-$Version"
$pluginDir = Join-Path $staging "BepInEx\plugins"
$bepInExVersion = "5.4.23.5"
$bepInExAsset = "BepInEx_win_x64_$bepInExVersion.zip"
$bepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$bepInExVersion/$bepInExAsset"
$bepInExSha256 = "82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4"
$bepInExArchive = Join-Path $artifacts $bepInExAsset
$bundleStaging = Join-Path $artifacts "SephiriaTogether-$Version-with-BepInEx"

& "$PSScriptRoot\build.ps1" -GameDir $GameDir -Configuration Release

$builtVersion = (Get-Item -LiteralPath "$projectRoot\bin\Release\netstandard2.1\SephiriaTogether.dll").VersionInfo.FileVersion
if (!$builtVersion.StartsWith("$Version.")) {
    throw "Package version $Version does not match built DLL version $builtVersion."
}

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}

New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot\bin\Release\netstandard2.1\SephiriaTogether.dll" -Destination $pluginDir
Copy-Item -LiteralPath "$projectRoot\README.md" -Destination $staging
Copy-Item -LiteralPath "$projectRoot\LICENSE" -Destination $staging
Copy-Item -LiteralPath "$projectRoot\CHANGELOG.md" -Destination $staging
Copy-Item -LiteralPath "$projectRoot\INSTALL.md" -Destination $staging

$zip = Join-Path $artifacts "SephiriaTogether-$Version.zip"
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

Compress-Archive -Path "$staging\*" -DestinationPath $zip -CompressionLevel Optimal
$latestZip = Join-Path $artifacts "SephiriaTogether.zip"
Copy-Item -LiteralPath $zip -Destination $latestZip -Force
Copy-Item -LiteralPath "$projectRoot\bin\Release\netstandard2.1\SephiriaTogether.dll" `
    -Destination (Join-Path $artifacts "SephiriaTogether.dll")

Write-Output "Created: $zip"
Write-Output "Created: $latestZip"

if (!(Test-Path -LiteralPath $bepInExArchive) -or
    (Get-FileHash -LiteralPath $bepInExArchive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $bepInExSha256) {
    Invoke-WebRequest -Uri $bepInExUrl -OutFile $bepInExArchive
}

$actualHash = (Get-FileHash -LiteralPath $bepInExArchive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $bepInExSha256) {
    throw "BepInEx archive checksum mismatch. Expected $bepInExSha256, got $actualHash."
}

if (Test-Path -LiteralPath $bundleStaging) {
    Remove-Item -LiteralPath $bundleStaging -Recurse -Force
}

Expand-Archive -LiteralPath $bepInExArchive -DestinationPath $bundleStaging
New-Item -ItemType Directory -Path "$bundleStaging\BepInEx\plugins" -Force | Out-Null
New-Item -ItemType Directory -Path "$bundleStaging\licenses" -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot\bin\Release\netstandard2.1\SephiriaTogether.dll" `
    -Destination "$bundleStaging\BepInEx\plugins\SephiriaTogether.dll"
Copy-Item -LiteralPath "$projectRoot\INSTALL.md" -Destination $bundleStaging
Copy-Item -LiteralPath "$projectRoot\README.md" -Destination $bundleStaging
Copy-Item -LiteralPath "$projectRoot\CHANGELOG.md" -Destination $bundleStaging
Copy-Item -LiteralPath "$projectRoot\LICENSE" -Destination "$bundleStaging\licenses\SephiriaTogether-MIT.txt"
Copy-Item -LiteralPath "$projectRoot\THIRD-PARTY-NOTICES.md" -Destination $bundleStaging
Invoke-WebRequest `
    -Uri "https://raw.githubusercontent.com/BepInEx/BepInEx/v$bepInExVersion/LICENSE" `
    -OutFile "$bundleStaging\licenses\BepInEx-LGPL-2.1.txt"
Invoke-WebRequest `
    -Uri "https://raw.githubusercontent.com/NeighTools/UnityDoorstop/v4.5.0/LICENSE" `
    -OutFile "$bundleStaging\licenses\UnityDoorstop-LGPL-2.1.txt"

$bundleZip = Join-Path $artifacts "SephiriaTogether-$Version-with-BepInEx-$bepInExVersion-win-x64.zip"
if (Test-Path -LiteralPath $bundleZip) {
    Remove-Item -LiteralPath $bundleZip -Force
}

Compress-Archive -Path "$bundleStaging\*" -DestinationPath $bundleZip -CompressionLevel Optimal
Write-Output "Created: $bundleZip"
