using System.Linq;
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class AutoPilot
    {
        private static bool enabled;
        private static float nextAttack;
        private static float releaseAttackAt;
        private static float nextUtilityCheck;
        private static float nextEntranceInteraction;
        private static float nextCombatAbility;
        private static float nextDash;
        private static float releaseCombatAbilityAt;
        private static int heldCombatAbility = -1;
        private static int nextQuickSlot;
        private static string lastInventorySignature;
        private static string pendingInventorySignature;
        private static float arrangeInventoryAt;
        private static float nextRewardAction;
        private static bool attackHeld;
        private static readonly HashSet<uint> SkippedAnvils = new HashSet<uint>();
        private static readonly HashSet<string> ResolvedAnvilFloors = new HashSet<string>();
        private static readonly HashSet<string> LoggedAnvilWaitFloors = new HashSet<string>();
        private static readonly Dictionary<uint, int> AnvilRerollCounts = new Dictionary<uint, int>();
        private static float nextAnvilDecision;
        private static readonly HashSet<int> IgnoredDroppedItems = new HashSet<int>();
        private static string ignoredDropFloor;
        private static float nextDefenseScan;
        private static float nextParry;
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
        private static UnitAvatar cachedEnemy;
        private static float nextEnemySearch;
        private static readonly Dictionary<uint, float> UnreachableEnemies = new Dictionary<uint, float>();
        private static Vector3 enemyProgressPosition;
        private static float enemyProgressDistance;
        private static float nextEnemyProgressCheck;
        private static int enemyStuckChecks;
        private static Vector2 lastAppliedMovement;
        private static bool movementApplied;
        private static UnitAvatar defenseAimOwner;
        private static Vector3 defenseAimPoint;
        private static UnitAvatar autoPilotAimOwner;
        private static Vector3 autoPilotAimPoint;
        private static bool autoPilotAimActive;
        private static bool bossAoeDefenseActive;
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
        private static Vector3 bossTriggerDestination;
        private static string bossTriggerFloor;
        private static float nextBossTriggerSearch;
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

        internal static bool Enabled => enabled;

        private sealed class AoeThreat
        {
            internal UI_AOEWarning Warning;
            internal Vector3 Center;
            internal float Radius;
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
                ReportBlockedDiagnostics(player, player == null ? "no-local-player" : player.IsDead ? "player-dead" : "no-local-storage");
                CancelRescue(player);
                ReleaseAttack(player);
                ReleaseCombatAbility(player, force: true);
                return;
            }
            if (UIManager.Instance != null && UIManager.Instance.CurrentControlStack != null)
            {
                ReportBlockedDiagnostics(player, "ui-open");
                CancelRescue(player);
                TrySelectPresetWeapon(player);
                ApplyMovement(player, Vector2.zero);
                ReleaseAttack(player);
                ReleaseCombatAbility(player, force: true);
                return;
            }

            UnitAvatar enemy = FindEnemy(player);
            PlayerAvatar leader = FollowTarget(player);
            Vector2 movement = Vector2.zero;
            string action = "idle";
            bool evadingAoe = TryEvadeAoe(player, enemy, out movement);
            bool rescuing = !evadingAoe && TryRescueTeammate(player, enemy, out movement);
            bool defending = !evadingAoe && !rescuing && TryAutoDefend(player, enemy);
            if (evadingAoe)
            {
                action = "aoe-evade";
                CancelRescue(player);
                ReleaseDefense(player);
                ReleaseAttack(player);
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
                if (toEnemy.sqrMagnitude > 9f) movement = Navigate(player, enemy.transform.position, 3f);
                if (!defending && !TryUseCombatAbility(player, enemy, toEnemy))
                    Attack(player, toEnemy.normalized);
            }
            else
            {
                ReleaseAttack(player);
                if (defending)
                {
                    action = "defend-projectile";
                    movement = Vector2.zero;
                }
                else if (TryApproachPresetChoice(player, out Vector2 choiceMovement))
                {
                    action = "anvil";
                    movement = choiceMovement;
                }
                else if (TryApproachBossTrigger(player, out Vector2 bossMovement))
                {
                    action = "boss-trigger";
                    movement = bossMovement;
                }
                else if (CanLeadParty(player) && TryApproachNextEntrance(player, out Vector2 entranceMovement))
                {
                    action = "entrance";
                    movement = entranceMovement;
                }
                else if (leader != null)
                {
                    action = "follow";
                    Vector2 toLeader = leader.transform.position - player.transform.position;
                    if (toLeader.sqrMagnitude > 9f) movement = Navigate(player, leader.transform.position, 3f);
                }
            }

            if (!defenseHeld && enemy == null && movement.sqrMagnitude > 0.01f)
                SetAutoPilotAim(player.transform.position + (Vector3)movement, null);
            MaintainAutoPilotAim(player);

            ApplyMovement(player, movement);
            ReportDiagnostics(player, action, enemy, movement);
            if (Time.unscaledTime >= nextUtilityCheck)
            {
                nextUtilityCheck = Time.unscaledTime + 0.35f;
                TryPickUpNearbyItem(player);
                TryClaimFavoriteReward(player);
            }
            TryAutoArrangeInventory(player, enemy);
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
            nextCombatAbility = 0f;
            nextDash = 0f;
            releaseCombatAbilityAt = 0f;
            heldCombatAbility = -1;
            nextQuickSlot = 0;
            lastInventorySignature = null;
            pendingInventorySignature = null;
            arrangeInventoryAt = 0f;
            nextRewardAction = 0f;
            attackHeld = false;
            SkippedAnvils.Clear();
            ResolvedAnvilFloors.Clear();
            LoggedAnvilWaitFloors.Clear();
            AnvilRerollCounts.Clear();
            nextAnvilDecision = 0f;
            IgnoredDroppedItems.Clear();
            ignoredDropFloor = null;
            nextDefenseScan = 0f;
            nextParry = 0f;
            defenseHeld = false;
            defenseStartedAt = 0f;
            defenseCooldownUntil = 0f;
            BulletPositions.Clear();
            ActiveBullets.Clear();
            ActiveMeleeCollisions.Clear();
            cachedEnemy = null;
            nextEnemySearch = 0f;
            UnreachableEnemies.Clear();
            enemyProgressPosition = Vector3.zero;
            enemyProgressDistance = float.MaxValue;
            nextEnemyProgressCheck = 0f;
            enemyStuckChecks = 0;
            movementApplied = false;
            lastAppliedMovement = Vector2.zero;
            defenseAimOwner = null;
            defenseAimPoint = Vector3.zero;
            autoPilotAimOwner = null;
            autoPilotAimPoint = Vector3.zero;
            autoPilotAimActive = false;
            bossAoeDefenseActive = false;
            cachedReward = null;
            nextRewardScan = 0f;
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
            bossTriggerDestination = Vector3.zero;
            bossTriggerFloor = null;
            nextBossTriggerSearch = 0f;
            ResetWorldObjectCache();
            ResetPath();
            lastDiagnosticState = null;
            nextDiagnosticHeartbeat = 0f;
            lastPathDiagnostic = null;
            nextPathDiagnostic = 0f;
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

        internal static void RegisterHostileAoe(UI_AOEWarning warning, Vector3 center, float radius, Color color)
        {
            if (warning == null || radius <= 0f || !ApproximatelyHostileColor(color)) return;
            ActiveAoeThreats.RemoveAll(threat => threat.Warning == null || threat.Warning == warning);
            ActiveAoeThreats.Add(new AoeThreat { Warning = warning, Center = center, Radius = radius + 0.5f });
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
                cachedEnemy = FindNearestReachableCandidate(player);
                nextEnemySearch = Time.unscaledTime + 0.12f;
                ResetEnemyProgress(player, cachedEnemy);
            }
            TrackEnemyProgress(player);
            return cachedEnemy;
        }

        private static UnitAvatar FindNearestReachableCandidate(PlayerAvatar player)
        {
            if (CombatManager.Instance == null) return null;
            long hostileLayers = player.GetHostileFactionLayers(EDamageFromType.None);
            UnitAvatar nearest = null;
            float nearestDistance = 2500f;
            foreach (UnitAvatar candidate in CombatManager.Instance.AllCreatures)
            {
                if (candidate == null || candidate == player || candidate.IsDead || !candidate.canBeTarget.IsTrue() ||
                    (hostileLayers & RuntimeFactionManager.Instance.FindFactionLayer(candidate.faction)) == 0L ||
                    UnreachableEnemies.TryGetValue(candidate.netId, out float retryAt) && Time.unscaledTime < retryAt)
                    continue;
                float distance = (candidate.transform.position - player.transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = candidate;
                nearestDistance = distance;
            }
            return nearest;
        }

        private static void TrackEnemyProgress(PlayerAvatar player)
        {
            if (cachedEnemy == null || Time.unscaledTime < nextEnemyProgressCheck) return;
            nextEnemyProgressCheck = Time.unscaledTime + 1f;
            float distance = (cachedEnemy.transform.position - player.transform.position).sqrMagnitude;
            if (distance <= 9f)
            {
                enemyStuckChecks = 0;
                enemyProgressPosition = player.transform.position;
                enemyProgressDistance = distance;
                return;
            }
            float moved = (player.transform.position - enemyProgressPosition).sqrMagnitude;
            bool progressed = moved > 0.04f || distance < enemyProgressDistance - 0.25f;
            if (progressed)
            {
                enemyStuckChecks = 0;
            }
            else if (++enemyStuckChecks >= 2)
            {
                UnreachableEnemies[cachedEnemy.netId] = Time.unscaledTime + 4f;
                Plugin.LogInfo($"AFK autopilot postponed stuck enemy: enemy={cachedEnemy.name}, retry=4s.");
                cachedEnemy = null;
                enemyStuckChecks = 0;
                ResetPath();
            }
            enemyProgressPosition = player.transform.position;
            enemyProgressDistance = distance;
        }

        private static void ResetEnemyProgress(PlayerAvatar player, UnitAvatar enemy)
        {
            enemyProgressPosition = player != null ? player.transform.position : Vector3.zero;
            enemyProgressDistance = player != null && enemy != null
                ? (enemy.transform.position - player.transform.position).sqrMagnitude
                : float.MaxValue;
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

        private static bool TryEvadeAoe(PlayerAvatar player, UnitAvatar enemy, out Vector2 movement)
        {
            movement = Vector2.zero;
            ActiveAoeThreats.RemoveAll(threat => threat.Warning == null || !threat.Warning.IsSpawned);
            Vector3 position = player.transform.position;
            AoeThreat threat = ActiveAoeThreats
                .Where(candidate => ((Vector2)(position - candidate.Center)).sqrMagnitude <=
                                    candidate.Radius * candidate.Radius)
                .OrderByDescending(candidate => candidate.Warning.TimerRatio)
                .FirstOrDefault();
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
                                   $"ratio={threat.Warning.TimerRatio:0.00}.");
                    if (threat.Warning.TimerRatio >= 0.55f && Time.unscaledTime >= nextDash && player.CanMove)
                    {
                        player.Dash(player.transform.position + (Vector3)(fallback * 4f));
                        nextDash = Time.unscaledTime + 0.5f;
                    }
                    movement = fallback;
                    return true;
                }
                Plugin.LogInfo($"AFK AOE evade: target={aoeEscapeDestination}, center={threat.Center}, " +
                               $"radius={threat.Radius:0.00}, ratio={threat.Warning.TimerRatio:0.00}.");
            }

            Vector2 direction = aoeEscapeDestination - player.transform.position;
            if (direction.sqrMagnitude <= 0.16f) return true;
            direction.Normalize();
            if (threat.Warning.TimerRatio >= 0.65f && Time.unscaledTime >= nextDash && player.CanMove)
            {
                player.Dash(player.transform.position + (Vector3)(direction * 4f));
                nextDash = Time.unscaledTime + 0.5f;
            }
            movement = Navigate(player, aoeEscapeDestination, 0.2f);
            return true;
        }

        private static bool TryFindAoeEscape(PlayerAvatar player, AoeThreat threat, out Vector3 destination)
        {
            destination = Vector3.zero;
            PathGrid grid = PathGrid.Current;
            if (grid == null || !grid.IsBuilt) return false;
            List<Vector3> candidates = new List<Vector3>();
            float escapeRadius = threat.Radius + 1f;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                Vector3 point = threat.Center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * escapeRadius;
                if (!grid.WorldToCell(point, out int x, out int y) || grid.IsBlocked(x, y)) continue;
                Vector3 world = grid.CellToWorld(x, y);
                if (ActiveAoeThreats.Any(active => active.Warning != null && active.Warning.IsSpawned &&
                    ((Vector2)(world - active.Center)).sqrMagnitude <= active.Radius * active.Radius)) continue;
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
            if (player.IsInBattle || WeaponPresetTerms().Length == 0 ||
                CatchUpRewards.IsWeaponFullyEnhanced(player.spawner)) return false;
            bool anvilFloor = CurrentFloorEvent(player) == EFloorMainEventType.Anvil;
            if (anvilFloor && ResolvedAnvilFloors.Contains(player.currentFloorGuid)) return false;
            RefreshWorldObjectCache(player);
            Anvil anvil = cachedAnvil != null && !SkippedAnvils.Contains(cachedAnvil.netId) ? cachedAnvil : null;
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

        private static EFloorMainEventType CurrentFloorEvent(PlayerAvatar player)
        {
            if (player == null || DungeonManager.Instance == null || string.IsNullOrEmpty(player.currentFloorGuid) ||
                !DungeonManager.Instance.generatedFloors.TryGetValue(player.currentFloorGuid, out FloorData floor))
                return EFloorMainEventType.Unknown;
            return floor.mainEventType;
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
            return count <= 1 || player?.spawner != null && player.spawner.isHost;
        }

        private static bool TryApproachBossTrigger(PlayerAvatar player, out Vector2 movement)
        {
            movement = Vector2.zero;
            RefreshWorldObjectCache(player);
            BossSpawner spawner = cachedBossSpawner;
            if (spawner == null) return false;

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
            if (player.IsInBattle) return false;
            RefreshWorldObjectCache(player);
            Interactable entrance = cachedEntrance;
            if (entrance == null || !entrance.IsInteractable(player.gameObject)) return false;
            Vector2 delta = entrance.transform.position - player.transform.position;
            if (delta.sqrMagnitude > 2.25f)
            {
                movement = Navigate(player, entrance.transform.position, 1.5f);
                return true;
            }
            if (Time.unscaledTime >= nextEntranceInteraction)
            {
                nextEntranceInteraction = Time.unscaledTime + 2f;
                DungeonStair stair = entrance.GetComponent<DungeonStair>();
                if (stair != null)
                {
                    TryMoveToConnectedFloor(player);
                }
                else
                {
                    entrance.Interactive(player.gameObject);
                    Plugin.LogInfo($"AFK autopilot used next entrance: player={player.Name}, entrance={entrance.name}.");
                }
            }
            return true;
        }

        private static void TryMoveToConnectedFloor(PlayerAvatar player)
        {
            if (DungeonManager.Instance == null ||
                !DungeonManager.Instance.generatedFloors.TryGetValue(player.currentFloorGuid, out FloorData current)) return;
            List<FloorData> forward = (current.connectionToOtherFloors ?? new string[0])
                .Select(guid => DungeonManager.Instance.generatedFloors.TryGetValue(guid, out FloorData floor) ? floor : null)
                .Where(floor => floor != null && floor.nodeProgress > current.nodeProgress)
                .ToList();
            if (forward.Count == 0) return;
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
            if (next == null) return;
            player.MoveFloorViaWorldmap(next.guid, delayOnMultiplayer: true, "FLOORSTARTING");
            Plugin.LogInfo($"AFK autopilot selected connected floor: player={player.Name}, " +
                           $"from={current.guid}, to={next.guid}, event={next.mainEventType}, " +
                           $"progress={current.nodeProgress}->{next.nodeProgress}.");
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
            string signature = InventorySignature(inventory);
            if (signature != pendingInventorySignature && signature != lastInventorySignature)
            {
                pendingInventorySignature = signature;
                arrangeInventoryAt = Time.unscaledTime + 2f;
            }
            if (signature == lastInventorySignature || Time.unscaledTime < arrangeInventoryAt ||
                player.IsInBattle || enemy != null) return;

            lastInventorySignature = signature;
            pendingInventorySignature = null;
            arrangeInventoryAt = 0f;
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
            if (cachedReward == null || !cachedReward.gameObject.activeInHierarchy || cachedReward.isAcquired)
            {
                cachedReward = null;
                if (Time.unscaledTime < nextRewardScan) return;
                nextRewardScan = Time.unscaledTime + 1f;
                cachedReward = Resources.FindObjectsOfTypeAll<Sephirite>()
                    .FirstOrDefault(reward => reward != null && reward.gameObject.activeInHierarchy &&
                                              reward.isOwned && !reward.isAcquired);
            }
            Sephirite sephirite = cachedReward;
            if (sephirite == null) return;
            if (!sephirite.isGenerated)
            {
                sephirite.CmdGenerateItemForOpen(player.gameObject);
                nextRewardAction = Time.unscaledTime + 1f;
                return;
            }
            if (Plugin.autoChoiceStrategy.Value == 2) return;
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
                Plugin.LogInfo($"AFK autopilot selected reward: player={player.Name}, item={entity.id}, rarity={entity.rarity}.");
            }
        }

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

        private static void Attack(PlayerAvatar player, Vector2 direction)
        {
            if (attackHeld && Time.unscaledTime >= releaseAttackAt) ReleaseAttack(player);
            if (attackHeld || Time.unscaledTime < nextAttack || direction.sqrMagnitude < 0.01f) return;
            player.AttackButtonDown(direction);
            attackHeld = true;
            releaseAttackAt = Time.unscaledTime + 0.08f;
            nextAttack = Time.unscaledTime + 0.45f;
        }

        private static bool TryAutoDefend(PlayerAvatar player, UnitAvatar enemy)
        {
            if (!Plugin.autoDefend.Value)
            {
                if (defenseHeld) ReleaseDefense(player);
                return false;
            }
            WeaponControllerSimple weaponController = player.GetComponent<WeaponControllerSimple>();
            WeaponSimple weapon = weaponController != null ? weaponController.currentWeapon : null;
            bool shield = weapon is WeaponSimple_SwordAndShield;
            bool dagger = weapon is WeaponSimple_Dagger;
            if (!shield && !dagger)
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

            if (shield)
            {
                WeaponSimple_SwordAndShield swordAndShield = (WeaponSimple_SwordAndShield)weapon;
                bool ready = player.CanMove && player.IsAvailableGuard && swordAndShield.isGuardAvailable &&
                             (player.MP > 0 || player.GetCustomStatUnsafe("INFINITYMP") > 0);
                if (!sustainedDefense && defenseHeld && Time.unscaledTime - defenseStartedAt >= 0.65f)
                {
                    ReleaseDefense(player);
                    defenseCooldownUntil = Time.unscaledTime + 0.3f;
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

            WeaponSimple_Dagger daggerWeapon = (WeaponSimple_Dagger)weapon;
            if (threatened && Time.unscaledTime >= nextParry && player.CanMove &&
                !daggerWeapon.parryReserved && player.MP >= daggerWeapon.ParryCost)
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
                nextParry = Time.unscaledTime + 0.35f;
                return true;
            }
            return false;
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
                if (currentDistance <= 16f && currentDistance < previousDistance)
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
            if (worldObjectFloor == player.currentFloorGuid && Time.unscaledTime < nextWorldObjectScan) return;
            bool floorChanged = worldObjectFloor != player.currentFloorGuid;
            if (!floorChanged && cachedAnvil != null && cachedBossSpawner != null && cachedEntrance != null) return;
            worldObjectFloor = player.currentFloorGuid;
            nextWorldObjectScan = Time.unscaledTime + 1f;
            if (floorChanged) ResetWorldObjectsOnly();
            if (cachedAnvil == null)
                cachedAnvil = Resources.FindObjectsOfTypeAll<Anvil>()
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy && candidate.netId != 0 &&
                                        !SkippedAnvils.Contains(candidate.netId))
                    .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                    .FirstOrDefault();
            if (cachedBossSpawner == null)
                cachedBossSpawner = Resources.FindObjectsOfTypeAll<BossSpawner>()
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
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
                cachedEntrance = Resources.FindObjectsOfTypeAll<Interactable>()
                    .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy &&
                                        (candidate.GetComponent<GoToNextPlaceTogether>() != null ||
                                         candidate.GetComponent<GoToNextStage>() != null ||
                                         candidate.GetComponent<DungeonStair>() is DungeonStair stair && stair.stairDir == EStairDir.Down))
                    .Where(candidate =>
                    {
                        FloorGenerator floor = candidate.GetComponentInParent<FloorGenerator>();
                        return floor != null && floor.guid == player.currentFloorGuid;
                    })
                    .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
                    .FirstOrDefault();
        }

        private static void ResetWorldObjectsOnly()
        {
            cachedAnvil = null;
            cachedBossSpawner = null;
            cachedEntrance = null;
            cachedReward = null;
        }

        private static void ResetWorldObjectCache()
        {
            nextWorldObjectScan = 0f;
            worldObjectFloor = null;
            cachedAnvil = null;
            cachedBossSpawner = null;
            cachedEntrance = null;
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

        private static bool TryUseCombatAbility(PlayerAvatar player, UnitAvatar enemy, Vector2 toEnemy)
        {
            if (heldCombatAbility >= 0 || Time.unscaledTime < nextCombatAbility) return false;
            IntegratedActionController actions = player.GetComponent<IntegratedActionController>();
            if (actions == null) return false;

            float distanceSquared = toEnemy.sqrMagnitude;
            if (distanceSquared >= 25f && distanceSquared <= 100f && Time.unscaledTime >= nextDash)
            {
                ReleaseAttack(player);
                player.Dash(player.transform.position + (Vector3)(toEnemy.normalized * 4f));
                nextDash = Time.unscaledTime + 2.5f;
                nextCombatAbility = Time.unscaledTime + 0.4f;
                return true;
            }

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
                    weaponController?.currentWeapon is WeaponSimple_Dagger)
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
            Plugin.LogInfo($"AFK status: action={action}, player={player.Name}, floor={ShortGuid(player.currentFloorGuid)}, " +
                           $"pos={player.transform.position}, enemy={DescribeUnit(enemy)}, move={movement}, " +
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
            player?.AttackButtonUp();
            attackHeld = false;
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
            ReleaseAttack(player);
            ReleaseCombatAbility(player, force: true);
            autoPilotAimOwner = null;
            autoPilotAimPoint = Vector3.zero;
            autoPilotAimActive = false;
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
        private static void Postfix(Vector3 to, Color color, float radius, UI_AOEWarning __result) =>
            AutoPilot.RegisterHostileAoe(__result, to, radius, color);
    }

    [HarmonyPatch(typeof(AOEWarningFactory), nameof(AOEWarningFactory.CreateAoe_Ellipse),
        new[] { typeof(Vector3), typeof(Vector3), typeof(Color), typeof(float), typeof(float) })]
    internal static class AutoPilotEllipseWarningPatch
    {
        private static void Postfix(Vector3 to, Color color, float radius, UI_AOEWarning __result) =>
            AutoPilot.RegisterHostileAoe(__result, to, radius, color);
    }
}
