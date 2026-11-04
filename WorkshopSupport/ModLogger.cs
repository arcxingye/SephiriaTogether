#if WORKSHOP
namespace SephiriaTogether.WorkshopSupport
{
    internal sealed class ModLogger
    {
        internal void LogInfo(object message) => UnityEngine.Debug.Log("[Sephiria Together] " + message);
        internal void LogWarning(object message) => UnityEngine.Debug.LogWarning("[Sephiria Together] " + message);
        internal void LogDebug(object message) => UnityEngine.Debug.Log("[Sephiria Together] " + message);
    }
}
#endif
