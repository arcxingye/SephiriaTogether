# Sephiria Together

A multiplayer framework for Sephiria. It provides Steam lobbies, TCP/IP rooms for offline environments, configurable capacity, mid-run joining, GUID reconnect, progress-gate bypass, catch-up rewards, and enemy scaling.

The F8 menu follows the game's active language for Simplified Chinese, Traditional Chinese, English, Korean and Japanese. Other languages fall back to English.

## Default scaling

- 1-4 players: no change beyond Sephiria's original scaling.
- 5+ players: add 15% of the game's already-scaled enemy health per player above four.
- Hostile non-player units are affected, including normal enemies, elites and bosses. Units that become hostile after spawning are checked again when their faction changes.
- Optional enemy-count scaling preserves the original procedural phase count and increases each phase's simultaneous spawn count instead of extending combat with many small pseudo-waves. The active-enemy limit is raised to the scaled phase size, capped at 32 for safety. Enemy damage is not changed by this setting.
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

Press `F8` to open the configuration panel. The player limit is a numeric field rather than a fixed mode; enter a value from 2 to 250 and apply it before creating the next lobby. The menu also configures progress admission, mid-run joining, catch-up rewards, enemy scaling, saves, transfers, and IP transport settings.

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

The host must install the plugin. Clients receive synchronized enemy health from the server. Fresh Steam mid-run joining does not require the plugin on clients. The host directly grants automatic catch-up and creates vanilla compensation objects such as Anvils, so those objects can be used without the client plugin; install the plugin for the F8 compensation status page, transfers, progress selection, rescue requests, or IP/LAN rooms. Game and Mod version differences no longer block joining; a temporary warning is shown for 20 seconds and then disappears.

`EnableAntiCheat` is intentionally basic. When enabled by the host, it blocks direct remote money mutations and explicit direct inventory writes; native rewards and other game interactions remain under the original game flow. The host can handle any other suspicious behavior through the native kick interface.

The optional delayed healing is host-authoritative. Players do not heal for 10 seconds after taking damage, then recover at a fixed 1 HP/s, including during combat. It uses the game's normal HP synchronization, so clients do not need the plugin.

Fresh mid-run players are caught up to the same-floor party median for money, current dice, and maximum dice, receive expansions from missed Inventory Storage floors, and receive their own Dimension Pocket items. These grants are host-side and do not require the client plugin. Vanilla catch-up objects, including Anvils, are also created by the host and can be used by unmodded clients. If both host and client use Sephiria Together, the client menu additionally exposes host-validated weapon upgrades, enchants, Charms, Miracles, Stone Tablets, and boss reward choices inferred from missed route floors.

Disconnected players receive the same route-difference catch-up if they rejoin after the party has advanced. Immediate rejoins with no missed floors receive nothing, and Dimension Pocket items are never granted again on rejoin.

Weapon-upgrade, enchant, Charm, Miracle, Stone Tablet, Tablet Fusion, and boss reward entitlements are stored by the host in the current run under a hashed player identity. Pending choices survive disconnects and game restarts, counted route floors cannot generate the same entitlement twice, and every successful claim is deducted and saved immediately. The F8 panel is available to modded clients and shows host confirmation plus the number of choices already claimed this run. Vanilla clients can still use the host-spawned original compensation objects. Miracle catch-up offers a stable host-generated set of three choices. Charm, Tablet, and boss claims create vanilla owner-authority Sephirite rewards, so their final item selection still uses the game's normal server validation.

Selectable catch-up is delivered through vanilla network objects placed near the player: Anvil, Enchant altar, Miracle selector, Charm Sephirite, Tablet Sephirite, Tablet Combiner, and boss reward choices. The menu is status-only and does not choose or generate rewards. Catch-up objects are visible only to their target player, and unmodded clients can use these original objects and interfaces; the host consumes an entitlement only after the vanilla completion command succeeds. Tablet Combiner compensation remains player-owned, preserves the normal money cost and tablet validation, and is consumed only after a successful fusion.

The base game does not save the exact unclaimed Miracle, Tablet, or boss candidate payload after a floor is unloaded. Historical catch-up therefore creates a deterministic equivalent offer; it does not claim to restore the exact candidates that were visible on the missed floor.

