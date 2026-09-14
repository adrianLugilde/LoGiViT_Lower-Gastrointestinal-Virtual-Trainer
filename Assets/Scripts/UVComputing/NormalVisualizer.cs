using UnityEngine;

[ExecuteAlways]
public class NormalVisualizer : MonoBehaviour
{
    public float length = 0.02f;
    public Color color = Color.yellow;
    [Range(1, 20)] public int drawEveryN = 1;
    public bool useBakedMesh = true;

    private Mesh _bakedMesh;
    private Mesh _originalMesh;

    private void OnDrawGizmos()
    {
        var mesh = ResolveMesh();
        if (mesh == null) return;

        var vertices = mesh.vertices;
        var normals  = mesh.normals;
        if (normals == null || normals.Length == 0) return;

        Gizmos.color = color;
        for (int i = 0; i < vertices.Length; i += drawEveryN)
        {
            Vector3 worldPos    = transform.TransformPoint(vertices[i]);
            Vector3 worldNormal = transform.TransformDirection(normals[i]).normalized;
            Gizmos.DrawLine(worldPos, worldPos + worldNormal * length);
        }
    }

    [ContextMenu("Recalculate Normals")]
    public void RecalculateNormals()
    {
        var renderer = GetRenderer();
        if (renderer == null) { Debug.LogWarning("[NormalVisualizer] No renderer found.", this); return; }

        var source = GetSharedMesh(renderer);
        if (source == null) { Debug.LogWarning("[NormalVisualizer] No mesh found.", this); return; }

        // Store original so we can restore it
        if (_originalMesh == null)
            _originalMesh = source;

        var copy = Object.Instantiate(source);
        copy.name = source.name + "_recalcNormals";
        copy.RecalculateNormals();

        SetSharedMesh(renderer, copy);
        Debug.Log($"[NormalVisualizer] Applied recalculated normals to renderer. Original mesh '{source.name}' unchanged.", this);
    }

    [ContextMenu("Restore Original Mesh")]
    public void RestoreOriginal()
    {
        if (_originalMesh == null) { Debug.LogWarning("[NormalVisualizer] No original mesh stored.", this); return; }

        var renderer = GetRenderer();
        if (renderer == null) return;

        SetSharedMesh(renderer, _originalMesh);
        _originalMesh = null;
        Debug.Log("[NormalVisualizer] Restored original mesh.", this);
    }

    private Mesh ResolveMesh()
    {
        var smr = GetComponent<SkinnedMeshRenderer>();
        if (smr != null)
        {
            if (useBakedMesh)
            {
                if (_bakedMesh == null) _bakedMesh = new Mesh();
                smr.BakeMesh(_bakedMesh);
                return _bakedMesh;
            }
            return smr.sharedMesh;
        }

        var mf = GetComponent<MeshFilter>();
        return mf != null ? mf.sharedMesh : null;
    }

    private Component GetRenderer()
    {
        Component r = GetComponent<SkinnedMeshRenderer>();
        return r ?? (Component)GetComponent<MeshFilter>();
    }

    private Mesh GetSharedMesh(Component renderer)
    {
        if (renderer is SkinnedMeshRenderer smr) return smr.sharedMesh;
        if (renderer is MeshFilter mf) return mf.sharedMesh;
        return null;
    }

    private void SetSharedMesh(Component renderer, Mesh mesh)
    {
        if (renderer is SkinnedMeshRenderer smr) smr.sharedMesh = mesh;
        else if (renderer is MeshFilter mf) mf.sharedMesh = mesh;
    }

    private void OnDisable()
    {
        if (_bakedMesh != null) DestroyImmediate(_bakedMesh);
        _bakedMesh = null;
    }
}
