using System;
using System.Linq;
using SephiriaTogether;
using SephiriaTogether.WorkshopSupport;
using UnityEngine;
using UnityEngine.InputSystem;

internal static class Program
{
    private sealed class WorkshopOrganizer
    {
        public static readonly WorkshopOrganizer Instance = new WorkshopOrganizer();
        private readonly KeyCode mainKey = KeyCode.F8;
        internal KeyCode ParsedKey => mainKey;
    }

    private sealed class LegacyOrganizer
    {
        public static readonly LegacyOrganizer Instance = new LegacyOrganizer();
        internal readonly TestEntry Hotkey = new TestEntry();
        internal sealed class TestEntry { public KeyboardShortcut Value => new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl); }
    }
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static void Main()
    {
        ModOptions storage = new ModOptions();
        ConfigFile config = new ConfigFile(storage);
        var limit = config.Bind("Multiplayer", "PlayerLimit", 16,
            new ConfigDescription("limit", new AcceptableValueRange<int>(2, 250)));
        Check(limit.Value == 16, "Missing settings must use defaults");
        Check(ReferenceEquals(limit, config.Bind("Multiplayer", "PlayerLimit", 4)), "Repeated binds must share an entry");
        limit.Value = 1000;
        Check(limit.Value == 250 && (int)storage.Saved["Multiplayer/PlayerLimit"] == 250, "Clamp before persisting");
        limit.Value = -5;
        Check(limit.Value == 2, "Lower bound");
        storage.Values["Multiplayer/PlayerLimit"] = "invalid";
        Check(limit.Value == 16, "Wrong native storage type must fall back");
        var scaling = config.Bind("Scaling", "Multiplier", 1f,
            new ConfigDescription("multiplier", new AcceptableValueRange<float>(0.05f, 4f)));
        scaling.Value = float.NaN;
        Check(scaling.Value == 1f && (float)storage.Saved["Scaling/Multiplier"] == 1f, "NaN must not reach storage");
        storage.Values["Scaling/Multiplier"] = float.PositiveInfinity;
        Check(scaling.Value == 1f, "Invalid stored floats must fall back");
        scaling.Value = 9f;
        Check(scaling.Value == 4f, "Float range");
        var enabled = config.Bind("Multiplayer", "EnableAntiCheat", false);
        int saves = storage.SaveCount;
        config.SaveOnConfigSet = false;
        enabled.Value = true;
        Check(enabled.Value && storage.SaveCount == saves, "Batch mode must retain in-memory values without saving");
        config.Save();
        Check((bool)storage.Saved["Multiplayer/EnableAntiCheat"], "Explicit save must flush changes");
        ConfigFile reloaded = new ConfigFile(storage);
        Check(reloaded.Bind("Multiplayer", "EnableAntiCheat", false).Value, "New adapter must read native saved state");
        storage.Values["Unrelated/Setting"] = 42;
        config.Remove(new ConfigDefinition("Multiplayer", "EnableAntiCheat"));
        Check(!storage.Values.ContainsKey("Multiplayer/EnableAntiCheat") && (int)storage.Values["Unrelated/Setting"] == 42, "Remove must be scoped to one key");
        Check(!config.Bind("Multiplayer", "EnableAntiCheat", false).Value, "Removed binding must use default on rebind");

        var hotkey = config.Bind("Interface", "MenuShortcut", new KeyboardShortcut(KeyCode.F8));
        hotkey.Value = new KeyboardShortcut(KeyCode.A, KeyCode.LeftControl, KeyCode.LeftShift, KeyCode.LeftShift);
        config.Save();
        var restored = new ConfigFile(storage).Bind("Interface", "MenuShortcut", KeyboardShortcut.Empty).Value;
        Check(restored.MainKey == KeyCode.A && restored.Modifiers.SequenceEqual(new[] { KeyCode.LeftControl, KeyCode.LeftShift }), "Shortcut round-trip and deduplication");
        storage.Values["Interface/MenuShortcut"] = "NotAKey + F8";
        Check(hotkey.Value.MainKey == KeyCode.F8, "Invalid shortcut must use default");
        Check(!KeyboardShortcut.TryParse("F8 +", out _), "Reject incomplete shortcut");
        Check(!KeyboardShortcut.TryParse("999999", out _), "Reject undefined key codes");
        Check(KeyboardShortcut.TryParse("None", out var empty) && empty.MainKey == KeyCode.None, "Disabled rescue shortcut persists");
        Check(KeyboardShortcut.TryParse("f8 + leftcontrol", out var combination), "Case-insensitive shortcut parsing");

        Keyboard.current = new Keyboard();
        Keyboard.current[Key.F8].wasPressedThisFrame = true;
        Check(!combination.IsDown(), "Missing modifier must not trigger shortcut");
        Keyboard.current[Key.LeftCtrl].isPressed = true;
        Check(combination.IsDown(), "Input System combination must trigger");
        Keyboard.current[Key.F8].wasPressedThisFrame = false;
        Keyboard.current[Key.F8].isPressed = true;
        Check(!combination.IsDown(), "Held key must not retrigger every frame");
        Keyboard.current[Key.Digit1].wasPressedThisFrame = true;
        Check(ShortcutInput.IsDown(KeyCode.Alpha1, Array.Empty<KeyCode>()), "Alpha key mapping");
        Keyboard.current[Key.Numpad2].wasPressedThisFrame = true;
        Check(ShortcutInput.IsDown(KeyCode.Keypad2, Array.Empty<KeyCode>()), "Numpad key mapping");
        Keyboard.current = null;
        Check(!combination.IsDown(), "No keyboard and disabled legacy input must not throw");
        Input.Available = true;
        Input.Down.Add(KeyCode.Mouse0);
        Check(ShortcutInput.IsDown(KeyCode.Mouse0, Array.Empty<KeyCode>()), "Legacy non-keyboard binding fallback");
        Check(!KeyboardShortcut.Empty.IsDown(), "Unbound shortcut never fires");

        bool unavailableRejected = false;
        try { _ = new ConfigFile(new ModOptions { IsAvailable = false }); }
        catch (InvalidOperationException) { unavailableRejected = true; }
        Check(unavailableRejected, "Do not initialize with unavailable native storage");

        // These exercise the same policy used by the transport reconciler:
        // late Workshop activation, idle application, restoration and client ports.
        Check(SessionTransportPolicy.Decide(true, 7777, false, 0, false, true) == SessionTransportChange.WaitForSessionEnd,
            "Late Workshop activation must defer rather than replace a running native host");
        Check(SessionTransportPolicy.Decide(true, 7777, false, 0, true, true) == SessionTransportChange.EnableIp,
            "Returning to an idle title must apply the saved IP preference");
        Check(SessionTransportPolicy.Decide(false, 7777, true, 7777, false, true) == SessionTransportChange.WaitForSessionEnd,
            "Disabling IP during a session must not disable its live transport");
        Check(SessionTransportPolicy.Decide(false, 7777, true, 7777, true, true) == SessionTransportChange.RestoreNative,
            "Idle return must restore the captured native transport");
        Check(SessionTransportPolicy.Decide(false, 7777, true, 7777, true, false) == SessionTransportChange.NativeTransportUnavailable,
            "Never disable TCP when there is no native transport to restore");
        Check(SessionTransportPolicy.Decide(true, 9000, true, 7777, false, true) == SessionTransportChange.WaitForSessionEnd,
            "A live host port must not change when preferences are edited");
        Check(SessionTransportPolicy.Decide(true, 9000, true, 7777, true, true) == SessionTransportChange.UpdateIpPort,
            "Apply a changed listening port only while idle");
        ushort comparedClientPort = SessionTransportPolicy.ComparisonPort(false, true, 7777, 8888);
        Check(comparedClientPort == 8888 && SessionTransportPolicy.Decide(true, comparedClientPort, true, 8888, false, true) == SessionTransportChange.None,
            "A remote client's target port must not be mistaken for an unapplied host-port preference");
        Check(SessionTransportPolicy.ClientPort(7777, 8888) == 8888 && SessionTransportPolicy.ClientPort(7777, null) == 7777,
            "Explicit IP:port must survive the StartClient preparation step");
        Check(SessionTransportPolicy.ComparisonPort(true, true, 9000, 7777) == 9000,
            "Local hosts must compare their listening port, not use the remote-client exception");
        Check(SessionTransportPolicy.ClientModeMatchesTarget(true, true), "Explicit TCP join uses IP mode");
        Check(SessionTransportPolicy.ClientModeMatchesTarget(false, false), "Native lobby join retains native transport");
        Check(!SessionTransportPolicy.ClientModeMatchesTarget(true, false), "Never reinterpret a native lobby address as TCP");
        Check(!SessionTransportPolicy.ClientModeMatchesTarget(false, true), "Do not send an explicit TCP target through the native lobby transport");
        foreach (bool server in new[] { false, true })
        foreach (bool client in new[] { false, true })
        foreach (bool transitioning in new[] { false, true })
        foreach (bool runStarted in new[] { false, true })
        {
            bool idle = SessionTransportPolicy.CanApply(server, client, transitioning, runStarted);
            if (server || client || transitioning || runStarted)
                Check(!idle, "Automatic application must wait for all session/transition/run blockers to clear");
            else Check(idle, "Idle title can apply preferences");
            bool atBoundary = SessionTransportPolicy.CanApply(server, client, transitioning, runStarted, startingSession: true);
            Check(atBoundary == (!server && !client), "An explicit start boundary may bypass scene flags, never active networking");
        }
        Check(OrganizerKeyReader.GetInstance(typeof(WorkshopOrganizer)) == WorkshopOrganizer.Instance, "Read existing organizer instance without creating another");
        Check(OrganizerKeyReader.TryReadMainKey(WorkshopOrganizer.Instance, out var organizerKey) && organizerKey == WorkshopOrganizer.Instance.ParsedKey,
            "Workshop organizer parsed hotkey layout");
        Check(OrganizerKeyReader.TryReadMainKey(LegacyOrganizer.Instance, out organizerKey) && organizerKey == KeyCode.F9,
            "Legacy organizer ConfigEntry shortcut layout");
        Check(!OrganizerKeyReader.TryReadMainKey(null, out _), "No organizer instance is not an F8 conflict");
        Check(!OrganizerKeyReader.TryReadMainKey(new object(), out _), "Unknown organizer layout must not guess its hotkey");
        Check(ModCompatibilityPolicy.MenuFallback(KeyCode.F8, true, KeyCode.F8, KeyCode.R) == KeyCode.F9,
            "Default F8 conflict uses a separate main key");
        Check(ModCompatibilityPolicy.MenuFallback(KeyCode.F8, false, KeyCode.F8, KeyCode.R) == KeyCode.None,
            "Disabling the organizer restores the original menu key");
        Check(ModCompatibilityPolicy.MenuFallback(KeyCode.F10, true, KeyCode.F8, KeyCode.R) == KeyCode.None,
            "Distinct configured keys must not be changed");
        Check(ModCompatibilityPolicy.MenuFallback(KeyCode.F9, true, KeyCode.F9, KeyCode.F10) == KeyCode.F11,
            "Fallback avoids both organizer and rescue main keys");
        Check(ModCompatibilityPolicy.MenuFallback(KeyCode.F8, true, KeyCode.None, KeyCode.R) == KeyCode.None,
            "An unbound organizer does not reserve F8");
        Check(ModCompatibilityPolicy.RestrictStoryProgress(false, true, 1, 0), "Protect Star Boss Rush even when the game has no native custom-game object");
        Check(ModCompatibilityPolicy.RestrictStoryProgress(false, true, 0, 1), "Protect Star Infinite Mode");
        Check(!ModCompatibilityPolicy.RestrictStoryProgress(false, false, 1, 1), "Stale Star save flags alone must not disable normal story selection");
        Check(!ModCompatibilityPolicy.RestrictStoryProgress(false, true, 0, 0), "Star normal story mode still allows progress selection");
        Check(ModCompatibilityPolicy.RestrictStoryProgress(true, false, 0, 0), "Keep native custom-game protection");
        Check(ModCompatibilityPolicy.CaptureRoomCapacity(8, 16) == 8, "Actual selected room cap beats configured maximum");
        Check(ModCompatibilityPolicy.CaptureRoomCapacity(80, 100) == 80, "Together room sizes above Star's 50 are supported by policy");
        Check(ModCompatibilityPolicy.CaptureRoomCapacity(50, 16) == 16, "Selected room respects Together maximum");
        Check(ModCompatibilityPolicy.CaptureRoomCapacity(1, 16) == 1, "Single-player custom-mode cap is not expanded");
        Console.WriteLine($"Workshop support tests passed: {assertions} assertions.");
    }
}
