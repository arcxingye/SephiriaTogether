using System;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.API;
using Mirror;
using SephiriaTogether;
using UnityEngine;

// Match the inspected Fizzy layouts and pre-Mirror admission rule. The actual
// game's private fields are also checked by validate-compatibility.ps1.
namespace Mirror.FizzySteam
{
    public class NextServer
    {
        private int maxConnections;
        public NextServer(int limit) { maxConnections = limit; }
        public bool Admit(int remoteCount) => remoteCount < maxConnections;
    }
    public class LegacyServer
    {
        private int maxConnections;
        public LegacyServer(int limit) { maxConnections = limit; }
        public bool Admit(int remoteCount) => remoteCount < maxConnections;
    }
    public class FizzySteamworks
    {
        private static object server;
        public FizzySteamworks(object value) { server = value; }
        public object Server => server;
    }
}

internal static class CapacityTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (bool legacy in new[] { false, true })
        {
            object server = legacy ? (object)new Mirror.FizzySteam.LegacyServer(4) : new Mirror.FizzySteam.NextServer(4);
            Func<int, bool> admit = count => legacy ? ((Mirror.FizzySteam.LegacyServer)server).Admit(count) : ((Mirror.FizzySteam.NextServer)server).Admit(count);
            var transport = new Mirror.FizzySteam.FizzySteamworks(server);
            NetworkManager.singleton = new NetworkManager { maxConnections = 16, transport = transport };
            NetworkServer.active = true;
            NetworkServer.maxConnections = 16;
            Plugin.playerLimit.Value = 16;
            check(!admit(4), "Reproduce the sixth total player's rejection below Mirror despite a Mirror cap of 16");
            PlayerLimit.EnsureActiveCapacity();
            check(admit(4) && NetworkServer.maxConnections == 16 && PlayerLimit.ActiveCapacity == 16,
                "Late activation synchronizes the already-listening Steam server");
            check(ReferenceEquals(transport.Server, server), "Capacity update must not restart transport or discard existing connections");
            PlayerLimit.ApplyServerCapacity(8);
            check(admit(6) && 7 < NetworkServer.maxConnections, "An eight-player room admits the eighth player");
            check(!(8 < NetworkServer.maxConnections), "Mirror still rejects a ninth player from an eight-player room including its host");
            Plugin.playerLimit.Value = 100;
            PlayerLimit.EnsureActiveCapacity();
            check(PlayerLimit.ActiveCapacity == 8 && NetworkManager.singleton.maxConnections == 8,
                "Editing the maximum must not enlarge an already selected room");
            PlayerLimit.ApplyServerCapacity(80);
            check(admit(55) && NetworkServer.maxConnections == 80, "Transport supports room caps above 50");
            CustomGameLauncher.Current = new CustomGameEntity { maxPlayers = 1 };
            PlayerLimit.ApplyServerCapacity(80);
            check(NetworkServer.maxConnections == 1, "Single-player custom-mode limits must not be expanded");
            CustomGameLauncher.Current = null;
            PlayerLimit.ClearServerCapacity();
            App.Initialized = true;
            SingletonObject.SteamManager = new GameObject
            {
                Component = new LobbyManager { HasLobby = true, Lobby = new LobbyData { IsOwner = true, MaxMembers = 3 } }
            };
            PlayerLimit.EnsureActiveCapacity();
            check(PlayerLimit.ActiveCapacity == 3 && !admit(3), "Late activation preserves an existing Steam lobby's selected small cap");
            PlayerLimit.ClearServerCapacity();
            SingletonObject.SteamManager = null;
            IpLobby.IsCreated = true;
            IpLobby.MaxPlayers = 6;
            NetworkManager.singleton.transport = new object();
            PlayerLimit.EnsureActiveCapacity();
            check(NetworkServer.maxConnections == 6, "Existing IP room keeps its own capacity without requiring Steam internals");
            PlayerLimit.ClearServerCapacity();
            IpLobby.IsCreated = false;
            NetworkServer.active = false;
            App.Initialized = false;
        }
        check(SteamTransportCapacity.Synchronize(new Mirror.FizzySteam.FizzySteamworks(null), 16).Contains("not listening"),
            "Idle Steam transport is handled without dereferencing a missing server");
        NetworkServer.active = true;
        NetworkManager.singleton = new NetworkManager { transport = new object() };
        Plugin.playerLimit.Value = 16;
        var eosCallback = typeof(EosRoomCapacityPatch).GetMethod("Postfix", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        eosCallback.Invoke(null, new object[] { new EOSLobbyInfo { maxMembers = 6 } });
        check(NetworkServer.maxConnections == 6 && PlayerLimit.ActiveCapacity == 6, "EOS room callback applies its reported member cap");
        eosCallback.Invoke(null, new object[] { new EOSLobbyInfo { maxMembers = 0 } });
        check(PlayerLimit.ActiveCapacity == 6, "Incomplete EOS metadata must not collapse the room to one player");
        PlayerLimit.ClearServerCapacity();
        NetworkServer.active = false;
    }
}
