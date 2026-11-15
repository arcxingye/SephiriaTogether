using System;
using System.Collections.Generic;

// Only external APIs are simulated. Tests execute the production handshake,
// its real registered handlers and a controllable clock/connection lifecycle.
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { }
        public HarmonyPatch(Type type, string method, Type[] arguments) { }
    }
    public sealed class HarmonyPriority : Attribute { public HarmonyPriority(int priority) { } }
    public static class Priority { public const int First = 800; }
}

namespace UnityEngine
{
    public enum KeyCode { None, F8, F9, F10, F11, F12 }
    public static class Mathf { public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max)); }
    public static class Application { public static string version = "1.1.3"; }
    public static class Time { public static float realtimeSinceStartup; }
    public class GameObject
    {
        public object Component;
        public bool TryGetComponent<T>(out T component) where T : class { component = Component as T; return component != null; }
    }
}

namespace Mirror
{
    public interface NetworkMessage { }
    public class NetworkWriter { public void WriteString(string value) { } }
    public class NetworkReader { public string ReadString() => ""; }
    public static class Writer<T> { public static Action<NetworkWriter, T> write; }
    public static class Reader<T> { public static Func<NetworkReader, T> read; }
    public sealed class NetworkConnectionToServer { public bool isAuthenticated = true; }
    public sealed class NetworkConnectionToClient
    {
        public int connectionId;
        public bool isAuthenticated = true;
        public bool isReady = true;
        public readonly List<object> Sent = new List<object>();
        public void Send<T>(T message) => Sent.Add(message);
    }
    public static class NetworkClient
    {
        public static bool active;
        public static bool ready;
        public static NetworkConnectionToServer connection;
        public static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();
        public static readonly List<object> Sent = new List<object>();
        public static void RegisterHandler<T>(Action<T> handler, bool authenticated) => Handlers[typeof(T)] = handler;
        public static void Send<T>(T message) => Sent.Add(message);
        public static void Receive<T>(T message) => ((Action<T>)Handlers[typeof(T)])(message);
    }
    public static class NetworkServer
    {
        public static bool active;
        public static int maxConnections;
        public static NetworkConnectionToClient localConnection;
        public static readonly Dictionary<int, NetworkConnectionToClient> connections = new Dictionary<int, NetworkConnectionToClient>();
        public static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();
        public static void RegisterHandler<T>(Action<NetworkConnectionToClient, T> handler, bool authenticated) => Handlers[typeof(T)] = handler;
        public static void Receive<T>(NetworkConnectionToClient connection, T message) =>
            ((Action<NetworkConnectionToClient, T>)Handlers[typeof(T)])(connection, message);
    }
    public class NetworkManager
    {
        public static NetworkManager singleton;
        public string networkAddress;
        public int maxConnections;
        public object transport;
    }
}

namespace HeathenEngineering.SteamworksIntegration
{
    public struct LobbyData
    {
        public bool IsOwner;
        public int MaxMembers;
        public Dictionary<string, string> Metadata;
        public string this[string key] => Metadata != null && Metadata.TryGetValue(key, out string value) ? value : "";
        public static LobbyData Get(ulong id) => new LobbyData();
    }
    public class LobbyManager
    {
        public bool HasLobby;
        public LobbyData Lobby;
        public void Join(LobbyData lobby) { }
        public void Join(ulong id) { }
    }
}
namespace HeathenEngineering.SteamworksIntegration.API { public static class App { public static bool Initialized; } }
public static class SingletonObject
{
    public static UnityEngine.GameObject SteamManager;
    public static UnityEngine.GameObject Find(string name) => SteamManager;
}
public class EOSLobbyInfo { public string version; public string lobbyId; public int maxMembers; }
public class UI_MultiplayerPanel { public void OnOpened() { } public void HandleLeave() { } }
public class UI_MultiplayerPanel_E { public void HandleLeave() { } public void HandleCreated(EOSLobbyInfo info) { } }
public class OptionsBinding { }
public class CustomGameEntity { public int maxPlayers; }
public static class CustomGameLauncher { public static CustomGameEntity Current; }
public static class SteamInvitation
{
    public static bool waitForExternalConnect;
    public static void ConnectToOtherHost(string address) { }
}
public class HorayNetworkAuthenticator { public struct VersionMessage { public string version; } }
public class HorayNetworkManager
{
    public void OnStartServer() { }
    public void OnStartClient() { }
    public void OnStopClient() { }
    public void OnStopServer() { }
    public void OnServerAddPlayer(Mirror.NetworkConnectionToClient conn) { }
    public void OnServerDisconnect(Mirror.NetworkConnectionToClient conn) { }
}
namespace SephiriaTogether
{
    internal static class Plugin
    {
        internal sealed class Setting { internal int Value = 16; }
        internal static Setting playerLimit = new Setting();
        internal static void ApplyPlayerLimit() { }
        internal const string PluginVersion = "test-local";
        internal static void LogInfo(string message) { }
    }
    internal static class IpLobby
    {
        internal static bool IsCreated;
        internal static int MaxPlayers;
    }
    internal static class IpTransport { internal static ushort ActivePort = 7777; }
    internal static class CatchUpRewards
    {
        internal static int Sends;
        private static object sentConnection;
        internal static void SendHello()
        {
            if (!ReferenceEquals(sentConnection, Mirror.NetworkClient.connection))
            {
                Sends++;
                sentConnection = Mirror.NetworkClient.connection;
            }
        }
    }
    internal static class MenuText { internal static string Get(string key) => key + ": {0} / {1}"; }
    internal static class VersionReminder
    {
        internal static readonly Dictionary<string, string> Visible = new Dictionary<string, string>();
        internal static void ShowTemporary(string text, string scope, bool survivesNoLobby = false) => Visible[scope] = text;
        internal static void Dismiss(string scope) => Visible.Remove(scope);
        internal static void Clear() => Visible.Clear();
    }
}
