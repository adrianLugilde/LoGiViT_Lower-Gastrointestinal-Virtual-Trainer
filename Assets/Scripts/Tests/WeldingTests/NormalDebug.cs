using UnityEditor;
using UnityEngine;

public class NormalDebug : MonoBehaviour
{
    public bool isShowNormal;
    public Color color = Color.yellow;
    public float normalsLength = 0.01f;
    public int vertexToDraw = 5000;
    
    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!isShowNormal) return;

        if (!TryGetComponent<SkinnedMeshRenderer>(out var smr)) return;

        var mesh = smr.sharedMesh;
        if (mesh == null) return;

        var defaultColor = Handles.color;
        Handles.matrix = transform.localToWorldMatrix;
        Handles.color = color;
        var verts = mesh.vertices;
        var normals = mesh.normals;
        int len = vertexToDraw == -1 ? mesh.vertexCount : vertexToDraw;


        for (int i = 0; i < len; i++)
        {
            Handles.DrawLine(verts[i], verts[i] + normals[i] * normalsLength);
        }

        Handles.color = defaultColor;
#endif
    }
}