[CmdletBinding()]
param(
    [string]$GameDir = $env:SEPHIRIA_DIR,
    [string]$PluginPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    throw "Set SEPHIRIA_DIR or pass -GameDir."
}
if ([string]::IsNullOrWhiteSpace($PluginPath)) {
    $PluginPath = Join-Path $repoRoot "bin\Release\netstandard2.1\SephiriaTogether.dll"
}
$managed = Join-Path $GameDir "Sephiria_Data\Managed"
$core = Join-Path $GameDir "BepInEx\core"
foreach ($path in @($PluginPath, (Join-Path $core "Mono.Cecil.dll"))) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing file: $path" }
}
# Load the metadata reader without modifying the installed game's file flags.
[void][System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Join-Path $core "Mono.Cecil.dll")))
$assemblies = @{}
$failures = New-Object 'System.Collections.Generic.List[string]'
$targetCount = 0
$parameterCount = 0
$reflectionCount = 0

function Get-MetadataType($reference) {
    $scope = $reference.Scope.Name -replace '\.dll$', ''
    if (-not $assemblies.ContainsKey($scope)) {
        $path = Join-Path $managed ($scope + ".dll")
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $path = Join-Path $core ($scope + ".dll")
        }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Cannot resolve assembly: $scope" }
        $assemblies[$scope] = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
    }
    return $assemblies[$scope].MainModule.GetType($reference.FullName)
}

function Find-Methods($type, [string]$name) {
    while ($null -ne $type) {
        $methods = @($type.Methods | Where-Object { $_.Name -ceq $name })
        if ($methods.Count -gt 0) { return $methods }
        if ($null -eq $type.BaseType) { break }
        $type = Get-MetadataType $type.BaseType
    }
}

function Find-Field($type, [string]$name) {
    while ($null -ne $type) {
        $field = $type.Fields | Where-Object { $_.Name -ceq $name }
        if ($null -ne $field) { return $field }
        if ($null -eq $type.BaseType) { break }
        $type = Get-MetadataType $type.BaseType
    }
}

function Test-Assignable($expected, $actual) {
    $expectedName = $expected.FullName.TrimEnd('&')
    if ($expectedName -eq "System.Object") { return $true }
    if ($expectedName -ceq $actual.FullName.TrimEnd('&')) { return $true }
    if ($actual -is [Mono.Cecil.ByReferenceType]) { $actual = $actual.ElementType }
    $type = Get-MetadataType $actual
    while ($null -ne $type -and $null -ne $type.BaseType) {
        if ($expectedName -ceq $type.BaseType.FullName) { return $true }
        $type = Get-MetadataType $type.BaseType
    }
    return $false
}

