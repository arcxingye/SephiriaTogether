using System;
using HarmonyLib;
using Mirror;

namespace SephiriaTogether
{
    internal static class RejoinInventory
    {
        private const int MaximumStorage = GridInventory.MaxWidth * GridInventory.MaxHeight;

        internal static string StorageKey(PlayerSpawner spawner)
        {
            return $"Player{spawner.currentPlayerIdxForSave}InventoryStorage";
        }
    }

    [HarmonyPatch(typeof(PlayerSpawner), nameof(PlayerSpawner.SaveCurrentSessionData))]
    internal static class SaveInventoryCapacityPatch
    {
        private static void Postfix(PlayerSpawner __instance)
        {
            if (!NetworkServer.active || SaveManager.CurrentRun == null || __instance.PlayerAvatar == null ||
                __instance.PlayerAvatar.Inventory == null || SaveManager.CurrentRun.GetInt("SaveVersion", 0) == 0)
            {
                return;
            }

            SaveManager.CurrentRun.SetInt(
                RejoinInventory.StorageKey(__instance),
                __instance.PlayerAvatar.Inventory.CurrentInventoryStorage);
        }
    }

}
