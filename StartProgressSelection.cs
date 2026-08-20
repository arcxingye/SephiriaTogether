using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal struct StartProgressReportMessage : NetworkMessage
    {
        public int progress;
        public int raceId;
        public int chapter;
        public int subChapter;
        public string[] currentNodeIds;
    }

    internal static class StartProgressSelection
    {
        private sealed class Report
        {
            internal int Progress;
            internal int RaceId;
            internal int Chapter;
            internal int SubChapter;
        }

        private static readonly Dictionary<int, Report> Reports = new Dictionary<int, Report>();
        private static string selectedPlayerGuid;
        private static int selectedConnectionId = int.MinValue;
        private static string status;
        private static float nextReportAt;

        internal static string Status => status;
        internal static bool CanSelect => NetworkServer.active && DungeonManager.Instance != null &&
            !DungeonManager.Instance.isRunStarted && SaveManager.CurrentRun != null &&
            !SaveManager.CurrentRun.GetBool("RunStarted", false);
        internal static bool CanApplySelected => CanSelect &&
            IsUsableCandidate(GetCandidates().FirstOrDefault(IsSelected));
        internal static bool CanSelectPlayer(PlayerSpawner player) => CanSelect && IsUsableCandidate(player);

        internal static void RegisterServerMessages()
        {
            ConfigureSerialization();
            NetworkServer.RegisterHandler<StartProgressReportMessage>(OnServerReport, true);
        }

        internal static void RegisterClientMessages()
        {
            ConfigureSerialization();
        }

        internal static void SendLocalReport(PlayerLocalDataStorage storage)
        {
            if (storage != null && Plugin.InstanceForPatches != null)
                Plugin.InstanceForPatches.StartCoroutine(SendWhenReady(storage));
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < nextReportAt) return;
            nextReportAt = Time.unscaledTime + 3f;
            if (DungeonManager.Instance == null || DungeonManager.Instance.isRunStarted ||
                SaveManager.CurrentRun == null || SaveManager.CurrentRun.GetBool("RunStarted", false)) return;
            PlayerLocalDataStorage storage = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.GetComponent<PlayerLocalDataStorage>()
                : null;
            if (storage != null && NetworkClient.active && NetworkClient.ready &&
                CatchUpRewards.HostSupportsProtocol()) SendReport(storage);
        }

        internal static List<PlayerSpawner> GetCandidates()
        {
            return (PlayerSpawner.MultiplayerList ?? new List<PlayerSpawner>())
                .Where(player => player != null && !CloneBotManager.IsBot(player) &&
                                 player.PlayerAvatar != null && player.PlayerAvatar.localDataStorage != null)
                .OrderBy(Progress)
                .ThenBy(Chapter)
                .ThenBy(SubChapter)
                .ThenBy(player => player.PlayerAvatar.Name ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(player => player.currentPlayerIdxForSave)
                .ToList();
        }

        internal static bool IsSelected(PlayerSpawner player) =>
            player != null && !string.IsNullOrEmpty(selectedPlayerGuid) &&
            string.Equals(selectedPlayerGuid, SelectionKey(player), StringComparison.Ordinal);

        internal static void Select(PlayerSpawner player)
        {
            if (!CanSelectPlayer(player) || !GetCandidates().Contains(player)) return;
            selectedPlayerGuid = SelectionKey(player);
            selectedConnectionId = player.connectionToClient?.connectionId ?? int.MinValue;
            status = string.Format(MenuText.Get("StartProgressSelected"), player.PlayerAvatar.Name,
                Progress(player));
            Plugin.LogInfo($"Start progress selected: player={player.PlayerAvatar.Name}, progress={Progress(player)}, " +
                           $"race={RaceId(player)}, chapter={Chapter(player)}-{SubChapter(player)}.");
        }

        internal static string Describe(PlayerSpawner player)
        {
            if (player?.PlayerAvatar == null) return MenuText.Get("UnknownPlayer");
            if (!HasReport(player) && player.connectionToClient != NetworkServer.localConnection)
                return string.Format(MenuText.Get("StartProgressAwaiting"), player.PlayerAvatar.Name,
                    "-");
            if (!IsUsableCandidate(player))
                return string.Format(MenuText.Get("StartProgressAwaiting"), player.PlayerAvatar.Name, Progress(player));
            string chapter = SubChapter(player) > 0
                ? Chapter(player) + "-" + SubChapter(player)
                : Chapter(player).ToString();
            return string.Format(MenuText.Get("StartProgressEntry"), player.PlayerAvatar.Name,
                Progress(player), chapter);
        }

        internal static bool ApplySelected()
        {
            if (!CanSelect)
            {
                status = MenuText.Get("StartProgressUnavailable");
                return false;
            }
            PlayerSpawner player = GetCandidates().FirstOrDefault(IsSelected);
            int raceId = RaceId(player);
            RaceEntity race = RaceDatabase.FindById(raceId);
            HorayNetworkManager manager = NetworkManager.singleton as HorayNetworkManager;
            if (player == null || race == null || manager == null)
            {
                status = MenuText.Get("StartProgressUnavailable");
                return false;
            }

            if (SaveManager.CurrentRun.GetInt("CurrentGame", -1) == raceId &&
                DungeonManager.Instance.raceId == raceId)
            {
                status = string.Format(MenuText.Get("StartProgressAlreadyApplied"), player.PlayerAvatar.Name);
                return false;
            }

            status = string.Format(MenuText.Get("StartProgressApplying"), player.PlayerAvatar.Name);
            Plugin.LogInfo($"Applying selected start progress: player={player.PlayerAvatar.Name}, " +
                           $"progress={Progress(player)}, race={raceId}, chapter={Chapter(player)}-{SubChapter(player)}.");
            manager.RestartGame(firstDeath: false, forceChapter: raceId);
            return true;
        }

        internal static void RemoveConnection(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            Reports.Remove(connection.connectionId);
            if (selectedConnectionId == connection.connectionId)
            {
                selectedPlayerGuid = null;
                selectedConnectionId = int.MinValue;
                status = null;
            }
        }

        internal static void Clear()
        {
            Reports.Clear();
            selectedPlayerGuid = null;
            selectedConnectionId = int.MinValue;
            status = null;
            nextReportAt = 0f;
        }

        private static IEnumerator SendWhenReady(PlayerLocalDataStorage storage)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (storage != null && NetworkClient.active &&
                   (!NetworkClient.ready || !CatchUpRewards.HostSupportsProtocol()) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            if (storage == null || !NetworkClient.active || !NetworkClient.ready ||
                !CatchUpRewards.HostSupportsProtocol()) yield break;
            SendReport(storage);
        }

        private static void SendReport(PlayerLocalDataStorage storage)
        {
            if (storage == null || SaveManager.Current == null) return;
            int progress = Mathf.Clamp(storage.mainQuestProgress, 0, 11);
            int raceId = ResolveLocalRace(storage);
            RaceEntity race = RaceDatabase.FindById(raceId);
            int chapter = race?.actualChapterNum ?? 0;
            int subChapter = 0;
            MainQuestNodeEntity targetNode = progress >= 11 ? ResolveTargetNode(storage) : null;
            if (targetNode != null)
            {
                chapter = targetNode.questChapterNum;
                subChapter = targetNode.subChapterNum;
            }
            NetworkClient.Send(new StartProgressReportMessage
            {
                progress = progress,
                raceId = raceId,
                chapter = chapter,
                subChapter = subChapter,
                currentNodeIds = progress >= 11
                    ? storage.GetCurrentMainQuestNode().Where(node => node != null && !string.IsNullOrEmpty(node.nodeID))
                        .Select(node => node.nodeID).Take(32).ToArray()
                    : Array.Empty<string>()
            });
        }

        private static void OnServerReport(NetworkConnectionToClient connection, StartProgressReportMessage message)
        {
            if (connection == null || connection.identity == null || connection.identity.netId == 0) return;
            RaceEntity race = RaceDatabase.FindById(message.raceId);
            PlayerLocalDataStorage storage = connection.identity.GetComponent<PlayerLocalDataStorage>();
            if (!IsSelectableRace(race) || storage == null ||
                race.mainQuestProgressRequired > message.progress || message.progress < 0 ||
                message.progress > 11 || message.progress < 11 && message.chapter != race.actualChapterNum ||
                message.progress < 11 && message.subChapter != 0 || message.subChapter < 0 ||
                message.subChapter > 100 ||
                !IsRaceConsistentWithProgress(message, race)) return;
            Report report = new Report
            {
                Progress = message.progress,
                RaceId = message.raceId,
                Chapter = Math.Max(0, message.chapter),
                SubChapter = Math.Max(0, message.subChapter)
            };
            if (Reports.TryGetValue(connection.connectionId, out Report existing) &&
                existing.Progress == report.Progress && existing.RaceId == report.RaceId &&
                existing.Chapter == report.Chapter && existing.SubChapter == report.SubChapter) return;
            Reports[connection.connectionId] = report;
            PlayerSpawner player = connection.identity.GetComponent<PlayerSpawner>();
            Plugin.LogInfo($"Start progress report: player={player?.PlayerAvatar?.Name}, " +
                           $"progress={message.progress}, race={message.raceId}, " +
                           $"chapter={message.chapter}-{message.subChapter}.");
        }

        private static void ConfigureSerialization()
        {
            Writer<StartProgressReportMessage>.write = (writer, value) =>
            {
                writer.WriteVarInt(value.progress);
                writer.WriteVarInt(value.raceId);
                writer.WriteVarInt(value.chapter);
                writer.WriteVarInt(value.subChapter);
                string[] nodes = value.currentNodeIds ?? Array.Empty<string>();
                writer.WriteVarInt(Math.Min(32, nodes.Length));
                for (int i = 0; i < nodes.Length && i < 32; i++) writer.WriteString(nodes[i] ?? "");
            };
            Reader<StartProgressReportMessage>.read = reader => new StartProgressReportMessage
            {
                progress = reader.ReadVarInt(),
                raceId = reader.ReadVarInt(),
                chapter = reader.ReadVarInt(),
                subChapter = reader.ReadVarInt(),
                currentNodeIds = ReadNodeIds(reader)
            };
        }

        private static string[] ReadNodeIds(NetworkReader reader)
        {
            int count = Math.Min(32, Math.Max(0, reader.ReadVarInt()));
            string[] nodes = new string[count];
            for (int i = 0; i < count; i++) nodes[i] = reader.ReadString();
            return nodes;
        }

        private static int Progress(PlayerSpawner player)
        {
            Report report = ReportFor(player);
            if (report != null) return report.Progress;
            return IsLocalPlayer(player)
                ? player?.PlayerAvatar?.localDataStorage?.mainQuestProgress ?? int.MaxValue
                : int.MaxValue;
        }

        private static int RaceId(PlayerSpawner player)
        {
            Report report = ReportFor(player);
            if (report != null) return report.RaceId;
            if (player == null || !IsLocalPlayer(player)) return -1;
            return ResolveLocalRace(player.PlayerAvatar.localDataStorage);
        }

        private static int Chapter(PlayerSpawner player) =>
            ReportFor(player)?.Chapter ?? RaceDatabase.FindById(RaceId(player))?.actualChapterNum ?? 0;

        private static int SubChapter(PlayerSpawner player) => ReportFor(player)?.SubChapter ?? 0;

        private static Report ReportFor(PlayerSpawner player)
        {
            int connectionId = player?.connectionToClient?.connectionId ?? int.MinValue;
            return Reports.TryGetValue(connectionId, out Report report) ? report : null;
        }

        private static bool HasReport(PlayerSpawner player) => ReportFor(player) != null;

        private static bool IsLocalPlayer(PlayerSpawner player) =>
            player != null && (player.isHost || player.connectionToClient == NetworkServer.localConnection);

        private static bool IsUsableCandidate(PlayerSpawner player)
        {
            if (player == null) return false;
            RaceEntity race = RaceDatabase.FindById(RaceId(player));
            return IsSelectableRace(race);
        }

        private static string SelectionKey(PlayerSpawner player) =>
            !string.IsNullOrEmpty(player?.playerGuid) ? player.playerGuid : "net:" + player?.netId;

        private static int ResolveLocalRace(PlayerLocalDataStorage storage)
        {
            if (storage == null || SaveManager.Current == null) return -1;
            if (!SwitchManager.GetDestinySwitch("PrologueClear", false)) return 0;

            int chapter3ClearCount = SaveManager.Current.GetInt("Chapter3ClearCount", 0);
            if (chapter3ClearCount > 0)
            {
                bool meet1 = SwitchManager.GetDestinySwitch("DeepCave_Hero_Meet_1", false);
                bool meet2 = SwitchManager.GetDestinySwitch("DeepCave_Hero_Meet_2", false);
                bool meet3 = SwitchManager.GetDestinySwitch("DeepCave_Hero_Meet_3", false);
                if (chapter3ClearCount >= 1 && !meet1 || chapter3ClearCount >= 2 && !meet2 ||
                    chapter3ClearCount >= 3 && !meet3) return 12;
                if (meet3)
                {
                    MainQuestNodeEntity target = ResolveTargetNode(storage);
                    if (target != null) return target.targetRaceId;
                }
                return 13;
            }
            if (SwitchManager.GetDestinySwitch("Chapter2Clear", false)) return 13;
            if (!SwitchManager.GetDestinySwitch("Chapter1Clear", false))
                return SaveManager.Current.GetBool("FirstDeathCutScene", false) ? 7 : 6;
            return 9;
        }

        private static MainQuestNodeEntity ResolveTargetNode(PlayerLocalDataStorage storage) =>
            storage?.GetCurrentMainQuestNode()
                .Where(node => node != null && node.targetRaceId != -1)
                .OrderBy(node => node.questChapterNum)
                .ThenBy(node => node.subChapterNum)
                .ThenBy(node => node.questOrder)
                .FirstOrDefault();

        private static bool IsSelectableRace(RaceEntity race) => race != null && race.lobbyStage != null &&
            race.id >= 0 && race.id < 100 && !race.isMultiplayerBlocked;

        private static bool IsRaceConsistentWithProgress(StartProgressReportMessage message, RaceEntity race)
        {
            if (message.progress == 0) return race.id == 0;
            if (message.progress == 1) return race.id == 6 || race.id == 7;
            if (message.progress == 2 || message.progress == 3)
                return race.id == 9;
            if (message.progress == 4 || message.progress == 5)
                return race.id == 13;
            if (message.progress >= 6 && message.progress <= 10) return race.id == 12;
            if (message.progress != 11) return false;

            bool hasTargetRaceNode = false;
            foreach (string nodeId in message.currentNodeIds ?? Array.Empty<string>())
            {
                MainQuestNodeEntity node = QuestDatabase.FindMainQuestNodeByID(nodeId);
                if (node == null || node.targetRaceId == -1) continue;
                hasTargetRaceNode = true;
                if (node.targetRaceId == race.id && node.questChapterNum == message.chapter &&
                    node.subChapterNum == message.subChapter) return true;
            }
            return race.id == 13 && !hasTargetRaceNode;
        }
    }

    [HarmonyPatch(typeof(PlayerLocalDataStorage), nameof(PlayerLocalDataStorage.OnStartAuthority))]
    internal static class StartProgressReportPatch
    {
        private static void Postfix(PlayerLocalDataStorage __instance) =>
            StartProgressSelection.SendLocalReport(__instance);
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopServer))]
    internal static class StartProgressSelectionServerStopPatch
    {
        private static void Prefix() => StartProgressSelection.Clear();
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.NewGame))]
    internal static class StartProgressSelectionNewGamePatch
    {
        private static void Prefix() => StartProgressSelection.Clear();
    }
}
