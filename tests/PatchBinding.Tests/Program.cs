using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using SephiriaTogether;

// A minimal external API surface with the same overloaded-indexer condition
// observed in the game's Player.log. The production transpiler is linked above.
namespace HeathenEngineering.SteamworksIntegration
{
    public struct LobbyData
    {
        public string this[string key] => key;
        public string this[int member] => member.ToString();
    }
}

public sealed class UI_MultiplayerPanel
{
    public void OnJoinButton(LobbyData lobby) { }
    public static void WorkshopCheck() { }
    public static void NativeJoin() { }
}

public sealed class UI_HorizontalSelectionBox_MultiplayerNumber
{
    public sealed class SelectionBox { public int numberOfElements; }
    public readonly SelectionBox box = new SelectionBox();
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void OnEnable() => box.numberOfElements = 3;
}

namespace SephiriaTogether
{
    internal sealed class Setting { internal bool Value; }
    internal static class Plugin { internal static readonly Setting allowLowerProgressPlayers = new Setting(); }
    internal static class ThirdPartyCompatibility
    {
        internal const string StarHarmonyOwner = "stars-sephiriamod";
        internal static bool StarActive;
    }
    internal static class PlayerLimit
    {
        internal static int CapturedCapacity;
        internal static int CurrentLimit = 16;
        internal static void RestoreServerCapacity() => Mirror.NetworkServer.maxConnections = CapturedCapacity;
    }
}

namespace Mirror
{
    internal static class NetworkServer
    {
        internal static int maxConnections;
        internal static int Count;
        internal static bool Listening = true;
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsConnectionAllowed(int connectionId, string address) =>
            Listening && connectionId > 0 && Count < maxConnections;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool Admit(int connectionId) => IsConnectionAllowed(connectionId, "test");
    }
}

internal static class Program
{
    private static int assertions;
    private static readonly MethodInfo Transpiler = typeof(SteamJoinProgressPatch).GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo Normalize = typeof(SteamJoinProgressPatch).GetMethod("ChapterForAdmission", BindingFlags.Static | BindingFlags.NonPublic);
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    private static List<CodeInstruction> Transform(IEnumerable<CodeInstruction> instructions) =>
        ((IEnumerable<CodeInstruction>)Transpiler.Invoke(null, new object[] { instructions })).ToList();
    private static string Chapter(string value) => (string)Normalize.Invoke(null, new object[] { value });

    // Reproduce Star 2.7.4's fixed-cap prefix, not the external mod itself.
    private static void CompetingCapacityPrefix() => Mirror.NetworkServer.maxConnections = 50;
    private static void CompetingDropdownPostfix(UI_HorizontalSelectionBox_MultiplayerNumber __instance)
    {
        if (__instance.box.numberOfElements < 49) __instance.box.numberOfElements = 49;
    }

    private static void TestCapacityOrdering(bool togetherFirst)
    {
        var star = new Harmony("stars-sephiriamod");
        var together = new Harmony("test.sephiriatogether.capacity");
        MethodInfo target = typeof(Mirror.NetworkServer).GetMethod("IsConnectionAllowed", BindingFlags.NonPublic | BindingFlags.Static);
        var starPrefix = new HarmonyMethod(typeof(Program).GetMethod(nameof(CompetingCapacityPrefix), BindingFlags.Static | BindingFlags.NonPublic));
        MethodInfo dropdown = typeof(UI_HorizontalSelectionBox_MultiplayerNumber).GetMethod("OnEnable");
        var starDropdown = new HarmonyMethod(typeof(Program).GetMethod(nameof(CompetingDropdownPostfix), BindingFlags.Static | BindingFlags.NonPublic));
        try
        {
            if (togetherFirst)
            {
                together.CreateClassProcessor(typeof(StarConnectionCapacityPatch)).Patch();
                together.CreateClassProcessor(typeof(MultiplayerLimitUiPatch)).Patch();
            }
            star.Patch(target, prefix: starPrefix);
            star.Patch(dropdown, postfix: starDropdown);
            if (!togetherFirst)
            {
                together.CreateClassProcessor(typeof(StarConnectionCapacityPatch)).Patch();
                together.CreateClassProcessor(typeof(MultiplayerLimitUiPatch)).Patch();
            }
            ThirdPartyCompatibility.StarActive = false; // detection may not have polled yet
            PlayerLimit.CapturedCapacity = 8;
            Mirror.NetworkServer.Count = 8;
            Check(!Mirror.NetworkServer.Admit(9) && Mirror.NetworkServer.maxConnections == 8,
                "Star's fixed 50 must not bypass the selected small room cap, either load order");
            PlayerLimit.CapturedCapacity = 80;
            Mirror.NetworkServer.Count = 55;
            Check(Mirror.NetworkServer.Admit(56) && Mirror.NetworkServer.maxConnections == 80,
                "Together cap over 50 must survive Star's prefix, either load order");
            Mirror.NetworkServer.Listening = false;
            Check(!Mirror.NetworkServer.Admit(56), "Compatibility must preserve native listening rejection");
            Mirror.NetworkServer.Listening = true;
            Check(!Mirror.NetworkServer.Admit(0), "Compatibility must preserve native connection ID rejection");
            var ui = new UI_HorizontalSelectionBox_MultiplayerNumber();
            PlayerLimit.CurrentLimit = 16;
            ui.OnEnable();
            Check(ui.box.numberOfElements == 15, "Dropdown must not be enlarged to 50 by Star, either load order");
            PlayerLimit.CurrentLimit = 100;
            ui.OnEnable();
            Check(ui.box.numberOfElements == 99, "Dropdown must still support Together limits above 50, either load order");
        }
        finally
        {
            UnpatchOwner(together);
            UnpatchOwner(star);
            Mirror.NetworkServer.Listening = true;
        }
    }

