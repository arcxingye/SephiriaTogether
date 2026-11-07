[CmdletBinding()]
param(
    [string]$GameDir = $env:SEPHIRIA_DIR,
    [string]$PackageRoot = "",
    # Allows validation of source metadata + a built DLL without making a package.
    [string]$PluginPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw "Pass -GameDir or set SEPHIRIA_DIR." }
if ([string]::IsNullOrWhiteSpace($PackageRoot)) { $PackageRoot = Join-Path $repoRoot "artifacts\WorkshopLocal\SephiriaTogether" }
$PackageRoot = [System.IO.Path]::GetFullPath($PackageRoot)
$metadataPath = Join-Path $PackageRoot "metadata.json"
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) { throw "Missing metadata.json" }
$metadata = [System.IO.File]::ReadAllText($metadataPath) | ConvertFrom-Json
if ($metadata.info.description.default -isnot [string] -or $metadata.info.description.'en-US' -isnot [string]) {
    throw "Expected localized default and en-US descriptions."
}
$descriptionPath = Join-Path $repoRoot "workshop\description.zh-CN.txt"
$description = [System.IO.File]::ReadAllText($descriptionPath).Replace("`r`n", "`n").TrimEnd()
if ($metadata.info.description.default.Replace("`r`n", "`n").TrimEnd() -cne $description) {
    throw "Chinese Workshop description and metadata differ."
}
foreach ($text in @($metadata.info.description.default, $metadata.info.description.'en-US')) {
    if ($text -match '(?i)stars-sephiriamod|Star.s Sephiria|Backpack Organizer|SephiriaBackpackOrganizer') {
        throw "Public Workshop descriptions must not name third-party Mods."
    }
}
if ($metadata.mod.schemaVersion -ne 1 -or $metadata.mod.modId -cne "com.sephiriamods.sephiriatogether" -or
    $metadata.mod.multiplayer.installation -cne "host_only") { throw "Unexpected Workshop identity/schema/installation mode." }
if ([string]::IsNullOrWhiteSpace($metadata.info.title)) { throw "Workshop title is required." }
if (@($metadata.mod.modules).Count -ne 1 -or $metadata.mod.modules[0].type -cne "dll" -or
    $metadata.mod.modules[0].path -cne "SephiriaTogether.Workshop.dll" -or
    $metadata.mod.modules[0].entryClass -cne "SephiriaTogether.WorkshopEntry") { throw "Unexpected native module declaration." }
if (@($metadata.mod.libraries | Where-Object { $null -ne $_ }).Count -ne 0) { throw "This native build should use the game's libraries." }
if ($metadata.info.preview -cne "thumbnail.png") { throw "Expected thumbnail.png" }
$preview = [System.IO.FileInfo]::new((Join-Path $PackageRoot "thumbnail.png"))
if (-not $preview.Exists -or $preview.Length -le 0 -or $preview.Length -ge 1000000) { throw "Thumbnail must be non-empty and below 1,000,000 bytes." }
$png = [System.IO.File]::ReadAllBytes($preview.FullName)
if ($png.Length -lt 24 -or [BitConverter]::ToString($png, 0, 8) -cne "89-50-4E-47-0D-0A-1A-0A") { throw "Invalid PNG thumbnail." }

