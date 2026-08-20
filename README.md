# Sephiria Together

A unified host-side multiplayer framework for Sephiria 1.0.29. It provides configurable lobby capacity, mid-run joining, GUID reconnect, progress-gate bypass, catch-up rewards, and enemy health scaling.

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

Weapon-upgrade, enchant, Charm, Miracle, Stone Tablet, Tablet Fusion, and boss reward entitlements are stored by the host in the current run under a hashed player identity. Pending choices survive disconnects and game restarts, counted route floors cannot generate the same entitlement twice, and every successful claim is deducted and saved immediately. The client F8 panel shows host confirmation and the number of choices already claimed this run. Miracle catch-up offers a stable host-generated set of three choices. Charm, Tablet, and boss claims create vanilla owner-authority Sephirite rewards, so their final item selection still uses the game's normal server validation.

Selectable catch-up is delivered through vanilla network objects placed near the player: Anvil, Enchant altar, Miracle selector, Charm Sephirite, Tablet Sephirite, Tablet Combiner, and boss reward choices. The menu is status-only and does not choose or generate rewards. Catch-up objects are visible only to their target player, and unmodded clients can use these original objects and interfaces; the host consumes an entitlement only after the vanilla completion command succeeds. Tablet Combiner compensation remains player-owned, preserves the normal money cost and tablet validation, and is consumed only after a successful fusion.

The base game does not save the exact unclaimed Miracle, Tablet, or boss candidate payload after a floor is unloaded. Historical catch-up therefore creates a deterministic equivalent offer; it does not claim to restore the exact candidates that were visible on the missed floor.

Missed HP floors apply the vanilla 60% heal, Max HP floors grant the vanilla `MAX_HP_NORATIO/20` status and heal 20 HP, Sapphire floors grant one run Sapphire, and inventory floors grant one slot up to the vanilla cap. These use vanilla synchronized server state and work for clients without the plugin. The host never sends custom compensation messages to a client until that client completes the Sephiria Together handshake; selectable entitlements for unmodded clients remain saved instead of being discarded or chosen automatically.

The menu is organized into Rules, Autopilot, Compensation, Diagnostics, History, Saves, and Transfer tabs. Rules contains host multiplayer, scaling, and player controls; Autopilot contains only local AFK controls and presets and is identical for hosts and clients. Transfer lets a modded sender choose any other online player, enter a whole Leaf amount, and confirm it. The host validates the sender identity, target, positive amount, balance, overflow, and request rate before atomically moving the Leaves; recipients do not need the mod. The menu shortcut is configurable through `Interface/MenuShortcut`, defaulting to `F8`; modifier keys are supported by the BepInEx `KeyboardShortcut` format.

Modded clients show a prominent banner when a teammate's synchronized `IsDead` state changes to downed. A downed player can press the configurable `Interface/RescueShortcut` (default `R`) to ask other modded players for rescue. The host verifies that the sender is actually downed and limits requests to once every 10 seconds. Unmodded clients receive no custom rescue messages.

The optional `Multiplayer/AutoReviveWhenClear` setting is host-authoritative and works for unmodded clients. When no living hostile non-dummy units remain and surviving players are out of combat for two seconds, all downed players revive at 50% max HP. If the final player falls after the room is already clear, the same enemy check runs before vanilla game over and revives the party immediately; active enemies always preserve the original game-over behavior.

Press `F9` to toggle conservative AFK autopilot. It follows the host or nearest living teammate on the same floor, searches the full room for enemies, attacks them, and picks up nearby items only after the vanilla inventory-capacity check passes. Combat reads the equipped runtime weapon after every upgrade: bows charge and release, crossbows/magic staffs/golems hold ranged fire while maintaining a safe standoff, and each melee family approaches its own working reach. Upgraded secondary modes use their original state, MP/ammo/stack gates, charge or warmup time, and animation input. Ready quick-slot skills in slots 1-5 still use their original ammo, cooldown, mana, and cast validation. A host or solo player approaches explicit vanilla next-stage entrances after the room is clear. At a dungeon down stair it selects only a connected route node with higher progress through the original world-map travel path; clients never initiate transitions, and all original quest, lock, and gathering checks remain active. It pauses and releases held actions while any choice UI is open.

