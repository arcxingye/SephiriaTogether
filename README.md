# Sephiria Together

[简体中文](README.zh-CN.md) · English

A multiplayer framework for Sephiria. It provides Steam lobbies, TCP/IP rooms for offline environments, configurable capacity, mid-run joining, GUID reconnect, progress-gate bypass, catch-up rewards, and enemy scaling.

The F8 menu follows the game's active language for Simplified Chinese, Traditional Chinese, English, Korean and Japanese. Other languages fall back to English.

Current version: **3.10.1 for Sephiria 1.1.3**. Download links point to the latest published release.

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

When Backpack Organizer uses the same main key, Together temporarily uses **F9** (or the next free fallback) and displays the effective shortcut. F8 can continue sorting; adding Ctrl to F8 alone is not sufficient because the organizer accepts extra modifiers. Neither saved binding is changed. The Diagnostics tab reports detected compatibility; see [COMPATIBILITY.md](COMPATIBILITY.md) for Star's SephiriaMod and organizer scope.

When lower-progress joining is enabled, a client's earlier chapter no longer blocks the host from starting the next stage. This only bypasses the server's transition check and does not change the client's quest save. The optional **Do not require everyone at the entrance** setting lets the host use the correct stage entrance normally without gathering every living player nearby. It does not guess or directly force-load a stage.

## Installation

Two loader builds are available: `SephiriaTogether.dll` for BepInEx and `SephiriaTogether.Workshop.dll` for the game's native Workshop loader. Enable one build per game process. Both use the same gameplay features and custom network message layouts.

Download links:

