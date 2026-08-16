using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class ChoiceRewardObjects
    {
        private static readonly Dictionary<uint, PlayerSpawner> Enchants = new Dictionary<uint, PlayerSpawner>();
        private static readonly Dictionary<uint, PlayerSpawner> Miracles = new Dictionary<uint, PlayerSpawner>();
        private static readonly FieldInfo EnchantRemaining = AccessTools.Field(typeof(AltarOfEnchant), "remainingByGuid");
        private static readonly MethodInfo EnchantTargetRemaining = AccessTools.Method(typeof(AltarOfEnchant), "TargetSetRemaining");
        private static GameObject miraclePrefab;

        internal static void CapturePrefabs(NodeBasedRewardSpawner spawner)
        {
            if (spawner?.miracle != null && spawner.miracle.GetComponent<NetworkIdentity>() != null)
                miraclePrefab = spawner.miracle;
        }

        internal static void SpawnPending(PlayerSpawner player)
        {
            if (!CatchUpRewards.CanSpawnCompensation(player)) return;
            if (CatchUpRewards.AvailableEnchantCredits(player) > 0 && !Enchants.Values.Contains(player))
                SpawnEnchant(player);
            if (CatchUpRewards.AvailableMiracleCredits(player) > 0 && !Miracles.Values.Contains(player))
                SpawnMiracle(player);
        }

        internal static void LockOut(PlayerSpawner player)
        {
            if (!NetworkServer.active || player == null || string.IsNullOrEmpty(player.playerGuid)) return;
            foreach (KeyValuePair<uint, PlayerSpawner> entry in Enchants)
            {
                if (entry.Value == player || !NetworkServer.spawned.TryGetValue(entry.Key, out NetworkIdentity identity)) continue;
                AltarOfEnchant altar = identity != null ? identity.GetComponent<AltarOfEnchant>() : null;
                Dictionary<string, int> remaining = altar != null
                    ? EnchantRemaining.GetValue(altar) as Dictionary<string, int>
                    : null;
                if (remaining != null) remaining[player.playerGuid] = 0;
                if (altar != null && player.connectionToClient != null)
                    EnchantTargetRemaining?.Invoke(altar, new object[] { player.connectionToClient, 0 });
            }
            string hash = HashGuid(player.playerGuid);
            foreach (KeyValuePair<uint, PlayerSpawner> entry in Miracles)
            {
                if (entry.Value == player || !NetworkServer.spawned.TryGetValue(entry.Key, out NetworkIdentity identity)) continue;
                MiracleSelector2 selector = identity != null ? identity.GetComponent<MiracleSelector2>() : null;
                if (selector != null && !selector.acquiredHashes.Contains(hash)) selector.acquiredHashes.Add(hash);
            }
        }

        private static void SpawnEnchant(PlayerSpawner player)
        {
            PropEntity entity = PropDatabase.FindPropById("AltarOfEnchant");
            GameObject prefab = entity != null ? entity.propPrefab : null;
            if (prefab == null) return;
            GameObject instance = UnityEngine.Object.Instantiate(
                prefab, player.PlayerAvatar.transform.position + Vector3.right * 2f, Quaternion.identity);
            AltarOfEnchant altar = instance.GetComponent<AltarOfEnchant>();
            if (altar == null)
            {
                UnityEngine.Object.Destroy(instance);
                return;
            }
            altar.localUseCount = 1;
            LockEnchantForOthers(altar, player);
            NetworkServer.Spawn(instance);
            PersonalizedVisibility.Register(altar.netIdentity, player.connectionToClient);
            Enchants[altar.netId] = player;
            CatchUpRewards.LockEnchantCredit(player);
        }

        private static void SpawnMiracle(PlayerSpawner player)
        {
            GameObject prefab = miraclePrefab != null ? miraclePrefab : Resources.FindObjectsOfTypeAll<NodeBasedRewardSpawner>()
                .Select(spawner => spawner != null ? spawner.miracle : null)
                .FirstOrDefault(candidate => candidate != null && candidate.GetComponent<NetworkIdentity>() != null);
            if (prefab == null) return;
            GameObject instance = UnityEngine.Object.Instantiate(
                prefab, player.PlayerAvatar.transform.position + Vector3.up * 2f, Quaternion.identity);
            MiracleSelector2 selector = instance.GetComponent<MiracleSelector2>();
            if (selector == null)
            {
                UnityEngine.Object.Destroy(instance);
                return;
            }
            selector.SetRandomID(StableSeed(player.playerGuid, CatchUpRewards.ClaimedMiracleCredits(player)));
            foreach (PlayerSpawner other in PlayerSpawner.MultiplayerList)
            {
                if (other == null || other == player || string.IsNullOrEmpty(other.playerGuid)) continue;
                string hash = HashGuid(other.playerGuid);
                if (!selector.acquiredHashes.Contains(hash)) selector.acquiredHashes.Add(hash);
            }
            NetworkServer.Spawn(instance);
            PersonalizedVisibility.Register(selector.netIdentity, player.connectionToClient);
            Miracles[selector.netId] = player;
            CatchUpRewards.LockMiracleCredit(player);
        }

        internal static void CompleteEnchant(AltarOfEnchant altar, NetworkConnectionToClient sender)
        {
            PlayerSpawner player = sender?.identity != null ? sender.identity.GetComponent<PlayerSpawner>() : null;
            if (altar == null || player == null) return;
            CatchUpRewards.MarkCurrentEnchantClaimed(player);
            if (!Enchants.TryGetValue(altar.netId, out PlayerSpawner owner) || owner != player) return;
            PersonalizedVisibility.Unregister(altar.netIdentity);
            Enchants.Remove(altar.netId);
            CatchUpRewards.CompleteEnchantCredit(player);
            NetworkServer.Destroy(altar.gameObject);
            SpawnPending(player);
        }

        internal static bool CanUseEnchant(AltarOfEnchant altar, NetworkConnectionToClient sender)
        {
            PlayerSpawner player = sender?.identity != null ? sender.identity.GetComponent<PlayerSpawner>() : null;
            if (altar == null || player == null || string.IsNullOrEmpty(player.playerGuid)) return false;
            Dictionary<string, int> remaining = EnchantRemaining.GetValue(altar) as Dictionary<string, int>;
            return remaining == null || !remaining.TryGetValue(player.playerGuid, out int count)
                ? altar.localUseCount > 0
                : count > 0;
        }

        internal static void CompleteMiracle(MiracleSelector2 selector, MiracleController controller)
        {
            PlayerSpawner player = controller != null ? controller.GetComponent<PlayerSpawner>() : null;
            if (selector == null || player == null) return;
            if (!Miracles.TryGetValue(selector.netId, out PlayerSpawner owner) || owner != player)
            {
                CatchUpRewards.MarkCurrentChoiceClaimed(player, EFloorMainEventType.Miracle);
                return;
            }
            PersonalizedVisibility.Unregister(selector.netIdentity);
            Miracles.Remove(selector.netId);
            CatchUpRewards.CompleteMiracleCredit(player);
            NetworkServer.Destroy(selector.gameObject);
            SpawnPending(player);
        }

        internal static void ReleaseEnchant(AltarOfEnchant altar)
        {
            if (altar == null || !Enchants.TryGetValue(altar.netId, out PlayerSpawner player)) return;
            PersonalizedVisibility.Unregister(altar.netIdentity);
            Enchants.Remove(altar.netId);
            CatchUpRewards.ReleaseEnchantCredit(player);
            if (player != null && player.connectionToClient != null) CatchUpRewards.ScheduleRewardObjects(player);
        }

        internal static void ReleaseMiracle(MiracleSelector2 selector)
        {
            if (selector == null || !Miracles.TryGetValue(selector.netId, out PlayerSpawner player)) return;
            PersonalizedVisibility.Unregister(selector.netIdentity);
            Miracles.Remove(selector.netId);
            CatchUpRewards.ReleaseMiracleCredit(player);
            if (player != null && player.connectionToClient != null) CatchUpRewards.ScheduleRewardObjects(player);
        }

        internal static void RemoveConnection(NetworkConnectionToClient connection)
        {
            PlayerSpawner player = connection?.identity != null ? connection.identity.GetComponent<PlayerSpawner>() : null;
            if (player == null) return;
            DestroyOwned(Enchants, player, CatchUpRewards.ReleaseEnchantCredit);
            DestroyOwned(Miracles, player, CatchUpRewards.ReleaseMiracleCredit);
        }

        internal static void Clear()
        {
            Enchants.Clear();
            Miracles.Clear();
            miraclePrefab = null;
        }

        private static void LockEnchantForOthers(AltarOfEnchant altar, PlayerSpawner owner)
        {
            Dictionary<string, int> remaining = EnchantRemaining.GetValue(altar) as Dictionary<string, int>;
            if (remaining == null) return;
            foreach (PlayerSpawner other in PlayerSpawner.MultiplayerList)
                if (other != null && other != owner && !string.IsNullOrEmpty(other.playerGuid)) remaining[other.playerGuid] = 0;
        }

        private static void DestroyOwned(Dictionary<uint, PlayerSpawner> map, PlayerSpawner player, Action<PlayerSpawner> release)
        {
            foreach (uint netId in map.Where(entry => entry.Value == player).Select(entry => entry.Key).ToArray())
            {
                map.Remove(netId);
                release(player);
                if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
                {
                    PersonalizedVisibility.Unregister(identity);
                    NetworkServer.Destroy(identity.gameObject);
                }
            }
        }

        private static string HashGuid(string guid)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(guid)));
        }

        private static int StableSeed(string guid, int claimed)
        {
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToInt32(sha.ComputeHash(Encoding.UTF8.GetBytes((guid ?? "") + ":miracle:" + claimed)), 0);
        }
    }

    [HarmonyPatch(typeof(AltarOfEnchant), "UserCode_CmdUse__NetworkConnectionToClient")]
    internal static class CompleteCatchUpEnchantPatch
    {
        private static void Prefix(AltarOfEnchant __instance, NetworkConnectionToClient sender, out bool __state) =>
            __state = ChoiceRewardObjects.CanUseEnchant(__instance, sender);

        private static void Postfix(AltarOfEnchant __instance, NetworkConnectionToClient sender, bool __state)
        {
            if (__state) ChoiceRewardObjects.CompleteEnchant(__instance, sender);
        }
    }

    [HarmonyPatch(typeof(MiracleSelector2), "UserCode_CmdHandleMiracleAcquired__MiracleController")]
    internal static class CompleteCatchUpMiraclePatch
    {
        private static void Prefix(MiracleSelector2 __instance, out int __state) =>
            __state = __instance != null ? __instance.acquiredHashes.Count : 0;

        private static void Postfix(MiracleSelector2 __instance, MiracleController miracleController, int __state)
        {
            if (__instance != null && __instance.acquiredHashes.Count > __state)
                ChoiceRewardObjects.CompleteMiracle(__instance, miracleController);
        }
    }

    [HarmonyPatch(typeof(AltarOfEnchant), "OnDestroy")]
    internal static class ReleaseCatchUpEnchantPatch
    {
        private static void Prefix(AltarOfEnchant __instance) => ChoiceRewardObjects.ReleaseEnchant(__instance);
    }

    [HarmonyPatch(typeof(MiracleSelector2), "OnDestroy")]
    internal static class ReleaseCatchUpMiraclePatch
    {
        private static void Prefix(MiracleSelector2 __instance) => ChoiceRewardObjects.ReleaseMiracle(__instance);
    }
}
