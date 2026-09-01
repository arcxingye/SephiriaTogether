[CmdletBinding()]
param(
    [string]$GameDir = $env:SEPHIRIA_DIR,
    [string]$SourcePath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Join-Path $repoRoot "AntiCheat.cs"
}
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    throw "Set SEPHIRIA_DIR or pass -GameDir with the Sephiria installation path."
}

$gameAssembly = Join-Path $GameDir "Sephiria_Data\Managed\Assembly-CSharp.dll"
$mirrorAssembly = Join-Path $GameDir "Sephiria_Data\Managed\Mirror.dll"
foreach ($path in @($SourcePath, $gameAssembly, $mirrorAssembly)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file was not found: $path"
    }
}

$ilspyCommand = Get-Command ilspycmd -ErrorAction SilentlyContinue
if ($ilspyCommand -eq $null) {
    throw "ilspycmd was not found on PATH. Install it with: dotnet tool install --global ilspycmd"
}
$ilspyPath = if (-not [string]::IsNullOrWhiteSpace($ilspyCommand.Source)) {
    $ilspyCommand.Source
} else {
    $ilspyCommand.Path
}

$source = [System.IO.File]::ReadAllText([System.IO.Path]::GetFullPath($SourcePath))
$pattern = '(?s)\[HarmonyPatch\(typeof\((?<type>[^)]+)\),\s*(?<method>nameof\([^)]+\)|"[^"]+")\)\]'
$targets = @([regex]::Matches($source, $pattern) | ForEach-Object {
    $rawMethod = $_.Groups["method"].Value
    if ($rawMethod.StartsWith("nameof(", [StringComparison]::Ordinal)) {
        $method = ($rawMethod.Substring(7, $rawMethod.Length - 8) -split '\.')[-1]
    } else {
        $method = $rawMethod.Trim('"')
    }
    [pscustomobject]@{
        Type = $_.Groups["type"].Value.Trim()
        Method = $method
    }
})
if ($targets.Count -eq 0) {
    throw "No HarmonyPatch targets were found in: $SourcePath"
}

$duplicates = @($targets | Group-Object Type, Method | Where-Object Count -gt 1)
$results = foreach ($group in ($targets | Group-Object Type)) {
    $typeName = $group.Name
    if ($typeName -eq "RemoteProcedureCalls") {
        $assembly = $mirrorAssembly
        $decompileType = "Mirror.RemoteCalls.RemoteProcedureCalls"
    } else {
        $assembly = $gameAssembly
        $decompileType = $typeName
    }
    $decompiled = (& $ilspyPath --disable-updatecheck -t $decompileType $assembly 2>&1) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw "ilspycmd failed for type '$decompileType' with exit code $LASTEXITCODE."
    }
    foreach ($target in $group.Group) {
        [pscustomobject]@{
            Type = $target.Type
            Method = $target.Method
            Found = $decompiled.Contains($target.Method)
        }
    }
}

$missing = @($results | Where-Object { -not $_.Found })
if ($missing.Count -gt 0) {
    Write-Host "Missing Harmony targets:"
    $missing | Format-Table Type, Method -AutoSize
}
if ($duplicates.Count -gt 0) {
    Write-Host "Duplicate Harmony target keys:"
    $duplicates | Select-Object Count, Name | Format-Table -AutoSize
}
if ($missing.Count -gt 0 -or $duplicates.Count -gt 0) {
    throw "Anti-cheat Harmony validation failed: targets=$($results.Count), missing=$($missing.Count), duplicates=$($duplicates.Count)."
}

Write-Host "ANTI-CHEAT HARMONY VALIDATION PASSED"
Write-Host "Targets: $($results.Count)"
Write-Host "Missing: 0"
Write-Host "Duplicate keys: 0"