- [Latest Release](https://github.com/arcxingye/SephiriaTogether/releases/latest)
- [Latest plugin ZIP](https://github.com/arcxingye/SephiriaTogether/releases/latest/download/SephiriaTogether.zip)
- `SephiriaTogether-<version>-Workshop.zip` is the native local-test/upload package; it extracts into `WorkshopLocal/SephiriaTogether` and is subject to the game's local-package restrictions.
- `SHA256SUMS-<version>.txt` lists checksums for release downloads.

The F8 menu's Diagnostics tab also opens the Release page and plugin ZIP links. The beginner package remains available from the Latest Release page for first-time installations.

For beginners, download the Release ZIP whose name contains `with-BepInEx`, then extract every file directly into the Sephiria game root. `winhttp.dll` and `Sephiria.exe` should end up in the same directory.

If BepInEx 5 is already installed:

1. Put `SephiriaTogether.dll` in `BepInEx/plugins` on the host.
2. Start the game once to generate `BepInEx/config/com.sephiriamods.sephiriatogether.cfg`.

See `INSTALL.md` for bilingual beginner instructions, or [README.zh-CN.md](README.zh-CN.md) for the Chinese feature/install overview.

The native package is produced by `scripts/build-workshop.ps1` under `artifacts/WorkshopLocal/SephiriaTogether`. Put the folder in the game's `WorkshopLocal` directory to inspect/upload it through the official Workshop panel. The DLL contains a public `SephiriaTogether.WorkshopEntry` entry and needs no BepInEx runtime. Native settings are saved through the game's `ModOptions` in device options; they are independent of the BepInEx `.cfg`. Recheck your F8 settings when switching loaders. See the [Workshop guide](https://github.com/arcxingye/SephiriaTogether/blob/main/workshop/README.md) for upload and testing instructions.

**Native-loader timing:** in Sephiria 1.1.3, Workshop DLL activation happens in `GameDataLoader.Awake` when entering a save. F8 is therefore unavailable on the first title screen of a fresh game launch. The Workshop runtime persists after activation; returning to the title does not intentionally destroy it. The BepInEx build initializes at process startup and retains first-title-screen access. The `OnTitleMenuReady` event alone cannot start an unloaded Workshop DLL.

### Session preparation

The two builds share gameplay code, but their startup capabilities differ:

| Capability | BepInEx build | Native Workshop build |
| --- | --- | --- |
| F8 on the first title screen | Available at startup | DLL has not loaded yet |
| In-game rules, scaling and rewards | Shared core | Shared core after activation |
| Select IP/native mode | Before entry or queue for the next session | Enter a save, configure in F8, return to title to apply, then re-enter |
| Start when the base game's own session cannot start | Early transport bootstrap is available | Cannot bootstrap code that has not loaded |

Network preferences are editable during play. The panel shows **Current transport** separately from **Saved for next session**; saving a preference never swaps a running connection. Use **Leave session and apply at title** to invoke the game's normal exit confirmation. A guest in a dungeon follows the native settlement flow. Once the session has ended at the title, preferences are applied and the network settings page reopens. Then load a save to host/join using the selected transport. This flow keeps the process running; restarting it would repeat native Workshop late activation.

Default host ports and explicit client destination ports are separate. Saving another default port does not change the port of an existing guest connection. Required Workshop-package checks remain in place. See `DESIGN.md` for lifecycle invariants and verification boundaries.

The host must install the plugin. Clients receive synchronized enemy health from the server. Fresh Steam mid-run joining does not require the plugin on clients. The host directly grants automatic catch-up and creates vanilla compensation objects such as Anvils, so those objects can be used without the client plugin; install the plugin for the F8 compensation status page, transfers, progress selection, rescue requests, or IP/LAN rooms. Sephiria Together version differences do not block joining; a temporary warning is shown for 20 seconds and then disappears. Game versions must match: updated game builds change the native network protocol, so a mismatched game version is rejected by the game's own authentication. Workshop packages remain subject to the game's own compatibility checks.

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

Before a main-story run enters the dungeon, the host can rebuild its starting progress from an online player's validated story progress or choose a specific normal story chapter from the manual dropdown. Internal transition, side-story, test, and multiplayer-blocked races are excluded. This changes only the current run's starting chapter and does not edit personal saves. The picker is disabled inside custom game modes. If an older client cannot send the Mod progress report, the host falls back to its synchronized main-quest progress; late progress without current quest-node data uses the corresponding normal chapter start.

The F8 Saves page can create a manual snapshot of the selected profile and its current-run TMP data. These snapshots are stored under `Documents/Saved Games/Sephiria/SephiriaTogetherBackups`, outside the filename pattern used by the game's rotating backups, so the game does not delete older mod snapshots. The save currently in use, automatic game backups, and manual backups created by this Mod can be activated from the same page. Activation requires confirmation, safely leaves the current session, waits for asynchronous saving to finish, creates a pre-restore snapshot, atomically replaces the selected slot, and reloads it from the title scene.

## IP and LAN rooms

The IP transport is selected only while networking is idle or at an explicit session-start boundary. Offline preferences default to TCP/IP and use `DirectConnect/OfflineEnabled`; Steam-online preferences use `DirectConnect/Enabled`. Preferences can be edited in-game and queued for the next session. The default TCP game port is `7777`. Native Workshop users follow the session-preparation flow above on each cold launch when the saved mode differs from the session that the base game already started.

After selecting a save, open the game's original multiplayer panel:

1. The host chooses **Create Room**, configures the room, and confirms. The host immediately enters the original joined-room page and MultiZone; no client is required to enter the room. After a successful, failed, or abandoned run, an active IP room keeps listening and returns the host to MultiZone for the next run.
2. A client can press **Refresh**. The Mod sends an on-demand discovery query to broadcast addresses and up to two same-subnet `/24` ranges. Confirmed IP rooms appear in the original room list as `LAN` entries.
3. A client can also press the original **Join** button and enter `IP` or `IP:port` manually.

Discovery uses UDP `7780`; gameplay uses the configured TCP port. Allow both through the firewall. The current client sends both the current discovery query and a `3.7.0` compatibility query, so rooms hosted by the previous Mod release can still appear. Automatic discovery requires a LAN or virtual network that permits peer-to-peer traffic. Route-only game accelerators may not expose peers to one another, so manual IP joining remains available.

IP players use Mirror-synchronized GUIDs, names, health/mana bars, and player lists without requiring Steam lobby identities. Different Sephiria Together versions can still connect, but both sides must run the same game version because updated game builds change Mirror serialization and command hashes. A `3.7.0` host can be discovered by a newer client after the compatibility query, yet joining still requires the same game version. IP rooms support the native custom-game selection and player cap. IP discovery does not exchange Workshop dependency lists; arrange matching required Workshop packages, versions, and load order before connecting.

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
- `DirectConnect/Enabled`: saved TCP/IP preference for Steam-online sessions. Editable during play; applied after the current session ends. Default: `false`.
- `DirectConnect/OfflineEnabled`: saved TCP/IP preference for offline sessions. Editable during play; applied while idle. Default: `true`.
- `DirectConnect/Port`: default TCP host/join port for the next session. Editing it never changes a running host's port or a guest's explicit connection target. Default: `7777`. LAN discovery uses UDP `7780`.

`PlayerLimit` is applied when creating the next host/Lobby. Do not rely on changing it after a server is already listening. The F8 menu is host-only for gameplay settings; clients can install the plugin for reconnect UI compatibility but cannot configure the host.

The player status panel is calculated directly from server-owned player objects and requires no client plugin. It shows player name, level, HP and floor. Kick disconnects the selected server connection. Persistent Steam/GUID bans are intentionally not exposed until connection identities are cryptographically bound server-side.

Steam rooms use the transport SteamID to recover existing run GUIDs. IP rooms fall back to the authenticated client GUID stored by the game. The host must have run the updated plugin at least once while saving the session for the strongest inventory recovery; legacy `SaveVersion=0` runs remain unsupported for multi-player slot recovery.

Configuration changes take effect for newly spawned enemies. Restart the run after changing settings for consistent results.

When the host has this option enabled, players who have already unlocked multiplayer but have lower quest progress can use the Steam lobby list, room code or Steam invitation without installing the plugin themselves. The host publishes only the lobby's admission chapter as `0`; this plugin does not directly modify save data or quest progress.

The plugin records game and Sephiria Together version differences for diagnostics. Differing Sephiria Together versions remain joinable, while a game-version mismatch is rejected by the game's native authentication. Existing players reconnect with their server-side run slot and do not receive catch-up experience. A fresh player gets a new run-save slot so they cannot overwrite a disconnected player's inventory or progress.

Fresh mid-run players follow the game's existing floor join behavior: normally they enter at the host floor's spawn point; during a boss fight the game places them near the host. EXP catch-up uses the normal `AddExp` path, so level-up Sephirite choices, level-derived inventory expansion and other level-up effects are generated exactly as they are for earned experience. Catch-up never selects a weapon build path for a player; selectable rewards remain in the host entitlement ledger until claimed through a vanilla compensation object or, for modded clients, the corresponding status flow.

Fresh mid-run joining requires a current run using the game's per-player save format (`SaveVersion != 0`). Legacy single-player run saves are rejected for fresh mid-run joining to prevent overwriting existing run data. Set the options before starting the run; changing lobby-related options during an active run does not retroactively update Steam lobby metadata.

## Building

The project references assemblies from a local Sephiria installation and does not redistribute them:

```powershell
$env:SEPHIRIA_DIR = "E:\SteamLibrary\steamapps\common\Sephiria"
.\scripts\build.ps1
.\scripts\validate-compatibility.ps1 -GameDir "$env:SEPHIRIA_DIR"
```

To build and copy the plugin directly into the game:

```powershell
dotnet build SephiriaTogether.csproj -c Release -p:GameDir="$env:SEPHIRIA_DIR" -p:DeployToGame=true
```

Create GitHub Release assets locally:

```powershell
 .\scripts\package.ps1 -Version 3.10.1 -IncludeWorkshop
```

This creates a standalone BepInEx DLL, a plugin-only ZIP, and a beginner ZIP containing the official BepInEx 5.4.23.5 Windows x64 distribution. `-IncludeWorkshop` also builds the separately named native Workshop ZIP; upload tickets and review proofs are excluded. SHA-256 checksums are generated for the assets. The script verifies the official BepInEx archive SHA-256 and includes its LGPL-2.1 license. Game assemblies are never included.

Build the native Workshop package (no automatic deployment or Steam upload):

```powershell
.\scripts\build-workshop.ps1 -GameDir "$env:SEPHIRIA_DIR"
```

The native DLL is built with `-p:Loader=Workshop` into `bin/Workshop/Release/netstandard2.1/SephiriaTogether.Workshop.dll`. Intermediate files are isolated from the BepInEx build. Package validation uses Mono.Cecil from the developer's BepInEx installation; it is not a runtime dependency and is not included in the package.

After uploading an update, separately verify its review status with `scripts/check-workshop-review.ps1 -PackageRoot "<original local upload folder>"`. This read-only tool computes the game's DLL-set fingerprint and queries the public review endpoint for the original Workshop ID. A successful Steam upload or an old `approved` ticket does not mean the updated DLL is approved. See `workshop/README.md` for review/F8 troubleshooting.

Run the adapter/policy tests (.NET SDK 9) and patch regression tests (.NET Framework 4.8 on Windows):

```powershell
dotnet run --project tests/WorkshopSupport.Tests/WorkshopSupport.Tests.csproj -c Release
dotnet run --project tests/NetworkSession.Tests/NetworkSession.Tests.csproj -c Release
dotnet run --project tests/PatchBinding.Tests/PatchBinding.Tests.csproj -c Release -p:GameDir="$env:SEPHIRIA_DIR"
```

## Compatibility

- Built and statically checked against Sephiria 1.1.3, Unity Mono, Windows x64. World-map overloads, handshake lifecycle and Steam transport-capacity paths have executable regression coverage; metadata checks also cover combat lifesteal, native Workshop activation and the new Boss timing/history paths. In-game loading, remote handshake, six-player admission and multi-Mod behavior on 1.1.3 still need live verification. The earlier user-confirmed menu test was Together 3.10.0 on game 1.1.2.
- The BepInEx build requires BepInEx 5 and is installed under `BepInEx/plugins`. The native build loads through `WorkshopModBase` and the game's official Workshop UI.
- Enable one loader build at a time, then restart. The native entry refuses duplicate loading, including older BepInEx instances, to avoid applying rewards/scaling twice.
- Steam joining retains the native Workshop package/version/load-order checks and the host-only-Mod notice. `AllowLowerProgressPlayers` affects story progress only. Native restrictions on loaded local Workshop packages also apply to IP creation/joining.
- Both sides must use the same game version and compatible gameplay packages. Host-only Sephiria Together features do not remove other packages' client requirements.
- Game updates can change internal methods and require a rebuild.
- Star 2.7.4 requires installation on all peers. Together retains its chosen room cap after Star's fixed-50 admission prefix and suspends story selection in Star's Boss Rush/Infinite modes. Backpack Organizer Workshop 3.0.0 uses native swap/rotate commands and needs no anti-cheat exemption. Targeted checks are complete; full three-Mod multiplayer regression is still pending.

## License

MIT. See `LICENSE`.
