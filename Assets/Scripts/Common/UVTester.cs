using GeometryUtils;
using LargeIntestine;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

public class UVTester : MonoBehaviour
{
    public int uvIndex = 0;
    public float uvScaleFactor = -1670.0f;
    public Vector2 newUvValue = Vector2.zero;
    public int[] triangleIdxs;

    private SkinnedMeshRenderer smr;
    private Mesh mesh;
    private Vector2[] uvs;
    private Texture texture;
    private NativeArray<Vector3> vertices;
    private NativeArray<Vector2> originalUvs;
    protected void SetRenderer()
    {
        smr = GetComponent<SkinnedMeshRenderer>();
        mesh = smr.sharedMesh;
        uvs = mesh.uv;
        //texture = smr.sharedMaterials[0].mainTexture;
        vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Persistent);
        vertices.CopyFrom(mesh.vertices);
        originalUvs = new NativeArray<Vector2>(mesh.vertexCount, Allocator.Persistent);
        originalUvs.CopyFrom(uvs);

    }

    protected void DebugUvAtIndex()
    {
        Debug.Log(uvs[uvIndex]);
    }

    protected void DebugRecalculateUvs()
    {
        Debug.LogWarning(new Vector2(mesh.vertices[uvIndex].y / texture.height * uvScaleFactor, mesh.vertices[uvIndex].x / texture.width * uvScaleFactor));
    }

    protected void RecalculateAndReplaceUvs()
    {
        NativeArray<Vector2> newUvs = new NativeArray<Vector2>(mesh.vertexCount, Allocator.TempJob);

        /*var tempUvs = new Vector2[mesh.vertexCount];
        for (int i = 0; i < mesh.vertices.Length; i++)
        {
            tempUvs[i] = new Vector2(mesh.vertices[uvIndex].y / texture.height * uvScaleFactor, mesh.vertices[uvIndex].x / texture.width * uvScaleFactor);
        }*/

        CalculateUvsJob calculateUvsJob = new CalculateUvsJob
        {
            origUvs = originalUvs,
            newUvs = newUvs,
            vertices = vertices,
            uvScaler = uvScaleFactor,
            tH = texture.height,
            tW = texture.width,
        };

        JobHandle calculateUvsJobHandle = calculateUvsJob.Schedule(mesh.vertexCount, 1000);
        calculateUvsJobHandle.Complete();

        var tempUvs = new Vector2[mesh.vertexCount];
        newUvs.CopyTo(tempUvs);
        mesh.uv = tempUvs;
        smr.sharedMesh = mesh;

        newUvs.Dispose();
    }

    protected void AsumeUv()
    {
        var f = mesh.vertices[uvIndex];
        var p1 = mesh.vertices[triangleIdxs[0]];
        var p2 = mesh.vertices[triangleIdxs[1]];
        var p3 = mesh.vertices[triangleIdxs[2]];
        MeshVerticesUtils.DrawVerticesAsSpheres(new Vector3[] { p1, p2, p3 }, 0.1f);
        var uv1 = uvs[triangleIdxs[0]];
        var uv2 = uvs[triangleIdxs[1]];
        var uv3 = uvs[triangleIdxs[2]];
        var f1 = p1 - f;
        var f2 = p2 - f;
        var f3 = p3 - f;

        var va = Vector3.Cross(p1 - p2, p1 - p3);
        var va1 = Vector3.Cross(f2, f3);
        var va2 = Vector3.Cross(f3, f1);
        var va3 = Vector3.Cross(f1, f2);

        var a = va.magnitude;
        var a1 = va1.magnitude / a * Mathf.Sign(Vector3.Dot(va, va1));
        var a2 = va2.magnitude / a * Mathf.Sign(Vector3.Dot(va, va2));
        var a3 = va3.magnitude / a * Mathf.Sign(Vector3.Dot(va, va3));
        var result = uv1 * a1 + uv2 * a2 + uv3 * a3;
        //result = new Vector3(Mathf.Abs(result.x), Mathf.Abs(result.x));
        //var algo = uv1.Scale(a1).addInPlace(uv2.scale(a2)).addInPlace(uv3.scale(a3));
        Debug.Log(result);
    }

    protected void ReplaceUvs()
    {
        uvs[uvIndex] = newUvValue;
        smr.sharedMesh.uv = uvs;
        SetRenderer();
    }

    [BurstCompile]
    private struct CalculateUvsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector2> origUvs;
        [NativeDisableParallelForRestriction] public NativeArray<Vector2> newUvs;
        [ReadOnly] public NativeArray<Vector3> vertices;
        public float uvScaler;
        public int tH;
        public int tW;

        public void Execute(int index)
        {
            newUvs[index] = new Vector2(vertices[index].y / tH * uvScaler, vertices[index].x / tW * uvScaler);
        }
    }

    public void SecondaryUV()
    {
        /*Unwrapping.GenerateSecondaryUVSet(mesh);
        mesh.uv = mesh.uv2;
        mesh.uv2 = new Vector2[0];*/
    }
    public void PerTriangleUV()
    {

        /*var newUvs = Unwrapping.GeneratePerTriangleUV(mesh);
        mesh.uv = newUvs;*/
    }

    private void OnDestroy()
    {
        if (vertices != null) vertices.Dispose();
        if (originalUvs != null) originalUvs.Dispose();
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(UVTester))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            UVTester myScript = (UVTester)target;
            if (GUILayout.Button("SetRenderer"))
            {
                myScript.SetRenderer();
            }
            else if (GUILayout.Button("DebugUvAtIndex"))
            {
                myScript.DebugUvAtIndex();
            }
            else if (GUILayout.Button("DebugRecalculateUvs"))
            {
                myScript.DebugRecalculateUvs();
            }
            else if (GUILayout.Button("RecalculateAndReplaceUvs"))
            {
                myScript.RecalculateAndReplaceUvs();
            }
            else if (GUILayout.Button("ReplaceUvs"))
            {
                myScript.ReplaceUvs();
            }
            else if (GUILayout.Button("AsumeUv"))
            {
                myScript.AsumeUv();
            }
            else if (GUILayout.Button("SecondaryUV"))
            {
                myScript.SecondaryUV();
            }
            else if (GUILayout.Button("PerTriangleUV"))
            {
                myScript.PerTriangleUV();
            }
        }
    }
#endif
}
