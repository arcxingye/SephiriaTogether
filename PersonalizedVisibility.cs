using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class PersonalizedVisibility
    {
        private static readonly Dictionary<uint, NetworkConnectionToClient> Owners =
            new Dictionary<uint, NetworkConnectionToClient>();
        private static readonly MethodInfo HideForConnection = AccessTools.Method(
            typeof(NetworkServer), "HideForConnection",
            new[] { typeof(NetworkIdentity), typeof(NetworkConnectionToClient) });

        internal static void Register(NetworkIdentity identity, NetworkConnectionToClient owner)
        {
            if (!NetworkServer.active || identity == null || owner == null) return;
            Owners[identity.netId] = owner;
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values.ToArray())
                Apply(identity, owner, connection);
            if (Plugin.InstanceForPatches != null && owner != NetworkServer.localConnection)
                Plugin.InstanceForPatches.StartCoroutine(HideOnHost(identity));
        }

        internal static void Unregister(NetworkIdentity identity)
        {
            if (identity != null) Owners.Remove(identity.netId);
        }

        internal static void ApplyToConnection(NetworkConnectionToClient connection)
        {
            if (!NetworkServer.active || connection == null) return;
            foreach (KeyValuePair<uint, NetworkConnectionToClient> entry in Owners.ToArray())
            {
                if (!NetworkServer.spawned.TryGetValue(entry.Key, out NetworkIdentity identity) || identity == null)
                {
                    Owners.Remove(entry.Key);
                    continue;
                }
                Apply(identity, entry.Value, connection);
            }
        }

        internal static void Clear() => Owners.Clear();

        private static void Apply(
            NetworkIdentity identity,
            NetworkConnectionToClient owner,
            NetworkConnectionToClient connection)
        {
            if (connection == null || connection == owner || connection == NetworkServer.localConnection) return;
            HideForConnection?.Invoke(null, new object[] { identity, connection });
        }

        private static IEnumerator HideOnHost(NetworkIdentity identity)
        {
            yield return null;
            if (identity == null) yield break;
            foreach (Renderer renderer in identity.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            if (UIManager.Instance != null)
                UIManager.Instance.GetElement<UI_MapPanel>()?.RemoveIcon(identity.transform);
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnServerAddPlayer))]
    internal static class PersonalizedVisibilityNewPlayerPatch
    {
        private static void Postfix(NetworkConnectionToClient conn) =>
            PersonalizedVisibility.ApplyToConnection(conn);
    }
}
