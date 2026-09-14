using GeometryUtils;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;


public struct RandomVertex
{
    public Vector3 pos;
    public Vector3 normal;
    public Vector2 uv;
}

public class RandomMeshWelder
{

    RandomVertex[] vertices;
    List<RandomVertex> newVerts;
    int[] map;
    bool[] dupMap;

    private Mesh originalMesh;
    Mesh m_Mesh;
    public float MaxPositionDelta = 1e-6f;
    public float MaxAngleDelta = 1e-6f;

    public RandomMeshWelder(Mesh aMesh)
    {
        m_Mesh = aMesh;
        originalMesh = new Mesh();
        MeshUtils.BuildMesh(originalMesh, m_Mesh);
    }


    private bool Compare(RandomVertex v1, RandomVertex v2)
    {
        if ((v1.pos - v2.pos).sqrMagnitude > MaxPositionDelta) return false;
        if (Vector3.Angle(v1.normal, v2.normal) > MaxAngleDelta) return false;

        return true;
    }

    private void CreateVertexList()
    {
        var Positions = m_Mesh.vertices;
        var Normals = m_Mesh.normals;
        var UVs = m_Mesh.uv;
       

        vertices = new RandomVertex[Positions.Length];
        for (int i = 0; i < Positions.Length; i++)
        {
            var v = new RandomVertex();
            v.pos = Positions[i];
            v.normal = Normals[i];
            v.uv = UVs[i];
            vertices[i] = v;
        }
    }
    private void RemoveDuplicates()
    {
        map = new int[vertices.Length];
        dupMap = new bool[map.Length];
        newVerts = new List<RandomVertex>();
        for (int i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            bool dup = false;
            for (int i2 = 0; i2 < newVerts.Count; i2++)
            {
                if (Compare(v, newVerts[i2]))
                {
                    map[i] = i2;
                    dup = true;
                    break;
                }
            }
            if (!dup)
            {
                map[i] = newVerts.Count;
                newVerts.Add(v);
            }
            dupMap[i] = dup;
        }
    }

    private void RemapBlendshapes()
    {
        m_Mesh.ClearBlendShapes();
        var vertexCount = vertices.Length;
        var newVertexCount = newVerts.Count;
        Vector3[] deltaVertices = new Vector3[vertexCount];
        Vector3[] deltaNormals = new Vector3[vertexCount];
        Vector3[] deltaTangents = new Vector3[vertexCount];
        var blendshapeCount = originalMesh.blendShapeCount;
        for (int i = 0; i < blendshapeCount; i++)
        {
            string blendShapeName = originalMesh.GetBlendShapeName(i);
            int frameCount = originalMesh.GetBlendShapeFrameCount(i);
            for (int j = 0; j < frameCount; j++)
            {
                float frameWeight = originalMesh.GetBlendShapeFrameWeight(i, j);
                List<Vector3> newDeltaVertices = new List<Vector3>();
                List<Vector3> newDeltaNormals = new List<Vector3>();
                List<Vector3> newDeltaTangents = new List<Vector3>();
                originalMesh.GetBlendShapeFrameVertices(i, j, deltaVertices, deltaNormals, deltaTangents);
                for(int t = 0; t < vertexCount; t++)
                {
                    if (dupMap[t] == false)
                    {
                        newDeltaVertices.Add(deltaVertices[t]);
                        newDeltaNormals.Add(deltaNormals[t]);
                        newDeltaTangents.Add(deltaTangents[t]);
                    }
                }
                m_Mesh.AddBlendShapeFrame(blendShapeName, frameWeight, newDeltaVertices.ToArray(), newDeltaNormals.ToArray(), newDeltaTangents.ToArray());
            }
        }
    }


    private void AssignNewVertexArrays()
    {
        m_Mesh.vertices = newVerts.Select(v => v.pos).ToArray();
        m_Mesh.normals = newVerts.Select(v => v.normal).ToArray();
        m_Mesh.uv = newVerts.Select(v => v.uv).ToArray();
    }

    private void RemapTriangles()
    {
        for (int n = 0; n < m_Mesh.subMeshCount; n++)
        {
            var tris = m_Mesh.GetTriangles(n);
            for (int i = 0; i < tris.Length; i++)
            {
                tris[i] = map[tris[i]];
            }
            m_Mesh.SetTriangles(tris, n);
        }
    }
    public void Weld()
    {
        CreateVertexList();
        RemoveDuplicates();
        RemapTriangles();
        AssignNewVertexArrays();
        RemapBlendshapes();
        m_Mesh.RecalculateBounds();
        m_Mesh.RecalculateTangents();
    }
}