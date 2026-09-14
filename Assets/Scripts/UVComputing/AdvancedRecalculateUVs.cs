using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class AdvancedRecalculateUVs : MonoBehaviour
{
    public float scale = 1.0f;

    void Start()
    {
        RecalculateUVs();
    }

    void RecalculateUVs()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        { 
            Debug.LogError("MeshFilter component missing!");
            return;
        }

        Mesh mesh = meshFilter.mesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;

        Vector2[] uvs = new Vector2[vertices.Length];

        // Basic unwrap using the normals for projection direction
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            Vector3 normal = normals[i];

            Vector2 uv;

            // Determine the best projection axis based on the normal
            if (Mathf.Abs(normal.x) > Mathf.Abs(normal.y) && Mathf.Abs(normal.x) > Mathf.Abs(normal.z))
            {
                uv = new Vector2(vertex.y, vertex.z);
            }
            else if (Mathf.Abs(normal.y) > Mathf.Abs(normal.x) && Mathf.Abs(normal.y) > Mathf.Abs(normal.z))
            {
                uv = new Vector2(vertex.x, vertex.z);
            }
            else
            {
                uv = new Vector2(vertex.x, vertex.y);
            }

            uvs[i] = uv * scale;
        }

        mesh.uv = uvs;
    }
}
