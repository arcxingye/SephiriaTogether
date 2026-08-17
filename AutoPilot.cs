using System.Linq;
using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal struct AutoPilotStateRequest : NetworkMessage { public bool enabled; }
    internal struct AutoPilotStateBroadcast : NetworkMessage { public uint netId; public bool enabled; }

    internal static class AutoPilot
    {
        private static bool enabled;
        private static float nextAttack;
        private static float releaseAttackAt;
        private static Vector3 combatRepositionDestination;
        private static float nextCombatReposition;
        private static uint combatRepositionTarget;
        private static float nextUtilityCheck;
        private static float nextEntranceInteraction;
        private static float nextEntranceDiagnostic;
        private static float nextMissingEntranceDiagnostic;
        private static float nextCombatAbility;
        private static float weaponSpecialPendingUntil;
        private static int pendingWeaponSpecialId;
        private static int lastLoggedWeaponId;
        private static float nextWeaponProfileLog;
        private static float nextDash;
        private static float releaseCombatAbilityAt;
        private static int heldCombatAbility = -1;
        private static int nextQuickSlot;
        private static string lastInventorySignature;
        private static string pendingInventorySignature;
        private static float arrangeInventoryAt;
        private static float nextRewardAction;
        private static bool attackHeld;
        private static int heldAttackWeaponId;
        private static float attackHeldSince;
        private static float nextAttackDecisionLog;
        private static float nextAttackInputLog;
        private static readonly HashSet<uint> SkippedAnvils = new HashSet<uint>();
        private static readonly HashSet<string> ResolvedAnvilFloors = new HashSet<string>();
        private static readonly HashSet<string> LoggedAnvilWaitFloors = new HashSet<string>();
        private static readonly Dictionary<uint, int> AnvilRerollCounts = new Dictionary<uint, int>();
        private static float nextAnvilDecision;
        private static string loggedAnvilFloor;
        private static readonly HashSet<int> IgnoredDroppedItems = new HashSet<int>();
        private static string ignoredDropFloor;
        private static float nextDefenseScan;
        private static float nextParry;
        private static float defensiveActionUntil;
        private static bool defenseHeld;
        private static float defenseStartedAt;
        private static float defenseCooldownUntil;
        private static readonly Dictionary<int, Vector3> BulletPositions = new Dictionary<int, Vector3>();
        private static readonly HashSet<Bullet> ActiveBullets = new HashSet<Bullet>();
        private static readonly HashSet<MeleeCollision> ActiveMeleeCollisions = new HashSet<MeleeCollision>();
        private static float nextWorldObjectScan;
        private static string worldObjectFloor;
        private static Anvil cachedAnvil;
        private static BossSpawner cachedBossSpawner;
        private static Interactable cachedEntrance;
        private static Interactable cachedQuestBoard;
        private static float nextQuestBoardAction;
        private static string questObjectScanFloor;
        private static float nextQuestObjectScan;
        private static List<BattleZone> cachedQuestBattleZones = new List<BattleZone>();
        private static List<EnemySpawner> cachedQuestSpawners = new List<EnemySpawner>();
        private static List<EnemySpawnerInteractableTrigger> cachedQuestTriggers =
            new List<EnemySpawnerInteractableTrigger>();
        private static uint lastLoggedQuestObjectiveNetId;
        private static float nextQuestObjectiveLog;
        private static float nextInventorySignatureCheck;
        private static float nextInventoryArrangeAllowed;
        private static Vector3 entranceApproachDestination;
        private static int entranceApproachId;
        private static float nextEntranceApproachSearch;
        private static readonly HashSet<int> StartedBossSpawners = new HashSet<int>();
        private static UnitAvatar cachedEnemy;
        private static float nextEnemySearch;
        private static readonly Dictionary<uint, float> UnreachableEnemies = new Dictionary<uint, float>();
        private static readonly List<Vector3> EnemyReachabilityPath = new List<Vector3>();
        private static Vector3 enemyProgressPosition;
        private static float enemyProgressDistance;
        private static float enemyProgressHp;
        private static float nextEnemyProgressCheck;
        private static int enemyStuckChecks;
        private static Vector2 lastAppliedMovement;
        private static bool movementApplied;
        private static bool runningRequested;
        private static UnitAvatar defenseAimOwner;
        private static Vector3 defenseAimPoint;
        private static UnitAvatar autoPilotAimOwner;
        private static Vector3 autoPilotAimPoint;
        private static bool autoPilotAimActive;
        private static bool bossAoeDefenseActive;
        private static Vector2 defenseEvasionMovement;
        private static Sephirite cachedReward;
        private static float nextRewardScan;
        private static PlayerAvatar rescueTarget;
        private static RevivePlayerByInteraction rescueInteraction;
        private static float rescueCompleteAt;
        private static float nextRescueSearch;
        private static float nextRescuePathCheck;
        private static Vector3 rescueDestination;
        private static Vector3 rescueChannelPosition;
        private static readonly Dictionary<uint, float> UnreachableRescues = new Dictionary<uint, float>();
        private static readonly List<Vector3> RescuePath = new List<Vector3>();
        private static readonly List<Vector3> BossTriggerPath = new List<Vector3>();
        private static readonly List<Vector3> AoeEscapePath = new List<Vector3>();
        private static readonly List<AoeThreat> ActiveAoeThreats = new List<AoeThreat>();
        private static AoeThreat activeAoeEscape;
        private static Vector3 aoeEscapeDestination;
        private static float nextAoeEscapeSearch;
        private static Vector3 laserEscapeDestination;
        private static float laserEscapeUntil;
        private static float nextLaserDiagnostic;
        private static Vector3 dynamiteEscapeDestination;
        private static float nextDynamiteSearch;
        private static float nextDynamiteDiagnostic;
        private static Vector3 bossTriggerDestination;
        private static string bossTriggerFloor;
        private static float nextBossTriggerSearch;
        private static int waitingBossSpawnerId;
        private static float waitingBossSince;
        private static readonly HashSet<int> CompletedBossSpawners = new HashSet<int>();
        private static readonly HashSet<string> CompletedBossFloors = new HashSet<string>();
        private static readonly List<Vector3> CurrentPath = new List<Vector3>();
        private static int pathIndex;
        private static Vector3 pathDestination;
        private static Vector3 lastPathPosition;
        private static float nextPathCalculation;
        private static float nextStuckCheck;
        private static string lastDiagnosticState;
        private static float nextDiagnosticHeartbeat;
        private static string lastPathDiagnostic;
        private static float nextPathDiagnostic;
        private static float nextStateSync;
        private static readonly HashSet<uint> EnabledPlayers = new HashSet<uint>();

        internal static bool Enabled => enabled;

        private sealed class AoeThreat
        {
            internal UI_ObjectPoolable Warning;
            internal AoeShape Shape;
            internal Vector3 Center;
            internal float Radius;
            internal Vector3 From;
            internal Vector3 To;
            internal Vector2 Size;
            internal float Angle;
            internal float CreatedAt;
            internal float WarningTime;
            internal float ActiveUntil;
        }

        private enum AoeShape { Circle, Segment, Box }

        private enum PrimaryInputMode { Hold, ChargeRelease }

        private struct WeaponTactics
        {
            internal string Name;
            internal PrimaryInputMode Input;
            internal float PreferredRange;
            internal float MinimumRange;
            internal float MaximumRange;
            internal float ChargeTime;
            internal bool IsRanged;
        }

        internal static void Toggle()
        {
            SetEnabled(!enabled);
        }

        internal static void SetEnabled(bool value)
        {
            if (enabled == value) return;
            enabled = value;
            if (!enabled) StopLocalPlayer();
            else
            {
                PlayerAvatar local = LocalPlayer();
                local?.AttackButtonUp();
                local?.SubAttackButtonUp();
            }
            Plugin.LogInfo("AFK autopilot " + (enabled ? "enabled" : "disabled") + ".");
            SendState(force: true);
            PlayerAvatar player = LocalPlayer();
            if (player != null && GameLogWriter.Instance != null)
                GameLogWriter.Instance.WriteLog(MenuText.Get(enabled ? "AutoPilotEnabled" : "AutoPilotDisabled"),
                    enabled ? Color.green : Color.white);
        }

        internal static void Tick(PlayerInputController controller)
        {
            if (!enabled) return;
            PlayerAvatar player = LocalPlayer();
            ReleaseCombatAbility(player, force: false);
            if (player == null || player.IsDead || player.localDataStorage == null)
            {
                UpdateRunning(player, false);
                ReportBlockedDiagnostics(player, player == null ? "no-local-player" : player.IsDead ? "player-dead" : "no-local-storage");
                CancelRescue(player);
                ReleaseAttack(player);
                ReleaseCombatAbility(player, force: true);
                return;
            }
            if (UIManager.Instance != null && UIManager.Instance.CurrentControlStack != null)
            {
                if (TrySelectQuestBoardEvent(player)) return;
                UpdateRunning(player, false);
                ReportBlockedDiagnostics(player, "ui-open");
                CancelRescue(player);
                TrySelectPresetWeapon(player);
                ApplyMovement(player, Vector2.zero);
                ReleaseAttack(player);
                ReleaseCombatAbility(player, force: true);
                return;
            }

            LogWeaponProfile(player);
            bool teammateAhead = FindConnectedForwardTeammate(player) != null;
            UnitAvatar enemy = teammateAhead ? null : FindEnemy(player);
            PlayerAvatar leader = FollowTarget(player);
            Vector2 movement = Vector2.zero;
            string action = "idle";
            bool evadingAoe = TryEvadeGroundDynamite(player, out movement) ||
                              TryEvadeActiveLaser(player, out movement) ||
                              TryEvadeProjectile(player, out movement) ||
                              TryEvadeAoe(player, enemy, out movement);
            bool rescuing = !evadingAoe && TryRescueTeammate(player, enemy, out movement);
            bool defending = !evadingAoe && !rescuing && TryAutoDefend(player, enemy);
            if (evadingAoe)
            {
                action = "aoe-evade";
                CancelRescue(player);
                ReleaseDefense(player);
                if (enemy != null)
                {
                    WeaponTactics tactics = AdjustTacticsForEnemy(GetWeaponTactics(player), enemy);
                    bool clearShot = HasClearLineOfFire(player.transform.position, enemy.transform.position);
                    if (tactics.IsRanged && clearShot)
                    {
                        ReportAttackDecision(player, enemy, tactics, "evade-fire", clearShot);
                        Attack(player, (enemy.transform.position - player.transform.position).normalized, tactics);
                    }
                    else
                    {
                        ReportAttackDecision(player, enemy, tactics,
                            tactics.IsRanged ? "evade-blocked-shot" : "evade-melee", clearShot);
                        ReleaseAttack(player);
                    }
                }
                else ReleaseAttack(player);
            }
            else if (rescuing)
            {
                action = rescueInteraction != null ? "revive-channel" : "rescue-approach";
                ReleaseAttack(player);
            }
            else if (enemy != null)
            {
                action = defending ? "defend" : "combat";
                Vector2 toEnemy = enemy.transform.position - player.transform.position;
                player.autoAimedTarget = enemy;
                SetAutoPilotAim(enemy.transform.position, enemy);
                WeaponTactics tactics = AdjustTacticsForEnemy(GetWeaponTactics(player), enemy);
                if (!defending) movement = GetCombatMovement(player, enemy, tactics);
                bool blockedShot = tactics.IsRanged && !HasClearLineOfFire(player.transform.position,
                    enemy.transform.position);
                if (blockedShot && !RequiresUninterruptedPrimary(player))
                {
                    ReportAttackDecision(player, enemy, tactics, "blocked-shot", false);
                    ReleaseAttack(player);
                }
                else if (defending)
                {
                    ReportAttackDecision(player, enemy, tactics, "defending", !blockedShot);
                }
                else
                {
                    bool abilityUsed = TryUseCombatAbility(player, enemy, toEnemy, tactics);
                    ReportAttackDecision(player, enemy, tactics, abilityUsed ? "ability-used" : "primary", !blockedShot);
                    if (!abilityUsed) Attack(player, toEnemy.normalized, tactics);
                }
            }
            else
            {
                ReleaseAttack(player);
                if (defending)
                {
                    action = "defend-projectile";
                    movement = Vector2.zero;
                }
                else if (TryApproachQuestBoard(player, out Vector2 questBoardMovement))
                {
                    action = "quest-board";
                    movement = questBoardMovement;
                }
                else if (TryApproachPresetChoice(player, out Vector2 choiceMovement))
                {
                    action = "anvil";
                    movement = choiceMovement;
                }
                else if (TryApproachQuestObjective(player, out Vector2 objectiveMovement))
                {
                    action = "quest-objective";
                    movement = objectiveMovement;
                }
                else if (TryApproachNextEntrance(player, out Vector2 entranceMovement))
                {
                    action = "entrance";
                    movement = entranceMovement;
                }
                else if (TryApproachBossTrigger(player, out Vector2 bossMovement))
                {
                    action = "boss-trigger";
                    movement = bossMovement;
                }
                else if (leader != null)
                {
                    action = "follow";
                    Vector2 toLeader = leader.transform.position - player.transform.position;
                    if (toLeader.sqrMagnitude > 9f) movement = Navigate(player, leader.transform.position, 3f);
                }
            }

            if (defenseEvasionMovement.sqrMagnitude > 0.01f) movement = defenseEvasionMovement;

            if (!defenseHeld && enemy == null && movement.sqrMagnitude > 0.01f)
                SetAutoPilotAim(player.transform.position + (Vector3)movement, null);
            MaintainAutoPilotAim(player);

            ApplyMovement(player, movement);
            UpdateRunning(player, enemy == null && !evadingAoe && movement.sqrMagnitude > 0.01f);
            ReportDiagnostics(player, action, enemy, movement);
            if (Time.unscaledTime >= nextUtilityCheck)
            {
                nextUtilityCheck = Time.unscaledTime + 0.35f;
                TryPickUpNearbyItem(player);
                TryClaimFavoriteReward(player);
            }
            TryAutoArrangeInventory(player, enemy);
            SendState(force: false);
        }

        internal static void Draw()
        {
            if (!enabled) return;
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Box(new Rect(Screen.width * 0.5f - 130f, 18f, 260f, 34f),
                string.Format(MenuText.Get("AutoPilotBanner"), Plugin.autoPilotShortcut.Value), style);
        }

        internal static bool IsFavorite(int itemId) =>
            SaveManager.Current != null && SaveManager.Current.GetBool("Item_Favorite_" + itemId, false);

        internal static void Clear()
        {
            SetEnabled(false);
            nextAttack = 0f;
            releaseAttackAt = 0f;
            nextUtilityCheck = 0f;
            nextEntranceInteraction = 0f;
            nextEntranceDiagnostic = 0f;
            entranceApproachDestination = Vector3.zero;
            entranceApproachId = 0;
            nextEntranceApproachSearch = 0f;
            nextCombatAbility = 0f;
            weaponSpecialPendingUntil = 0f;
            pendingWeaponSpecialId = 0;
            lastLoggedWeaponId = 0;
            nextWeaponProfileLog = 0f;
            nextDash = 0f;
            releaseCombatAbilityAt = 0f;
            heldCombatAbility = -1;
            nextQuickSlot = 0;
            lastInventorySignature = null;
            pendingInventorySignature = null;
            arrangeInventoryAt = 0f;
            nextRewardAction = 0f;
            attackHeld = false;
            attackHeldSince = 0f;
            nextAttackDecisionLog = 0f;
            nextAttackInputLog = 0f;
            SkippedAnvils.Clear();
            ResolvedAnvilFloors.Clear();
            LoggedAnvilWaitFloors.Clear();
            AnvilRerollCounts.Clear();
            nextAnvilDecision = 0f;
            IgnoredDroppedItems.Clear();
            ignoredDropFloor = null;
            nextDefenseScan = 0f;
            nextParry = 0f;
            defensiveActionUntil = 0f;
            defenseHeld = false;
            defenseStartedAt = 0f;
            defenseCooldownUntil = 0f;
            BulletPositions.Clear();
            ActiveBullets.Clear();
            ActiveMeleeCollisions.Clear();
            StartedBossSpawners.Clear();
            CompletedBossSpawners.Clear();
            CompletedBossFloors.Clear();
            waitingBossSpawnerId = 0;
            waitingBossSince = 0f;
            cachedEnemy = null;
            nextEnemySearch = 0f;
            UnreachableEnemies.Clear();
            EnemyReachabilityPath.Clear();
            enemyProgressPosition = Vector3.zero;
            enemyProgressDistance = float.MaxValue;
            enemyProgressHp = float.MaxValue;
            nextEnemyProgressCheck = 0f;
            enemyStuckChecks = 0;
            movementApplied = false;
            lastAppliedMovement = Vector2.zero;
            runningRequested = false;
            defenseAimOwner = null;
            defenseAimPoint = Vector3.zero;
            autoPilotAimOwner = null;
            autoPilotAimPoint = Vector3.zero;
            autoPilotAimActive = false;
            bossAoeDefenseActive = false;
            defenseEvasionMovement = Vector2.zero;
            cachedReward = null;
            nextRewardScan = 0f;
            cachedQuestBoard = null;
            nextQuestBoardAction = 0f;
            CancelRescue(LocalPlayer());
            rescueTarget = null;
            nextRescueSearch = 0f;
            nextRescuePathCheck = 0f;
            UnreachableRescues.Clear();
            RescuePath.Clear();
            BossTriggerPath.Clear();
            AoeEscapePath.Clear();
            ActiveAoeThreats.Clear();
            activeAoeEscape = null;
            aoeEscapeDestination = Vector3.zero;
            nextAoeEscapeSearch = 0f;
            laserEscapeDestination = Vector3.zero;
            laserEscapeUntil = 0f;
            nextLaserDiagnostic = 0f;
            dynamiteEscapeDestination = Vector3.zero;
            nextDynamiteSearch = 0f;
            nextDynamiteDiagnostic = 0f;
            bossTriggerDestination = Vector3.zero;
            bossTriggerFloor = null;
            nextBossTriggerSearch = 0f;
            ResetWorldObjectCache();
            ResetPath();
            lastDiagnosticState = null;
            nextDiagnosticHeartbeat = 0f;
            lastPathDiagnostic = null;
            nextPathDiagnostic = 0f;
            nextStateSync = 0f;
        }

        internal static void RegisterBullet(Bullet bullet)
        {
            if (bullet != null) ActiveBullets.Add(bullet);
        }

        internal static void UnregisterBullet(Bullet bullet)
        {
            if (bullet == null) return;
            ActiveBullets.Remove(bullet);
            BulletPositions.Remove(bullet.GetInstanceID());
        }

        internal static void RegisterMelee(MeleeCollision melee)
        {
            if (melee != null) ActiveMeleeCollisions.Add(melee);
        }

        internal static void UnregisterMelee(MeleeCollision melee)
        {
            if (melee != null) ActiveMeleeCollisions.Remove(melee);
        }

        internal static void RegisterHostileAoe(UI_AOEWarning warning, Vector3 center, float radius,
            float warningTime, Color color)
        {
            if (warning == null || radius <= 0f || !ApproximatelyHostileColor(color)) return;
            ActiveAoeThreats.RemoveAll(threat => threat.Warning == null || threat.Warning == warning);
            ActiveAoeThreats.Add(new AoeThreat
            {
                Warning = warning,
                Shape = AoeShape.Circle,
                Center = center,
                Radius = radius + 0.65f,
                CreatedAt = Time.unscaledTime,
                WarningTime = Mathf.Max(0.1f, warningTime),
                ActiveUntil = Time.unscaledTime + Mathf.Max(1.25f, warningTime + 0.75f)
            });
        }

        internal static void RegisterServerMessages()
        {
            ConfigureStateSerialization();
            NetworkServer.RegisterHandler<AutoPilotStateRequest>((connection, message) =>
            {
                PlayerSpawner spawner = connection?.identity != null
                    ? connection.identity.GetComponent<PlayerSpawner>()
                    : null;
                PlayerAvatar player = spawner?.PlayerAvatar != null
                    ? spawner.PlayerAvatar
                    : connection?.identity != null ? connection.identity.GetComponent<PlayerAvatar>() : null;
                if (player == null) return;
                if (message.enabled) EnabledPlayers.Add(player.netId);
                else EnabledPlayers.Remove(player.netId);
                AutoPilotStateBroadcast state = new AutoPilotStateBroadcast { netId = player.netId, enabled = message.enabled };
                foreach (NetworkConnectionToClient target in NetworkServer.connections.Values)
                    if (target != null && target.isReady &&
                        (target == NetworkServer.localConnection || CatchUpRewards.IsModdedConnection(target)))
                        target.Send(state);
            }, true);
        }

        internal static void RegisterClientMessages()
        {
            ConfigureStateSerialization();
            EnabledPlayers.Clear();
            NetworkClient.RegisterHandler<AutoPilotStateBroadcast>(message =>
            {
                if (message.enabled) EnabledPlayers.Add(message.netId);
                else EnabledPlayers.Remove(message.netId);
            }, true);
        }

        internal static string DisplayName(PlayerAvatar player)
        {
            if (player == null) return "-";
            return EnabledPlayers.Contains(player.netId) || player.isOwned && enabled
                ? MenuText.Get("AutoPilotNamePrefix") + player.Name
                : player.Name;
        }

        private static void SendState(bool force)
        {
            if (!NetworkClient.active || !NetworkClient.ready || !force && Time.unscaledTime < nextStateSync) return;
            nextStateSync = Time.unscaledTime + 3f;
            NetworkClient.Send(new AutoPilotStateRequest { enabled = enabled });
        }

        private static void ConfigureStateSerialization()
        {
            Writer<AutoPilotStateRequest>.write = (writer, value) => writer.WriteBool(value.enabled);
            Reader<AutoPilotStateRequest>.read = reader => new AutoPilotStateRequest { enabled = reader.ReadBool() };
            Writer<AutoPilotStateBroadcast>.write = (writer, value) =>
            {
                writer.WriteUInt(value.netId);
                writer.WriteBool(value.enabled);
            };
            Reader<AutoPilotStateBroadcast>.read = reader => new AutoPilotStateBroadcast
            {
                netId = reader.ReadUInt(),
                enabled = reader.ReadBool()
            };
        }

        internal static void RegisterHostileLine(UI_ObjectPoolable warning, Vector3 from, Vector3 to,
            float warningTime, Color color)
        {
            if (warning == null || !ApproximatelyHostileColor(color)) return;
            ActiveAoeThreats.RemoveAll(threat => threat.Warning == null || threat.Warning == warning);
            ActiveAoeThreats.Add(new AoeThreat
            {
                Warning = warning,
                Shape = AoeShape.Segment,
                From = from,
                To = to,
                Center = (from + to) * 0.5f,
                Radius = 1.25f,
                CreatedAt = Time.unscaledTime,
                WarningTime = Mathf.Max(0.1f, warningTime),
                ActiveUntil = Time.unscaledTime + Mathf.Max(2.5f, warningTime + 1.75f)
            });
        }

        internal static void RegisterHostileBox(UI_ObjectPoolable warning, Vector3 center, Vector2 size,
            float angle, float warningTime, Color color)
        {
            if (warning == null || !ApproximatelyHostileColor(color)) return;
            ActiveAoeThreats.RemoveAll(threat => threat.Warning == null || threat.Warning == warning);
            ActiveAoeThreats.Add(new AoeThreat
            {
                Warning = warning,
                Shape = AoeShape.Box,
                Center = center,
                Size = size + Vector2.one * 1.2f,
                Angle = angle,
                CreatedAt = Time.unscaledTime,
                WarningTime = Mathf.Max(0.1f, warningTime),
                ActiveUntil = Time.unscaledTime + Mathf.Max(1.25f, warningTime + 0.6f)
            });
        }

        private static bool ApproximatelyHostileColor(Color color)
        {
            Color hostile = AOEWarningFactory.HostileWarningColor;
            return Mathf.Abs(color.r - hostile.r) < 0.08f && Mathf.Abs(color.g - hostile.g) < 0.08f &&
                   Mathf.Abs(color.b - hostile.b) < 0.08f;
        }

        internal static void MaintainAutoPilotAim(PlayerAvatar player)
        {
            if (player == null) return;
            if (defenseHeld) LockAim(player, defenseAimPoint, defenseAimOwner);
            else if (autoPilotAimActive) LockAim(player, autoPilotAimPoint, autoPilotAimOwner);
        }

        internal static void NotifyBossDefeated(BossSpawner spawner)
        {
            if (spawner == null) return;
            int id = spawner.GetInstanceID();
            CompletedBossSpawners.Add(id);
            StartedBossSpawners.Add(id);
            if (cachedBossSpawner == spawner) cachedBossSpawner = null;
            FloorGenerator floor = spawner.parent != null ? spawner.parent : spawner.GetComponentInParent<FloorGenerator>();
            string floorGuid = floor != null ? floor.guid : LocalPlayer()?.currentFloorGuid;
            if (!string.IsNullOrEmpty(floorGuid)) CompletedBossFloors.Add(floorGuid);
            cachedEntrance = null;
            entranceApproachDestination = Vector3.zero;
            entranceApproachId = 0;
            nextWorldObjectScan = 0f;
            nextEntranceInteraction = 0f;
            ResetPath();
            Plugin.LogInfo($"AFK Boss defeat confirmed by vanilla RpcByeEnd: spawner={spawner.name}, " +
                           $"floor={ShortGuid(floorGuid)}; rescanning exits.");
        }

        internal static void NotifySeedBossDefeated(SeedBossSpawner spawner)
        {
            if (spawner == null) return;
            FloorGenerator floor = spawner.GetComponentInParent<FloorGenerator>();
            string floorGuid = floor != null ? floor.guid : LocalPlayer()?.currentFloorGuid;
            if (!string.IsNullOrEmpty(floorGuid)) CompletedBossFloors.Add(floorGuid);
            cachedBossSpawner = null;
            cachedEntrance = null;
            entranceApproachDestination = Vector3.zero;
            entranceApproachId = 0;
            nextWorldObjectScan = 0f;
            nextEntranceInteraction = 0f;
            ResetPath();
            Plugin.LogInfo($"AFK Seed Boss defeat confirmed by vanilla RpcByeEnd: spawner={spawner.name}, " +
                           $"floor={ShortGuid(floorGuid)}; rescanning exits.");
        }

        private static void SetAutoPilotAim(Vector3 point, UnitAvatar owner)
        {
            autoPilotAimPoint = point;
            autoPilotAimOwner = owner;
            autoPilotAimActive = true;
        }

        private static UnitAvatar FindEnemy(PlayerAvatar player)
        {
            bool invalid = cachedEnemy == null || cachedEnemy.IsDead || !cachedEnemy.canBeTarget.IsTrue() ||
                           (cachedEnemy.transform.position - player.transform.position).sqrMagnitude > 2500f ||
                           UnreachableEnemies.TryGetValue(cachedEnemy.netId, out float retryAt) &&
                           Time.unscaledTime < retryAt;
            if (invalid || Time.unscaledTime >= nextEnemySearch)
            {
                UnitAvatar previous = cachedEnemy;
                UnitAvatar selected = FindNearestReachableCandidate(player);
                cachedEnemy = selected;
                nextEnemySearch = Time.unscaledTime + 0.12f;
                if (previous == null || selected == null || previous.netId != selected.netId)
                    ResetEnemyProgress(player, selected);
            }
            TrackEnemyProgress(player);
            return cachedEnemy;
        }

        private static UnitAvatar FindNearestReachableCandidate(PlayerAvatar player)
        {
            if (CombatManager.Instance == null) return null;
            long hostileLayers = player.GetHostileFactionLayers(EDamageFromType.None);
            PathGrid grid = PathGrid.Current;
            IEnumerable<UnitAvatar> candidates = CombatManager.Instance.AllCreatures
                .Where(candidate => candidate != null && candidate != player && !candidate.IsDead &&
                    candidate.canBeTarget.IsTrue() &&
                    (hostileLayers & RuntimeFactionManager.Instance.FindFactionLayer(candidate.faction)) != 0L &&
                    (!UnreachableEnemies.TryGetValue(candidate.netId, out float retryAt) ||
                     Time.unscaledTime >= retryAt))
                .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude);
            foreach (UnitAvatar candidate in candidates)
            {
                float distance = (candidate.transform.position - player.transform.position).sqrMagnitude;
                if (distance > 2500f) break;
                if (grid != null && grid.IsBuilt)
                {
                    EnemyReachabilityPath.Clear();
                    if (!grid.WorldToCell(candidate.transform.position, out int x, out int y) || grid.IsBlocked(x, y) ||
                        !PathFinder.Find(grid, player.transform.position, candidate.transform.position,
                            EnemyReachabilityPath) || EnemyReachabilityPath.Count == 0)
                    {
                        UnreachableEnemies[candidate.netId] = Time.unscaledTime + 1f;
                        continue;
                    }
                }
                return candidate;
            }
            UnitAvatar objectiveCandidate = FindQuestObjectiveCreature(player);
            if (objectiveCandidate != null)
            {
                if (lastLoggedQuestObjectiveNetId != objectiveCandidate.netId &&
                    Time.unscaledTime >= nextQuestObjectiveLog)
                {
                    lastLoggedQuestObjectiveNetId = objectiveCandidate.netId;
                    nextQuestObjectiveLog = Time.unscaledTime + 1f;
                    Plugin.LogInfo($"AFK quest objective creature selected: floor={ShortGuid(player.currentFloorGuid)}, " +
                                   $"event={FloorGenerator.FindByGuid(player.currentFloorGuid)?.questBoardEventId}, " +
                                   $"unit={DescribeUnit(objectiveCandidate)}, faction={objectiveCandidate.faction}.");
                }
                return objectiveCandidate;
            }
            lastLoggedQuestObjectiveNetId = 0;
            return null;
        }

        private static UnitAvatar FindQuestObjectiveCreature(PlayerAvatar player)
        {
            FloorGenerator floor = player != null ? FloorGenerator.FindByGuid(player.currentFloorGuid) : null;
            if (floor == null || floor.isQuestObjectiveCompleted || string.IsNullOrEmpty(floor.questBoardEventId) ||
                CombatManager.Instance == null) return null;
            return CombatManager.Instance.AllCreatures
                .Where(candidate => candidate != null && candidate != player && !(candidate is PlayerAvatar) &&
                                    !candidate.IsDead && candidate.canBeTarget.IsTrue() &&
                                    (candidate.transform.position - player.transform.position).sqrMagnitude <= 2500f)
                .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                .FirstOrDefault();
        }

        private static void TrackEnemyProgress(PlayerAvatar player)
        {
            if (cachedEnemy == null || Time.unscaledTime < nextEnemyProgressCheck) return;
            nextEnemyProgressCheck = Time.unscaledTime + 1f;
            float distance = (cachedEnemy.transform.position - player.transform.position).sqrMagnitude;
            float moved = (player.transform.position - enemyProgressPosition).sqrMagnitude;
            bool damaged = cachedEnemy.hp < enemyProgressHp - 0.01f;
            bool progressed = damaged || moved > 0.04f || distance < enemyProgressDistance - 0.25f;
            if (progressed)
            {
                enemyStuckChecks = 0;
            }
            else
            {
                enemyStuckChecks++;
                bool clearShot = HasClearLineOfFire(player.transform.position, cachedEnemy.transform.position);
                int stuckLimit = clearShot ? 5 : 3;
                if (enemyStuckChecks >= stuckLimit)
                {
                    UnreachableEnemies[cachedEnemy.netId] = Time.unscaledTime + 4f;
                    Plugin.LogInfo($"AFK autopilot postponed stalled enemy: enemy={cachedEnemy.name}, " +
                                   $"lineOfFire={clearShot}, hp={cachedEnemy.hp:0.##}, retry=4s.");
                    cachedEnemy = null;
                    enemyStuckChecks = 0;
                    ResetPath();
                }
            }
            enemyProgressPosition = player.transform.position;
            enemyProgressDistance = distance;
            enemyProgressHp = cachedEnemy != null ? cachedEnemy.hp : float.MaxValue;
        }

        private static void ResetEnemyProgress(PlayerAvatar player, UnitAvatar enemy)
        {
            enemyProgressPosition = player != null ? player.transform.position : Vector3.zero;
            enemyProgressDistance = player != null && enemy != null
                ? (enemy.transform.position - player.transform.position).sqrMagnitude
                : float.MaxValue;
            enemyProgressHp = enemy != null ? enemy.hp : float.MaxValue;
            nextEnemyProgressCheck = Time.unscaledTime + 1f;
            enemyStuckChecks = 0;
        }

        private static void ApplyMovement(PlayerAvatar player, Vector2 movement)
        {
            bool shouldMove = movement.sqrMagnitude > 0.01f;
            if (!shouldMove)
            {
                if (movementApplied) player.localDataStorage.Stop();
                movementApplied = false;
                lastAppliedMovement = Vector2.zero;
                return;
            }
            movement.Normalize();
            player.localDataStorage.Move(movement);
            lastAppliedMovement = movement;
            movementApplied = true;
        }

        private static void UpdateRunning(PlayerAvatar player, bool shouldRun)
        {
            if (player?.localDataStorage == null) return;
            if (shouldRun)
            {
                if (!runningRequested) player.localDataStorage.StartRunningCountdown();
                runningRequested = true;
            }
            else if (runningRequested)
            {
                player.localDataStorage.StopRunningCountdown();
                runningRequested = false;
            }
        }

        private static bool TryEvadeAoe(PlayerAvatar player, UnitAvatar enemy, out Vector2 movement)
        {
            movement = Vector2.zero;
            ActiveAoeThreats.RemoveAll(threat => Time.unscaledTime >= threat.ActiveUntil);
            Vector3 position = player.transform.position;
            AoeThreat threat = ActiveAoeThreats
                .Where(candidate => IsInsideAoe(candidate, position))
                .OrderByDescending(AoeRatio)
                .FirstOrDefault();
            if (threat == null && activeAoeEscape != null && Time.unscaledTime < activeAoeEscape.ActiveUntil)
            {
                threat = activeAoeEscape;
                if (!IsInsideAnyAoe(position) && (position - aoeEscapeDestination).sqrMagnitude <= 1f)
                {
                    movement = Vector2.zero;
                    return true;
                }
            }
            if (threat == null)
            {
                activeAoeEscape = null;
                return false;
            }

            if (activeAoeEscape != threat || Time.unscaledTime >= nextAoeEscapeSearch)
            {
                activeAoeEscape = threat;
                nextAoeEscapeSearch = Time.unscaledTime + 0.25f;
                if (!TryFindAoeEscape(player, threat, out aoeEscapeDestination))
                {
                    Vector2 fallback = player.transform.position - threat.Center;
                    if (fallback.sqrMagnitude < 0.01f && enemy != null)
                        fallback = player.transform.position - enemy.transform.position;
                    if (fallback.sqrMagnitude < 0.01f) fallback = Vector2.right;
                    fallback.Normalize();
                    Plugin.LogInfo($"AFK AOE evade: no safe path, center={threat.Center}, radius={threat.Radius:0.00}, " +
                                   $"ratio={AoeRatio(threat):0.00}, shape={threat.Shape}.");
                    if (AoeRatio(threat) >= 0.55f && Time.unscaledTime >= nextDash && player.CanMove)
                    {
                        player.Dash(player.transform.position + (Vector3)(fallback * 4f));
                        nextDash = Time.unscaledTime + 0.5f;
                    }
                    movement = fallback;
                    return true;
                }
                Plugin.LogInfo($"AFK AOE evade: target={aoeEscapeDestination}, center={threat.Center}, " +
                               $"radius={threat.Radius:0.00}, ratio={AoeRatio(threat):0.00}, shape={threat.Shape}.");
            }

            Vector2 direction = aoeEscapeDestination - player.transform.position;
            if (direction.sqrMagnitude <= 0.16f) return true;
            direction.Normalize();
            if (AoeRatio(threat) >= 0.65f && Time.unscaledTime >= nextDash && player.CanMove)
            {
                player.Dash(player.transform.position + (Vector3)(direction * 4f));
                nextDash = Time.unscaledTime + 0.5f;
            }
            movement = Navigate(player, aoeEscapeDestination, 0.2f);
            return true;
        }

        private static bool TryEvadeActiveLaser(PlayerAvatar player, out Vector2 movement)
        {
            movement = Vector2.zero;
            Bullet laserBullet = null;
            Vector2 segmentStart = Vector2.zero;
            Vector2 segmentEnd = Vector2.zero;
            float safeWidth = 0f;
            float nearestDistance = float.MaxValue;
            foreach (Bullet bullet in ActiveBullets)
            {
                BulletMoveModule_Laser laser = bullet?.MoveModule as BulletMoveModule_Laser;
                BoxCollider2D box = bullet?.attackingCollider as BoxCollider2D;
                if (laser == null || box == null || !bullet.IsSpawned || !bullet.isCollisionEnabled ||
                    bullet.Owner == player || !CombatManager.ContainsAttackableFaction(bullet.AttackableFactionLayers, player.faction))
                    continue;
                Vector2 start = bullet.transform.position;
                Vector2 direction = HorayUtility.GetVector3FromAngle(laser.GetBodyAngle());
                float length = Mathf.Abs(box.size.y * box.transform.lossyScale.y);
                Vector2 end = start + direction * length;
                float distance = DistanceToSegment(player.transform.position, start, end);
                float width = Mathf.Abs(box.size.x * box.transform.lossyScale.x) * 0.5f + 0.75f;
                if (distance > width || distance >= nearestDistance) continue;
                laserBullet = bullet;
                segmentStart = start;
                segmentEnd = end;
                safeWidth = width;
                nearestDistance = distance;
            }

            if (laserBullet != null)
            {
                Vector2 beam = segmentEnd - segmentStart;
                Vector2 perpendicular = beam.sqrMagnitude > 0.001f
                    ? new Vector2(-beam.y, beam.x).normalized
                    : Vector2.up;
                Vector2 closest = ClosestPointOnSegment(player.transform.position, segmentStart, segmentEnd);
                if (Vector2.Dot((Vector2)player.transform.position - closest, perpendicular) < 0f) perpendicular = -perpendicular;
                Vector3 preferred = closest + perpendicular * (safeWidth + 1.25f);
                if (!TryReachablePointNear(player, preferred, out laserEscapeDestination))
                    laserEscapeDestination = player.transform.position + (Vector3)(perpendicular * (safeWidth + 1.25f));
                laserEscapeUntil = Time.unscaledTime + 0.65f;
                if (Time.unscaledTime >= nextLaserDiagnostic)
                {
                    nextLaserDiagnostic = Time.unscaledTime + 0.5f;
                    Plugin.LogInfo($"AFK laser evade: target={laserEscapeDestination}, from={segmentStart}, " +
                                   $"to={segmentEnd}, width={safeWidth:0.00}.");
                }
            }
            if (Time.unscaledTime >= laserEscapeUntil) return false;
            Vector2 escape = laserEscapeDestination - player.transform.position;
            if (escape.sqrMagnitude <= 0.25f) return true;
            escape.Normalize();
            if (laserBullet != null && Time.unscaledTime >= nextDash && player.CanMove)
            {
                player.Dash(player.transform.position + (Vector3)(escape * 4f));
                nextDash = Time.unscaledTime + 0.5f;
            }
            movement = Navigate(player, laserEscapeDestination, 0.2f);
            if (movement.sqrMagnitude < 0.01f) movement = escape;
            return true;
        }

        private static bool TryEvadeGroundDynamite(PlayerAvatar player, out Vector2 movement)
        {
            movement = Vector2.zero;
            Bullet nearest = ActiveBullets
                .Where(bullet => bullet != null && bullet.IsSpawned && bullet.gameObject.activeInHierarchy &&
                                 bullet.Owner != player &&
                                 bullet.GetComponent<BulletAnimationTransitionController_Dynamite>() != null &&
                                 CombatManager.ContainsAttackableFaction(bullet.AttackableFactionLayers, player.faction))
                .OrderBy(bullet => (bullet.transform.position - player.transform.position).sqrMagnitude)
                .FirstOrDefault();
            if (nearest == null) return false;
            Vector2 away = player.transform.position - nearest.transform.position;
            const float dangerRadius = 4.5f;
            if (away.sqrMagnitude > dangerRadius * dangerRadius) return false;
            if (Time.unscaledTime >= nextDynamiteSearch)
            {
                nextDynamiteSearch = Time.unscaledTime + 0.25f;
                if (away.sqrMagnitude < 0.01f) away = Vector2.right;
                Vector3 preferred = nearest.transform.position + (Vector3)(away.normalized * (dangerRadius + 1f));
                if (!TryReachablePointNear(player, preferred, out dynamiteEscapeDestination))
                    dynamiteEscapeDestination = preferred;
            }
            Vector2 escape = dynamiteEscapeDestination - player.transform.position;
            if (Time.unscaledTime >= nextDynamiteDiagnostic)
            {
                nextDynamiteDiagnostic = Time.unscaledTime + 0.75f;
                Plugin.LogInfo($"AFK dynamite evade: dynamite={nearest.transform.position}, " +
                               $"target={dynamiteEscapeDestination}, distance={Mathf.Sqrt(away.sqrMagnitude):0.00}.");
            }
            if (away.sqrMagnitude <= 6.25f && Time.unscaledTime >= nextDash && player.CanMove)
            {
                player.Dash(player.transform.position + (Vector3)(escape.normalized * 4f));
                nextDash = Time.unscaledTime + 0.5f;
            }
            movement = Navigate(player, dynamiteEscapeDestination, 0.2f);
            if (movement.sqrMagnitude < 0.01f) movement = escape.normalized;
            return true;
        }

        private static bool TryEvadeProjectile(PlayerAvatar player, out Vector2 movement)
        {
            movement = Vector2.zero;
            WeaponControllerSimple controller = player.GetComponent<WeaponControllerSimple>();
            if (controller?.currentWeapon is WeaponSimple_SwordAndShield ||
                controller?.currentWeapon is WeaponSimple_Dagger) return false;
            Bullet threat = null;
            Vector2 travel = Vector2.zero;
            float nearestDistance = 9f;
            foreach (Bullet bullet in ActiveBullets)
            {
                if (bullet == null || !bullet.IsSpawned || !bullet.isCollisionEnabled || bullet.Owner == player ||
                    bullet.MoveModule == null || bullet.MoveModule is BulletMoveModule_Laser ||
                    bullet.GetComponent<BulletAnimationTransitionController_Dynamite>() != null ||
                    !CombatManager.ContainsAttackableFaction(bullet.AttackableFactionLayers, player.faction)) continue;
                Vector2 delta = player.transform.position - bullet.transform.position;
                Vector2 direction = bullet.MoveModule.CurMovingDirection;
                if (delta.sqrMagnitude >= nearestDistance || direction.sqrMagnitude < 0.01f ||
                    Vector2.Dot(direction.normalized, delta.normalized) < 0.55f) continue;
                nearestDistance = delta.sqrMagnitude;
                threat = bullet;
                travel = direction.normalized;
            }
            if (threat == null) return false;
            Vector2 perpendicular = new Vector2(-travel.y, travel.x);
            Vector3 destination;
            if (!TryReachablePointNear(player, player.transform.position + (Vector3)(perpendicular * 3f), out destination) &&
                !TryReachablePointNear(player, player.transform.position - (Vector3)(perpendicular * 3f), out destination))
                destination = player.transform.position + (Vector3)(perpendicular * 3f);
            Vector2 escape = destination - player.transform.position;
            if (nearestDistance <= 4f && Time.unscaledTime >= nextDash && player.CanMove)
            {
                player.Dash(player.transform.position + (Vector3)(escape.normalized * 4f));
                nextDash = Time.unscaledTime + 0.5f;
            }
            movement = Navigate(player, destination, 0.2f);
            if (movement.sqrMagnitude < 0.01f) movement = escape.normalized;
            return true;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b) =>
            Vector2.Distance(point, ClosestPointOnSegment(point, a, b));

        private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float denominator = segment.sqrMagnitude;
            float t = denominator > 0.001f ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / denominator) : 0f;
            return a + segment * t;
        }

        private static bool TryReachablePointNear(PlayerAvatar player, Vector3 preferred, out Vector3 destination)
        {
            destination = Vector3.zero;
            PathGrid grid = PathGrid.Current;
            if (grid == null || !grid.IsBuilt || !grid.WorldToCell(preferred, out int x, out int y) || grid.IsBlocked(x, y))
                return false;
            Vector3 world = grid.CellToWorld(x, y);
            if (IsInsideAnyAoe(world)) return false;
            AoeEscapePath.Clear();
            if (!PathFinder.Find(grid, player.transform.position, world, AoeEscapePath) || AoeEscapePath.Count == 0)
                return false;
            destination = world;
            return true;
        }

        private static bool TryFindAoeEscape(PlayerAvatar player, AoeThreat threat, out Vector3 destination)
        {
            destination = Vector3.zero;
            PathGrid grid = PathGrid.Current;
            if (grid == null || !grid.IsBuilt) return false;
            List<Vector3> candidates = new List<Vector3>();
            foreach (Vector3 point in GenerateEscapeCandidates(player.transform.position, threat))
            {
                if (!grid.WorldToCell(point, out int x, out int y) || grid.IsBlocked(x, y)) continue;
                Vector3 world = grid.CellToWorld(x, y);
                if (IsInsideAnyAoe(world)) continue;
                candidates.Add(world);
            }
            foreach (Vector3 candidate in candidates.OrderBy(point =>
                         (point - player.transform.position).sqrMagnitude))
            {
                AoeEscapePath.Clear();
                if (!PathFinder.Find(grid, player.transform.position, candidate, AoeEscapePath) ||
                    AoeEscapePath.Count == 0) continue;
                destination = candidate;
                return true;
            }
            return false;
        }

        private static IEnumerable<Vector3> GenerateEscapeCandidates(Vector3 playerPosition, AoeThreat threat)
        {
            if (threat.Shape == AoeShape.Circle)
            {
                float radius = threat.Radius + 1f;
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI * 2f / 16f;
                    yield return threat.Center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                yield break;
            }
            if (threat.Shape == AoeShape.Segment)
            {
                Vector2 a = threat.From;
                Vector2 b = threat.To;
                Vector2 segment = b - a;
                float denominator = segment.sqrMagnitude;
                float t = denominator > 0.001f
                    ? Mathf.Clamp01(Vector2.Dot((Vector2)playerPosition - a, segment) / denominator)
                    : 0f;
                Vector2 closest = a + segment * t;
                Vector2 perpendicular = denominator > 0.001f
                    ? new Vector2(-segment.y, segment.x).normalized
                    : Vector2.up;
                float distance = threat.Radius + 1f;
                yield return closest + perpendicular * distance;
                yield return closest - perpendicular * distance;
                yield return a - segment.normalized * distance;
                yield return b + segment.normalized * distance;
                yield break;
            }
            Vector2 half = threat.Size * 0.5f + Vector2.one;
            Vector2[] localPoints =
            {
                new Vector2(half.x, 0f), new Vector2(-half.x, 0f),
                new Vector2(0f, half.y), new Vector2(0f, -half.y),
                new Vector2(half.x, half.y), new Vector2(half.x, -half.y),
                new Vector2(-half.x, half.y), new Vector2(-half.x, -half.y)
            };
            foreach (Vector2 local in localPoints)
                yield return threat.Center + Quaternion.Euler(0f, 0f, threat.Angle) * local;
        }

        private static float AoeRatio(AoeThreat threat) => threat == null || threat.WarningTime <= 0f
            ? 1f
            : Mathf.Clamp01((Time.unscaledTime - threat.CreatedAt) / threat.WarningTime);

        private static bool IsInsideAnyAoe(Vector3 point) => ActiveAoeThreats.Any(threat =>
            Time.unscaledTime < threat.ActiveUntil && IsInsideAoe(threat, point));

        private static bool IsInsideAoe(AoeThreat threat, Vector3 point)
        {
            Vector2 p = point;
            if (threat.Shape == AoeShape.Circle)
                return (p - (Vector2)threat.Center).sqrMagnitude <= threat.Radius * threat.Radius;
            if (threat.Shape == AoeShape.Segment)
            {
                Vector2 a = threat.From;
                Vector2 b = threat.To;
                Vector2 segment = b - a;
                float denominator = segment.sqrMagnitude;
                float t = denominator > 0.001f ? Mathf.Clamp01(Vector2.Dot(p - a, segment) / denominator) : 0f;
                Vector2 closest = a + segment * t;
                return (p - closest).sqrMagnitude <= threat.Radius * threat.Radius;
            }
            Vector2 local = Quaternion.Euler(0f, 0f, -threat.Angle) * (p - (Vector2)threat.Center);
            return Mathf.Abs(local.x) <= threat.Size.x * 0.5f && Mathf.Abs(local.y) <= threat.Size.y * 0.5f;
        }

        private static bool TryRescueTeammate(PlayerAvatar player, UnitAvatar enemy, out Vector2 movement)
        {
            movement = Vector2.zero;
            if ((PlayerSpawner.MultiplayerList?.Count ?? 0) <= 1) return false;
            if (rescueTarget != null && (!rescueTarget.IsDead ||
                                         rescueTarget.currentFloorGuid != player.currentFloorGuid))
                CancelRescue(player);

            if (rescueTarget == null && Time.unscaledTime >= nextRescueSearch)
            {
                nextRescueSearch = Time.unscaledTime + 0.75f;
                rescueTarget = FindReachableDownedPlayer(player);
            }
            if (rescueTarget == null) return false;

            Vector2 delta = rescueTarget.transform.position - player.transform.position;
            if (delta.sqrMagnitude > 2.25f)
            {
                StopRescueChannel(player);
                if (Time.unscaledTime >= nextRescuePathCheck)
                {
                    nextRescuePathCheck = Time.unscaledTime + 1f;
                    if (!CanReachRescue(player, rescueTarget))
                    {
                        MarkRescueUnreachable(rescueTarget);
                        CancelRescue(player);
                        return false;
                    }
                }
                HoldShieldForRescue(player, enemy, delta);
                movement = Navigate(player, rescueDestination, 0.35f);
                if (movement.sqrMagnitude < 0.01f)
                {
                    MarkRescueUnreachable(rescueTarget);
                    CancelRescue(player);
                    return false;
                }
                return true;
            }

            ApplyMovement(player, Vector2.zero);
            HoldShieldForRescue(player, enemy, delta.sqrMagnitude > 0.001f ? delta : Vector2.up);
            if (rescueInteraction == null)
            {
                rescueInteraction = rescueTarget.GetComponentInChildren<RevivePlayerByInteraction>(true);
                if (rescueInteraction == null || !rescueInteraction.IsInteractable(player.gameObject))
                {
                    MarkRescueUnreachable(rescueTarget);
                    CancelRescue(player);
                    return false;
                }
                rescueInteraction.InteractionStart(player.gameObject);
                rescueCompleteAt = Time.unscaledTime + rescueInteraction.GetDelayTime();
                rescueChannelPosition = player.transform.position;
                Plugin.LogInfo($"AFK autopilot started teammate rescue: player={player.Name}, target={rescueTarget.Name}.");
            }
            if ((player.transform.position - rescueChannelPosition).sqrMagnitude > 0.09f)
            {
                Plugin.LogInfo($"AFK autopilot rescue interrupted by displacement: player={player.Name}, " +
                               $"target={rescueTarget.Name}.");
                StopRescueChannel(player);
                nextRescuePathCheck = 0f;
                return true;
            }
            if (!rescueTarget.IsDead)
            {
                CancelRescue(player);
                return false;
            }
            if (Time.unscaledTime >= rescueCompleteAt)
            {
                rescueInteraction.Interactive(player.gameObject);
                rescueInteraction = null;
                rescueTarget = null;
                rescueCompleteAt = 0f;
                nextRescueSearch = Time.unscaledTime + 1f;
            }
            return true;
        }

        private static PlayerAvatar FindReachableDownedPlayer(PlayerAvatar player)
        {
            PlayerAvatar[] candidates = PlayerSpawner.MultiplayerList
                .Where(spawner => spawner?.PlayerAvatar != null && spawner.PlayerAvatar != player &&
                                  spawner.PlayerAvatar.IsDead &&
                                  spawner.PlayerAvatar.currentFloorGuid == player.currentFloorGuid &&
                                  (!UnreachableRescues.TryGetValue(spawner.PlayerAvatar.netId, out float retryAt) ||
                                   Time.unscaledTime >= retryAt))
                .Select(spawner => spawner.PlayerAvatar)
                .OrderBy(target => (target.transform.position - player.transform.position).sqrMagnitude)
                .ToArray();
            foreach (PlayerAvatar candidate in candidates)
            {
                if (CanReachRescue(player, candidate)) return candidate;
                MarkRescueUnreachable(candidate);
            }
            return null;
        }

        private static bool CanReachRescue(PlayerAvatar player, PlayerAvatar target)
        {
            PathGrid grid = PathGrid.Current;
            if (grid == null || !grid.IsBuilt || target == null) return false;
            Vector3 targetPosition = target.transform.position;
            Vector3[] destinations =
            {
                targetPosition,
                targetPosition + Vector3.left * 1.1f,
                targetPosition + Vector3.right * 1.1f,
                targetPosition + Vector3.up * 1.1f,
                targetPosition + Vector3.down * 1.1f
            };
            foreach (Vector3 destination in destinations.OrderBy(value =>
                         (value - player.transform.position).sqrMagnitude))
            {
                RescuePath.Clear();
                if (!PathFinder.Find(grid, player.transform.position, destination, RescuePath) || RescuePath.Count == 0)
                    continue;
                rescueDestination = destination;
                return true;
            }
            Vector2 direct = targetPosition - player.transform.position;
            int mask = CombatManager.PathfindingObstacleLayerMask | CombatManager.BlockableLayerMask;
            if (direct.sqrMagnitude <= 225f &&
                !Physics2D.CircleCast(player.transform.position, 0.25f, direct.normalized,
                    Mathf.Sqrt(direct.sqrMagnitude), mask))
            {
                rescueDestination = targetPosition;
                return true;
            }
            return false;
        }

        private static void MarkRescueUnreachable(PlayerAvatar target)
        {
            if (target == null) return;
            UnreachableRescues[target.netId] = Time.unscaledTime + 5f;
            Plugin.LogInfo($"AFK autopilot postponed unreachable rescue: target={target.Name}, retry=5s.");
        }

        private static void HoldShieldForRescue(PlayerAvatar player, UnitAvatar enemy, Vector2 travelDirection)
        {
            WeaponControllerSimple controller = player.GetComponent<WeaponControllerSimple>();
            WeaponSimple_SwordAndShield shield = controller?.currentWeapon as WeaponSimple_SwordAndShield;
            if (shield == null || !player.CanMove || !player.IsAvailableGuard || !shield.isGuardAvailable ||
                player.MP <= 0 && player.GetCustomStatUnsafe("INFINITYMP") <= 0) return;
            Vector3 aim = enemy != null ? enemy.transform.position : player.transform.position + (Vector3)travelDirection;
            defenseAimOwner = enemy;
            defenseAimPoint = aim;
            LockAim(player, aim, enemy);
            if (!defenseHeld)
            {
                ReleaseCombatAbility(player, force: true);
                player.GetComponent<IntegratedActionController>()?.Cast(101, aim, enemy);
                heldCombatAbility = 101;
                defenseHeld = true;
                defenseStartedAt = Time.unscaledTime;
            }
            releaseCombatAbilityAt = Time.unscaledTime + 0.2f;
        }

        private static void CancelRescue(PlayerAvatar player)
        {
            StopRescueChannel(player);
            rescueTarget = null;
            nextRescuePathCheck = 0f;
            rescueDestination = Vector3.zero;
            if (defenseHeld) ReleaseDefense(player);
        }

        private static void StopRescueChannel(PlayerAvatar player)
        {
            if (rescueInteraction != null && player != null) rescueInteraction.InteractionStop(player.gameObject);
            rescueInteraction = null;
            rescueCompleteAt = 0f;
            rescueChannelPosition = Vector3.zero;
        }

        private static bool TryApproachPresetChoice(PlayerAvatar player, out Vector2 movement)
        {
            movement = Vector2.zero;
            FloorGenerator currentFloor = FloorGenerator.FindByGuid(player?.currentFloorGuid);
            bool anvilFloor = currentFloor != null
                ? currentFloor.floorMainEventType == EFloorMainEventType.Anvil
                : CurrentFloorEvent(player) == EFloorMainEventType.Anvil;
            if (!anvilFloor && (player.IsInBattle || WeaponPresetTerms().Length == 0 ||
                                CatchUpRewards.IsWeaponFullyEnhanced(player.spawner))) return false;
            if (player.IsInBattle || CatchUpRewards.IsWeaponFullyEnhanced(player.spawner)) return false;
            if (anvilFloor && ResolvedAnvilFloors.Contains(player.currentFloorGuid)) return false;
            if (anvilFloor && loggedAnvilFloor != player.currentFloorGuid)
            {
                loggedAnvilFloor = player.currentFloorGuid;
                Plugin.LogInfo($"AFK anvil floor detected: player={player.Name}, " +
                               $"floor={ShortGuid(player.currentFloorGuid)}, host={CanLeadParty(player)}, " +
                               $"presets={string.Join("|", WeaponPresetTerms())}.");
            }
            RefreshWorldObjectCache(player);
            Anvil anvil = cachedAnvil != null &&
                          (cachedAnvil.netId == 0 || !SkippedAnvils.Contains(cachedAnvil.netId))
                ? cachedAnvil : null;
            if (anvil == null)
            {
                LogAnvilWait(player, "waiting for the vanilla Anvil object to spawn");
                return anvilFloor;
            }
            if (Traverse.Create(anvil).Property("LocalEnhanced").GetValue<bool>())
            {
                if (anvilFloor) ResolvedAnvilFloors.Add(player.currentFloorGuid);
                return false;
            }
            Interactable interactable = anvil.GetComponent<Interactable>();
            if (interactable == null || !interactable.IsInteractable(player.gameObject))
            {
                LogAnvilWait(player, "waiting for the vanilla Anvil to become interactable");
                return anvilFloor;
            }
            Vector2 delta = anvil.transform.position - player.transform.position;
            if (delta.sqrMagnitude > 2.25f)
            {
                movement = Navigate(player, anvil.transform.position, 1.5f);
                return true;
            }
            if (Time.unscaledTime >= nextEntranceInteraction)
            {
                nextEntranceInteraction = Time.unscaledTime + 1f;
                interactable.Interactive(player.gameObject);
            }
            return true;
        }

        private static bool TryApproachQuestBoard(PlayerAvatar player, out Vector2 movement)
        {
            movement = Vector2.zero;
            if (player == null || player.IsInBattle) return false;
            RefreshWorldObjectCache(player);
            Interactable board = cachedQuestBoard;
            if (board == null) return false;
            Vector2 delta = board.transform.position - player.transform.position;
            if (delta.sqrMagnitude > 2.25f)
            {
                movement = Navigate(player, board.transform.position, 1.25f);
                return true;
            }
            if (Time.unscaledTime >= nextQuestBoardAction)
            {
                nextQuestBoardAction = Time.unscaledTime + 2f;
                board.Interactive(player.gameObject);
                Plugin.LogInfo($"AFK quest board opened: player={player.Name}, board={board.name}, " +
                               $"pos={board.transform.position}, floor={ShortGuid(player.currentFloorGuid)}.");
            }
            return true;
        }

        private static bool TrySelectQuestBoardEvent(PlayerAvatar player)
        {
            if (player == null || Time.unscaledTime < nextQuestBoardAction ||
                UIManager.Instance?.CurrentControlStack == null) return false;
            UI_NewWorldMapPanel panel = UIManager.Instance.GetElement<UI_NewWorldMapPanel>();
            if (panel == null || !panel.IsOpened || !UIManager.Instance.CurrentControlStack.Contains(panel)) return false;
            UI_NewWorldMapStageBoardEvent choice = Resources.FindObjectsOfTypeAll<UI_NewWorldMapStageBoardEvent>()
                .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                    candidate.floor != null && !candidate.IsCleared &&
                                    candidate.buttonBase != null && candidate.buttonBase.interactable)
                .OrderBy(candidate => FloorPresetRank(candidate.floor))
                .ThenBy(candidate => candidate.floor.nodeProgress)
                .FirstOrDefault();
            if (choice == null)
            {
                UI_WorldMapStageElement boss = Resources.FindObjectsOfTypeAll<UI_WorldMapStageElement>()
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                        candidate.floor != null &&
                                        candidate.floor.threatType == EFloorThreatType.Boss &&
                                        candidate.button != null && candidate.button.interactable)
                    .OrderBy(candidate => candidate.floor.nodeProgress)
                    .FirstOrDefault();
                if (boss == null) return false;
                nextQuestBoardAction = Time.unscaledTime + 3f;
                boss.Click();
                Plugin.LogInfo($"AFK quest board boss selected: player={player.Name}, " +
                               $"floor={ShortGuid(boss.floor.guid)}, progress={boss.floor.nodeProgress}, " +
                               $"threat={boss.floor.threatType}.");
                return true;
            }
            nextQuestBoardAction = Time.unscaledTime + 3f;
            choice.Click();
            Plugin.LogInfo($"AFK quest board event selected: player={player.Name}, floor={ShortGuid(choice.floor.guid)}, " +
                           $"event={choice.eventNameText?.text ?? "-"}, progress={choice.floor.nodeProgress}.");
            return true;
        }

        private static int FloorPresetRank(FloorData floor)
        {
            if (floor == null) return int.MaxValue;
            string[] presets = FloorPresetTerms();
            for (int i = 0; i < presets.Length; i++)
            {
                string eventType = presets[i].StartsWith("floor:", StringComparison.OrdinalIgnoreCase)
                    ? presets[i].Substring(6)
                    : presets[i];
                if (string.Equals(floor.mainEventType.ToString(), eventType, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return presets.Length;
        }

        private static EFloorMainEventType CurrentFloorEvent(PlayerAvatar player)
        {
            if (player == null || DungeonManager.Instance == null || string.IsNullOrEmpty(player.currentFloorGuid) ||
                !DungeonManager.Instance.generatedFloors.TryGetValue(player.currentFloorGuid, out FloorData floor))
                return EFloorMainEventType.Unknown;
            return floor.mainEventType;
        }

        private static bool TryApproachQuestObjective(PlayerAvatar player, out Vector2 movement)
        {
            movement = Vector2.zero;
            FloorGenerator floor = player != null ? FloorGenerator.FindByGuid(player.currentFloorGuid) : null;
            if (floor == null || string.IsNullOrEmpty(floor.questBoardEventId) ||
                floor.isQuestObjectiveCompleted) return false;

            RefreshQuestObjects(floor);
            BattleZone battleZone = cachedQuestBattleZones
                .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                    candidate.spawner != null && !candidate.spawner.isCleared)
                .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                .FirstOrDefault();
            if (battleZone != null)
            {
                Vector2 zoneLower = battleZone.spawner.detectArea_lb;
                Vector2 zoneUpper = battleZone.spawner.detectArea_rt;
                Vector3 zoneDestination = (Vector2.Min(zoneLower, zoneUpper) + Vector2.Max(zoneLower, zoneUpper)) * 0.5f;
                Vector2 playerPosition = player.transform.position;
                bool insideZone = playerPosition.x >= Mathf.Min(zoneLower.x, zoneUpper.x) &&
                                  playerPosition.y >= Mathf.Min(zoneLower.y, zoneUpper.y) &&
                                  playerPosition.x <= Mathf.Max(zoneLower.x, zoneUpper.x) &&
                                  playerPosition.y <= Mathf.Max(zoneLower.y, zoneUpper.y);
                if (!insideZone) movement = Navigate(player, zoneDestination, 0.25f);
                if (Time.unscaledTime >= nextEntranceDiagnostic)
                {
                    nextEntranceDiagnostic = Time.unscaledTime + 2f;
                    Plugin.LogInfo($"AFK quest battle zone: floor={ShortGuid(player.currentFloorGuid)}, " +
                                   $"event={floor.questBoardEventId}, progress={floor.currentObjectiveCount}, " +
                                   $"zone={battleZone.name}, spawned={battleZone.spawner.isSpawned}, " +
                                   $"cleared={battleZone.spawner.isCleared}, destination={zoneDestination}, inside={insideZone}.");
                }
                return true;
            }

            EnemySpawner spawner = cachedQuestSpawners
                .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                    candidate.spawnEnabled &&
                                    (candidate.parent != null ? candidate.parent : candidate.GetComponentInParent<FloorGenerator>()) == floor &&
                                    Traverse.Create(candidate).Field("battlePhase").GetValue<EnemySpawner.EBattlePhase>() ==
                                    EnemySpawner.EBattlePhase.None)
                .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                .FirstOrDefault();
            if (spawner == null)
            {
                EnemySpawnerInteractableTrigger trigger = cachedQuestTriggers
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                        candidate.IsInteractable(player.gameObject) &&
                                        IsOnCurrentQuestFloor(candidate, floor))
                    .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                    .FirstOrDefault();
                if (trigger != null)
                {
                    Vector2 delta = trigger.transform.position - player.transform.position;
                    if (delta.sqrMagnitude > 2.25f)
                    {
                        movement = Navigate(player, trigger.transform.position, 1.25f);
                        LogQuestObjectiveTrigger(player, floor, trigger.name, trigger.transform.position, false);
                    }
                    else if (Time.unscaledTime >= nextEntranceInteraction)
                    {
                        nextEntranceInteraction = Time.unscaledTime + 2f;
                        trigger.Interactive(player.gameObject);
                        LogQuestObjectiveTrigger(player, floor, trigger.name, trigger.transform.position, true);
                    }
                    return true;
                }
                if (Time.unscaledTime >= nextEntranceDiagnostic)
                {
                    nextEntranceDiagnostic = Time.unscaledTime + 2f;
                    Plugin.LogInfo($"AFK quest objective waiting: floor={ShortGuid(player.currentFloorGuid)}, " +
                                   $"event={floor.questBoardEventId}, progress={floor.currentObjectiveCount}, " +
                                   $"spawner=none, battleZones={cachedQuestBattleZones.Count}, " +
                                   $"registeredSpawners={cachedQuestSpawners.Count}, triggers={cachedQuestTriggers.Count}, " +
                                   $"connections={DescribeQuestConnections(player)}.");
                }
                return true;
            }

            Vector2 lower = (Vector2)spawner.transform.position + spawner.detectArea_lb;
            Vector2 upper = (Vector2)spawner.transform.position + spawner.detectArea_rt;
            Vector3 destination = (Vector2.Min(lower, upper) + Vector2.Max(lower, upper)) * 0.5f;
            Vector2 position = player.transform.position;
            bool inside = position.x >= Mathf.Min(lower.x, upper.x) && position.y >= Mathf.Min(lower.y, upper.y) &&
                          position.x <= Mathf.Max(lower.x, upper.x) && position.y <= Mathf.Max(lower.y, upper.y);
            if (!inside) movement = Navigate(player, destination, 0.25f);
            if (Time.unscaledTime >= nextEntranceDiagnostic)
            {
                nextEntranceDiagnostic = Time.unscaledTime + 2f;
                Plugin.LogInfo($"AFK quest objective trigger: floor={ShortGuid(player.currentFloorGuid)}, " +
                               $"event={floor.questBoardEventId}, progress={floor.currentObjectiveCount}, " +
                               $"spawner={spawner.name}, destination={destination}, inside={inside}.");
            }
            return true;
        }

        private static void RefreshQuestObjects(FloorGenerator floor)
        {
            if (floor == null) return;
            if (questObjectScanFloor == floor.guid && Time.unscaledTime < nextQuestObjectScan) return;
            questObjectScanFloor = floor.guid;
            nextQuestObjectScan = Time.unscaledTime + 5f;

            List<BattleZone> registered = Traverse.Create(floor).Field("allBattleZones").GetValue<List<BattleZone>>();
            cachedQuestBattleZones = (registered ?? new List<BattleZone>())
                .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy)
                .ToList();
            if (cachedQuestBattleZones.Count > 0)
            {
                cachedQuestSpawners.Clear();
                cachedQuestTriggers.Clear();
                return;
            }
            cachedQuestSpawners = Resources.FindObjectsOfTypeAll<EnemySpawner>()
                .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                    IsOnCurrentQuestFloor(candidate, floor))
                .ToList();
            cachedQuestTriggers = Resources.FindObjectsOfTypeAll<EnemySpawnerInteractableTrigger>()
                .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                    IsOnCurrentQuestFloor(candidate, floor))
                .ToList();

            if (cachedQuestBattleZones.Count == 0)
                cachedQuestBattleZones = Resources.FindObjectsOfTypeAll<BattleZone>()
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                        (Traverse.Create(candidate).Field("floorGenerator").GetValue<FloorGenerator>() == floor ||
                                         candidate.transform.IsChildOf(floor.transform) ||
                                         candidate.spawner != null && candidate.spawner.transform.IsChildOf(floor.transform)))
                    .ToList();
        }

        private static bool IsOnCurrentQuestFloor(Component component, FloorGenerator floor)
        {
            FloorGenerator parent = component?.GetComponentInParent<FloorGenerator>();
            return parent == null || parent == floor || parent.guid == floor.guid;
        }

        private static string DescribeQuestConnections(PlayerAvatar player)
        {
            if (player == null || DungeonManager.Instance == null ||
                !DungeonManager.Instance.generatedFloors.TryGetValue(player.currentFloorGuid, out FloorData current))
                return "-";
            return string.Join("|", (current.connectionToOtherFloors ?? Array.Empty<string>())
                .Select(guid => DungeonManager.Instance.generatedFloors.TryGetValue(guid, out FloorData floor)
                    ? ShortGuid(floor.guid) + ":" + floor.nodeProgress + ":" + floor.mainEventType
                    : ShortGuid(guid) + ":missing"));
        }

        private static void LogQuestObjectiveTrigger(PlayerAvatar player, FloorGenerator floor,
            string trigger, Vector3 destination, bool activated)
        {
            if (Time.unscaledTime < nextEntranceDiagnostic) return;
            nextEntranceDiagnostic = Time.unscaledTime + 2f;
            Plugin.LogInfo($"AFK quest objective interactable: floor={ShortGuid(player.currentFloorGuid)}, " +
                           $"event={floor.questBoardEventId}, trigger={trigger}, destination={destination}, " +
                           $"activated={activated}.");
        }

        private static void LogAnvilWait(PlayerAvatar player, string reason)
        {
            if (player == null || CurrentFloorEvent(player) != EFloorMainEventType.Anvil ||
                !LoggedAnvilWaitFloors.Add(player.currentFloorGuid)) return;
            Plugin.LogInfo($"AFK autopilot holding Anvil floor: player={player.Name}, " +
                           $"floor={player.currentFloorGuid.Substring(0, Math.Min(8, player.currentFloorGuid.Length))}, " +
                           $"reason={reason}, presets={string.Join("|", WeaponPresetTerms())}.");
        }

        private static bool CanLeadParty(PlayerAvatar player)
        {
            int count = PlayerSpawner.MultiplayerList?.Count ?? 0;
            return count <= 1 || NetworkServer.active || player?.spawner != null && player.spawner.isHost;
        }

        private static bool TryApproachBossTrigger(PlayerAvatar player, out Vector2 movement)
        {
            movement = Vector2.zero;
            RefreshWorldObjectCache(player);
            BossSpawner spawner = cachedBossSpawner;
            if (spawner == null || spawner.IsCleared || spawner.IsBossBattleInProgress)
            {
                cachedBossSpawner = null;
                return false;
            }
            int spawnerId = spawner.GetInstanceID();
            if (CompletedBossSpawners.Contains(spawnerId))
            {
                cachedBossSpawner = null;
                return false;
            }
            if (spawner.bossObject != null) StartedBossSpawners.Add(spawnerId);
            if (StartedBossSpawners.Contains(spawnerId) &&
                (spawner.bossObject == null || spawner.bossObject.IsDead))
            {
                Plugin.LogInfo($"AFK ignored completed client-side BossSpawner: floor={ShortGuid(player.currentFloorGuid)}, " +
                               $"spawner={spawner.name}, boss={(spawner.bossObject != null ? DescribeUnit(spawner.bossObject) : "destroyed")}.");
                cachedBossSpawner = null;
                CompletedBossSpawners.Add(spawnerId);
                return false;
            }

            Vector2 lower = (Vector2)spawner.transform.position + spawner.detectArea_lb;
            Vector2 upper = (Vector2)spawner.transform.position + spawner.detectArea_rt;
            Vector2 position = player.transform.position;
            Vector2 min = Vector2.Min(lower, upper);
            Vector2 max = Vector2.Max(lower, upper);
            Vector2 margin = new Vector2(Mathf.Min(1.5f, (max.x - min.x) * 0.25f),
                Mathf.Min(1.5f, (max.y - min.y) * 0.25f));
            Vector2 safeMin = min + margin;
            Vector2 safeMax = max - margin;
            if (position.x >= safeMin.x && position.y >= safeMin.y &&
                position.x <= safeMax.x && position.y <= safeMax.y)
            {
                ResetPath();
                if (spawner.bossObject != null)
                {
                    StartedBossSpawners.Add(spawnerId);
                    waitingBossSpawnerId = 0;
                    waitingBossSince = 0f;
                }
                else if (waitingBossSpawnerId != spawnerId)
                {
                    waitingBossSpawnerId = spawnerId;
                    waitingBossSince = Time.unscaledTime;
                }
                else if (Time.unscaledTime - waitingBossSince >= 10f)
                {
                    CompletedBossSpawners.Add(spawnerId);
                    cachedBossSpawner = null;
                    Plugin.LogInfo($"AFK abandoned stale BossSpawner after waiting in trigger: " +
                                   $"floor={ShortGuid(player.currentFloorGuid)}, spawner={spawner.name}.");
                    return false;
                }
                return true;
            }
            if (bossTriggerFloor != player.currentFloorGuid || bossTriggerDestination == Vector3.zero ||
                Time.unscaledTime >= nextBossTriggerSearch)
            {
                nextBossTriggerSearch = Time.unscaledTime + 1f;
                if (!TryFindReachableBossTrigger(player, safeMin, safeMax, out bossTriggerDestination))
                {
                    LogPathDiagnostic("no-reachable-boss-trigger-cell", player,
                        spawner.transform.position + (Vector3)((spawner.detectArea_lb + spawner.detectArea_rt) * 0.5f));
                    return true;
                }
                bossTriggerFloor = player.currentFloorGuid;
                Plugin.LogInfo($"AFK boss trigger selected: floor={ShortGuid(player.currentFloorGuid)}, " +
                               $"destination={bossTriggerDestination}, area={lower}->{upper}, safe={safeMin}->{safeMax}.");
            }
            movement = Navigate(player, bossTriggerDestination, 0.1f);
            return true;
        }

        private static bool TryFindReachableBossTrigger(PlayerAvatar player, Vector2 lower, Vector2 upper,
            out Vector3 destination)
        {
            destination = Vector3.zero;
            PathGrid grid = PathGrid.Current;
            if (grid == null || !grid.IsBuilt) return false;
            Vector2 min = Vector2.Min(lower, upper);
            Vector2 max = Vector2.Max(lower, upper);
            Vector2 gridA = grid.CellToWorld(0, 0);
            Vector2 gridB = grid.CellToWorld(grid.Width - 1, grid.Height - 1);
            Vector2 gridMin = Vector2.Min(gridA, gridB);
            Vector2 gridMax = Vector2.Max(gridA, gridB);
            Vector2 clampedMin = new Vector2(Mathf.Clamp(min.x, gridMin.x, gridMax.x),
                Mathf.Clamp(min.y, gridMin.y, gridMax.y));
            Vector2 clampedMax = new Vector2(Mathf.Clamp(max.x, gridMin.x, gridMax.x),
                Mathf.Clamp(max.y, gridMin.y, gridMax.y));
            if (!grid.WorldToCell(clampedMin, out int minX, out int minY) ||
                !grid.WorldToCell(clampedMax, out int maxX, out int maxY)) return false;
            List<Vector3> candidates = new List<Vector3>();
            for (int y = Math.Min(minY, maxY); y <= Math.Max(minY, maxY); y++)
                for (int x = Math.Min(minX, maxX); x <= Math.Max(minX, maxX); x++)
                {
                    if (grid.IsBlocked(x, y)) continue;
                    Vector2 world = grid.CellToWorld(x, y);
                    if (world.x >= min.x && world.x <= max.x && world.y >= min.y && world.y <= max.y)
                        candidates.Add(world);
                }
            foreach (Vector3 candidate in candidates.OrderBy(point =>
                         (point - player.transform.position).sqrMagnitude))
            {
                BossTriggerPath.Clear();
                if (!PathFinder.Find(grid, player.transform.position, candidate, BossTriggerPath) ||
                    BossTriggerPath.Count == 0) continue;
                destination = candidate;
                return true;
            }
            return false;
        }

        private static bool TryApproachNextEntrance(PlayerAvatar player, out Vector2 movement)
        {
            movement = Vector2.zero;
            if (player.IsInBattle && !CompletedBossFloors.Contains(player.currentFloorGuid)) return false;
            RefreshWorldObjectCache(player);
            Interactable entrance = cachedEntrance;
            if (entrance == null) return false;
            bool namedUpFloorMover = IsNamedUpFloorMover(entrance);
            bool questReturnFloorMover = IsQuestReturnFloorMover(entrance);
            if (!entrance.IsInteractable(player.gameObject) && !namedUpFloorMover && !questReturnFloorMover)
            {
                LogEntranceDiagnostic(player, entrance, "not-interactable");
                return false;
            }
            if (namedUpFloorMover && !entrance.IsInteractable(player.gameObject))
                LogEntranceDiagnostic(player, entrance, "named-up-floor-mover-bypass");
            Vector2 delta = entrance.transform.position - player.transform.position;
            int entranceId = entrance.GetInstanceID();
            bool atApproach = entranceApproachId == entranceId && entranceApproachDestination != Vector3.zero &&
                              (entranceApproachDestination - player.transform.position).sqrMagnitude <= 0.25f;
            if (namedUpFloorMover && delta.sqrMagnitude > 2.25f)
            {
                movement = delta.normalized;
                return true;
            }
            if (delta.sqrMagnitude > 2.25f && !atApproach)
            {
                if (entranceApproachId != entranceId || Time.unscaledTime >= nextEntranceApproachSearch ||
                    entranceApproachDestination == Vector3.zero)
                {
                    entranceApproachId = entranceId;
                    nextEntranceApproachSearch = Time.unscaledTime + 1f;
                    if (!TryFindEntranceApproach(player, entrance, out entranceApproachDestination))
                    {
                        Vector2 direct = entrance.transform.position - player.transform.position;
                        if (direct.sqrMagnitude > 0.01f)
                        {
                            movement = direct.normalized;
                            LogEntranceDiagnostic(player, entrance, "direct-fallback-no-path");
                            return true;
                        }
                        LogEntranceDiagnostic(player, entrance, "no-reachable-approach");
                        return true;
                    }
                }
                movement = Navigate(player, entranceApproachDestination, 0.2f);
                return true;
            }
            if (Time.unscaledTime >= nextEntranceInteraction)
            {
                nextEntranceInteraction = Time.unscaledTime + 2f;
                if (namedUpFloorMover)
                {
                    entrance.Interactive(player.gameObject);
                    Plugin.LogInfo($"AFK autopilot used named up entrance: player={player.Name}, " +
                                   $"entrance={entrance.name}, interactable={entrance.IsInteractable(player.gameObject)}.");
                    return true;
                }
                if (questReturnFloorMover)
                {
                    entrance.Interactive(player.gameObject);
                    Plugin.LogInfo($"AFK autopilot used quest return entrance: player={player.Name}, " +
                                   $"entrance={entrance.name}, interactable={entrance.IsInteractable(player.gameObject)}.");
                    return true;
                }
                DungeonStair stair = entrance.GetComponent<DungeonStair>() ??
                                     entrance.GetComponentInParent<DungeonStair>() ??
                                     entrance.GetComponentInChildren<DungeonStair>(true);
                if (stair != null)
                {
                    if (TryMoveToTeammateFloor(player))
                    {
                        // The host has already moved the party route; this player is catching up.
                    }
                    else if (!ArePlayersReadyAtStair(player, entrance, out string waitReason))
                    {
                        LogEntranceDiagnostic(player, entrance, "waiting-party-" + waitReason);
                        player.spawner?.CreateMultiplayPing(allowOnMultiplayer: true, entrance.transform.position);
                    }
                    else if (CanLeadParty(player))
                    {
                        TryMoveToConnectedFloor(player);
                    }
                    else
                    {
                        LogEntranceDiagnostic(player, entrance, "waiting-host-route-selection");
                    }
                }
                else
                {
                    entrance.Interactive(player.gameObject);
                    GoToNextPlaceTogether together = entrance.GetComponentInParent<GoToNextPlaceTogether>() ??
                                                     entrance.GetComponentInChildren<GoToNextPlaceTogether>(true);
                    Plugin.LogInfo($"AFK autopilot used next entrance: player={player.Name}, entrance={entrance.name}, " +
                                   $"together={together != null}, checkDistance={(together != null ? together.checkDistance : 0f):0.0}.");
                }
            }
            return true;
        }

        private static bool ArePlayersReadyAtStair(PlayerAvatar local, Interactable entrance, out string reason)
        {
            reason = "ready";
            if (local == null || entrance == null || PlayerSpawner.MultiplayerList == null) return true;
            foreach (PlayerSpawner peer in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar avatar = peer?.PlayerAvatar;
                if (avatar == null || avatar.IsDead) continue;
                if (avatar.IsInBattle)
                {
                    reason = "battle";
                    return false;
                }
                if (avatar.localDataStorage != null && avatar.localDataStorage.preparingUIThings)
                {
                    reason = "preparing";
                    return false;
                }
                if (Vector2.Distance(avatar.transform.position, entrance.transform.position) > 10f)
                {
                    reason = "distance";
                    return false;
                }
            }
            return true;
        }

        private static bool TryFindEntranceApproach(PlayerAvatar player, Interactable entrance,
            out Vector3 destination)
        {
            destination = Vector3.zero;
            PathGrid grid = PathGrid.Current;
            if (grid == null || !grid.IsBuilt || entrance == null) return false;
            List<Vector3> path = new List<Vector3>();
            Vector3 best = Vector3.zero;
            float bestScore = float.MaxValue;
            float[] radii = { 0.75f, 1.25f, 1.75f, 2.25f };
            foreach (float radius in radii)
            {
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI * 2f / 16f;
                    Vector3 point = entrance.transform.position +
                                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!grid.WorldToCell(point, out int x, out int y) || grid.IsBlocked(x, y)) continue;
                    Vector3 world = grid.CellToWorld(x, y);
                    path.Clear();
                    if (!PathFinder.Find(grid, player.transform.position, world, path) || path.Count == 0) continue;
                    float score = path.Count + (world - entrance.transform.position).sqrMagnitude * 0.1f;
                    if (score >= bestScore) continue;
                    best = world;
                    bestScore = score;
                }
            }
            if (bestScore == float.MaxValue) return false;
            destination = best;
            Plugin.LogInfo($"AFK entrance approach selected: entrance={entrance.name}, target={destination}, " +
                           $"interactionDistance={Vector2.Distance(destination, entrance.transform.position):0.0}.");
            return true;
        }

        private static bool TryMoveToTeammateFloor(PlayerAvatar player)
        {
            PlayerAvatar teammate = FindConnectedForwardTeammate(player);
            if (teammate?.spawner == null || !teammate.spawner.isHost) return false;
            FloorData current = DungeonManager.Instance.generatedFloors[player.currentFloorGuid];
            DungeonManager.Instance.MoveFloor(player, teammate.currentFloorGuid, "FLOORSTARTING", 0);
            Plugin.LogInfo($"AFK autopilot followed teammate to connected floor: player={player.Name}, " +
                           $"from={current.guid}, to={teammate.currentFloorGuid}.");
            return true;
        }

        private static PlayerAvatar FindConnectedForwardTeammate(PlayerAvatar player)
        {
            if (player == null || DungeonManager.Instance == null || PlayerSpawner.MultiplayerList == null ||
                !DungeonManager.Instance.generatedFloors.TryGetValue(player.currentFloorGuid, out FloorData current))
                return null;
            HashSet<string> forward = new HashSet<string>((current.connectionToOtherFloors ?? Array.Empty<string>())
                .Where(guid => DungeonManager.Instance.generatedFloors.TryGetValue(guid, out FloorData floor) &&
                               floor.nodeProgress > current.nodeProgress));
            return PlayerSpawner.MultiplayerList
                .Where(peer => peer?.PlayerAvatar != null && peer.PlayerAvatar != player && !peer.PlayerAvatar.IsDead &&
                               forward.Contains(peer.PlayerAvatar.currentFloorGuid))
                .OrderByDescending(peer => peer.isHost)
                .Select(peer => peer.PlayerAvatar)
                .FirstOrDefault();
        }

        private static bool TryMoveToConnectedFloor(PlayerAvatar player)
        {
            if (DungeonManager.Instance == null ||
                !DungeonManager.Instance.generatedFloors.TryGetValue(player.currentFloorGuid, out FloorData current)) return false;
            List<FloorData> forward = (current.connectionToOtherFloors ?? new string[0])
                .Select(guid => DungeonManager.Instance.generatedFloors.TryGetValue(guid, out FloorData floor) ? floor : null)
                .Where(floor => floor != null && floor.nodeProgress > current.nodeProgress)
                .ToList();
            if (forward.Count == 0)
            {
                Plugin.LogInfo($"AFK stair has no connected forward floor: player={player.Name}, " +
                               $"floor={ShortGuid(player.currentFloorGuid)}.");
                return false;
            }
            int nextProgress = forward.Min(floor => floor.nodeProgress);
            List<FloorData> branches = forward.Where(floor => floor.nodeProgress == nextProgress).ToList();
            if (CatchUpRewards.IsWeaponFullyEnhanced(player.spawner))
            {
                List<FloorData> nonAnvilBranches = branches
                    .Where(floor => floor.mainEventType != EFloorMainEventType.Anvil)
                    .ToList();
                if (nonAnvilBranches.Count > 0) branches = nonAnvilBranches;
            }
            FloorData next = null;
            foreach (string term in FloorPresetTerms())
            {
                string eventType = term.StartsWith("floor:", StringComparison.OrdinalIgnoreCase)
                    ? term.Substring(6)
                    : term;
                next = branches.FirstOrDefault(floor =>
                    string.Equals(floor.mainEventType.ToString(), eventType, StringComparison.OrdinalIgnoreCase));
                if (next != null) break;
            }
            if (next == null) next = branches[UnityEngine.Random.Range(0, branches.Count)];
            if (next == null) return false;
            if (!CanLeadParty(player)) return false;
            player.MoveFloorViaWorldmap(next.guid, delayOnMultiplayer: true, "FLOORSTARTING");
            Plugin.LogInfo($"AFK autopilot selected connected floor: player={player.Name}, " +
                            $"from={current.guid}, to={next.guid}, event={next.mainEventType}, " +
                            $"progress={current.nodeProgress}->{next.nodeProgress}.");
            return true;
        }

        private static void TryPickUpNearbyItem(PlayerAvatar player)
        {
            if (Item.managedItemInstances == null || player.Inventory == null) return;
            if (ignoredDropFloor != player.currentFloorGuid)
            {
                IgnoredDroppedItems.Clear();
                ignoredDropFloor = player.currentFloorGuid;
            }
            Item item = Item.managedItemInstances
                .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                    !IgnoredDroppedItems.Contains(candidate.itemInstanceID) &&
                                    (!candidate.isBound || candidate.isOwned) &&
                                    ((Vector2)(candidate.transform.position - player.transform.position)).sqrMagnitude <= 2.25f)
                .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                .FirstOrDefault();
            if (item == null) return;
            ItemEntity entity = ItemDatabase.FindItemById(item.itemEntityID);
            if (entity == null) return;
            ItemAdditionCheckResult result = entity.type == EItemType.Potion && player.Inventory.numberOfPotionStorage > 0
                ? player.Inventory.CanAddPotionStorage(entity, item.itemQuantity)
                : player.Inventory.CanAddItem(entity, item.itemQuantity);
            if (result == ItemAdditionCheckResult.Success || result == ItemAdditionCheckResult.Success_Stack)
                item.Acquire(player.Inventory);
        }

        private static void TryAutoArrangeInventory(PlayerAvatar player, UnitAvatar enemy)
        {
            if (!Plugin.autoArrangeInventory.Value)
            {
                lastInventorySignature = null;
                pendingInventorySignature = null;
                arrangeInventoryAt = 0f;
                return;
            }
            GridInventory inventory = player.Inventory;
            if (inventory == null) return;
            FloorGenerator floor = FloorGenerator.FindByGuid(player.currentFloorGuid);
            if (floor != null && !string.IsNullOrEmpty(floor.questBoardEventId))
            {
                pendingInventorySignature = null;
                arrangeInventoryAt = 0f;
                return;
            }
            if (Time.unscaledTime < nextInventorySignatureCheck) return;
            nextInventorySignatureCheck = Time.unscaledTime + 0.5f;
            string signature = InventorySignature(inventory);
            if (signature != pendingInventorySignature && signature != lastInventorySignature)
            {
                pendingInventorySignature = signature;
                arrangeInventoryAt = Time.unscaledTime + 2f;
            }
            if (signature == lastInventorySignature || Time.unscaledTime < arrangeInventoryAt ||
                Time.unscaledTime < nextInventoryArrangeAllowed ||
                player.IsInBattle || enemy != null) return;

            lastInventorySignature = signature;
            pendingInventorySignature = null;
            arrangeInventoryAt = 0f;
            nextInventoryArrangeAllowed = Time.unscaledTime + 5f;
            inventory.RequestAutoArrangeInventoryForBestCharmLevels(1, allowTabletRotation: true);
            Plugin.LogInfo($"AFK autopilot requested inventory optimization: player={player.Name}, " +
                           $"items={inventory.inventoryMatrix.Count}, storage={inventory.CurrentInventoryStorage}.");
        }

        private static string InventorySignature(GridInventory inventory)
        {
            IEnumerable<NewItemOwnInstance> items = inventory.inventoryMatrix.Values
                .Where(item => item != null)
                .OrderBy(item => item.InstanceID);
            return inventory.CurrentInventoryStorage + ":" + inventory.charms.Count + ":" +
                   inventory.stoneTablets.Count + ":" + string.Join("|", items.Select(item =>
                item.InstanceID + "," + item.EntityID + "," + item.Quantity));
        }

        private static void TryClaimFavoriteReward(PlayerAvatar player)
        {
            if (player.Inventory == null || Time.unscaledTime < nextRewardAction) return;
            bool forceTabletOnFloor = IsMutualTabletChoiceFloor(player);
            if (forceTabletOnFloor && cachedReward != null && cachedReward.type != Sephirite.Type.TABLET)
                cachedReward = null;
            if (cachedReward == null || !cachedReward.gameObject.activeInHierarchy || cachedReward.isAcquired)
            {
                cachedReward = null;
                if (Time.unscaledTime < nextRewardScan) return;
                nextRewardScan = Time.unscaledTime + 1f;
                cachedReward = Resources.FindObjectsOfTypeAll<Sephirite>()
                    .Where(reward => reward != null && reward.gameObject.activeInHierarchy &&
                                     reward.isOwned && !reward.isAcquired)
                    .OrderByDescending(reward => IsForcedTabletChoice(player, reward))
                    .FirstOrDefault();
            }
            Sephirite sephirite = cachedReward;
            if (sephirite == null) return;
            if (!sephirite.isGenerated)
            {
                sephirite.CmdGenerateItemForOpen(player.gameObject);
                nextRewardAction = Time.unscaledTime + 1f;
                return;
            }
            bool forcedTablet = IsForcedTabletChoice(player, sephirite);
            if (Plugin.autoChoiceStrategy.Value == 2 && !forcedTablet) return;
            int selected = FindRewardIndex(sephirite);
            if (selected >= 0)
            {
                ItemEntity entity = ItemDatabase.FindItemById(sephirite.Rewards[selected].entityID);
                ItemAdditionCheckResult result = player.Inventory.CanAddItem(entity, 1);
                ItemPosition destination = new ItemPosition(-1, -1);
                if (result != ItemAdditionCheckResult.Success && result != ItemAdditionCheckResult.Success_Stack)
                {
                    if (result != ItemAdditionCheckResult.Full) return;
                    NewItemOwnInstance discard = FindRewardReplacement(player.Inventory, entity);
                    if (discard == null && Plugin.autoFullInventoryStrategy.Value == 2)
                        discard = FindForcedRewardReplacement(player.Inventory);
                    if (discard == null) return;
                    destination = discard.Position;
                    IgnoredDroppedItems.Add(discard.InstanceID);
                    ignoredDropFloor = player.currentFloorGuid;
                    player.Inventory.ThrowItem(discard.XIdx, discard.YIdx,
                        player.transform.position + (Vector3)(UnityEngine.Random.insideUnitCircle * 1.5f));
                    Plugin.LogInfo($"AFK autopilot made room for reward: player={player.Name}, " +
                                   $"discarded={discard.EntityID}/{discard.Name}, reward={entity.id}/{entity.Name}.");
                }
                player.SelectSephiriteReward(sephirite, selected, destination);
                nextRewardAction = Time.unscaledTime + 1.5f;
                Plugin.LogInfo($"AFK autopilot selected reward: player={player.Name}, item={entity.id}, " +
                               $"rarity={entity.rarity}, forcedTabletChoice={forcedTablet}.");
            }
        }

        private static bool IsForcedTabletChoice(PlayerAvatar player, Sephirite reward) =>
            player != null && reward != null && reward.type == Sephirite.Type.TABLET &&
            IsMutualTabletChoiceFloor(player);

        private static bool IsMutualTabletChoiceFloor(PlayerAvatar player) =>
            player != null && CurrentFloorEvent(player) == EFloorMainEventType.StoneTablet &&
            (FusionCompensation.IsObservedFloor(player.currentFloorGuid) ||
             Resources.FindObjectsOfTypeAll<TabletMix_Personal>().Any(mix => mix != null &&
                 mix.gameObject.activeInHierarchy && mix.isOwned));

        private static NewItemOwnInstance FindRewardReplacement(GridInventory inventory, ItemEntity reward)
        {
            int strategy = Plugin.autoFullInventoryStrategy.Value;
            if (strategy <= 0 || reward == null ||
                reward.type != EItemType.Charm && reward.type != EItemType.StoneTablet) return null;

            return inventory.inventoryMatrix.Values
                .Where(item => IsSafeReplacement(item, reward, strategy))
                .OrderBy(item => item.Entity.type == EItemType.Charm ? 1 : 0)
                .ThenBy(item => item.Entity.rarity)
                .ThenBy(item => item.Quantity)
                .ThenBy(item => item.Entity.cost)
                .FirstOrDefault();
        }

        private static bool IsSafeReplacement(NewItemOwnInstance item, ItemEntity reward, int strategy)
        {
            ItemEntity entity = item?.Entity;
            if (entity == null || item.Quantity != 1 || item.YIdx >= 100 || entity.cannotThrow || entity.isDestroyedOnDiscard ||
                entity.itemBehaviour != ItemEntity.EItemBehaviour.None || entity.type == EItemType.StoneTablet ||
                entity.type == EItemType.Potion || entity.type == EItemType.Identifiable ||
                RewardPresetTerms().Any(term => Matches(entity, term))) return false;
            if (DungeonManager.Instance != null &&
                (DungeonManager.Instance.GetGlobalItemStatValue(item.InstanceID, "Destructible") == "1" ||
                 !string.IsNullOrEmpty(DungeonManager.Instance.GetGlobalItemStatValue(item.InstanceID, "Bound")))) return false;

            if (entity.type == EItemType.Charm)
                return !IsFavorite(entity.id) && item.Charm != null && !item.Charm.IsEffectEnabled &&
                       reward.rarity > entity.rarity;
            if (strategy < 2) return false;
            bool ordinary = entity.type == EItemType.Misc || entity.type == EItemType.ThrowingWeapon ||
                            entity.type == EItemType.Food || entity.type == EItemType.Scroll;
            return ordinary && reward.rarity >= entity.rarity;
        }

        private static NewItemOwnInstance FindForcedRewardReplacement(GridInventory inventory)
        {
            return inventory.inventoryMatrix.Values
                .Where(IsLegallyDroppableSingleItem)
                .OrderBy(ForcedReplacementClass)
                .ThenBy(item => item.Entity.type == EItemType.StoneTablet
                    ? TabletRemovalImpact(inventory, item.StoneTablet)
                    : 0f)
                .ThenBy(item => item.Entity.rarity)
                .ThenBy(item => item.Entity.cost)
                .FirstOrDefault();
        }

        private static bool IsLegallyDroppableSingleItem(NewItemOwnInstance item)
        {
            ItemEntity entity = item?.Entity;
            if (entity == null || item.Quantity != 1 || item.YIdx >= 100 || entity.cannotThrow ||
                entity.isDestroyedOnDiscard || entity.itemBehaviour != ItemEntity.EItemBehaviour.None ||
                entity.type == EItemType.Potion || entity.type == EItemType.Identifiable) return false;
            if (DungeonManager.Instance == null) return true;
            return DungeonManager.Instance.GetGlobalItemStatValue(item.InstanceID, "Destructible") != "1" &&
                   string.IsNullOrEmpty(DungeonManager.Instance.GetGlobalItemStatValue(item.InstanceID, "Bound"));
        }

        private static int ForcedReplacementClass(NewItemOwnInstance item)
        {
            ItemEntity entity = item.Entity;
            bool protectedChoice = IsFavorite(entity.id) || RewardPresetTerms().Any(term => Matches(entity, term));
            if (entity.type == EItemType.Misc || entity.type == EItemType.ThrowingWeapon ||
                entity.type == EItemType.Food || entity.type == EItemType.Scroll) return protectedChoice ? 4 : 0;
            if (entity.type == EItemType.Charm)
            {
                if (protectedChoice) return 5;
                return item.Charm != null && item.Charm.IsEffectEnabled ? 2 : 1;
            }
            if (entity.type == EItemType.StoneTablet) return 3;
            return 6;
        }

        private static float TabletRemovalImpact(GridInventory inventory, StoneTablet tablet)
        {
            if (tablet == null || !tablet.IsApplied) return -1f;
            float current = ScoreCharms(inventory, null);
            float without = ScoreCharms(inventory, tablet);
            return current - without;
        }

        private static float ScoreCharms(GridInventory inventory, StoneTablet removedTablet)
        {
            int enabledCount = 0;
            int disabledCount = 0;
            int enabledLevels = 0;
            int rawLevels = 0;
            int overCapLevels = 0;
            int negativeLevels = 0;
            foreach (Charm_Basic charm in inventory.charms.Values)
            {
                if (charm == null) continue;
                ItemPosition position = new ItemPosition(charm.xIdx, charm.yIdx);
                int level = VirtualLevelWithoutTablet(inventory, removedTablet, position);
                rawLevels += level;
                if (level < 0) negativeLevels += -level;

                bool enabled = removedTablet == null
                    ? charm.IsEffectEnabled
                    : IsCharmEnabledWithoutTablet(inventory, charm, removedTablet, position, level);
                if (enabled)
                {
                    enabledCount++;
                    enabledLevels += Mathf.Clamp(level, 0, charm.maxLevel);
                }
                else disabledCount++;
                if (level > charm.maxLevel) overCapLevels += level - charm.maxLevel;
            }
            return enabledLevels * 10000f + enabledCount * 1000f + rawLevels * 10f + overCapLevels -
                   disabledCount * 750f - negativeLevels * 250f;
        }

        private static int VirtualLevelWithoutTablet(GridInventory inventory, StoneTablet tablet, ItemPosition position)
        {
            inventory.levelMatrix.TryGetValue(position, out int currentLevel);
            if (tablet == null || !tablet.IsApplied) return currentLevel;

            int levelContribution = 0;
            int multiplierContribution = 0;
            foreach (StoneTablet.AdditionEffectData effect in tablet.EffectRange)
            {
                if (effect.position != position) continue;
                if (effect.effectType == StoneTablet.EffectType.IncreaseConstLevel)
                    levelContribution += effect.levelParam;
                else if (effect.effectType == StoneTablet.EffectType.MultiplyConstLevel)
                    multiplierContribution += effect.levelParam;
            }
            inventory.multiplyLevelMatrix.TryGetValue(position, out int totalMultiplier);
            int baseLevel = totalMultiplier != 0 ? currentLevel / totalMultiplier : currentLevel;
            baseLevel -= levelContribution;
            int remainingMultiplier = totalMultiplier - multiplierContribution;
            return remainingMultiplier != 0 ? baseLevel * remainingMultiplier : baseLevel;
        }

        private static bool IsCharmEnabledWithoutTablet(GridInventory inventory, Charm_Basic charm,
            StoneTablet tablet, ItemPosition position, int level)
        {
            inventory.disableMatrix.TryGetValue(position, out int disableCount);
            inventory.ignoreCriteriaMatrix.TryGetValue(position, out int ignoreCount);
            foreach (StoneTablet.AdditionEffectData effect in tablet.EffectRange)
            {
                if (effect.position != position) continue;
                if (effect.effectType == StoneTablet.EffectType.Disable) disableCount--;
                else if (effect.effectType == StoneTablet.EffectType.IgnoreCriteria) ignoreCount--;
            }
            bool weaponMatches = !charm.isWeaponRelatedCharm ||
                                 charm.WeaponController != null && charm.WeaponController.currentWeapon != null &&
                                 charm.WeaponController.currentWeapon.weaponType == charm.relatedWeapon;
            bool criteriaMatches = ignoreCount > 0 || charm.criteria == null || charm.criteria.GetCriteria(charm);
            return inventory.globalActiveValue > 0 && disableCount <= 0 && level >= 0 &&
                   weaponMatches && criteriaMatches;
        }

        private static int FindRewardIndex(Sephirite sephirite)
        {
            int preferred = Plugin.autoChoiceStrategy.Value == 1
                ? FindFavoriteReward(sephirite)
                : FindPresetReward(sephirite, RewardPresetTerms());
            return preferred >= 0 ? preferred : FindBestRarityReward(sephirite);
        }

        private static int FindPresetReward(Sephirite sephirite, string[] terms)
        {
            foreach (string term in terms)
                for (int i = 0; i < sephirite.Rewards.Count; i++)
                {
                    ItemEntity entity = ItemDatabase.FindItemById(sephirite.Rewards[i].entityID);
                    if (Matches(entity, term)) return i;
                }
            return -1;
        }

        private static int FindFavoriteReward(Sephirite sephirite)
        {
            for (int i = 0; i < sephirite.Rewards.Count; i++)
            {
                ItemEntity entity = ItemDatabase.FindItemById(sephirite.Rewards[i].entityID);
                if (entity != null && entity.type == EItemType.Charm && IsFavorite(entity.id)) return i;
            }
            return -1;
        }

        private static int FindBestRarityReward(Sephirite sephirite)
        {
            List<int> best = new List<int>();
            EItemRarity bestRarity = EItemRarity.Common;
            bool found = false;
            for (int i = 0; i < sephirite.Rewards.Count; i++)
            {
                ItemEntity entity = ItemDatabase.FindItemById(sephirite.Rewards[i].entityID);
                if (entity == null) continue;
                if (!found || entity.rarity > bestRarity)
                {
                    best.Clear();
                    bestRarity = entity.rarity;
                    found = true;
                }
                if (entity.rarity == bestRarity) best.Add(i);
            }
            return best.Count > 0 ? best[UnityEngine.Random.Range(0, best.Count)] : -1;
        }

        private static bool Matches(ItemEntity entity, string term)
        {
            if (entity == null || string.IsNullOrWhiteSpace(term)) return false;
            if (term.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
                return term.Substring(5) == entity.id.ToString();
            if (term.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
                return entity.categories.Any(category => string.Equals(category, term.Substring(9), StringComparison.OrdinalIgnoreCase));
            if (Contains(entity.id.ToString(), term) || Contains(entity.Name, term) || Contains(entity.aName.key, term))
                return true;
            return entity.categories.Any(category =>
            {
                ItemCategoryEntity metadata = ItemDatabase.FindItemCategory(category);
                return Contains(category, term) || Contains(metadata?.id, term) ||
                       Contains(metadata?.categoryName?.ToString(), term);
            });
        }

        private static void TrySelectPresetWeapon(PlayerAvatar player)
        {
            if (!enabled || player == null || WeaponPresetTerms().Length == 0) return;
            if (Time.unscaledTime < nextAnvilDecision) return;
            UI_WeaponEnhancementPanel panel = UIManager.Instance?.GetElement<UI_WeaponEnhancementPanel>();
            if (panel == null || !panel.IsOpened) return;
            if (CatchUpRewards.IsWeaponFullyEnhanced(player.spawner))
            {
                panel.Close();
                return;
            }
            Anvil anvil = Resources.FindObjectsOfTypeAll<Anvil>()
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                             candidate.localWeaponList.Count > 0 &&
                                             (candidate.transform.position - player.transform.position).sqrMagnitude <= 16f);
            if (anvil == null) return;
            EnhancementMetadata selected = null;
            foreach (string term in WeaponPresetTerms())
            {
                selected = anvil.localWeaponList.FirstOrDefault(candidate =>
                    candidate?.enhanced != null &&
                    WeaponMatchesOrLeadsTo(candidate.enhanced, term, new HashSet<int>()));
                if (selected != null) break;
            }
            if (selected?.enhanced == null)
            {
                if (CanRerollAnvil(player, anvil))
                {
                    panel.Reroll();
                    int rerolls = AnvilRerollCounts.TryGetValue(anvil.netId, out int count) ? count + 1 : 1;
                    AnvilRerollCounts[anvil.netId] = rerolls;
                    nextAnvilDecision = Time.unscaledTime + 0.4f;
                    Plugin.LogInfo($"AFK autopilot rerolled Anvil for preset: player={player.Name}, " +
                                   $"netId={anvil.netId}, rerolls={rerolls}, remainingDice={player.rerollDice}.");
                    return;
                }
                SkippedAnvils.Add(anvil.netId);
                if (CurrentFloorEvent(player) == EFloorMainEventType.Anvil)
                    ResolvedAnvilFloors.Add(player.currentFloorGuid);
                panel.Close();
                int usedRerolls = AnvilRerollCounts.TryGetValue(anvil.netId, out int previousRerolls) ? previousRerolls : 0;
                Plugin.LogInfo($"AFK autopilot left Anvil unclaimed: player={player.Name}, netId={anvil.netId}, " +
                               $"no preset match after {usedRerolls} rerolls, remainingDice={player.rerollDice}.");
                return;
            }
            WeaponControllerSimple controller = player.GetComponent<WeaponControllerSimple>();
            if (controller == null) return;
            controller.EquipWeapon(fromTownObject: false, selected.enhanced.id);
            anvil.EnhanceClient();
            anvil.PlayEnhanceSound();
            if (CurrentFloorEvent(player) == EFloorMainEventType.Anvil)
                ResolvedAnvilFloors.Add(player.currentFloorGuid);
            panel.Close();
            Plugin.LogInfo($"AFK autopilot selected preset weapon enhancement: player={player.Name}, " +
                           $"weapon={selected.enhanced.id}/{selected.enhanced.Name}.");
        }

        private static bool CanRerollAnvil(PlayerAvatar player, Anvil anvil)
        {
            if (player == null || anvil == null || player.rerollDice <= 0) return false;
            WeaponControllerSimple controller = player.GetComponent<WeaponControllerSimple>();
            WeaponSimple currentWeapon = controller != null ? controller.currentWeapon : null;
            if (currentWeapon == null) return false;
            List<EnhancementMetadata> enhancements = WeaponDatabase.GetWeaponEnhancements(currentWeapon.entityId);
            if (enhancements == null) return false;
            int visibleChoices = anvil.enhanceSlotCount + player.GetCustomStatUnsafe("EXTRAWEAPONCHOICES");
            return enhancements.Count > visibleChoices;
        }

        private static string[] RewardPresetTerms() => PresetTerms(Plugin.autoChoicePresets.Value);

        private static bool WeaponMatchesOrLeadsTo(WeaponEntity weapon, string term, HashSet<int> visited)
        {
            if (weapon == null || !visited.Add(weapon.id)) return false;
            bool matches = term.StartsWith("weapon:", StringComparison.OrdinalIgnoreCase)
                ? term.Substring(7) == weapon.id.ToString()
                : Contains(weapon.id.ToString(), term) || Contains(weapon.Name, term) ||
                  Contains(weapon.aName.key, term);
            if (matches) return true;
            return (WeaponDatabase.GetWeaponEnhancements(weapon.id) ?? new List<EnhancementMetadata>())
                .Any(enhancement => enhancement?.enhanced != null && enhancement.enabled &&
                                    WeaponMatchesOrLeadsTo(enhancement.enhanced, term, visited));
        }

        private static string[] WeaponPresetTerms() => PresetTerms(Plugin.autoWeaponPresets.Value);

        private static string[] FloorPresetTerms() => PresetTerms(Plugin.autoFloorPresets.Value);

        private static string[] PresetTerms(string value) => (value ?? "")
            .Split(new[] { ',', '，', ';', '；', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Trim())
            .Where(term => term.Length > 0)
            .ToArray();

        private static bool Contains(string value, string term) =>
            !string.IsNullOrEmpty(value) && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void Attack(PlayerAvatar player, Vector2 direction, WeaponTactics tactics)
        {
            if (direction.sqrMagnitude < 0.01f)
            {
                ReportAttackInput(player, "skip-zero-direction", null);
                return;
            }
            WeaponControllerSimple controller = player.GetComponent<WeaponControllerSimple>();
            WeaponSimple currentWeapon = controller?.currentWeapon;
            int weaponId = currentWeapon != null ? currentWeapon.GetInstanceID() : 0;
            if (attackHeld && heldAttackWeaponId != weaponId) ReleaseAttack(player);
            if (tactics.Input == PrimaryInputMode.Hold)
            {
                if (attackHeld)
                {
                    ReportAttackInput(player, "already-held", currentWeapon);
                    return;
                }
                ReportAttackInput(player, "send-down", currentWeapon);
                player.AttackButtonDown(direction);
                attackHeld = true;
                heldAttackWeaponId = weaponId;
                attackHeldSince = Time.unscaledTime;
                releaseAttackAt = float.PositiveInfinity;
                if (RequiresUninterruptedPrimary(player))
                    Plugin.LogInfo($"AFK sustained primary started: weapon={DescribeWeapon(currentWeapon)}, " +
                                   $"targetDirection={direction}, swing=" +
                                   $"{player.GetComponent<WeaponControllerSimple>()?.currentWeaponSwing ?? -1}.");
                return;
            }
            if (attackHeld)
            {
                if (Time.unscaledTime < releaseAttackAt)
                {
                    ReportAttackInput(player, "charging", currentWeapon);
                    return;
                }
                ReportAttackInput(player, "charge-send-up", currentWeapon);
                ReleaseAttack(player);
                nextAttack = Time.unscaledTime + 0.12f;
                return;
            }
            if (Time.unscaledTime < nextAttack)
            {
                ReportAttackInput(player, "charge-cooldown", currentWeapon);
                return;
            }
            ReportAttackInput(player, "charge-send-down", currentWeapon);
            player.AttackButtonDown(direction);
            attackHeld = true;
            heldAttackWeaponId = weaponId;
            attackHeldSince = Time.unscaledTime;
            releaseAttackAt = Time.unscaledTime + tactics.ChargeTime;
        }

        private static WeaponTactics GetWeaponTactics(PlayerAvatar player)
        {
            WeaponSimple weapon = player.GetComponent<WeaponControllerSimple>()?.currentWeapon;
            if (weapon is WeaponSimple_Bow bow)
                return ScaleRangedTactics(player, Tactics("bow-charge", PrimaryInputMode.ChargeRelease,
                    6f, 4.5f, 8f, Mathf.Max(0.15f, bow.pullingTriggerTimer.time + 0.05f)));
            if (weapon is WeaponSimple_Crossbow)
                return ScaleRangedTactics(player,
                    Tactics("crossbow", PrimaryInputMode.Hold, 6f, 4.5f, 8f));
            if (weapon is WeaponSimple_Staff)
                return ScaleRangedTactics(player,
                    Tactics("staff-ranged", PrimaryInputMode.Hold, 5.5f, 4f, 7.5f));
            if (weapon is WeaponSimple_Golem)
                return ScaleRangedTactics(player,
                    Tactics("golem-ranged", PrimaryInputMode.Hold, 7f, 5.5f, 9f));
            if (weapon is WeaponSimple_GreatSword rangedGreatSword && HasRangedGreatSwordTransform(rangedGreatSword))
                return ScaleRangedTactics(player,
                    Tactics("greatsword-transform-ranged", PrimaryInputMode.Hold, 7f, 5f, 10f));
            if (weapon != null && weapon.isRangedWeapon)
            {
                float rangeScale = Mathf.Clamp(1f + player.GetCustomStat(ECustomStat.WeaponRange) / 100f, 1f, 6f);
                float preferred = Mathf.Clamp(5.5f * Mathf.Sqrt(rangeScale), 5.5f, 11f);
                return Tactics(weapon.GetType().Name + "-ranged", PrimaryInputMode.Hold, preferred,
                    Mathf.Max(4.5f, preferred - 1.5f), preferred + 2f);
            }
            if (weapon is WeaponSimple_Dagger)
                return ScaleMeleeTactics(player, Tactics("dagger", PrimaryInputMode.Hold, 1.15f, 0.45f, 1.55f));
            if (weapon is WeaponSimple_GreatSword)
                return ScaleMeleeTactics(player, Tactics("greatsword", PrimaryInputMode.Hold, 1.8f, 0.8f, 2.35f));
            if (weapon is WeaponSimple_QuartterStaff)
                return ScaleMeleeTactics(player, Tactics("quarterstaff", PrimaryInputMode.Hold, 2.15f, 1.1f, 2.8f));
            if (weapon is WeaponSimple_Katana katana && katana.isBladeSheathed &&
                player.GetCustomStatUnsafe("RANGESHEATH") > 0)
                return Tactics("katana-ranged-sheath", PrimaryInputMode.Hold, 5f, 3.5f, 7f);
            if (weapon is WeaponSimple_Katana || weapon is WeaponSimple_Katana_New)
                return ScaleMeleeTactics(player, Tactics("katana", PrimaryInputMode.Hold, 1.65f, 0.75f, 2.2f));
            if (weapon != null && (weapon.weaponType == EWeaponType.Crossbow ||
                                   weapon.weaponType == EWeaponType.StaffMagic ||
                                   weapon.weaponType == EWeaponType.Golem))
                return Tactics("ranged-fallback", PrimaryInputMode.Hold, 5.5f, 4f, 7.5f);
            return ScaleMeleeTactics(player,
                Tactics("melee-fallback", PrimaryInputMode.Hold, 1.6f, 0.7f, 2.2f));
        }

        private static bool RequiresUninterruptedPrimary(PlayerAvatar player)
        {
            WeaponSimple weapon = player?.GetComponent<WeaponControllerSimple>()?.currentWeapon;
            return weapon is WeaponSimple_GreatSword && weapon.isRangedWeapon &&
                   weapon.basicComboAttacks != null && weapon.basicComboAttacks.Any(attack => attack != null &&
                       (Contains(attack.name, "GreatswordLaser") || attack is NewWeaponFireData_BulletBurst));
        }

        private static WeaponTactics Tactics(string name, PrimaryInputMode input, float preferred,
            float minimum, float maximum, float charge = 0f) => new WeaponTactics
        {
            Name = name,
            Input = input,
            PreferredRange = preferred,
            MinimumRange = minimum,
            MaximumRange = maximum,
            ChargeTime = charge,
            IsRanged = minimum >= 3f
        };

        private static WeaponTactics ScaleMeleeTactics(PlayerAvatar player, WeaponTactics tactics)
        {
            float scale = Mathf.Clamp(1f + player.GetCustomStat(ECustomStat.WeaponRange) / 100f, 0.65f, 6f);
            tactics.PreferredRange *= scale;
            tactics.MinimumRange *= scale;
            tactics.MaximumRange *= scale;
            tactics.IsRanged = tactics.PreferredRange >= 4f;
            return tactics;
        }

        private static WeaponTactics ScaleRangedTactics(PlayerAvatar player, WeaponTactics tactics)
        {
            float range = Mathf.Clamp(1f + player.GetCustomStat(ECustomStat.WeaponRange) / 100f, 1f, 9f);
            float scale = Mathf.Sqrt(range);
            tactics.PreferredRange = Mathf.Min(14f, tactics.PreferredRange * scale);
            tactics.MinimumRange = Mathf.Max(4f, tactics.PreferredRange - 2f);
            tactics.MaximumRange = Mathf.Min(16f, tactics.MaximumRange * scale);
            return tactics;
        }

        private static bool HasRangedGreatSwordTransform(WeaponSimple_GreatSword weapon) =>
            weapon != null && weapon.specialAttackToTransform && weapon.overrideTransformAttack != null &&
            weapon.overrideTransformAttack.Any(attack => attack != null);

        private static WeaponTactics AdjustTacticsForEnemy(WeaponTactics tactics, UnitAvatar enemy)
        {
            if (!tactics.IsRanged || enemy == null || enemy.GetComponent<UnitAI_BossBasic>() == null)
                return tactics;
            tactics.PreferredRange = Mathf.Min(tactics.MaximumRange, tactics.PreferredRange + 1.5f);
            tactics.MinimumRange = Mathf.Min(tactics.PreferredRange - 0.5f, tactics.MinimumRange + 1.5f);
            return tactics;
        }

        private static Vector2 GetCombatMovement(PlayerAvatar player, UnitAvatar enemy, WeaponTactics tactics)
        {
            Vector2 offset = player.transform.position - enemy.transform.position;
            float distance = offset.magnitude;
            bool blockedShot = tactics.IsRanged && !HasClearLineOfFire(player.transform.position,
                enemy.transform.position);
            if (blockedShot)
            {
                if (combatRepositionTarget != enemy.netId || Time.unscaledTime >= nextCombatReposition ||
                    !HasClearLineOfFire(combatRepositionDestination, enemy.transform.position))
                {
                    combatRepositionTarget = enemy.netId;
                    nextCombatReposition = Time.unscaledTime + 1f;
                    if (!TryFindFiringPosition(player, enemy, tactics, out combatRepositionDestination))
                        combatRepositionDestination = enemy.transform.position;
                }
                return Navigate(player, combatRepositionDestination,
                    combatRepositionDestination == enemy.transform.position ? 1.25f : 0.2f);
            }
            if (distance > tactics.MaximumRange)
                return Navigate(player, enemy.transform.position, tactics.PreferredRange);
            if (distance >= tactics.MinimumRange) return Vector2.zero;

            if (combatRepositionTarget != enemy.netId || Time.unscaledTime >= nextCombatReposition ||
                (combatRepositionDestination - player.transform.position).sqrMagnitude < 0.25f)
            {
                combatRepositionTarget = enemy.netId;
                nextCombatReposition = Time.unscaledTime + 0.3f;
                Vector2 away = distance > 0.05f ? offset / distance : Vector2.right;
                Vector2 side = new Vector2(-away.y, away.x);
                Vector3 preferred = enemy.transform.position + (Vector3)(away * tactics.PreferredRange);
                if (!TryReachablePointNear(player, preferred, out combatRepositionDestination) &&
                    !TryReachablePointNear(player, player.transform.position + (Vector3)(side * 2f), out combatRepositionDestination))
                    combatRepositionDestination = player.transform.position + (Vector3)(away * 2f);
            }
            Vector2 retreat = Navigate(player, combatRepositionDestination, 0.2f);
            if (tactics.IsRanged && enemy.GetComponent<UnitAI_BossBasic>() != null &&
                distance < tactics.MinimumRange - 0.75f && retreat.sqrMagnitude > 0.01f &&
                Time.unscaledTime >= nextDash && player.CanMove)
            {
                player.Dash(player.transform.position + (Vector3)(retreat.normalized * 4f));
                nextDash = Time.unscaledTime + 0.75f;
            }
            return retreat;
        }

        private static bool HasClearLineOfFire(Vector3 from, Vector3 to)
        {
            return !TryGetLineOfFireBlocker(from, to, out _);
        }

        private static bool TryGetLineOfFireBlocker(Vector3 from, Vector3 to, out RaycastHit2D blocker)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 0.01f)
            {
                blocker = default;
                return false;
            }
            RaycastHit2D[] hits = Physics2D.RaycastAll(from, direction.normalized, direction.magnitude,
                CombatManager.PathfindingObstacleLayerMask);
            blocker = default;
            float nearestDistance = float.MaxValue;
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.distance <= 0.05f ||
                    hit.collider.GetComponentInParent<UnitAvatar>() != null) continue;
                if (hit.distance < nearestDistance)
                {
                    blocker = hit;
                    nearestDistance = hit.distance;
                }
            }
            return blocker.collider != null;
        }

        private static void ReportAttackDecision(PlayerAvatar player, UnitAvatar enemy, WeaponTactics tactics,
            string reason, bool clearShot)
        {
            if (Time.unscaledTime < nextAttackDecisionLog) return;
            nextAttackDecisionLog = Time.unscaledTime + 0.75f;
            WeaponControllerSimple controller = player?.GetComponent<WeaponControllerSimple>();
            WeaponSimple weapon = controller?.currentWeapon;
            float distance = player != null && enemy != null
                ? Vector2.Distance(player.transform.position, enemy.transform.position)
                : -1f;
            string blocker = player != null && enemy != null &&
                             TryGetLineOfFireBlocker(player.transform.position, enemy.transform.position,
                                 out RaycastHit2D hit)
                ? $"{hit.collider.name}/layer={hit.collider.gameObject.layer}/distance={hit.distance:0.00}"
                : "-";
            Plugin.LogInfo($"AFK attack decision: reason={reason}, weapon={DescribeWeapon(weapon)}, " +
                           $"tactics={tactics.Name}/{tactics.Input}, distance={distance:0.00}, clear={clearShot}, " +
                           $"blocker={blocker}, " +
                           $"held={attackHeld}, heldWeapon={heldAttackWeaponId}, swing={controller?.currentWeaponSwing ?? -99}, " +
                           $"ability={heldCombatAbility}, nextAbility={Mathf.Max(0f, nextCombatAbility - Time.unscaledTime):0.00}, " +
                           $"canMove={player?.CanMove ?? false}.");
        }

        private static void ReportAttackInput(PlayerAvatar player, string stage, WeaponSimple weapon)
        {
            bool stateOnly = stage == "already-held" || stage == "charging" || stage == "charge-cooldown";
            if (stateOnly && Time.unscaledTime < nextAttackInputLog) return;
            if (stateOnly) nextAttackInputLog = Time.unscaledTime + 1f;
            WeaponControllerSimple controller = player?.GetComponent<WeaponControllerSimple>();
            Plugin.LogInfo($"AFK attack input: stage={stage}, weapon={DescribeWeapon(weapon)}, " +
                           $"held={attackHeld}, swing={controller?.currentWeaponSwing ?? -99}, " +
                           $"server={controller?.isServer ?? false}, authority={controller?.isOwned ?? false}, " +
                           $"canMove={player?.CanMove ?? false}.");
        }

        private static bool TryFindFiringPosition(PlayerAvatar player, UnitAvatar enemy, WeaponTactics tactics,
            out Vector3 destination)
        {
            destination = Vector3.zero;
            PathGrid grid = PathGrid.Current;
            if (grid == null || !grid.IsBuilt) return false;
            List<Vector3> path = new List<Vector3>();
            float[] radii = { tactics.PreferredRange, tactics.MinimumRange, tactics.MaximumRange };
            Vector3 best = Vector3.zero;
            float bestScore = float.MaxValue;
            foreach (float radius in radii)
            {
                for (int i = 0; i < 20; i++)
                {
                    float angle = i * Mathf.PI * 2f / 20f;
                    Vector3 point = enemy.transform.position +
                                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!grid.WorldToCell(point, out int x, out int y) || grid.IsBlocked(x, y)) continue;
                    Vector3 world = grid.CellToWorld(x, y);
                    if (IsInsideAnyAoe(world) || !HasClearLineOfFire(world, enemy.transform.position)) continue;
                    path.Clear();
                    if (!PathFinder.Find(grid, player.transform.position, world, path) || path.Count == 0) continue;
                    float score = (world - player.transform.position).sqrMagnitude + path.Count * 0.1f;
                    if (score >= bestScore) continue;
                    best = world;
                    bestScore = score;
                }
            }
            if (bestScore == float.MaxValue) return false;
            destination = best;
            Plugin.LogInfo($"AFK firing position: enemy={enemy.name}, target={destination}, " +
                           $"distance={Vector2.Distance(destination, enemy.transform.position):0.0}.");
            return true;
        }

        private static bool TryAutoDefend(PlayerAvatar player, UnitAvatar enemy)
        {
            defenseEvasionMovement = Vector2.zero;
            if (!Plugin.autoDefend.Value)
            {
                if (defenseHeld) ReleaseDefense(player);
                return false;
            }
            WeaponControllerSimple weaponController = player.GetComponent<WeaponControllerSimple>();
            WeaponSimple weapon = weaponController != null ? weaponController.currentWeapon : null;
            bool shield = weapon is WeaponSimple_SwordAndShield;
            bool dagger = weapon is WeaponSimple_Dagger;
            WeaponSimple_Katana katana = weapon as WeaponSimple_Katana;
            WeaponSimple_QuartterStaff staff = weapon as WeaponSimple_QuartterStaff;
            bool greatSword = weapon is WeaponSimple_GreatSword;
            bool katanaNew = weapon is WeaponSimple_Katana_New;
            if (greatSword && RequiresUninterruptedPrimary(player))
            {
                if (defenseHeld) ReleaseDefense(player);
                return false;
            }
            if (!shield && !dagger && katana == null && staff == null && !greatSword && !katanaNew)
            {
                if (defenseHeld) ReleaseDefense(player);
                return false;
            }

            if (Time.unscaledTime < nextDefenseScan) return defenseHeld;
            nextDefenseScan = Time.unscaledTime + 0.04f;
            UnitAvatar threatOwner;
            Vector3 threatPoint;
            bool sustainedDefense;
            bool threatened = FindIncomingThreat(player, enemy, out threatOwner, out threatPoint, out sustainedDefense);
            IntegratedActionController actions = player.GetComponent<IntegratedActionController>();
            if (actions == null) return false;
            if (Time.unscaledTime < defensiveActionUntil) return true;

            if (shield)
            {
                WeaponSimple_SwordAndShield swordAndShield = (WeaponSimple_SwordAndShield)weapon;
                bool ready = player.CanMove && player.IsAvailableGuard && swordAndShield.isGuardAvailable &&
                             (player.MP > 0 || player.GetCustomStatUnsafe("INFINITYMP") > 0);
                if (!sustainedDefense && defenseHeld && Time.unscaledTime - defenseStartedAt >= 1f)
                {
                    ReleaseDefense(player);
                    defenseCooldownUntil = Time.unscaledTime + 0.15f;
                    return false;
                }
                if (!defenseHeld && Time.unscaledTime < defenseCooldownUntil) return false;
                if (threatened && ready)
                {
                    ReleaseAttack(player);
                    player.autoAimedTarget = threatOwner;
                    defenseAimOwner = threatOwner;
                    defenseAimPoint = threatPoint;
                    LockAim(player, threatPoint, threatOwner);
                    if (!defenseHeld)
                    {
                        ReleaseCombatAbility(player, force: true);
                        actions.Cast(101, threatPoint, threatOwner);
                        heldCombatAbility = 101;
                        defenseHeld = true;
                        defenseStartedAt = Time.unscaledTime;
                    }
                    releaseCombatAbilityAt = Time.unscaledTime + 0.2f;
                    return true;
                }
                if (defenseHeld)
                {
                    ReleaseDefense(player);
                    defenseCooldownUntil = Time.unscaledTime + 0.15f;
                }
                return false;
            }

            if (dagger)
            {
                WeaponSimple_Dagger daggerWeapon = (WeaponSimple_Dagger)weapon;
                if (threatened && Time.unscaledTime >= nextParry && player.CanMove &&
                    !daggerWeapon.parryReserved && player.MP >= daggerWeapon.ParryCost)
                {
                    TriggerDefensiveTap(player, actions, threatOwner, threatPoint);
                    defensiveActionUntil = Time.unscaledTime + 0.4f;
                    nextParry = Time.unscaledTime + 0.35f;
                    return true;
                }
                return false;
            }

            if (katana != null)
            {
                if (katana.isSheathAnimationRunning || weaponController.currentWeaponSwing >= 0) return true;
                bool defensiveForm = katana.sheathActionType == WeaponSimple_Katana.ESheathActionType.Deflecting ||
                                     katana.sheathActionType == WeaponSimple_Katana.ESheathActionType.Sheath;
                bool enoughMp = katana.sheathActionType != WeaponSimple_Katana.ESheathActionType.Deflecting ||
                                player.MP >= katana.SpecialAttackCost;
                if (threatened && defensiveForm && enoughMp && !katana.isSheathAnimationRunning &&
                    Time.unscaledTime >= nextParry && player.CanMove)
                {
                    TriggerDefensiveTap(player, actions, threatOwner, threatPoint);
                    defensiveActionUntil = Time.unscaledTime + 0.45f;
                    nextParry = Time.unscaledTime + 0.4f;
                    return true;
                }
                return threatened && !defensiveForm && TryEvadeDefensiveThreat(player, threatPoint, out _);
            }

            if (staff != null)
            {
                if (player.isGuardEnabled) return true;
                if (weaponController.currentWeaponSwing >= 0) return true;
                int bigSpearCost = KeywordDatabase.GetConstValue("staffThrowingSpearBigRequiredStack");
                bool wouldThrow = staff.enableBigThrowingSpear && bigSpearCost > 0 &&
                                  staff.currentBigThrowingSpearStack >= bigSpearCost;
                if (threatened && !wouldThrow && player.MP >= staff.SpecialAttackCost &&
                    weaponController.currentWeaponSwing < 10 && Time.unscaledTime >= nextParry)
                {
                    TriggerDefensiveTap(player, actions, threatOwner, threatPoint);
                    defensiveActionUntil = Time.unscaledTime + 1.2f;
                    nextParry = Time.unscaledTime + 1.1f;
                    return true;
                }
                if (threatened && wouldThrow)
                {
                    Vector2 ignored;
                    return TryEvadeDefensiveThreat(player, threatPoint, out ignored);
                }
                return false;
            }

            if (threatened && (greatSword || katanaNew))
            {
                Vector2 ignored;
                return TryEvadeDefensiveThreat(player, threatPoint, out ignored);
            }
            return false;
        }

        private static void TriggerDefensiveTap(PlayerAvatar player, IntegratedActionController actions,
            UnitAvatar threatOwner, Vector3 threatPoint)
        {
            ReleaseAttack(player);
            ReleaseCombatAbility(player, force: true);
            player.autoAimedTarget = threatOwner;
            defenseAimOwner = threatOwner;
            defenseAimPoint = threatPoint;
            LockAim(player, threatPoint, threatOwner);
            actions.Cast(101, threatPoint, threatOwner);
            heldCombatAbility = 101;
            releaseCombatAbilityAt = Time.unscaledTime + 0.08f;
        }

        private static bool TryEvadeDefensiveThreat(PlayerAvatar player, Vector3 threatPoint, out Vector2 movement)
        {
            movement = Vector2.zero;
            Vector2 away = player.transform.position - threatPoint;
            if (away.sqrMagnitude < 0.01f) away = Vector2.right;
            away.Normalize();
            Vector2 side = new Vector2(-away.y, away.x);
            Vector3 destination;
            if (!TryReachablePointNear(player, player.transform.position + (Vector3)(side * 3f), out destination) &&
                !TryReachablePointNear(player, player.transform.position - (Vector3)(side * 3f), out destination))
                destination = player.transform.position + (Vector3)(away * 3f);
            if (Time.unscaledTime >= nextDash && player.CanMove)
            {
                player.Dash(player.transform.position + (Vector3)((destination - player.transform.position).normalized * 4f));
                nextDash = Time.unscaledTime + 0.5f;
            }
            movement = Navigate(player, destination, 0.2f);
            defenseEvasionMovement = movement.sqrMagnitude > 0.01f ? movement : away;
            return true;
        }

        private static void LockAim(PlayerAvatar player, Vector3 threatPoint, UnitAvatar threatOwner)
        {
            Vector3 aim = threatOwner != null ? threatOwner.transform.position : threatPoint;
            SetAutoPilotAim(aim, threatOwner);
            PlayerInputController.Instance?.ForceAimToPosition(aim);
            WeaponControllerSimple weaponController = player.GetComponent<WeaponControllerSimple>();
            if (weaponController == null) return;
            weaponController.aimedPositionClientside = aim;
            weaponController.Aim(aim);
        }

        private static bool FindIncomingThreat(PlayerAvatar player, UnitAvatar enemy,
            out UnitAvatar threatOwner, out Vector3 threatPoint, out bool sustainedDefense)
        {
            threatOwner = null;
            threatPoint = player.transform.position;
            sustainedDefense = false;
            bool bossAoeWarning = TryGetUrgentBossAoe(enemy, out float warningRatio);
            if (bossAoeWarning && warningRatio >= 0.55f)
            {
                if (!bossAoeDefenseActive)
                    Plugin.LogInfo($"AFK defense: sustained Boss AOE guard started, enemy={enemy?.name}, " +
                                   $"warningRatio={warningRatio:0.00}.");
                bossAoeDefenseActive = true;
                threatOwner = enemy;
                threatPoint = enemy.transform.position;
                sustainedDefense = true;
                return true;
            }
            if (bossAoeDefenseActive && !bossAoeWarning)
            {
                bossAoeDefenseActive = false;
                Plugin.LogInfo("AFK defense: sustained Boss AOE guard ended.");
            }
            if (!bossAoeWarning && enemy != null && (enemy.attackPhase == EMonsterAttackPhase.Ready ||
                                  enemy.attackPhase == EMonsterAttackPhase.Fire) &&
                (enemy.transform.position - player.transform.position).sqrMagnitude <= 25f)
            {
                threatOwner = enemy;
                threatPoint = enemy.transform.position;
                return true;
            }

            ActiveMeleeCollisions.RemoveWhere(candidate => candidate == null || !candidate.gameObject.activeInHierarchy);
            MeleeCollision melee = ActiveMeleeCollisions
                .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                    candidate.owner != null && candidate.owner != player &&
                                    CombatManager.ContainsAttackableFaction(candidate.targetTeam, player.faction))
                .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                .FirstOrDefault(candidate =>
                    (candidate.transform.position - player.transform.position).sqrMagnitude <= 16f);
            if (melee != null)
            {
                threatOwner = melee.owner;
                threatPoint = melee.owner.transform.position;
                return true;
            }

            ActiveBullets.RemoveWhere(candidate => candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.IsSpawned);
            foreach (Bullet bullet in ActiveBullets)
            {
                if (bullet == null || !bullet.gameObject.activeInHierarchy || !bullet.IsSpawned ||
                    !bullet.isCollisionEnabled || bullet.Owner == player ||
                    !CombatManager.ContainsAttackableFaction(bullet.AttackableFactionLayers, player.faction)) continue;
                Vector3 current = bullet.transform.position;
                int id = bullet.GetInstanceID();
                bool hasPrevious = BulletPositions.TryGetValue(id, out Vector3 previous);
                BulletPositions[id] = current;
                if (!hasPrevious) continue;
                float currentDistance = (current - player.transform.position).sqrMagnitude;
                float previousDistance = (previous - player.transform.position).sqrMagnitude;
                if (currentDistance <= 36f && currentDistance < previousDistance)
                {
                    threatOwner = bullet.Owner;
                    threatPoint = current;
                    return true;
                }
            }
            return false;
        }

        private static bool TryGetUrgentBossAoe(UnitAvatar enemy, out float ratio)
        {
            ratio = 0f;
            Unit_MoleChieftain chieftain = enemy as Unit_MoleChieftain;
            if (chieftain == null) return false;
            UI_AOEWarning_MeleeAttackLine_Windmill warning = Traverse.Create(chieftain)
                .Field("windmillWarning").GetValue<UI_AOEWarning_MeleeAttackLine_Windmill>();
            if (warning == null || !warning.gameObject.activeInHierarchy) return false;
            Timer timer = Traverse.Create(warning).Field("timer").GetValue<Timer>();
            if (timer == null) return false;
            ratio = timer.Ratio;
            return true;
        }

        private static void RefreshWorldObjectCache(PlayerAvatar player)
        {
            if (player == null) return;
            if (cachedBossSpawner != null && (!cachedBossSpawner.gameObject.activeInHierarchy ||
                cachedBossSpawner.IsCleared || cachedBossSpawner.IsBossBattleInProgress))
                cachedBossSpawner = null;
            if (cachedEntrance != null && !cachedEntrance.gameObject.activeInHierarchy)
                cachedEntrance = null;
            if (cachedQuestBoard != null && !cachedQuestBoard.gameObject.activeInHierarchy)
                cachedQuestBoard = null;
            if (worldObjectFloor == player.currentFloorGuid && Time.unscaledTime < nextWorldObjectScan) return;
            bool floorChanged = worldObjectFloor != player.currentFloorGuid;
            worldObjectFloor = player.currentFloorGuid;
            nextWorldObjectScan = Time.unscaledTime + 5f;
            if (floorChanged) ResetWorldObjectsOnly();
            if (cachedQuestBoard == null)
                cachedQuestBoard = Resources.FindObjectsOfTypeAll<QuestSelectionBoard>()
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy)
                    .Select(candidate => candidate.GetComponent<Interactable>() ??
                                         candidate.GetComponentInParent<Interactable>() ??
                                         candidate.GetComponentInChildren<Interactable>(true))
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy)
                    .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                    .FirstOrDefault();
            if (cachedAnvil == null)
                cachedAnvil = Resources.FindObjectsOfTypeAll<Anvil>()
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                        !SkippedAnvils.Contains(candidate.netId))
                    .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                    .FirstOrDefault();
            if (cachedBossSpawner == null)
                cachedBossSpawner = Resources.FindObjectsOfTypeAll<BossSpawner>()
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                        !CompletedBossSpawners.Contains(candidate.GetInstanceID()) &&
                                        !candidate.IsCleared && !candidate.IsBossBattleInProgress &&
                                        candidate.bossBattleStartDetectType == BossSpawner.EBossBattleStartDetectType.Collide)
                    .Where(candidate =>
                    {
                        FloorGenerator floor = candidate.parent != null ? candidate.parent : candidate.GetComponentInParent<FloorGenerator>();
                        return floor != null && floor.guid == player.currentFloorGuid;
                    })
                    .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                    .FirstOrDefault();
            if (cachedEntrance == null)
            {
                cachedEntrance = Resources.FindObjectsOfTypeAll<Interactable>()
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                        IsNextEntrance(candidate))
                    .Where(candidate =>
                    {
                        FloorGenerator floor = candidate.GetComponentInParent<FloorGenerator>();
                        if (floor != null) return floor.guid == player.currentFloorGuid;
                        if (IsNamedUpFloorMover(candidate)) return true;
                        PathGrid grid = PathGrid.Current;
                        return grid != null && grid.IsBuilt &&
                               grid.WorldToCell(candidate.transform.position, out int x, out int y) &&
                               !grid.IsBlocked(x, y);
                    })
                    .OrderBy(candidate => EntrancePriority(candidate))
                    .ThenBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                    .FirstOrDefault();
                if (cachedEntrance != null) LogEntranceDiagnostic(player, cachedEntrance, "discovered");
                else if (Time.unscaledTime >= nextMissingEntranceDiagnostic)
                {
                    nextMissingEntranceDiagnostic = Time.unscaledTime + 5f;
                    string nearby = string.Join("|", Resources.FindObjectsOfTypeAll<Interactable>()
                        .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                            (candidate.transform.position - player.transform.position).sqrMagnitude <= 900f)
                        .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                        .Take(12)
                        .Select(candidate => candidate.GetType().Name + ":" + candidate.name));
                    Plugin.LogInfo($"AFK entrance scan empty: floor={ShortGuid(player.currentFloorGuid)}, " +
                                   $"pos={player.transform.position}, nearbyInteractables={nearby}.");
                }
            }
        }

        private static bool IsNextEntrance(Interactable candidate)
        {
            if (candidate == null) return false;
            if (IsNamedUpFloorMover(candidate)) return true;
            if (candidate.GetComponent<GoToNextPlaceTogether>() != null ||
                candidate.GetComponentInParent<GoToNextPlaceTogether>() != null ||
                candidate.GetComponentInChildren<GoToNextPlaceTogether>(true) != null ||
                candidate.GetComponent<GoToNextStage>() != null ||
                candidate.GetComponentInParent<GoToNextStage>() != null ||
                candidate.GetComponentInChildren<GoToNextStage>(true) != null ||
                candidate.GetComponent<GoToNextStage_MultiZone>() != null ||
                candidate.GetComponentInParent<GoToNextStage_MultiZone>() != null ||
                candidate.GetComponentInChildren<GoToNextStage_MultiZone>(true) != null ||
                candidate.GetComponent<DungeonStairCustom>() != null ||
                candidate.GetComponentInParent<DungeonStairCustom>() != null ||
                candidate.GetComponentInChildren<DungeonStairCustom>(true) != null ||
                candidate.GetComponent<PortalToAnotherFloor>() != null ||
                candidate.GetComponentInParent<PortalToAnotherFloor>() != null ||
                candidate.GetComponentInChildren<PortalToAnotherFloor>(true) != null ||
                candidate.GetComponent<PortalToAnotherFloor_Dark>() != null ||
                candidate.GetComponentInParent<PortalToAnotherFloor_Dark>() != null ||
                 candidate.GetComponentInChildren<PortalToAnotherFloor_Dark>(true) != null ||
                 candidate.GetComponent<AreaTeleporter>() != null ||
                 candidate.GetComponentInParent<AreaTeleporter>() != null ||
                 candidate.GetComponentInChildren<AreaTeleporter>(true) != null ||
                 candidate.GetComponent<PortalToGrasstown>() != null ||
                 candidate.GetComponentInParent<PortalToGrasstown>() != null ||
                 candidate.GetComponentInChildren<PortalToGrasstown>(true) != null ||
                 candidate.GetComponent<DungeonStair>() is DungeonStair stair && stair.stairDir == EStairDir.Down)
                return true;
            return candidate.GetComponent<DungeonStair_Chapter3End>() != null ||
                   candidate.GetComponentInParent<DungeonStair_Chapter3End>() != null ||
                   candidate.GetComponentInChildren<DungeonStair_Chapter3End>(true) != null;
        }

        private static bool IsNamedUpFloorMover(Interactable candidate) =>
            candidate != null && candidate.name.StartsWith("FloorMover_", StringComparison.OrdinalIgnoreCase) &&
            candidate.name.EndsWith("_Up", StringComparison.OrdinalIgnoreCase);

        private static bool IsQuestReturnFloorMover(Interactable candidate) =>
            candidate != null && candidate.name.StartsWith("FloorMover_Grassland_Down", StringComparison.OrdinalIgnoreCase);

        private static int EntrancePriority(Interactable entrance)
        {
            if (entrance == null) return 99;
            if (entrance.GetComponent<GoToNextPlaceTogether>() != null ||
                entrance.GetComponentInParent<GoToNextPlaceTogether>() != null ||
                entrance.GetComponentInChildren<GoToNextPlaceTogether>(true) != null) return 0;
            if (entrance.GetComponent<DungeonStair_Chapter3End>() != null ||
                entrance.GetComponentInParent<DungeonStair_Chapter3End>() != null ||
                entrance.GetComponentInChildren<DungeonStair_Chapter3End>(true) != null) return 1;
            if (entrance.GetComponent<GoToNextStage>() != null ||
                entrance.GetComponentInParent<GoToNextStage>() != null ||
                entrance.GetComponentInChildren<GoToNextStage>(true) != null ||
                entrance.GetComponent<GoToNextStage_MultiZone>() != null ||
                entrance.GetComponentInParent<GoToNextStage_MultiZone>() != null ||
                entrance.GetComponentInChildren<GoToNextStage_MultiZone>(true) != null) return 2;
            if (entrance.GetComponent<DungeonStairCustom>() != null ||
                entrance.GetComponentInParent<DungeonStairCustom>() != null ||
                entrance.GetComponentInChildren<DungeonStairCustom>(true) != null ||
                entrance.GetComponent<PortalToAnotherFloor>() != null ||
                entrance.GetComponentInParent<PortalToAnotherFloor>() != null ||
                entrance.GetComponentInChildren<PortalToAnotherFloor>(true) != null ||
                entrance.GetComponent<PortalToAnotherFloor_Dark>() != null ||
                entrance.GetComponentInParent<PortalToAnotherFloor_Dark>() != null ||
                 entrance.GetComponentInChildren<PortalToAnotherFloor_Dark>(true) != null) return 3;
            if (entrance.GetComponent<AreaTeleporter>() != null ||
                entrance.GetComponentInParent<AreaTeleporter>() != null ||
                entrance.GetComponentInChildren<AreaTeleporter>(true) != null) return 3;
            if (entrance.GetComponent<PortalToGrasstown>() != null ||
                entrance.GetComponentInParent<PortalToGrasstown>() != null ||
                entrance.GetComponentInChildren<PortalToGrasstown>(true) != null) return 3;
            return 4;
        }

        private static void LogEntranceDiagnostic(PlayerAvatar player, Interactable entrance, string reason)
        {
            if (Time.unscaledTime < nextEntranceDiagnostic) return;
            nextEntranceDiagnostic = Time.unscaledTime + 2f;
            GoToNextPlaceTogether together = entrance?.GetComponentInParent<GoToNextPlaceTogether>() ??
                                             entrance?.GetComponentInChildren<GoToNextPlaceTogether>(true);
            Plugin.LogInfo($"AFK entrance: reason={reason}, floor={ShortGuid(player?.currentFloorGuid)}, " +
                           $"entrance={entrance?.name}, pos={entrance?.transform.position}, " +
                           $"distance={(entrance != null && player != null ? Vector2.Distance(entrance.transform.position, player.transform.position) : -1f):0.0}, " +
                           $"together={together != null}, checkDistance={(together != null ? together.checkDistance : 0f):0.0}.");
        }

        private static void ResetWorldObjectsOnly()
        {
            cachedAnvil = null;
            cachedBossSpawner = null;
            cachedEntrance = null;
            cachedQuestBoard = null;
            entranceApproachDestination = Vector3.zero;
            entranceApproachId = 0;
            cachedReward = null;
        }

        private static void ResetWorldObjectCache()
        {
            nextWorldObjectScan = 0f;
            worldObjectFloor = null;
            cachedAnvil = null;
            cachedBossSpawner = null;
            cachedEntrance = null;
            cachedQuestBoard = null;
            entranceApproachDestination = Vector3.zero;
            entranceApproachId = 0;
            cachedReward = null;
        }

        private static void ReleaseDefense(PlayerAvatar player)
        {
            player?.GetComponent<IntegratedActionController>()?.CastStop(101);
            defenseHeld = false;
            defenseStartedAt = 0f;
            defenseAimOwner = null;
            defenseAimPoint = Vector3.zero;
            if (heldCombatAbility == 101) heldCombatAbility = -1;
            releaseCombatAbilityAt = 0f;
        }

        private static bool TryUseCombatAbility(PlayerAvatar player, UnitAvatar enemy, Vector2 toEnemy,
            WeaponTactics tactics)
        {
            if (heldCombatAbility >= 0) return true;
            if (Time.unscaledTime < nextCombatAbility) return false;
            if (attackHeld && player.GetComponent<WeaponControllerSimple>()?.currentWeapon is WeaponSimple_Bow)
                return false;
            IntegratedActionController actions = player.GetComponent<IntegratedActionController>();
            if (actions == null) return false;

            float distanceSquared = toEnemy.sqrMagnitude;
            if (!tactics.IsRanged && distanceSquared >= 25f && distanceSquared <= 100f &&
                Time.unscaledTime >= nextDash)
            {
                ReleaseAttack(player);
                player.Dash(player.transform.position + (Vector3)(toEnemy.normalized * 4f));
                nextDash = Time.unscaledTime + 2.5f;
                nextCombatAbility = Time.unscaledTime + 0.4f;
                return true;
            }

            WeaponSimple currentWeapon = player.GetComponent<WeaponControllerSimple>()?.currentWeapon;
            if (RequiresUninterruptedPrimary(player)) return false;
            if (TryUseWeaponSpecial(player, enemy, actions, distanceSquared)) return true;
            if (attackHeld && (currentWeapon is WeaponSimple_Crossbow ||
                               currentWeapon is WeaponSimple_Staff ||
                               currentWeapon is WeaponSimple_Golem))
                return false;
            if (attackHeld) return false;

            for (int offset = 0; offset < 5; offset++)
            {
                int slotIndex = (nextQuickSlot + offset) % 5;
                QuickSlotData slot = actions.quickSlots[slotIndex];
                if (!CanUseQuickSlot(player, slot)) continue;
                ReleaseAttack(player);
                actions.Cast(slotIndex, enemy.transform.position, enemy);
                nextQuickSlot = (slotIndex + 1) % 5;
                nextCombatAbility = Time.unscaledTime + 0.85f;
                if (slot.Type == QuickSlotType.Magic)
                {
                    heldCombatAbility = slotIndex;
                    releaseCombatAbilityAt = Time.unscaledTime + 0.3f;
                }
                return true;
            }

            if (distanceSquared <= 64f)
            {
                WeaponControllerSimple weaponController = player.GetComponent<WeaponControllerSimple>();
                if (weaponController?.currentWeapon is WeaponSimple_SwordAndShield ||
                    weaponController?.currentWeapon is WeaponSimple_Dagger ||
                    weaponController?.currentWeapon is WeaponSimple_Bow ||
                    weaponController?.currentWeapon is WeaponSimple_Crossbow ||
                    weaponController?.currentWeapon is WeaponSimple_Staff ||
                    weaponController?.currentWeapon is WeaponSimple_Golem ||
                    weaponController?.currentWeapon is WeaponSimple_GreatSword ||
                    weaponController?.currentWeapon is WeaponSimple_QuartterStaff ||
                    weaponController?.currentWeapon is WeaponSimple_Katana ||
                    weaponController?.currentWeapon is WeaponSimple_Katana_New)
                {
                    nextCombatAbility = Time.unscaledTime + 0.3f;
                    return false;
                }
                ReleaseAttack(player);
                actions.Cast(101, enemy.transform.position, enemy);
                heldCombatAbility = 101;
                releaseCombatAbilityAt = Time.unscaledTime + 0.12f;
                nextCombatAbility = Time.unscaledTime + 1.5f;
                return true;
            }
            nextCombatAbility = Time.unscaledTime + 0.3f;
            return false;
        }

        private static bool TryUseWeaponSpecial(PlayerAvatar player, UnitAvatar enemy,
            IntegratedActionController actions, float distanceSquared)
        {
            WeaponControllerSimple controller = player.GetComponent<WeaponControllerSimple>();
            WeaponSimple weapon = controller?.currentWeapon;
            if (weapon == null) return false;
            int weaponId = weapon.GetInstanceID();
            bool pending = pendingWeaponSpecialId == weaponId && Time.unscaledTime < weaponSpecialPendingUntil;

            float holdTime = 0.1f;
            float cooldown = 1.5f;
            if (weapon is WeaponSimple_Crossbow crossbow)
            {
                if (player.MP < crossbow.SpecialAttackCost) return false;
                switch (crossbow.specialAttackType)
                {
                    case WeaponSimple_Crossbow.ESpecialAttackType.FastReload:
                        if (crossbow.isReloading || crossbow.ammoInCurrentMagazine >
                            Mathf.Max(1, crossbow.currentMagazineCapacity / 3)) return false;
                        cooldown = 2f;
                        break;
                    case WeaponSimple_Crossbow.ESpecialAttackType.IceBuff:
                        if (!crossbow.iceBuffCoolDownTimer.Check()) return false;
                        cooldown = 2.2f;
                        break;
                    case WeaponSimple_Crossbow.ESpecialAttackType.AmmoCompression:
                        if (crossbow.isReloading || crossbow.hasCompressedAmmo ||
                            crossbow.ammoInCurrentMagazine <= 1) return false;
                        cooldown = 1.5f;
                        break;
                    case WeaponSimple_Crossbow.ESpecialAttackType.Minigun:
                        if (distanceSquared < 20.25f || player.MP < Mathf.Max(5f, crossbow.SpecialAttackCost * 5f))
                            return false;
                        holdTime = 2.5f;
                        cooldown = 3f;
                        break;
                    default:
                        cooldown = crossbow.continueBonus ? 0.55f : 1.4f;
                        break;
                }
            }
            else if (weapon is WeaponSimple_GreatSword greatSword)
            {
                bool needsTransform = greatSword.specialAttackToTransform && !greatSword.isTransformed;
                if (greatSword.specialAttackToTransform && greatSword.isTransformed) return false;
                if (!needsTransform && distanceSquared > 9f ||
                    !greatSword.moneyWhirlwind && player.MP < greatSword.SweepCost)
                    return false;
                if (controller.currentWeaponSwing >= 0 || attackHeld)
                {
                    ReleaseAttack(player);
                    pendingWeaponSpecialId = weaponId;
                    weaponSpecialPendingUntil = Time.unscaledTime + 2f;
                    if (!pending)
                        Plugin.LogInfo($"AFK greatsword special queued: swing={controller.currentWeaponSwing}, " +
                                       $"transform={greatSword.specialAttackToTransform}/{greatSword.isTransformed}, " +
                                       $"always={greatSword.isAlwaysTransformed}, sweepCost={greatSword.SweepCost}, " +
                                       $"mp={player.MP}, addons={DescribeWeaponAddons(greatSword)}.");
                    return true;
                }
                float chargeScale = greatSword.longCharge ? 0.6f : greatSword.superQuickSweep ? 100f :
                    greatSword.quickSweep ? 1.5f : 1f;
                holdTime = greatSword.sweepTimer.time / chargeScale + 0.35f;
                cooldown = needsTransform ? 1.5f : 3f;
            }
            else if (weapon is WeaponSimple_Dagger dagger)
            {
                if (dagger.currentFury <= 0 || player.MP < dagger.FuryCost || distanceSquared > 9f) return false;
                if (controller.currentWeaponSwing >= 0 || attackHeld)
                    return QueueWeaponSpecial(player, controller, weapon, pending, "dagger-fury");
                cooldown = 0.7f;
            }
            else if (weapon is WeaponSimple_QuartterStaff staff)
            {
                int stackCost = KeywordDatabase.GetConstValue("staffThrowingSpearBigRequiredStack");
                bool bigSpear = staff.enableBigThrowingSpear && stackCost > 0 &&
                                staff.currentBigThrowingSpearStack >= stackCost;
                if (!bigSpear && !staff.canCrystalExplosion && player.MP < staff.SpecialAttackCost) return false;
                if (!bigSpear && distanceSquared > 12.25f) return false;
                if (controller.currentWeaponSwing >= 0 || attackHeld)
                    return QueueWeaponSpecial(player, controller, weapon, pending,
                        bigSpear ? "staff-big-spear" : "staff-special");
                cooldown = bigSpear ? 0.8f : 1.8f;
            }
            else if (weapon is WeaponSimple_Katana_New newKatana)
            {
                if (controller.currentWeaponSwing < 0 || player.MP < newKatana.SpecialAttackCost ||
                    distanceSquared > 9f) return false;
                cooldown = 1f;
            }
            else if (weapon is WeaponSimple_Katana katana)
            {
                if (katana.sheathActionType != WeaponSimple_Katana.ESheathActionType.Eclipse ||
                    katana.isEclipseBuffActivated || distanceSquared > 16f) return false;
                if (controller.currentWeaponSwing >= 0 || attackHeld)
                    return QueueWeaponSpecial(player, controller, weapon, pending, "katana-eclipse");
                cooldown = 2f;
            }
            else
            {
                return false;
            }

            ReleaseAttack(player);
            LockAim(player, enemy.transform.position, enemy);
            actions.Cast(101, enemy.transform.position, enemy);
            if (weapon is WeaponSimple_GreatSword loggedGreatSword)
                Plugin.LogInfo($"AFK greatsword special started: hold={holdTime:0.00}, " +
                               $"sweepTime={loggedGreatSword.sweepTimer.time:0.00}, " +
                               $"transform={loggedGreatSword.specialAttackToTransform}/{loggedGreatSword.isTransformed}, " +
                               $"always={loggedGreatSword.isAlwaysTransformed}, addons={DescribeWeaponAddons(loggedGreatSword)}.");
            pendingWeaponSpecialId = 0;
            weaponSpecialPendingUntil = 0f;
            heldCombatAbility = 101;
            releaseCombatAbilityAt = Time.unscaledTime + holdTime;
            nextCombatAbility = Time.unscaledTime + cooldown;
            return true;
        }

        private static bool QueueWeaponSpecial(PlayerAvatar player, WeaponControllerSimple controller,
            WeaponSimple weapon, bool alreadyPending, string reason)
        {
            ReleaseAttack(player);
            pendingWeaponSpecialId = weapon.GetInstanceID();
            weaponSpecialPendingUntil = Time.unscaledTime + 2f;
            if (!alreadyPending)
                Plugin.LogInfo($"AFK weapon special queued: reason={reason}, weapon={DescribeWeapon(weapon)}, " +
                               $"swing={controller.currentWeaponSwing}, mp={player.MP}, addons={DescribeWeaponAddons(weapon)}.");
            return true;
        }

        private static string DescribeWeaponAddons(WeaponSimple weapon) => weapon?.addons == null
            ? "-"
            : string.Join("|", weapon.addons.Where(addon => addon != null).Select(addon => addon.GetType().Name));

        private static string DescribeWeapon(WeaponSimple weapon)
        {
            if (weapon == null) return "-";
            WeaponEntity entity = WeaponDatabase.FindWeaponById(weapon.entityId);
            return $"{weapon.GetType().Name}#{weapon.entityId}/{entity?.Name ?? "?"}";
        }

        private static string DescribeFireData(IEnumerable<NewWeaponFireData> attacks) => attacks == null
            ? "-"
            : string.Join("|", attacks.Where(attack => attack != null)
                .Select(attack => $"{attack.GetType().Name}:{attack.name}:{attack.swingID}"));

        private static void LogWeaponProfile(PlayerAvatar player)
        {
            WeaponControllerSimple controller = player?.GetComponent<WeaponControllerSimple>();
            WeaponSimple weapon = controller?.currentWeapon;
            int id = weapon != null ? weapon.GetInstanceID() : 0;
            if (id == lastLoggedWeaponId && Time.unscaledTime < nextWeaponProfileLog) return;
            lastLoggedWeaponId = id;
            nextWeaponProfileLog = Time.unscaledTime + 10f;
            if (weapon == null) return;
            float attackSpeed = player.GetCustomStat(ECustomStat.AttackSpeed);
            float fixedSpeed = player.GetCustomStatUnsafe("FIXEDATTACKSPEED");
            string extra = weapon is WeaponSimple_GreatSword greatSword
                ? $", transform={greatSword.specialAttackToTransform}/{greatSword.isTransformed}, " +
                  $"always={greatSword.isAlwaysTransformed}, sweep={greatSword.sweepTimer.time:0.00}/{greatSword.SweepCost}, " +
                  $"sweepState={greatSword.sweepSwing}/" +
                  $"{Traverse.Create(greatSword).Field("sweepRequest").GetValue<bool>()}, " +
                  $"overrideTransform={DescribeFireData(greatSword.overrideTransformAttack)}, " +
                  $"mpBasic={greatSword.useMPBasicAttack}, mpAttacks={DescribeFireData(greatSword.mpConsumedBasicAttack)}"
                : "";
            Plugin.LogInfo($"AFK weapon profile: weapon={DescribeWeapon(weapon)}, type={weapon.weaponType}, " +
                           $"ranged={weapon.isRangedWeapon}, attackMoveSet={weapon.attackMoveSet}, " +
                           $"attackSpeed={attackSpeed:0.##}, fixedAttackSpeed={fixedSpeed:0.##}, " +
                           $"amplify={weapon.attackSpeedAmplify:0.###}, weight={weapon.AttackWeightPerSwing:0.###}, " +
                           $"swing={controller.currentWeaponSwing}, attackHeld={attackHeld}, " +
                           $"heldAbility={heldCombatAbility}, mp={player.MP}, " +
                           $"specialUsesAttackSpeed={weapon.specialAttackIsRelatedToAttackSpeed}, " +
                           $"basic={DescribeFireData(weapon.basicComboAttacks)}, special={DescribeFireData(weapon.specialAttacks)}, " +
                           $"addons={DescribeWeaponAddons(weapon)}{extra}.");
        }

        private static bool CanUseQuickSlot(PlayerAvatar player, QuickSlotData slot)
        {
            if (slot == null || slot.IsEmpty) return false;
            if (slot.Type == QuickSlotType.Magic)
                return slot.magic != null &&
                       slot.magic.CanCast(player, useCooldown: true, useMp: true) == ECanUseSkillResult.Succeeded;
            if (slot.Type == QuickSlotType.Active)
                return slot.active != null && slot.active.GetCooldownRatio() <= 0.001f;
            return false;
        }

        private static void ReleaseCombatAbility(PlayerAvatar player, bool force)
        {
            if (heldCombatAbility < 0 || !force && Time.unscaledTime < releaseCombatAbilityAt) return;
            player?.GetComponent<IntegratedActionController>()?.CastStop(heldCombatAbility);
            if (heldCombatAbility == 101) defenseHeld = false;
            heldCombatAbility = -1;
            releaseCombatAbilityAt = 0f;
        }

        private static Vector2 Navigate(PlayerAvatar player, Vector3 destination, float stopDistance)
        {
            Vector2 direct = destination - player.transform.position;
            if (direct.sqrMagnitude <= stopDistance * stopDistance)
            {
                ResetPath();
                return Vector2.zero;
            }
            PathGrid grid = PathGrid.Current;
            if (grid == null || !grid.IsBuilt)
            {
                LogPathDiagnostic("grid-not-ready", player, destination);
                return Vector2.zero;
            }

            bool destinationChanged = (destination - pathDestination).sqrMagnitude > 1f;
            if (destinationChanged || CurrentPath.Count == 0 || pathIndex >= CurrentPath.Count ||
                Time.unscaledTime >= nextPathCalculation)
            {
                CurrentPath.Clear();
                if (PathFinder.Find(grid, player.transform.position, destination, CurrentPath))
                {
                    PathSmoother.Smooth(grid, CurrentPath);
                    pathIndex = CurrentPath.Count > 1 ? 1 : 0;
                    pathDestination = destination;
                }
                else LogPathDiagnostic("no-path", player, destination);
                nextPathCalculation = Time.unscaledTime + 0.5f;
            }

            if (Time.unscaledTime >= nextStuckCheck)
            {
                if ((player.transform.position - lastPathPosition).sqrMagnitude < 0.01f)
                {
                    CurrentPath.Clear();
                    nextPathCalculation = 0f;
                }
                lastPathPosition = player.transform.position;
                nextStuckCheck = Time.unscaledTime + 1f;
            }

            while (pathIndex < CurrentPath.Count &&
                   (CurrentPath[pathIndex] - player.transform.position).sqrMagnitude < 0.25f)
                pathIndex++;
            if (pathIndex >= CurrentPath.Count) return Vector2.zero;
            Vector2 direction = ((Vector2)(CurrentPath[pathIndex] - player.transform.position)).normalized;
            return direction;
        }

        private static void ReportDiagnostics(PlayerAvatar player, string action, UnitAvatar enemy, Vector2 movement)
        {
            string state = action + ":" + (enemy != null ? enemy.netId.ToString() : "-") + ":" +
                           (movement.sqrMagnitude > 0.01f ? "move" : "stop");
            bool changed = state != lastDiagnosticState;
            if (!changed && Time.unscaledTime < nextDiagnosticHeartbeat) return;
            lastDiagnosticState = state;
            nextDiagnosticHeartbeat = Time.unscaledTime + 3f;
            PathGrid grid = PathGrid.Current;
            WeaponTactics tactics = AdjustTacticsForEnemy(GetWeaponTactics(player), enemy);
            Plugin.LogInfo($"AFK status: action={action}, player={player.Name}, floor={ShortGuid(player.currentFloorGuid)}, " +
                            $"pos={player.transform.position}, enemy={DescribeUnit(enemy)}, move={movement}, " +
                            $"weapon={tactics.Name}, range={tactics.MinimumRange:0.0}-{tactics.MaximumRange:0.0}, " +
                            $"attackHeld={attackHeld}/{(attackHeld ? Time.unscaledTime - attackHeldSince : 0f):0.0}s, " +
                            $"peers={DescribePeerFloors(player)}, " +
                            $"input={player.localDataStorage.currentInput}/{player.localDataStorage.isInputReceived}, " +
                           $"grid={(grid != null ? grid.IsBuilt.ToString() : "null")}, path={pathIndex}/{CurrentPath.Count}, " +
                           $"ui={(UIManager.Instance?.CurrentControlStack != null)}, defending={defenseHeld}, " +
                           $"rescue={(rescueTarget != null ? rescueTarget.Name : "-")}.");
        }

        private static void ReportBlockedDiagnostics(PlayerAvatar player, string reason)
        {
            string state = "blocked:" + reason;
            if (state == lastDiagnosticState && Time.unscaledTime < nextDiagnosticHeartbeat) return;
            lastDiagnosticState = state;
            nextDiagnosticHeartbeat = Time.unscaledTime + 3f;
            Plugin.LogInfo($"AFK status: action=blocked, reason={reason}, player={(player != null ? player.Name : "-")}, " +
                           $"floor={ShortGuid(player?.currentFloorGuid)}, pos={player?.transform.position}.");
        }

        private static void LogPathDiagnostic(string reason, PlayerAvatar player, Vector3 destination)
        {
            string key = reason + ":" + ShortGuid(player?.currentFloorGuid);
            if (key == lastPathDiagnostic && Time.unscaledTime < nextPathDiagnostic) return;
            lastPathDiagnostic = key;
            nextPathDiagnostic = Time.unscaledTime + 2f;
            Plugin.LogInfo($"AFK path: reason={reason}, floor={ShortGuid(player?.currentFloorGuid)}, " +
                           $"from={player?.transform.position}, to={destination}, path={pathIndex}/{CurrentPath.Count}.");
        }

        private static string DescribeUnit(UnitAvatar unit) => unit == null
            ? "-"
            : unit.name + "#" + unit.netId + "@" + unit.transform.position + "/dead=" + unit.IsDead;

        private static string DescribePeerFloors(PlayerAvatar local) => PlayerSpawner.MultiplayerList == null
            ? "-"
            : string.Join(",", PlayerSpawner.MultiplayerList
                .Where(peer => peer?.PlayerAvatar != null && peer.PlayerAvatar != local)
                .Select(peer => peer.PlayerAvatar.Name + "@" + ShortGuid(peer.PlayerAvatar.currentFloorGuid) +
                                (peer.PlayerAvatar.IsDead ? "/dead" : "")));

        private static string ShortGuid(string guid) => string.IsNullOrEmpty(guid)
            ? "-"
            : guid.Substring(0, Math.Min(8, guid.Length));

        private static void ResetPath()
        {
            CurrentPath.Clear();
            pathIndex = 0;
            pathDestination = Vector3.zero;
            nextPathCalculation = 0f;
            nextStuckCheck = 0f;
        }

        private static void ReleaseAttack(PlayerAvatar player)
        {
            if (!attackHeld) return;
            WeaponSimple weapon = player?.GetComponent<WeaponControllerSimple>()?.currentWeapon;
            if (weapon is WeaponSimple_GreatSword && weapon.isRangedWeapon)
                Plugin.LogInfo($"AFK sustained primary released: weapon={DescribeWeapon(weapon)}, " +
                               $"held={Time.unscaledTime - attackHeldSince:0.00}s, swing=" +
                               $"{player.GetComponent<WeaponControllerSimple>()?.currentWeaponSwing ?? -1}.");
            player?.AttackButtonUp();
            attackHeld = false;
            heldAttackWeaponId = 0;
            attackHeldSince = 0f;
        }

        internal static void NotifyWeaponActionCancelled(WeaponSimple weapon)
        {
            if (!enabled || !attackHeld || weapon == null || weapon.GetInstanceID() != heldAttackWeaponId) return;
            Plugin.LogInfo($"AFK attack input reset by vanilla CancelWeaponAction: weapon={DescribeWeapon(weapon)}, " +
                           $"held={Time.unscaledTime - attackHeldSince:0.00}s.");
            attackHeld = false;
            heldAttackWeaponId = 0;
            attackHeldSince = 0f;
            releaseAttackAt = 0f;
            nextAttack = 0f;
        }

        private static PlayerAvatar FollowTarget(PlayerAvatar local)
        {
            if (PlayerSpawner.MultiplayerList == null) return null;
            PlayerSpawner host = PlayerSpawner.MultiplayerList.FirstOrDefault(player =>
                player != null && player.isHost && player.PlayerAvatar != null && !player.PlayerAvatar.IsDead &&
                player.PlayerAvatar != local && player.PlayerAvatar.currentFloorGuid == local.currentFloorGuid);
            if (host != null) return host.PlayerAvatar;
            return PlayerSpawner.MultiplayerList
                .Where(player => player?.PlayerAvatar != null && !player.PlayerAvatar.IsDead &&
                                 player.PlayerAvatar != local && player.PlayerAvatar.currentFloorGuid == local.currentFloorGuid)
                .OrderBy(player => (player.PlayerAvatar.transform.position - local.transform.position).sqrMagnitude)
                .Select(player => player.PlayerAvatar)
                .FirstOrDefault();
        }

        private static PlayerAvatar LocalPlayer() =>
            CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayer : null;

        private static void StopLocalPlayer()
        {
            PlayerAvatar player = LocalPlayer();
            CancelRescue(player);
            player?.localDataStorage?.Stop();
            movementApplied = false;
            lastAppliedMovement = Vector2.zero;
            if (runningRequested) player?.localDataStorage?.StopRunningCountdown();
            runningRequested = false;
            ReleaseAttack(player);
            ReleaseCombatAbility(player, force: true);
            autoPilotAimOwner = null;
            autoPilotAimPoint = Vector3.zero;
            autoPilotAimActive = false;
        }
    }

    [HarmonyPatch(typeof(WeaponSimple), nameof(WeaponSimple.CancelWeaponAction))]
    internal static class AutoPilotWeaponCancelPatch
    {
        private static void Prefix(WeaponSimple __instance) => AutoPilot.NotifyWeaponActionCancelled(__instance);
    }

    [HarmonyPatch(typeof(WeaponControllerSimple), nameof(WeaponControllerSimple.AttackButtonDown))]
    internal static class AutoPilotAttackDispatchPatch
    {
        private static void Prefix(WeaponControllerSimple __instance, Vector2 attackDirection, int dashAttack)
        {
            if (!AutoPilot.Enabled) return;
            Plugin.LogInfo($"AFK vanilla attack dispatch begin: weapon={__instance.currentWeapon?.GetType().Name ?? "-"}, " +
                           $"direction={attackDirection}, dash={dashAttack}, server={__instance.isServer}, " +
                           $"authority={__instance.isOwned}, swing={__instance.currentWeaponSwing}.");
        }

        private static void Postfix(WeaponControllerSimple __instance)
        {
            if (!AutoPilot.Enabled) return;
            Plugin.LogInfo($"AFK vanilla attack dispatch end: weapon={__instance.currentWeapon?.GetType().Name ?? "-"}, " +
                           $"swing={__instance.currentWeaponSwing}, attackDirection={__instance.attackDirection}.");
        }
    }

    [HarmonyPatch(typeof(PlayerInputController), "Update")]
    internal static class AutoPilotInputPatch
    {
        private static readonly AccessTools.FieldRef<PlayerInputController, bool> BlockInput =
            AccessTools.FieldRefAccess<PlayerInputController, bool>("blockAvatarInput");

        private static void Prefix(PlayerInputController __instance, out bool __state)
        {
            __state = BlockInput(__instance);
            if (AutoPilot.Enabled) BlockInput(__instance) = true;
        }

        private static void Postfix(PlayerInputController __instance, bool __state)
        {
            BlockInput(__instance) = __state;
            AutoPilot.Tick(__instance);
        }
    }

    [HarmonyPatch(typeof(WeaponControllerSimple), "Update")]
    internal static class AutoPilotAimPatch
    {
        private static void Postfix(WeaponControllerSimple __instance)
        {
            if (!AutoPilot.Enabled || __instance == null) return;
            AutoPilot.MaintainAutoPilotAim(__instance.GetComponent<PlayerAvatar>());
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "Update")]
    internal static class AutoPilotPlayerAimPatch
    {
        private static void Postfix(PlayerAvatar __instance)
        {
            if (!AutoPilot.Enabled || __instance == null || !__instance.isOwned) return;
            AutoPilot.MaintainAutoPilotAim(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerInputController), nameof(PlayerInputController.HandleOnAim))]
    internal static class AutoPilotManualAimPatch
    {
        private static bool Prefix() => !AutoPilot.Enabled;
    }

    [HarmonyPatch(typeof(PlayerInputController), nameof(PlayerInputController.HandleOnFire))]
    internal static class AutoPilotManualFirePatch
    {
        private static bool Prefix() => !AutoPilot.Enabled;
    }

    [HarmonyPatch(typeof(BossSpawner), "UserCode_RpcByeEnd")]
    internal static class AutoPilotBossDefeatPatch
    {
        private static void Postfix(BossSpawner __instance) => AutoPilot.NotifyBossDefeated(__instance);
    }

    [HarmonyPatch(typeof(SeedBossSpawner), "UserCode_RpcByeEnd")]
    internal static class AutoPilotSeedBossDefeatPatch
    {
        private static void Postfix(SeedBossSpawner __instance) => AutoPilot.NotifySeedBossDefeated(__instance);
    }

    [HarmonyPatch(typeof(PlayerInputController), nameof(PlayerInputController.HandleOnSubFire))]
    internal static class AutoPilotManualSubFirePatch
    {
        private static bool Prefix() => !AutoPilot.Enabled;
    }

    [HarmonyPatch(typeof(Bullet), "OnSpawn")]
    internal static class AutoPilotBulletSpawnPatch
    {
        private static void Postfix(Bullet __instance) => AutoPilot.RegisterBullet(__instance);
    }

    [HarmonyPatch(typeof(Bullet), "OnDespawn")]
    internal static class AutoPilotBulletDespawnPatch
    {
        private static void Postfix(Bullet __instance) => AutoPilot.UnregisterBullet(__instance);
    }

    [HarmonyPatch(typeof(MeleeCollision), "OnSpawn")]
    internal static class AutoPilotMeleeSpawnPatch
    {
        private static void Postfix(MeleeCollision __instance) => AutoPilot.RegisterMelee(__instance);
    }

    [HarmonyPatch(typeof(MeleeCollision), "OnDespawn")]
    internal static class AutoPilotMeleeDespawnPatch
    {
        private static void Postfix(MeleeCollision __instance) => AutoPilot.UnregisterMelee(__instance);
    }

    [HarmonyPatch(typeof(AOEWarningFactory), nameof(AOEWarningFactory.CreateAoe_Circle),
        new[] { typeof(Vector3), typeof(Vector3), typeof(Color), typeof(float), typeof(float) })]
    internal static class AutoPilotCircleWarningPatch
    {
        private static void Postfix(Vector3 to, Color color, float radius, float time, UI_AOEWarning __result) =>
            AutoPilot.RegisterHostileAoe(__result, to, radius, time, color);
    }

    [HarmonyPatch(typeof(AOEWarningFactory), nameof(AOEWarningFactory.CreateAoe_Ellipse),
        new[] { typeof(Vector3), typeof(Vector3), typeof(Color), typeof(float), typeof(float) })]
    internal static class AutoPilotEllipseWarningPatch
    {
        private static void Postfix(Vector3 to, Color color, float radius, float time, UI_AOEWarning __result) =>
            AutoPilot.RegisterHostileAoe(__result, to, radius + 1.35f, time, color);
    }

    [HarmonyPatch(typeof(AOEWarningFactory), nameof(AOEWarningFactory.CreateAoe_RangeAttackLine))]
    internal static class AutoPilotRangeWarningPatch
    {
        private static void Postfix(Vector3 from, Vector3 to, Color color, float time,
            UI_AOEWarning_RangeAttackLine __result) =>
            AutoPilot.RegisterHostileLine(__result, from, to, time, color);
    }

    [HarmonyPatch(typeof(AOEWarningFactory), nameof(AOEWarningFactory.CreateAoe_Rectangle),
        new[] { typeof(Vector3), typeof(Vector3), typeof(Color), typeof(Vector2), typeof(float) })]
    internal static class AutoPilotRectangleWarningPatch
    {
        private static void Postfix(Vector3 to, Color color, Vector2 size, float time,
            UI_AOEWarning_Rectangle __result) =>
            AutoPilot.RegisterHostileBox(__result, to, size, 0f, time, color);
    }

    [HarmonyPatch(typeof(AOEWarningFactory), nameof(AOEWarningFactory.CreateAoe_Rectangle),
        new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(Color), typeof(Vector2), typeof(float) })]
    internal static class AutoPilotAngledRectangleWarningPatch
    {
        private static void Postfix(Vector3 to, float angle, Color color, Vector2 size, float time,
            UI_AOEWarning_Rectangle __result) =>
            AutoPilot.RegisterHostileBox(__result, to, size, angle, time, color);
    }

    [HarmonyPatch(typeof(AOEWarningFactory), nameof(AOEWarningFactory.CreateAoe_MeleeAttackLine))]
    internal static class AutoPilotMeleeWarningPatch
    {
        private static void Postfix(Vector3 position, Vector2 size, float angle, float time, Color color,
            UI_AOEWarning_MeleeAttackLine __result) =>
            AutoPilot.RegisterHostileBox(__result, position, size, angle, time, color);
    }

    [HarmonyPatch(typeof(AOEWarningFactory), nameof(AOEWarningFactory.CreateAoe_MeleeAttackLine_Windmill))]
    internal static class AutoPilotWindmillWarningPatch
    {
        private static void Postfix(Vector3 position, Vector2 size, float angle, float time, Color color,
            UI_AOEWarning_MeleeAttackLine_Windmill __result) =>
            AutoPilot.RegisterHostileBox(__result, position, size, angle, time, color);
    }

    [HarmonyPatch(typeof(PlayerSpawner), "Update")]
    internal static class AutoPilotWorldNamePatch
    {
        private static void Postfix(PlayerSpawner __instance)
        {
            if (__instance?.PlayerAvatar != null && __instance.WorldUserName != null)
                __instance.WorldUserName.text = AutoPilot.DisplayName(__instance.PlayerAvatar);
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerInDungeonUserIcon), nameof(UI_MultiplayerInDungeonUserIcon.SetUser))]
    internal static class AutoPilotUserIconNamePatch
    {
        private static void Postfix(UI_MultiplayerInDungeonUserIcon __instance, PlayerSpawner spawner)
        {
            if (__instance?.nameText != null && spawner?.PlayerAvatar != null)
                __instance.nameText.text = AutoPilot.DisplayName(spawner.PlayerAvatar);
        }
    }
}
