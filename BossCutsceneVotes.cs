using System.Collections.Generic;
using HarmonyLib;
using Mirror;

namespace SephiriaTogether
{
    internal static class BossCutsceneVotes
    {
        private enum VoteKind
        {
            Intro,
            DramaticDie,
            PhaseChange
        }

        private sealed class VoteSession
        {
            internal BossSpawner Boss;
            internal VoteKind Kind;
            internal readonly HashSet<int> EligibleConnections = new HashSet<int>();
        }

        private static readonly AccessTools.FieldRef<BossSpawner, BossSpawner.EBattlePhase> BattlePhase =
            AccessTools.FieldRefAccess<BossSpawner, BossSpawner.EBattlePhase>("battlePhase");
        private static readonly AccessTools.FieldRef<BossSpawner, bool> CheckingDramaticDie =
            AccessTools.FieldRefAccess<BossSpawner, bool>("isCheckingSkipOnBossDie");
        private static readonly AccessTools.FieldRef<BossSpawner, bool> CheckingPhaseChange =
            AccessTools.FieldRefAccess<BossSpawner, bool>("isCheckingSkipOnBossPhaseChange");
        private static readonly AccessTools.FieldRef<BossSpawner, bool> Skippable =
            AccessTools.FieldRefAccess<BossSpawner, bool>("skippable");
        private static readonly Dictionary<uint, HashSet<int>> IntroParticipants =
            new Dictionary<uint, HashSet<int>>();
        private static readonly List<VoteSession> Sessions = new List<VoteSession>();

        internal static bool CanBeginIntro(BossSpawner boss) =>
            NetworkServer.active && boss != null && BattlePhase(boss) == BossSpawner.EBattlePhase.None;

        internal static void BeginIntro(BossSpawner boss, bool shouldBegin)
        {
            if (shouldBegin && boss != null && boss.IsBossBattleInProgress)
                Begin(boss, VoteKind.Intro);
        }

        internal static void BeginDramaticDie(BossSpawner boss) => Begin(boss, VoteKind.DramaticDie);

        internal static void BeginPhaseChange(BossSpawner boss) => Begin(boss, VoteKind.PhaseChange);

        internal static void EndIntro(BossSpawner boss) => End(boss, VoteKind.Intro);

        internal static void EndDramaticDie(BossSpawner boss) => End(boss, VoteKind.DramaticDie);

        internal static void EndPhaseChange(BossSpawner boss) => End(boss, VoteKind.PhaseChange);

        internal static void AddLateJoiner(NetworkConnectionToClient connection)
        {
            if (!NetworkServer.active || Sessions.Count == 0 || connection?.identity == null) return;
            PlayerSpawner player = connection.identity.GetComponent<PlayerSpawner>();
            if (player == null) return;
            AddExemption(player);
            Plugin.LogInfo($"Boss cutscene vote excluded late joiner: conn={connection.connectionId}, " +
                           $"player={player.PlayerAvatar?.Name ?? "-"}, sessions={Sessions.Count}.");
        }

        internal static void RemoveConnection(NetworkConnectionToClient connection)
        {
            if (!NetworkServer.active || connection == null) return;
            foreach (VoteSession session in Sessions)
                session.EligibleConnections.Remove(connection.connectionId);
            foreach (HashSet<int> participants in IntroParticipants.Values)
                participants.Remove(connection.connectionId);
            PlayerSpawner player = connection.identity != null
                ? connection.identity.GetComponent<PlayerSpawner>()
                : null;
            if (Sessions.Count > 0 && player != null && DungeonManager.Instance != null)
                DungeonManager.Instance.bossSkipVoteList.Remove(player);
        }

        internal static bool AllowRemoteVote(PlayerSpawner claimedPlayer)
        {
            if (!NetworkServer.active || Sessions.Count == 0) return true;
            NetworkConnectionToClient sender = AntiCheat.CurrentSender;
            PlayerSpawner actual = sender?.identity != null
                ? sender.identity.GetComponent<PlayerSpawner>()
                : null;
            bool valid = sender != null && actual != null && claimedPlayer == actual &&
                         Sessions.Exists(session => session.EligibleConnections.Contains(sender.connectionId));
            if (!valid)
                Plugin.LogInfo($"Boss cutscene vote rejected: conn={sender?.connectionId ?? -1}, sessions={Sessions.Count}.");
            return valid;
        }

        internal static void Clear()
        {
            Sessions.Clear();
            IntroParticipants.Clear();
        }

        internal static void RemoveBoss(BossSpawner boss)
        {
            if (boss == null) return;
            IntroParticipants.Remove(boss.netId);
            Sessions.RemoveAll(session => session.Boss == boss);
            if (Sessions.Count == 0 && NetworkServer.active && DungeonManager.Instance != null)
                DungeonManager.Instance.ClearBossSkipVote();
        }

