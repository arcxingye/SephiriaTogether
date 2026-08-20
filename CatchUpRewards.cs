using System;
using System.Collections;
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
        public int fusionCredits;
        public int weaponClaimed;
        public int enchantClaimed;
        public int miracleClaimed;
        public int tabletClaimed;
        public int bossClaimed;
        public int charmClaimed;
        public int fusionClaimed;
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
            internal int Fusions;
            internal int WeaponClaimed;
            internal int EnchantClaimed;
            internal int MiracleClaimed;
            internal int TabletClaimed;
            internal int BossClaimed;
            internal int CharmClaimed;
            internal int FusionClaimed;
            internal bool ClientMod;
            internal string SavePrefix;
            internal int PendingTablets;
            internal int PendingBosses;
            internal int PendingCharms;
            internal int PendingWeapons;
            internal int PendingEnchants;
            internal int PendingMiracles;
            internal int PendingFusions;
            internal readonly HashSet<string> CountedFloors = new HashSet<string>();
            internal readonly HashSet<string> PendingAnvilFloors = new HashSet<string>();
            internal readonly HashSet<string> PendingEnchantFloors = new HashSet<string>();
            internal readonly HashSet<string> PendingMiracleFloors = new HashSet<string>();
            internal readonly HashSet<string> PendingCharmFloors = new HashSet<string>();
            internal readonly HashSet<string> PendingTabletFloors = new HashSet<string>();
            internal readonly HashSet<string> PendingFusionFloors = new HashSet<string>();
            internal readonly List<string> History = new List<string>();
            internal readonly List<string> CapturedMiracles = new List<string>();
            internal readonly List<string> CapturedBossRewards = new List<string>();
        }

        private sealed class PendingSephirite
        {
            internal Credits Credits;
            internal NetworkConnectionToClient Connection;
            internal byte RewardType;
            internal int Group;
        }

        private sealed class BossRewardSession
        {
            internal string FloorGuid;
            internal readonly Dictionary<string, int> PlayerSlots = new Dictionary<string, int>();
        }

        private static readonly Dictionary<int, Credits> ServerCredits = new Dictionary<int, Credits>();
        private static readonly HashSet<int> ModdedConnections = new HashSet<int>();
        private static readonly Dictionary<uint, PendingSephirite> PendingSephirites = new Dictionary<uint, PendingSephirite>();
        private static readonly Dictionary<uint, BossRewardSession> BossRewardSessions = new Dictionary<uint, BossRewardSession>();
        private static bool clientHelloSent;
        private static bool clientClaimPending;
        private static int nextRewardGroup;
        internal static int ClientWeaponCredits { get; private set; }
        internal static int ClientEnchantCredits { get; private set; }
        internal static int ClientMiracleCredits { get; private set; }
        internal static int ClientTabletCredits { get; private set; }
        internal static int ClientBossCredits { get; private set; }
        internal static int ClientCharmCredits { get; private set; }
        internal static int ClientFusionCredits { get; private set; }
        internal static int ClientWeaponClaimed { get; private set; }
        internal static int ClientEnchantClaimed { get; private set; }
        internal static int ClientMiracleClaimed { get; private set; }
        internal static int ClientTabletClaimed { get; private set; }
        internal static int ClientBossClaimed { get; private set; }
        internal static int ClientCharmClaimed { get; private set; }
        internal static int ClientFusionClaimed { get; private set; }
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
            MidRunJoin.RegisterServerMessages();
            RescueAlerts.RegisterServerMessages();
            AutoPilot.RegisterServerMessages();
            MoneyTransfer.RegisterServerMessages();
            StartProgressSelection.RegisterServerMessages();
        }

        internal static void RegisterClientMessages()
        {
            ConfigureSerialization();
            NetworkClient.RegisterHandler<CatchUpOfferMessage>(OnClientOffer, true);
            RescueAlerts.RegisterClientMessages();
            AutoPilot.RegisterClientMessages();
            MoneyTransfer.RegisterClientMessages();
            StartProgressSelection.RegisterClientMessages();
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
                writer.WriteVarInt(value.fusionCredits);
                writer.WriteVarInt(value.weaponClaimed);
                writer.WriteVarInt(value.enchantClaimed);
                writer.WriteVarInt(value.miracleClaimed);
                writer.WriteVarInt(value.tabletClaimed);
                writer.WriteVarInt(value.bossClaimed);
                writer.WriteVarInt(value.charmClaimed);
                writer.WriteVarInt(value.fusionClaimed);
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
                fusionCredits = reader.ReadVarInt(),
                weaponClaimed = reader.ReadVarInt(),
                enchantClaimed = reader.ReadVarInt(),
                miracleClaimed = reader.ReadVarInt(),
                tabletClaimed = reader.ReadVarInt(),
                bossClaimed = reader.ReadVarInt(),
                charmClaimed = reader.ReadVarInt(),
                fusionClaimed = reader.ReadVarInt(),
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

        internal static void Prepare(PlayerSpawner newcomer, IEnumerable<string> missedFloors)
        {
            if (!NetworkServer.active || CloneBotManager.IsBot(newcomer) || newcomer?.connectionToClient == null ||
                newcomer.PlayerAvatar == null || DungeonManager.Instance == null)
            {
                return;
            }
            bool clientMod = ModdedConnections.Contains(newcomer.connectionToClient.connectionId) ||
                             ServerCredits.TryGetValue(newcomer.connectionToClient.connectionId, out Credits existing) &&
                             existing.ClientMod;
            Credits credits = Load(newcomer.playerGuid);
            credits.ClientMod = clientMod;
            ServerCredits[newcomer.connectionToClient.connectionId] = credits;
            ConvertPendingAnvils(newcomer, newcomer.PlayerAvatar.currentFloorGuid);
            ConvertPendingEnchants(newcomer, newcomer.PlayerAvatar.currentFloorGuid);
            ConvertPendingChoiceFloors(newcomer, newcomer.PlayerAvatar.currentFloorGuid);
            ConvertPendingFusions(newcomer, newcomer.PlayerAvatar.currentFloorGuid);
            Plugin.LogInfo($"Catch-up prepare: player={newcomer.PlayerAvatar.Name}, conn={newcomer.connectionToClient.connectionId}, " +
                           $"guid={ShortGuid(newcomer.playerGuid)}, floor={ShortGuid(newcomer.PlayerAvatar.currentFloorGuid)}, " +
                           $"history={newcomer.PlayerAvatar.floorTravelHistory.Count}, pendingWeapon={credits.Weapons}, " +
                           $"pendingAnvilFloors={credits.PendingAnvilFloors.Count}.");

            HashSet<string> counted = new HashSet<string>();
            bool changed = false;
            foreach (string guid in missedFloors ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrEmpty(guid) || !counted.Add(guid) ||
                    !DungeonManager.Instance.generatedFloors.TryGetValue(guid, out FloorData floor))
                {
                    continue;
                }

                Plugin.LogInfo($"Catch-up eligible completed floor: player={newcomer.PlayerAvatar.Name}, floor={ShortGuid(guid)}, " +
                               $"progress={floor.nodeProgress}, event={floor.mainEventType}, threat={floor.threatType}.");

                string eventKey = EventKey(guid, floor.mainEventType);
                bool alreadyProcessed = WasEventProcessed(credits, guid, floor.mainEventType) ||
                                        floor.mainEventType == EFloorMainEventType.StoneTablet &&
                                        credits.CountedFloors.Contains(TabletFusionChoiceKey(guid));
                if (!alreadyProcessed && IsLegacyRemovedFloor(credits, guid, floor.mainEventType))
                {
                    alreadyProcessed = true;
                    credits.CountedFloors.Add(eventKey);
                    changed = true;
                    Plugin.LogInfo($"Migrated legacy removed floor marker: player={newcomer.PlayerAvatar.Name}, " +
                                   $"floor={ShortGuid(guid)}, event={floor.mainEventType}.");
                }
                if (!alreadyProcessed)
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
                else
                {
                    Plugin.LogInfo($"Catch-up skipped already processed floor: player={newcomer.PlayerAvatar.Name}, " +
                                   $"floor={ShortGuid(guid)}, event={floor.mainEventType}.");
                }

                string fusionKey = guid + ":Fusion";
                bool tabletFusionChoice = floor.mainEventType == EFloorMainEventType.StoneTablet &&
                                           FusionCompensation.IsObservedFloor(guid);
                if (!tabletFusionChoice && FusionCompensation.IsObservedFloor(guid) &&
                    !WasFusionProcessed(credits, guid))
                {
                    credits.Fusions++;
                    credits.CountedFloors.Add(fusionKey);
                    AddHistory(credits, "Catch-up: Tablet Fusion @ " + ShortGuid(guid));
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
            AnvilCompensation.ScheduleSpawn(newcomer);
            ScheduleRewardObjects(newcomer);
            FusionCompensation.ScheduleSpawn(newcomer);
        }

        internal static bool ShouldTrackUnclaimedFloor(PlayerSpawner player)
        {
            if (!NetworkServer.active || player?.PlayerAvatar == null || player.connectionToClient == null) return false;
            if (CloneBotManager.IsBot(player)) return false;
            return !player.isHost && player.connectionToClient != NetworkServer.localConnection;
        }

        internal static void DiscardUntrackedFloorOpportunities(PlayerSpawner player)
        {
            if (!NetworkServer.active || player == null ||
                !player.isHost && player.connectionToClient != NetworkServer.localConnection) return;
            if (CloneBotManager.IsBot(player)) return;
            Credits credits = GetServerCredits(player);
            int removed = credits.PendingAnvilFloors.Count + credits.PendingEnchantFloors.Count +
                          credits.PendingMiracleFloors.Count + credits.PendingCharmFloors.Count +
                          credits.PendingTabletFloors.Count + credits.PendingFusionFloors.Count;
            if (removed == 0) return;
            credits.PendingAnvilFloors.Clear();
            credits.PendingEnchantFloors.Clear();
            credits.PendingMiracleFloors.Clear();
            credits.PendingCharmFloors.Clear();
            credits.PendingTabletFloors.Clear();
            credits.PendingFusionFloors.Clear();
            AddHistory(credits, $"Discarded {removed} local/host reward-floor marker(s)");
            Plugin.LogInfo($"Discarded ineligible local reward-floor markers: player={player?.PlayerAvatar?.Name}, " +
                           $"count={removed}, host={player?.isHost}, party={CloneBotManager.RealPlayerCount}.");
            Save(credits);
        }

        internal static void RecordPendingFusion(PlayerSpawner player, string floorGuid)
        {
            if (!ShouldTrackUnclaimedFloor(player)) return;
            Credits credits = GetServerCredits(player);
            if (WasFusionProcessed(credits, floorGuid)) return;
            if (string.IsNullOrEmpty(floorGuid) || !credits.PendingFusionFloors.Add(floorGuid)) return;
            Plugin.LogInfo($"Pending Tablet Fusion floor recorded: player={player?.PlayerAvatar?.Name}, floor={ShortGuid(floorGuid)}.");
            Save(credits);
        }

        internal static void ConvertPendingFusions(PlayerSpawner player, string currentFloorGuid)
        {
            Credits credits = GetServerCredits(player);
            if (!ConvertPendingFusionSet(credits, currentFloorGuid, player)) return;
            Save(credits);
            Plugin.LogInfo($"Pending Tablet Fusion converted: player={player?.PlayerAvatar?.Name}, fusionCredits={credits.Fusions}.");
        }

        private static bool ConvertPendingFusionSet(Credits credits, string currentFloorGuid, PlayerSpawner player)
        {
            bool changed = false;
            foreach (string floor in credits.PendingFusionFloors.ToArray())
            {
                if (floor == currentFloorGuid) continue;
                credits.PendingFusionFloors.Remove(floor);
                bool mutualChoice = IsTabletFusionChoiceFloor(floor);
                if (WasFusionProcessed(credits, floor) ||
                    mutualChoice && (WasEventProcessed(credits, floor, EFloorMainEventType.StoneTablet) ||
                                     credits.CountedFloors.Contains(TabletFusionChoiceKey(floor))))
                {
                    if (mutualChoice) credits.PendingTabletFloors.Remove(floor);
                    changed = true;
                    continue;
                }
                if (mutualChoice)
                {
                    credits.PendingTabletFloors.Remove(floor);
                    credits.CountedFloors.Add(TabletFusionChoiceKey(floor));
                    credits.CountedFloors.Add(EventKey(floor, EFloorMainEventType.StoneTablet));
                    credits.Tablets++;
                    AddHistory(credits, "Catch-up: unclaimed Tablet/Fusion choice -> StoneTablet @ " + ShortGuid(floor));
                    Plugin.LogInfo($"Mutual Tablet/Fusion choice converted once: player={player?.PlayerAvatar?.Name}, " +
                                   $"floor={ShortGuid(floor)}, grant=StoneTablet.");
                }
                else
                {
                    credits.Fusions++;
                    credits.CountedFloors.Add(EventKey(floor, "Fusion"));
                    AddHistory(credits, "Catch-up: unclaimed Tablet Fusion @ " + ShortGuid(floor));
                }
                changed = true;
            }
            return changed;
        }

        internal static void MarkCurrentFusionClaimed(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            string floor = player?.PlayerAvatar?.currentFloorGuid;
            if (string.IsNullOrEmpty(floor)) return;
            bool fusion = credits.PendingFusionFloors.Remove(floor);
            bool tablet = credits.PendingTabletFloors.Remove(floor);
            if (!fusion && !tablet && !FusionCompensation.IsObservedFloor(floor)) return;
            if (IsTabletFusionChoiceFloor(floor))
            {
                credits.CountedFloors.Add(TabletFusionChoiceKey(floor));
                credits.CountedFloors.Add(EventKey(floor, EFloorMainEventType.StoneTablet));
            }
            else
            {
                credits.CountedFloors.Add(EventKey(floor, "Fusion"));
            }
            Plugin.LogInfo($"Original Tablet Fusion used: player={player.PlayerAvatar?.Name}, floor={ShortGuid(floor)}, " +
                           $"clearedFusion={fusion}, clearedTabletAlternative={tablet}.");
            AddHistory(credits, "Original Tablet Fusion used @ " + ShortGuid(floor));
            player.SaveCurrentSessionData();
            Save(credits);
        }

        internal static bool ConvertCurrentFusionToCredit(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            string floor = player?.PlayerAvatar?.currentFloorGuid;
            if (string.IsNullOrEmpty(floor) || !credits.PendingFusionFloors.Remove(floor)) return false;
            bool mutualChoice = IsTabletFusionChoiceFloor(floor);
            if (WasFusionProcessed(credits, floor) ||
                mutualChoice && (WasEventProcessed(credits, floor, EFloorMainEventType.StoneTablet) ||
                                 credits.CountedFloors.Contains(TabletFusionChoiceKey(floor))))
            {
                Save(credits);
                return false;
            }
            if (mutualChoice)
            {
                credits.PendingTabletFloors.Add(floor);
                Save(credits);
                Plugin.LogInfo($"Current Tablet Fusion unavailable on mutual-choice floor; preserving only Tablet alternative: " +
                               $"player={player.PlayerAvatar?.Name}, floor={ShortGuid(floor)}.");
                return false;
            }
            credits.Fusions++;
            credits.CountedFloors.Add(EventKey(floor, "Fusion"));
            AddHistory(credits, "Catch-up: no personal Tablet Fusion spawned @ " + ShortGuid(floor));
            Save(credits);
            Plugin.LogInfo($"Current Tablet Fusion converted for late player: player={player.PlayerAvatar?.Name}, " +
                           $"floor={ShortGuid(floor)}, fusionCredits={credits.Fusions}.");
            return true;
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

        internal static void RecordPendingAnvil(PlayerSpawner player, string floorGuid)
        {
            if (!ShouldTrackUnclaimedFloor(player)) return;
            Credits credits = GetServerCredits(player);
            if (ClearWeaponCreditsIfMaxed(player, credits)) return;
            if (WasEventProcessed(credits, floorGuid, EFloorMainEventType.Anvil)) return;
            if (!string.IsNullOrEmpty(floorGuid) && credits.PendingAnvilFloors.Add(floorGuid))
            {
                Plugin.LogInfo($"Pending Anvil floor recorded: player={player?.PlayerAvatar?.Name}, floor={ShortGuid(floorGuid)}, count={credits.PendingAnvilFloors.Count}.");
                Save(credits);
            }
        }

        internal static void ConvertPendingAnvils(PlayerSpawner player, string currentFloorGuid)
        {
            Credits credits = GetServerCredits(player);
            if (ClearWeaponCreditsIfMaxed(player, credits)) return;
            bool changed = false;
            foreach (string floor in credits.PendingAnvilFloors.ToArray())
            {
                if (floor == currentFloorGuid) continue;
                credits.PendingAnvilFloors.Remove(floor);
                if (WasEventProcessed(credits, floor, EFloorMainEventType.Anvil))
                {
                    changed = true;
                    continue;
                }
                credits.Weapons++;
                credits.CountedFloors.Add(EventKey(floor, EFloorMainEventType.Anvil));
                AddHistory(credits, "Catch-up: unclaimed Anvil @ " + ShortGuid(floor));
                changed = true;
            }
            if (changed) Save(credits);
            if (changed)
                Plugin.LogInfo($"Pending Anvil floors converted after travel: player={player?.PlayerAvatar?.Name}, weaponCredits={credits.Weapons}.");
        }

        internal static void ConvertAllPendingAnvils(PlayerSpawner player)
        {
            if (!NetworkServer.active || player == null) return;
            Credits credits = GetServerCredits(player);
            if (ClearWeaponCreditsIfMaxed(player, credits)) return;
            if (credits.PendingAnvilFloors.Count == 0) return;
            foreach (string floor in credits.PendingAnvilFloors)
            {
                if (WasEventProcessed(credits, floor, EFloorMainEventType.Anvil)) continue;
                credits.Weapons++;
                credits.CountedFloors.Add(EventKey(floor, EFloorMainEventType.Anvil));
                AddHistory(credits, "Catch-up: disconnected before claiming Anvil @ " + ShortGuid(floor));
            }
            Plugin.LogInfo($"Converted {credits.PendingAnvilFloors.Count} unclaimed Anvil floor(s) for disconnected player.");
            credits.PendingAnvilFloors.Clear();
            Save(credits);
        }

        internal static void RecordPendingEnchant(PlayerSpawner player, string floorGuid)
        {
            if (!ShouldTrackUnclaimedFloor(player)) return;
            Credits credits = GetServerCredits(player);
            if (WasEventProcessed(credits, floorGuid, EFloorMainEventType.Enchant)) return;
            if (!string.IsNullOrEmpty(floorGuid) && credits.PendingEnchantFloors.Add(floorGuid))
            {
                Plugin.LogInfo($"Pending Enchant floor recorded: player={player?.PlayerAvatar?.Name}, floor={ShortGuid(floorGuid)}, count={credits.PendingEnchantFloors.Count}.");
                Save(credits);
            }
        }

        internal static void ConvertPendingEnchants(PlayerSpawner player, string currentFloorGuid)
        {
            Credits credits = GetServerCredits(player);
            bool changed = false;
            foreach (string floor in credits.PendingEnchantFloors.ToArray())
            {
                if (floor == currentFloorGuid) continue;
                credits.PendingEnchantFloors.Remove(floor);
                if (WasEventProcessed(credits, floor, EFloorMainEventType.Enchant))
                {
                    changed = true;
                    continue;
                }
                credits.Enchants++;
                credits.CountedFloors.Add(EventKey(floor, EFloorMainEventType.Enchant));
                AddHistory(credits, "Catch-up: unclaimed Enchant @ " + ShortGuid(floor));
                changed = true;
            }
            if (!changed) return;
            Save(credits);
            Plugin.LogInfo($"Pending Enchant floors converted after travel: player={player?.PlayerAvatar?.Name}, enchantCredits={credits.Enchants}.");
        }

        internal static void MarkCurrentEnchantClaimed(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            string floor = player?.PlayerAvatar?.currentFloorGuid;
            if (string.IsNullOrEmpty(floor)) return;
            bool removed = credits.PendingEnchantFloors.Remove(floor);
            if (!removed && !CurrentFloorMatches(player, EFloorMainEventType.Enchant)) return;
            string eventKey = EventKey(floor, EFloorMainEventType.Enchant);
            credits.CountedFloors.Add(eventKey);
            Plugin.LogInfo($"Original Enchant floor claimed: player={player.PlayerAvatar.Name}, floor={ShortGuid(floor)}, " +
                           $"pendingRemoved={removed}.");
            AddHistory(credits, "Original Enchant floor claimed @ " + ShortGuid(floor));
            player.SaveCurrentSessionData();
            Save(credits);
        }

        internal static void RecordPendingChoiceFloor(PlayerSpawner player, string floorGuid, EFloorMainEventType type)
        {
            if (!ShouldTrackUnclaimedFloor(player)) return;
            Credits credits = GetServerCredits(player);
            if (type == EFloorMainEventType.StoneTablet &&
                credits.CountedFloors.Contains(TabletFusionChoiceKey(floorGuid))) return;
            if (WasEventProcessed(credits, floorGuid, type)) return;
            HashSet<string> pending = PendingChoiceSet(credits, type);
            if (pending == null || string.IsNullOrEmpty(floorGuid) || !pending.Add(floorGuid)) return;
            Plugin.LogInfo($"Pending {type} floor recorded: player={player?.PlayerAvatar?.Name}, floor={ShortGuid(floorGuid)}, count={pending.Count}.");
            Save(credits);
        }

        internal static void ForgetPendingChoiceFloor(PlayerSpawner player, string floorGuid, EFloorMainEventType type)
        {
            Credits credits = GetServerCredits(player);
            HashSet<string> pending = PendingChoiceSet(credits, type);
            if (pending == null || string.IsNullOrEmpty(floorGuid) || !pending.Remove(floorGuid)) return;
            Plugin.LogInfo($"Ignored pending {type} compensation for quest floor: player={player?.PlayerAvatar?.Name}, " +
                           $"floor={ShortGuid(floorGuid)}.");
            Save(credits);
        }

        internal static void ConvertPendingChoiceFloors(PlayerSpawner player, string currentFloorGuid)
        {
            Credits credits = GetServerCredits(player);
            bool changed = ConvertChoiceSet(credits.PendingMiracleFloors, currentFloorGuid, () => credits.Miracles++, "Miracle", credits) |
                           ConvertChoiceSet(credits.PendingCharmFloors, currentFloorGuid, () => credits.Charms++, "Charm", credits) |
                           ConvertPendingTabletChoices(credits, currentFloorGuid, player);
            if (!changed) return;
            Save(credits);
            Plugin.LogInfo($"Pending choice floors converted: player={player?.PlayerAvatar?.Name}, miracles={credits.Miracles}, " +
                           $"charms={credits.Charms}, tablets={credits.Tablets}.");
        }

        internal static void MarkCurrentChoiceClaimed(PlayerSpawner player, EFloorMainEventType type)
        {
            Credits credits = GetServerCredits(player);
            HashSet<string> pending = PendingChoiceSet(credits, type);
            string floor = player?.PlayerAvatar?.currentFloorGuid;
            if (pending == null || string.IsNullOrEmpty(floor)) return;
            bool currentFloorMatches = CurrentFloorMatches(player, type);
            bool claimed = pending.Remove(floor);
            bool fusionAlternative = type == EFloorMainEventType.StoneTablet &&
                                     (claimed || currentFloorMatches) &&
                                     credits.PendingFusionFloors.Remove(floor);
            if (!claimed && !fusionAlternative && !currentFloorMatches) return;
            string eventKey = EventKey(floor, type);
            if (type == EFloorMainEventType.StoneTablet)
            {
                if (fusionAlternative || IsTabletFusionChoiceFloor(floor))
                    credits.CountedFloors.Add(TabletFusionChoiceKey(floor));
                credits.CountedFloors.Add(EventKey(floor, EFloorMainEventType.StoneTablet));
            }
            else
                credits.CountedFloors.Add(eventKey);
            Plugin.LogInfo($"Original {type} floor claimed: player={player.PlayerAvatar.Name}, floor={ShortGuid(floor)}, " +
                           $"pendingRemoved={claimed}, clearedFusionAlternative={fusionAlternative}.");
            AddHistory(credits, "Original " + type + " floor claimed @ " + ShortGuid(floor));
            player.SaveCurrentSessionData();
            Save(credits);
        }

        private static bool ConvertChoiceSet(HashSet<string> pending, string currentFloorGuid, Action grant, string name, Credits credits)
        {
            bool changed = false;
            foreach (string floor in pending.ToArray())
            {
                if (floor == currentFloorGuid) continue;
                pending.Remove(floor);
                if (WasEventProcessed(credits, floor, name))
                {
                    changed = true;
                    continue;
                }
                grant();
                credits.CountedFloors.Add(EventKey(floor, name));
                AddHistory(credits, "Catch-up: unclaimed " + name + " @ " + ShortGuid(floor));
                changed = true;
            }
            return changed;
        }

        private static bool ConvertPendingTabletChoices(Credits credits, string currentFloorGuid, PlayerSpawner player)
        {
            bool changed = false;
            foreach (string floor in credits.PendingTabletFloors.ToArray())
            {
                if (floor == currentFloorGuid) continue;
                credits.PendingTabletFloors.Remove(floor);
                bool mutualChoice = IsTabletFusionChoiceFloor(floor);
                if (WasEventProcessed(credits, floor, EFloorMainEventType.StoneTablet) ||
                    mutualChoice && credits.CountedFloors.Contains(TabletFusionChoiceKey(floor)))
                {
                    if (mutualChoice) credits.PendingFusionFloors.Remove(floor);
                    changed = true;
                    continue;
                }
                bool fusionAlternative = mutualChoice && credits.PendingFusionFloors.Remove(floor);
                if (mutualChoice) credits.CountedFloors.Add(TabletFusionChoiceKey(floor));
                credits.CountedFloors.Add(EventKey(floor, EFloorMainEventType.StoneTablet));
                credits.Tablets++;
                AddHistory(credits, "Catch-up: unclaimed StoneTablet" +
                    (fusionAlternative ? "/Fusion choice" : "") + " @ " + ShortGuid(floor));
                if (fusionAlternative)
                    Plugin.LogInfo($"Mutual Tablet/Fusion choice converted once: player={player?.PlayerAvatar?.Name}, " +
                                   $"floor={ShortGuid(floor)}, grant=StoneTablet.");
                changed = true;
            }
            return changed;
        }

        private static bool IsTabletFusionChoiceFloor(string floorGuid)
        {
            if (string.IsNullOrEmpty(floorGuid) || !FusionCompensation.IsObservedFloor(floorGuid)) return false;
            if (DungeonManager.Instance != null &&
                DungeonManager.Instance.generatedFloors.TryGetValue(floorGuid, out FloorData floor))
                return floor.mainEventType == EFloorMainEventType.StoneTablet;
            return false;
        }

        private static string TabletFusionChoiceKey(string floorGuid) =>
            string.IsNullOrEmpty(floorGuid) ? "" : floorGuid + ":TabletOrFusionResolved";

        private static HashSet<string> PendingChoiceSet(Credits credits, EFloorMainEventType type)
        {
            if (type == EFloorMainEventType.Miracle) return credits.PendingMiracleFloors;
            if (type == EFloorMainEventType.Charm) return credits.PendingCharmFloors;
            if (type == EFloorMainEventType.StoneTablet) return credits.PendingTabletFloors;
            return null;
        }

        internal static bool WasDimensionPocketGranted(PlayerSpawner player)
        {
            if (SaveManager.CurrentRun == null || player == null) return false;
            return SaveManager.CurrentRun.GetBool(DimensionPocketGrantKey(player.playerGuid), false);
        }

        internal static void MarkDimensionPocketGranted(PlayerSpawner player)
        {
            if (SaveManager.CurrentRun == null || player == null) return;
            SaveManager.CurrentRun.SetBool(DimensionPocketGrantKey(player.playerGuid), true);
        }

        private static string DimensionPocketGrantKey(string playerGuid) =>
            "SephiriaTogetherCatchUp_" + Hash(playerGuid) + "_DimensionPocketGranted";

        internal static void MarkCurrentAnvilClaimed(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            string floor = player?.PlayerAvatar?.currentFloorGuid;
            if (string.IsNullOrEmpty(floor)) return;
            bool removed = credits.PendingAnvilFloors.Remove(floor);
            if (!removed && !CurrentFloorMatches(player, EFloorMainEventType.Anvil)) return;
            string eventKey = EventKey(floor, EFloorMainEventType.Anvil);
            credits.CountedFloors.Add(eventKey);
            Plugin.LogInfo($"Original Anvil floor claimed: player={player?.PlayerAvatar?.Name}, " +
                           $"floor={ShortGuid(floor)}, pendingRemoved={removed}.");
            AddHistory(credits, "Original Anvil floor claimed @ " + ShortGuid(floor));
            player.SaveCurrentSessionData();
            Save(credits);
        }

        internal static int AvailableWeaponCredits(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            if (ClearWeaponCreditsIfMaxed(player, credits)) return 0;
            return Math.Max(0, credits.Weapons - credits.PendingWeapons);
        }

        internal static bool IsWeaponFullyEnhanced(PlayerSpawner player)
        {
            WeaponControllerSimple controller = player?.PlayerAvatar != null
                ? player.PlayerAvatar.GetComponent<WeaponControllerSimple>()
                : null;
            WeaponSimple weapon = controller != null ? controller.currentWeapon : null;
            if (weapon == null) return false;
            List<EnhancementMetadata> enhancements = WeaponDatabase.GetWeaponEnhancements(weapon.entityId);
            return enhancements == null || enhancements.Count == 0;
        }

        private static bool ClearWeaponCreditsIfMaxed(PlayerSpawner player, Credits credits)
        {
            if (!IsWeaponFullyEnhanced(player)) return false;
            int removedCredits = credits.Weapons;
            int removedFloors = credits.PendingAnvilFloors.Count;
            if (removedCredits == 0 && removedFloors == 0 && credits.PendingWeapons == 0) return true;
            credits.Weapons = 0;
            credits.PendingWeapons = 0;
            credits.PendingAnvilFloors.Clear();
            AddHistory(credits, $"Cleared weapon catch-up at max enhancement: credits {removedCredits}, floors {removedFloors}");
            Plugin.LogInfo($"Weapon catch-up cleared at max enhancement: player={player?.PlayerAvatar?.Name}, " +
                           $"removedCredits={removedCredits}, removedFloors={removedFloors}.");
            Save(credits);
            if (credits.ClientMod && player?.connectionToClient != null && player.connectionToClient.isReady)
                SendOffer(player.connectionToClient, credits, 0);
            return true;
        }

        internal static int ClaimedWeaponCredits(PlayerSpawner player) => GetServerCredits(player).WeaponClaimed;

        internal static void LockWeaponCredit(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            credits.PendingWeapons++;
            if (credits.ClientMod && player?.connectionToClient != null && player.connectionToClient.isReady)
                SendOffer(player.connectionToClient, credits, 0);
        }

        internal static void ReleaseWeaponCredit(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            credits.PendingWeapons = Math.Max(0, credits.PendingWeapons - 1);
        }

        internal static void CompleteWeaponCredit(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            credits.PendingWeapons = Math.Max(0, credits.PendingWeapons - 1);
            credits.Weapons = Math.Max(0, credits.Weapons - 1);
            credits.WeaponClaimed++;
            AddHistory(credits, "Claimed weapon upgrade from catch-up Anvil");
            player?.SaveCurrentSessionData();
            Save(credits);
            ClearWeaponCreditsIfMaxed(player, credits);
            if (credits.ClientMod && player?.connectionToClient != null && player.connectionToClient.isReady)
                SendOffer(player.connectionToClient, credits, 1);
        }

        internal static int AvailableEnchantCredits(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            return Math.Max(0, credits.Enchants - credits.PendingEnchants);
        }

        internal static void LockEnchantCredit(PlayerSpawner player) => GetServerCredits(player).PendingEnchants++;

        internal static void ReleaseEnchantCredit(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            credits.PendingEnchants = Math.Max(0, credits.PendingEnchants - 1);
        }

        internal static void CompleteEnchantCredit(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            credits.PendingEnchants = Math.Max(0, credits.PendingEnchants - 1);
            credits.Enchants = Math.Max(0, credits.Enchants - 1);
            credits.EnchantClaimed++;
            AddHistory(credits, "Claimed enchant from catch-up altar");
            player?.SaveCurrentSessionData();
            Save(credits);
        }

        internal static int AvailableMiracleCredits(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            return Math.Max(0, credits.Miracles - credits.PendingMiracles);
        }

        internal static int ClaimedMiracleCredits(PlayerSpawner player) => GetServerCredits(player).MiracleClaimed;
        internal static void LockMiracleCredit(PlayerSpawner player) => GetServerCredits(player).PendingMiracles++;

        internal static void ReleaseMiracleCredit(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            credits.PendingMiracles = Math.Max(0, credits.PendingMiracles - 1);
        }

        internal static void CompleteMiracleCredit(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            credits.PendingMiracles = Math.Max(0, credits.PendingMiracles - 1);
            credits.Miracles = Math.Max(0, credits.Miracles - 1);
            credits.MiracleClaimed++;
            credits.CapturedMiracles.Clear();
            AddHistory(credits, "Claimed Miracle from catch-up selector");
            player?.SaveCurrentSessionData();
            Save(credits);
        }

        internal static int AvailableFusionCredits(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            return Math.Max(0, credits.Fusions - credits.PendingFusions);
        }

        internal static void LockFusionCredit(PlayerSpawner player) => GetServerCredits(player).PendingFusions++;

        internal static void ReleaseFusionCredit(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            credits.PendingFusions = Math.Max(0, credits.PendingFusions - 1);
        }

        internal static void CompleteFusionCredit(PlayerSpawner player)
        {
            Credits credits = GetServerCredits(player);
            credits.PendingFusions = Math.Max(0, credits.PendingFusions - 1);
            credits.Fusions = Math.Max(0, credits.Fusions - 1);
            credits.FusionClaimed++;
            AddHistory(credits, "Used catch-up Tablet Fusion");
            player?.SaveCurrentSessionData();
            Save(credits);
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
            if (connection != null)
            {
                MoneyTransfer.RemoveConnection(connection);
                StartProgressSelection.RemoveConnection(connection);
                AnvilCompensation.RemoveConnection(connection);
                ChoiceRewardObjects.RemoveConnection(connection);
                bool hasPersonalFusion = connection.owned.Any(identity =>
                    identity != null && identity.GetComponent<TabletMix_Personal>() != null);
                FusionCompensation.RemoveConnection(connection);
                if (ServerCredits.TryGetValue(connection.connectionId, out Credits credits) &&
                    (credits.PendingAnvilFloors.Count > 0 || credits.PendingEnchantFloors.Count > 0 ||
                     credits.PendingMiracleFloors.Count > 0 || credits.PendingCharmFloors.Count > 0 ||
                     credits.PendingTabletFloors.Count > 0 || credits.PendingFusionFloors.Count > 0))
                {
                    foreach (string floor in credits.PendingAnvilFloors)
                    {
                        if (WasEventProcessed(credits, floor, EFloorMainEventType.Anvil)) continue;
                        credits.Weapons++;
                        credits.CountedFloors.Add(EventKey(floor, EFloorMainEventType.Anvil));
                        AddHistory(credits, "Catch-up: disconnected before claiming Anvil @ " + ShortGuid(floor));
                    }
                    if (credits.PendingAnvilFloors.Count > 0)
                        Plugin.LogInfo($"Converted {credits.PendingAnvilFloors.Count} cached unclaimed Anvil floor(s) on disconnect; " +
                                       $"weaponCredits={credits.Weapons}.");
                    credits.PendingAnvilFloors.Clear();
                    foreach (string floor in credits.PendingEnchantFloors)
                    {
                        if (WasEventProcessed(credits, floor, EFloorMainEventType.Enchant)) continue;
                        credits.Enchants++;
                        credits.CountedFloors.Add(EventKey(floor, EFloorMainEventType.Enchant));
                        AddHistory(credits, "Catch-up: disconnected before claiming Enchant @ " + ShortGuid(floor));
                    }
                    if (credits.PendingEnchantFloors.Count > 0)
                        Plugin.LogInfo($"Converted {credits.PendingEnchantFloors.Count} cached unclaimed Enchant floor(s) on disconnect; " +
                                       $"enchantCredits={credits.Enchants}.");
                    credits.PendingEnchantFloors.Clear();
                    ConvertChoiceSet(credits.PendingMiracleFloors, null, () => credits.Miracles++, "Miracle", credits);
                    ConvertChoiceSet(credits.PendingCharmFloors, null, () => credits.Charms++, "Charm", credits);
                    PlayerSpawner disconnected = connection.identity != null
                        ? connection.identity.GetComponent<PlayerSpawner>()
                        : null;
                    if (!hasPersonalFusion) ConvertPendingFusionSet(credits, null, disconnected);
                    ConvertPendingTabletChoices(credits, null, disconnected);
                    Save(credits);
                }
                ServerCredits.Remove(connection.connectionId);
                ModdedConnections.Remove(connection.connectionId);
            }
        }

        internal static void ClearClientState()
        {
            RescueAlerts.ClearClient();
            MoneyTransfer.ClearClient();
            VersionReminder.Clear();
            clientHelloSent = false;
            clientClaimPending = false;
            ClientWeaponCredits = 0;
            ClientEnchantCredits = 0;
            ClientMiracleCredits = 0;
            ClientTabletCredits = 0;
            ClientBossCredits = 0;
            ClientCharmCredits = 0;
            ClientFusionCredits = 0;
            ClientWeaponClaimed = 0;
            ClientEnchantClaimed = 0;
            ClientMiracleClaimed = 0;
            ClientTabletClaimed = 0;
            ClientBossClaimed = 0;
            ClientCharmClaimed = 0;
            ClientFusionClaimed = 0;
            ClientMiracleOptions = "";
            ClientLastResult = 0;
            ClientRules = "";
            ClientDiagnostics = "";
            ClientHistory = "";
        }

        internal static void ClearServerState()
        {
            RescueAlerts.ClearServer();
            MoneyTransfer.ClearServer();
            ServerCredits.Clear();
            PendingSephirites.Clear();
            BossRewardSessions.Clear();
            AnvilCompensation.Clear();
            ChoiceRewardObjects.Clear();
            FusionCompensation.Clear();
            PersonalizedVisibility.Clear();
        }

        internal static void ClearServerConnectionState()
        {
            ModdedConnections.Clear();
            ClearServerState();
        }

        internal static void RefreshExistingClientOffers()
        {
            if (!NetworkServer.active) return;
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values.ToArray())
            {
                if (connection == null || !connection.isReady || !ModdedConnections.Contains(connection.connectionId) ||
                    connection.identity == null) continue;
                PlayerSpawner spawner = connection.identity.GetComponent<PlayerSpawner>();
                Credits credits = Load(spawner?.playerGuid);
                credits.ClientMod = true;
                ServerCredits[connection.connectionId] = credits;
                SendOffer(connection, credits, 0);
            }
        }

        internal static void ScheduleExistingClientOffersRefresh()
        {
            if (Plugin.InstanceForPatches != null)
                Plugin.InstanceForPatches.StartCoroutine(RefreshExistingClientOffersWhenReady());
        }

        private static IEnumerator RefreshExistingClientOffersWhenReady()
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (NetworkServer.active && Time.realtimeSinceStartup < deadline)
            {
                RefreshExistingClientOffers();
                yield return new WaitForSeconds(0.25f);
            }
        }

        private static void OnServerHello(NetworkConnectionToClient connection, CatchUpHelloMessage message)
        {
            PlayerSpawner spawner = connection.identity != null
                ? connection.identity.GetComponent<PlayerSpawner>()
                : null;
            string expectedPrefix = string.IsNullOrEmpty(spawner?.playerGuid)
                ? null
                : "SephiriaTogetherCatchUp_" + Hash(spawner.playerGuid) + "_";
            if (!ServerCredits.TryGetValue(connection.connectionId, out Credits credits) ||
                (expectedPrefix != null && credits.SavePrefix != expectedPrefix))
            {
                ServerCredits[connection.connectionId] = credits = Load(spawner?.playerGuid);
            }
            else if (MigrateLegacyProcessedFloors(credits)) Save(credits);
            credits.ClientMod = true;
            ModdedConnections.Add(connection.connectionId);
            SendOffer(connection, credits, 0);
        }

        private static void OnServerClaim(NetworkConnectionToClient connection, CatchUpClaimMessage message)
        {
            if (!ServerCredits.TryGetValue(connection.connectionId, out Credits credits) ||
                !credits.ClientMod || connection.identity == null) return;
            PlayerAvatar player = connection.identity.GetComponent<PlayerAvatar>();
            if (player == null) return;
            bool claimed = false;

            if (message.rewardType == 2 && credits.Enchants > 0 && player.Inventory != null)
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
                weaponCredits = Math.Max(0, credits.Weapons - credits.PendingWeapons),
                enchantCredits = Math.Max(0, credits.Enchants - credits.PendingEnchants),
                miracleCredits = Math.Max(0, credits.Miracles - credits.PendingMiracles),
                tabletCredits = Math.Max(0, credits.Tablets - credits.PendingTablets),
                bossCredits = Math.Max(0, credits.Bosses - credits.PendingBosses),
                charmCredits = Math.Max(0, credits.Charms - credits.PendingCharms),
                fusionCredits = Math.Max(0, credits.Fusions - credits.PendingFusions),
                weaponClaimed = credits.WeaponClaimed,
                enchantClaimed = credits.EnchantClaimed,
                miracleClaimed = credits.MiracleClaimed,
                tabletClaimed = credits.TabletClaimed,
                bossClaimed = credits.BossClaimed,
                charmClaimed = credits.CharmClaimed,
                fusionClaimed = credits.FusionClaimed,
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
            ClientFusionCredits = Math.Max(0, message.fusionCredits);
            ClientWeaponClaimed = Math.Max(0, message.weaponClaimed);
            ClientEnchantClaimed = Math.Max(0, message.enchantClaimed);
            ClientMiracleClaimed = Math.Max(0, message.miracleClaimed);
            ClientTabletClaimed = Math.Max(0, message.tabletClaimed);
            ClientBossClaimed = Math.Max(0, message.bossClaimed);
            ClientCharmClaimed = Math.Max(0, message.charmClaimed);
            ClientFusionClaimed = Math.Max(0, message.fusionClaimed);
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
            Credits active = ServerCredits.Values
                .FirstOrDefault(candidate => candidate != null && candidate.SavePrefix == prefix) ??
                PendingSephirites.Values
                .Select(pending => pending.Credits)
                .FirstOrDefault(candidate => candidate != null && candidate.SavePrefix == prefix);
            if (active != null)
            {
                if (MigrateLegacyProcessedFloors(active)) Save(active);
                return active;
            }
            credits.SavePrefix = prefix;
            credits.Weapons = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "WeaponPending", 0));
            credits.Enchants = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "EnchantPending", 0));
            credits.Miracles = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "MiraclePending", 0));
            credits.Tablets = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "TabletPending", 0));
            credits.Bosses = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "BossPending", 0));
            credits.Charms = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "CharmPending", 0));
            credits.Fusions = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "FusionPending", 0));
            credits.WeaponClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "WeaponClaimed", 0));
            credits.EnchantClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "EnchantClaimed", 0));
            credits.MiracleClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "MiracleClaimed", 0));
            credits.TabletClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "TabletClaimed", 0));
            credits.BossClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "BossClaimed", 0));
            credits.CharmClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "CharmClaimed", 0));
            credits.FusionClaimed = Math.Max(0, SaveManager.CurrentRun.GetInt(credits.SavePrefix + "FusionClaimed", 0));
            string floors = SaveManager.CurrentRun.GetString(credits.SavePrefix + "CountedFloors", "");
            foreach (string floor in floors.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                credits.CountedFloors.Add(floor);
            string pendingAnvils = SaveManager.CurrentRun.GetString(credits.SavePrefix + "PendingAnvils", "");
            foreach (string floor in pendingAnvils.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                credits.PendingAnvilFloors.Add(floor);
            string pendingEnchants = SaveManager.CurrentRun.GetString(credits.SavePrefix + "PendingEnchants", "");
            foreach (string floor in pendingEnchants.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                credits.PendingEnchantFloors.Add(floor);
            LoadFloorSet(credits.PendingMiracleFloors, credits.SavePrefix + "PendingMiracles");
            LoadFloorSet(credits.PendingCharmFloors, credits.SavePrefix + "PendingCharms");
            LoadFloorSet(credits.PendingTabletFloors, credits.SavePrefix + "PendingTablets");
            LoadFloorSet(credits.PendingFusionFloors, credits.SavePrefix + "PendingFusions");
            string history = SaveManager.CurrentRun.GetString(credits.SavePrefix + "History", "");
            credits.History.AddRange(history.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            credits.CapturedMiracles.AddRange(SaveManager.CurrentRun.GetString(credits.SavePrefix + "MiracleOffers", "")
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            credits.CapturedBossRewards.AddRange(SaveManager.CurrentRun.GetString(credits.SavePrefix + "BossOffers", "")
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            if (MigrateLegacyProcessedFloors(credits)) Save(credits);
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
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "FusionPending", credits.Fusions);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "WeaponClaimed", credits.WeaponClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "EnchantClaimed", credits.EnchantClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "MiracleClaimed", credits.MiracleClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "TabletClaimed", credits.TabletClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "BossClaimed", credits.BossClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "CharmClaimed", credits.CharmClaimed);
            SaveManager.CurrentRun.SetInt(credits.SavePrefix + "FusionClaimed", credits.FusionClaimed);
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "CountedFloors", string.Join("|", credits.CountedFloors));
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "PendingAnvils", string.Join("|", credits.PendingAnvilFloors));
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "PendingEnchants", string.Join("|", credits.PendingEnchantFloors));
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "PendingMiracles", string.Join("|", credits.PendingMiracleFloors));
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "PendingCharms", string.Join("|", credits.PendingCharmFloors));
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "PendingTablets", string.Join("|", credits.PendingTabletFloors));
            SaveManager.CurrentRun.SetString(credits.SavePrefix + "PendingFusions", string.Join("|", credits.PendingFusionFloors));
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

        private static string EventKey(string floorGuid, EFloorMainEventType type) =>
            EventKey(floorGuid, type.ToString());

        private static string EventKey(string floorGuid, string type) =>
            string.IsNullOrEmpty(floorGuid) ? "" : floorGuid + ":" + type;

        private static bool WasEventProcessed(Credits credits, string floorGuid, EFloorMainEventType type) =>
            WasEventProcessed(credits, floorGuid, type.ToString());

        private static bool WasEventProcessed(Credits credits, string floorGuid, string type) =>
            credits != null && !string.IsNullOrEmpty(floorGuid) &&
            (credits.CountedFloors.Contains(EventKey(floorGuid, type)) ||
             Enum.TryParse(type, out EFloorMainEventType _) && credits.CountedFloors.Contains(floorGuid));

        private static bool WasFusionProcessed(Credits credits, string floorGuid) =>
            credits != null && !string.IsNullOrEmpty(floorGuid) &&
            (credits.CountedFloors.Contains(EventKey(floorGuid, "Fusion")) ||
             credits.CountedFloors.Contains(TabletFusionChoiceKey(floorGuid)) ||
             IsTabletFusionChoiceFloor(floorGuid) &&
             WasEventProcessed(credits, floorGuid, EFloorMainEventType.StoneTablet));

        private static bool MigrateLegacyProcessedFloors(Credits credits)
        {
            if (credits == null || DungeonManager.Instance == null || credits.History.Count == 0) return false;
            bool changed = false;
            HashSet<string> resolvedEvents = new HashSet<string>();
            foreach (string line in credits.History.AsEnumerable().Reverse())
            {
                if (string.IsNullOrEmpty(line)) continue;
                int marker = line.LastIndexOf(" @ ", StringComparison.Ordinal);
                if (marker < 0) continue;
                string shortGuid = line.Substring(marker + 3).Trim();
                string[] matchingFloors = DungeonManager.Instance.generatedFloors.Keys
                    .Where(guid => string.Equals(ShortGuid(guid), shortGuid, StringComparison.OrdinalIgnoreCase))
                    .Take(2)
                    .ToArray();
                if (matchingFloors.Length != 1) continue;
                string floorGuid = matchingFloors[0];
                if (string.IsNullOrEmpty(floorGuid)) continue;

                if (line.IndexOf("Removed current-floor catch-up:", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (EFloorMainEventType type in Enum.GetValues(typeof(EFloorMainEventType)))
                        if (line.IndexOf("Removed current-floor catch-up: " + type,
                                StringComparison.OrdinalIgnoreCase) >= 0)
                            resolvedEvents.Add(EventKey(floorGuid, type));
                    continue;
                }

                if (line.IndexOf("Catch-up: Boss choices", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Original Boss choices completed", StringComparison.OrdinalIgnoreCase) >= 0)
                    changed |= AddLegacyEventMarker(credits, resolvedEvents, floorGuid, "Boss");
                if (line.IndexOf("unclaimed Anvil", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("disconnected before claiming Anvil", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Original Anvil floor claimed", StringComparison.OrdinalIgnoreCase) >= 0)
                    changed |= AddLegacyEventMarker(credits, resolvedEvents, floorGuid, EFloorMainEventType.Anvil);
                if (line.IndexOf("unclaimed Enchant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("disconnected before claiming Enchant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Original Enchant floor claimed", StringComparison.OrdinalIgnoreCase) >= 0)
                    changed |= AddLegacyEventMarker(credits, resolvedEvents, floorGuid, EFloorMainEventType.Enchant);
                if (line.IndexOf("unclaimed Miracle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Original Miracle floor claimed", StringComparison.OrdinalIgnoreCase) >= 0)
                    changed |= AddLegacyEventMarker(credits, resolvedEvents, floorGuid, EFloorMainEventType.Miracle);
                if (line.IndexOf("unclaimed Charm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Original Charm floor claimed", StringComparison.OrdinalIgnoreCase) >= 0)
                    changed |= AddLegacyEventMarker(credits, resolvedEvents, floorGuid, EFloorMainEventType.Charm);
                if (line.IndexOf("unclaimed StoneTablet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("-> StoneTablet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Original StoneTablet floor claimed", StringComparison.OrdinalIgnoreCase) >= 0)
                    changed |= AddLegacyEventMarker(credits, resolvedEvents, floorGuid, EFloorMainEventType.StoneTablet);
                if (line.IndexOf("Tablet/Fusion choice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("StoneTablet/Fusion choice", StringComparison.OrdinalIgnoreCase) >= 0)
                    changed |= credits.CountedFloors.Add(TabletFusionChoiceKey(floorGuid));
                else if (line.IndexOf("unclaimed Tablet Fusion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         line.IndexOf("no personal Tablet Fusion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         line.IndexOf("Original Tablet Fusion used", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         line.IndexOf("Catch-up: Tablet Fusion", StringComparison.OrdinalIgnoreCase) >= 0)
                    changed |= AddLegacyEventMarker(credits, resolvedEvents, floorGuid, "Fusion");
                if (line.IndexOf("Catch-up: ", StringComparison.OrdinalIgnoreCase) >= 0)
                    foreach (EFloorMainEventType type in Enum.GetValues(typeof(EFloorMainEventType)))
                        if (line.IndexOf("Catch-up: " + type + " @ ", StringComparison.OrdinalIgnoreCase) >= 0)
                            changed |= AddLegacyEventMarker(credits, resolvedEvents, floorGuid, type);
            }
            if (changed) Plugin.LogInfo("Migrated legacy compensation floor markers from saved history.");
            return changed;
        }

        private static bool AddLegacyEventMarker(Credits credits, HashSet<string> resolvedEvents,
            string floorGuid, EFloorMainEventType type) =>
            AddLegacyEventMarker(credits, resolvedEvents, floorGuid, type.ToString());

        private static bool AddLegacyEventMarker(Credits credits, HashSet<string> resolvedEvents,
            string floorGuid, string type)
        {
            string key = EventKey(floorGuid, type);
            if (!resolvedEvents.Add(key)) return false;
            return credits.CountedFloors.Add(key);
        }

        private static bool IsLegacyRemovedFloor(Credits credits, string floorGuid, EFloorMainEventType type)
        {
            if (credits == null || string.IsNullOrEmpty(floorGuid)) return false;
            if (type == EFloorMainEventType.Anvil || type == EFloorMainEventType.Enchant ||
                type == EFloorMainEventType.Miracle || type == EFloorMainEventType.Charm ||
                type == EFloorMainEventType.StoneTablet)
                return false;
            string shortGuid = ShortGuid(floorGuid);
            return credits.History.Any(line =>
                !string.IsNullOrEmpty(line) &&
                line.IndexOf("Removed current-floor catch-up: " + type, StringComparison.OrdinalIgnoreCase) >= 0 &&
                line.EndsWith(" @ " + shortGuid, StringComparison.OrdinalIgnoreCase));
        }

        private static bool CurrentFloorMatches(PlayerSpawner player, EFloorMainEventType type)
        {
            string floorGuid = player?.PlayerAvatar?.currentFloorGuid;
            if (string.IsNullOrEmpty(floorGuid)) return false;
            if (DungeonManager.Instance != null &&
                DungeonManager.Instance.generatedFloors.TryGetValue(floorGuid, out FloorData data))
                return data.mainEventType == type;
            FloorGenerator generator = FloorGenerator.FindByGuid(floorGuid);
            return generator != null && generator.floorMainEventType == type;
        }

        private static void LoadFloorSet(HashSet<string> target, string key)
        {
            foreach (string floor in SaveManager.CurrentRun.GetString(key, "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                target.Add(floor);
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
            byte rewardType,
            int group = 0)
        {
            if (player == null || player.spawner == null) return false;
            GameObject prefab = Resources.Load<GameObject>("Sephirite/" + prefabName);
            if (prefab == null) return false;
            Vector3 offset = rewardType == 6 ? new Vector3(-1.5f, -1.5f)
                : rewardType == 4 ? new Vector3(1.5f, -1.5f)
                : prefabName == "Sephirite_Huge" ? new Vector3(-1.5f, 1.5f)
                : new Vector3(1.5f, 1.5f);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, player.transform.position + offset, Quaternion.identity);
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
                RewardType = rewardType,
                Group = group
            };
            Plugin.LogInfo($"Catch-up Sephirite spawned: player={player.Name}, prefab={prefabName}, " +
                           $"rewardType={rewardType}, netId={sephirite.netId}, floor={ShortGuid(player.currentFloorGuid)}.");
            return true;
        }

        internal static void CompleteSephirite(PlayerAvatar player, Sephirite sephirite)
        {
            if (sephirite == null) return;
            if (!PendingSephirites.TryGetValue(sephirite.netId, out PendingSephirite pending))
            {
                if (sephirite.type == Sephirite.Type.CHARM)
                    MarkCurrentChoiceClaimed(player?.spawner, EFloorMainEventType.Charm);
                else if (sephirite.type == Sephirite.Type.TABLET ||
                         sephirite.type == Sephirite.Type.TABLET_BOSS)
                    MarkCurrentChoiceClaimed(player?.spawner, EFloorMainEventType.StoneTablet);
                return;
            }
            PendingSephirites.Remove(sephirite.netId);
            Credits credits = pending.Credits;
            if (pending.Group != 0)
            {
                foreach (uint sibling in PendingSephirites
                    .Where(entry => entry.Value.Credits == credits && entry.Value.Group == pending.Group)
                    .Select(entry => entry.Key).ToArray())
                {
                    PendingSephirites.Remove(sibling);
                    if (NetworkServer.spawned.TryGetValue(sibling, out NetworkIdentity identity) && identity != null)
                        NetworkServer.Destroy(identity.gameObject);
                }
            }
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
            AutoSpawnRewardObjects(player?.spawner);
        }

        internal static void ReleaseSephirite(Sephirite sephirite)
        {
            if (sephirite == null || !PendingSephirites.TryGetValue(sephirite.netId, out PendingSephirite pending)) return;
            PendingSephirites.Remove(sephirite.netId);
            if (pending.Group != 0)
            {
                foreach (uint sibling in PendingSephirites
                    .Where(entry => entry.Value.Credits == pending.Credits && entry.Value.Group == pending.Group)
                    .Select(entry => entry.Key).ToArray())
                    PendingSephirites.Remove(sibling);
            }
            if (pending.RewardType == 4) pending.Credits.PendingTablets--;
            else if (pending.RewardType == 5) pending.Credits.PendingBosses--;
            else if (pending.RewardType == 6) pending.Credits.PendingCharms--;
            if (pending.Credits.ClientMod && pending.Connection != null && pending.Connection.isReady)
                SendOffer(pending.Connection, pending.Credits, 0);
            PlayerSpawner player = pending.Connection?.identity != null
                ? pending.Connection.identity.GetComponent<PlayerSpawner>()
                : null;
            if (player != null) ScheduleRewardObjects(player);
        }

        internal static void AutoSpawnRewardObjects(PlayerSpawner player)
        {
            if (!CanSpawnCompensation(player)) return;
            Credits credits = GetServerCredits(player);
            if (credits.Charms > credits.PendingCharms &&
                SpawnSephirite(player.PlayerAvatar, "Sephirite_Charm", credits.CharmClaimed,
                    credits, player.connectionToClient, 6))
                credits.PendingCharms++;
            if (credits.Tablets > credits.PendingTablets &&
                SpawnSephirite(player.PlayerAvatar, "Sephirite_Tablet", credits.TabletClaimed,
                    credits, player.connectionToClient, 4))
                credits.PendingTablets++;
            if (credits.Bosses > credits.PendingBosses)
            {
                int group = ++nextRewardGroup;
                bool huge = SpawnSephirite(player.PlayerAvatar, "Sephirite_Huge", credits.BossClaimed,
                    credits, player.connectionToClient, 5, group);
                bool tablet = SpawnSephirite(player.PlayerAvatar, "Sephirite_Tablet", credits.BossClaimed + 10000,
                    credits, player.connectionToClient, 5, group);
                if (huge || tablet) credits.PendingBosses++;
            }
        }

        internal static bool CanSpawnCompensation(PlayerSpawner player)
        {
            PlayerAvatar avatar = player?.PlayerAvatar;
            HorayNetworkManager manager = NetworkManager.singleton as HorayNetworkManager;
            return NetworkServer.active && avatar != null && !CloneBotManager.IsBot(player) &&
                   player.connectionToClient != null && !avatar.IsDead &&
                   !player.isHost && player.connectionToClient != NetworkServer.localConnection &&
                   avatar.isInDungeon > 0 && !string.IsNullOrEmpty(avatar.currentFloorGuid) &&
                   FloorGenerator.FindByGuid(avatar.currentFloorGuid) != null &&
                   (DungeonManager.Instance == null || !DungeonManager.Instance.isGiveUpRun) &&
                   (manager == null || !manager.selfLeaveToGameOver);
        }

        internal static void ScheduleRewardObjects(PlayerSpawner player)
        {
            if (Plugin.InstanceForPatches != null && player != null)
                Plugin.InstanceForPatches.StartCoroutine(SpawnRewardObjectsAfterTravel(player));
        }

        private static System.Collections.IEnumerator SpawnRewardObjectsAfterTravel(PlayerSpawner player)
        {
            yield return new WaitForSeconds(1f);
            AutoSpawnRewardObjects(player);
            ChoiceRewardObjects.SpawnPending(player);
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
            connection != null && (ModdedConnections.Contains(connection.connectionId) ||
                ServerCredits.TryGetValue(connection.connectionId, out Credits credits) && credits.ClientMod);

        private static string BuildRules()
        {
            return $"Sephiria Together {Plugin.PluginVersion}\n" +
                   string.Format(MenuText.Get("RuleGameVersion"), Application.version) + "\n" +
                   string.Format(MenuText.Get("RuleMidRunJoin"), OnOff(Plugin.allowMidRunJoin.Value)) + "\n" +
                   string.Format(MenuText.Get("RuleLowerProgress"), OnOff(Plugin.allowLowerProgressPlayers.Value)) + "\n" +
                   string.Format(MenuText.Get("RuleUngrouped"), OnOff(Plugin.allowUngroupedStageTransition.Value)) + "\n" +
                   string.Format(MenuText.Get("RuleFriendlyFire"), OnOff(Plugin.friendlyFire.Value)) + "\n" +
                   string.Format(MenuText.Get("RuleHealing"), OnOff(Plugin.breathingHeal.Value)) + "\n" +
                    string.Format(MenuText.Get("RuleAutoRevive"), OnOff(Plugin.autoReviveWhenClear.Value)) + "\n" +
                    string.Format(MenuText.Get("RuleExpCatchup"), Plugin.catchUpExperienceRatio.Value.ToString("P0")) + "\n" +
                    string.Format(MenuText.Get("RuleEnemyBase"), Plugin.BaseEnemyMultiplierValue.ToString("0.##") + "x") + "\n" +
                    string.Format(MenuText.Get("RuleEnemyHp"), Plugin.BaselinePlayersValue,
                       Plugin.HealthPerExtraPlayerValue.ToString("P0"), Plugin.MaximumMultiplierValue > 0f
                           ? Plugin.MaximumMultiplierValue.ToString("0.##") + "x"
                           : MenuText.Get("NoLimit")) + "\n" +
                   string.Format(MenuText.Get("RuleEnemyCount"), OnOff(Plugin.scaleEnemyCount.Value),
                       Plugin.EnemyCountPerExtraPlayerValue.ToString("P0"),
                       Plugin.MaximumEnemyCountMultiplierValue.ToString("0.##")) + "\n" +
                   string.Format(MenuText.Get("RuleBossLifesteal"), OnOff(Plugin.bossLifesteal.Value)) + "\n" +
                   BuildOriginalScalingSummary() + "\n" +
                   string.Format(MenuText.Get("RulePlayerLimit"), PlayerLimit.CurrentLimit);
        }

        internal static string BuildHostDiagnostics(NetworkConnectionToClient connection)
        {
            PlayerSpawner player = connection?.identity != null ? connection.identity.GetComponent<PlayerSpawner>() : null;
            string floor = player?.PlayerAvatar?.currentFloorGuid ?? "-";
            string identity = !string.IsNullOrEmpty(player?.playerGuid) ? Hash(player.playerGuid) : "-";
            return string.Format(MenuText.Get("DiagnosticProtocol"), Plugin.PluginVersion) + "\n" +
                   string.Format(MenuText.Get("RuleGameVersion"), Application.version) + "\n" +
                   string.Format(MenuText.Get("DiagnosticServer"), OnOff(NetworkServer.active)) + "\n" +
                   string.Format(MenuText.Get("DiagnosticClient"), OnOff(NetworkClient.active)) + "\n" +
                   string.Format(MenuText.Get("DiagnosticHandshake"), connection != null ? OnOff(true) : MenuText.Get("Host")) + "\n" +
                   string.Format(MenuText.Get("DiagnosticConnection"), connection != null ? connection.connectionId.ToString() : "-") + "\n" +
                   string.Format(MenuText.Get("DiagnosticPlayer"), identity) + "\n" +
                   string.Format(MenuText.Get("DiagnosticFloor"), FloorDisplay.Format(floor)) + "\n" +
                   string.Format(MenuText.Get("DiagnosticPlayers"), CloneBotManager.RealPlayerCount) + "\n" +
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
                int bloodFestival = GetHardModeValue("BLOODFESTIVAL");
                int bossHeal = KeywordDatabase.GetConstValue("hardModeBloodFestivalHealBossAndMiniboss");
                int players = CloneBotManager.RealPlayerCount;
                float partyScale = players == 2 ? 0.66f : players == 3 ? 0.5f : players == 4 ? 0.33f : players >= 5 ? 0.25f : 1f;
                return string.Format(MenuText.Get("VanillaHpSummary"), normal, miniboss, boss) + "\n" +
                       string.Format(MenuText.Get("HardModeSummary"), hardPoints,
                           HardModeName("TENACIOUSBODY"), tenacious,
                           HardModeName("FEROCIOUSCLAWS"), ferocious) + "\n" +
                       string.Format(MenuText.Get("BloodFestivalSummary"), HardModeName("BLOODFESTIVAL"),
                           bloodFestival, bossHeal, (bossHeal * bloodFestival * partyScale).ToString("0.##"));
            }
            catch (Exception)
            {
                return MenuText.Get("ScalingDataUnavailable");
            }
        }

        private static int GetHardModeValue(string key)
        {
            return DungeonManager.Instance != null && DungeonManager.Instance.hardModeEnvironment.TryGetValue(key, out int value)
                ? value
                : 0;
        }

        private static string HardModeName(string key)
        {
            try
            {
                HardModeShardEntity shard = HardModeDatebase.Find(key);
                string name = shard != null ? shard.aName.ToString() : null;
                return string.IsNullOrWhiteSpace(name) ? HardModeFallback(key) : name;
            }
            catch
            {
                return HardModeFallback(key);
            }
        }

        private static string HardModeFallback(string key)
        {
            if (key == "TENACIOUSBODY") return MenuText.Get("HardModeTenacious");
            if (key == "FEROCIOUSCLAWS") return MenuText.Get("HardModeFerocious");
            if (key == "BLOODFESTIVAL") return MenuText.Get("HardModeBloodFestival");
            return "-";
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

        private static string OnOff(bool value) => MenuText.Get(value ? "ToggleOn" : "ToggleOff");

        internal static void CaptureMiracles(MiracleController controller, MiracleMetadata[] candidates)
        {
            if (!NetworkServer.active || controller?.UnitAvatar == null || candidates == null) return;
            PlayerSpawner spawner = controller.UnitAvatar.GetComponent<PlayerSpawner>();
            if (CloneBotManager.IsBot(spawner)) return;
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
                    item != null && !CloneBotManager.IsBot(item) &&
                    item.currentPlayerIdxForSave == reward.playerIndex);
                if (player == null) continue;
                Credits credits = GetServerCredits(player);
                if (!string.IsNullOrEmpty(reward.rewardName) && !credits.CapturedBossRewards.Contains(reward.rewardName))
                    credits.CapturedBossRewards.Add(reward.rewardName);
                Save(credits);
            }
        }

        internal static void TrackBossRewardSession(BossRewardSpawner spawner)
        {
            if (!NetworkServer.active || spawner == null) return;
            FloorGenerator floor = spawner.GetComponentInParent<FloorGenerator>();
            BossRewardSession session = new BossRewardSession { FloorGuid = floor != null ? floor.guid : "" };
            foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
                if (ShouldTrackUnclaimedFloor(player) && !string.IsNullOrEmpty(player.playerGuid))
                    session.PlayerSlots[player.playerGuid] = player.currentPlayerIdxForSave;
            BossRewardSessions[spawner.netId] = session;
            Plugin.LogInfo($"Boss reward session tracked: floor={ShortGuid(session.FloorGuid)}, players={session.PlayerSlots.Count}.");
        }

        internal static void FinalizeBossRewardSession(BossRewardSpawner spawner)
        {
            if (!NetworkServer.active || spawner == null || !BossRewardSessions.TryGetValue(spawner.netId, out BossRewardSession session)) return;
            BossRewardSessions.Remove(spawner.netId);
            HorayNetworkManager manager = NetworkManager.singleton as HorayNetworkManager;
            bool partyWiped = PlayerSpawner.MultiplayerList != null && PlayerSpawner.MultiplayerList
                .Where(player => player?.PlayerAvatar != null && !CloneBotManager.IsBot(player) &&
                                 player.PlayerAvatar.isInDungeon > 0)
                .All(player => player.PlayerAvatar.IsDead);
            if ((DungeonManager.Instance != null && DungeonManager.Instance.isGiveUpRun) ||
                (manager != null && manager.selfLeaveToGameOver) || partyWiped)
            {
                Plugin.LogInfo($"Boss reward remainder discarded at Game Over: floor={ShortGuid(session.FloorGuid)}, wiped={partyWiped}.");
                return;
            }
            foreach (KeyValuePair<string, int> player in session.PlayerSlots)
            {
                int selected = spawner.acquiredRewardEachPlayer.TryGetValue(player.Value, out int count) ? count : 0;
                int remaining = Mathf.Clamp(2 - selected, 0, 2);
                Credits credits = Load(player.Key);
                string bossKey = session.FloorGuid + ":Boss";
                if (!string.IsNullOrEmpty(session.FloorGuid) && credits.CountedFloors.Contains(bossKey)) continue;
                if (remaining > 0) credits.Bosses += remaining;
                if (!string.IsNullOrEmpty(session.FloorGuid)) credits.CountedFloors.Add(bossKey);
                AddHistory(credits, remaining > 0
                    ? $"Catch-up: unclaimed Boss choices +{remaining} @ {ShortGuid(session.FloorGuid)}"
                    : $"Original Boss choices completed @ {ShortGuid(session.FloorGuid)}");
                Save(credits);
                Plugin.LogInfo($"Boss reward remainder converted: player={ShortGuid(player.Key)}, floor={ShortGuid(session.FloorGuid)}, " +
                               $"selected={selected}, granted={remaining}.");
            }
        }

        private static Credits GetServerCredits(PlayerSpawner spawner)
        {
            string expectedPrefix = string.IsNullOrWhiteSpace(spawner?.playerGuid)
                ? null
                : "SephiriaTogetherCatchUp_" + Hash(spawner.playerGuid) + "_";
            if (spawner?.connectionToClient != null &&
                ServerCredits.TryGetValue(spawner.connectionToClient.connectionId, out Credits credits) &&
                expectedPrefix != null && credits.SavePrefix == expectedPrefix)
            {
                if (MigrateLegacyProcessedFloors(credits)) Save(credits);
                return credits;
            }
            credits = Load(spawner?.playerGuid);
            if (spawner?.connectionToClient != null)
            {
                credits.ClientMod = ModdedConnections.Contains(spawner.connectionToClient.connectionId);
                ServerCredits[spawner.connectionToClient.connectionId] = credits;
            }
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
        private static void Postfix() => CatchUpRewards.ClearServerConnectionState();
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
        private static void Postfix(BossRewardSpawner __instance)
        {
            CatchUpRewards.CaptureBossRewards(__instance);
            CatchUpRewards.TrackBossRewardSession(__instance);
        }
    }

    [HarmonyPatch(typeof(BossRewardSpawner), "OnDestroy")]
    internal static class CatchUpBossFinalizePatch
    {
        private static void Prefix(BossRewardSpawner __instance) => CatchUpRewards.FinalizeBossRewardSession(__instance);
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.NewGame))]
    internal static class CatchUpNewRunCleanupPatch
    {
        private static void Prefix() => CatchUpRewards.ClearServerState();

        private static void Postfix()
        {
            StartProgressSelection.Clear();
            CatchUpRewards.ScheduleExistingClientOffersRefresh();
        }
    }
}