# Cecil is a development-time validator dependency only. It is never packaged.
$cecil = Join-Path $GameDir "BepInEx\core\Mono.Cecil.dll"
if (-not (Test-Path -LiteralPath $cecil -PathType Leaf)) { throw "Validation requires a development copy of Mono.Cecil.dll under BepInEx/core." }
[void][System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($cecil))
$dllPath = if ([string]::IsNullOrWhiteSpace($PluginPath)) { Join-Path $PackageRoot "SephiriaTogether.Workshop.dll" } else { [System.IO.Path]::GetFullPath($PluginPath) }
$native = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dllPath)
$harmony = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir "Sephiria_Data\Managed\0Harmony.dll"))
$legacy = $null
try {
    if ($native.Name.Name -cne "SephiriaTogether.Workshop") { throw "Wrong native assembly name." }
    if ($native.Name.Version.ToString(3) -cne $metadata.mod.version) { throw "Manifest version does not match native DLL." }
    if ($native.MainModule.AssemblyReferences | Where-Object { $_.Name -match '^(BepInEx|MonoMod|SephiriaTogether$)' }) { throw "Native DLL has a loader/legacy dependency." }
    $entry = $native.MainModule.GetType($metadata.mod.modules[0].entryClass)
    if ($null -eq $entry -or -not $entry.IsPublic -or $entry.IsAbstract -or $entry.BaseType.FullName -cne "WorkshopModBase") { throw "Native entry must be a public concrete WorkshopModBase." }
    if (-not ($entry.Methods | Where-Object { $_.IsConstructor -and $_.IsPublic -and $_.Parameters.Count -eq 0 })) { throw "Native entry requires a public parameterless constructor." }
    foreach ($methodName in @("OnModLoaded", "OnModUnloaded")) {
        if (-not ($entry.Methods | Where-Object { $_.Name -ceq $methodName -and $_.IsVirtual -and -not $_.IsNewSlot })) { throw "Missing native lifecycle override: $methodName" }
    }
    $runtime = $native.MainModule.GetType("SephiriaTogether.Plugin")
    if ($runtime.BaseType.FullName -cne "UnityEngine.MonoBehaviour") { throw "Native runtime must not inherit BaseUnityPlugin." }
    $harmonyRef = $native.MainModule.AssemblyReferences | Where-Object { $_.Name -eq "0Harmony" }
    if ($null -eq $harmonyRef -or $harmonyRef.Version -ne $harmony.Name.Version) { throw "Native build does not reference the bundled game Harmony version." }
    $forbidden = @([System.IO.Directory]::GetFiles($PackageRoot, "*.dll", [System.IO.SearchOption]::AllDirectories) | Where-Object {
        [System.IO.Path]::GetFullPath($_) -cne $dllPath
    })
    if ($forbidden.Count -gt 0) { throw "Unexpected bundled DLLs: $($forbidden -join ', ')" }

    $legacyPath = Join-Path $repoRoot "bin\Release\netstandard2.1\SephiriaTogether.dll"
    if (Test-Path -LiteralPath $legacyPath -PathType Leaf) {
        $legacy = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($legacyPath)
        $messages = @($native.MainModule.Types | Where-Object { $_.Interfaces.InterfaceType.FullName -contains "Mirror.NetworkMessage" })
        foreach ($message in $messages) {
            $other = $legacy.MainModule.GetType($message.FullName)
            if ($null -eq $other) { throw "Legacy build lacks message $($message.FullName)" }
            $layout = ($message.Fields | Where-Object { -not $_.IsStatic } | ForEach-Object { $_.Name + ':' + $_.FieldType.FullName }) -join ','
            $otherLayout = ($other.Fields | Where-Object { -not $_.IsStatic } | ForEach-Object { $_.Name + ':' + $_.FieldType.FullName }) -join ','
            if ($layout -cne $otherLayout) { throw "Cross-loader message layout mismatch: $($message.FullName)" }
        }
        Write-Output "Cross-loader network message layouts: $($messages.Count) matched."
    }
    Write-Output "WORKSHOP PACKAGE METADATA VALIDATION PASSED"
    Write-Output "Version: $($metadata.mod.version); entry: $($entry.FullName); thumbnail: $($preview.Length) bytes"
    Write-Output "Native dependencies: $(($native.MainModule.AssemblyReferences.Name) -join ', ')"
    Write-Output "This does not execute Unity or submit anything to Steam."
}
finally {
    if ($null -ne $legacy) { $legacy.Dispose() }
    $native.Dispose()
    $harmony.Dispose()
}
