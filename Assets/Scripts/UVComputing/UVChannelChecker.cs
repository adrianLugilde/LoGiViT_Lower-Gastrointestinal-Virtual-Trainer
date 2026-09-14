using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class UVChannelChecker : MonoBehaviour
{
    [Header("Target (leave empty to use this GameObject's renderer)")]
    public Mesh meshOverride;

    [Header("Results — right-click → Check UV Channels")]
    [TextArea(1, 2)] public string uv0 = "not checked";
    [TextArea(1, 2)] public string uv1 = "not checked";
    [TextArea(1, 2)] public string uv2 = "not checked";
    [TextArea(1, 2)] public string uv3 = "not checked";
    [TextArea(1, 2)] public string uv4 = "not checked";

    private void OnValidate() => CheckUVChannels();

    [ContextMenu("Check UV Channels")]
    public void CheckUVChannels()
    {
        var mesh = ResolveMesh();
        if (mesh == null)
        {
            uv0 = uv1 = uv2 = uv3 = uv4 = "NO MESH FOUND";
            return;
        }

        var channels = new List<List<Vector2>>();
        for (int i = 0; i < 5; i++)
        {
            var list = new List<Vector2>();
            mesh.GetUVs(i, list);
            channels.Add(list);
        }

        uv0 = Describe(0, channels);
        uv1 = Describe(1, channels);
        uv2 = Describe(2, channels);
        uv3 = Describe(3, channels);
        uv4 = Describe(4, channels);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    private string Describe(int channel, List<List<Vector2>> all)
    {
        var verts = all[channel];

        if (verts.Count == 0)
            return "EMPTY";

        // Duplicate check
        for (int j = 0; j < channel; j++)
        {
            if (IsDuplicate(verts, all[j]))
                return $"DUPLICATE of UV{j}  ({verts.Count} verts)";
        }

        // All-zero check
        bool allZero = true;
        for (int v = 0; v < Mathf.Min(verts.Count, 20); v++)
            if (verts[v] != Vector2.zero) { allZero = false; break; }

        Vector2 min = verts[0], max = verts[0];
        foreach (var uv in verts) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }

        string zeroWarn = allZero ? "  !! ALL ZERO" : "";
        return $"OK  {verts.Count} verts  range X:[{min.x:F2}..{max.x:F2}] Y:[{min.y:F2}..{max.y:F2}]{zeroWarn}";
    }

    private bool IsDuplicate(List<Vector2> a, List<Vector2> b)
    {
        if (a.Count != b.Count || a.Count == 0) return false;
        int step = Mathf.Max(1, a.Count / 20);
        for (int i = 0; i < a.Count; i += step)
            if (a[i] != b[i]) return false;
        return true;
    }

    private Mesh ResolveMesh()
    {
        if (meshOverride != null) return meshOverride;
        var smr = GetComponent<SkinnedMeshRenderer>();
        if (smr != null) return smr.sharedMesh;
        var mf = GetComponent<MeshFilter>();
        if (mf != null) return mf.sharedMesh;
        return null;
    }
}
