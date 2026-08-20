using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal sealed class CloneBotController : MonoBehaviour
    {
        private readonly List<Vector3> path = new List<Vector3>();
        private PlayerSpawner source;
        private PlayerSpawner bot;
        private PlayerAvatar avatar;
        private WeaponControllerSimple weaponController;
        private int pathIndex;
        private Vector3 pathDestination;
        private float nextPathCalculation;
        private float nextDecision;
        private float releaseAttackAt;
        private bool attackHeld;
        private string movingFloor;
        private float nextTravelRetry;
        private uint currentTargetId;
        private float targetObservedAt;

        internal void Initialize(PlayerSpawner sourcePlayer)
        {
            source = sourcePlayer;
            bot = GetComponent<PlayerSpawner>();
            avatar = bot != null ? bot.PlayerAvatar : null;
            weaponController = GetComponent<WeaponControllerSimple>();
        }

        private void Update()
        {
            if (!NetworkServer.active || source?.PlayerAvatar == null || avatar == null)
            {
                CloneBotManager.RemoveBot(bot);
                return;
            }

            PlayerAvatar leader = source.PlayerAvatar;
            if (avatar.currentFloorGuid != leader.currentFloorGuid)
            {
                ReleaseAttack();
                Stop();
                if (!string.IsNullOrEmpty(leader.currentFloorGuid) &&
                    (movingFloor != leader.currentFloorGuid || Time.unscaledTime >= nextTravelRetry) &&
                    DungeonManager.Instance != null)
                {
                    movingFloor = leader.currentFloorGuid;
                    nextTravelRetry = Time.unscaledTime + 5f;
                    DungeonManager.Instance.MoveFloor(avatar, leader.currentFloorGuid,
                        string.IsNullOrEmpty(leader.currentSpawnPoint) ? "FLOORSTARTING" : leader.currentSpawnPoint,
                        0, recordHistory: false, allowSave: false, keepPrevFloor: false,
                        randomPosition: false, leaveTrainingSchool: false,
                        overridePosition: leader.transform.position + (Vector3)(UnityEngine.Random.insideUnitCircle * 1.5f));
                }
                return;
            }
            movingFloor = null;

            if (avatar.IsDead)
            {
                CloneBotManager.RemoveBot(bot);
                return;
            }

            if (Time.unscaledTime < nextDecision) return;
            nextDecision = Time.unscaledTime + 0.08f;

            UnitAvatar target = SelectTarget(leader);
            if (target != null)
            {
                Combat(target);
                return;
            }

            ReleaseAttack();
            float distance = Vector2.Distance(avatar.transform.position, leader.transform.position);
            if (distance > 18f)
            {
                TeleportNear(leader);
                return;
            }
            if (distance > 2.5f) Move(leader.transform.position, 1.8f);
            else Stop();
        }

        private UnitAvatar SelectTarget(PlayerAvatar leader)
        {
            if (currentTargetId != 0 && NetworkServer.spawned.TryGetValue(currentTargetId,
                    out NetworkIdentity currentIdentity))
            {
                UnitAvatar current = currentIdentity != null ? currentIdentity.GetComponent<UnitAvatar>() : null;
                if (Time.unscaledTime - targetObservedAt <= 1.25f && IsValidTarget(leader, current))
                    return current;
            }
            if (CombatManager.Instance == null) return null;
            long hostileLayers = leader.GetHostileFactionLayers(EDamageFromType.None);
            return CombatManager.Instance.AllCreatures
                .Where(candidate => IsValidTarget(leader, candidate) &&
                                    (hostileLayers & RuntimeFactionManager.Instance.FindFactionLayer(candidate.faction)) != 0L &&
                                    (candidate.transform.position - leader.transform.position).sqrMagnitude <= 225f)
                .OrderBy(candidate => (candidate.transform.position - leader.transform.position).sqrMagnitude)
                .FirstOrDefault();
        }

        internal void ObserveSourceAttack(UnitAvatar target)
        {
            if (!IsValidTarget(source?.PlayerAvatar, target)) return;
            currentTargetId = target.netId;
            targetObservedAt = Time.unscaledTime;
        }

        private static bool IsValidTarget(PlayerAvatar leader, UnitAvatar target)
        {
            return target != null && target != leader && !(target is PlayerAvatar) &&
                   !CloneBotManager.IsBot(target.GetComponent<PlayerSpawner>()) && !target.IsDead &&
                   target.canBeTarget.IsTrue() &&
                   !string.Equals(target.faction, "Merchant", StringComparison.OrdinalIgnoreCase);
        }

        private void Combat(UnitAvatar target)
        {
            Vector2 direction = target.transform.position - avatar.transform.position;
            float distance = DistanceToTarget(target);
            WeaponSimple weapon = weaponController != null ? weaponController.currentWeapon : null;
            float range = CloneBotManager.PrimaryRange(avatar, weapon);
            avatar.autoAimedTarget = target;
            avatar.ForceAimToPosition(target.transform.position);
            if (distance > range)
            {
                ReleaseAttack();
                Move(target.transform.position, Mathf.Max(0.5f, range * 0.8f));
                return;
            }

            Stop();
            if (weapon is WeaponSimple_Bow)
            {
                if (!attackHeld)
                {
                    avatar.AttackButtonDown(direction);
                    attackHeld = avatar.CanMove;
                    releaseAttackAt = Time.unscaledTime + 0.65f;
                }
                else if (Time.unscaledTime >= releaseAttackAt)
                {
                    ReleaseAttack();
                    nextDecision = Time.unscaledTime + 0.12f;
                }
                return;
            }

            if (!attackHeld)
            {
                avatar.AttackButtonDown(direction);
                attackHeld = avatar.CanMove;
            }
        }

        private float DistanceToTarget(UnitAvatar target)
        {
            Collider2D collider = target?.TopdownRigidbody?.MovementCollider;
            return collider != null && collider.enabled
                ? Vector2.Distance(avatar.transform.position, collider.ClosestPoint(avatar.transform.position))
                : Vector2.Distance(avatar.transform.position, target.transform.position);
        }

        private void Move(Vector3 destination, float stopDistance)
        {
            PlayerLocalDataStorage input = bot?.LocalDataStorage;
            if (input == null) return;
            Vector2 direct = destination - avatar.transform.position;
            if (direct.sqrMagnitude <= stopDistance * stopDistance)
            {
                Stop();
                return;
            }

            PathGrid grid = PathGrid.Current;
            if (grid == null || !grid.IsBuilt)
            {
                input.Move(direct.normalized);
                return;
            }
            bool changed = (destination - pathDestination).sqrMagnitude > 1f;
            if (changed || pathIndex >= path.Count || Time.unscaledTime >= nextPathCalculation)
            {
                path.Clear();
                if (PathFinder.Find(grid, avatar.transform.position, destination, path))
                {
                    PathSmoother.Smooth(grid, path);
                    pathIndex = path.Count > 1 ? 1 : 0;
                    pathDestination = destination;
                }
                nextPathCalculation = Time.unscaledTime + 0.4f;
            }
            while (pathIndex < path.Count &&
                   (path[pathIndex] - avatar.transform.position).sqrMagnitude < 0.25f)
                pathIndex++;
            input.Move(pathIndex < path.Count
                ? ((Vector2)(path[pathIndex] - avatar.transform.position)).normalized
                : direct.normalized);
        }

        private void Stop()
        {
            bot?.LocalDataStorage?.Stop();
        }

        private void TeleportNear(PlayerAvatar leader)
        {
            Vector3 destination = leader.transform.position + (Vector3)(UnityEngine.Random.insideUnitCircle * 1.5f);
            NetworkTransformReliable transformSync = avatar.GetComponent<NetworkTransformReliable>();
            if (transformSync != null) transformSync.ServerTeleport(destination, avatar.transform.rotation);
            else avatar.transform.position = destination;
            path.Clear();
            pathIndex = 0;
        }

        private void ReleaseAttack()
        {
            if (!attackHeld) return;
            avatar?.AttackButtonUp();
            attackHeld = false;
            releaseAttackAt = 0f;
        }

        private void OnDestroy()
        {
            ReleaseAttack();
        }
    }

    internal static class CloneBotManager
    {
        private const string GuidPrefix = "SephiriaTogetherBot:";
        private const ulong BotSteamId = ulong.MaxValue;
        private static readonly List<PlayerSpawner> Bots = new List<PlayerSpawner>();
        private const int MaximumBots = 8;
        private static bool creatingBot;
        private static PlayerSpawner creatingSource;

        internal static bool IsCreatingBot => creatingBot;
        internal static PlayerSpawner CreatingSource => creatingSource;

        internal static IReadOnlyList<PlayerSpawner> ActiveBots
        {
            get
            {
                Bots.RemoveAll(bot => bot == null);
                return Bots;
            }
        }

        internal static int RealPlayerCount => Math.Max(1, PlayerSpawner.MultiplayerList?
            .Count(player => player?.PlayerAvatar != null && !IsBot(player)) ?? 1);

        internal static bool IsBot(PlayerSpawner player) => player != null &&
            (player.steamID == BotSteamId || player.currentPlayerIdxForSave < 0 ||
             !string.IsNullOrEmpty(player.playerGuid) &&
             player.playerGuid.StartsWith(GuidPrefix, StringComparison.Ordinal));

        internal static void RemoveFromPlayerRoster(PlayerSpawner player)
        {
            if (IsBot(player)) PlayerSpawner.MultiplayerList?.Remove(player);
        }

        internal static bool CanCloneLocalPlayer()
        {
            return CanClonePlayer(LocalSpawner());
        }

        internal static bool CanClonePlayer(PlayerSpawner source)
        {
            return NetworkServer.active && source?.PlayerAvatar != null && !IsBot(source) &&
                   Bots.Count < MaximumBots && AllClientsSupportBots() &&
                   NetworkManager.singleton != null && NetworkManager.singleton.playerPrefab != null;
        }

        internal static void CreateCloneOf(PlayerSpawner source)
        {
            if (!CanClonePlayer(source) || Plugin.InstanceForPatches == null) return;
            Plugin.InstanceForPatches.StartCoroutine(CreateClone(source));
        }

        internal static void CreateLocalClone() => CreateCloneOf(LocalSpawner());

        internal static bool AllClientsSupportBots()
        {
            if (!NetworkServer.active) return false;
            return NetworkServer.connections.Values.All(connection => connection == null ||
                connection == NetworkServer.localConnection || CatchUpRewards.IsModdedConnection(connection));
        }

        private static IEnumerator CreateClone(PlayerSpawner source)
        {
            GameObject prefab = NetworkManager.singleton?.playerPrefab;
            if (!NetworkServer.active || source?.PlayerAvatar == null || prefab == null) yield break;

            creatingBot = true;
            creatingSource = source;
            GameObject instance;
            instance = UnityEngine.Object.Instantiate(prefab,
                source.PlayerAvatar.transform.position + Vector3.right * 1.5f, Quaternion.identity);
            PlayerSpawner bot = instance.GetComponent<PlayerSpawner>();
            if (bot == null)
            {
                creatingBot = false;
                creatingSource = null;
                UnityEngine.Object.Destroy(instance);
                yield break;
            }
            bot.playerGuid = GuidPrefix + Guid.NewGuid().ToString("N");
            bot.NetworksteamID = BotSteamId;
            instance.name = "CloneBot_" + StripRichText(source.PlayerAvatar.Name);
            PlayerSpawner.MultiplayerList?.Remove(bot);
            foreach (NetworkBehaviour behaviour in instance.GetComponentsInChildren<NetworkBehaviour>(true))
                behaviour.syncDirection = SyncDirection.ServerToClient;
            try
            {
                NetworkServer.Spawn(instance);
            }
            finally
            {
                creatingBot = false;
                creatingSource = null;
            }
            bot.NetworkcurrentPlayerIdxForSave = -1;
            PlayerSpawner.MultiplayerList?.Remove(bot);
            yield return null;

            try
            {
                CopySourceState(source, bot);
                CloneBotController controller = instance.AddComponent<CloneBotController>();
                controller.Initialize(source);
                SourceControllers(source).Add(controller);
                Bots.Add(bot);
                Plugin.LogInfo($"Clone Bot created: bot={bot.PlayerAvatar.Name}#{bot.netId}, " +
                               $"source={source.PlayerAvatar.Name}#{source.netId}.");
            }
            catch (Exception exception)
            {
                Plugin.LogInfo("Clone Bot initialization failed: " + exception);
                NetworkServer.Destroy(instance);
            }
        }

        private static void CopySourceState(PlayerSpawner source, PlayerSpawner bot)
        {
            PlayerAvatar from = source.PlayerAvatar;
            PlayerAvatar to = bot.PlayerAvatar;
            bot.isHost = false;
            bot.NetworkisHost = false;
            to.dieIsGameOver.SetValue(false);
            to.SetPlayerName("[Bot] " + StripRichText(from.Name));
            to.SetRace(from.Race);
            to.EquipCostume(from.currentCostume, from.currentCostumeSkin);
            to.Inventory?.ForceRemoveAll();
            to.NetworkisInDungeon = from.isInDungeon;
            to.NetworkcurrentFloorGuid = from.currentFloorGuid;
            to.NetworkcurrentSpawnPoint = from.currentSpawnPoint;
            to.Networkfaction = from.faction;
            to.SetRandomID(DungeonManager.Instance != null
                ? DungeonManager.Instance.playerSeedRand.Next()
                : UnityEngine.Random.Range(1, int.MaxValue));

            LevelController sourceLevel = source.GetComponent<LevelController>();
            LevelController botLevel = bot.GetComponent<LevelController>();
            if (sourceLevel != null && botLevel != null)
                botLevel.Initialize(sourceLevel.currentLevel, sourceLevel.currentExp);
            WeaponSimple sourceWeapon = source.GetComponent<WeaponControllerSimple>()?.currentWeapon;
            WeaponControllerSimple sourceWeaponController = source.GetComponent<WeaponControllerSimple>();
            WeaponControllerSimple botWeapon = bot.GetComponent<WeaponControllerSimple>();
            if (sourceWeapon != null && botWeapon != null)
            {
                botWeapon.EquipWeapon(fromTownObject: false, sourceWeapon.entityId);
                if (sourceWeapon is WeaponSimple_GreatSword fromGreatSword &&
                    botWeapon.currentWeapon is WeaponSimple_GreatSword toGreatSword)
                    toGreatSword.NetworkisTransformed = fromGreatSword.isTransformed;
                if (!string.IsNullOrEmpty(sourceWeaponController?.currentWeaponForm))
                {
                    botWeapon.currentWeaponForm = sourceWeaponController.currentWeaponForm;
                    botWeapon.currentWeapon.ChangeWeaponFormOnServer(sourceWeaponController.currentWeaponForm);
                }
            }
            MiracleController sourceMiracles = source.GetComponent<MiracleController>();
            MiracleController botMiracles = bot.GetComponent<MiracleController>();
            if (sourceMiracles != null && botMiracles != null)
            {
                botMiracles.ClearMiracle();
                foreach (Miracle miracle in sourceMiracles.miracles)
                    if (miracle != null && !string.IsNullOrEmpty(miracle.id)) botMiracles.AddMiracle(miracle.id);
            }
            CopyEffectiveStats(from, to);
            to.passiveStats.Clear();
            foreach (KeyValuePair<ulong, int> stat in from.passiveStats) to.passiveStats[stat.Key] = stat.Value;
            to.NetworkisHPCursed = from.isHPCursed;
            to.NetworkcursedMaxHp = from.cursedMaxHp;
            to.Networkmp = Mathf.Min(from.MP, to.MaxMp);
            to.NetworkreservedMp = Mathf.Min(from.reservedMp, to.MaxMp);
            to.SetHp(Mathf.Min(from.hp, to.MaxHp));

            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.eachPlayersPosition[to.netId] = from.currentFloorGuid;
            }
            NetworkTransformReliable transformSync = to.GetComponent<NetworkTransformReliable>();
            Vector3 destination = from.transform.position + Vector3.right * 1.5f;
            if (transformSync != null) transformSync.ServerTeleport(destination, to.transform.rotation);
            else to.transform.position = destination;
        }

        private static void CopyEffectiveStats(PlayerAvatar source, PlayerAvatar target)
        {
            target.customStats.Clear();
            foreach (KeyValuePair<string, int> stat in source.customStats) target.customStats[stat.Key] = stat.Value;
            target.calculatedBonusStats.Clear();
            foreach (KeyValuePair<string, int> stat in source.calculatedBonusStats)
                target.calculatedBonusStats[stat.Key] = stat.Value;
            target.customStatsAmp.Clear();
            foreach (KeyValuePair<string, int> stat in source.customStatsAmp)
                target.customStatsAmp[stat.Key] = stat.Value;
            target.NetworkmaxHp = source.maxHp;
            target.NetworkfinalMaxHp = source.finalMaxHp;
            target.NetworkmaxMp = source.maxMp;
            target.Networkattack = source.attack;
            target.NetworkhighestElementalBonus = source.highestElementalBonus;
            target.NetworkadditionalLife = source.additionalLife;
            target.NetworkadditionalLifeUsed = source.additionalLifeUsed;
            target.NetworkmoveSpeed = source.moveSpeed;
            target.NetworkmoveSpeedMultiplier = source.moveSpeedMultiplier;
            target.NetworkfrostbiteMoveSpeedMultiplier = source.frostbiteMoveSpeedMultiplier;
            target.NetworklocalMoveSpeedMultiplier = source.localMoveSpeedMultiplier;
            target.NetworkmaxRerollDice = source.maxRerollDice;
            target.NetworkrerollDice = source.rerollDice;
            target.NetworkmaxPassivePoint = source.maxPassivePoint;
            target.finalDamageMultiplier = source.finalDamageMultiplier;
            target.runSpeedMultiplier = source.runSpeedMultiplier;
        }

        internal static float PrimaryRange(PlayerAvatar player, WeaponSimple weapon)
        {
            if (weapon == null) return 1.5f;
            if (weapon.isRangedWeapon) return 6f;
            if (weapon is WeaponSimple_Bow || weapon is WeaponSimple_Crossbow ||
                weapon is WeaponSimple_Staff || weapon is WeaponSimple_Golem) return 6f;
            return Mathf.Clamp(1.8f * (1f + player.GetCustomStat(ECustomStat.WeaponRange) / 100f),
                1.25f, 6f);
        }

        internal static void RemoveBot(PlayerSpawner bot)
        {
            if (bot == null) return;
            Bots.Remove(bot);
            foreach (List<CloneBotController> controllers in ControllersBySource.Values)
                controllers.RemoveAll(controller => controller == null || controller.gameObject == bot.gameObject);
            if (NetworkServer.active)
            {
                bot.LocalDataStorage?.Stop();
                bot.PlayerAvatar?.AttackButtonUp();
                bot.GetComponent<WeaponControllerSimple>()?.UnequipWeapon();
                bot.GetComponent<MiracleController>()?.ClearMiracle();
                bot.PlayerAvatar?.Inventory?.ForceRemoveAll();
                DungeonManager.Instance?.RemovePlayerFloorOccupancy(bot.netId);
                NetworkServer.Destroy(bot.gameObject);
            }
        }

        internal static void RemoveAllBots()
        {
            foreach (PlayerSpawner bot in Bots.ToArray()) RemoveBot(bot);
            Bots.Clear();
        }

        internal static void RemoveBotsForSource(PlayerSpawner source)
        {
            if (source == null || !ControllersBySource.TryGetValue(source.netId,
                    out List<CloneBotController> controllers)) return;
            foreach (CloneBotController controller in controllers.ToArray())
                if (controller != null) RemoveBot(controller.GetComponent<PlayerSpawner>());
            ControllersBySource.Remove(source.netId);
        }

        internal static void Clear()
        {
            if (NetworkServer.active) RemoveAllBots();
            else Bots.Clear();
            ControllersBySource.Clear();
        }

        private static PlayerSpawner LocalSpawner() => CombatManager.Instance?.CurrentPlayer?.spawner;

        private static readonly Dictionary<uint, List<CloneBotController>> ControllersBySource =
            new Dictionary<uint, List<CloneBotController>>();

        private static List<CloneBotController> SourceControllers(PlayerSpawner source)
        {
            if (!ControllersBySource.TryGetValue(source.netId, out List<CloneBotController> controllers))
                ControllersBySource[source.netId] = controllers = new List<CloneBotController>();
            controllers.RemoveAll(controller => controller == null);
            return controllers;
        }

        internal static void ObserveSourceAttack(PlayerAvatar source, UnitAvatar target)
        {
            if (source?.spawner == null || target == null ||
                !ControllersBySource.TryGetValue(source.spawner.netId, out List<CloneBotController> controllers))
                return;
            foreach (CloneBotController controller in controllers.ToArray())
                if (controller != null) controller.ObserveSourceAttack(target);
        }

        private static string StripRichText(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Player";
            int start;
            while ((start = value.IndexOf('<')) >= 0)
            {
                int end = value.IndexOf('>', start);
                if (end < 0) break;
                value = value.Remove(start, end - start + 1);
            }
            return value;
        }
    }

    [HarmonyPatch(typeof(MonsterSpawnPhases), nameof(MonsterSpawnPhases.GeneratePhaseData))]
    internal static class CloneBotEnemyCountPatch
    {
        private static void Prefix(ref int multiplayerCount)
        {
            multiplayerCount = CloneBotManager.RealPlayerCount;
        }
    }

    [HarmonyPatch(typeof(BossRewardSpawner), nameof(BossRewardSpawner.OnStartServer))]
    [HarmonyPriority(Priority.First)]
    internal static class CloneBotBossRewardRosterPatch
    {
        private static void Prefix(out PlayerSpawner[] __state)
        {
            __state = PlayerSpawner.MultiplayerList?.Where(CloneBotManager.IsBot).ToArray() ??
                      Array.Empty<PlayerSpawner>();
            if (__state.Length == 0) return;
            foreach (PlayerSpawner bot in __state) PlayerSpawner.MultiplayerList.Remove(bot);
        }

        private static void Postfix(PlayerSpawner[] __state)
        {
            // Bots intentionally stay out of the real-player roster.
        }

        private static Exception Finalizer(Exception __exception, PlayerSpawner[] __state) => __exception;
    }

    [HarmonyPatch(typeof(RandomEnemyPhaseSpawner), "SpawnEnemy")]
    internal static class CloneBotConcurrentRosterPatch
    {
        private static void Prefix(out PlayerSpawner[] __state)
        {
            __state = PlayerSpawner.MultiplayerList?.Where(CloneBotManager.IsBot).ToArray() ??
                      Array.Empty<PlayerSpawner>();
            foreach (PlayerSpawner bot in __state) PlayerSpawner.MultiplayerList.Remove(bot);
        }

        private static Exception Finalizer(Exception __exception, PlayerSpawner[] __state) => __exception;
    }

    [HarmonyPatch(typeof(PlayerAvatar), "AddDealStat")]
    internal static class CloneBotSourceTargetPatch
    {
        private static void Prefix(PlayerAvatar __instance, UnitAvatar victim)
        {
            CloneBotManager.ObserveSourceAttack(__instance, victim);
        }
    }

    [HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Spawn),
        new[] { typeof(GameObject), typeof(GameObject) })]
    internal static class CloneBotOwnedSpawnPatch
    {
        private static bool Prefix(GameObject obj, GameObject ownerPlayer)
        {
            PlayerSpawner owner = ownerPlayer != null ? ownerPlayer.GetComponent<PlayerSpawner>() : null;
            if (!CloneBotManager.IsBot(owner)) return true;
            foreach (NetworkBehaviour behaviour in obj.GetComponentsInChildren<NetworkBehaviour>(true))
                behaviour.syncDirection = SyncDirection.ServerToClient;
            NetworkServer.Spawn(obj);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerSpawner), "ResolveCurrentPlayerIdxForSave")]
    [HarmonyPriority(Priority.First)]
    internal static class CloneBotSaveSlotPatch
    {
        private static bool Prefix(PlayerSpawner __instance)
        {
            if (!CloneBotManager.IsBot(__instance)) return true;
            __instance.NetworkcurrentPlayerIdxForSave = -1;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerSpawner), nameof(PlayerSpawner.SaveCurrentSessionData))]
    [HarmonyPriority(Priority.First)]
    internal static class CloneBotSavePatch
    {
        private static bool Prefix(PlayerSpawner __instance) => !CloneBotManager.IsBot(__instance);
    }

    [HarmonyPatch(typeof(PlayerSpawner), nameof(PlayerSpawner.OnStartServer))]
    [HarmonyPriority(Priority.Last)]
    internal static class CloneBotStartServerPatch
    {
        private static void Postfix(PlayerSpawner __instance)
        {
            if (!CloneBotManager.IsCreatingBot || __instance == null) return;
            __instance.NetworksteamID = ulong.MaxValue;
            __instance.NetworkcurrentPlayerIdxForSave = -1;
            CloneBotManager.RemoveFromPlayerRoster(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerSpawner), "HandleDieServerside")]
    [HarmonyPriority(Priority.First)]
    internal static class CloneBotSourceDeathPatch
    {
        private static void Prefix(PlayerSpawner __instance)
        {
            if (!CloneBotManager.IsBot(__instance)) CloneBotManager.RemoveBotsForSource(__instance);
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopServer))]
    internal static class CloneBotServerStopPatch
    {
        private static void Prefix() => CloneBotManager.RemoveAllBots();
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.RestartGame))]
    internal static class CloneBotRestartPatch
    {
        private static void Prefix() => CloneBotManager.RemoveAllBots();
    }

    [HarmonyPatch(typeof(PlayerSpawner), "HookPlayerIdxChanged")]
    internal static class CloneBotRosterPatch
    {
        private static void Postfix(PlayerSpawner __instance)
        {
            CloneBotManager.RemoveFromPlayerRoster(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerSpawner), "Update")]
    internal static class CloneBotRosterHeartbeatPatch
    {
        private static void Postfix(PlayerSpawner __instance)
        {
            CloneBotManager.RemoveFromPlayerRoster(__instance);
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnServerConnect))]
    internal static class CloneBotJoinCleanupPatch
    {
        private static void Prefix(NetworkConnectionToClient conn)
        {
            if (conn != null && conn != NetworkServer.localConnection && CloneBotManager.ActiveBots.Count > 0)
                CloneBotManager.RemoveAllBots();
        }
    }

}
