using System;
using System.Linq;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    [HarmonyPatch(typeof(CombatManager), "Update")]
    internal static class AutoRevivePatch
    {
        private const float ClearDelay = 2f;
        private static float clearTimer;

        private static void Postfix()
        {
            if (!NetworkServer.active || !Plugin.reviveWhenClear.Value ||
                PlayerSpawner.MultiplayerList == null || CombatManager.Instance == null || IsGivingUp())
            {
                clearTimer = 0f;
                return;
            }

            PlayerAvatar[] players = PlayerSpawner.MultiplayerList
                .Where(spawner => spawner?.PlayerAvatar != null)
                .Select(spawner => spawner.PlayerAvatar)
                .Where(player => player.isInDungeon > 0 && !string.IsNullOrEmpty(player.currentFloorGuid))
                .ToArray();
            if (!players.Any(player => player.IsDead) || players.Any(player => !player.IsDead && player.IsInBattle) ||
                HasLivingEnemy(players))
            {
                clearTimer = 0f;
                return;
            }

            clearTimer += Time.deltaTime;
            if (clearTimer < ClearDelay) return;
            clearTimer = 0f;
            foreach (PlayerAvatar player in players)
            {
                if (player.IsDead)
                {
                    player.Revive(Mathf.Max(1f, Mathf.Ceil(player.MaxHp * 0.5f)));
                }
            }
        }

        private static bool HasLivingEnemy(PlayerAvatar[] players)
        {
            foreach (UnitAvatar creature in CombatManager.Instance.AllCreatures.ToArray())
            {
                if (creature == null || creature is PlayerAvatar || creature.IsDead ||
                    !creature.gameObject.activeInHierarchy || creature.monsterType == EMonsterType.Dummy)
                {
                    continue;
                }

                long hostileLayers = creature.GetHostileFactionLayers(EDamageFromType.None);
                if (players.Any(player => CombatManager.ContainsAttackableFaction(hostileLayers, player.faction)))
                {
                    return true;
                }
            }
            return false;
        }

        internal static void Clear() => clearTimer = 0f;

        internal static bool PreventClearGameOver()
        {
            if (!NetworkServer.active || !Plugin.reviveWhenClear.Value || PlayerSpawner.MultiplayerList == null)
                return false;
            if (IsGivingUp()) return false;
            PlayerAvatar[] players = PlayerSpawner.MultiplayerList
                .Where(spawner => spawner?.PlayerAvatar != null)
                .Select(spawner => spawner.PlayerAvatar)
                .Where(player => player.isInDungeon > 0 && !string.IsNullOrEmpty(player.currentFloorGuid))
                .ToArray();
            if (players.Length == 0 || players.Any(player => !player.IsDead) || HasLivingEnemy(players)) return false;
            foreach (PlayerAvatar player in players)
                player.Revive(Mathf.Max(1f, Mathf.Ceil(player.MaxHp * 0.5f)));
            clearTimer = 0f;
            return true;
        }

        private static bool IsGivingUp()
        {
            if (DungeonManager.Instance != null && DungeonManager.Instance.isGiveUpRun) return true;
            HorayNetworkManager manager = NetworkManager.singleton as HorayNetworkManager;
            return manager != null && manager.selfLeaveToGameOver;
        }
    }

    [HarmonyPatch(typeof(PlayerSpawner), "HandleDieServerside")]
    internal static class AutoReviveGameOverPatch
    {
        private static bool Prefix() => !AutoRevivePatch.PreventClearGameOver();
    }
}
