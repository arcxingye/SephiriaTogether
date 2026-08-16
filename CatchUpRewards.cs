using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal struct CatchUpHelloMessage : NetworkMessage { }

    internal struct CatchUpOfferMessage : NetworkMessage
    {
        public int weaponCredits;
        public int enchantCredits;
        public int miracleCredits;
        public int tabletCredits;
        public int bossCredits;
        public int charmCredits;
        public int weaponClaimed;
        public int enchantClaimed;
        public int miracleClaimed;
        public int tabletClaimed;
        public int bossClaimed;
        public int charmClaimed;
        public string miracleOptions;
        public string rules;
        public string diagnostics;
        public string history;
        public string bossOptions;
        public byte lastResult;
    }

    internal struct CatchUpClaimMessage : NetworkMessage
    {
        public byte rewardType;
        public int choiceId;
        public string choiceKey;
        public sbyte x;
        public sbyte y;
    }

    internal static class CatchUpRewards
    {
        private sealed class Credits
        {
            internal int Weapons;
            internal int Enchants;
            internal int Miracles;
            internal int Tablets;
            internal int Bosses;
            internal int Charms;
            internal int WeaponClaimed;
            internal int EnchantClaimed;
            internal int MiracleClaimed;
            internal int TabletClaimed;
            internal int BossClaimed;
            internal int CharmClaimed;
            internal bool ClientMod;
            internal string SavePrefix;
            internal int PendingTablets;
            internal int PendingBosses;
            internal int PendingCharms;
            internal readonly HashSet<string> CountedFloors = new HashSet<string>();
            internal readonly List<string> History = new List<string>();
            internal readonly List<string> CapturedMiracles = new List<string>();
            internal readonly List<string> CapturedBossRewards = new List<string>();
        }

        private sealed class PendingSephirite
        {
            internal Credits Credits;
            internal NetworkConnectionToClient Connection;
            internal byte RewardType;
        }

        private static readonly Dictionary<int, Credits> ServerCredits = new Dictionary<int, Credits>();
        private static readonly Dictionary<uint, PendingSephirite> PendingSephirites = new Dictionary<uint, PendingSephirite>();
        private static bool clientHelloSent;
        private static bool clientClaimPending;
        internal static int ClientWeaponCredits { get; private set; }
        internal static int ClientEnchantCredits { get; private set; }
        internal static int ClientMiracleCredits { get; private set; }
        internal static int ClientTabletCredits { get; private set; }
        internal static int ClientBossCredits { get; private set; }
        internal static int ClientCharmCredits { get; private set; }
        internal static int ClientWeaponClaimed { get; private set; }
        internal static int ClientEnchantClaimed { get; private set; }
        internal static int ClientMiracleClaimed { get; private set; }
        internal static int ClientTabletClaimed { get; private set; }
        internal static int ClientBossClaimed { get; private set; }
        internal static int ClientCharmClaimed { get; private set; }
        internal static string ClientMiracleOptions { get; private set; } = "";
        internal static byte ClientLastResult { get; private set; }
        internal static bool ClientClaimPending => clientClaimPending;
        internal static string ClientRules { get; private set; } = "";
        internal static string ClientDiagnostics { get; private set; } = "";
        internal static string ClientHistory { get; private set; } = "";
        internal const string ReleasePageUrl = "https://github.com/arcxingye/SephiriaTogether/releases/latest";
        internal const string PluginZipUrl = "https://github.com/arcxingye/SephiriaTogether/releases/latest/download/SephiriaTogether.zip";

        internal static void RegisterServerMessages()
        {
            ConfigureSerialization();
            NetworkServer.RegisterHandler<CatchUpHelloMessage>(OnServerHello, true);
            NetworkServer.RegisterHandler<CatchUpClaimMessage>(OnServerClaim, true);
            RescueAlerts.RegisterServerMessages();
        }

        internal static void RegisterClientMessages()
        {
            ConfigureSerialization();
            NetworkClient.RegisterHandler<CatchUpOfferMessage>(OnClientOffer, true);
            RescueAlerts.RegisterClientMessages();
        }

        private static void ConfigureSerialization()
        {
            Writer<CatchUpHelloMessage>.write = (writer, value) => { };
            Reader<CatchUpHelloMessage>.read = reader => new CatchUpHelloMessage();
            Writer<CatchUpOfferMessage>.write = (writer, value) =>
            {
                writer.WriteVarInt(value.weaponCredits);
                writer.WriteVarInt(value.enchantCredits);
                writer.WriteVarInt(value.miracleCredits);
                writer.WriteVarInt(value.tabletCredits);
                writer.WriteVarInt(value.bossCredits);
                writer.WriteVarInt(value.charmCredits);
                writer.WriteVarInt(value.weaponClaimed);
                writer.WriteVarInt(value.enchantClaimed);
                writer.WriteVarInt(value.miracleClaimed);
                writer.WriteVarInt(value.tabletClaimed);
                writer.WriteVarInt(value.bossClaimed);
                writer.WriteVarInt(value.charmClaimed);
                writer.WriteString(value.miracleOptions);
                writer.WriteString(value.rules);
                writer.WriteString(value.diagnostics);
                writer.WriteString(value.history);
                writer.WriteString(value.bossOptions);
                writer.WriteByte(value.lastResult);
            };
            Reader<CatchUpOfferMessage>.read = reader => new CatchUpOfferMessage
            {
                weaponCredits = reader.ReadVarInt(),
                enchantCredits = reader.ReadVarInt(),
                miracleCredits = reader.ReadVarInt(),
                tabletCredits = reader.ReadVarInt(),
                bossCredits = reader.ReadVarInt(),
                charmCredits = reader.ReadVarInt(),
                weaponClaimed = reader.ReadVarInt(),
                enchantClaimed = reader.ReadVarInt(),
                miracleClaimed = reader.ReadVarInt(),
                tabletClaimed = reader.ReadVarInt(),
                bossClaimed = reader.ReadVarInt(),
                charmClaimed = reader.ReadVarInt(),
                miracleOptions = reader.ReadString(),
                rules = reader.ReadString(),
                diagnostics = reader.ReadString(),
                history = reader.ReadString(),
                bossOptions = reader.ReadString(),
                lastResult = reader.ReadByte()
            };
            Writer<CatchUpClaimMessage>.write = (writer, value) =>
            {
                writer.WriteByte(value.rewardType);
                writer.WriteVarInt(value.choiceId);
                writer.WriteString(value.choiceKey);
                writer.WriteSByte(value.x);
                writer.WriteSByte(value.y);
            };
            Reader<CatchUpClaimMessage>.read = reader => new CatchUpClaimMessage
            {
                rewardType = reader.ReadByte(),
                choiceId = reader.ReadVarInt(),
                choiceKey = reader.ReadString(),
                x = reader.ReadSByte(),
                y = reader.ReadSByte()
            };
        }

        internal static void Prepare(PlayerSpawner newcomer)
        {
            if (!NetworkServer.active || newcomer?.connectionToClient == null ||
                newcomer.PlayerAvatar == null || DungeonManager.Instance == null)
            {
                return;
            }
            bool clientMod = ServerCredits.TryGetValue(newcomer.connectionToClient.connectionId, out Credits existing) &&
                             existing.ClientMod;
            Credits credits = Load(newcomer.playerGuid);
            credits.ClientMod = clientMod;
            ServerCredits[newcomer.connectionToClient.connectionId] = credits;

            PlayerSpawner canonical = PlayerSpawner.MultiplayerList?
                .Where(player => player != null && player != newcomer && player.PlayerAvatar != null)
                .OrderByDescending(player => player.PlayerAvatar.floorTravelHistory.Count)
                .ThenByDescending(player => player.isHost)
                .FirstOrDefault();
            if (canonical == null)
            {
                if (credits.ClientMod) SendOffer(newcomer.connectionToClient, credits, 0);
                return;
            }

            HashSet<string> localHistory = new HashSet<string>(newcomer.PlayerAvatar.floorTravelHistory);
            HashSet<string> counted = new HashSet<string>();
            bool changed = false;
            foreach (string guid in canonical.PlayerAvatar.floorTravelHistory)
            {
                if (string.IsNullOrEmpty(guid) || localHistory.Contains(guid) || !counted.Add(guid) ||
                    !DungeonManager.Instance.generatedFloors.TryGetValue(guid, out FloorData floor))
                {
                    continue;
                }

                string eventKey = guid + ":" + floor.mainEventType;
                if (!credits.CountedFloors.Contains(eventKey) && !credits.CountedFloors.Contains(guid))
                {
                    switch (floor.mainEventType)
                    {
                        case EFloorMainEventType.Anvil:
                            credits.Weapons++;
                            break;
                        case EFloorMainEventType.Enchant:
                            credits.Enchants++;
                            break;
                        case EFloorMainEventType.Miracle:
                            credits.Miracles++;
                            break;
                        case EFloorMainEventType.Charm:
                            credits.Charms++;
                            break;
                        case EFloorMainEventType.StoneTablet:
                            credits.Tablets++;
                            break;
                        case EFloorMainEventType.InventoryStorage:
                            if (newcomer.PlayerAvatar.Inventory != null &&
                                newcomer.PlayerAvatar.Inventory.CurrentInventoryStorage < GridInventory.MaxWidth * GridInventory.MaxHeight)
                            {
                                newcomer.PlayerAvatar.Inventory.AddStorage(1);
                            }
                            break;
                        case EFloorMainEventType.MaxHP:
                            StatusInstance status = StatusDatabase.CreateStatusEntity("MAX_HP_NORATIO", 20);
                            if (status != null)
                            {
                                newcomer.PlayerAvatar.AddOrphanedStatusInstance(status);
                                newcomer.PlayerAvatar.Heal(20f, false, true);
                            }
                            break;
                        case EFloorMainEventType.HP:
                            newcomer.PlayerAvatar.HealPercent(60f);
                            break;
                        case EFloorMainEventType.Sapphire:
                            newcomer.NetworksapphireInRun = newcomer.sapphireInRun == int.MaxValue
                                ? int.MaxValue
                                : newcomer.sapphireInRun + 1;
                            break;
                    }
                    credits.CountedFloors.Add(eventKey);
                    AddHistory(credits, "Catch-up: " + floor.mainEventType + " @ " + ShortGuid(guid));
                    changed = true;
                }

                string bossKey = guid + ":Boss";
                if (floor.threatType == EFloorThreatType.Boss && !credits.CountedFloors.Contains(bossKey))
                {
                    credits.Bosses += 2;
                    credits.CountedFloors.Add(bossKey);
                    AddHistory(credits, "Catch-up: Boss choices +2 @ " + ShortGuid(guid));
                    changed = true;
                }
            }
            if (changed)
            {
                newcomer.SaveCurrentSessionData();
                Save(credits);
            }
            if (credits.ClientMod) SendOffer(newcomer.connectionToClient, credits, 0);
        }

        internal static void SendHello()
        {
            if (!clientHelloSent && NetworkClient.active && NetworkClient.ready && HostSupportsProtocol())
            {
                clientHelloSent = true;
                NetworkClient.Send(new CatchUpHelloMessage());
            }
        }

        internal static void ClaimWeapon(int weaponId)
        {
            if (ClientWeaponCredits > 0 && !clientClaimPending)
            {
                clientClaimPending = true;
                NetworkClient.Send(new CatchUpClaimMessage { rewardType = 1, choiceId = weaponId });
            }
        }

        internal static void ClaimEnchant(ItemPosition position)
        {
            if (ClientEnchantCredits > 0 && !clientClaimPending)
            {
                clientClaimPending = true;
                NetworkClient.Send(new CatchUpClaimMessage { rewardType = 2, x = position.x, y = position.y });
            }
        }

        internal static void ClaimMiracle(string miracleId) => SendClaim(3, miracleId);
        internal static void ClaimTablet() => SendClaim(4, "SEPHIRITE_TABLET");
        internal static void ClaimBoss(string reward) => SendClaim(5, reward);
        internal static void ClaimCharm() => SendClaim(6, "SEPHIRITE_CHARM");

        private static void SendClaim(byte rewardType, string choice)
        {
            if (!clientClaimPending && NetworkClient.active)
            {
                clientClaimPending = true;
                NetworkClient.Send(new CatchUpClaimMessage { rewardType = rewardType, choiceKey = choice });
            }
        }

        internal static void RemoveConnection(NetworkConnectionToClient connection)
        {
            if (connection != null) ServerCredits.Remove(connection.connectionId);
        }

        internal static void ClearClientState()
        {
            RescueAlerts.ClearClient();
            clientHelloSent = false;
            clientClaimPending = false;
            ClientWeaponCredits = 0;
            ClientEnchantCredits = 0;
            ClientMiracleCredits = 0;
            ClientTabletCredits = 0;
            ClientBossCredits = 0;
            ClientCharmCredits = 0;
            ClientWeaponClaimed = 0;
            ClientEnchantClaimed = 0;
            ClientMiracleClaimed = 0;
            ClientTabletClaimed = 0;
            ClientBossClaimed = 0;
            ClientCharmClaimed = 0;
            ClientMiracleOptions = "";
            ClientLastResult = 0;
            ClientRules = "";
            ClientDiagnostics = "";
            ClientHistory = "";
        }

        internal static void ClearServerState()
        {
            RescueAlerts.ClearServer();
            ServerCredits.Clear();
            PendingSephirites.Clear();
        }

        private static void OnServerHello(NetworkConnectionToClient connection, CatchUpHelloMessage message)
        {
            if (!ServerCredits.TryGetValue(connection.connectionId, out Credits credits))
            {
                PlayerSpawner spawner = connection.identity != null
                    ? connection.identity.GetComponent<PlayerSpawner>()
                    : null;
                ServerCredits[connection.connectionId] = credits = Load(spawner?.playerGuid);
            }
            credits.ClientMod = true;
            SendOffer(connection, credits, 0);
        }

        private static void OnServerClaim(NetworkConnectionToClient connection, CatchUpClaimMessage message)
        {
            if (!ServerCredits.TryGetValue(connection.connectionId, out Credits credits) ||
                !credits.ClientMod || connection.identity == null) return;
            PlayerAvatar player = connection.identity.GetComponent<PlayerAvatar>();
            if (player == null) return;
            bool claimed = false;

            if (message.rewardType == 1 && credits.Weapons > 0)
            {
                WeaponControllerSimple controller = player.GetComponent<WeaponControllerSimple>();
                WeaponSimple weapon = controller != null ? controller.currentWeapon : null;
                List<EnhancementMetadata> valid = weapon != null ? WeaponDatabase.GetWeaponEnhancements(weapon.entityId) : null;
                if (valid != null && valid.Any(choice => choice != null && choice.enabled &&
                    choice.enhanced != null && choice.enhanced.id == message.choiceId))
                {
                    controller.EquipWeapon(false, message.choiceId);
                    credits.Weapons--;
                    credits.WeaponClaimed++;
                    AddHistory(credits, "Claimed weapon upgrade: " + message.choiceId);
                    claimed = true;
                }
            }
            else if (message.rewardType == 2 && credits.Enchants > 0 && player.Inventory != null)
            {
                ItemPosition position = new ItemPosition(message.x, message.y);
                NewItemOwnInstance item = player.Inventory.FindItem(position);
                if (item != null && item.Entity != null && item.Entity.type == EItemType.Charm &&
                    item.Charm != null && item.Charm.maxLevel > 0)
                {
                    int.TryParse(DungeonManager.Instance.GetGlobalItemStatValue(item.InstanceID, "Enchant"), out int level);
                    if (level < item.Charm.maxLevel)
                    {
                        player.Inventory.Enchant(position);
                        credits.Enchants--;
                        credits.EnchantClaimed++;
                        AddHistory(credits, "Claimed enchant");
                        claimed = true;
                    }
                }
            }
            else if (message.rewardType == 3 && credits.Miracles > 0)
            {
                Miracle miracle = MiracleDatabase.FindMiracle(message.choiceKey ?? "");
                MiracleController controller = player.GetComponent<MiracleController>();
                if (miracle != null && GetMiracleOptions(credits).Contains(miracle.id) && controller != null)
                {
                    int seed = HashCode(message.choiceKey, credits.MiracleClaimed);
                    ItemMetadata[] items = miracle.GetItems(false, controller, seed);
                    if (controller.miracles.Count > 0) controller.RemoveMiracle(0);
                    controller.AddMiracle(miracle.id);
                    if (items != null && player.Inventory != null)
                        player.Inventory.AddItemsWithGenerateInstanceID(seed, items, true, true);
                credits.Miracles--;
                credits.MiracleClaimed++;
                    if (credits.CapturedMiracles.Count > 0) credits.CapturedMiracles.Clear();
                    AddHistory(credits, "Claimed Miracle: " + miracle.id);
                    claimed = true;
                }
            }
            else if (message.rewardType == 4 && credits.Tablets > credits.PendingTablets &&
                     SpawnSephirite(player, "Sephirite_Tablet", credits.TabletClaimed, credits, connection, 4))
            {
                credits.PendingTablets++;
                claimed = true;
            }
            else if (message.rewardType == 5 && credits.Bosses > credits.PendingBosses &&
                     (message.choiceKey == "SEPHIRITE_BOSS" || message.choiceKey == "SEPHIRITE_TABLET"))
            {
                string prefab = message.choiceKey == "SEPHIRITE_BOSS" ? "Sephirite_Huge" : "Sephirite_Tablet";
                if (SpawnSephirite(player, prefab, credits.BossClaimed, credits, connection, 5))
                {
                    credits.PendingBosses++;
                    claimed = true;
                }
            }
            else if (message.rewardType == 6 && credits.Charms > credits.PendingCharms &&
                     SpawnSephirite(player, "Sephirite_Charm", credits.CharmClaimed, credits, connection, 6))
            {
                credits.PendingCharms++;
                claimed = true;
            }
            if (claimed && message.rewardType <= 3)
            {
                player.spawner?.SaveCurrentSessionData();
                Save(credits);
            }
            SendOffer(connection, credits, claimed ? (byte)1 : (byte)2);
        }

        private static void SendOffer(NetworkConnectionToClient connection, Credits credits, byte result)
        {
            connection.Send(new CatchUpOfferMessage
            {
                weaponCredits = credits.Weapons,
                enchantCredits = credits.Enchants,
                miracleCredits = credits.Miracles,
                tabletCredits = Math.Max(0, credits.Tablets - credits.PendingTablets),
                bossCredits = Math.Max(0, credits.Bosses - credits.PendingBosses),
                charmCredits = Math.Max(0, credits.Charms - credits.PendingCharms),
                weaponClaimed = credits.WeaponClaimed,
                enchantClaimed = credits.EnchantClaimed,
                miracleClaimed = credits.MiracleClaimed,
                tabletClaimed = credits.TabletClaimed,
                bossClaimed = credits.BossClaimed,
                charmClaimed = credits.CharmClaimed,
                miracleOptions = string.Join("|", GetMiracleOptions(credits)),
                rules = BuildRules(),
                diagnostics = BuildHostDiagnostics(connection),
                history = string.Join("\n", credits.History),
                bossOptions = string.Join("|", credits.CapturedBossRewards),
                lastResult = result
            });
        }

        private static void OnClientOffer(CatchUpOfferMessage message)
        {
            ClientWeaponCredits = Math.Max(0, message.weaponCredits);
            ClientEnchantCredits = Math.Max(0, message.enchantCredits);
            ClientMiracleCredits = Math.Max(0, message.miracleCredits);
            ClientTabletCredits = Math.Max(0, message.tabletCredits);
            ClientBossCredits = Math.Max(0, message.bossCredits);
            ClientCharmCredits = Math.Max(0, message.charmCredits);
            ClientWeaponClaimed = Math.Max(0, message.weaponClaimed);
            ClientEnchantClaimed = Math.Max(0, message.enchantClaimed);
            ClientMiracleClaimed = Math.Max(0, message.miracleClaimed);
            ClientTabletClaimed = Math.Max(0, message.tabletClaimed);
            ClientBossClaimed = Math.Max(0, message.bossClaimed);
            ClientCharmClaimed = Math.Max(0, message.charmClaimed);
            ClientMiracleOptions = message.miracleOptions ?? "";
            ClientLastResult = message.lastResult;
            ClientRules = message.rules ?? "";
            ClientDiagnostics = message.diagnostics ?? "";
            ClientHistory = message.history ?? "";
            clientClaimPending = false;
        }

        private static Credits Load(string playerGuid)
        {
            Credits credits = new Credits();
            if (string.IsNullOrWhiteSpace(playerGuid) || SaveManager.CurrentRun == null) return credits;
            string prefix = "SephiriaTogetherCatchUp_" + Hash(playerGuid) + "_";
            Credits active = PendingSephirites.Values
                .Select(pending => pending.Credits)
                .FirstOrDefault(candidate => candidate != null && candidate.SavePrefix == prefix);
            if (active != null) return active;
            credits.SavePrefix = prefix;
            credits.Weapons = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "WeaponPending", 0));
            credits.Enchants = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "EnchantPending", 0));
            credits.Miracles = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "MiraclePending", 0));
            credits.Tablets = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "TabletPending", 0));
            credits.Bosses = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "BossPending", 0));
            credits.Charms = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "CharmPending", 0));
            credits.WeaponClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "WeaponClaimed", 0));
            credits.EnchantClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "EnchantClaimed", 0));
            credits.MiracleClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "MiracleClaimed", 0));
            credits.TabletClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "TabletClaimed", 0));
            credits.BossClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "BossClaimed", 0));
            credits.CharmClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "CharmClaimed", 0));
            string floors = SaveManager.CurrentRun.GetString(credits.SavePrefix + "CountedFloors", "");
            foreach (string floor in floors.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                credits.CountedFloors.Add(floor);
            string history = SaveManager.CurrentRun.GetString(credits.SavePrefix + "History", "");
            credits.History.AddRange(history.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            credits.CapturedMiracles.AddRange(SaveManager.CurrentRun.GetString(credits.SavePrefix + "MiracleOffers", "")
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            credits.CapturedBossRewards.AddRange(SaveManager.CurrentRun.GetString(credits.SavePrefix + "BossOffers", "")
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            return credits;
        }

        private static void Save(Credits credits)
        {
            if (credits == null || string.IsNullOrEmpty(credits.SavePrefix) || SaveManager.CurrentRun == null) return;
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "WeaponPending", credits.Weapons);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "EnchantPending", credits.Enchants);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "MiraclePending", credits.Miracles);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "TabletPending", credits.Tablets);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "BossPending", credits.Bosses);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "CharmPending", credits.Charms);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "WeaponClaimed", credits.WeaponClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "EnchantClaimed", credits.EnchantClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "MiracleClaimed", credits.MiracleClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "TabletClaimed", credits.TabletClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "BossClaimed", credits.BossClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "CharmClaimed", credits.CharmClaimed);
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "CountedFloors", string.Join("|", credits.CountedFloors));
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "History", string.Join("\n", credits.History));
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "MiracleOffers", string.Join("|", credits.CapturedMiracles));
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "BossOffers", string.Join("|", credits.CapturedBossRewards));
            SaveManager.Save(saveCurrent: false, saveCurrentRun: true);
        }

        private static string Hash(string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(bytes, 0, 12).Replace("-", "");
        }

        private static int HashCode(string value, int salt)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes((value ?? "") + ":" + salt));
            return BitConverter.ToInt32(bytes, 0);
        }

        private static List<string> GetMiracleOptions(Credits credits)
        {
            if (credits.CapturedMiracles.Count > 0)
            {
                return credits.CapturedMiracles.Take(3).ToList();
            }
            List<string> pool = MiracleDatabase.GetAll()
                .Where(miracle => miracle != null && miracle.isEnabled && miracle.tier == Miracle.ETier.Tier1)
                .Select(miracle => miracle.id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            List<string> result = new List<string>();
            if (pool.Count == 0 || credits.Miracles <= 0) return result;
            System.Random random = new System.Random(HashCode(credits.SavePrefix, credits.MiracleClaimed));
            while (result.Count < 3 && pool.Count > 0)
            {
                int index = random.Next(pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }

        private static bool SpawnSephirite(
            PlayerAvatar player,
            string prefabName,
            int salt,
            Credits credits,
            NetworkConnectionToClient connection,
            byte rewardType)
        {
            if (player == null || player.spawner == null) return false;
            GameObject prefab = Resources.Load<GameObject>("Sephirite/" + prefabName);
            if (prefab == null) return false;
            GameObject instance = UnityEngine.Object.Instantiate(prefab, player.transform.position, Quaternion.identity);
            Sephirite sephirite = instance.GetComponent<Sephirite>();
            if (sephirite == null)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }
            sephirite.Initialize(HashCode(player.spawner.playerGuid, salt));
            NetworkServer.Spawn(instance, player.gameObject);
            PendingSephirites[sephirite.netId] = new PendingSephirite
            {
                Credits = credits,
                Connection = connection,
                RewardType = rewardType
            };
            return true;
        }

        internal static void CompleteSephirite(PlayerAvatar player, Sephirite sephirite)
        {
            if (sephirite == null || !PendingSephirites.TryGetValue(sephirite.netId, out PendingSephirite pending)) return;
            PendingSephirites.Remove(sephirite.netId);
            Credits credits = pending.Credits;
            if (pending.RewardType == 4)
            {
                credits.PendingTablets--;
                credits.Tablets--;
                credits.TabletClaimed++;
                AddHistory(credits, "Claimed Stone Tablet reward");
            }
            else if (pending.RewardType == 5)
            {
                credits.PendingBosses--;
                credits.Bosses--;
                credits.BossClaimed++;
                AddHistory(credits, "Claimed boss reward");
            }
            else if (pending.RewardType == 6)
            {
                credits.PendingCharms--;
                credits.Charms--;
                credits.CharmClaimed++;
                AddHistory(credits, "Claimed Charm reward");
            }
            player?.spawner?.SaveCurrentSessionData();
            Save(credits);
            if (credits.ClientMod && pending.Connection != null && pending.Connection.isReady)
                SendOffer(pending.Connection, credits, 1);
        }

        internal static void ReleaseSephirite(Sephirite sephirite)
        {
            if (sephirite == null || !PendingSephirites.TryGetValue(sephirite.netId, out PendingSephirite pending)) return;
            PendingSephirites.Remove(sephirite.netId);
            if (pending.RewardType == 4) pending.Credits.PendingTablets--;
            else if (pending.RewardType == 5) pending.Credits.PendingBosses--;
            else if (pending.RewardType == 6) pending.Credits.PendingCharms--;
            if (pending.Credits.ClientMod && pending.Connection != null && pending.Connection.isReady)
                SendOffer(pending.Connection, pending.Credits, 0);
        }

        internal static bool HasNewResourceFloors(PlayerSpawner player, List<string> missedFloors)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.playerGuid) || SaveManager.CurrentRun == null)
                return false;
            HashSet<string> granted = new HashSet<string>(SaveManager.CurrentRun
                .GetString(ResourceKey(player.playerGuid), "")
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            return missedFloors.Any(floor => !granted.Contains(floor));
        }

        internal static void CommitResourceFloors(PlayerSpawner player, List<string> missedFloors)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.playerGuid) || SaveManager.CurrentRun == null) return;
            string key = ResourceKey(player.playerGuid);
            HashSet<string> granted = new HashSet<string>(SaveManager.CurrentRun.GetString(key, "")
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            granted.UnionWith(missedFloors.Where(floor => !string.IsNullOrEmpty(floor)));
            SaveManager.CurrentRun.SetString(key, string.Join("|", granted));
        }

        private static string ResourceKey(string playerGuid) =>
            "SephiriaTogetherCatchUp_" + Hash(playerGuid) + "_ResourceFloors";

        internal static bool HostSupportsProtocol()
        {
            GameObject steamManager = SingletonObject.Find("SteamManager");
            return steamManager != null && steamManager.TryGetComponent(out LobbyManager manager) &&
                   manager.HasLobby && manager.Lobby["SephiriaTogether"] == Plugin.PluginVersion;
        }

        internal static bool IsModdedConnection(NetworkConnectionToClient connection) =>
            connection != null && ServerCredits.TryGetValue(connection.connectionId, out Credits credits) && credits.ClientMod;

        private static string BuildRules()
        {
            return $"Sephiria Together {Plugin.PluginVersion}\n" +
                   $"Game {Application.version}\n" +
                   $"Mid-run join: {OnOff(Plugin.allowMidRunJoin.Value)}\n" +
                   $"Lower progress: {OnOff(Plugin.allowLowerProgressPlayers.Value)}\n" +
                   $"Ungrouped transition: {OnOff(Plugin.allowUngroupedStageTransition.Value)}\n" +
                   $"Friendly fire: {OnOff(Plugin.friendlyFire.Value)}\n" +
                   $"Delayed healing: {OnOff(Plugin.breathingHeal.Value)}\n" +
                   $"Auto revive when clear: {OnOff(Plugin.autoReviveWhenClear.Value)}\n" +
                   $"EXP catch-up: {Plugin.catchUpExperienceRatio.Value:P0}\n" +
                   $"Enemy HP: +{Plugin.HealthPerExtraPlayerValue:P0} per player above {Plugin.BaselinePlayersValue}, cap {Plugin.MaximumMultiplierValue:0.##}x\n" +
                   $"Enemy count: {OnOff(Plugin.scaleEnemyCount.Value)}, +{Plugin.EnemyCountPerExtraPlayerValue:P0}, cap {Plugin.MaximumEnemyCountMultiplierValue:0.##}x\n" +
                   BuildOriginalScalingSummary() + "\n" +
                   $"Player limit: {PlayerLimit.CurrentLimit}";
        }

        internal static string BuildHostDiagnostics(NetworkConnectionToClient connection)
        {
            PlayerSpawner player = connection?.identity != null ? connection.identity.GetComponent<PlayerSpawner>() : null;
            string floor = player?.PlayerAvatar?.currentFloorGuid ?? "-";
            string identity = !string.IsNullOrEmpty(player?.playerGuid) ? Hash(player.playerGuid) : "-";
            return $"Protocol: {Plugin.PluginVersion}\n" +
                   $"Game: {Application.version}\n" +
                   $"Server active: {NetworkServer.active}\n" +
                   $"Client active: {NetworkClient.active}\n" +
                   $"Authenticated mod handshake: {(connection != null ? "YES" : "HOST")}\n" +
                   $"Connection ID: {(connection != null ? connection.connectionId.ToString() : "local")}\n" +
                   $"Player hash: {identity}\nFloor: {floor}\n" +
                   $"Players: {(PlayerSpawner.MultiplayerList?.Count ?? 0)}\n" +
                   BuildOriginalScalingSummary();
        }

        internal static string BuildOriginalScalingSummary()
        {
            try
            {
                int normal = KeywordDatabase.GetConstValue("enemyBonusHpByPlayerNumber");
                int miniboss = KeywordDatabase.GetConstValue("minibossBonusHpByPlayerNumber");
                int boss = KeywordDatabase.GetConstValue("bossBonusHpByPlayerNumber");
                int hardPoints = DungeonManager.Instance != null ? DungeonManager.Instance.CalculateCurrentHardModePoints() : 0;
                int tenacious = GetHardModeValue("TENACIOUSBODY");
                int ferocious = GetHardModeValue("FEROCIOUSCLAWS");
                return $"Vanilla HP/player: normal +{normal}%, miniboss +{miniboss}%, boss +{boss}%\n" +
                       $"Hard mode: {hardPoints} points, Tenacious Body +{tenacious}% HP, Ferocious Claws +{ferocious}% damage";
            }
            catch (Exception)
            {
                return "Vanilla scaling data: unavailable";
            }
        }

        private static int GetHardModeValue(string key)
        {
            return DungeonManager.Instance != null && DungeonManager.Instance.hardModeEnvironment.TryGetValue(key, out int value)
                ? value
                : 0;
        }

        internal static string GetHostHistory()
        {
            IEnumerable<string> entries = ServerCredits.Values
                .SelectMany(credits => credits.History)
                .TakeLast(40);
            return string.Join("\n", entries);
        }

        private static void AddHistory(Credits credits, string message)
        {
            if (credits == null || string.IsNullOrEmpty(message)) return;
            credits.History.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message.Replace('\n', ' '));
            while (credits.History.Count > 40) credits.History.RemoveAt(0);
        }

        private static string ShortGuid(string value) =>
            string.IsNullOrEmpty(value) ? "-" : value.Substring(0, Math.Min(8, value.Length));

        private static string OnOff(bool value) => value ? "ON" : "OFF";

        internal static void CaptureMiracles(MiracleController controller, MiracleMetadata[] candidates)
        {
            if (!NetworkServer.active || controller?.UnitAvatar == null || candidates == null) return;
            PlayerSpawner spawner = controller.UnitAvatar.GetComponent<PlayerSpawner>();
            Credits credits = GetServerCredits(spawner);
            foreach (MiracleMetadata candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate.id) && !credits.CapturedMiracles.Contains(candidate.id))
                    credits.CapturedMiracles.Add(candidate.id);
            }
            Save(credits);
        }

        internal static void CaptureBossRewards(BossRewardSpawner spawner)
        {
            if (!NetworkServer.active || spawner == null) return;
            foreach (BossRewardSpawner.GeneratedRewardInfo reward in spawner.generatedRewards)
            {
                PlayerSpawner player = PlayerSpawner.MultiplayerList?.FirstOrDefault(item =>
                    item != null && item.currentPlayerIdxForSave == reward.playerIndex);
                if (player == null) continue;
                Credits credits = GetServerCredits(player);
                if (!string.IsNullOrEmpty(reward.rewardName) && !credits.CapturedBossRewards.Contains(reward.rewardName))
                    credits.CapturedBossRewards.Add(reward.rewardName);
                Save(credits);
            }
        }

        private static Credits GetServerCredits(PlayerSpawner spawner)
        {
            if (spawner?.connectionToClient != null && ServerCredits.TryGetValue(spawner.connectionToClient.connectionId, out Credits credits))
                return credits;
            credits = Load(spawner?.playerGuid);
            if (spawner?.connectionToClient != null)
                ServerCredits[spawner.connectionToClient.connectionId] = credits;
            return credits;
        }
    }

    [HarmonyPatch(typeof(PlayerSpawner), "OnStartAuthority")]
    internal static class CatchUpClientHelloPatch
    {
        private static void Postfix() => CatchUpRewards.SendHello();
    }

    [HarmonyPatch(typeof(HorayNetworkManager), "OnStopClient")]
    internal static class CatchUpClientCleanupPatch
    {
        private static void Postfix() => CatchUpRewards.ClearClientState();
    }

    [HarmonyPatch(typeof(PlayerAvatar), "FinishSephiriteAcquire")]
    internal static class CatchUpSephiriteCompletePatch
    {
        private static void Prefix(PlayerAvatar __instance, Sephirite sephirite) =>
            CatchUpRewards.CompleteSephirite(__instance, sephirite);
    }

    [HarmonyPatch(typeof(Sephirite), "OnDestroy")]
    internal static class CatchUpSephiriteReleasePatch
    {
        private static void Prefix(Sephirite __instance) => CatchUpRewards.ReleaseSephirite(__instance);
    }

    [HarmonyPatch(typeof(HorayNetworkManager), "OnStartServer")]
    internal static class CatchUpServerMessagesPatch
    {
        private static void Postfix() => CatchUpRewards.RegisterServerMessages();
    }

    [HarmonyPatch(typeof(HorayNetworkManager), "OnStartClient")]
    internal static class CatchUpClientMessagesPatch
    {
        private static void Postfix() => CatchUpRewards.RegisterClientMessages();
    }

    [HarmonyPatch(typeof(HorayNetworkManager), "OnStopServer")]
    internal static class CatchUpServerCleanupPatch
    {
        private static void Postfix() => CatchUpRewards.ClearServerState();
    }

    [HarmonyPatch(typeof(MiracleSelector2), "GenerateMiracles")]
    internal static class CatchUpMiracleCapturePatch
    {
        private static void Postfix(NetworkIdentity identity, MiracleMetadata[] __result)
        {
            MiracleController controller = identity != null ? identity.GetComponent<MiracleController>() : null;
            CatchUpRewards.CaptureMiracles(controller, __result);
        }
    }

    [HarmonyPatch(typeof(BossRewardSpawner), "OnStartServer")]
    internal static class CatchUpBossCapturePatch
    {
        private static void Postfix(BossRewardSpawner __instance) => CatchUpRewards.CaptureBossRewards(__instance);
    }
}
