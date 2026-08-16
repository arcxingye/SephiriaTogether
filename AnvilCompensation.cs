using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class AnvilCompensation
    {
        private static readonly Dictionary<uint, PlayerSpawner> SpawnedFor = new Dictionary<uint, PlayerSpawner>();
        private static readonly HashSet<string> AnvilFloors = new HashSet<string>();
        private static readonly HashSet<string> EnchantFloors = new HashSet<string>();
        private static readonly HashSet<string> MiracleFloors = new HashSet<string>();
        private static readonly HashSet<string> CharmFloors = new HashSet<string>();
        private static readonly HashSet<string> TabletFloors = new HashSet<string>();
        private static GameObject anvilPrefab;

        internal static void CapturePrefab(NodeBasedRewardSpawner spawner)
        {
            ChoiceRewardObjects.CapturePrefabs(spawner);
            if (spawner != null && spawner.anvil != null && spawner.anvil.GetComponent<NetworkIdentity>() != null)
                anvilPrefab = spawner.anvil;
        }

        internal static void ObserveRewardFloor(NodeBasedRewardSpawner spawner, FloorGenerator floor)
        {
            CapturePrefab(spawner);
            if (!NetworkServer.active || floor == null || string.IsNullOrEmpty(floor.guid)) return;
            EFloorMainEventType type = floor.floorMainEventType;
            if (type != EFloorMainEventType.Anvil)
            {
                HashSet<string> observed = ChoiceFloorSet(type);
                if (observed == null) return;
                observed.Add(floor.guid);
                Plugin.LogInfo($"Vanilla {type} reward floor observed: floor={Short(floor.guid)}, generator={floor.name}.");
                if (PlayerSpawner.MultiplayerList == null) return;
                foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
                    if (player?.PlayerAvatar != null && player.PlayerAvatar.currentFloorGuid == floor.guid)
                    {
                        if (type == EFloorMainEventType.Enchant) CatchUpRewards.RecordPendingEnchant(player, floor.guid);
                        else CatchUpRewards.RecordPendingChoiceFloor(player, floor.guid, type);
                    }
                return;
            }
            AnvilFloors.Add(floor.guid);
            Plugin.LogInfo($"Vanilla Anvil reward floor observed: floor={Short(floor.guid)}, generator={floor.name}.");
            if (PlayerSpawner.MultiplayerList == null) return;
            foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
                if (player?.PlayerAvatar != null && player.PlayerAvatar.currentFloorGuid == floor.guid)
                    CatchUpRewards.RecordPendingAnvil(player, floor.guid);
        }

        internal static void OnFloorChanged(PlayerAvatar player, string floorGuid)
        {
            if (!NetworkServer.active || player?.spawner == null || string.IsNullOrEmpty(floorGuid)) return;
            Plugin.LogInfo($"Floor changed: player={player.Name}, floor={Short(floorGuid)}, pos={player.transform.position}, " +
                           $"dead={player.IsDead}, event={FloorEvent(floorGuid)}.");
            if (CatchUpRewards.IsWeaponFullyEnhanced(player.spawner)) RemoveForMaxedWeapon(player.spawner);
            LockOut(player.spawner);
            CatchUpRewards.ConvertPendingAnvils(player.spawner, floorGuid);
            CatchUpRewards.ConvertPendingEnchants(player.spawner, floorGuid);
            CatchUpRewards.ConvertPendingChoiceFloors(player.spawner, floorGuid);
            FusionCompensation.OnFloorChanged(player.spawner, floorGuid);
            ChoiceRewardObjects.LockOut(player.spawner);
            if (AnvilFloors.Contains(floorGuid) ||
                (DungeonManager.Instance != null && DungeonManager.Instance.generatedFloors.TryGetValue(floorGuid, out FloorData floor) &&
                 floor.mainEventType == EFloorMainEventType.Anvil))
            {
                CatchUpRewards.RecordPendingAnvil(player.spawner, floorGuid);
            }
            if (EnchantFloors.Contains(floorGuid) ||
                (DungeonManager.Instance != null && DungeonManager.Instance.generatedFloors.TryGetValue(floorGuid, out FloorData enchantFloor) &&
                 enchantFloor.mainEventType == EFloorMainEventType.Enchant))
                CatchUpRewards.RecordPendingEnchant(player.spawner, floorGuid);
            foreach (EFloorMainEventType type in new[] { EFloorMainEventType.Miracle, EFloorMainEventType.Charm, EFloorMainEventType.StoneTablet })
                if (ChoiceFloorSet(type).Contains(floorGuid)) CatchUpRewards.RecordPendingChoiceFloor(player.spawner, floorGuid, type);
            if (Plugin.InstanceForPatches != null)
                Plugin.InstanceForPatches.StartCoroutine(ConfirmAnvilFloor(player.spawner, floorGuid));
            MidRunJoin.ScheduleExperienceCatchUp(player.spawner);
            ScheduleSpawn(player.spawner);
            CatchUpRewards.ScheduleRewardObjects(player.spawner);
        }

        private static IEnumerator ConfirmAnvilFloor(PlayerSpawner player, string floorGuid)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            EFloorMainEventType eventType = EFloorMainEventType.Unknown;
            while (NetworkServer.active && player?.PlayerAvatar != null && Time.realtimeSinceStartup < deadline)
            {
                if (DungeonManager.Instance != null &&
                    DungeonManager.Instance.generatedFloors.TryGetValue(floorGuid, out FloorData data))
                    eventType = data.mainEventType;
                if (eventType == EFloorMainEventType.Unknown)
                {
                    FloorGenerator generator = FloorGenerator.FindByGuid(floorGuid);
                    if (generator != null) eventType = generator.floorMainEventType;
                }
                if (eventType != EFloorMainEventType.Unknown) break;
                yield return new WaitForSeconds(0.25f);
            }
            Plugin.LogInfo($"Floor event confirmed: player={player?.PlayerAvatar?.Name}, floor={Short(floorGuid)}, event={eventType}, " +
                           $"stillHere={player?.PlayerAvatar?.currentFloorGuid == floorGuid}.");
            if (eventType == EFloorMainEventType.Anvil)
            {
                AnvilFloors.Add(floorGuid);
                CatchUpRewards.RecordPendingAnvil(player, floorGuid);
                CatchUpRewards.ConvertPendingAnvils(player, player?.PlayerAvatar?.currentFloorGuid);
                ScheduleSpawn(player);
            }
            else if (eventType == EFloorMainEventType.Enchant)
            {
                EnchantFloors.Add(floorGuid);
                CatchUpRewards.RecordPendingEnchant(player, floorGuid);
                CatchUpRewards.ConvertPendingEnchants(player, player?.PlayerAvatar?.currentFloorGuid);
                CatchUpRewards.ScheduleRewardObjects(player);
            }
            else if (eventType == EFloorMainEventType.Miracle || eventType == EFloorMainEventType.Charm ||
                     eventType == EFloorMainEventType.StoneTablet)
            {
                ChoiceFloorSet(eventType).Add(floorGuid);
                CatchUpRewards.RecordPendingChoiceFloor(player, floorGuid, eventType);
                CatchUpRewards.ConvertPendingChoiceFloors(player, player?.PlayerAvatar?.currentFloorGuid);
                CatchUpRewards.ScheduleRewardObjects(player);
            }
        }

        private static void LockOut(PlayerSpawner player)
        {
            if (player == null || string.IsNullOrEmpty(player.playerGuid)) return;
            string hash = HashGuid(player.playerGuid);
            foreach (KeyValuePair<uint, PlayerSpawner> entry in SpawnedFor)
            {
                if (entry.Value == player || !NetworkServer.spawned.TryGetValue(entry.Key, out NetworkIdentity identity)) continue;
                Anvil anvil = identity != null ? identity.GetComponent<Anvil>() : null;
                if (anvil != null && !anvil.enhancedGuidHashes.Contains(hash)) anvil.enhancedGuidHashes.Add(hash);
            }
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

        internal static bool TrySpawn(PlayerSpawner player)
        {
            if (!CatchUpRewards.CanSpawnCompensation(player) ||
                CatchUpRewards.AvailableWeaponCredits(player) <= 0 || SpawnedFor.Values.Any(owner => owner == player))
            {
                return false;
            }

            GameObject prefab = anvilPrefab != null ? anvilPrefab : Resources.FindObjectsOfTypeAll<NodeBasedRewardSpawner>()
                .Select(spawner => spawner != null ? spawner.anvil : null)
                .FirstOrDefault(candidate => candidate != null && candidate.GetComponent<NetworkIdentity>() != null);
            if (prefab == null)
            {
                Plugin.LogInfo("Unable to find the vanilla Anvil prefab for catch-up.");
                return false;
            }

            GameObject instance = UnityEngine.Object.Instantiate(
                prefab, player.PlayerAvatar.transform.position + Vector3.left * 2f, Quaternion.identity);
            Anvil anvil = instance.GetComponent<Anvil>();
            if (anvil == null)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }
            anvil.SetRandomID(StableSeed(player.playerGuid, CatchUpRewards.ClaimedWeaponCredits(player)));
            foreach (PlayerSpawner other in PlayerSpawner.MultiplayerList)
            {
                if (other == null || other == player || string.IsNullOrEmpty(other.playerGuid)) continue;
                string hash = HashGuid(other.playerGuid);
                if (!anvil.enhancedGuidHashes.Contains(hash)) anvil.enhancedGuidHashes.Add(hash);
            }
            NetworkServer.Spawn(instance);
            PersonalizedVisibility.Register(anvil.netIdentity, player.connectionToClient);
            SpawnedFor[anvil.netId] = player;
            CatchUpRewards.LockWeaponCredit(player);
            Plugin.LogInfo($"Catch-up Anvil spawned: player={player.PlayerAvatar.Name}, netId={anvil.netId}, " +
                           $"floor={Short(player.PlayerAvatar.currentFloorGuid)}, pos={instance.transform.position}.");
            return true;
        }

        private static void RemoveForMaxedWeapon(PlayerSpawner player)
        {
            foreach (uint netId in SpawnedFor.Where(entry => entry.Value == player).Select(entry => entry.Key).ToArray())
            {
                SpawnedFor.Remove(netId);
                if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
                {
                    PersonalizedVisibility.Unregister(identity);
                    NetworkServer.Destroy(identity.gameObject);
                }
            }
        }

        internal static void MarkClaimed(Anvil anvil, NetworkConnectionToClient sender)
        {
            PlayerSpawner player = sender?.identity != null ? sender.identity.GetComponent<PlayerSpawner>() : null;
            if (player == null) return;
            CatchUpRewards.MarkCurrentAnvilClaimed(player);
            bool compensation = SpawnedFor.TryGetValue(anvil.netId, out PlayerSpawner owner) && owner == player;
            Plugin.LogInfo($"Anvil completion observed: player={player.PlayerAvatar?.Name}, netId={anvil.netId}, " +
                           $"compensation={compensation}.");
            if (!compensation) return;
            PersonalizedVisibility.Unregister(anvil.netIdentity);
            SpawnedFor.Remove(anvil.netId);
            CatchUpRewards.CompleteWeaponCredit(player);
            NetworkServer.Destroy(anvil.gameObject);
            ScheduleSpawn(player);
        }

        internal static void Release(Anvil anvil)
        {
            if (anvil == null || !SpawnedFor.TryGetValue(anvil.netId, out PlayerSpawner owner)) return;
            PersonalizedVisibility.Unregister(anvil.netIdentity);
            SpawnedFor.Remove(anvil.netId);
            CatchUpRewards.ReleaseWeaponCredit(owner);
            Plugin.LogInfo($"Catch-up Anvil released without claim: player={owner?.PlayerAvatar?.Name}, netId={anvil.netId}.");
            if (owner != null && owner.connectionToClient != null) ScheduleSpawn(owner);
        }

        internal static void RemoveConnection(NetworkConnectionToClient connection)
        {
            PlayerSpawner player = connection?.identity != null ? connection.identity.GetComponent<PlayerSpawner>() : null;
            if (player == null) return;
            foreach (uint netId in SpawnedFor.Where(entry => entry.Value == player).Select(entry => entry.Key).ToArray())
            {
                SpawnedFor.Remove(netId);
                CatchUpRewards.ReleaseWeaponCredit(player);
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
            AnvilFloors.Clear();
            EnchantFloors.Clear();
            MiracleFloors.Clear();
            CharmFloors.Clear();
            TabletFloors.Clear();
            anvilPrefab = null;
        }

        private static string HashGuid(string guid)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(guid)));
        }

        private static int StableSeed(string guid, int claimed)
        {
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToInt32(sha.ComputeHash(Encoding.UTF8.GetBytes((guid ?? "") + ":anvil:" + claimed)), 0);
        }

        private static string Short(string value) => string.IsNullOrEmpty(value)
            ? "-"
            : value.Substring(0, Math.Min(8, value.Length));

        private static string FloorEvent(string guid)
        {
            return DungeonManager.Instance != null && DungeonManager.Instance.generatedFloors.TryGetValue(guid, out FloorData floor)
                ? floor.mainEventType + "/" + floor.threatType
                : "unknown";
        }

        private static HashSet<string> ChoiceFloorSet(EFloorMainEventType type)
        {
            if (type == EFloorMainEventType.Enchant) return EnchantFloors;
            if (type == EFloorMainEventType.Miracle) return MiracleFloors;
            if (type == EFloorMainEventType.Charm) return CharmFloors;
            if (type == EFloorMainEventType.StoneTablet) return TabletFloors;
            return null;
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "HookCurrentFloorValue")]
    internal static class TrackAnvilFloorPatch
    {
        private static void Postfix(PlayerAvatar __instance, string newValue) =>
            AnvilCompensation.OnFloorChanged(__instance, newValue);
    }

    [HarmonyPatch(typeof(NodeBasedRewardSpawner), nameof(NodeBasedRewardSpawner.SpawnReward))]
    internal static class CaptureAnvilPrefabPatch
    {
        private static void Prefix(NodeBasedRewardSpawner __instance, FloorGenerator floorGenerator) =>
            AnvilCompensation.ObserveRewardFloor(__instance, floorGenerator);
    }

    [HarmonyPatch(typeof(Anvil), "UserCode_CmdMarkEnhanced__NetworkConnectionToClient")]
    internal static class CompleteCompensationAnvilPatch
    {
        private static void Postfix(Anvil __instance, NetworkConnectionToClient sender) =>
            AnvilCompensation.MarkClaimed(__instance, sender);
    }

    [HarmonyPatch(typeof(Anvil), "OnDestroy")]
    internal static class ReleaseCompensationAnvilPatch
    {
        private static void Prefix(Anvil __instance) => AnvilCompensation.Release(__instance);
    }
}