try {
    $plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($PluginPath)
    $assemblies[$plugin.Name.Name] = $plugin
    foreach ($patchType in $plugin.MainModule.Types) {
        $attributes = @($patchType.CustomAttributes | Where-Object {
            $_.AttributeType.FullName -eq "HarmonyLib.HarmonyPatch"
        })
        if ($attributes.Count -eq 0) { continue }
        $targetType = $null
        $methodName = $null
        $argumentTypes = $null
        foreach ($attribute in $attributes) {
            foreach ($argument in $attribute.ConstructorArguments) {
                switch ($argument.Type.FullName) {
                    "System.Type" { $targetType = $argument.Value }
                    "System.String" { $methodName = [string]$argument.Value }
                    "System.Type[]" { $argumentTypes = @($argument.Value | ForEach-Object { $_.Value.FullName }) }
                }
            }
        }
        # Dynamic selectors are checked separately below.
        if ($null -eq $targetType -or $null -eq $methodName) { continue }
        $type = Get-MetadataType $targetType
        if ($null -eq $type) {
            $failures.Add("$($patchType.Name): missing type $($targetType.FullName)")
            continue
        }
        $candidates = @(Find-Methods $type $methodName)
        if ($null -ne $argumentTypes) {
            $wanted = $argumentTypes -join ","
            $candidates = @($candidates | Where-Object {
                (($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ",") -ceq $wanted
            })
        }
        if ($candidates.Count -ne 1) {
            $signatures = ($candidates | ForEach-Object { $_.FullName }) -join "; "
            $failures.Add("$($patchType.Name): $($type.FullName).$methodName matches=$($candidates.Count) [$signatures]")
            continue
        }
        $targetCount++
        $method = $candidates[0]
        foreach ($hook in $patchType.Methods | Where-Object { $_.Name -in @("Prefix", "Postfix", "Finalizer") }) {
            foreach ($parameter in $hook.Parameters) {
                $name = $parameter.Name
                $actual = $null
                if ($name -eq "__instance") { $actual = $targetType }
                elseif ($name -eq "__result") { $actual = $method.ReturnType }
                elseif ($name -match '^___(.+)$') {
                    $field = Find-Field $type $Matches[1]
                    if ($null -ne $field) { $actual = $field.FieldType }
                }
                elseif ($name -match '^__(\d+)$') {
                    $index = [int]$Matches[1]
                    if ($index -lt $method.Parameters.Count) { $actual = $method.Parameters[$index].ParameterType }
                }
                elseif ($name.StartsWith("__")) { continue }
                else {
                    $original = $method.Parameters | Where-Object { $_.Name -ceq $name }
                    if ($null -ne $original) { $actual = $original.ParameterType }
                }
                if ($null -eq $actual) {
                    $failures.Add("$($patchType.Name).$($hook.Name): missing argument/field '$name' on $($method.FullName)")
                }
                elseif (-not (Test-Assignable $parameter.ParameterType $actual)) {
                    $failures.Add("$($patchType.Name).$($hook.Name): '$name' expects $($parameter.ParameterType.FullName), got $($actual.FullName)")
                }
                else { $parameterCount++ }
            }
        }
    }

    $game = $assemblies["Assembly-CSharp"].MainModule
    foreach ($typeName in @("GoToNextStage", "GoToNextStage_MultiZone")) {
        $methods = @(Find-Methods ($game.GetType($typeName)) "MoveTo")
        if ($methods.Count -ne 1) { $failures.Add("Dynamic target: $typeName.MoveTo matches=$($methods.Count)") }
        else { $targetCount++ }
    }
    $progress = @($game.GetType("DungeonManager").Methods | Where-Object {
        $_.Name.StartsWith("UserCode_CmdSendProgressValidationRequest") -and $_.Parameters.Count -eq 7
    })
    if ($progress.Count -ne 1 -or -not ($progress[0].Parameters | Where-Object {
        $_.Name -eq "clientChapterNum" -and $_.ParameterType.FullName -eq "System.Int32"
    })) { $failures.Add("Dynamic target: DungeonManager progress-validation signature changed") }
    else { $targetCount++ }

    # Private members used by AccessTools/Traverse are not checked by the C# compiler.
    $fields = @{
        BossSpawner = @("battlePhase", "isCheckingSkipOnBossDie", "isCheckingSkipOnBossPhaseChange", "skippable")
        AltarOfEnchant = @("remainingByGuid")
        UnitAI_NewBasic = @("merchantType")
        PlayerSpawner = @("isRejoinDetected")
        HorayNetworkManager = @("versionApprovedConnIds", "rejoinWhitelist")
        DropItemOnDie = @("isEXPAlreadyDropped")
        UI_MultiplayerPanel = @("moveMultiZone", "networkLobby", "playerAvatar", "roomCode", "isRoomCodeHide")
        UI_MultiplayerHUD = @("mySpawner")
        UIManager = @("connectedPlayer", "allUIBaseObjects")
    }
    foreach ($typeName in $fields.Keys) {
        foreach ($name in $fields[$typeName]) {
            if ($null -eq (Find-Field ($game.GetType($typeName)) $name)) {
                $failures.Add("Reflection field missing: $typeName.$name")
            }
            else { $reflectionCount++ }
        }
    }
    $methods = @{
        UI_MultiplayerPanel = @("RefreshDefaultSelectable", "get_SelectedGameMode", "IsSteamLobbyReady")
        UnitAvatar = @("ApplyCorpseVisual", "RpcBloodFestivalHealFx")
        AltarOfEnchant = @("TargetSetRemaining")
        WorkshopModManager = @("PrepareForActivation")
    }
    foreach ($typeName in $methods.Keys) {
        foreach ($name in $methods[$typeName]) {
            if (@(Find-Methods ($game.GetType($typeName)) $name).Count -ne 1) {
                $failures.Add("Reflection method missing/ambiguous: $typeName.$name")
            }
            else { $reflectionCount++ }
        }
    }

    $panel = $game.GetType("UI_MultiplayerPanel")
    $join = @($panel.Methods | Where-Object {
        $_.Name -eq "OnJoinButton" -and $_.Parameters.Count -eq 1 -and
        $_.Parameters[0].ParameterType.FullName -eq "HeathenEngineering.SteamworksIntegration.LobbyData"
    })
    if ($join.Count -ne 1) { $failures.Add("Steam join transpiler target changed") }
    else {
        $lobbyType = Get-MetadataType $join[0].Parameters[0].ParameterType
        $metadataGetter = @($lobbyType.Methods | Where-Object {
            $_.Name -ceq "get_Item" -and $_.Parameters.Count -eq 1 -and
            $_.Parameters[0].ParameterType.FullName -ceq "System.String" -and
            $_.ReturnType.FullName -ceq "System.String"
        })
        if ($metadataGetter.Count -ne 1) { $failures.Add("LobbyData.get_Item(string) returning string is missing or ambiguous") }
        else { $reflectionCount++ }
        $instructions = $join[0].Body.Instructions
        $matches = 0
        for ($i = 1; $i -lt $instructions.Count; $i++) {
            if ($instructions[$i - 1].OpCode.Code -eq "Ldstr" -and $instructions[$i - 1].Operand -ceq "Chapter" -and
                $instructions[$i].Operand -is [Mono.Cecil.MethodReference] -and
                $instructions[$i].Operand.Name -eq "get_Item" -and
                $instructions[$i].Operand.DeclaringType.FullName -eq "HeathenEngineering.SteamworksIntegration.LobbyData") {
                $matches++
            }
        }
        if ($matches -ne 1) { $failures.Add("Steam join Chapter transpiler anchor matches=$matches") }
        foreach ($required in @("BlockLocalModJoining", "CheckLobbyDetailed", "JoinAfterHostOnlyNotice", "CanJoinLobbyGameMode")) {
            if (-not ($instructions | Where-Object {
                $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $required
            })) { $failures.Add("Native Steam join check disappeared: $required") }
        }
    }
    $create = @(Find-Methods $panel "OnCreateButton")
    $readiness = @($create[0].Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq "IsSteamLobbyReady"
    })
    if ($readiness.Count -ne 1) { $failures.Add("IP creation readiness transpiler anchor matches=$($readiness.Count)") }
    $restart = @($game.GetType("HorayNetworkManager").Methods | Where-Object {
        $_.Name -eq "RestartGame" -and $_.Parameters.Count -eq 2
    })
    if ($restart.Count -ne 1 -or -not ($restart[0].Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq "RestartGame" -and
        $_.Operand.Parameters.Count -eq 4
    })) { $failures.Add("RestartGame wrapper no longer forwards to the four-argument target") }

    # Organizer 3.0.0 uses these native operations for both forward apply and
    # rollback. They must remain outside the direct-write anti-cheat filters.
    $organizerCommands = @("UserCode_CmdSwap__SByte__SByte__SByte__SByte", "UserCode_CmdDoClickAction__ItemPosition")
    foreach ($name in $organizerCommands) {
        if (@(Find-Methods ($game.GetType("GridInventory")) $name).Count -ne 1) {
            $failures.Add("Organizer native operation missing: $name")
        }
        foreach ($patch in $plugin.MainModule.Types | Where-Object { $_.Name.StartsWith("AntiCheat") }) {
            if ($patch.CustomAttributes | Where-Object {
                $_.AttributeType.FullName -eq "HarmonyLib.HarmonyPatch" -and
                @($_.ConstructorArguments | Where-Object { $_.Type.FullName -eq "System.String" -and $_.Value -ceq $name }).Count -gt 0
            }) { $failures.Add("Organizer native operation is now intercepted by $($patch.Name): $name") }
        }
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Output $_ }
        throw "Compatibility validation failed: $($failures.Count) issue(s)."
    }
    Write-Output "HARMONY METADATA VALIDATION PASSED"
    Write-Output "Targets: $targetCount; injected arguments: $parameterCount"
    Write-Output "Reflected members: $reflectionCount; join/create transpiler anchors and restart forwarding: verified"
    Write-Output "Organizer native swap/rotate commands: present and outside direct-write anti-cheat filters"
    Write-Output "Metadata inspection only; does not execute Unity or prove runtime behavior."
}
finally {
    foreach ($assembly in $assemblies.Values) { $assembly.Dispose() }
}
