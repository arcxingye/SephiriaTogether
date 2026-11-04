using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
#if WORKSHOP
using KeyboardShortcut = SephiriaTogether.WorkshopSupport.KeyboardShortcut;
#else
using KeyboardShortcut = BepInEx.Configuration.KeyboardShortcut;
#endif

namespace SephiriaTogether
{
    internal static class ThirdPartyCompatibility
    {
        internal const string StarHarmonyOwner = "stars-sephiriamod";
        private const string OrganizerModId = "sephiria-backpack-organizer";
        private const string BossRushKey = "[Star's Sephiria Mod]BossRushMode";
        private const string InfiniteKey = "[Star's Sephiria Mod]InfiniteMode";
        private static float nextScan;
        private static Type organizerType;
        private static bool organizerActive;
        private static KeyCode organizerKey;
        private static string organizerVersion = "";
        private static string starVersion = "";
        private static string lastSignature;
        private static string pendingNotice;
        private static bool readFailureLogged;
        private static bool scanFailureLogged;
        private static bool noticeFailureLogged;
        private static KeyCode menuFallback;

        internal static bool StarActive { get; private set; }
        internal static bool StarSpecialModeActive => StarActive &&
            ModCompatibilityPolicy.RestrictStoryProgress(false, true, ModeValue(BossRushKey), ModeValue(InfiniteKey));
        internal static bool IsStoryProgressRestricted => CustomGameLauncher.IsActive || StarSpecialModeActive;
        internal static KeyboardShortcut EffectiveMenuShortcut => menuFallback != KeyCode.None
            ? new KeyboardShortcut(menuFallback) : Plugin.menuShortcut.Value;

        private static int ModeValue(string key)
        {
            if (DungeonManager.Instance != null && DungeonManager.Instance.hardModeEnvironment.TryGetValue(key, out int value))
                return value;
            return SaveManager.Current?.GetInt("HardModeShard_" + key + "_Level", 0) ?? 0;
        }

        internal static void Tick(bool force = false)
        {
            if (force || Time.realtimeSinceStartup >= nextScan)
            {
                nextScan = Time.realtimeSinceStartup + 1f;
                try { ScanLoadedMods(); }
                catch (Exception exception)
                {
                    // Compatibility discovery is optional. A changed external
                    // API must not stop the core runtime's Update/OnEnable.
                    if (!scanFailureLogged)
                    {
                        scanFailureLogged = true;
                        Plugin.LogInfo("Compatibility discovery failed: " + exception.Message);
                    }
                }
            }
            menuFallback = ModCompatibilityPolicy.MenuFallback(Plugin.menuShortcut.Value.MainKey,
                organizerActive, organizerKey, Plugin.rescueShortcut.Value.MainKey);
            string signature = $"{StarActive}:{starVersion}|{organizerActive}:{organizerVersion}:{organizerKey}|{menuFallback}";
            if (lastSignature != signature)
            {
                Plugin.LogInfo($"Compatibility: Star={StarActive} {starVersion}, Organizer={organizerActive} {organizerVersion}, " +
                               $"organizerKey={organizerKey}, effectiveMenu={EffectiveMenuShortcut}.");
                pendingNotice = menuFallback != KeyCode.None ? ShortcutNotice() : null;
                lastSignature = signature;
            }
            if (pendingNotice != null && UIManager.Instance != null)
            {
                try
                {
                    UI_SystemMessage message = UIManager.Instance.GetElement<UI_SystemMessage>();
                    if (message != null)
                    {
                        message.Open(pendingNotice, 8f);
                        pendingNotice = null;
                    }
                }
                catch (Exception exception)
                {
                    // Diagnostics continues to show the effective shortcut if
                    // this scene cannot display a native system-message popup.
                    pendingNotice = null;
                    if (!noticeFailureLogged)
                    {
                        noticeFailureLogged = true;
                        Plugin.LogInfo("Compatibility shortcut notice unavailable: " + exception.Message);
                    }
                }
            }
        }

        private static void ScanLoadedMods()
        {
            StarActive = WorkshopModRuntime.IsActive(StarHarmonyOwner) || Harmony.HasAnyPatches(StarHarmonyOwner);
            starVersion = WorkshopModRuntime.Active.TryGetValue(StarHarmonyOwner, out WorkshopModActivation star)
                ? star.Version : "";
            if (organizerType == null)
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate =>
                    string.Equals(candidate.GetName().Name, "SephiriaBackpackOrganizer", StringComparison.Ordinal));
                organizerType = assembly?.GetType("SephiriaBackpackOrganizer.Plugin", false);
                organizerVersion = assembly?.GetName().Version?.ToString() ?? "";
            }
            if (WorkshopModRuntime.Active.TryGetValue(OrganizerModId, out WorkshopModActivation organizer) && organizer.SatisfiesDependents)
                organizerVersion = organizer.Version;
            try
            {
                object instance = OrganizerKeyReader.GetInstance(organizerType);
                organizerActive = instance != null && !(instance is UnityEngine.Object unityObject && unityObject == null);
                if (instance is Behaviour behaviour && !behaviour.isActiveAndEnabled) organizerActive = false;
                organizerKey = KeyCode.None;
                if (organizerActive && !OrganizerKeyReader.TryReadMainKey(instance, out organizerKey) && !readFailureLogged)
                {
                    readFailureLogged = true;
                    Plugin.LogInfo("Organizer hotkey layout is not recognized. Configure distinct main keys manually; no foreign setting was changed.");
                }
            }
            catch (Exception exception)
            {
                organizerActive = false;
                organizerKey = KeyCode.None;
                if (!readFailureLogged)
                {
                    readFailureLogged = true;
                    Plugin.LogInfo("Organizer compatibility inspection failed: " + exception.Message);
                }
            }
        }

        internal static string ShortcutNotice() => menuFallback == KeyCode.None ? "" :
            string.Format(MenuText.Get("CompatMenuShortcut"), organizerKey, menuFallback);

        internal static string Describe() =>
            "Star's SephiriaMod: " + (StarActive ? starVersion : MenuText.Get("CompatNotDetected")) + "\n" +
            "Backpack Organizer: " + (organizerActive ? organizerVersion + " / " + organizerKey : MenuText.Get("CompatNotDetected")) + "\n" +
            string.Format(MenuText.Get("CompatEffectiveMenu"), Plugin.FormatShortcut(EffectiveMenuShortcut)) +
            (menuFallback != KeyCode.None ? "\n" + ShortcutNotice() : "") +
            (StarActive ? "\n" + MenuText.Get("CompatStarRequirements") : "") +
            (StarSpecialModeActive ? "\n" + MenuText.Get("CompatStarModes") : "") +
            (organizerActive ? "\n" + MenuText.Get("CompatOrganizerScope") : "");

        internal static void Clear()
        {
            nextScan = 0f;
            StarActive = false;
            organizerActive = false;
            menuFallback = KeyCode.None;
            organizerKey = KeyCode.None;
            organizerType = null;
            organizerVersion = starVersion = "";
            lastSignature = pendingNotice = null;
            readFailureLogged = false;
            scanFailureLogged = false;
            noticeFailureLogged = false;
        }
    }
}
