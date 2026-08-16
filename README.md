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

When lower-progress joining is enabled, a client's earlier chapter no longer blocks the host from starting the next stage. This only bypasses the server's transition check and does not change the client's quest save. The optional **Do not require everyone at the entrance** setting lets the host use the correct stage entrance normally without gathering every living player nearby. It does not guess or directly force-load a stage.

## Installation

Download links:

- [Latest Release](https://github.com/arcxingye/SephiriaTogether/releases/latest)
- [Latest plugin ZIP](https://github.com/arcxingye/SephiriaTogether/releases/latest/download/SephiriaTogether.zip)

The F8 menu's Diagnostics tab also opens the Release page and plugin ZIP links. The beginner package remains available from the Latest Release page for first-time installations.

For beginners, download the Release ZIP whose name contains `with-BepInEx`, then extract every file directly into the Sephiria game root. `winhttp.dll` and `Sephiria.exe` should end up in the same directory.

If BepInEx 5 is already installed:

1. Put `SephiriaTogether.dll` in `BepInEx/plugins` on the host.
2. Start the game once to generate `BepInEx/config/com.sephiriamods.sephiriatogether.cfg`.

See `INSTALL.md` for bilingual beginner instructions.

The host must install the plugin. Clients receive synchronized enemy health from the server. Fresh mid-run joining does not require the plugin on clients; clients that need the game's automatic reconnect UI should also install it or use `-allow_rejoin`.

The optional delayed healing is host-authoritative. Players do not heal for 10 seconds after taking damage, then recover at a fixed 1 HP/s, including during combat. It uses the game's normal HP synchronization, so clients do not need the plugin.

Fresh mid-run players are caught up to the same-floor party median for money, current dice, and maximum dice, receive expansions from missed Inventory Storage floors, and receive their own Dimension Pocket items. These grants are host-side and do not require the client plugin. If both host and client use Sephiria Together, the client menu also exposes host-validated weapon upgrades, enchants, Charms, Miracles, Stone Tablets, and boss reward choices inferred from missed route floors.

Disconnected players receive the same route-difference catch-up if they rejoin after the party has advanced. Immediate rejoins with no missed floors receive nothing, and Dimension Pocket items are never granted again on rejoin.

Weapon-upgrade, enchant, Charm, Miracle, Stone Tablet, and boss reward entitlements are stored by the host in the current run under a hashed player identity. Pending choices survive disconnects and game restarts, counted route floors cannot generate the same entitlement twice, and every successful claim is deducted and saved immediately. The client F8 panel shows host confirmation and the number of choices already claimed this run. Miracle catch-up offers a stable host-generated set of three choices. Charm, Tablet, and boss claims create vanilla owner-authority Sephirite rewards, so their final item selection still uses the game's normal server validation.

The base game does not save the exact unclaimed Miracle, Tablet, or boss candidate payload after a floor is unloaded. Historical catch-up therefore creates a deterministic equivalent offer; it does not claim to restore the exact candidates that were visible on the missed floor.

Missed HP floors apply the vanilla 60% heal, Max HP floors grant the vanilla `MAX_HP_NORATIO/20` status and heal 20 HP, Sapphire floors grant one run Sapphire, and inventory floors grant one slot up to the vanilla cap. These use vanilla synchronized server state and work for clients without the plugin. The host never sends custom compensation messages to a client until that client completes the Sephiria Together handshake; selectable entitlements for unmodded clients remain saved instead of being discarded or chosen automatically.

The menu is organized into Rules, Compensation, Diagnostics, and History tabs. Its shortcut is configurable through the BepInEx setting `Interface/MenuShortcut`, defaulting to `F8`; modifier keys are supported by the BepInEx `KeyboardShortcut` format.

Modded clients show a prominent banner when a teammate's synchronized `IsDead` state changes to downed. A downed player can press the configurable `Interface/RescueShortcut` (default `R`) to ask other modded players for rescue. The host verifies that the sender is actually downed and limits requests to once every 10 seconds. Unmodded clients receive no custom rescue messages.

The optional `Multiplayer/AutoReviveWhenClear` setting is host-authoritative and works for unmodded clients. When no living hostile non-dummy units remain and surviving players are out of combat for two seconds, all downed players revive at 50% max HP. If the final player falls after the room is already clear, the same enemy check runs before vanilla game over and revives the party immediately; active enemies always preserve the original game-over behavior.

## Configuration

- `PlayerLimit`: lobby/network limit, from `2` to `250`. Default: `16`. Applied when creating the next lobby/server.
- `BaselinePlayers`: player count at or below which no extra multiplier is applied. Default: `4`. Set it to `0` in advanced settings to test scaling with one player.
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

Fresh mid-run players follow the game's existing floor join behavior: normally they enter at the host floor's spawn point; during a boss fight the game places them near the host. EXP catch-up uses the normal `AddExp` path, so level-up Sephirite choices, level-derived inventory expansion and other level-up effects are generated exactly as they are for earned experience. Weapons are never upgraded automatically because doing so would choose a build path for the player; selectable rewards are retained in the host entitlement ledger for the client menu.

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
.\scripts\package.ps1 -Version 3.4.0
```

This creates a standalone DLL, a plugin-only ZIP, and a beginner ZIP containing the official BepInEx 5.4.23.5 Windows x64 distribution. The script verifies the official BepInEx archive SHA-256 and includes its LGPL-2.1 license. Game assemblies are never included.

## Compatibility

- Built for Sephiria 1.0.29, Unity Mono, Windows x64.
- Requires BepInEx 5.
- Game updates can change internal methods and require a rebuild.

## License

MIT. See `LICENSE`.
