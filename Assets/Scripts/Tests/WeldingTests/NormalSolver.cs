
/*====================================================
*
* Francesco Cucchiara - 3POINT SOFT
* http://threepointsoft.altervista.org
*
=====================================================*/

/* 
 * The following code was taken from: https://schemingdeveloper.com
 *
 * Visit our game studio website: http://stopthegnomes.com
 *
 * License: You may use this code however you see fit, as long as you include this notice
 *          without any modifications.
 *
 *          You may not publish a paid asset on Unity store if its main function is based on
 *          the following code, but you may publish a paid asset that uses this code.
 *
 *          If you intend to use this in a Unity store asset or a commercial project, it would
 *          be appreciated, but not required, if you let me know with a link to the asset. If I
 *          don't get back to you just go ahead and use it anyway!
 */

using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;


public static class NormalSolver
{
    public static void UnweldVertices(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int origVertCount = mesh.vertexCount;

        // Read all 8 UV channels upfront
        var uvSrc = new List<Vector2>[8];
        for (int c = 0; c < 8; c++)
        {
            uvSrc[c] = new List<Vector2>();
            mesh.GetUVs(c, uvSrc[c]);
        }

        // Read blendshapes before vertex count changes (they're indexed by original vertex)
        int shapeCount = mesh.blendShapeCount;
        var bsNames        = new string[shapeCount];
        var bsFrameCounts  = new int[shapeCount];
        var bsFrameWeights = new float[shapeCount][];
        var bsDeltaVerts    = new List<Vector3[]>();
        var bsDeltaNormals  = new List<Vector3[]>();
        var bsDeltaTangents = new List<Vector3[]>();

        for (int s = 0; s < shapeCount; s++)
        {
            bsNames[s]       = mesh.GetBlendShapeName(s);
            bsFrameCounts[s] = mesh.GetBlendShapeFrameCount(s);
            bsFrameWeights[s] = new float[bsFrameCounts[s]];
            for (int f = 0; f < bsFrameCounts[s]; f++)
            {
                bsFrameWeights[s][f] = mesh.GetBlendShapeFrameWeight(s, f);
                var dv = new Vector3[origVertCount];
                var dn = new Vector3[origVertCount];
                var dt = new Vector3[origVertCount];
                mesh.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                bsDeltaVerts.Add(dv);
                bsDeltaNormals.Add(dn);
                bsDeltaTangents.Add(dt);
            }
        }

        List<Vector3> unweldedVerticesList = new List<Vector3>();
        int[][] unweldedSubTriangles = new int[mesh.subMeshCount][];
        var unweldedUVLists = new List<Vector2>[8];
        for (int c = 0; c < 8; c++) unweldedUVLists[c] = new List<Vector2>();
        var indexMapping = new List<int>(); // newVertexIdx → origVertexIdx
        int currVertex = 0;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            int[] triangles = mesh.GetTriangles(i);
            Vector3[] unweldedVertices = new Vector3[triangles.Length];
            int[] unweldedTriangles = new int[triangles.Length];
            var unweldedUVs = new Vector2[8][];
            for (int c = 0; c < 8; c++) unweldedUVs[c] = new Vector2[triangles.Length];

            for (int j = 0; j < triangles.Length; j++)
            {
                int srcIdx = triangles[j];
                unweldedVertices[j] = vertices[srcIdx];
                indexMapping.Add(srcIdx);
                for (int c = 0; c < 8; c++)
                {
                    if (uvSrc[c].Count > srcIdx)
                        unweldedUVs[c][j] = uvSrc[c][srcIdx];
                }
                unweldedTriangles[j] = currVertex;
                currVertex++;
            }

            unweldedVerticesList.AddRange(unweldedVertices);
            unweldedSubTriangles[i] = unweldedTriangles;
            for (int c = 0; c < 8; c++) unweldedUVLists[c].AddRange(unweldedUVs[c]);
        }

