# Compatibility audit — Together 3.9.9

Audited against game **1.1.1**, Star's SephiriaMod **2.7.4** (Workshop
`3829559495`) and Backpack Organizer Workshop **3.0.0** (`3830579017`).
The external assemblies were inspected read-only; no external DLL, config,
Workshop manifest, or approval record was modified or bundled.

## Backpack Organizer

- Its default **F8** clashes with Together's default menu key. The organizer
  accepts extra modifiers, so simply moving the menu to Ctrl+F8 would still
  trigger sorting. Together now reads the active organizer's parsed main key
  without executing sorting or writing the organizer's settings. When the main
  keys collide, Together temporarily uses **F9** (then F10/F11/F12 if reserved by
  the organizer or rescue binding). The saved menu shortcut is unchanged.
- A system notice and the Diagnostics page display the effective menu key. If the
  organizer is disabled, Together returns to the saved shortcut. Manual distinct
  bindings still work. The BepInEx 2.5.x ConfigEntry layout is readable as well,
  but this does not repair that old organizer's removed-game-type errors.
- In 3.0.0, both host and client sorting apply existing-item `GridInventory.Swap`
  and `DoClickAction` operations, with snapshot checks and acknowledgement waits.
  Together's direct item-write anti-cheat does not block those commands. It does
  not need a blanket item-write exemption or disabled anti-cheat.
- Run **one organizer build**. The BepInEx 2.5.0 DLL and Workshop 3.0.0 DLL have
  the same assembly name; the native loader can reject the name collision before
  Together sees an active organizer. A TypeLoadException was also observed with
  2.5.0 against the updated game. Disable the old organizer when using its
  Workshop replacement.

Star introduces new effects and changes vanilla effect formulas. The organizer's
offline scoring model is its own implementation. Together cannot certify optimal
placement of every Star item or fix its scoring by changing network permissions.
Use the organizer's supported priorities/direction bindings and report specific
scoring errors to its maintainer. No scoring code is copied into Together.

## Star's SephiriaMod

- Star's manifest declares **everyone**. All peers need the same Star version
  and compatible Workshop load order. Together being host-side does not remove
  Star's client requirements or the game's native Workshop checks.
- Star 2.7.4 writes **50** into `NetworkServer.maxConnections` in
  `NetworkServer.IsConnectionAllowed` on *every* connection attempt. Together
  captures the actual selected room capacity, runs after Star's prefix, and
  restores that cap before the original Mirror admission check. The original
  listening/ID/count validation still executes. The player-limit dropdown also
  runs after Star's fixed-50 dropdown adjustment. Custom-game caps are retained.
- Star's Boss Rush / Infinite Mode are represented in its hard-mode environment,
  not necessarily `CustomGameLauncher.Current`. Together recognizes these flags
  only when Star is active and disables its story-progress picker in those modes.
  Normal story mode remains selectable. Stale Star flags without the mod do not
  disable the picker.
- Star adds items/weapons/miracles through the game's databases. Together's
  catch-up candidates use those live databases and original reward objects, and
  inventory expansion calls `AddStorage`, allowing Star's own skin/cap patch to
  run. Server-side rewards/sales remain outside direct remote-write filtering.
- Together's optional healing uses native `Heal`, so Star's healing restrictions
  still participate. Optional enemy scaling adds to existing game/Star values;
  this can intentionally produce much harder enemies. Shared combat targets are
  not by themselves proof of a conflict. Star's infinite-route / Boss Rush late
  joins and reward semantics still need live regression testing.

## Evidence and test scope

Exact audited external DLL SHA-256 values:

| DLL | SHA-256 |
| --- | --- |
| `SPMod.dll` 2.7.4 | `56669185A0FFC39B482D3E454CEDD1144A5E78192C3CB5F4BAE328BD5D5AE298` |
| Organizer Workshop 3.0.0 | `B5BC2BF01182B046FA85D127635AEF9AD547B9D52CE50E3B6B20636396555CF7` |
| Organizer BepInEx 2.5.0 | `D7D54CF57FBE557A24FA2089D635FFAA221786CC698C71570DE64C3A5B778887` |

The support suite executes production hotkey-reading/fallback and mode policies.
Patch tests execute the production capacity prefix through real Harmony detours
in both registration orders against a fixture reproducing Star's fixed-50 write;
they also check native listening/ID rejection remains intact. They run with both
the game Harmony 2.4.3 and BepInEx HarmonyX 2.9. External Mod game code is not
executed by these fixtures.

Required live verification before claiming full compatibility:

1. Use subscribed packages with current review status and only one organizer.
2. With Star + Organizer + Together, enter a save: F8 sorts and F9 opens/closes
   Together. Diagnostics must show the active mods and effective key.
3. On a guest, sort with host anti-cheat enabled; verify item IDs/counts are
   preserved and acknowledgement completes.
4. Test a small selected room cap, and a larger cap, with Star loaded in both
   orders. Check logs/native admission, not only the dropdown.
5. Toggle Star's special modes and confirm story progress cannot replace them;
   separately test late joins, reward objects and reconnect in those modes.

Current scope is targeted compatibility changes plus static/executable-fixture
verification, not a completed three-Mod multiplayer playthrough.
