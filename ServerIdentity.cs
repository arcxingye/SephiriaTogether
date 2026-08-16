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

    [HarmonyPatch(typeof(PlayerLocalDataStorage), nameof(PlayerLocalDataStorage.OnStartAuthority))]
    internal static class SendFreshPocketItemsPatch
    {
        private static void Postfix(PlayerLocalDataStorage __instance) => MidRunJoin.SendFreshPocketItems(__instance);
    }

    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.ServerSpawnAsDead))]
    internal static class FreshJoinAlivePatch
    {
        private static bool Prefix(UnitAvatar __instance)
        {
            if (!(__instance is PlayerAvatar player)) return true;
            PlayerSpawner spawner = player.spawner;
            bool allow = spawner == null || !MidRunJoin.IsFreshConnection(spawner.connectionToClient);
            Plugin.LogInfo($"ServerSpawnAsDead requested: player={player.Name}, conn={spawner?.connectionToClient?.connectionId ?? -1}, " +
                           $"fresh={spawner != null && MidRunJoin.IsFreshConnection(spawner.connectionToClient)}, allow={allow}, " +
                           $"hp={player.hp:0.##}, floor={Short(player.currentFloorGuid)}, pos={player.transform.position}.");
            return allow;
        }

        private static string Short(string value) => string.IsNullOrEmpty(value)
            ? "-"
            : value.Substring(0, Math.Min(8, value.Length));
    }

    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.Die))]
    internal static class PlayerDeathDiagnosticsPatch
    {
        private static void Prefix(UnitAvatar __instance, int hitLevel, DamageInstance diedFrom)
        {
            if (!NetworkServer.active || !(__instance is PlayerAvatar player)) return;
            Plugin.LogInfo($"Player Die called: name={player.Name}, netId={player.netId}, hitLevel={hitLevel}, " +
                           $"alreadyDead={player.IsDead}, hp={player.hp:0.##}, floor={Short(player.currentFloorGuid)}, pos={player.transform.position}, " +
                           $"damageNull={diedFrom == null}.");
        }

        private static string Short(string value) => string.IsNullOrEmpty(value)
            ? "-"
            : value.Substring(0, Math.Min(8, value.Length));
    }

    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.Revive))]
    internal static class PlayerReviveDiagnosticsPatch
    {
        private static void Prefix(UnitAvatar __instance, float hpAmount)
        {
            if (!NetworkServer.active || !(__instance is PlayerAvatar player)) return;
            Plugin.LogInfo($"Player Revive called: name={player.Name}, netId={player.netId}, amount={hpAmount:0.##}, " +
                           $"wasDead={player.IsDead}, hp={player.hp:0.##}, floor={Short(player.currentFloorGuid)}, pos={player.transform.position}.");
        }

        private static string Short(string value) => string.IsNullOrEmpty(value)
            ? "-"
            : value.Substring(0, Math.Min(8, value.Length));
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
