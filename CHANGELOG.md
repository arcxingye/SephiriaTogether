# Changelog

## 3.5.2

- Add host-validated player-to-player Leaf transfers with an F8 recipient list, amount entry, confirmation, balance checks, atomic server updates, persistence, and sender/recipient notifications.
- Allow authenticated clients to transfer Leaves to the host or another client without relying on catch-up handshake state or a remote-only recipient connection.
- Add an F8 save manager for independent snapshots, vanilla backup discovery, pre-restore snapshots, atomic replacement, and immediate reload from the selected profile.
- Expand autoplay attack preference to Left only, Prefer left, Right only, and Prefer right while keeping skills, defense, and evasion independent.
- Add ordered Miracle presets with vanilla-localized choices, vanilla rerolls, empty-preset skipping, and leave-unclaimed behavior after rerolls are exhausted.
- Default Miracle priority to Hunter and next-floor priority to Miracle, Anvil, Inventory Expansion, Charm, then EXP, migrating only the previous exact floor default.
- Prioritize available personal Miracle selectors over the quest board so town compensation offers are opened and resolved before starting another commission.
- Complete additional Grassland quest objectives and return through state- and connection-based floor routing instead of object-name-specific stairs.
- Improve Boss trigger navigation, large-collider targeting, and warning-based laser, projectile, bomb, rectangle, ellipse, and circle evasion.
- Read laser projectile range from its movement module instead of the prefab's unexpanded collider, preventing ranged greatswords from moving into melee range.
- Recover from blocked spawn cells by retaining the first escape waypoint and temporarily disabling path smoothing after a no-progress check.
- Keep local hosts and solo players out of remote missed-reward tracking, recognize both Tablet reward variants, and make autoplay resolve the vanilla Tablet altar before leaving the floor.
- Run automatic inventory arrangement only after a genuinely idle two-second window, avoiding saves, UI, travel, rewards, and quest floors.

## 3.5.1

- Extend AFK autopilot through Grassland quest-board BattleZones and automatically select the Boss node after the required quest events are cleared.
- Fix repeated reward and inventory-arrangement work during quest completion, reducing frame stalls and post-battle freezes.
- Improve Anvil detection for players joining another host, including runtime floor-event detection and client-side Anvil discovery.
- Prevent quest floors from being treated as ordinary Charm compensation floors, avoiding repeated rewards after restarting an unfinished quest.

## 3.5.0

