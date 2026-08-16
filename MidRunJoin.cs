using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.API;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class MidRunJoin
    {
        private static readonly HashSet<NetworkConnectionToClient> FreshConnections =
            new HashSet<NetworkConnectionToClient>();
        private static readonly FieldInfo RejoinDetectedField =
            AccessTools.Field(typeof(PlayerSpawner), "isRejoinDetected");
        private static readonly FieldInfo VersionApprovedConnectionsField =
            AccessTools.Field(typeof(HorayNetworkManager), "versionApprovedConnIds");

        [ThreadStatic]
        private static bool bypassDungeonGate;

        internal static ManualLogSource Log { get; set; }

        internal static bool BypassDungeonGate => bypassDungeonGate;

        internal static void BeginAuthentication(
            NetworkConnectionToClient connection,
            HorayNetworkAuthenticator.VersionMessage message,
            out bool __state)
        {
            __state = false;
            if (!Plugin.allowMidRunJoin.Value || !NetworkServer.active ||
                !HorayNetworkAuthenticator.AccessDeny_InDungeon)
            {
                return;
            }

            HorayNetworkManager manager = NetworkManager.singleton as HorayNetworkManager;
            if (manager == null || NetworkServer.localConnection == connection || SaveManager.CurrentRun == null ||
                SaveManager.CurrentRun.GetInt("SaveVersion", 0) == 0)
            {
                return;
            }

            if (manager != null && !string.IsNullOrWhiteSpace(message.playerGuid) &&
                manager.IsRejoinBanned(message.playerGuid))
            {
                return;
            }

            bool isKnownRejoin = manager.IsInRejoinWhitelist(message.playerGuid);
            if (isKnownRejoin)
            {
                return;
            }

            __state = true;
            bypassDungeonGate = true;
        }

        internal static void EndAuthentication(
            NetworkConnectionToClient connection,
            HorayNetworkAuthenticator.VersionMessage message,
            bool __state,
            bool succeeded)
        {
            if (!__state)
            {
                return;
            }

            bypassDungeonGate = false;
            if (succeeded)
            {
                HorayNetworkManager manager = NetworkManager.singleton as HorayNetworkManager;
                HashSet<int> approved = VersionApprovedConnectionsField?.GetValue(manager) as HashSet<int>;
                if (message.version == Application.version &&
                    approved != null && approved.Contains(connection.connectionId))
                {
                    FreshConnections.Add(connection);
                    Log?.LogInfo($"Authorized fresh mid-run connection {connection.connectionId}.");
                }
            }
        }

        internal static bool IsFreshConnection(NetworkConnectionToClient connection)
        {
            return connection != null && FreshConnections.Contains(connection);
        }

        internal static void BeginFreshPlayerOperation(NetworkConnectionToClient connection, out bool __state)
        {
            __state = IsFreshConnection(connection);
            if (__state)
            {
                bypassDungeonGate = true;
            }
        }

        internal static void EndFreshPlayerOperation(bool __state)
        {
            if (__state)
            {
                bypassDungeonGate = false;
            }
        }

        internal static void RemoveConnection(NetworkConnectionToClient connection)
        {
            if (connection != null)
            {
                FreshConnections.Remove(connection);
                CatchUpRewards.RemoveConnection(connection);
            }
        }

        internal static void ClearConnections()
        {
            FreshConnections.Clear();
            bypassDungeonGate = false;
        }

        internal static void ScheduleCatchUp(PlayerSpawner spawner)
        {
            if (!NetworkServer.active || spawner == null || spawner.connectionToClient == null ||
                !Plugin.allowMidRunJoin.Value)
            {
                return;
            }

            bool isFresh = FreshConnections.Remove(spawner.connectionToClient);
            bool isRejoin = RejoinDetectedField != null && (bool)RejoinDetectedField.GetValue(spawner);
            if ((isFresh || isRejoin) && Plugin.InstanceForPatches != null)
            {
                Plugin.InstanceForPatches.StartCoroutine(CatchUpAfterTravel(spawner, isRejoin));
            }
        }

        private static IEnumerator CatchUpAfterTravel(PlayerSpawner spawner, bool isRejoin)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (spawner != null && spawner.PlayerAvatar != null &&
                   (string.IsNullOrEmpty(spawner.PlayerAvatar.currentFloorGuid) ||
                    spawner.PlayerAvatar.isInDungeon <= 0) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!NetworkServer.active || spawner == null || spawner.PlayerAvatar == null)
            {
                yield break;
            }

            if (string.IsNullOrEmpty(spawner.PlayerAvatar.currentFloorGuid) || spawner.PlayerAvatar.isInDungeon <= 0)
            {
                Log?.LogWarning($"Catch-up skipped because {spawner.PlayerAvatar.name} did not finish floor travel in time.");
                yield break;
            }

            LevelController newcomer = spawner.GetComponent<LevelController>();
            if (newcomer == null)
            {
                yield break;
            }

            PlayerSpawner routeSource = PlayerSpawner.MultiplayerList
                .Where(peer => peer != null && peer != spawner && peer.PlayerAvatar != null)
                .OrderByDescending(peer => peer.PlayerAvatar.floorTravelHistory.Count)
                .ThenByDescending(peer => peer.isHost)
                .FirstOrDefault();
            HashSet<string> newcomerHistory = new HashSet<string>(spawner.PlayerAvatar.floorTravelHistory);
            List<string> missedFloors = routeSource != null
                ? routeSource.PlayerAvatar.floorTravelHistory
                    .Where(guid => !string.IsNullOrEmpty(guid) && !newcomerHistory.Contains(guid))
                    .Distinct()
                    .ToList()
                : new List<string>();
            bool grantResources = CatchUpRewards.HasNewResourceFloors(spawner, missedFloors);
            CatchUpRewards.Prepare(spawner);
            if (isRejoin && missedFloors.Count == 0)
            {
                Log?.LogInfo($"Rejoin catch-up not needed for {spawner.PlayerAvatar.name}.");
                yield break;
            }

            List<int> peerExperience = new List<int>();
            List<int> peerMoney = new List<int>();
            List<int> peerDice = new List<int>();
            List<int> peerMaxDice = new List<int>();
            foreach (PlayerSpawner peer in PlayerSpawner.MultiplayerList)
            {
                if (peer == null || peer == spawner || peer.PlayerAvatar == null ||
                    peer.PlayerAvatar.isInDungeon <= 0 ||
                    peer.PlayerAvatar.currentFloorGuid != spawner.PlayerAvatar.currentFloorGuid)
                {
                    continue;
                }

                LevelController level = peer.GetComponent<LevelController>();
                if (level != null)
                {
                    peerExperience.Add(Math.Max(0, level.currentExp));
                }
                peerMoney.Add(Math.Max(0, peer.PlayerAvatar.Money));
                peerDice.Add(Math.Max(0, peer.PlayerAvatar.rerollDice));
                peerMaxDice.Add(Math.Max(0, peer.PlayerAvatar.maxRerollDice));
            }

            if (grantResources && peerExperience.Count > 0)
            {
                int target = Mathf.FloorToInt(Median(peerExperience) * Plugin.catchUpExperienceRatio.Value);
                int amount = Math.Max(0, target - newcomer.currentExp);
                if (amount > 0)
                {
                    newcomer.AddExp(amount);
                    Log?.LogInfo(
                        $"Granted {amount} catch-up EXP to {spawner.PlayerAvatar.name} " +
                        $"(target {target}).");
                }
            }

            int money = grantResources ? Math.Max(0, Median(peerMoney) - spawner.PlayerAvatar.Money) : 0;
            int maxDice = grantResources ? Math.Max(0, Median(peerMaxDice) - spawner.PlayerAvatar.maxRerollDice) : 0;
            int dice = grantResources ? Math.Max(0, Median(peerDice) - spawner.PlayerAvatar.rerollDice) : 0;
            if (money > 0) spawner.PlayerAvatar.AddMoney(money);
            if (maxDice > 0) spawner.PlayerAvatar.AddMaxDice(maxDice);
            if (dice > 0) spawner.PlayerAvatar.AddDice(dice);

            int[] pocketItems = spawner.LocalDataStorage != null
                ? spawner.LocalDataStorage.dimensionPocketItem.ToArray()
                : null;
            if (!isRejoin && pocketItems != null && pocketItems.Length > 0)
            {
                spawner.AddDimensionPocketItemsOnServer(pocketItems);
            }

            if (grantResources) CatchUpRewards.CommitResourceFloors(spawner, missedFloors);
            spawner.SaveCurrentSessionData();
            SaveManager.Save(saveCurrent: false, saveCurrentRun: true);

            Log?.LogInfo(
                $"{(isRejoin ? "Rejoin" : "Fresh-join")} resources for {spawner.PlayerAvatar.name}: " +
                $"missed floors {missedFloors.Count}, money +{money}, " +
                $"dice +{dice}, max dice +{maxDice}, " +
                $"pocket items {(isRejoin ? 0 : pocketItems?.Length ?? 0)}.");
        }

        private static int Median(List<int> values)
        {
            if (values == null || values.Count == 0) return 0;
            values.Sort();
            int middle = values.Count / 2;
            return values.Count % 2 == 0
                ? (int)(((long)values[middle - 1] + values[middle]) / 2L)
                : values[middle];
        }
    }

    [HarmonyPatch(typeof(HorayNetworkAuthenticator), "OnServerVersionMessage")]
    internal static class MidRunAuthenticationPatch
    {
        private static void Prefix(
            NetworkConnectionToClient conn,
            ref HorayNetworkAuthenticator.VersionMessage message,
            out bool __state)
        {
            ServerIdentity.NormalizeAuthGuid(conn, ref message);
            MidRunJoin.BeginAuthentication(conn, message, out __state);
        }

        private static void Finalizer(
            NetworkConnectionToClient conn,
            HorayNetworkAuthenticator.VersionMessage message,
            bool __state,
            Exception __exception)
        {
            MidRunJoin.EndAuthentication(conn, message, __state, __exception == null);
        }
    }

    [HarmonyPatch(typeof(HorayNetworkAuthenticator), "get_AccessDeny_InDungeon")]
    internal static class ScopedDungeonGatePatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!MidRunJoin.BypassDungeonGate)
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), "get_AllowRejoin")]
    internal static class EnableReconnectPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Plugin.allowMidRunJoin.Value) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(GridInventory), nameof(GridInventory.AddStartingItem))]
    internal static class FreshPlayerStartingItemPatch
    {
        private static void Prefix(GridInventory __instance, out bool __state)
        {
            PlayerSpawner spawner = __instance.GetComponent<PlayerSpawner>();
            MidRunJoin.BeginFreshPlayerOperation(spawner != null ? spawner.connectionToClient : null, out __state);
        }

        private static void Finalizer(bool __state)
        {
            MidRunJoin.EndFreshPlayerOperation(__state);
        }
    }

    [HarmonyPatch(typeof(DungeonManager), "LoadStageAndMove")]
    internal static class KeepLobbyOpenOnRunStartPatch
    {
        private static void Postfix()
        {
            if (!Plugin.allowMidRunJoin.Value)
            {
                return;
            }

            GameObject steamManager = SingletonObject.Find("SteamManager");
            if (steamManager != null && App.Initialized &&
                steamManager.TryGetComponent(out LobbyManager lobbyManager) && lobbyManager.HasLobby)
            {
                LobbyData lobby = lobbyManager.Lobby;
                lobby["pw"] = "open";
                lobby["SephiriaTogether"] = Plugin.PluginVersion;
                if (Plugin.allowLowerProgressPlayers.Value)
                {
                    lobby["Chapter"] = "0";
                }
            }
        }
    }


    [HarmonyPatch(typeof(PlayerSpawner), "ResolveCurrentPlayerIdxForSave")]
    internal static class FreshPlayerSaveSlotPatch
    {
        private static void Prefix(PlayerSpawner __instance)
        {
            if (!MidRunJoin.IsFreshConnection(__instance.connectionToClient) || SaveManager.CurrentRun == null)
            {
                return;
            }

            int newSlot = Math.Max(0, SaveManager.CurrentRun.GetInt("SavedPlayerCount", 0));
            __instance.NetworkcurrentPlayerIdxForSave = newSlot;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnServerDisconnect))]
    internal static class MidRunDisconnectCleanupPatch
    {
        private static void Postfix(NetworkConnectionToClient conn)
        {
            MidRunJoin.RemoveConnection(conn);
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), "OnStopServer")]
    internal static class MidRunStopServerCleanupPatch
    {
        private static void Postfix()
        {
            MidRunJoin.ClearConnections();
        }
    }

    [HarmonyPatch(typeof(PlayerSpawner), "RestorePreservedRejoinables")]
    internal static class MidRunCatchUpPatch
    {
        private static void Postfix(PlayerSpawner __instance)
        {
            MidRunJoin.ScheduleCatchUp(__instance);
        }
    }
}
