# Changelog

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
