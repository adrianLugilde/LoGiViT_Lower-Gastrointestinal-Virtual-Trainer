// Tools → Find All Volume Profiles
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static class VolumeFinder
{
    [MenuItem("Tools/Find All Volume Profiles")]
    public static void FindAllVolumes()
    {
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
        foreach (var v in volumes)
        {
            string profile = v.profile != null ? v.profile.name : "NULL";
            string type = v.isGlobal ? "Global" : "Local";
            Debug.Log($"[{type}] <b>{v.gameObject.name}</b> → Profile: <b>{profile}</b>", v.gameObject);
        }
        Debug.Log($"Total volumes found: {volumes.Length}");
    }
}
#endif