    private static void UnpatchOwner(Harmony harmony)
    {
        MethodInfo self = typeof(Harmony).GetMethod("UnpatchSelf", Type.EmptyTypes);
        if (self != null) self.Invoke(harmony, null);
        else typeof(Harmony).GetMethod("UnpatchAll", new[] { typeof(string) }).Invoke(harmony, new object[] { harmony.Id });
    }

    private static void Main()
    {
        bool reproduced = false;
        try { AccessTools.PropertyGetter(typeof(LobbyData), "Item"); }
        catch (AmbiguousMatchException) { reproduced = true; }
        Check(reproduced, "Regression fixture must reproduce the reported indexer ambiguity");
        MethodInfo getter = typeof(LobbyData).GetMethod("get_Item", new[] { typeof(string) });
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Call, typeof(UI_MultiplayerPanel).GetMethod("WorkshopCheck")),
            new CodeInstruction(OpCodes.Ldstr, "Mods"), new CodeInstruction(OpCodes.Call, getter),
            new CodeInstruction(OpCodes.Ldstr, "GameMode"), new CodeInstruction(OpCodes.Call, getter),
            new CodeInstruction(OpCodes.Ldstr, "Chapter"), new CodeInstruction(OpCodes.Call, getter),
            new CodeInstruction(OpCodes.Call, typeof(UI_MultiplayerPanel).GetMethod("NativeJoin"))
        };
        List<CodeInstruction> output = Transform(input);
        Check(output.Count == input.Count + 1, "Insert exactly one normalization call");
        Check(output.Where(instruction => !instruction.Calls(Normalize)).SequenceEqual(input), "Preserve every original instruction, including Workshop checks and native join");
        Check(output[7].Calls(Normalize), "Insert normalization immediately after the Chapter getter");
        Check(output.Count(instruction => instruction.Calls(getter)) == 3, "Leave other metadata reads unchanged");

        Plugin.allowLowerProgressPlayers.Value = false;
        Check(Chapter("5-2") == "5-2", "Disabled bypass preserves chapter");
        Plugin.allowLowerProgressPlayers.Value = true;
        Check(Chapter("5-2") == "0" && Chapter("3") == "0", "Enabled bypass relaxes valid story chapters");
        foreach (string malformed in new[] { null, "", "ERR", "-1", "2--1", "1-2-3" })
            Check(Chapter(malformed) == malformed, "Malformed chapter must still be rejected by native code");
        bool missingRejected = false;
        try { Transform(input.Take(5)); }
        catch (InvalidOperationException) { missingRejected = true; }
        Check(missingRejected, "Fail if the Chapter anchor disappears");
        bool duplicateRejected = false;
        try { Transform(input.Concat(input)); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        Check(duplicateRejected, "Fail if the Chapter anchor becomes ambiguous");
        TestCapacityOrdering(togetherFirst: false);
        TestCapacityOrdering(togetherFirst: true);
        Console.WriteLine($"Patch binding regression tests passed: {assertions} assertions using {typeof(Harmony).Assembly.FullName}.");
    }
}
