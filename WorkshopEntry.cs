#if WORKSHOP
using System;
using UnityEngine;

namespace SephiriaTogether
{
    public sealed class WorkshopEntry : WorkshopModBase
    {
        private GameObject runtimeObject;
        private Plugin runtime;

        protected override void OnModLoaded()
        {
            if (runtime != null) return;
            if (Context == null) throw new InvalidOperationException("Workshop context was not attached by the native loader.");
            try
            {
                runtimeObject = new GameObject("Sephiria Together Workshop Runtime");
                runtimeObject.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(runtimeObject);
                runtime = runtimeObject.AddComponent<Plugin>();
                runtime.InitializeForWorkshop(Context.Options);
                runtimeObject.SetActive(true);
            }
            catch
            {
                OnModUnloaded();
                throw;
            }
        }

        protected override void OnModUnloaded()
        {
            try { runtime?.ShutdownRuntime(); }
            finally
            {
                if (runtimeObject != null) UnityEngine.Object.Destroy(runtimeObject);
                runtime = null;
                runtimeObject = null;
            }
        }
    }
}
#endif
