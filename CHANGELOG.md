# Changelog

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
