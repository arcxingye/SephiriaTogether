param(
    [string]$GameDir = $env:SEPHIRIA_DIR,
    [string]$Version = "3.9.9",
    [switch]$IncludeWorkshop
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

if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) {
    throw "Project root is missing: $projectRoot"
}
if (-not (Test-Path -LiteralPath $artifacts -PathType Container)) {
    New-Item -ItemType Directory -Path $artifacts | Out-Null
}

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
foreach ($document in @("README.zh-CN.md", "COMPATIBILITY.md", "DESIGN.md")) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination $staging
}

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
foreach ($document in @("README.zh-CN.md", "COMPATIBILITY.md", "DESIGN.md")) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination $bundleStaging
}
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

$releaseFiles = @($zip, $latestZip, (Join-Path $artifacts "SephiriaTogether.dll"), $bundleZip)
if ($IncludeWorkshop) {
    & "$PSScriptRoot\build-workshop.ps1" -GameDir $GameDir
    $nativePackage = Join-Path $artifacts "WorkshopLocal\SephiriaTogether"
    $nativeZip = Join-Path $artifacts "SephiriaTogether-$Version-Workshop.zip"
    $nativeMetadata = [System.IO.File]::ReadAllText((Join-Path $nativePackage "metadata.json")) | ConvertFrom-Json
    if ($nativeMetadata.mod.version -cne $Version) { throw "Workshop metadata version differs from release $Version." }
    $nativeFiles = @("SephiriaTogether.Workshop.dll", "metadata.json", "thumbnail.png", "README.md", "LICENSE")
    foreach ($name in $nativeFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $nativePackage $name) -PathType Leaf)) { throw "Native package input missing: $name" }
    }
    if (Test-Path -LiteralPath $nativeZip) { Remove-Item -LiteralPath $nativeZip -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::Open($nativeZip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        # Whitelist files: a reused upload directory may contain private review
        # tickets/proofs, which must never become public GitHub release assets.
        foreach ($name in $nativeFiles) {
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,
                (Join-Path $nativePackage $name), "WorkshopLocal/SephiriaTogether/$name",
                [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally { $archive.Dispose() }
    $releaseFiles += $nativeZip
    Write-Output "Created: $nativeZip"
}

$checksums = foreach ($file in $releaseFiles) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($file))"
}
$checksumPath = Join-Path $artifacts "SHA256SUMS-$Version.txt"
[System.IO.File]::WriteAllText($checksumPath, (($checksums -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding($false)))
Write-Output "Created: $checksumPath"
