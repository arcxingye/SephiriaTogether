[CmdletBinding()]
param(
    [string]$PackageRoot = "",
    [UInt64]$PublishedFileId = 0,
    [switch]$Offline
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = Join-Path $repoRoot "artifacts\WorkshopLocal\SephiriaTogether"
}
$PackageRoot = [System.IO.Path]::GetFullPath($PackageRoot)
if (-not (Test-Path -LiteralPath $PackageRoot -PathType Container)) { throw "Package folder missing: $PackageRoot" }

function Assert-RegularPath([string]$Path) {
    if (([System.IO.File]::GetAttributes($Path) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Linked package paths are not supported: $Path"
    }
}

Assert-RegularPath $PackageRoot
$metadataPath = Join-Path $PackageRoot "metadata.json"
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) { throw "Missing metadata.json" }
Assert-RegularPath $metadataPath
$metadata = [System.IO.File]::ReadAllText($metadataPath) | ConvertFrom-Json
if ($metadata.mod.modId -cne "com.sephiriamods.sephiriatogether") { throw "This checker expects a Sephiria Together package." }

$ticket = $null
$ticketPath = Join-Path $PackageRoot "uploadticket.json"
if (Test-Path -LiteralPath $ticketPath -PathType Leaf) {
    Assert-RegularPath $ticketPath
    $ticket = [System.IO.File]::ReadAllText($ticketPath) | ConvertFrom-Json
    if ($ticket.modId -and $ticket.modId -cne $metadata.mod.modId) { throw "The upload ticket belongs to a different mod." }
    if ($PublishedFileId -eq 0 -and $ticket.publishedFileId) {
        $PublishedFileId = [UInt64]$ticket.publishedFileId
    }
    elseif ($PublishedFileId -ne 0 -and $ticket.publishedFileId -and $PublishedFileId -ne [UInt64]$ticket.publishedFileId) {
        throw "The explicit Workshop ID differs from uploadticket.json. Check the package/ID before continuing."
    }
}
if (-not $Offline -and $PublishedFileId -eq 0) { throw "Pass -PublishedFileId or use the original local upload folder containing uploadticket.json." }

# Match ModGuardWorkshop.ComputeHash: include ALL DLLs under the specified
# package, sort their relative paths ordinally, then hash the domain-separated
# path/NUL/length/NUL/file-hash/newline payload. Never load or execute a DLL.
$queue = New-Object 'System.Collections.Generic.Queue[string]'
$queue.Enqueue($PackageRoot)
$files = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([System.StringComparer]::Ordinal)
$prefix = $PackageRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
while ($queue.Count -gt 0) {
    $directory = $queue.Dequeue()
    foreach ($path in [System.IO.Directory]::EnumerateFileSystemEntries($directory)) {
        Assert-RegularPath $path
        if ([System.IO.Directory]::Exists($path)) { $queue.Enqueue($path); continue }
        if ($path.EndsWith(".dll", [StringComparison]::OrdinalIgnoreCase)) {
            $relative = $path.Substring($prefix.Length).Replace('\', '/')
            $files.Add($relative, $path)
        }
    }
}
if ($files.Count -eq 0) { throw "No DLLs in this package." }
[string[]]$paths = @($files.Keys)
[Array]::Sort($paths, [StringComparer]::Ordinal)
$payload = New-Object System.Text.StringBuilder
[void]$payload.Append("mod-guard:dll:v1`n")
$dlls = @()
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    foreach ($relative in $paths) {
        $stream = [System.IO.File]::Open($files[$relative], [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        try {
            $length = $stream.Length
            $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
        }
        finally { $stream.Dispose() }
        [void]$payload.Append($relative).Append([char]0).Append($length.ToString([Globalization.CultureInfo]::InvariantCulture)).Append([char]0).Append($hash).Append("`n")
        $dlls += [pscustomobject]@{Path=$relative; Bytes=$length; Sha256=$hash}
    }
    $dllSetHash = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload.ToString()))).Replace('-', '').ToLowerInvariant()
}
finally { $sha.Dispose() }

$endpoint = $null
$serverStatus = "not-queried"
$lookupError = $null
if (-not $Offline) {
    $endpoint = "https://mod-guard.teamhoray.com/v1/workshop/$PublishedFileId/$dllSetHash"
    $oldTls = [Net.ServicePointManager]::SecurityProtocol
    try {
        [Net.ServicePointManager]::SecurityProtocol = $oldTls -bor [Net.SecurityProtocolType]::Tls12
        # Public read-only lookup used by the game. No account cookie/token,
        # upload, local cache rewrite, ticket update or approval override.
        $reply = Invoke-RestMethod -Method Get -Uri $endpoint -TimeoutSec 30
        if ([string]$reply.published_file_id -cne [string]$PublishedFileId -or
            $reply.dll_sha256 -cne $dllSetHash -or [string]$reply.app_id -cne "2436940") {
            throw "The review service returned a different item/hash/app identity."
        }
        if (@("approved", "waiting", "checking", "rejected", "unknown") -cnotcontains [string]$reply.status) {
            throw "Unrecognized review service status: $($reply.status)"
        }
        $serverStatus = [string]$reply.status
    }
    catch {
        $serverStatus = "unavailable"
        $lookupError = $_.Exception.Message
    }
    finally { [Net.ServicePointManager]::SecurityProtocol = $oldTls }
}

$ticketVersionMatches = $null
if ($null -ne $ticket) { $ticketVersionMatches = ([string]$ticket.version -ceq [string]$metadata.mod.version) }
$nextStep = switch ($serverStatus) {
    "approved" { "This exact DLL set is approved for this Workshop ID. Test the downloaded subscription copy with the local development package disabled." }
    "waiting" { "Submission is queued. Follow its status in the native Workshop panel; do not rebuild/replace the DLL while tracking this submission." }
    "checking" { "Submission is being reviewed. Follow its status in the native Workshop panel." }
    "unknown" { "No matching review record for these DLL bytes and this item ID. Retry Upload/Request review from the ORIGINAL local package in the game; keep uploadticket.json. Steam upload success alone does not complete review submission." }
    "rejected" { "Read the native review reason. Resolve the stated issue and use the game's resubmission process; do not alter approval files." }
    "unavailable" { "Review lookup failed; this is not a rejection. Retry when the service is reachable and inspect the lookup error." }
    default { "Offline fingerprint only. Run without -Offline and supply the original Workshop item ID to check the review service." }
}

[pscustomobject]@{
    PackageRoot = $PackageRoot
    PackageVersion = [string]$metadata.mod.version
    PublishedFileId = [string]$PublishedFileId
    DllSetHash = $dllSetHash
    DllFiles = $dlls
    LastTicketVersion = if ($null -ne $ticket) { [string]$ticket.version } else { $null }
    LastTicketStatus = if ($null -ne $ticket) { [string]$ticket.serverStatus } else { $null }
    TicketVersionMatchesPackage = $ticketVersionMatches
    CurrentDllReviewStatus = $serverStatus
    LookupEndpoint = $endpoint
    LookupError = $lookupError
    CheckedAtUtc = [DateTime]::UtcNow.ToString("o")
    NextStep = $nextStep
    Scope = "Review status for local package bytes only; this does not download the Steam item or prove what a subscriber has installed. An old ticket's approval is not proof for new DLL bytes."
}