The local attack-mode setting has four choices: Left only, Prefer left, Right only, and Prefer right. Prefer left sustains normal attacks and inserts usable weapon specials; Prefer right prioritizes the equipped weapon's secondary input, uses normal attacks while the original MP, Fury, stack, combo, range, or weapon-state requirements are unavailable, and switches back as soon as the special becomes usable. The two Only modes never issue the opposite offensive weapon input. Ready quick-slot skills, automatic defense, rescue guard, and hazard evasion remain independent.

The F8 Saves page can create a manual snapshot of the selected profile and its current-run TMP data. These snapshots are stored under `Documents/Saved Games/Sephiria/SephiriaTogetherBackups`, outside the filename pattern used by the game's rotating backups, so the game does not delete older mod snapshots. Active slots, original game backups, and independent snapshots can be activated from the same page. Activation requires confirmation, safely leaves the current session, waits for asynchronous saving to finish, creates a pre-restore snapshot, atomically replaces the selected slot, and reloads it from the title scene.

While autopilot is enabled, local manual aim, primary-fire, and secondary-fire input callbacks are suppressed. Autopilot continuously writes its enemy, incoming-threat, rescue, or travel aim after both player-input and weapon updates, so an in-window mouse cursor cannot rotate the weapon on the next frame. The mouse cursor and F8 menu remain usable. Disabling autopilot clears the aim lock and restores the original input callbacks immediately.

In multiplayer, autopilot prioritizes a reachable downed teammate on the same floor over enemies. It first requests a PathGrid route; a teammate behind a closed combat-room gate normally has no route, so that rescue is postponed for five seconds while autopilot fights locally instead of walking into a wall forever. Reachable targets are approached through normal pathfinding. Sword-and-shield holds vanilla guard and locks aim toward the nearest threat while traveling and throughout the original delayed `RevivePlayerByInteraction` channel. If knockback moves the rescuer more than 0.3 meters or outside interaction range, the original interaction is stopped immediately; autopilot keeps the target, approaches again, and restarts the revive from zero. Death, opening a UI, disabling autopilot, a vanished target, or a lost route also cancels the active channel safely.

The optional local auto-optimize inventory setting calls the game's server-authoritative best-Charm-level arranger after inventory contents remain stable for two seconds while autopilot is out of combat. The original solver can move every regular inventory item and rotate rotatable Stone Tablets. It scores enabled Charms, effective levels, disabled Charms, and negative levels, and keeps only improving layouts. Ordinary items are moved when they help Tablet conditions or make room for a higher-scoring Charm layout; items without positional effects have no independent notion of a best slot.

The optional automatic guard and parry assistant is input-only. It uses sword-and-shield guard, dagger parry/Fury, defensive legacy-katana forms, and the quarterstaff's guarded special phases. Greatsword and purely offensive katana/staff forms evade because their secondary action does not provide a vanilla guard. It predicts nearby enemy windup/fire phases, hostile active melee collisions, and approaching hostile projectiles, then supplies the appropriate vanilla input. Vanilla movement state, resources, animation events, guard/parry windows, latency handling, and server authority still decide whether defense succeeds. The mod never starts guard or invulnerability directly and never changes damage in an ApplyDamage hook.

Hostile circular and elliptical ground warnings take priority over combat, rescue, and blocking. If the player stands inside one, autoplay selects a PathGrid-reachable point beyond the inflated warning radius and walks out immediately; after 65% of the warning time, it also requests a vanilla dash toward that safe point. This handles Mole Chieftain's phase-two rocks, which snapshot the target position and impact about one second later without producing an approaching projectile. Weapons without a defensive secondary action therefore evade instead of wasting right-click.