- Add a conservative F9 AFK autopilot that follows the same-floor host, attacks nearby enemies, collects nearby drops, lets a host or solo player use explicit vanilla next-stage entrances after clearing, and never consumes a reward when no favorite choice is available.
- Let solo autopilot search the full room and advance through dungeon down stairs by selecting a directly connected higher-progress route node.
- Add local F8 choice policies to prefer presets, prefer heart favorites, or always wait, with ordered stable-ID matching and a random highest-rarity reward fallback for the two prefer modes.
- Route autopilot movement through the game's PathGrid with smoothed waypoints, moving-target recalculation, and stuck detection instead of walking directly into walls.
- Replace free-text autopilot presets with separate vanilla-localized reward and weapon-upgrade dropdowns, ordered removable tags, and stable ID-based matching.
- Make autopilot ignore Anvil interactions at full enhancement and avoid Anvil floor branches when alternatives exist, while still allowing an all-Anvil route to progress.
- Add a vanilla-localized next-floor event priority list; matching forward branches are preferred and unmatched branches use a random fallback.
- Let autopilot rotate normal attacks with vanilla dash, secondary weapon actions, and ready quick-slot 1-5 magic or active skills using original cooldown and resource validation.
- Redesign autopilot presets into compact reward, weapon, and route tabs with wrapping removable tags and cleaned vanilla event labels.
- Navigate into the vanilla BossSpawner detection area on uncleared Boss floors so autopilot starts the encounter instead of waiting at spawn.
- Filter placeholder and unresolved localization-key rewards from preset dropdowns, use explicit localized floor-event labels, and disable horizontal menu scrolling.
- Use the game's localized item-type names in menu labels and descriptions instead of the mod's inconsistent literal Charm and Tablet translations.
- Limit weapon presets to the equipped weapon's complete reachable enhancement tree, label upgrade depth, and route earlier Anvil choices toward a selected final upgrade.
- Default next-floor priority to Anvil, Inventory Expansion, Charm/神器, then EXP, with a one-time migration for existing empty configurations.
- Disable and dim the reward preset tab when the active strategy prefers favorites or always waits, preventing misleading reward configuration.
- Add an optional out-of-combat auto inventory optimizer that invokes the game's server-authoritative best-Charm-level arranger after contents stabilize, including item swaps and Tablet rotation.
- Add configurable full-inventory reward replacement with protected item classes, rarity safeguards, and vanilla drop-then-claim behavior.
- Make ordinary-item full-inventory mode force a least-loss legal replacement when conservative candidates run out, and ignore the dropped instance for the rest of the floor.
- Rank Tablet discard candidates by a read-only virtual removal score using their applied level, disable, ignore-criteria, and multiplier contributions with the vanilla Charm scoring weights.
- Add optional input-only automatic sword-and-shield guard and dagger parry prediction from attack phases, hostile melee collisions, and approaching projectiles while preserving vanilla validation and timing.
- Keep weapon presets active under every reward-choice strategy and hold Anvil event floors until the vanilla Anvil spawns and its upgrade choice is selected or explicitly left unmatched.
- Replace high-frequency global projectile and melee searches with spawn/despawn registries, cache world facilities for half a second, and cap inventory optimization to one hill-climb pass to reduce autopilot frame stalls.
- Prevent original input Stop followed by autopilot Move every frame, cache hostile searches, suppress redundant movement SyncVar writes, and continuously lock defensive aim against mouse overrides.
- Prioritize reachable same-floor downed teammates in multiplayer, postpone blocked rescues to avoid loops, approach under sword-and-shield guard, and complete the vanilla delayed revive interaction.
- Temporarily skip enemies after repeated no-progress checks instead of endlessly recalculating the same blocked path; remove the unverified local CircleCast steering that could suppress all movement at spawn.
- Add low-frequency AFK state heartbeats and throttled path diagnostics covering action, target, movement input, PathGrid, path index, UI, defense, and rescue state.
- Choose the nearest A*-reachable free cell inside the BossSpawner detection rectangle instead of assuming its geometric center is walkable.
- Inset BossSpawner trigger destinations by up to 1.5 meters and use a tighter arrival radius so client/server position differences cannot stop autopilot on an untriggered edge cell.
- Spend available vanilla Anvil rerolls when candidates miss weapon presets, selecting immediately on a match and skipping only after usable rerolls are exhausted.
- Preserve vanilla procedural phase counts by folding multiplayer enemy-count extras into each phase's spawn counts and raising the active-enemy cap, with a 32-enemy safety ceiling.
- Show the complete local autopilot controls and shortcut binding on clients as well as hosts.
- Add direct same-room rescue fallback and explicit unreachable-rescue logs, and pulse combat guard with forced attack gaps while preserving sustained guard only during rescue travel.
- Keep sword-and-shield guard held throughout the vanilla delayed teammate-revive channel instead of dropping guard at interaction range.
- Split local autopilot controls and presets into a dedicated top-level F8 tab shared by hosts and clients, leaving multiplayer rules and scaling on the Rules page.
- Fully suppress manual mouse aim and fire callbacks during autopilot and maintain a unified enemy, threat, rescue, or travel aim after player and weapon updates.
- Treat same-floor Stone Tablet rewards and personal Tablet Fusion as mutually exclusive: either claim clears the alternative, leaving clears both pending records for one Tablet credit, and historical catch-up never grants both.
- Rewrite F8 text around player-visible behavior and game terminology, localize rules/diagnostics, use game item and hard-mode names where available, and remove obsolete menu strings.
- Align Mole Chieftain windmill defense to its 1.65-second warning: attack during the early telegraph, then hold guard continuously through the damage window instead of using the normal guard pulse.
- Register hostile circle and ellipse warnings, prioritize PathGrid escape, and dash late in the telegraph so non-blocking weapons evade Mole Chieftain phase-two rocks instead of standing still or using offensive right-click.
- Synchronize local autoplay state between modded peers and prefix world/list display names without changing saved player names.
- Use the game's normal run countdown during enemy-free autoplay navigation, and cancel running when combat, AOE evasion, or movement stops.
- Treat grounded dynamite bullets as persistent blast hazards, dodge them before combat, widen shield projectile reaction distance, and extend normal guard pulses to improve sword-and-shield uptime.
- Extend automatic defense to defensive katana forms and quarterstaff guard phases, while routing greatsword and non-defensive katana/staff forms to movement and dash evasion.
- Adapt autoplay positioning and input to every runtime weapon form: charge and release bows, sustain automatic ranged weapons at safe distance, use per-weapon melee reach, and honor upgraded secondary modes and resource gates.
- Stop ranged fire through PathGrid obstacles, route to a reachable clear firing position around the target, and abandon engagements that make no movement, distance, or damage progress instead of deadlocking across cover.
- Invalidate cleared or active BossSpawner caches so exits can take over after victory, discover unparented current-grid exits, and scale ranged/Boss standoff with runtime ranged form and Weapon Range upgrades.
- Restrict EXP catch-up to authenticated fresh mid-run players instead of every non-host floor traveler or reconnect, and reconstruct synced dead-enemy corpse/HP-bar state for late observers without replaying death rewards.
- Require enemies to be reachable on the local PathGrid before entering combat, continue toward exits when teammates open the next room, and restore ranged greatsword transform/laser activation at standoff range without toggling it off again.
- On mutual Tablet/Fusion floors, make autoplay always prioritize and claim the right-side Stone Tablet reward, even when ordinary reward auto-selection is disabled, leaving the fusion alternative unused.
- Restore greatsword charged secondary actions by releasing sustained primary combos, waiting for the current swing to finish, then holding vanilla secondary through its charge window; log runtime transform and addon state for diagnosis.
- Treat any populated greatsword transform replacement as the tier-2 ranged form, generalize special-action queuing across sustained combo weapons, preserve continuous vanilla attack-speed input, and emit periodic entity/fire-data/addon/attack-speed/input diagnostics.
- Prioritize spawned F-interaction exits over stale client BossSpawner state, discover gathering exits across parent/child components, repeatedly use the vanilla interaction while players assemble, and abandon trigger zones that never recreate a boss.
- Route to reachable cells around blocked FloorMover centers instead of their occupied origin, follow teammates through connected floors with the vanilla network MoveFloor request rather than a server-only call/world-map UI, and log peer floor state.
- Detect the actual ranged greatsword laser (`Weapon_GreatswordLaser_*` BulletBurst attacks) as an uninterrupted held-primary weapon, preserving left-click while kiting and suppressing right-click, quick-slot, dash, and pseudo-defense interruptions.
- Fix blocked-shot/retreat boolean precedence that still prevented the uninterrupted laser primary from ever pressing left-click, and keep it firing during evade movement even when conservative obstacle sight tests disagree.
- Complete the two-stage post-Boss route by using FloorMover/DungeonStair to select a validated connected forward floor for clients, then prioritize the final all-player gathering interaction in the intermediate room instead of stopping at the world-map UI.
- Drive post-Boss exit search from the vanilla `RpcByeEnd` completion event, immediately invalidating stale client BossSpawner/path caches and allowing confirmed Boss floors to advance despite a delayed `IsInBattle` flag.
- Cover the independent SeedBossSpawner completion path and special post-Boss exits (`DungeonStairCustom`, floor portals, and multi-zone stage exits) in addition to standard BossSpawner stairs and gathering points.
- Keep rejoining players moving toward a discovered FloorMover when their spawn lies in a disconnected/not-yet-updated PathGrid region, using direct entrance steering as a fallback instead of dropping to a stationary teammate follow state.
- Restore vanilla party semantics at ordinary FloorMover stairs: wait for every living player within 10 meters with no battle/preparation state, let only the host select a route, and use delayed `MoveFloorViaWorldmap`/`MoveTogether` instead of moving one client alone.
- Log server-side connection, player, and active-creature counts when a client disconnects so mass disconnects after rejoin or room transitions can be distinguished from combat deaths and spawn load.
- Remove per-hit friendly-fire Info logging, which multiplied across players and multi-hit area attacks and could stall a large-room host when combined with damage-meter logging; retain behavior and concise disconnect identity diagnostics.
- Keep ranged primary attacks active while retreating or evading hazards when line of fire remains clear, and resynchronize held input after vanilla cancels a weapon action so bows, crossbows, staves, golems, and special ranged basics can resume firing.
- Cancel the vanilla revive channel on knockback, range loss, death, UI interruption, or autopilot shutdown, then retain a living rescue target for a clean re-approach and restart.

