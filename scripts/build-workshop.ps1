[CmdletBinding()]
param([string]$GameDir = $env:SEPHIRIA_DIR)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw "Pass -GameDir or set SEPHIRIA_DIR." }
if (-not (Test-Path -LiteralPath $repoRoot -PathType Container)) { throw "Project root missing." }
foreach ($file in @("metadata.json", "thumbnail.png", "README.md")) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot "workshop\$file") -PathType Leaf)) { throw "Workshop source missing: $file" }
}
dotnet run --project (Join-Path $repoRoot "tests\PatchBinding.Tests\PatchBinding.Tests.csproj") -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "Workshop transpiler regression tests failed." }
dotnet build (Join-Path $repoRoot "SephiriaTogether.csproj") -c Release -p:Loader=Workshop "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "Workshop build failed." }
$dll = Join-Path $repoRoot "bin\Workshop\Release\netstandard2.1\SephiriaTogether.Workshop.dll"
& (Join-Path $PSScriptRoot "validate-compatibility.ps1") -GameDir $GameDir -PluginPath $dll

$artifacts = Join-Path $repoRoot "artifacts"
if (-not (Test-Path -LiteralPath $artifacts -PathType Container)) { [void](New-Item -ItemType Directory -Path $artifacts) }
$localRoot = Join-Path $artifacts "WorkshopLocal"
if (-not (Test-Path -LiteralPath $localRoot -PathType Container)) { [void](New-Item -ItemType Directory -Path $localRoot) }
$package = Join-Path $localRoot "SephiriaTogether"
if (-not (Test-Path -LiteralPath $package -PathType Container)) { [void](New-Item -ItemType Directory -Path $package) }
# Replace only generated package inputs. Keep uploadticket.json and review proof
# files intact so rebuilding an existing upload folder retains its Workshop ID.
Copy-Item -LiteralPath $dll -Destination (Join-Path $package "SephiriaTogether.Workshop.dll") -Force
foreach ($file in @("metadata.json", "thumbnail.png", "README.md")) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "workshop\$file") -Destination (Join-Path $package $file) -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination (Join-Path $package "LICENSE") -Force
& (Join-Path $PSScriptRoot "validate-workshop.ps1") -GameDir $GameDir -PackageRoot $package
Write-Output "Workshop package ready: $package"
Write-Output "Copy this folder into the game's WorkshopLocal folder to inspect/upload it in the native Workshop panel."