        mesh.vertices = unweldedVerticesList.ToArray();
        for (int c = 0; c < 8; c++)
        {
            if (uvSrc[c].Count > 0)
                mesh.SetUVs(c, unweldedUVLists[c]);
        }

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            mesh.SetTriangles(unweldedSubTriangles[i], i, false);
        }

        // Expand blendshape deltas from origVertCount → newVertCount using the same mapping
        if (shapeCount > 0)
        {
            int newCount = indexMapping.Count;
            mesh.ClearBlendShapes();
            int frameIdx = 0;
            for (int s = 0; s < shapeCount; s++)
            {
                for (int f = 0; f < bsFrameCounts[s]; f++, frameIdx++)
                {
                    var dv = new Vector3[newCount];
                    var dn = new Vector3[newCount];
                    var dt = new Vector3[newCount];
                    for (int i = 0; i < newCount; i++)
                    {
                        int orig = indexMapping[i];
                        dv[i] = bsDeltaVerts[frameIdx][orig];
                        dn[i] = bsDeltaNormals[frameIdx][orig];
                        dt[i] = bsDeltaTangents[frameIdx][orig];
                    }
                    mesh.AddBlendShapeFrame(bsNames[s], bsFrameWeights[s][f], dv, dn, dt);
                }
            }
        }

        RecalculateTangents(mesh);
    }

    public static void UnweldVertices2(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;

        List<Vector3> unweldedVerticesList = new List<Vector3>();
        int[][] unweldedSubTriangles = new int[mesh.subMeshCount][];
        List<Vector2> unweldedUvsList = new List<Vector2>();
        int currVertex = 0;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            int[] triangles = mesh.GetTriangles(i);
            Vector3[] unweldedVertices = new Vector3[triangles.Length];
            int[] unweldedTriangles = new int[triangles.Length];
            Vector2[] unweldedUVs = new Vector2[unweldedVertices.Length];

            for (int j = 0; j < triangles.Length; j++)
            {
                unweldedVertices[j] = vertices[triangles[j]]; //unwelded vertices are just all the vertices as they appear in the triangles array
                if (uvs.Length > triangles[j])
                {
                    unweldedUVs[j] = uvs[triangles[j]];
                }
                unweldedTriangles[j] = currVertex; //the unwelded triangle array will contain global progressive vertex indexes (1, 2, 3, ...)
                currVertex++;
            }

            unweldedVerticesList.AddRange(unweldedVertices);
            unweldedSubTriangles[i] = unweldedTriangles;
            unweldedUvsList.AddRange(unweldedUVs);
        }

        var totalTrianglesCount = mesh.triangles.Length;
        var vertexCount = vertices.Length;
        
        Vector3[] deltaVertices = new Vector3[vertexCount];
        Vector3[] deltaNormals = new Vector3[vertexCount];
        Vector3[] deltaTangents = new Vector3[vertexCount];

        /*for (int i = 0; i < blendshapeCount; i++)
        {
            string blendShapeName = mesh.GetBlendShapeName(i);
            int frameCount = mesh.GetBlendShapeFrameCount(i);
            for (int j = 0; j < frameCount; j++)
            {
                float frameWeight = mesh.GetBlendShapeFrameWeight(i, j);
                mesh.GetBlendShapeFrameVertices(i, j, deltaVertices, deltaNormals, deltaTangents);

                for (int i = 0; i < totalTrianglesCount; i++)
                {
                    bendedMesh.AddBlendShapeFrame(blendShapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
                }
            }
        }*/

        mesh.vertices = unweldedVerticesList.ToArray();
        mesh.uv = unweldedUvsList.ToArray();

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            mesh.SetTriangles(unweldedSubTriangles[i], i, false);
        }

        RecalculateTangents(mesh);
    }


    
    /// <summary>
    ///     Recalculate the normals of a mesh based on an angle threshold. This takes
    ///     into account distinct vertices that have the same position.
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="angle">
    ///     The smoothing angle. Note that triangles that already share
    ///     the same vertex will be smooth regardless of the angle! 
    /// </param>
    public static void AdvancedRecalculateNormals(this Mesh mesh, float angle, bool unweld = false)
    {
        if(unweld) UnweldVertices(mesh);

        float cosineThreshold = Mathf.Cos(angle * Mathf.Deg2Rad);

        Vector3[] vertices      = mesh.vertices;
        Vector3[] importedNormals = mesh.normals; // normals from Blender — used as-is, no cross product
        Vector3[] normals       = new Vector3[vertices.Length];

        // Group vertex indices by world position.
        var groups = new Dictionary<VertexKey, List<int>>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            var key = new VertexKey(vertices[i]);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<int>(4);
                groups.Add(key, list);
            }
            list.Add(i);
        }

        // For each vertex, average the imported normals of all co-located vertices
        // whose angle is within the smoothing threshold.
        // Using imported normals avoids sign ambiguity from cross-product winding.
        foreach (var group in groups.Values)
        {
            for (int i = 0; i < group.Count; i++)
            {
                int vi = group[i];
                Vector3 lhsN = importedNormals[vi];
                Vector3 sum  = Vector3.zero;

                for (int j = 0; j < group.Count; j++)
                {
                    int vj = group[j];
                    Vector3 rhsN = importedNormals[vj];
                    if (Vector3.Dot(lhsN, rhsN) >= cosineThreshold)
                        sum += rhsN;
                }

                normals[vi] = sum.normalized;
            }
        }

        mesh.normals = normals;
    }

    private struct VertexKey
    {
        private readonly long _x;
        private readonly long _y;
        private readonly long _z;

        // Change this if you require a different precision.
        private const int Tolerance = 100000;

        // Magic FNV values. Do not change these.
        private const long FNV32Init = 0x811c9dc5;
        private const long FNV32Prime = 0x01000193;

        public VertexKey(Vector3 position)
        {
            _x = (long) (Mathf.Round(position.x * Tolerance));
            _y = (long) (Mathf.Round(position.y * Tolerance));
            _z = (long) (Mathf.Round(position.z * Tolerance));
        }

        public override bool Equals(object obj)
        {
            VertexKey key = (VertexKey) obj;
            return _x == key._x && _y == key._y && _z == key._z;
        }

        public override int GetHashCode()
        {
            long rv = FNV32Init;
            rv ^= _x;
            rv *= FNV32Prime;
            rv ^= _y;
            rv *= FNV32Prime;
            rv ^= _z;
            rv *= FNV32Prime;

            return rv.GetHashCode();
        }
    }

    private struct VertexEntry
    {
        public int MeshIndex;
        public int TriangleIndex;
        public int VertexIndex;

        public VertexEntry(int meshIndex, int triIndex, int vertIndex)
        {
            MeshIndex = meshIndex;
            TriangleIndex = triIndex;
            VertexIndex = vertIndex;
        }
    }

        
    /// <summary>
    /// Recalculates mesh tangents
    /// 
    /// For some reason the built-in RecalculateTangents function produces artifacts on dense geometries.
    /// 
    /// This implementation id derived from:
    /// 
    /// Lengyel, Eric. Computing Tangent Space Basis Vectors for an Arbitrary Mesh.
    /// Terathon Software 3D Graphics Library, 2001.
    /// http://www.terathon.com/code/tangent.html
    /// </summary>
    /// <param name="mesh"></param>
    public static void RecalculateTangents(Mesh mesh)
    {
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        Vector3[] normals = mesh.normals;
            
        int triangleCount = triangles.Length;
        int vertexCount = vertices.Length;

        Vector3[] tan1 = new Vector3[vertexCount];
        Vector3[] tan2 = new Vector3[vertexCount];

        Vector4[] tangents = new Vector4[vertexCount];

        for (int a = 0; a < triangleCount; a += 3)
        {
            int i1 = triangles[a + 0];
            int i2 = triangles[a + 1];
            int i3 = triangles[a + 2];

            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            Vector3 v3 = vertices[i3];

            Vector2 w1 = uv[i1];
            Vector2 w2 = uv[i2];
            Vector2 w3 = uv[i3];

            float x1 = v2.x - v1.x;
            float x2 = v3.x - v1.x;
            float y1 = v2.y - v1.y;
            float y2 = v3.y - v1.y;
            float z1 = v2.z - v1.z;
            float z2 = v3.z - v1.z;

            float s1 = w2.x - w1.x;
            float s2 = w3.x - w1.x;
            float t1 = w2.y - w1.y;
            float t2 = w3.y - w1.y;
                
            float div = s1 * t2 - s2 * t1;
            float r = div == 0.0f ? 0.0f : 1.0f / div;

            Vector3 sDir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
            Vector3 tDir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

            tan1[i1] += sDir;
            tan1[i2] += sDir;
            tan1[i3] += sDir;

            tan2[i1] += tDir;
            tan2[i2] += tDir;
            tan2[i3] += tDir;
        }
            
        for (int a = 0; a < vertexCount; ++a)
        {
            Vector3 n = normals[a];
            Vector3 t = tan1[a];
                
            Vector3.OrthoNormalize(ref n, ref t);
            tangents[a].x = t.x;
            tangents[a].y = t.y;
            tangents[a].z = t.z;

            tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
        }

        mesh.tangents = tangents;
    }

    public static void RecalculateTangentsParallel(Mesh mesh)
    {

        int triangleCount = mesh.triangles.Length;
        int vertexCount = mesh.vertices.Length;

        NativeArray<int> trianglesNA = new NativeArray<int>(triangleCount, Allocator.TempJob);
        NativeArray<Vector3> verticesNA = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
        NativeArray<Vector3> normalsNA = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
        NativeArray<Vector2> uvNA = new NativeArray<Vector2>(vertexCount, Allocator.TempJob);

        trianglesNA.CopyFrom(mesh.triangles);
        verticesNA.CopyFrom(mesh.vertices);
        normalsNA.CopyFrom(mesh.normals);
        uvNA.CopyFrom(mesh.uv);

        NativeArray<Vector3> tan1NA = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
        NativeArray<Vector3> tan2NA = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
        NativeArray<Vector4> tangentsNA = new NativeArray<Vector4>(vertexCount, Allocator.TempJob);


        GenerateNewTangentsDataJob generateNewTangentsDataJob = new GenerateNewTangentsDataJob
        {
            triangles = trianglesNA,
            vertices = verticesNA,
            normals = normalsNA,
            uv= uvNA,
            tan1 = tan1NA,
            tan2 = tan2NA,
        };

        JobHandle generateNewTangentsDataJobHandle = generateNewTangentsDataJob.Schedule(triangleCount / 3, 64);
        generateNewTangentsDataJobHandle.Complete();

        UpdateTangentsJob updateTangentsJob = new UpdateTangentsJob
        {
            tan1 = tan1NA,
            tan2 = tan2NA,
            normals = normalsNA,
            tangents = tangentsNA,
        };

        JobHandle updateTangentsJobHandle = updateTangentsJob.Schedule(vertexCount, 64);
        updateTangentsJobHandle.Complete();

        Vector4[] tangents = new Vector4[vertexCount];
        tangentsNA.CopyTo(tangents);

        mesh.tangents = tangents;

        trianglesNA.Dispose();
        verticesNA.Dispose();
        normalsNA.Dispose();
        uvNA.Dispose();
        tan1NA.Dispose();
        tan2NA.Dispose();
        tangentsNA.Dispose();

    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct GenerateNewTangentsDataJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> triangles;
        [ReadOnly] public NativeArray<Vector3> vertices;
        [ReadOnly] public NativeArray<Vector3> normals; 
        [ReadOnly] public NativeArray<Vector2> uv;
        [NativeDisableParallelForRestriction] public NativeArray<Vector3> tan1;
        [NativeDisableParallelForRestriction] public NativeArray<Vector3> tan2;

        public void Execute(int index)
        {
            var idx = index * 3;
            int i1 = triangles[idx + 0];
            int i2 = triangles[idx + 1];
            int i3 = triangles[idx + 2];

            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            Vector3 v3 = vertices[i3];

            Vector2 w1 = uv[i1];
            Vector2 w2 = uv[i2];
            Vector2 w3 = uv[i3];

            float x1 = v2.x - v1.x;
            float x2 = v3.x - v1.x;
            float y1 = v2.y - v1.y;
            float y2 = v3.y - v1.y;
            float z1 = v2.z - v1.z;
            float z2 = v3.z - v1.z;

            float s1 = w2.x - w1.x;
            float s2 = w3.x - w1.x;
            float t1 = w2.y - w1.y;
            float t2 = w3.y - w1.y;

            float div = s1 * t2 - s2 * t1;
            float r = div == 0.0f ? 0.0f : 1.0f / div;

            Vector3 sDir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
            Vector3 tDir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

            tan1[i1] += sDir;
            tan1[i2] += sDir;
            tan1[i3] += sDir;

            tan2[i1] += tDir;
            tan2[i2] += tDir;
            tan2[i3] += tDir;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct UpdateTangentsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> tan1;
        [ReadOnly] public NativeArray<Vector3> tan2;
        [ReadOnly] public NativeArray<Vector3> normals;
        [NativeDisableParallelForRestriction] public NativeArray<Vector4> tangents;
        public void Execute(int index)
        {
            Vector3 n = normals[index];
            Vector3 t = tan1[index];

            Vector3.OrthoNormalize(ref n, ref t);
            var tangent = new Vector4(t.x, t.y, t.z, (Vector3.Dot(Vector3.Cross(n, t), tan2[index]) < 0.0f) ? -1.0f : 1.0f);
            tangents[index] = tangent; 
        }
    }
}