## 3.4.2

- Add a host toggle for original or disabled Boss and Miniboss Blood Festival lifesteal from player hits.
- Track unclaimed Anvil floors per player and replace unrestricted F8 weapon choices with player-locked vanilla catch-up Anvils that preserve normal candidate rolls and dice rerolls.
- Show a prominent version-mismatch banner before protocol handshake and link outdated clients directly to the latest plugin ZIP.
- Automatically spawn vanilla Anvil, Enchant altar, Miracle selector, Charm, Stone Tablet, and boss reward objects for selectable catch-up; F8 is status-only and unmodded clients can use the original interfaces.
- Replace raw floor GUIDs in player and rescue UI with route progress and readable room types.
- Move fresh joiners and rejoining players into an active boss arena after floor travel completes, preventing closed boss gates from leaving them outside the fight.
- Preserve unclaimed Anvil compensation when a player disconnects directly from the Anvil floor.
- Upload the fresh joiner's actual Dimension Pocket selection to the host before granting it, and prevent stale downed state from carrying into a fresh mid-run character.
- Count only completed floors before the host's current floor as missed rewards, preventing current or future Stone Tablet rewards from being granted early.
- Track unclaimed Anvil floors from the vanilla reward spawner itself so opening and cancelling the Anvil UI still grants compensation after leaving.
- Persist unclaimed Enchant, Miracle, Charm, and Stone Tablet floors; opening and cancelling their vanilla UI no longer loses compensation after travel or disconnect.
- Stop recording or spawning Anvil compensation after the current weapon has no further vanilla enhancements, and clear accumulated weapon credits at max enhancement.
- Let a host who continued directly into a saved dungeon create a new vanilla Steam lobby for the current run without returning to the multiplayer staging area.
- Compensate missed Tablet Combiner opportunities with player-owned vanilla fusion objects, preserving normal tablet validation and money cost and consuming credit only after successful fusion.
- Restrict catch-up Anvil, Enchant altar, Miracle selector, and Tablet Combiner visibility to their target player instead of showing them to the whole party.