Missed HP floors apply the vanilla 60% heal, Max HP floors grant the vanilla `MAX_HP_NORATIO/20` status and heal 20 HP, Sapphire floors grant one run Sapphire, and inventory floors grant one slot up to the vanilla cap. These use vanilla synchronized server state and work for clients without the plugin. The host does not send custom menu or claim messages to an unmodded client; it still creates the applicable vanilla compensation objects, and the host-side entitlement remains until the original object is successfully claimed.

The menu is organized into Rules, Compensation, Diagnostics, History, Saves, and Transfer tabs. Rules contains host multiplayer, scaling, player controls, and IP transport settings. Transfer lets a modded sender choose any other online player, enter a whole Leaf amount, and confirm it. The host validates the sender identity, target, positive amount, balance, overflow, and request rate before atomically moving the Leaves; recipients do not need the mod. The menu shortcut is configurable through `Interface/MenuShortcut`, defaulting to `F8`; modifier keys are supported by the BepInEx `KeyboardShortcut` format.

Modded clients show a prominent banner when a teammate's synchronized `IsDead` state changes to downed. A downed player can press the configurable `Interface/RescueShortcut` (default `R`) to ask other modded players for rescue. The host verifies that the sender is actually downed and limits requests to once every 10 seconds. Unmodded clients receive no custom rescue messages.

The optional `Multiplayer/AutoReviveWhenClear` setting is host-authoritative and works for unmodded clients. When no living hostile non-dummy units remain and surviving players are out of combat for two seconds, all downed players revive at 50% max HP. If the final player falls after the room is already clear, the same enemy check runs before vanilla game over and revives the party immediately; active enemies always preserve the original game-over behavior.

Before a run enters the dungeon, the host can rebuild its starting progress from an online player's validated story progress or choose a specific normal story chapter from the manual dropdown. Internal transition, side-story, test, and multiplayer-blocked races are excluded. This changes only the current run's starting chapter and does not edit personal saves. If an older client cannot send the Mod progress report, the host falls back to its synchronized main-quest progress; late progress without current quest-node data uses the corresponding normal chapter start.

The F8 Saves page can create a manual snapshot of the selected profile and its current-run TMP data. These snapshots are stored under `Documents/Saved Games/Sephiria/SephiriaTogetherBackups`, outside the filename pattern used by the game's rotating backups, so the game does not delete older mod snapshots. The save currently in use, automatic game backups, and manual backups created by this Mod can be activated from the same page. Activation requires confirmation, safely leaves the current session, waits for asynchronous saving to finish, creates a pre-restore snapshot, atomically replaces the selected slot, and reloads it from the title scene.

## IP and LAN rooms

The IP transport is installed before the first network session. If Steam is not logged in, TCP/IP is enabled automatically. Steam users can enable `DirectConnect/Enabled` in the F8 menu or config; changes apply before a room starts, while an active room or game session cannot hot-switch transport. The default TCP game port is `7777`.

After selecting a save, open the game's original multiplayer panel:

1. The host chooses **Create Room**, configures the room, and confirms. The host immediately enters the original joined-room page and MultiZone; no client is required to enter the room. After a successful, failed, or abandoned run, an active IP room keeps listening and returns the host to MultiZone for the next run.
2. A client can press **Refresh**. The Mod sends an on-demand discovery query to broadcast addresses and up to two same-subnet `/24` ranges. Confirmed IP rooms appear in the original room list as `LAN` entries.
3. A client can also press the original **Join** button and enter `IP` or `IP:port` manually.

Discovery uses UDP `7780`; gameplay uses the configured TCP port. Allow both through the firewall. The current client sends both the current discovery query and a `3.7.0` compatibility query, so rooms hosted by the previous Mod release can still appear. Automatic discovery requires a LAN or virtual network that permits peer-to-peer traffic. Route-only game accelerators may not expose peers to one another, so manual IP joining remains available.

IP players use Mirror-synchronized GUIDs, names, health/mana bars, and player lists without requiring Steam lobby identities. Different game or Mod versions can connect when the host has the compatibility patch. A `3.7.0` host can be discovered by a `3.8.0` client after the compatibility query. For a manually entered or legacy-discovered `1.0.29` host, a `1.0.30` client retries the native authentication once with the host's game version; the old host still cannot provide newer custom protocol features.

## Configuration

