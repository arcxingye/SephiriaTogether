using System;
using HarmonyLib;
using Mirror;
using Mirror.RemoteCalls;

namespace SephiriaTogether
{
    internal static class AntiCheat
    {
        [ThreadStatic]
        private static NetworkConnectionToClient currentSender;

        internal static NetworkConnectionToClient CurrentSender => currentSender;

        internal static bool Enabled => NetworkServer.active &&
                                        Plugin.antiCheat != null &&
                                        Plugin.antiCheat.Value;

        internal static void BeginCommand(NetworkConnectionToClient sender)
        {
            currentSender = sender;
        }

        internal static void EndCommand(NetworkConnectionToClient previous = null)
        {
            currentSender = previous;
        }

        internal static void Clear()
        {
            currentSender = null;
        }

        internal static bool RejectRemote(NetworkBehaviour behaviour, string command)
        {
            if (!Enabled || currentSender == null || currentSender == NetworkServer.localConnection)
                return true;

            PlayerSpawner player = currentSender.identity != null
                ? currentSender.identity.GetComponent<PlayerSpawner>()
                : null;
            Plugin.LogInfo("Anti-cheat blocked direct remote mutation: command=" + command +
                           ", player=" + (player?.PlayerAvatar?.Name ?? behaviour?.name ?? "unknown") +
                           ", conn=" + currentSender.connectionId + ".");
            return false;
        }

        internal static bool AllowDirectItemAdd(GridInventory inventory, int entityId)
        {
            if (!Enabled || currentSender == null || currentSender == NetworkServer.localConnection)
                return true;

            PlayerAvatar player = SenderPlayer();
            ItemEntity item = ItemDatabase.FindItemById(entityId);
            if (player == null || inventory == null || inventory.UnitAvatar != player || item == null)
                return RejectRemote(inventory, "GridInventory.CmdAddItem entity=" + entityId);

            // Repeat purchases in the Pocket Dimension are the one native path
            // that sends a direct item-add command from the client.
            if (item.activeType != EItemActiveType.PocketDimensionShop ||
                DungeonManager.Instance == null || string.IsNullOrEmpty(player.currentFloorGuid) ||
                !DungeonManager.Instance.generatedFloors.TryGetValue(player.currentFloorGuid, out FloorData floor) ||
                floor == null || !string.Equals(floor.name, "PocketDimensionShop", StringComparison.Ordinal))
                return RejectRemote(inventory, "GridInventory.CmdAddItem entity=" + entityId);

            foreach (PocketDimensionShop shop in UnityEngine.Resources.FindObjectsOfTypeAll<PocketDimensionShop>())
            {
                if (shop == null || !shop.gameObject.scene.IsValid() || shop.arms == null)
                    continue;
                foreach (PocketDimensionShopArm arm in shop.arms)
                {
                    if (arm != null && arm.item == item && arm.interactable != null && arm.interactable.enabled &&
                        (arm.transform.position - player.transform.position).sqrMagnitude <= 25f)
                        return true;
                }
            }

            return RejectRemote(inventory, "GridInventory direct item add unavailable entity=" + entityId);
        }

        private static PlayerAvatar SenderPlayer()
        {
            if (currentSender?.identity == null) return null;
            return currentSender.identity.GetComponent<PlayerAvatar>() ??
                   currentSender.identity.GetComponent<PlayerSpawner>()?.PlayerAvatar;
        }
    }

    [HarmonyPatch(typeof(RemoteProcedureCalls), "Invoke")]
    internal static class AntiCheatCommandContextPatch
    {
        private static void Prefix(RemoteCallType remoteCallType, NetworkConnectionToClient senderConnection,
            out NetworkConnectionToClient __state)
        {
            __state = AntiCheat.CurrentSender;
            if (remoteCallType == RemoteCallType.Command)
                AntiCheat.BeginCommand(senderConnection);
        }

        private static void Postfix(RemoteCallType remoteCallType, NetworkConnectionToClient __state)
        {
            if (remoteCallType == RemoteCallType.Command)
                AntiCheat.EndCommand(__state);
        }

        private static Exception Finalizer(Exception __exception, RemoteCallType remoteCallType,
            NetworkConnectionToClient __state)
        {
            if (remoteCallType == RemoteCallType.Command)
                AntiCheat.EndCommand(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopServer))]
    internal static class AntiCheatCleanupPatch
    {
        private static void Postfix() => AntiCheat.Clear();
    }

    [HarmonyPatch(typeof(UnitAvatar), "UserCode_CmdSetMoney__Int32")]
    internal static class AntiCheatSetMoneyPatch
    {
        private static bool Prefix(UnitAvatar __instance, int __0) =>
            AntiCheat.RejectRemote(__instance, "UnitAvatar.CmdSetMoney amount=" + __0);
    }

    [HarmonyPatch(typeof(UnitAvatar), "UserCode_CmdAddMoney__Int32")]
    internal static class AntiCheatAddMoneyPatch
    {
        private static bool Prefix(UnitAvatar __instance, int __0) =>
            AntiCheat.RejectRemote(__instance, "UnitAvatar.CmdAddMoney amount=" + __0);
    }

    [HarmonyPatch(typeof(UnitAvatar), "UserCode_CmdGiveMoney__UnitAvatar__Int32")]
    internal static class AntiCheatGiveMoneyPatch
    {
        private static bool Prefix(UnitAvatar __instance, UnitAvatar __0, int __1) =>
            AntiCheat.RejectRemote(__instance, "UnitAvatar.CmdGiveMoney amount=" + __1);
    }

    [HarmonyPatch(typeof(GridInventory), "UserCode_CmdAddItem__Int32__Int32__SByte__Int32__Boolean__Boolean")]
    internal static class AntiCheatDirectItemPatch
    {
        private static bool Prefix(GridInventory __instance, int __1) =>
            AntiCheat.AllowDirectItemAdd(__instance, __1);
    }

    [HarmonyPatch(typeof(GridInventory), "UserCode_CmdAddItemAtPosition__Int32__Int32__SByte__ItemPosition__Int32__Boolean__Boolean")]
    internal static class AntiCheatPositionedItemPatch
    {
        private static bool Prefix(GridInventory __instance, int __1) =>
            AntiCheat.AllowDirectItemAdd(__instance, __1);
    }

    [HarmonyPatch(typeof(GridInventory), "UserCode_CmdAddItems__ItemMetadata[]__Boolean__Boolean")]
    internal static class AntiCheatDirectItemsPatch
    {
        private static bool Prefix(GridInventory __instance) =>
            AntiCheat.RejectRemote(__instance, "GridInventory.CmdAddItems");
    }

    [HarmonyPatch(typeof(GridInventory), "UserCode_CmdAddItems__ItemMetadataWithPosition[]__Boolean__Boolean")]
    internal static class AntiCheatPositionedItemsPatch
    {
        private static bool Prefix(GridInventory __instance) =>
            AntiCheat.RejectRemote(__instance, "GridInventory.CmdAddItemsWithPosition");
    }
}
