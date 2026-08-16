namespace SephiriaTogether
{
    internal static class FloorDisplay
    {
        internal static string Format(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "-";

            FloorData data = null;
            if (DungeonManager.Instance != null)
                DungeonManager.Instance.generatedFloors.TryGetValue(guid, out data);

            FloorGenerator generator = FloorGenerator.FindByGuid(guid);
            EFloorThreatType threat = data != null ? data.threatType
                : generator != null ? generator.floorThreatType
                : EFloorThreatType.Unknown;
            EFloorMainEventType mainEvent = data != null ? data.mainEventType
                : generator != null ? generator.floorMainEventType
                : EFloorMainEventType.Unknown;

            string room = ThreatName(threat);
            if (string.IsNullOrEmpty(room)) room = EventName(mainEvent);
            if (data == null)
                return string.IsNullOrEmpty(room) ? MenuText.Get("CurrentRoom") : room;

            string progress = string.Format(MenuText.Get("RouteFloor"), data.nodeProgress + 1);
            return string.IsNullOrEmpty(room) ? progress : progress + " · " + room;
        }

        private static string ThreatName(EFloorThreatType threat)
        {
            switch (threat)
            {
                case EFloorThreatType.Boss: return MenuText.Get("RoomBoss");
                case EFloorThreatType.MiniBoss: return MenuText.Get("RoomMiniBoss");
                case EFloorThreatType.HardBattle: return MenuText.Get("RoomHardBattle");
                case EFloorThreatType.Battle:
                case EFloorThreatType.BattleFloor:
                case EFloorThreatType.UnknownBattle: return MenuText.Get("RoomBattle");
                default: return "";
            }
        }

        private static string EventName(EFloorMainEventType mainEvent)
        {
            switch (mainEvent)
            {
                case EFloorMainEventType.Money: return MenuText.Get("RoomMoney");
                case EFloorMainEventType.EXP: return MenuText.Get("RoomExp");
                case EFloorMainEventType.HP: return MenuText.Get("RoomHeal");
                case EFloorMainEventType.Merchant: return MenuText.Get("RoomMerchant");
                case EFloorMainEventType.Miracle: return MenuText.Get("RoomMiracle");
                case EFloorMainEventType.Charm: return MenuText.Get("RoomCharm");
                case EFloorMainEventType.StoneTablet: return MenuText.Get("RoomTablet");
                case EFloorMainEventType.Enchant: return MenuText.Get("RoomEnchant");
                case EFloorMainEventType.RandomEncounter: return MenuText.Get("RoomEncounter");
                case EFloorMainEventType.Anvil: return MenuText.Get("RoomAnvil");
                case EFloorMainEventType.Dice: return MenuText.Get("RoomDice");
                case EFloorMainEventType.Sapphire: return MenuText.Get("RoomSapphire");
                case EFloorMainEventType.MaxHP: return MenuText.Get("RoomMaxHp");
                case EFloorMainEventType.InventoryStorage: return MenuText.Get("RoomInventory");
                default: return "";
            }
        }
    }
}
