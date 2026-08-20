using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class FusionCompensation
    {
        private const string ObservedFloorsKey = "SephiriaTogetherFusionFloors";
        private const string PropIdKey = "SephiriaTogetherFusionPropId";
        private static readonly HashSet<string> ObservedFloors = new HashSet<string>();
        private static readonly Dictionary<uint, PlayerSpawner> SpawnedFor = new Dictionary<uint, PlayerSpawner>();
        private static string propId;

        internal static void ObserveSpawner(PropSpawnerEachPlayer spawner)
        {
            if (!NetworkServer.active || spawner == null || string.IsNullOrEmpty(spawner.propID)) return;
            PropEntity entity = PropDatabase.FindPropById(spawner.propID);
            if (entity?.propPrefab == null || entity.propPrefab.GetComponent<TabletMix_Personal>() == null) return;
            FloorGenerator floor = spawner.GetComponentInParent<FloorGenerator>();
            if (floor == null || string.IsNullOrEmpty(floor.guid)) return;
            propId = spawner.propID;
            if (ObservedFloors.Add(floor.guid)) SaveObservedFloors();
            Plugin.LogInfo($"Vanilla Tablet Fusion observed: floor={Short(floor.guid)}, prop={propId}.");
            foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList ?? Enumerable.Empty<PlayerSpawner>())
                if (player?.PlayerAvatar != null && player.PlayerAvatar.currentFloorGuid == floor.guid)
                    CatchUpRewards.RecordPendingFusion(player, floor.guid);
        }

        internal static bool IsObservedFloor(string floorGuid)
        {
            LoadObservedFloors();
            return !string.IsNullOrEmpty(floorGuid) && ObservedFloors.Contains(floorGuid);
        }

        internal static void OnFloorChanged(PlayerSpawner player, string floorGuid)
        {
            if (!NetworkServer.active || player == null) return;
            if (Plugin.InstanceForPatches != null)
                Plugin.InstanceForPatches.StartCoroutine(EnsureCurrentOpportunity(player, floorGuid));
        }

        private static IEnumerator EnsureCurrentOpportunity(PlayerSpawner player, string floorGuid)
        {
            yield return new WaitForSeconds(1f);
            if (player?.PlayerAvatar == null || player.PlayerAvatar.currentFloorGuid != floorGuid) yield break;
            CatchUpRewards.ConvertPendingFusions(player, floorGuid);
            if (IsObservedFloor(floorGuid)) CatchUpRewards.RecordPendingFusion(player, floorGuid);
            bool hasPersonalMix = player.connectionToClient != null && player.connectionToClient.owned
                .Any(identity => identity != null && identity.GetComponent<TabletMix_Personal>() != null);
            if (!hasPersonalMix) CatchUpRewards.ConvertCurrentFusionToCredit(player);
            TrySpawn(player);
        }

        internal static void ScheduleSpawn(PlayerSpawner player)
        {
            if (Plugin.InstanceForPatches != null && player != null)
                Plugin.InstanceForPatches.StartCoroutine(SpawnAfterTravel(player));
        }

        private static IEnumerator SpawnAfterTravel(PlayerSpawner player)
        {
            yield return new WaitForSeconds(1f);
            TrySpawn(player);
        }

        private static bool TrySpawn(PlayerSpawner player)
        {
            if (!CatchUpRewards.CanSpawnCompensation(player) || CatchUpRewards.AvailableFusionCredits(player) <= 0 ||
                SpawnedFor.Values.Contains(player)) return false;
            LoadObservedFloors();
            GameObject prefab = null;
            if (!string.IsNullOrEmpty(propId)) prefab = PropDatabase.FindPropById(propId)?.propPrefab;
            if (prefab == null)
                prefab = Resources.FindObjectsOfTypeAll<TabletMix_Personal>()
                    .Select(mix => mix != null ? mix.gameObject : null)
                    .FirstOrDefault(candidate => candidate != null && candidate.GetComponent<NetworkIdentity>() != null);
            if (prefab == null)
            {
                Plugin.LogInfo("Unable to find the vanilla personal Tablet Fusion prefab for catch-up.");
                return false;
            }
            GameObject instance = UnityEngine.Object.Instantiate(
                prefab, player.PlayerAvatar.transform.position + Vector3.down * 2f, Quaternion.identity);
            TabletMix_Personal mix = instance.GetComponent<TabletMix_Personal>();
            if (mix == null)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }
            NetworkServer.Spawn(instance, player.connectionToClient);
            PersonalizedVisibility.Register(mix.netIdentity, player.connectionToClient);
            SpawnedFor[mix.netId] = player;
            CatchUpRewards.LockFusionCredit(player);
            Plugin.LogInfo($"Catch-up Tablet Fusion spawned: player={player.PlayerAvatar.Name}, netId={mix.netId}, " +
                           $"floor={Short(player.PlayerAvatar.currentFloorGuid)}, cost={mix.mixCost}.");
            return true;
        }

        internal static void Complete(TabletMix mix, PlayerSpawner player)
        {
            if (mix == null || player == null) return;
            if (!SpawnedFor.TryGetValue(mix.netId, out PlayerSpawner owner) || owner != player)
            {
                CatchUpRewards.MarkCurrentFusionClaimed(player);
                return;
            }
            PersonalizedVisibility.Unregister(mix.netIdentity);
            SpawnedFor.Remove(mix.netId);
            CatchUpRewards.CompleteFusionCredit(player);
            NetworkServer.Destroy(mix.gameObject);
            ScheduleSpawn(player);
        }

        internal static void Release(TabletMix mix)
        {
            if (mix == null || !SpawnedFor.TryGetValue(mix.netId, out PlayerSpawner player)) return;
            PersonalizedVisibility.Unregister(mix.netIdentity);
            SpawnedFor.Remove(mix.netId);
            CatchUpRewards.ReleaseFusionCredit(player);
            if (player?.connectionToClient != null) ScheduleSpawn(player);
        }

        internal static void RemoveConnection(NetworkConnectionToClient connection)
        {
            PlayerSpawner player = connection?.identity != null ? connection.identity.GetComponent<PlayerSpawner>() : null;
            if (player == null) return;
            foreach (uint netId in SpawnedFor.Where(entry => entry.Value == player).Select(entry => entry.Key).ToArray())
            {
                SpawnedFor.Remove(netId);
                CatchUpRewards.ReleaseFusionCredit(player);
                if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
                {
                    PersonalizedVisibility.Unregister(identity);
                    NetworkServer.Destroy(identity.gameObject);
                }
            }
        }

        internal static void Clear()
        {
            SpawnedFor.Clear();
            ObservedFloors.Clear();
            propId = null;
        }

        private static void LoadObservedFloors()
        {
            if (SaveManager.CurrentRun == null) return;
            foreach (string floor in SaveManager.CurrentRun.GetString(ObservedFloorsKey, "")
                         .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                ObservedFloors.Add(floor);
            if (string.IsNullOrEmpty(propId)) propId = SaveManager.CurrentRun.GetString(PropIdKey, "");
        }

        private static void SaveObservedFloors()
        {
            if (SaveManager.CurrentRun == null) return;
            SaveManager.CurrentRun.SetString(ObservedFloorsKey, string.Join("|", ObservedFloors));
            SaveManager.CurrentRun.SetString(PropIdKey, propId ?? "");
            SaveManager.Save(saveCurrent: false, saveCurrentRun: true);
        }

        private static string Short(string value) => string.IsNullOrEmpty(value)
            ? "-"
            : value.Substring(0, Math.Min(8, value.Length));
    }

    [HarmonyPatch(typeof(PropSpawnerEachPlayer), nameof(PropSpawnerEachPlayer.OnStartServer))]
    internal static class ObserveTabletFusionPatch
    {
        private static void Prefix(PropSpawnerEachPlayer __instance) => FusionCompensation.ObserveSpawner(__instance);
    }

    [HarmonyPatch(typeof(GridInventory), nameof(GridInventory.ServerMixTablet))]
    internal static class CompleteTabletFusionPatch
    {
        private static void Prefix(GridInventory __instance, TabletMix tabletMix, out bool __state)
        {
            PlayerSpawner player = __instance != null ? __instance.GetComponent<PlayerSpawner>() : null;
            __state = tabletMix != null && player != null && tabletMix.IsUsedByGuid(player.playerGuid);
        }

        private static void Postfix(GridInventory __instance, TabletMix tabletMix, bool __state)
        {
            PlayerSpawner player = __instance != null ? __instance.GetComponent<PlayerSpawner>() : null;
            if (!__state && tabletMix != null && player != null && tabletMix.IsUsedByGuid(player.playerGuid))
                FusionCompensation.Complete(tabletMix, player);
        }
    }

    [HarmonyPatch(typeof(TabletMix), "OnDestroy")]
    internal static class ReleaseTabletFusionPatch
    {
        private static void Prefix(TabletMix __instance) => FusionCompensation.Release(__instance);
    }
}
