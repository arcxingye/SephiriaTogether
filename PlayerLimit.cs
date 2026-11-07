using System;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.API;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class PlayerLimit
    {
        private static int activeRoomCapacity;
        private static string transportCapacity = "not synchronized";
        internal static int CurrentLimit => Plugin.playerLimit != null ? Plugin.playerLimit.Value : 16;
        internal static int ActiveCapacity => activeRoomCapacity;
        internal static string Diagnostics => $"Capacity: configured={CurrentLimit}, room={activeRoomCapacity}, " +
            $"manager={NetworkManager.singleton?.maxConnections}, Mirror={NetworkServer.maxConnections}, " +
            $"connected={NetworkServer.connections.Count}, transport={transportCapacity}";

        internal static void SetLimit(int value)
        {
            Plugin.playerLimit.Value = UnityEngine.Mathf.Clamp(value, 2, 250);
            Plugin.ApplyPlayerLimit();
        }

        internal static void ApplyServerCapacity(int capacity, string source = "room")
        {
            if (!NetworkServer.active || NetworkManager.singleton == null) return;
            capacity = ModCompatibilityPolicy.CaptureRoomCapacity(capacity, CurrentLimit);
            int modeLimit = CustomGameLauncher.Current != null ? CustomGameLauncher.Current.maxPlayers : 0;
            if (modeLimit > 0) capacity = Math.Min(capacity, modeLimit);
            activeRoomCapacity = capacity;
            RestoreServerCapacity();
            Plugin.LogInfo($"Server connection cap applied ({source}): {Diagnostics}.");
        }

        internal static void EnsureActiveCapacity()
        {
            if (!NetworkServer.active || NetworkManager.singleton == null) return;
            if (activeRoomCapacity > 0) { RestoreServerCapacity(); return; }
            if (IpLobby.IsCreated && IpLobby.MaxPlayers > 0)
            {
                ApplyServerCapacity(IpLobby.MaxPlayers, "existing IP room");
                return;
            }
            GameObject steamManager = SingletonObject.Find("SteamManager");
            if (steamManager != null && App.Initialized &&
                steamManager.TryGetComponent(out LobbyManager manager) && manager.HasLobby && manager.Lobby.IsOwner)
            {
                ApplyServerCapacity(manager.Lobby.MaxMembers > 0 ? manager.Lobby.MaxMembers : CurrentLimit, "existing Steam room");
                return;
            }
            ApplyServerCapacity(CurrentLimit, "late activation before room creation");
        }

        internal static void RestoreServerCapacity()
        {
            if (!NetworkServer.active || NetworkManager.singleton == null || activeRoomCapacity <= 0) return;
            NetworkManager.singleton.maxConnections = activeRoomCapacity;
            NetworkServer.maxConnections = activeRoomCapacity;
            transportCapacity = SteamTransportCapacity.Synchronize(NetworkManager.singleton.transport, activeRoomCapacity);
        }

        internal static void ClearServerCapacity()
        {
            activeRoomCapacity = 0;
            transportCapacity = "not synchronized";
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStartServer))]
    internal static class ServerCapacityPatch
    {
        private static void Postfix() => PlayerLimit.EnsureActiveCapacity();
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel_E), nameof(UI_MultiplayerPanel_E.HandleCreated), new[] { typeof(EOSLobbyInfo) })]
    internal static class EosRoomCapacityPatch
    {
        private static void Postfix(EOSLobbyInfo info)
        {
            if (info != null && info.maxMembers > 0) PlayerLimit.ApplyServerCapacity(info.maxMembers, "EOS room");
        }
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
