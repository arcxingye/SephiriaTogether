using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SephiriaTogether
{
    [HarmonyPatch(typeof(UI_MultiplayerHUD), "Update")]
    internal static class IpMultiplayerHudPatch
    {
        private static float nextRefresh;

        private static bool Prefix(UI_MultiplayerHUD __instance)
        {
            if (!IpTransport.IsActive) return true;
            PlayerSpawner local = Traverse.Create(__instance).Field("mySpawner").GetValue<PlayerSpawner>();
            if (local == null || Time.unscaledTime < nextRefresh) return false;
            nextRefresh = Time.unscaledTime + 0.5f;

            foreach (PlayerSpawner player in new List<PlayerSpawner>(PlayerSpawner.MultiplayerList))
            {
                if (player == null || player == local || player.PlayerAvatar == null || player.HPBarObject == null)
                    continue;
                if (player.HPBarObject.transform.parent != __instance.contentsZone)
                {
                    player.HPBarObject.transform.SetParent(__instance.contentsZone);
                    player.HPBarObject.transform.localScale = Vector3.one;
                }
                player.HPBarObject.gameObject.SetActive(true);
                player.HPBarObject.SetSteamProfile("", null);
                if (player.WorldUserName != null)
                {
                    player.WorldUserName.text = player.PlayerAvatar.Name;
                    player.WorldUserName.gameObject.SetActive(true);
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UIManager), nameof(UIManager.DisconnectPlayerObject))]
    internal static class IpSafeUiDisconnectPatch
    {
        private static bool Prefix(UIManager __instance)
        {
            if (!IpTransport.IsActive) return true;
            Traverse traverse = Traverse.Create(__instance);
            GameObject connected = traverse.Field("connectedPlayer").GetValue<GameObject>();
            if (connected == null) return false;
            List<GameObject> objects = traverse.Field("allUIBaseObjects").GetValue<List<GameObject>>();
            if (objects != null)
            {
                foreach (GameObject item in objects.ToArray())
                {
                    if (item == null) continue;
                    try
                    {
                        if (item.TryGetComponent<IUIPlayerSettable>(out IUIPlayerSettable component))
                            component.Disconnect();
                    }
                    catch (Exception exception)
                    {
                        Plugin.LogInfo("IP UI disconnect skipped invalid element: " + exception.Message);
                    }
                }
            }
            traverse.Field("connectedPlayer").SetValue(null);
            return false;
        }
    }
}
