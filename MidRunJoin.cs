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
    internal struct FreshPocketItemsMessage : NetworkMessage
    {
        public int[] itemIds;
    }

    internal static class MidRunJoin
    {
        private static readonly HashSet<NetworkConnectionToClient> FreshConnections =
            new HashSet<NetworkConnectionToClient>();
        private static readonly HashSet<int> FreshSessionConnectionIds = new HashSet<int>();
        private static readonly Dictionary<int, int[]> FreshPocketItems = new Dictionary<int, int[]>();
        private static readonly FieldInfo RejoinDetectedField =
            AccessTools.Field(typeof(PlayerSpawner), "isRejoinDetected");
        private static readonly FieldInfo VersionApprovedConnectionsField =
            AccessTools.Field(typeof(HorayNetworkManager), "versionApprovedConnIds");
        private static readonly FieldInfo RejoinWhitelistField =
            AccessTools.Field(typeof(HorayNetworkManager), "rejoinWhitelist");

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
            Log?.LogInfo($"Join auth begin: conn={connection?.connectionId ?? -1}, guid={ShortId(message.playerGuid)}, " +
                         $"denyInDungeon={HorayNetworkAuthenticator.AccessDeny_InDungeon}, server={NetworkServer.active}.");
            if (!Plugin.allowMidRunJoin.Value || !NetworkServer.active ||
                !HorayNetworkAuthenticator.AccessDeny_InDungeon)
            {
                Log?.LogInfo($"Join auth uses vanilla path: conn={connection?.connectionId ?? -1}, midRun={Plugin.allowMidRunJoin.Value}.");
                return;
            }

            HorayNetworkManager manager = NetworkManager.singleton as HorayNetworkManager;
            if (manager == null || NetworkServer.localConnection == connection || SaveManager.CurrentRun == null ||
                SaveManager.CurrentRun.GetInt("SaveVersion", 0) == 0)
            {
                Log?.LogInfo($"Join auth bypass unavailable: conn={connection?.connectionId ?? -1}, manager={manager != null}, " +
                             $"local={NetworkServer.localConnection == connection}, run={SaveManager.CurrentRun != null}.");
                return;
            }

            if (manager != null && !string.IsNullOrWhiteSpace(message.playerGuid) &&
                manager.IsRejoinBanned(message.playerGuid))
            {
                Log?.LogWarning($"Join auth blocked by rejoin ban: conn={connection.connectionId}, guid={ShortId(message.playerGuid)}.");
                return;
            }

            bool isKnownRejoin = manager.IsInRejoinWhitelist(message.playerGuid);
            if (isKnownRejoin && IsStaleRejoin(message.playerGuid))
            {
                HashSet<string> whitelist = RejoinWhitelistField?.GetValue(manager) as HashSet<string>;
                whitelist?.Remove(message.playerGuid);
                isKnownRejoin = false;
                Log?.LogWarning($"Ignored stale cross-run rejoin identity for connection {connection.connectionId}.");
            }
            if (isKnownRejoin)
            {
                Log?.LogInfo($"Join auth classified as rejoin: conn={connection.connectionId}, guid={ShortId(message.playerGuid)}.");
                return;
            }

            __state = true;
            bypassDungeonGate = true;
            Log?.LogInfo($"Join auth classified as fresh mid-run: conn={connection.connectionId}, guid={ShortId(message.playerGuid)}.");
        }

        private static bool IsStaleRejoin(string playerGuid)
        {
            if (SaveManager.CurrentRun == null || string.IsNullOrWhiteSpace(playerGuid)) return false;
            PlayerSpawner host = PlayerSpawner.MultiplayerList?
                .FirstOrDefault(player => player != null && player.isHost && player.PlayerAvatar != null);
            if (host?.PlayerAvatar == null || host.PlayerAvatar.floorTravelHistory.Count == 0) return false;

            int count = SaveManager.CurrentRun.GetInt("SavedPlayerCount", 0);
            for (int slot = 0; slot < count; slot++)
            {
                if (SaveManager.CurrentRun.GetString($"Player{slot}Guid", "") != playerGuid) continue;
                int floorCount = SaveManager.CurrentRun.GetInt($"Player{slot}FloorTravelHistoryCount", 0);
                if (floorCount == 0) return false;
                HashSet<string> hostFloors = new HashSet<string>(host.PlayerAvatar.floorTravelHistory);
                for (int i = 0; i < floorCount; i++)
                    if (hostFloors.Contains(SaveManager.CurrentRun.GetString($"Player{slot}FloorTravelHistory{i}", "")))
                        return false;
                return true;
            }
            return false;
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
            Log?.LogInfo($"Join auth end: conn={connection?.connectionId ?? -1}, freshCandidate={__state}, succeeded={succeeded}.");
            if (succeeded)
            {
                HorayNetworkManager manager = NetworkManager.singleton as HorayNetworkManager;
                HashSet<int> approved = VersionApprovedConnectionsField?.GetValue(manager) as HashSet<int>;
                if (message.version == Application.version &&
                    approved != null && approved.Contains(connection.connectionId))
                {
                    FreshConnections.Add(connection);
                    FreshSessionConnectionIds.Add(connection.connectionId);
                    Log?.LogInfo($"Authorized fresh mid-run connection {connection.connectionId}.");
                }
            }
        }

        internal static bool IsFreshConnection(NetworkConnectionToClient connection)
        {
            return connection != null && FreshConnections.Contains(connection);
        }

        internal static void ConfigureMessages()
        {
            Writer<FreshPocketItemsMessage>.write = (writer, value) =>
            {
                int[] items = value.itemIds ?? Array.Empty<int>();
                writer.WriteVarInt(items.Length);
                foreach (int item in items) writer.WriteVarInt(item);
            };
            Reader<FreshPocketItemsMessage>.read = reader =>
            {
                int count = Math.Min(64, Math.Max(0, reader.ReadVarInt()));
                int[] items = new int[count];
                for (int i = 0; i < count; i++) items[i] = reader.ReadVarInt();
                return new FreshPocketItemsMessage { itemIds = items };
            };
        }

        internal static void RegisterServerMessages()
        {
            ConfigureMessages();
            NetworkServer.RegisterHandler<FreshPocketItemsMessage>(OnServerFreshPocketItems, true);
        }

        internal static void SendFreshPocketItems(PlayerLocalDataStorage storage)
        {
            if (storage != null && Plugin.InstanceForPatches != null)
            {
                Log?.LogInfo($"Dimension Pocket upload scheduled: localCount={storage.dimensionPocketItem.Count}.");
                Plugin.InstanceForPatches.StartCoroutine(SendFreshPocketItemsWhenReady(storage));
            }
        }

        private static IEnumerator SendFreshPocketItemsWhenReady(PlayerLocalDataStorage storage)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (storage != null && NetworkClient.active &&
                   (!NetworkClient.ready || !CatchUpRewards.HostSupportsProtocol()) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            if (storage != null && NetworkClient.active && NetworkClient.ready && CatchUpRewards.HostSupportsProtocol())
            {
                Log?.LogInfo($"Dimension Pocket upload sent: count={storage.dimensionPocketItem.Count}.");
                NetworkClient.Send(new FreshPocketItemsMessage { itemIds = storage.dimensionPocketItem.ToArray() });
            }
            else
            {
                Log?.LogWarning($"Dimension Pocket upload not sent before timeout: storage={storage != null}, " +
                                $"client={NetworkClient.active}, ready={NetworkClient.ready}, protocol={CatchUpRewards.HostSupportsProtocol()}.");
            }
        }

        private static void OnServerFreshPocketItems(NetworkConnectionToClient connection, FreshPocketItemsMessage message)
        {
            if (connection == null) return;
            FreshPocketItems[connection.connectionId] = (message.itemIds ?? Array.Empty<int>())
                .Where(id => ItemDatabase.FindItemById(id) != null)
                .ToArray();
            Log?.LogInfo($"Received {FreshPocketItems[connection.connectionId].Length} Dimension Pocket items from connection {connection.connectionId}; " +
                         $"fresh={FreshSessionConnectionIds.Contains(connection.connectionId)}.");
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
                PlayerSpawner player = connection.identity != null
                    ? connection.identity.GetComponent<PlayerSpawner>()
                    : null;
                Log?.LogInfo($"Connection cleanup: conn={connection.connectionId}, player={Describe(player)}, " +
                             $"fresh={FreshConnections.Contains(connection)}.");
                CatchUpRewards.ConvertAllPendingAnvils(player);
                FreshConnections.Remove(connection);
                FreshSessionConnectionIds.Remove(connection.connectionId);
                FreshPocketItems.Remove(connection.connectionId);
                CatchUpRewards.RemoveConnection(connection);
            }
        }

        internal static void ClearConnections()
        {
            FreshConnections.Clear();
            FreshSessionConnectionIds.Clear();
            FreshPocketItems.Clear();
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
            Log?.LogInfo($"Catch-up scheduling check: conn={spawner.connectionToClient.connectionId}, fresh={isFresh}, " +
                         $"rejoin={isRejoin}, player={Describe(spawner)}.");
            if ((isFresh || isRejoin) && Plugin.InstanceForPatches != null)
            {
                Plugin.InstanceForPatches.StartCoroutine(CatchUpAfterTravel(spawner, isFresh, isRejoin));
            }
        }

        private static IEnumerator CatchUpAfterTravel(PlayerSpawner spawner, bool isFresh, bool isRejoin)
        {
            Log?.LogInfo($"Catch-up travel wait started: fresh={isFresh}, rejoin={isRejoin}, player={Describe(spawner)}.");
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

            Log?.LogInfo($"Catch-up travel ready: fresh={isFresh}, rejoin={isRejoin}, player={Describe(spawner)}.");

            if (string.IsNullOrEmpty(spawner.PlayerAvatar.currentFloorGuid) || spawner.PlayerAvatar.isInDungeon <= 0)
            {
                Log?.LogWarning($"Catch-up skipped because {spawner.PlayerAvatar.name} did not finish floor travel in time.");
                yield break;
            }

            if (isFresh && spawner.PlayerAvatar.IsDead)
            {
                spawner.PlayerAvatar.Revive(spawner.PlayerAvatar.MaxHp);
                Log?.LogWarning($"Cleared stale downed state for fresh mid-run player {spawner.PlayerAvatar.Name}.");
            }

            LevelController newcomer = spawner.GetComponent<LevelController>();
            if (newcomer == null)
            {
                yield break;
            }

            PlayerSpawner routeSource = PlayerSpawner.MultiplayerList
                .Where(peer => peer != null && peer != spawner && peer.PlayerAvatar != null &&
                               !CloneBotManager.IsBot(peer))
                .OrderByDescending(peer => peer.PlayerAvatar.floorTravelHistory.Count)
                .ThenByDescending(peer => peer.isHost)
                .FirstOrDefault();
            HashSet<string> newcomerHistory = new HashSet<string>(spawner.PlayerAvatar.floorTravelHistory);
            List<string> missedFloors = routeSource != null
                ? routeSource.PlayerAvatar.floorTravelHistory
                    .Where(guid => !string.IsNullOrEmpty(guid) &&
                        guid != routeSource.PlayerAvatar.currentFloorGuid &&
                        guid != spawner.PlayerAvatar.currentFloorGuid && !newcomerHistory.Contains(guid))
                    .Distinct()
                    .ToList()
                : new List<string>();
            Log?.LogInfo($"Catch-up route comparison: player={Describe(spawner)}, source={Describe(routeSource)}, " +
                         $"localHistory={newcomerHistory.Count}, sourceHistory={routeSource?.PlayerAvatar?.floorTravelHistory.Count ?? 0}, " +
                         $"missed={missedFloors.Count}.");
            bool grantResources = CatchUpRewards.HasNewResourceFloors(spawner, missedFloors);
            CatchUpRewards.Prepare(spawner, missedFloors);
            List<int> peerExperience = new List<int>();
            List<int> peerMoney = new List<int>();
            List<int> peerDice = new List<int>();
            List<int> peerMaxDice = new List<int>();
            foreach (PlayerSpawner peer in PlayerSpawner.MultiplayerList)
            {
                if (peer == null || peer == spawner || peer.PlayerAvatar == null || CloneBotManager.IsBot(peer) ||
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

            if (isFresh && peerExperience.Count > 0)
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

            int[] pocketItems = null;
            bool pocketAlreadyGranted = CatchUpRewards.WasDimensionPocketGranted(spawner);
            if (!pocketAlreadyGranted && spawner.connectionToClient != null &&
                !FreshPocketItems.ContainsKey(spawner.connectionToClient.connectionId))
            {
                float pocketDeadline = Time.realtimeSinceStartup + 3f;
                while (spawner != null && spawner.connectionToClient != null &&
                       !FreshPocketItems.ContainsKey(spawner.connectionToClient.connectionId) &&
                       Time.realtimeSinceStartup < pocketDeadline)
                    yield return null;
            }
            if (!pocketAlreadyGranted && spawner.connectionToClient != null)
                FreshPocketItems.TryGetValue(spawner.connectionToClient.connectionId, out pocketItems);
            if (!pocketAlreadyGranted && (pocketItems == null || pocketItems.Length == 0) && spawner.LocalDataStorage != null)
            {
                pocketItems = spawner.LocalDataStorage.dimensionPocketItem.ToArray();
                Log?.LogInfo($"Dimension Pocket using synchronized fallback: conn={spawner.connectionToClient?.connectionId ?? -1}, count={pocketItems.Length}.");
            }
            if (!pocketAlreadyGranted && pocketItems != null && pocketItems.Length > 0)
            {
                int before = CountInventoryItems(spawner.PlayerAvatar.Inventory, pocketItems);
                Log?.LogInfo($"Dimension Pocket grant begin: player={Describe(spawner)}, count={pocketItems.Length}, " +
                             $"capacity={spawner.PlayerAvatar.Inventory?.dimensionPocket ?? -1}, matchingBefore={before}.");
                spawner.AddDimensionPocketItemsOnServer(pocketItems);
                int after = CountInventoryItems(spawner.PlayerAvatar.Inventory, pocketItems);
                int added = Math.Max(0, after - before);
                if (added > 0) CatchUpRewards.MarkDimensionPocketGranted(spawner);
                Log?.LogInfo($"Dimension Pocket grant end: player={Describe(spawner)}, inventoryStorage={spawner.PlayerAvatar.Inventory?.CurrentInventoryStorage ?? -1}, " +
                             $"matchingAfter={after}, added={added}, marked={added > 0}.");
            }
            if (spawner.connectionToClient != null)
            {
                FreshPocketItems.Remove(spawner.connectionToClient.connectionId);
                FreshSessionConnectionIds.Remove(spawner.connectionToClient.connectionId);
            }

            if (grantResources) CatchUpRewards.CommitResourceFloors(spawner, missedFloors);
            spawner.SaveCurrentSessionData();
            SaveManager.Save(saveCurrent: false, saveCurrentRun: true);

            Log?.LogInfo(
                $"{(isRejoin ? "Rejoin" : "Fresh-join")} resources for {spawner.PlayerAvatar.name}: " +
                $"missed floors {missedFloors.Count}, money +{money}, " +
                $"dice +{dice}, max dice +{maxDice}, " +
                $"pocket items {(pocketAlreadyGranted ? 0 : pocketItems?.Length ?? 0)}, exp={newcomer.currentExp}, level={newcomer.currentLevel}.");

            yield return PlaceInsideActiveBossRoom(spawner);
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

        private static IEnumerator PlaceInsideActiveBossRoom(PlayerSpawner spawner)
        {
            PlayerAvatar newcomer = spawner?.PlayerAvatar;
            FloorGenerator floor = newcomer != null ? FloorGenerator.FindByGuid(newcomer.currentFloorGuid) : null;
            if (floor == null) yield break;

            BossSpawner boss = FindActiveBoss(floor);
            if (boss == null)
            {
                Log?.LogInfo($"Boss join placement not needed: player={Describe(spawner)}, no active boss on floor.");
                yield break;
            }

            Vector2 lower = (Vector2)boss.transform.position + boss.playerPreventArea_lb;
            Vector2 upper = (Vector2)boss.transform.position + boss.playerPreventArea_rt;
            Rect battleArea = Rect.MinMaxRect(
                Mathf.Min(lower.x, upper.x), Mathf.Min(lower.y, upper.y),
                Mathf.Max(lower.x, upper.x), Mathf.Max(lower.y, upper.y));
            if (battleArea.width <= 0.25f || battleArea.height <= 0.25f) yield break;

            Vector3 destination = FindBossJoinPosition(spawner, boss, battleArea);
            Log?.LogInfo($"Boss join placement: player={Describe(spawner)}, boss={boss.name}, area={battleArea}, destination={destination}.");
            newcomer.ReqSetPosition(destination, teleport: true);
            yield return new WaitForSeconds(0.5f);
            if (boss != null && newcomer != null && !battleArea.Contains(newcomer.transform.position))
            {
                Log?.LogWarning($"Boss join placement retry: player={Describe(spawner)}, destination={destination}.");
                newcomer.ReqSetPosition(destination, teleport: true);
            }
            else
            {
                Log?.LogInfo($"Boss join placement confirmed: player={Describe(spawner)}.");
            }
        }

        private static BossSpawner FindActiveBoss(FloorGenerator floor)
        {
            foreach (BossSpawner candidate in Resources.FindObjectsOfTypeAll<BossSpawner>())
            {
                try
                {
                    if (candidate != null && candidate.isServer && candidate.IsBossBattleInProgress &&
                        (candidate.parent == floor || candidate.transform.IsChildOf(floor.transform)))
                        return candidate;
                }
                catch (NullReferenceException)
                {
                    // Unity can destroy a cached spawner between enumeration and property access.
                }
            }
            return null;
        }

        private static int CountInventoryItems(GridInventory inventory, int[] entityIds)
        {
            if (inventory == null || entityIds == null || entityIds.Length == 0) return 0;
            HashSet<int> wanted = new HashSet<int>(entityIds);
            int count = 0;
            foreach (NewItemOwnInstance item in inventory.inventoryMatrix.Values)
                if (item != null && item.Entity != null && wanted.Contains(item.Entity.id)) count += Math.Max(1, (int)item.Quantity);
            return count;
        }

        private static Vector3 FindBossJoinPosition(PlayerSpawner newcomer, BossSpawner boss, Rect area)
        {
            const float inset = 1.25f;
            float minX = area.xMin + Mathf.Min(inset, area.width * 0.25f);
            float maxX = area.xMax - Mathf.Min(inset, area.width * 0.25f);
            float minY = area.yMin + Mathf.Min(inset, area.height * 0.25f);
            float maxY = area.yMax - Mathf.Min(inset, area.height * 0.25f);

            PlayerAvatar peer = PlayerSpawner.MultiplayerList
                .Where(candidate => candidate != null && candidate != newcomer && candidate.PlayerAvatar != null &&
                    !CloneBotManager.IsBot(candidate) &&
                    candidate.PlayerAvatar.currentFloorGuid == newcomer.PlayerAvatar.currentFloorGuid &&
                    area.Contains(candidate.PlayerAvatar.transform.position))
                .OrderBy(candidate => candidate.PlayerAvatar.IsDead)
                .Select(candidate => candidate.PlayerAvatar)
                .FirstOrDefault();
            Vector3 center = peer != null ? peer.transform.position + Vector3.right * 0.8f
                : boss.BossEnvironment != null ? boss.BossEnvironment.BossZoneCenter
                : area.center;
            center.x = Mathf.Clamp(center.x, minX, maxX);
            center.y = Mathf.Clamp(center.y, minY, maxY);

            return center;
        }

        private static string Describe(PlayerSpawner player)
        {
            PlayerAvatar avatar = player?.PlayerAvatar;
            return avatar == null
                ? "null"
                : $"name={avatar.Name}, conn={player.connectionToClient?.connectionId ?? -1}, guid={ShortId(player.playerGuid)}, " +
                  $"floor={ShortId(avatar.currentFloorGuid)}, pos={avatar.transform.position}, dead={avatar.IsDead}, " +
                  $"hp={avatar.hp:0.##}/{avatar.MaxHp:0.##}, inDungeon={avatar.isInDungeon}";
        }

        private static string ShortId(string value) => string.IsNullOrEmpty(value)
            ? "-"
            : value.Substring(0, Math.Min(8, value.Length));
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
        private static bool Prefix(PlayerSpawner __instance, string playerGuid)
        {
            if (!MidRunJoin.IsFreshConnection(__instance.connectionToClient) || SaveManager.CurrentRun == null)
            {
                return true;
            }

            int newSlot = Math.Max(0, SaveManager.CurrentRun.GetInt("SavedPlayerCount", 0));
            __instance.NetworkcurrentPlayerIdxForSave = newSlot;
            SaveManager.CurrentRun.SetInt("SavedPlayerCount", newSlot + 1);
            if (!string.IsNullOrWhiteSpace(playerGuid))
                SaveManager.CurrentRun.SetString($"Player{newSlot}Guid", playerGuid);
            Plugin.LogInfo($"Fresh mid-run save slot assigned: {newSlot}.");
            return false;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnServerDisconnect))]
    internal static class MidRunDisconnectCleanupPatch
    {
        private static void Prefix(NetworkConnectionToClient conn)
        {
            int creatures = CombatManager.Instance?.AllCreatures?.Count ?? 0;
            PlayerSpawner player = conn?.identity != null ? conn.identity.GetComponent<PlayerSpawner>() : null;
            string address = conn != null && NetworkManager.singleton?.transport != null
                ? NetworkManager.singleton.transport.ServerGetClientAddress(conn.connectionId)
                : "-";
            Plugin.LogInfo($"Server disconnect observed: conn={conn?.connectionId ?? -1}, " +
                            $"player={player?.PlayerAvatar?.Name ?? "-"}, address={address}, " +
                            $"ready={conn?.isReady ?? false}, players={PlayerSpawner.MultiplayerList?.Count ?? 0}, " +
                           $"connections={NetworkServer.connections.Count}, creatures={creatures}.");
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

    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.OnStartClient))]
    internal static class LateJoinedDeadUnitStatePatch
    {
        private static void Postfix(UnitAvatar __instance)
        {
            if (__instance == null || __instance is PlayerAvatar || !__instance.IsDead) return;
            ApplyDeadState(__instance);
            if (Plugin.InstanceForPatches != null)
                Plugin.InstanceForPatches.StartCoroutine(ApplyAfterStart(__instance));
        }

        private static IEnumerator ApplyAfterStart(UnitAvatar unit)
        {
            yield return null;
            if (unit != null && unit.IsDead) ApplyDeadState(unit);
        }

        private static void ApplyDeadState(UnitAvatar unit)
        {
            Traverse.Create(unit).Method("ApplyCorpseVisual").GetValue();
            unit.HPBar?.Hide(forced: true);
        }
    }

    [HarmonyPatch(typeof(DropItemOnDie), nameof(DropItemOnDie.DropEXP))]
    internal static class SafeExperienceDropPatch
    {
        private static readonly AccessTools.FieldRef<DropItemOnDie, bool> AlreadyDropped =
            AccessTools.FieldRefAccess<DropItemOnDie, bool>("isEXPAlreadyDropped");

        private static bool Prefix(DropItemOnDie __instance, int exp)
        {
            if (__instance == null || !NetworkServer.active || !__instance.isServer) return true;
            if (AlreadyDropped(__instance)) return false;
            AlreadyDropped(__instance) = true;
            try
            {
                NetworkConnectionToClient[] snapshot = NetworkServer.connections.Values.ToArray();
                List<KeyValuePair<NetworkConnectionToClient, PlayerAvatar>> recipients = new List<KeyValuePair<NetworkConnectionToClient, PlayerAvatar>>();
                foreach (NetworkConnectionToClient connection in snapshot)
                {
                    if (connection == null || !connection.isAuthenticated || !connection.isReady ||
                        connection.identity == null || connection.identity.netId == 0)
                    {
                        continue;
                    }

                    PlayerAvatar player = null;
                    try
                    {
                        player = connection.identity.GetComponent<PlayerAvatar>();
                    }
                    catch (Exception exception)
                    {
                        Plugin.LogInfo($"Safe EXP recipient inspection failed for connection " +
                                       $"{connection.connectionId}: {exception.Message}");
                    }

                    if (player != null && player.spawner != null && !CloneBotManager.IsBot(player.spawner) &&
                        player.isInDungeon > 0 && !string.IsNullOrEmpty(player.currentFloorGuid))
                        recipients.Add(new KeyValuePair<NetworkConnectionToClient, PlayerAvatar>(connection, player));
                }
                if (recipients.Count == 0)
                {
                    Plugin.LogInfo("Skipped EXP drop because no initialized in-dungeon player connection was ready.");
                    return false;
                }

                GameObject largePrefab = Resources.Load<GameObject>("Exp");
                GameObject smallPrefab = Resources.Load<GameObject>("ExpMini");
                int divisor = Mathf.Clamp(recipients.Count, 1, 3);
                int spawned = 0;
                int failed = 0;
                foreach (KeyValuePair<NetworkConnectionToClient, PlayerAvatar> recipient in recipients)
                {
                    int adjusted = exp;
                    try
                    {
                        adjusted += (int)(exp * (recipient.Value.GetCustomStat(ECustomStat.EXPDrop) / 100f));
                    }
                    catch (Exception exception)
                    {
                        Plugin.LogInfo($"Safe EXP stat lookup failed for connection " +
                                       $"{recipient.Key.connectionId}: {exception.Message}");
                    }
                    for (int remaining = Math.Max(0, adjusted); remaining > 0; remaining -= 100)
                    {
                        int chunk = Math.Min(remaining, 100);
                        GameObject prefab = chunk < 100 ? smallPrefab : largePrefab;
                        if (prefab == null)
                        {
                            failed++;
                            continue;
                        }
                        GameObject instance = null;
                        try
                        {
                            Vector3 position = __instance.transform.position;
                            position.z = 0f;
                            instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
                            Exp experience = instance.GetComponent<Exp>();
                            if (experience == null)
                            {
                                DestroyFailedExperience(instance);
                                failed++;
                                continue;
                            }
                            experience.target = recipient.Value;
                            experience.amount = Mathf.RoundToInt((float)chunk / divisor);
                            if (experience.amount <= 0)
                            {
                                DestroyFailedExperience(instance);
                                failed++;
                                continue;
                            }
                            experience.ignoreAdjustment = true;
                            NetworkServer.Spawn(instance, recipient.Key);
                            experience.AddPhysicalForce(UnityEngine.Random.insideUnitCircle * 3f,
                                UnityEngine.Random.Range(3f, 5f));
                            spawned++;
                        }
                        catch (Exception exception)
                        {
                            DestroyFailedExperience(instance);
                            failed++;
                            Plugin.LogInfo($"Safe EXP object creation failed for connection " +
                                           $"{recipient.Key.connectionId}: {exception.Message}");
                        }
                    }
                }
                Plugin.LogInfo($"Safe EXP drop: eligible={recipients.Count}, divisor={divisor}, " +
                               $"connections={snapshot.Length}, excluded={snapshot.Length - recipients.Count}, " +
                               $"spawned={spawned}, failed={failed}, base={exp}.");
                return false;
            }
            catch (Exception exception)
            {
                Plugin.LogInfo("Safe EXP drop failed without interrupting unit death: " + exception);
                return false;
            }
        }

        private static void DestroyFailedExperience(GameObject instance)
        {
            if (instance == null) return;
            try
            {
                NetworkIdentity identity = instance.GetComponent<NetworkIdentity>();
                if (identity != null && identity.netId != 0) NetworkServer.Destroy(instance);
                else UnityEngine.Object.Destroy(instance);
            }
            catch (Exception exception)
            {
                Plugin.LogInfo("Safe EXP cleanup failed: " + exception.Message);
                UnityEngine.Object.Destroy(instance);
            }

            if (Exp.managedExpInstances != null)
                Exp.managedExpInstances.RemoveAll(candidate =>
                    candidate == null || ReferenceEquals(candidate, instance));
        }

    }
}
