#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Management;

[InitializeOnLoad]
public static class XRPlayModeCleanup
{
    static XRPlayModeCleanup()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        Debug.Log($"[XRPlayModeCleanup] PlayModeStateChange: {state}");
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            var xr = XRGeneralSettings.Instance;
            if (xr != null && xr.Manager != null)
            {
                try
                {
                    // Stop and destroy loaders to avoid subsystems lingering in Editor
                    xr.Manager.StopSubsystems();
                    xr.Manager.DeinitializeLoader();
                }
                catch (System.Exception e)
                {
                    Debug.Log($"[XRPlayModeCleanup] {e.Message}");
                }
            }
        }
    }
}
#endif
