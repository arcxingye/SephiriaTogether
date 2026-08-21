using System.Collections;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class ManualInventoryArrangement
    {
        private static string status;
        private static bool pendingRefresh;

        internal static string Status => status;

        internal static bool CanArrange
        {
            get
            {
                PlayerAvatar player = LocalPlayer();
                GridInventory inventory = player?.Inventory;
                return player != null && player.isOwned && !player.IsDead && !player.IsInBattle &&
                       inventory != null && player.localDataStorage != null &&
                       !player.localDataStorage.preparingUIThings &&
                       (UIManager.Instance == null || UIManager.Instance.CurrentControlStack == null) &&
                       !SaveManagement.IsBusy && SaveManager.IsSaving == SaveManager.ESaveState.None &&
                       inventory.CurrentInventoryStorage > 1 && inventory.charms.Count > 0;
            }
        }

        internal static void ArrangeNow()
        {
            PlayerAvatar player = LocalPlayer();
            GridInventory inventory = player?.Inventory;
            if (!CanArrange || inventory == null)
            {
                status = MenuText.Get("ManualArrangeUnavailable");
                return;
            }

            if (inventory.isServer)
            {
                bool changed = inventory.AutoArrangeInventoryForBestCharmLevels(1, allowTabletRotation: true);
                status = changed ? MenuText.Get("ManualArrangeComplete") : MenuText.Get("ManualArrangeNoChange");
                RefreshLocalInventoryUi(inventory);
            }
            else
            {
                pendingRefresh = true;
                inventory.OnCharmEffectRefreshedForClient -= OnClientArrangeSignal;
                inventory.OnCharmEffectRefreshedForClient += OnClientArrangeSignal;
                inventory.RequestAutoArrangeInventoryForBestCharmLevels(1, allowTabletRotation: true);
                status = MenuText.Get("ManualArrangeStarted");
                if (Plugin.InstanceForPatches != null)
                    Plugin.InstanceForPatches.StartCoroutine(ClearPendingArrangeAfterDelay(inventory));
            }
            Plugin.LogInfo($"Manual inventory optimization requested: player={player.Name}, " +
                           $"items={inventory.inventoryMatrix.Count}, storage={inventory.CurrentInventoryStorage}.");
        }

        internal static void ClearStatus() => status = null;

        internal static bool CanArrangeOnServer(GridInventory inventory)
        {
            PlayerAvatar player = inventory?.UnitAvatar as PlayerAvatar;
            PlayerLocalDataStorage storage = player?.localDataStorage;
            return NetworkServer.active && inventory != null && inventory.isServer && player != null &&
                   !player.IsDead && !player.IsInBattle && storage != null &&
                   !storage.doingSomeUIThings && !storage.preparingUIThings && !SaveManagement.IsBusy &&
                   SaveManager.IsSaving == SaveManager.ESaveState.None &&
                   inventory.CurrentInventoryStorage > 1 && inventory.charms.Count > 0;
        }

        private static void OnClientArrangeSignal()
        {
            if (!pendingRefresh) return;
            pendingRefresh = false;
            GridInventory inventory = LocalPlayer()?.Inventory;
            if (inventory != null)
                inventory.OnCharmEffectRefreshedForClient -= OnClientArrangeSignal;
            if (Plugin.InstanceForPatches != null)
                Plugin.InstanceForPatches.StartCoroutine(RefreshLocalInventoryUiNextFrame(inventory));
        }

        private static IEnumerator ClearPendingArrangeAfterDelay(GridInventory inventory)
        {
            yield return new WaitForSeconds(3f);
            if (!pendingRefresh) yield break;
            pendingRefresh = false;
            if (inventory != null)
                inventory.OnCharmEffectRefreshedForClient -= OnClientArrangeSignal;
            RefreshLocalInventoryUi(inventory);
        }

        private static IEnumerator RefreshLocalInventoryUiNextFrame(GridInventory inventory)
        {
            yield return null;
            RefreshLocalInventoryUi(inventory);
        }

        private static void RefreshLocalInventoryUi(GridInventory inventory)
        {
            if (inventory == null || UIManager.Instance == null) return;
            UI_InventoryViewer viewer = UIManager.Instance.GetElement<UI_InventoryViewer>();
            if (viewer == null) return;
            foreach (UI_NewInventoryIcon icon in viewer.icons)
                if (icon != null && icon.Inventory != null) icon.UpdateIcon();
        }

        private static PlayerAvatar LocalPlayer() =>
            CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayer : null;
    }

    [HarmonyPatch(typeof(GridInventory), "UserCode_CmdAutoArrangeInventoryForBestCharmLevels__Int32__Boolean")]
    internal static class ManualInventoryArrangementServerGuardPatch
    {
        private static bool Prefix(GridInventory __instance, int maxIterations, bool allowTabletRotation) =>
            ManualInventoryArrangement.CanArrangeOnServer(__instance) &&
            maxIterations == 1 && allowTabletRotation;
    }
}
