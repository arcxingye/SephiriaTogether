using System;
using System.Reflection;

namespace SephiriaTogether
{
    internal static class SteamTransportCapacity
    {
        // FizzySteamworks snapshots the manager limit into its private server
        // at startup. Workshop can load after that; changing Mirror alone leaves
        // the transport rejecting the fifth remote client before Mirror sees it.
        internal static string Synchronize(object transport, int capacity)
        {
            if (transport == null) return "none";
            Type type = transport.GetType();
            if (type.FullName != "Mirror.FizzySteam.FizzySteamworks") return type.Name + " (Mirror cap)";
            try
            {
                FieldInfo serverField = type.GetField("server", BindingFlags.NonPublic | BindingFlags.Static);
                if (serverField == null) return "FizzySteamworks: server field unavailable";
                object server = serverField.GetValue(null);
                if (server == null) return "FizzySteamworks: not listening";
                Type serverType = server.GetType();
                if (serverType.FullName != "Mirror.FizzySteam.NextServer" &&
                    serverType.FullName != "Mirror.FizzySteam.LegacyServer")
                    return "FizzySteamworks: unsupported server " + serverType.Name;
                FieldInfo limit = serverType.GetField("maxConnections", BindingFlags.NonPublic | BindingFlags.Instance);
                if (limit == null || limit.FieldType != typeof(int))
                    return serverType.Name + ": capacity field unavailable";
                int before = (int)limit.GetValue(server);
                // This count excludes the local host. Mirror continues enforcing
                // the selected total room cap, including the host, afterwards.
                limit.SetValue(server, capacity);
                return $"{serverType.Name}={limit.GetValue(server)} (was {before})";
            }
            catch (Exception exception)
            {
                return "FizzySteamworks: capacity sync failed: " + exception.Message;
            }
        }
    }
}
