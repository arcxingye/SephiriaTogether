# Changelog

## 3.8.0

- Allow different game and Sephiria Together versions to connect; show one non-blocking 20-second countdown warning per room instead of rejecting or permanently displaying the mismatch.
- Extend the unstarted-run progress picker with a manual chapter dropdown while retaining player-derived progress selection.
- Gate custom network messages on a confirmed matching Mod protocol so older or unmodded clients can still join without receiving incompatible feature messages.
- Restrict manual starting choices to the normal story Race entries and exclude internal, side-story, test, and multiplayer-blocked entries.
- Send a legacy `3.7.0` LAN discovery query alongside the current query so mixed-version rooms remain discoverable; retain the old host's native authentication limitation when the older Mod is hosting.
- Retry one manual/legacy LAN authentication against a detected `1.0.29` host so the old server's native game-version check does not reject a `1.0.30` client before admission.
- Keep the starting-progress picker usable with older clients that do not send the current progress report by deriving a validated fallback from the synchronized main-quest progress.
- Keep the IP direct-connect controls visible in the in-game menu and show a disabled explanation when the player is already inside a game instead of hiding the feature.
- Make mid-run experience compensation always use 100% median experience; remove its configuration switch and menu control.

## 3.7.0

- Remove all automatic-play and Clone Bot code, UI, settings, patches, and network messages from Sephiria Together. Those features are no longer part of this plugin.
- Narrow the plugin to multiplayer, scaling, reconnect, compensation, saves, transfer, and lobby-progress features; matching `3.7.0` clients are required for custom network features.
- Add a startup-selected TCP/IP transport for Steam-offline and non-Steam environments, with configurable port, manual `IP:port` joining, and the original create/join lobby controls.
- Add query/response LAN discovery on UDP `7780`. Refreshing the original room list searches broadcasts and up to two local `/24` subnets, and confirmed IP rooms appear as native room-list entries.
- Make confirmed IP hosts enter the original joined-room page and MultiZone immediately, advertise only after confirmation, close cleanly, and accept remote players without Steam lobby metadata.
- Restore IP-player HUD entries, health/mana bars, world names, and safe UI cleanup directly from Mirror player objects instead of Steam lobby membership.
- Remove the inventory arrangement button, arranger invocation, synchronization guard, and inventory-icon refresh code from Sephiria Together.
- Fix menu shortcut rebinding so the F8 fallback applies only to an exact unmodified F8 binding and combinations such as `Alt+F8` wait for the non-modifier key before saving.
- Rebuild the F8 menu around exact-width navigation grids, a fixed host sub-navigation row, one shared scroll area, and a stable footer; narrow windows stack long labels and actions instead of squeezing or misaligning them.
- Clarify save labels as the save currently in use, automatic game backups, and manual backups created by this Mod.

## 3.6.0

- Add a host-only starting story progress picker for unstarted runs. Online real players are sorted from lowest to highest main-quest progress, with chapter-aware ordering for late-game progress; applying a selection rebuilds the run through the vanilla restart path without changing personal saves.
- Add validated progress reports, protocol version separation, and reconnect-safe Mod handshake state across an in-place run restart.
- Harden catch-up idempotency migration, connection-time EXP dropping, manual inventory arrangement guards, and disconnect cleanup.

## 3.5.2

- Add host-validated player-to-player Leaf transfers with an F8 recipient list, amount entry, confirmation, balance checks, atomic server updates, persistence, and sender/recipient notifications.
- Allow authenticated clients to transfer Leaves to the host or another client without relying on catch-up handshake state or a remote-only recipient connection.
- Add an F8 save manager for independent snapshots, vanilla backup discovery, pre-restore snapshots, atomic replacement, and immediate reload from the selected profile.
- Keep local hosts and solo players out of remote missed-reward tracking and recognize both Tablet reward variants.

## 3.5.1

- Fix repeated reward-object work during quest completion, reducing frame stalls and post-battle freezes.
- Improve Anvil detection for players joining another host, including runtime floor-event detection and client-side Anvil discovery.
- Prevent quest floors from being treated as ordinary Charm compensation floors, avoiding repeated rewards after restarting an unfinished quest.

## 3.5.0

- Preserve vanilla procedural phase counts by folding multiplayer enemy-count extras into each phase's spawn counts and raising the active-enemy cap, with a 32-enemy safety ceiling.
- Treat same-floor Stone Tablet rewards and personal Tablet Fusion as mutually exclusive: either claim clears the alternative, leaving clears both pending records for one Tablet credit, and historical catch-up never grants both.
- Localize multiplayer rules and diagnostics and remove obsolete menu strings.
- Restrict EXP catch-up to authenticated fresh mid-run players instead of every non-host floor traveler or reconnect, and reconstruct synced dead-enemy corpse/HP-bar state for late observers without replaying death rewards.
- Log server-side connection, player, and active-creature counts when a client disconnects so mass disconnects after rejoin or room transitions can be distinguished from combat deaths and spawn load.
- Remove per-hit friendly-fire Info logging, which multiplied across players and multi-hit area attacks and could stall a large-room host when combined with damage-meter logging; retain behavior and concise disconnect identity diagnostics.

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
- Keep weapon upgrades player-selected during catch-up.
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
