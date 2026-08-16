using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal struct RescueRequestMessage : NetworkMessage { }

    internal struct RescueAlertMessage : NetworkMessage
    {
        public string playerName;
    }

    internal static class RescueAlerts
    {
        private static readonly Dictionary<int, double> LastRequests = new Dictionary<int, double>();
        private static readonly HashSet<uint> KnownDownPlayers = new HashSet<uint>();
        private static readonly Dictionary<uint, float> DownFirstSeen = new Dictionary<uint, float>();
        private static string bannerPlayer = "";
        private static float bannerUntil;
        private static bool urgent;
        private static GUIStyle bannerStyle;
        private static Texture2D bannerTexture;

        internal static void RegisterServerMessages()
        {
            ConfigureSerialization();
            NetworkServer.RegisterHandler<RescueRequestMessage>(OnServerRequest, true);
            LastRequests.Clear();
        }

        internal static void RegisterClientMessages()
        {
            ConfigureSerialization();
            NetworkClient.RegisterHandler<RescueAlertMessage>(OnClientAlert, true);
            KnownDownPlayers.Clear();
            DownFirstSeen.Clear();
        }

        internal static void Update()
        {
            PlayerAvatar local = CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayer : null;
            if (Plugin.rescueShortcut.Value.IsDown() && local != null && local.IsDead &&
                CatchUpRewards.HostSupportsProtocol())
            {
                NetworkClient.Send(new RescueRequestMessage());
                Show(local.Name, true);
            }

            if (PlayerSpawner.MultiplayerList == null) return;
            HashSet<uint> current = new HashSet<uint>();
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar player = spawner != null ? spawner.PlayerAvatar : null;
                if (player == null || player == local || !player.IsDead) continue;
                current.Add(player.netId);
                if (!DownFirstSeen.TryGetValue(player.netId, out float firstSeen))
                {
                    DownFirstSeen[player.netId] = Time.unscaledTime;
                    Plugin.LogInfo($"Rescue state first seen dead: {Describe(player)}");
                    continue;
                }
                if (Time.unscaledTime - firstSeen >= 3f && KnownDownPlayers.Add(player.netId))
                {
                    Plugin.LogInfo($"Rescue state confirmed dead after grace period: {Describe(player)}");
                    Show(player.Name, false);
                }
            }
            foreach (uint netId in DownFirstSeen.Keys.Where(netId => !current.Contains(netId)).ToArray())
            {
                if (KnownDownPlayers.Contains(netId)) Plugin.LogInfo($"Rescue state cleared: netId={netId}");
                DownFirstSeen.Remove(netId);
                KnownDownPlayers.Remove(netId);
            }
        }

        internal static void Draw()
        {
            PlayerAvatar local = CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayer : null;
            PlayerAvatar down = PlayerSpawner.MultiplayerList?
                .Where(spawner => spawner?.PlayerAvatar != null && spawner.PlayerAvatar != local &&
                    spawner.PlayerAvatar.IsDead && KnownDownPlayers.Contains(spawner.PlayerAvatar.netId))
                .Select(spawner => spawner.PlayerAvatar)
                .FirstOrDefault();
            if (down != null && Time.unscaledTime >= bannerUntil)
            {
                bannerPlayer = down.Name;
                urgent = false;
            }
            if (down == null && Time.unscaledTime >= bannerUntil) return;

            EnsureStyle();
            float pulse = urgent ? 0.72f + Mathf.PingPong(Time.unscaledTime * 1.8f, 0.28f) : 0.9f;
            GUI.color = new Color(1f, pulse, pulse, 1f);
            Rect area = new Rect(Mathf.Max(12f, Screen.width * 0.5f - 310f), 32f, Mathf.Min(620f, Screen.width - 24f), 92f);
            GUI.Box(area, string.Format(urgent ? MenuText.Get("RescueRequested") : MenuText.Get("PlayerDown"), bannerPlayer), bannerStyle);
            GUI.color = Color.white;
        }

        internal static void ClearClient()
        {
            KnownDownPlayers.Clear();
            DownFirstSeen.Clear();
            bannerPlayer = "";
            bannerUntil = 0f;
            urgent = false;
        }

        internal static void ClearServer() => LastRequests.Clear();

        private static void OnServerRequest(NetworkConnectionToClient connection, RescueRequestMessage message)
        {
            PlayerAvatar player = connection?.identity != null ? connection.identity.GetComponent<PlayerAvatar>() : null;
            if (player == null || !player.IsDead || !CatchUpRewards.IsModdedConnection(connection))
            {
                Plugin.LogInfo($"Rescue request rejected: conn={connection?.connectionId ?? -1}, player={Describe(player)}, modded={CatchUpRewards.IsModdedConnection(connection)}");
                return;
            }
            double now = NetworkTime.time;
            if (LastRequests.TryGetValue(connection.connectionId, out double last) && now - last < 10d)
            {
                Plugin.LogInfo($"Rescue request rate-limited: conn={connection.connectionId}");
                return;
            }
            LastRequests[connection.connectionId] = now;
            Plugin.LogInfo($"Rescue request accepted: conn={connection.connectionId}, {Describe(player)}");
            RescueAlertMessage alert = new RescueAlertMessage
            {
                playerName = SafePlayerName(player.Name)
            };
            foreach (NetworkConnectionToClient target in NetworkServer.connections.Values)
            {
                if (target != null && target.isReady && CatchUpRewards.IsModdedConnection(target)) target.Send(alert);
            }
        }

        private static void OnClientAlert(RescueAlertMessage message)
        {
            Show(message.playerName, true);
            if (GameLogWriter.Instance != null)
                GameLogWriter.Instance.WriteLog(string.Format(MenuText.Get("RescueRequested"), message.playerName), Color.red);
        }

        private static void Show(string playerName, bool isUrgent)
        {
            bannerPlayer = string.IsNullOrEmpty(playerName) ? MenuText.Get("UnknownPlayer") : playerName;
            urgent = isUrgent;
            bannerUntil = Time.unscaledTime + (isUrgent ? 12f : 7f);
        }

        private static void ConfigureSerialization()
        {
            Writer<RescueRequestMessage>.write = (writer, value) => { };
            Reader<RescueRequestMessage>.read = reader => new RescueRequestMessage();
            Writer<RescueAlertMessage>.write = (writer, value) =>
            {
                writer.WriteString(value.playerName);
            };
            Reader<RescueAlertMessage>.read = reader => new RescueAlertMessage
            {
                playerName = reader.ReadString()
            };
        }

        private static void EnsureStyle()
        {
            if (bannerStyle != null) return;
            bannerTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            bannerTexture.SetPixel(0, 0, new Color(0.52f, 0.025f, 0.035f, 0.97f));
            bannerTexture.Apply();
            bannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(16, 16, 10, 10)
            };
            bannerStyle.normal.background = bannerTexture;
            bannerStyle.normal.textColor = Color.white;
        }

        private static string SafePlayerName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return MenuText.Get("UnknownPlayer");
            string safe = value.Replace("<", "").Replace(">", "").Replace("\r", " ").Replace("\n", " ").Trim();
            return safe.Length > 24 ? safe.Substring(0, 24) : safe;
        }

        private static string Describe(PlayerAvatar player) => player == null
            ? "null"
            : $"name={SafePlayerName(player.Name)}, netId={player.netId}, dead={player.IsDead}, hp={player.hp:0.##}, " +
              $"floor={Short(player.currentFloorGuid)}, pos={player.transform.position}, inDungeon={player.isInDungeon}";

        private static string Short(string value) => string.IsNullOrEmpty(value)
            ? "-"
            : value.Substring(0, Math.Min(8, value.Length));
    }
}
