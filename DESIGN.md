# Loader and session lifecycle

## Shared core, different startup guarantees

Both binaries compile the same gameplay rules, reward bookkeeping, UI and custom
message layouts. Loader adapters own startup, logging and configuration storage.
The BepInEx adapter runs at process startup. In Sephiria 1.1.2, the native adapter
runs only when `GameDataLoader.Awake` activates Workshop DLLs after entering a save.
These are not interchangeable startup guarantees.

The native build cannot provide first-title-screen input or rescue an installation
that cannot reach the base game's first session. Its runtime stays alive after
activation, allowing subsequent title-screen/session preparation in that process.
Early bootstrap remains the BepInEx build's responsibility.

## Three separate pieces of network state

1. **Saved preferences:** online/offline IP selection and default port. Local UI
   may edit these during play; native `ModOptions` or BepInEx persists them.
2. **Current session:** the network manager's actual transport and port. Existing
   connections use this state, even if saved preferences have changed.
3. **Connection action:** a host uses the saved listening port; an explicit client
   join carries its own destination port. A Steam lobby address must not be
   interpreted as a TCP endpoint just because a saved preference changed.

`SessionTransportPolicy` is the dependency-free decision layer. `IpTransport`
applies its result through Unity/Mirror. All TCP port assignment is centralized
in the idle/start-boundary activation path. An active server or client always
prevents transport mutation, including when a start method is called again.

## Native Workshop workflow

```text
Cold launch -> base game starts a session -> Workshop DLL activates
    -> F8 edits/saves next-session preferences
    -> native pause-menu exit confirmation / guest settlement
    -> title + both networking sides stopped
    -> reconcile preferences and reopen network settings
    -> user loads a save -> selected transport starts the next session
```

Changing preferences never automatically exits a game. The explicit return action
uses `UI_PausePanel.GoToTitle`, retaining the game's confirmation and guest
settlement behavior. It does not invent a forced run snapshot or bypass local-Mod
hosting/joining restrictions. A process restart repeats late activation and is
not the apply action for native Workshop networking.

The original transport reference is tied to its network manager. IP-to-native
restoration refuses to disable TCP if no original transport is available. Client
target ports are cleared on disconnect and never replace the persisted default.

## Other activation-sensitive features

- Room capacity is applied to `NetworkServer.maxConnections` on server startup
  and room creation. Together also updates the 1.1.2 Steam transport's live
  private `NextServer`/`LegacyServer` capacity after late Workshop
  activation; changing only `NetworkManager.maxConnections` after
  `Mirror.Listen` would leave an old admission cap. IP and EOS creation apply
  both values. The selected/custom-mode room cap is preserved.
- Enemy scaling and basic protection are evaluated when gameplay events occur.
- Reward handlers and reconnect bookkeeping use session callbacks; custom protocol
  features remain gated by the version handshake. Handlers can be registered
  repeatedly without clearing confirmation. The handshake waits for readiness,
  probes every three seconds at first and every thirty seconds after five sends,
  and stops on a complete report. A thirty-second unanswered wait is displayed
  as unconfirmed, never as proof that the Mod is absent. State follows connection
  objects, so reused connection IDs cannot inherit confirmation. Live reports
  take precedence over potentially stale lobby version labels.
- Save management is available after native activation, not before the first save
  is entered on a cold launch.

## Verification boundaries

The adapter tests execute the production configuration, input and transport
decision code. They cover active-session deferral, idle application, restoration,
missing originals, explicit client ports, and all combinations of session/run/
transition blockers. Patch regression tests execute the production Steam join
transpiler and world-map radius patch through real Harmony against overloaded
fixtures. Network-session tests execute the production handshake and capacity
code with simulated time, connections and both Steam server layouts. Metadata
checks inspect the actual game's entry points and private fields.

The automated checks alone do not prove socket or scene-lifecycle behavior.
On 2026-11-08 the user confirmed the deployed 3.10.0 native Workshop menu opens
on game 1.1.2. This verifies menu availability, not remote handshake or capacity.
On 2026-11-01 the user completed the live 3.9.8 native acceptance flow in both
directions: queue IP in a save, exit normally, apply at title and re-enter; then
queue native mode and repeat. Player.log confirms deferred preferences while
connected, idle-only application in each direction, a TCP listener on port 7777
after re-entry, and an actual server capacity of 16. Native F8 input also works.

This verifies the single-host lifecycle flow, not remote multiplayer. A separate
two-peer test must cover `IP:nondefault-port`, disconnect/rejoin and room capacity.
Local Workshop package restrictions still apply; full multiplayer needs an
appropriate subscribed/approved package setup.