Autopilot choice handling is local and configurable in F8: prefer presets, prefer favorites, or always wait. Reward, weapon-upgrade, and Miracle presets have separate vanilla-name dropdowns. The weapon dropdown reads the equipped weapon and shows its complete reachable enhancement tree with upgrade-depth labels. A final upgrade can be selected at the start; at an earlier Anvil, autopilot chooses the immediate candidate whose descendant tree contains that highest-priority target. Reopening F8 refreshes the available weapon and Miracle choices. Added choices appear as ordered removable tags; their order is the matching priority. If a reward has no preferred match, autopilot randomly chooses among its highest-rarity candidates. Always wait leaves ordinary rewards unclaimed. Weapon upgrades and Miracles remain preset-only, so an unmatched choice is left unclaimed rather than selecting an irreversible build path at random.

Reward choice strategy does not disable weapon presets. On an Anvil event floor with a weapon preset and a non-maxed weapon, autopilot holds the floor while the vanilla network Anvil initializes, then approaches and opens it. The next-floor action remains blocked until a matching upgrade is selected or the generated Anvil choices are explicitly resolved as unmatched.

If an opened Anvil has no candidate matching or leading to the ordered weapon presets, autopilot uses `UI_WeaponEnhancementPanel.Reroll()`, preserving the original dice deduction, seed progression, candidate generation, sound, UI refresh, and server synchronization. It continues until a preset route appears or reroll dice reach zero. Rerolls are not spent when the current weapon has no hidden alternatives beyond the visible choice count. Only after all usable rerolls fail is that Anvil left unclaimed and the floor released.

Miracle presets use stable `miracle:<id>` values and default to `miracle:Hunter` (Hunter/猎人). An empty list skips Miracle selectors without spending dice. With configured targets, autopilot checks candidates in preset order and uses the vanilla network reroll path until a match appears or synchronized reroll dice reach zero; the final unmatched offer is then closed and left unclaimed. Remote clients wait for the host's replacement candidate payload before making another decision, so one accepted reroll consumes exactly one die.

Full-inventory reward handling is separately configurable: wait, replace only a lower-rarity unfavorited Charm, or also replace ordinary items. Replacement is considered only for Charm or Stone Tablet rewards and only when vanilla reports a genuinely full inventory. The solver first applies conservative rarity and protection rules. In ordinary-item mode, if no conservative candidate exists, it forces the least-loss legally droppable single item so a preferred reward does not block future choices. Ranking is ordinary items, inactive Charms, active Charms, Tablets, then favorite/preset matches. Tablets within their class are ranked by a read-only virtual removal: the tablet's applied level, disable, ignore-criteria, and multiplier contributions are subtracted from the current matrices, then all Charms are rescored with the vanilla weights. A negative contribution Tablet is discarded first; rarity and cost only break contribution ties. Non-throwable, bound, stacked, potion-belt, destroyed-on-discard, identifiable, and startup/core items remain hard-protected. The old item uses the vanilla ground-drop path, its instance ID is ignored by autopilot for the rest of that floor, and the reward enters the vacated slot. If no legally droppable single item exists, the reward must still wait.

Next-floor event priority is also configurable with vanilla-localized removable tags. When several directly connected forward branches exist at the next route progress, autopilot selects the first matching event priority. If none match, it chooses one of those branches at random. It never selects a backward connection or skips over a nearer route progress. Once the equipped weapon is fully enhanced, Anvil branches are excluded whenever a non-Anvil forward branch exists. If every forward branch is an Anvil, autopilot may enter one to keep the run progressing, but it still ignores the Anvil interaction inside.

The default next-floor priority is Miracle, Anvil, Inventory Expansion, the game's localized Charm item type (神器 in Simplified Chinese), then EXP. Existing installations still using the previous exact default are migrated once; custom priorities and intentionally empty lists are preserved.

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
 .\scripts\package.ps1 -Version 3.6.0
```

This creates a standalone DLL, a plugin-only ZIP, and a beginner ZIP containing the official BepInEx 5.4.23.5 Windows x64 distribution. The script verifies the official BepInEx archive SHA-256 and includes its LGPL-2.1 license. Game assemblies are never included.

## Compatibility

- Built for Sephiria 1.0.29, Unity Mono, Windows x64.
- Requires BepInEx 5.
- Game updates can change internal methods and require a rebuild.

## License

MIT. See `LICENSE`.
