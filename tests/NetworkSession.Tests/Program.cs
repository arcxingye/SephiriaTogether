using System;
using System.Linq;
using Mirror;
using SephiriaTogether;
using UnityEngine;

internal static class Program
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    private static void Tick(float time) { Time.realtimeSinceStartup = time; VersionCompatibility.Tick(); }
    private static VersionCompatibilityHelloMessage Hello(string mod = Plugin.PluginVersion) =>
        new VersionCompatibilityHelloMessage { gameVersion = Application.version, modVersion = mod };
    private static VersionCompatibilityNoticeMessage Notice(string mod = Plugin.PluginVersion, string game = "1.1.3") =>
        new VersionCompatibilityNoticeMessage { gameVersion = game, modVersion = mod };
    private static void StartClient()
    {
        VersionCompatibility.AbortClientJoin();
        NetworkServer.active = false;
        NetworkClient.active = true;
        NetworkClient.ready = false;
        NetworkClient.connection = new NetworkConnectionToServer();
        NetworkClient.Sent.Clear();
        VersionCompatibility.PrepareIpJoin("", "", "test", 7777);
        VersionCompatibility.RegisterClientMessages();
    }
    private static void TestClient()
    {
        StartClient();
        Tick(0); Tick(180);
        Check(NetworkClient.Sent.Count == 0 && VersionReminder.Visible.Count == 0, "Slow scene loading must not expire the handshake or warn before ready");
        NetworkClient.ready = true;
        Tick(181);
        Check(NetworkClient.Sent.Count == 1, "Send when a slow client finally becomes ready");
        Check(!VersionCompatibility.HostSupportsProtocolMetadata() && CatchUpRewards.Sends == 0, "No custom features before confirmed protocol");
        VersionCompatibility.RegisterClientMessages();
        Tick(182);
        Check(NetworkClient.Sent.Count == 1, "Repeated registration must not restart the retry clock");
        Tick(184); Tick(187); Tick(190); Tick(193);
        Check(NetworkClient.Sent.Count == 5, "Retry unanswered hellos while a host loads");
        Tick(211);
        Check(VersionReminder.Visible.Values.Single().StartsWith("VersionUnconfirmedWarning"), "Unknown peer must not be labelled mismatched or uninstalled");
        Tick(223);
        Check(NetworkClient.Sent.Count == 6, "Continue slow probing after the initial retry window");
        NetworkClient.Receive(Notice());
        Check(VersionCompatibility.HostSupportsProtocolMetadata() && VersionReminder.Visible.Count == 0, "Late matching notice confirms protocol and clears pending warning");
        VersionCompatibility.InspectLobby(new HeathenEngineering.SteamworksIntegration.LobbyData
        {
            Metadata = new System.Collections.Generic.Dictionary<string, string> { ["SephiriaTogether"] = "stale-version" }
        });
        Check(VersionCompatibility.HasClientVersionReport && VersionReminder.Visible.Count == 0,
            "Live version reports take precedence over stale Steam lobby labels");
        VersionCompatibility.RegisterClientMessages();
        Check(VersionCompatibility.HostSupportsProtocolMetadata(), "Registration after confirmation must retain the confirmed state");
        Tick(224); Tick(300);
        Check(NetworkClient.Sent.Count == 6 && CatchUpRewards.Sends == 1, "Stop hello retries after confirmation and enable one catch-up hello");
        NetworkClient.connection = new NetworkConnectionToServer();
        Check(!VersionCompatibility.HostSupportsProtocolMetadata() && !VersionCompatibility.HasClientVersionReport,
            "A replacement connection must not inherit protocol access or report state before the next tick");
        Tick(301);
        Check(NetworkClient.Sent.Count == 7, "Reconnect starts a new handshake");
        NetworkClient.Receive(Notice("other"));
        Tick(302);
        Check(!VersionCompatibility.HostSupportsProtocolMetadata() && CatchUpRewards.Sends == 1, "A real Mod mismatch keeps custom features gated");
        Check(VersionReminder.Visible.Values.Single().StartsWith("ModVersionMismatchWarning"), "A reported Mod mismatch has its own warning");
        NetworkClient.Receive(Notice(game: ""));
        Check(!VersionCompatibility.HostSupportsProtocolMetadata(), "Missing game version must never confirm a protocol");
        Check(VersionReminder.Visible.Values.Single().StartsWith("VersionUnconfirmedWarning"), "Incomplete reports must not describe identical Mod versions as mismatched");
        Tick(305);
        Check(NetworkClient.Sent.Count == 8, "Continue retrying after an incomplete notice");
        NetworkClient.Receive(Notice(game: "1.1.2"));
        Check(VersionReminder.Visible.Values.Single().StartsWith("GameVersionMismatchWarning"), "Game mismatch remains explicit");
        Check(!VersionCompatibility.HostSupportsProtocolMetadata(), "A 1.1.2 report cannot authorize custom features in a 1.1.3 session");
        SteamInvitation.waitForExternalConnect = true;
        VersionCompatibility.ClearClientTarget();
        NetworkClient.connection = null;
        Tick(400);
        Check(VersionCompatibility.IsPreparedForIp("test", 7777) && !VersionCompatibility.HostSupportsProtocolMetadata(), "Room handoff keeps only target metadata, never handshake confirmation");
        SteamInvitation.waitForExternalConnect = false;
        VersionCompatibility.ClearClientTarget();
        NetworkClient.active = false;
    }
    private static void TestServer()
    {
        NetworkServer.active = true;
        var first = new NetworkConnectionToClient { connectionId = 6, isReady = false };
        NetworkServer.connections[6] = first;
        VersionCompatibility.RegisterServerMessages();
        Tick(500); Tick(700);
        Check(VersionReminder.Visible.Count == 0, "Late registration tracks existing clients but does not warn while they are loading");
        first.isReady = true;
        Tick(701); Tick(731);
        Check(VersionReminder.Visible.ContainsKey("host-peer:6"), "Pending client warning is scoped to that peer");
        NetworkServer.Receive(first, Hello());
        Check(VersionCompatibility.IsProtocolCompatibleConnection(first) && first.Sent.Count == 1 && VersionReminder.Visible.Count == 0,
            "Late client hello confirms and dismisses only its own warning");
        NetworkServer.Receive(first, Hello());
        Check(first.Sent.Count == 2, "Duplicate hello must receive another notice if the previous one was lost");
        NetworkServer.Receive(first, Hello("different"));
        Check(!VersionCompatibility.IsProtocolCompatibleConnection(first) && VersionCompatibility.IsCrossVersionConnection(first), "A changed incompatible report revokes previous protocol access");
        var replacement = new NetworkConnectionToClient { connectionId = 6 };
        NetworkServer.connections[6] = replacement;
        Check(!VersionCompatibility.IsProtocolCompatibleConnection(replacement), "Reused Mirror IDs do not inherit another connection's handshake");
        int replies = first.Sent.Count;
        NetworkServer.Receive(first, Hello());
        Check(first.Sent.Count == replies, "Ignore stale connection callbacks");
        VersionCompatibility.RemoveConnection(first);
        VersionCompatibility.TrackConnection(replacement);
        Tick(732); Tick(762);
        var other = new NetworkConnectionToClient { connectionId = 7 };
        NetworkServer.connections[7] = other;
        VersionCompatibility.TrackConnection(other);
        Tick(763); Tick(793);
        NetworkServer.Receive(replacement, Hello());
        Check(!VersionReminder.Visible.ContainsKey("host-peer:6") && VersionReminder.Visible.ContainsKey("host-peer:7"), "One confirmed peer must not erase another peer's warning");
        VersionCompatibility.ClearConnections();
        Check(VersionReminder.Visible.Count == 0 && !VersionCompatibility.IsProtocolCompatibleConnection(replacement), "Server stop clears all handshake state");
        NetworkServer.connections.Clear();
        NetworkServer.active = false;
    }
    private static void Main()
    {
        TestClient();
        TestServer();
        CapacityTests.Run(Check);
        Console.WriteLine($"Network session regression tests passed: {assertions} assertions.");
    }
}
