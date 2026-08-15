# Sephiria Together

A unified host-side multiplayer framework for Sephiria 1.0.29. It provides configurable lobby capacity, mid-run joining, GUID reconnect, progress-gate bypass, catch-up rewards, and enemy health scaling.

The F8 menu follows the game's active language for Simplified Chinese, Traditional Chinese, English, Korean and Japanese. Other languages fall back to English.

## Default scaling

- 1-4 players: no change beyond Sephiria's original scaling.
- 5+ players: add 15% of the game's already-scaled enemy health per player above four.
- Hostile non-player units are affected, including normal enemies, elites and bosses. Units that become hostile after spawning are checked again when their faction changes.
- Enemy count and enemy damage are not changed.
- The extra multiplier is capped at 8x by default.

Examples:

| Players | Extra multiplier |
| ---: | ---: |
| 4 | 1.00x |
| 5 | 1.15x |
| 10 | 1.90x |
| 20 | 3.40x |
| 50 | 7.90x |

## In-game menu

The host can press `F8` to open the configuration panel. The player limit is a numeric field rather than a fixed mode; enter a value from 2 to 250 and apply it before creating the next lobby. The menu also configures progress admission, mid-run joining, catch-up rewards and enemy scaling.

## Installation

1. Install BepInEx 5 for Sephiria.
2. Remove `Sephiria50.dll`, `Sephiria50Scaling.dll` and `Sephiria100Coop.dll`.
3. Put `SephiriaTogether.dll` in `BepInEx/plugins` on the host.
4. Start the game once to generate `BepInEx/config/com.sephiriamods.sephiriatogether.cfg`.

The host must install the plugin. Clients receive synchronized enemy health from the server. Fresh mid-run joining does not require the plugin on clients; clients that need the game's automatic reconnect UI should also install it or use `-allow_rejoin`.

## Configuration

- `PlayerLimit`: lobby/network limit, from `2` to `250`. Default: `16`. Applied when creating the next lobby/server.
- `BaselinePlayers`: player count at or below which no extra multiplier is applied. Default: `4`.
- `HealthPerExtraPlayer`: health added per player above the baseline. Default: `0.15` (15%).
- `MaximumExtraMultiplier`: cap on this plugin's multiplier. Default: `8`. Use `0` for no cap.
- `AllowLowerProgressPlayers`: publish the host lobby as unrestricted by chapter and bypass the local chapter gate when joining. Default: `true`.
- `AllowMidRunJoin`: keep the Steam lobby open and accept fresh players after a dungeon run starts. On the host, it also enables the server's GUID-based reconnect path without requiring `-allow_rejoin`. Fresh joining clients do not need the plugin; clients that need the game's automatic reconnect UI should also install it or launch with `-allow_rejoin`. Default: `true`.
- `CatchUpExperienceRatio`: fresh mid-run players receive enough experience to reach this fraction of the other same-floor players' median cumulative experience. Default: `1` (100%). Use `0` to disable compensation.
- `ScaleEnemyCount`: increase enemy count for procedural multiplayer waves. Default: `true`.
- `EnemyCountPerExtraPlayer`: extra procedural-wave enemies per player above the baseline. Default: `0.08` (8%).
- `MaximumEnemyCountMultiplier`: cap for the additional procedural-wave count multiplier. Default: `3`.

`PlayerLimit` is applied when creating the next host/Lobby. Do not rely on changing it after a server is already listening. The F8 menu is host-only for gameplay settings; clients can install the plugin for reconnect UI compatibility but cannot configure the host.

The player status panel is calculated directly from server-owned player objects and requires no client plugin. It shows player name, level, HP and floor. Kick disconnects the selected server connection. Persistent Steam/GUID bans are intentionally not exposed until connection identities are cryptographically bound server-side.

For host-only reconnect, the plugin now uses the SteamID supplied by the server's FizzySteamworks transport to recover the player's existing run GUID. A client-side plugin is not required for the server to identify the reconnecting Steam account. The host must have run the updated plugin at least once while saving the session for the strongest inventory recovery; legacy `SaveVersion=0` runs remain unsupported for multi-player slot recovery.

Configuration changes take effect for newly spawned enemies. Restart the run after changing settings for consistent results.

When the host has this option enabled, players who have already unlocked multiplayer but have lower quest progress can use the Steam lobby list, room code or Steam invitation without installing the plugin themselves. The host publishes only the lobby's admission chapter as `0`; this plugin does not directly modify save data or quest progress.

The plugin keeps the version check and race-specific multiplayer block. Existing players reconnect with their server-side run slot and do not receive catch-up experience. A fresh player gets a new run-save slot so they cannot overwrite a disconnected player's inventory or progress.

Fresh mid-run players follow the game's existing floor join behavior: normally they enter at the host floor's spawn point; during a boss fight the game places them near the host. EXP catch-up uses the normal `AddExp` path, so level-up Sephirite choices, level-derived inventory expansion and other level-up effects are generated exactly as they are for earned experience. Weapons are never upgraded automatically because doing so would choose a build path for the player. Historical anvils, Miracles and Boss choice rewards require a dedicated entitlement ledger and per-player selection UI before they can be restored safely.

Fresh mid-run joining requires a current run using the game's per-player save format (`SaveVersion != 0`). Legacy single-player run saves are rejected for fresh mid-run joining to prevent overwriting existing run data. Set the options before starting the run; changing lobby-related options during an active run does not retroactively update Steam lobby metadata.

## Building

The project references assemblies from a local Sephiria installation and does not redistribute them:

```powershell
$env:SEPHIRIA_DIR = "E:\SteamLibrary\steamapps\common\Sephiria"
.\scripts\build.ps1
```

To build and copy the plugin directly into the game:

```powershell
dotnet build SephiriaTogether.csproj -c Release -p:GameDir="$env:SEPHIRIA_DIR" -p:DeployToGame=true
```

Create GitHub Release assets locally:

```powershell
.\scripts\package.ps1 -Version 3.2.0
```

This creates `artifacts/SephiriaTogether.dll` and `artifacts/SephiriaTogether-3.2.0.zip`. Game assemblies and BepInEx binaries are never included.

## Compatibility

- Built for Sephiria 1.0.29, Unity Mono, Windows x64.
- Requires BepInEx 5.
- Replaces the old `Sephiria50.dll`, `Sephiria50Scaling.dll` and `Sephiria100Coop.dll`.
- Game updates can change internal methods and require a rebuild.

## License

MIT. See `LICENSE`.
