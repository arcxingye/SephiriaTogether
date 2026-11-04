using HarmonyLib;
using Mirror;

namespace SephiriaTogether
{
    // Star 2.7.4 unconditionally writes 50 in its admission prefix. Reapply the
    // cap captured when this room was created before Mirror performs its check.
    // The native check still validates listening state, IDs and current count.
    [HarmonyPatch(typeof(NetworkServer), "IsConnectionAllowed", new[] { typeof(int), typeof(string) })]
    [HarmonyAfter("stars-sephiriamod")]
    [HarmonyPriority(Priority.Last)]
    internal static class StarConnectionCapacityPatch
    {
        private static void Prefix()
        {
            if (ThirdPartyCompatibility.StarActive || Harmony.HasAnyPatches(ThirdPartyCompatibility.StarHarmonyOwner))
                PlayerLimit.RestoreServerCapacity();
        }
    }

    [HarmonyPatch(typeof(UI_HorizontalSelectionBox_MultiplayerNumber), "OnEnable")]
    [HarmonyAfter("stars-sephiriamod")]
    [HarmonyPriority(Priority.Last)]
    internal static class MultiplayerLimitUiPatch
    {
        private static void Postfix(UI_HorizontalSelectionBox_MultiplayerNumber __instance)
        {
            __instance.box.numberOfElements = PlayerLimit.CurrentLimit - 1;
        }
    }
}