## 3.4.0
- Add host-side fresh-join catch-up for median money, dice, max dice, missed inventory expansions, and the joining player's Dimension Pocket items.
- Apply route-difference catch-up when a disconnected player rejoins after the party has advanced, without granting Dimension Pocket items again.
- Add a client F8 compensation panel for host-validated missed weapon-upgrade and enchant choices when both sides run the mod.
- Persist pending and claimed compensation choices per player in the current run, deduplicate counted floors, and show claim status and history to clients.
- Compensate missed HP, Max HP, Sapphire, and inventory floors through vanilla server state, and add persisted Charm, Miracle, Stone Tablet, and boss reward entitlements.
- Keep unmodded clients compatible by sending no custom protocol messages before a successful mod handshake; their selectable entitlements remain saved.
- Reconstruct deterministic equivalent choices when the base game has already discarded the original unloaded-floor candidate payload.
- Split the menu into Rules, Compensation, Diagnostics, and History tabs, add a configurable BepInEx menu shortcut, and expose host rules, connection diagnostics, and persisted catch-up history.
- Add prominent synchronized downed-player rescue banners and a configurable, server-validated rescue-request shortcut with per-player rate limiting.
- Add an optional host-authoritative clear-room auto-revive that restores all downed players at 50% HP and prevents game over only when no living hostile units remain.
- Do not auto-revive or intercept game over while the host is deliberately giving up or leaving the run.
- Show the game's runtime normal, miniboss, and boss multiplayer HP constants plus the active hard-mode HP and damage modifiers in Rules and Diagnostics.

## 3.3.1

- Add optional host-only breathing HP recovery after combat.
- Add optional host-only player friendly fire at 1% damage, capped at 5 HP per hit.
- Add fixed 1 HP/s delayed healing after damage, with a 10-second delay and combat support.
- Prevent friendly-fire target handling from making dagger attacks trigger parries on empty swings.
- Remove the unsafe force-next-stage action, which could regenerate the current stage.
- Add an optional host setting that bypasses only the living-player distance check when the host uses the correct stage entrance normally.

## 3.3.0

- Allow lower-progress clients to pass the server's between-stage chapter validation without changing their quest saves.
- Add a host-only F8 action that forces all connected players into the last requested next stage when players are away from the entrance.
- Redesign the F8 menu with a high-contrast card layout and localized player status controls.
- Add understandable enemy-scaling presets, live party previews, and collapsible advanced settings.
- Allow a baseline of zero for testing enemy scaling alone.
- Add an optional beginner package containing the official BepInEx 5 Windows x64 release and required third-party license notices.

## 3.2.0

- Identify reconnecting players from the host's Steam transport identity.
- Restore existing run slots without requiring the client plugin.
- Remove automatic weapon upgrades.
- Save inventory capacity metadata for future reconnect recovery.
- Add localized host menu, procedural enemy-count scaling, and player status controls.

## 3.1.0

- Add localized host menu.
- Add configurable procedural-wave enemy count scaling.
- Add host player status panel and connection-level kick control.

## 3.0.0

- Rename the project to Sephiria Together.
- Add configurable lobby capacity.
- Add the F8 host configuration menu.

## 2.0.0

- Combine player limit, enemy scaling, progress bypass, mid-run join, reconnect, and catch-up into one plugin.
