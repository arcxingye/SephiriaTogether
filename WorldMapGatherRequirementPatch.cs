using HarmonyLib;
using Mirror;

namespace SephiriaTogether
{
    // Both the host's RequestWorldMap overload and the remote command's server
    // implementation resolve their radius here. Keep quest/combat/UI checks and
    // the 1.1.2 baseFloorGuid forwarding in the original methods.
    [HarmonyPatch(typeof(PlayerAvatar), "ResolveGatherDistance", new[] { typeof(float), typeof(bool) })]
    internal static class WorldMapGatherRequirementPatch
    {
        private static void Postfix(ref float __result)
        {
            if (!NetworkServer.active || !Plugin.allowUngroupedStageTransition.Value) return;
            __result = float.MaxValue;
            Plugin.LogInfo("Bypassing world-map gather distance.");
        }
    }
}
