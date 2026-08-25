using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class LanRoomDiscovery
    {
        internal const ushort DiscoveryPort = 7780;
        private const string QueryMagic = "STQ";
        private const string ResponseMagic = "STR";
        private const string LegacyDiscoveryVersion = "3.7.0";
        private const float RoomTimeout = 10f;

        internal sealed class Room
        {
            internal string Address;
            internal ushort Port;
            internal string Name;
            internal int Players;
            internal int MaxPlayers;
            internal string Chapter;
            internal string GameVersion;
            internal string ModVersion;
            internal bool HasGameVersion;
            internal float LastSeen;
        }

        private sealed class Query
        {
            internal string Address;
            internal string Nonce;
            internal string RequestedModVersion;
            internal bool SupportsGameVersion;
        }

        private static readonly object Sync = new object();
        private static readonly List<Query> PendingQueries = new List<Query>();
        private static readonly List<Room> PendingRooms = new List<Room>();
        private static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        private static readonly string InstanceId = Guid.NewGuid().ToString("N").Substring(0, 12);
        private static UdpClient listener;
        private static Thread receiveThread;
        private static bool listening;
        private static bool dirty;
        private static string activeNonce;

        internal static void StartListening()
        {
            if (listening) return;
            try
            {
                listener = new UdpClient();
                listener.ExclusiveAddressUse = false;
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                listening = true;
                receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "SephiriaLanRoomDiscovery" };
                receiveThread.Start();
                Plugin.LogInfo("LAN room query service listening on port " + DiscoveryPort + ".");
            }
            catch (Exception exception)
            {
                listening = false;
                listener = null;
                Plugin.LogInfo("LAN room query service failed: " + exception.Message);
            }
        }

        internal static void StopListening()
        {
            listening = false;
            try { listener?.Close(); } catch (Exception) { }
            listener = null;
        }

        internal static void Refresh()
        {
            activeNonce = Guid.NewGuid().ToString("N").Substring(0, 8);
            lock (Sync)
            {
                Rooms.Clear();
                PendingRooms.Clear();
                dirty = true;
            }
            SendQueries(activeNonce);
        }

        internal static void Tick()
        {
            List<Query> queries;
            lock (Sync)
            {
                queries = new List<Query>(PendingQueries);
                PendingQueries.Clear();
                foreach (Room room in PendingRooms)
                {
                    room.LastSeen = Time.unscaledTime;
                    string key = room.Address + ":" + room.Port;
                    if (!Rooms.TryGetValue(key, out Room existing) || room.HasGameVersion ||
                        !existing.HasGameVersion)
                        Rooms[key] = room;
                    else
                        existing.LastSeen = room.LastSeen;
                    dirty = true;
                }
                PendingRooms.Clear();
                List<string> expired = new List<string>();
                foreach (KeyValuePair<string, Room> room in Rooms)
                    if (Time.unscaledTime - room.Value.LastSeen > RoomTimeout) expired.Add(room.Key);
                foreach (string key in expired)
                {
                    Rooms.Remove(key);
                    dirty = true;
                }
            }

            if (IpLobby.IsCreated)
                foreach (Query query in queries) SendResponse(query);
            if (dirty)
            {
                dirty = false;
                LanRoomListUi.NotifyRoomsChanged();
            }
        }

        internal static List<Room> Snapshot()
        {
            lock (Sync) return new List<Room>(Rooms.Values);
        }

        private static void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            while (listening)
            {
                try
                {
                    byte[] data = listener.Receive(ref remote);
                    ParsePacket(Encoding.UTF8.GetString(data), remote.Address.ToString());
                }
                catch (Exception)
                {
                    if (!listening) break;
                }
            }
        }

        private static void ParsePacket(string text, string sourceAddress)
        {
            string[] parts = (text ?? "").Split('|');
            if (parts.Length < 4 || string.IsNullOrEmpty(parts[1])) return;
            if (parts[0] == QueryMagic)
            {
                if (parts[3] == InstanceId) return;
                lock (Sync)
                    PendingQueries.Add(new Query
                    {
                        Address = sourceAddress,
                        Nonce = parts[2],
                        RequestedModVersion = parts[1],
                        SupportsGameVersion = parts.Length >= 5
                    });
                return;
            }
            if (parts[0] != ResponseMagic || parts.Length < 9 || parts[2] != activeNonce ||
                parts[3] == InstanceId) return;
            if (!ushort.TryParse(parts[4], out ushort port) || port == 0 ||
                !int.TryParse(parts[5], out int players) || !int.TryParse(parts[6], out int maxPlayers)) return;
            bool hasGameVersion = parts.Length >= 10 &&
                                  parts[8].StartsWith("G:", StringComparison.Ordinal) &&
                                  Version.TryParse(parts[8].Substring(2), out _);
            int nameIndex = hasGameVersion ? 9 : 8;
            string name = string.Join("|", parts, nameIndex, parts.Length - nameIndex).Trim();
            if (name.Length == 0) name = "Host";
            lock (Sync)
                PendingRooms.Add(new Room
                {
                    Address = sourceAddress,
                    Port = port,
                    Players = Math.Max(1, players),
                    MaxPlayers = Math.Max(2, maxPlayers),
                    Chapter = parts[7],
                    GameVersion = hasGameVersion
                        ? parts[8].StartsWith("G:", StringComparison.Ordinal) ? parts[8].Substring(2) : parts[8]
                        : "",
                    ModVersion = parts[1],
                    HasGameVersion = hasGameVersion,
                    Name = name
                });
        }

        private static void SendResponse(Query query)
        {
            try
            {
                // A 3.7 client rejects a response whose version field is not
                // exactly 3.7. Keep the legacy response shape for legacy
                // queries, while modern queries receive the real host version.
                string responseVersion = query.SupportsGameVersion ? Plugin.PluginVersion :
                    query.RequestedModVersion ?? Plugin.PluginVersion;
                string response = ResponseMagic + "|" + responseVersion + "|" + query.Nonce + "|" + InstanceId +
                                  "|" + IpTransport.ActivePort + "|" + Plugin.PlayerCount + "|" +
                                  IpLobby.MaxPlayers + "|" + Chapter() + "|" +
                                  (query.SupportsGameVersion ? "G:" + Application.version + "|" : "") + IpLobby.RoomName;
                byte[] data = Encoding.UTF8.GetBytes(response);
                using (UdpClient sender = new UdpClient())
                    sender.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(query.Address), DiscoveryPort));
            }
            catch (Exception exception)
            {
                Plugin.LogInfo("LAN room response failed: " + exception.Message);
            }
        }

        private static void SendQueries(string nonce)
        {
            try
            {
                HashSet<string> targets = DiscoveryTargets();
                using (UdpClient sender = new UdpClient())
                {
                    sender.EnableBroadcast = true;
                    SendQueryVariant(sender, targets, Plugin.PluginVersion, nonce, includeGameVersion: true);
                    if (!string.Equals(Plugin.PluginVersion, LegacyDiscoveryVersion,
                        StringComparison.OrdinalIgnoreCase))
                        SendQueryVariant(sender, targets, LegacyDiscoveryVersion, nonce,
                            includeGameVersion: false);
                }
                Plugin.LogInfo("LAN room refresh sent to " + targets.Count + " target(s) with compatibility queries.");
            }
            catch (Exception exception)
            {
                Plugin.LogInfo("LAN room refresh failed: " + exception.Message);
            }
        }

        private static void SendQueryVariant(UdpClient sender, HashSet<string> targets,
            string requestedModVersion, string nonce, bool includeGameVersion)
        {
            string suffix = includeGameVersion ? "|" + Application.version : "";
            byte[] data = Encoding.UTF8.GetBytes(QueryMagic + "|" + requestedModVersion + "|" + nonce +
                                                 "|" + InstanceId + suffix);
            foreach (string target in targets)
                sender.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(target), DiscoveryPort));
        }

        private static HashSet<string> DiscoveryTargets()
        {
            HashSet<string> targets = new HashSet<string> { IPAddress.Broadcast.ToString() };
            HashSet<string> scannedPrefixes = new HashSet<string>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address.Address)) continue;
                    byte[] ip = address.Address.GetAddressBytes();
                    if (ip[0] == 169 && ip[1] == 254) continue;
                    if (address.IPv4Mask != null)
                    {
                        byte[] mask = address.IPv4Mask.GetAddressBytes();
                        byte[] broadcast = new byte[4];
                        for (int i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | ~mask[i]);
                        targets.Add(new IPAddress(broadcast).ToString());
                    }
                    string prefix = ip[0] + "." + ip[1] + "." + ip[2];
                    if (scannedPrefixes.Count >= 2 || !scannedPrefixes.Add(prefix)) continue;
                    for (int host = 1; host <= 254; host++)
                        if (host != ip[3]) targets.Add(prefix + "." + host);
                }
            }
            return targets;
        }

        private static string Chapter()
        {
            try
            {
                if (DungeonManager.Instance != null && DungeonManager.Instance.Race != null)
                    return DungeonManager.Instance.Race.actualChapterNum.ToString();
            }
            catch (Exception) { }
            return "-";
        }
    }
}
