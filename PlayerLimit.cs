using HarmonyLib;
using Mirror;

namespace SephiriaTogether
{
    internal static class PlayerLimit
    {
        private static int activeRoomCapacity;
        internal static int CurrentLimit => Plugin.playerLimit != null ? Plugin.playerLimit.Value : 16;

        internal static void SetLimit(int value)
        {
            Plugin.playerLimit.Value = UnityEngine.Mathf.Clamp(value, 2, 250);
            Plugin.ApplyPlayerLimit();
        }

        internal static void ApplyServerCapacity(int capacity)
        {
            if (!NetworkServer.active || NetworkManager.singleton == null) return;
            capacity = ModCompatibilityPolicy.CaptureRoomCapacity(capacity, CurrentLimit);
            activeRoomCapacity = capacity;
            NetworkManager.singleton.maxConnections = capacity;
            NetworkServer.maxConnections = capacity;
            Plugin.LogInfo($"Server connection cap applied: room={capacity}, configuredMaximum={CurrentLimit}.");
        }

        internal static void RestoreServerCapacity()
        {
            if (!NetworkServer.active || NetworkManager.singleton == null || activeRoomCapacity <= 0) return;
            NetworkManager.singleton.maxConnections = activeRoomCapacity;
            NetworkServer.maxConnections = activeRoomCapacity;
        }

        internal static void ClearServerCapacity() => activeRoomCapacity = 0;
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStartServer))]
    internal static class ServerCapacityPatch
    {
        private static void Postfix() => PlayerLimit.ApplyServerCapacity(PlayerLimit.CurrentLimit);
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopServer))]
    internal static class ServerCapacityCleanupPatch
    {
        private static void Postfix() => PlayerLimit.ClearServerCapacity();
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

}
