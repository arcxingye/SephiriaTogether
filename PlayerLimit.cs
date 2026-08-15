using HarmonyLib;
using Mirror;

namespace SephiriaTogether
{
    internal static class PlayerLimit
    {
        internal static int CurrentLimit => Plugin.playerLimit != null ? Plugin.playerLimit.Value : 16;

        internal static void SetLimit(int value)
        {
            Plugin.playerLimit.Value = UnityEngine.Mathf.Clamp(value, 2, 250);
            Plugin.ApplyPlayerLimit();
        }
    }

    [HarmonyPatch(typeof(OptionsBinding), "Awake")]
    internal static class OptionsLimitPatch
    {
        private static void Postfix() => Plugin.ApplyPlayerLimit();
    }

    [HarmonyPatch(typeof(NetworkManager), "Awake")]
    internal static class NetworkLimitPatch
    {
        private static void Prefix(NetworkManager __instance) => __instance.maxConnections = PlayerLimit.CurrentLimit;
        private static void Postfix(NetworkManager __instance) => __instance.maxConnections = PlayerLimit.CurrentLimit;
    }

    [HarmonyPatch(typeof(UI_HorizontalSelectionBox_MultiplayerNumber), "OnEnable")]
    internal static class MultiplayerLimitUiPatch
    {
        private static void Postfix(UI_HorizontalSelectionBox_MultiplayerNumber __instance)
        {
            __instance.box.numberOfElements = PlayerLimit.CurrentLimit - 1;
        }
    }
}