- `PlayerLimit`: lobby/network limit, from `2` to `250`. Default: `16`. Applied when creating the next lobby/server.
- `BaselinePlayers`: player count at or below which no extra multiplier is applied. Default: `4`. Set it to `0` in advanced settings to test scaling with one player.
- `HealthPerExtraPlayer`: health added per player above the baseline. Default: `0.15` (15%).
- `MaximumExtraMultiplier`: cap on this plugin's multiplier. Default: `8`. Use `0` for no cap.
- `AllowLowerProgressPlayers`: publish the host lobby as unrestricted by chapter and bypass the local chapter gate when joining. Default: `true`.
- `AllowMidRunJoin`: keep the Steam lobby open and accept fresh players after a dungeon run starts. On the host, it also enables the server's GUID-based reconnect path without requiring `-allow_rejoin`. Fresh joining clients do not need the plugin; clients that need the game's automatic reconnect UI should also install it or launch with `-allow_rejoin`. Default: `true`.
- `AllowAttackingMerchants`: allow players and their followers to damage merchants. The host enforces this rule and clients do not need the plugin. Default: `false`.
- `EnableAntiCheat`: enable the basic host-side filter for direct remote money changes and direct item writes. Native rewards and other interactions remain native; the host handles other abuse by kicking the player. Clients do not need the plugin. Default: `false`.
- `ScaleEnemyCount`: increase enemy count for procedural multiplayer waves. Default: `true`.
- `EnemyCountPerExtraPlayer`: extra procedural-wave enemies per player above the baseline. Default: `0.08` (8%).
- `MaximumEnemyCountMultiplier`: cap for the additional procedural-wave count multiplier. Default: `3`.
- `DirectConnect/Enabled`: enable TCP/IP rooms while no network session is active. Offline environments enable IP automatically. Default: `false`.
- `DirectConnect/Port`: TCP game port. It can be changed before a session starts; an active session keeps its current transport and port. Default: `7777`. LAN discovery uses UDP `7780`.

`PlayerLimit` is applied when creating the next host/Lobby. Do not rely on changing it after a server is already listening. The F8 menu is host-only for gameplay settings; clients can install the plugin for reconnect UI compatibility but cannot configure the host.

The player status panel is calculated directly from server-owned player objects and requires no client plugin. It shows player name, level, HP and floor. Kick disconnects the selected server connection. Persistent Steam/GUID bans are intentionally not exposed until connection identities are cryptographically bound server-side.

Steam rooms use the transport SteamID to recover existing run GUIDs. IP rooms fall back to the authenticated client GUID stored by the game. The host must have run the updated plugin at least once while saving the session for the strongest inventory recovery; legacy `SaveVersion=0` runs remain unsupported for multi-player slot recovery.

Configuration changes take effect for newly spawned enemies. Restart the run after changing settings for consistent results.

When the host has this option enabled, players who have already unlocked multiplayer but have lower quest progress can use the Steam lobby list, room code or Steam invitation without installing the plugin themselves. The host publishes only the lobby's admission chapter as `0`; this plugin does not directly modify save data or quest progress.

The plugin records version differences for diagnostics but does not block them. Existing players reconnect with their server-side run slot and do not receive catch-up experience. A fresh player gets a new run-save slot so they cannot overwrite a disconnected player's inventory or progress.

Fresh mid-run players follow the game's existing floor join behavior: normally they enter at the host floor's spawn point; during a boss fight the game places them near the host. EXP catch-up uses the normal `AddExp` path, so level-up Sephirite choices, level-derived inventory expansion and other level-up effects are generated exactly as they are for earned experience. Catch-up never selects a weapon build path for a player; selectable rewards remain in the host entitlement ledger until claimed through a vanilla compensation object or, for modded clients, the corresponding status flow.

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
 .\scripts\package.ps1 -Version 3.9.0
```

This creates a standalone DLL, a plugin-only ZIP, and a beginner ZIP containing the official BepInEx 5.4.23.5 Windows x64 distribution. The script verifies the official BepInEx archive SHA-256 and includes its LGPL-2.1 license. Game assemblies are never included.

## Compatibility

- Built and tested against Sephiria 1.0.30, Unity Mono, Windows x64.
- Requires BepInEx 5.
- Supports the Steam build and tested offline builds that expose compatible networking APIs; version admission is non-blocking, but changed game internals can still make a pairing unusable.
- Game updates can change internal methods and require a rebuild.

## License

MIT. See `LICENSE`.
