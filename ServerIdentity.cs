using System;
using HarmonyLib;
using Mirror;

namespace SephiriaTogether
{
    internal static class ServerIdentity
    {
        private const string SteamGuidPrefix = "steam:";

        internal static string ResolveGuid(NetworkConnectionToClient connection)
        {
            if (connection == null || connection == NetworkServer.localConnection || NetworkManager.singleton == null ||
                NetworkManager.singleton.transport == null)
            {
                return "";
            }

            string address = NetworkManager.singleton.transport.ServerGetClientAddress(connection.connectionId);
            if (!ulong.TryParse(address, out ulong steamId) || steamId == 0)
            {
                return "";
            }

            if (SaveManager.CurrentRun != null)
            {
                int count = SaveManager.CurrentRun.GetInt("SavedPlayerCount", 0);
                for (int i = 0; i < count; i++)
                {
                    if (ulong.TryParse(SaveManager.CurrentRun.GetString($"Player{i}SteamID", ""), out ulong savedSteamId) &&
                        savedSteamId == steamId)
                    {
                        string savedGuid = SaveManager.CurrentRun.GetString($"Player{i}Guid", "");
                        if (!string.IsNullOrWhiteSpace(savedGuid))
                        {
                            return savedGuid;
                        }
                    }
                }
            }

            return SteamGuidPrefix + steamId;
        }

        internal static void NormalizeAuthGuid(NetworkConnectionToClient connection, ref HorayNetworkAuthenticator.VersionMessage message)
        {
            string guid = ResolveGuid(connection);
            if (!string.IsNullOrWhiteSpace(guid))
            {
                message.playerGuid = guid;
            }
        }
    }

    [HarmonyPatch(typeof(HorayNetworkAuthenticator), "ResolveAcceptedPlayerGuid")]
    internal static class StableAcceptedGuidPatch
    {
        private static bool Prefix(HorayNetworkManager hnm, string requestedPlayerGuid, ref string __result)
        {
            if (string.IsNullOrEmpty(requestedPlayerGuid) || !requestedPlayerGuid.StartsWith("steam:", StringComparison.Ordinal))
            {
                return true;
            }

            __result = requestedPlayerGuid;
            hnm?.AddToRejoinWhitelist(__result);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerSpawner), "ResolveCurrentPlayerIdxForSave")]
    [HarmonyPriority(Priority.First)]
    internal static class StablePlayerSlotIdentityPatch
    {
        private static void Prefix(PlayerSpawner __instance, ref string playerGuid)
        {
            string serverGuid = ServerIdentity.ResolveGuid(__instance.connectionToClient);
            if (!string.IsNullOrWhiteSpace(serverGuid))
            {
                playerGuid = serverGuid;
                __instance.playerGuid = serverGuid;
                string address = NetworkManager.singleton.transport.ServerGetClientAddress(__instance.connectionToClient.connectionId);
                if (ulong.TryParse(address, out ulong steamId))
                {
                    __instance.NetworksteamID = steamId;
                }
            }
        }
    }
}
