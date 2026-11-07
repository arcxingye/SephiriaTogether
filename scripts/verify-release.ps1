[CmdletBinding()]
param([string]$Version = "3.10.0")

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifacts = Join-Path $repoRoot "artifacts"
$checksumsPath = Join-Path $artifacts "SHA256SUMS-$Version.txt"
if (-not (Test-Path -LiteralPath $checksumsPath -PathType Leaf)) { throw "Missing checksum file: $checksumsPath" }
foreach ($line in [System.IO.File]::ReadAllLines($checksumsPath)) {
    if ($line -notmatch '^([0-9a-f]{64})  ([^\\/]+)$') { throw "Invalid checksum entry." }
    $expected = $Matches[1]
    $path = Join-Path $artifacts $Matches[2]
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expected) {
        throw "Checksum mismatch: $path"
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
function Get-EntryHash($entry) {
    $stream = $entry.Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose(); $stream.Dispose() }
}

function Assert-Zip([string]$FileName, [string]$DllEntry, [string]$SourceDll, [string[]]$ExpectedFiles = @()) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $artifacts $FileName))
    try {
        $entries = @($archive.Entries | Where-Object { $_.Name.Length -gt 0 })
        $paths = @($entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($path in $paths) {
            if ($path -match '(^|/)(uploadticket\.json|mod-guard-manifest[^/]*|AGENTS\.md|CODEX_HANDOFF\.md|Assembly-CSharp\.dll|UnityEngine[^/]*\.dll|Mirror[^/]*\.dll)$' -or
                $path -match '(^|/)(\.git|Sephiria_Data|SephiriaDecompCache|backups)(/|$)') {
                throw "Unexpected private/development/game file in ${FileName}: $path"
            }
        }
        if ($ExpectedFiles.Count -gt 0 -and @(Compare-Object ($ExpectedFiles | Sort-Object) ($paths | Sort-Object)).Count -gt 0) {
            throw "Package file list differs from expected contents: $FileName"
        }
        $entry = @($entries | Where-Object { $_.FullName.Replace('\', '/') -ceq $DllEntry })
        if ($entry.Count -ne 1) { throw "Expected exactly one plugin DLL in $FileName" }
        $expectedHash = (Get-FileHash -LiteralPath $SourceDll -Algorithm SHA256).Hash.ToLowerInvariant()
        if ((Get-EntryHash $entry[0]) -cne $expectedHash) { throw "Packaged DLL does not match build: $FileName" }
        Write-Output "Verified $FileName ($($entries.Count) files; plugin hash matches)."
    }
    finally { $archive.Dispose() }
}

$legacyDll = Join-Path $repoRoot "bin\Release\netstandard2.1\SephiriaTogether.dll"
if ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($legacyDll).FileVersion -cne "$Version.0") {
    throw "Built DLL version mismatch."
}
$docs = @("README.md", "README.zh-CN.md", "LICENSE", "CHANGELOG.md", "INSTALL.md", "COMPATIBILITY.md", "DESIGN.md")
$pluginFiles = @("BepInEx/plugins/SephiriaTogether.dll") + $docs
Assert-Zip "SephiriaTogether-$Version.zip" "BepInEx/plugins/SephiriaTogether.dll" $legacyDll $pluginFiles
Assert-Zip "SephiriaTogether-$Version-with-BepInEx-5.4.23.5-win-x64.zip" "BepInEx/plugins/SephiriaTogether.dll" $legacyDll
if ((Get-FileHash -LiteralPath (Join-Path $artifacts "SephiriaTogether-$Version.zip")).Hash -cne
    (Get-FileHash -LiteralPath (Join-Path $artifacts "SephiriaTogether.zip")).Hash) { throw "Latest ZIP alias differs." }
if ((Get-FileHash -LiteralPath $legacyDll).Hash -cne
    (Get-FileHash -LiteralPath (Join-Path $artifacts "SephiriaTogether.dll")).Hash) { throw "Standalone DLL differs." }

$nativeZip = Join-Path $artifacts "SephiriaTogether-$Version-Workshop.zip"
if (Test-Path -LiteralPath $nativeZip -PathType Leaf) {
    $nativeDll = Join-Path $repoRoot "bin\Workshop\Release\netstandard2.1\SephiriaTogether.Workshop.dll"
    $prefix = "WorkshopLocal/SephiriaTogether/"
    $nativeFiles = @("SephiriaTogether.Workshop.dll", "metadata.json", "thumbnail.png", "README.md", "LICENSE") |
        ForEach-Object { $prefix + $_ }
    Assert-Zip "SephiriaTogether-$Version-Workshop.zip" ($prefix + "SephiriaTogether.Workshop.dll") $nativeDll $nativeFiles
    $archive = [System.IO.Compression.ZipFile]::OpenRead($nativeZip)
    try {
        $reader = New-Object System.IO.StreamReader ($archive.GetEntry($prefix + "metadata.json").Open())
        try { $metadata = $reader.ReadToEnd() | ConvertFrom-Json }
        finally { $reader.Dispose() }
        if ($metadata.mod.version -cne $Version) { throw "Native ZIP metadata version mismatch." }
        foreach ($text in @([string]$metadata.info.description.default, [string]$metadata.info.description.'en-US')) {
            if ([string]::IsNullOrWhiteSpace($text) -or $text.Length -gt 7999 -or
                $text -match "Star's|SephiriaMod|Backpack Organizer|Organizer Workshop|背包自动整理|背包自動整理") {
                throw "Public Workshop description failed its language/length/naming check."
            }
        }
    }
    finally { $archive.Dispose() }
}
Write-Output "RELEASE ASSET VERIFICATION PASSED ($Version)"