        private static void Begin(BossSpawner boss, VoteKind kind)
        {
            if (!NetworkServer.active || boss == null || DungeonManager.Instance == null) return;
            Sessions.RemoveAll(existing => existing.Boss == boss && existing.Kind == kind);
            bool overlapping = Sessions.Count > 0;
            VoteSession session = new VoteSession { Boss = boss, Kind = kind };
            if (boss.netIdentity != null)
                foreach (NetworkConnectionToClient connection in boss.netIdentity.observers.Values)
                    if (connection != null && connection.isReady && connection.identity != null)
                    {
                        if (kind != VoteKind.PhaseChange || IntroParticipants.TryGetValue(boss.netId,
                                out HashSet<int> intro) && intro.Contains(connection.connectionId))
                            session.EligibleConnections.Add(connection.connectionId);
                    }
            if (kind == VoteKind.Intro)
                IntroParticipants[boss.netId] = new HashSet<int>(session.EligibleConnections);
            Sessions.Add(session);

            if (overlapping && PlayerSpawner.MultiplayerList != null)
            {
                foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
                    AddExemption(player);
                Plugin.LogInfo($"Overlapping Boss cutscenes detected; auto-skipping to avoid a shared-vote deadlock: " +
                               $"event={kind}, boss={boss.name}.");
            }
            else if (PlayerSpawner.MultiplayerList != null)
                foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
                {
                    NetworkConnectionToClient connection = player?.connectionToClient;
                    if (player != null && (connection == null || !session.EligibleConnections.Contains(connection.connectionId)))
                        AddExemption(player);
                }

            Plugin.LogInfo($"Boss cutscene vote started: event={kind}, boss={boss.name}, " +
                           $"eligible={session.EligibleConnections.Count}, players={PlayerSpawner.MultiplayerList?.Count ?? 0}.");
        }

        private static void End(BossSpawner boss, VoteKind kind)
        {
            if (boss == null) return;
            if (kind == VoteKind.DramaticDie) CheckingDramaticDie(boss) = false;
            else if (kind == VoteKind.PhaseChange) CheckingPhaseChange(boss) = false;
            else Skippable(boss) = false;
            if (kind == VoteKind.DramaticDie) IntroParticipants.Remove(boss.netId);
            int removed = Sessions.RemoveAll(session => session.Boss == boss && session.Kind == kind);
            if (removed == 0 || Sessions.Count > 0) return;
            if (NetworkServer.active && DungeonManager.Instance != null)
                DungeonManager.Instance.ClearBossSkipVote();
        }

        private static void AddExemption(PlayerSpawner player)
        {
            if (player == null || DungeonManager.Instance == null ||
                DungeonManager.Instance.bossSkipVoteList.Contains(player)) return;
            DungeonManager.Instance.bossSkipVoteList.Add(player);
        }
    }

    [HarmonyPatch(typeof(BossSpawner), nameof(BossSpawner.StartBattle))]
    internal static class BossCutsceneIntroBeginPatch
    {
        private static void Prefix(BossSpawner __instance, out bool __state) =>
            __state = BossCutsceneVotes.CanBeginIntro(__instance);

        private static void Postfix(BossSpawner __instance, bool __state) =>
            BossCutsceneVotes.BeginIntro(__instance, __state);
    }

    [HarmonyPatch(typeof(BossSpawner), "ServerBeginBossDramaticDie")]
    internal static class BossCutsceneDeathBeginPatch
    {
        private static void Postfix(BossSpawner __instance) => BossCutsceneVotes.BeginDramaticDie(__instance);
    }

    [HarmonyPatch(typeof(BossSpawner), "ServerBeginPhaseChange")]
    internal static class BossCutscenePhaseBeginPatch
    {
        private static void Postfix(BossSpawner __instance) => BossCutsceneVotes.BeginPhaseChange(__instance);
    }

    [HarmonyPatch(typeof(BossSpawner), "RpcHelloEnd")]
    internal static class BossCutsceneIntroEndPatch
    {
        private static void Prefix(BossSpawner __instance) => BossCutsceneVotes.EndIntro(__instance);
    }

    [HarmonyPatch(typeof(BossSpawner), "ServerEndBossDramaticDie")]
    internal static class BossCutsceneDeathEndPatch
    {
        private static void Prefix(BossSpawner __instance) => BossCutsceneVotes.EndDramaticDie(__instance);
    }

    [HarmonyPatch(typeof(BossSpawner), "ServerEndPhaseChange")]
    internal static class BossCutscenePhaseEndPatch
    {
        private static void Prefix(BossSpawner __instance) => BossCutsceneVotes.EndPhaseChange(__instance);
    }

    [HarmonyPatch(typeof(DungeonManager), "UserCode_CmdSkipVote__PlayerSpawner")]
    internal static class BossCutsceneVoteSenderPatch
    {
        private static bool Prefix(PlayerSpawner id) => BossCutsceneVotes.AllowRemoteVote(id);
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnServerAddPlayer))]
    internal static class BossCutsceneLateJoinPatch
    {
        private static void Postfix(NetworkConnectionToClient conn) => BossCutsceneVotes.AddLateJoiner(conn);
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnServerDisconnect))]
    internal static class BossCutsceneDisconnectPatch
    {
        private static void Prefix(NetworkConnectionToClient conn) => BossCutsceneVotes.RemoveConnection(conn);
    }

    [HarmonyPatch(typeof(BossSpawner), nameof(BossSpawner.OnStopServer))]
    internal static class BossCutsceneBossCleanupPatch
    {
        private static void Prefix(BossSpawner __instance) => BossCutsceneVotes.RemoveBoss(__instance);
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopServer))]
    internal static class BossCutsceneCleanupPatch
    {
        private static void Postfix() => BossCutsceneVotes.Clear();
    }
}
