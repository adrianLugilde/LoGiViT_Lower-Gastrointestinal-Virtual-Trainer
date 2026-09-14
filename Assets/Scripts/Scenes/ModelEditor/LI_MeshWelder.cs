using JobsUtils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using GeometryUtils;
using System.Linq;
using System.Collections.Generic;
using Vertex = GeometryUtils.Vertex;
using System;

public class LI_MeshWelder
{
    Mesh weldedMesh;
    List<Mesh> meshesToWeld = new List<Mesh>();
    List<Vector3> combinedVertices = new List<Vector3>();
    List<Vector3> combinedNormals = new List<Vector3>();
    List<Vector4> combinedTangents = new List<Vector4>();
    List<Vector2> combinedUvs = new List<Vector2>();
    List<int> combinedTriangles = new List<int>();
    List<int> meshZeroIdxs = new List<int>();
    List<int> defaultMeshIdxs = new List<int>();
    List<Vector3> duplicatedVertices = new List<Vector3>();
    int vertexCount = 0;
    int vertexOffset = 0;

    //Mesh has 1656 vertex, after removing duplicates is 1593 except for the first one

    public void Weld(SkinnedMeshRenderer skinnedMeshRenderer, SkinnedMeshRenderer[] renderersToCombine, float maxPosDelta, float maxAngleDelta, bool bakeRenderers = false, bool showWeldedVertices = false, bool recalculateNormals = false)
    {
        float startTime = Time.realtimeSinceStartup;
        meshesToWeld.Clear();
        combinedVertices.Clear();
        combinedNormals.Clear();
        combinedTangents.Clear();
        combinedUvs.Clear();
        combinedTriangles.Clear();
        meshZeroIdxs.Clear();
        defaultMeshIdxs.Clear();
        duplicatedVertices.Clear();
        vertexCount = 0;
        vertexOffset = 0 ;
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds for clears");
         

        for (int i = 0; i < renderersToCombine.Length; i++)
        {
            var tempMesh = new Mesh();
            renderersToCombine[i].BakeMesh(tempMesh);
            meshesToWeld.Add(tempMesh);
        }
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds for bake meshes");

        NativeArray<float4> points = new NativeArray<float4>(meshesToWeld[1].vertices.Length, Allocator.TempJob);
        NativeArray<float4> prevPoints = new NativeArray<float4>(meshesToWeld[0].vertices.Length, Allocator.TempJob);
        NativeArray<bool> isDuplicatedPoints = new NativeArray<bool>(meshesToWeld[1].vertices.Length, Allocator.TempJob);
        NativeArray<float4> prevPointOfDuplicates = new NativeArray<float4>(meshesToWeld[1].vertices.Length, Allocator.TempJob);
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to declare array points");

        points.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToWeld[1].vertices));
        prevPoints.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToWeld[0].vertices));
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds for populate points");

        FindDuplicatedPositions findDuplicatedPoints = new FindDuplicatedPositions
        {
            points = points,
            prevPoints = prevPoints,
            maxPosDiff = maxPosDelta,
            isDuplicatedPoints = isDuplicatedPoints,
            prevPointOfDuplicates = prevPointOfDuplicates
        };

        JobHandle findDuplicatedPointsJob = findDuplicatedPoints.Schedule(meshesToWeld[1].vertices.Length, 1);
        findDuplicatedPointsJob.Complete();
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to find duplicated points");

        for (int l = 0; l < meshesToWeld[0].vertexCount; l++)
        {
            meshZeroIdxs.Add(l);
            vertexCount++;
        }

        for (int z = 0; z < isDuplicatedPoints.Length; z++)
        {
            if (isDuplicatedPoints[z])
            {
                defaultMeshIdxs.Add(prevPoints.IndexOf(prevPointOfDuplicates[z]));
                vertexCount++;
            }
            else
            {
                defaultMeshIdxs.Add(vertexCount++);
            }
        }
        for (int i = 1; i < meshesToWeld.Count; i++)
        {
            for (int z = 0; z < isDuplicatedPoints.Length; z++)
            {
                if (isDuplicatedPoints[z])
                {
                    duplicatedVertices.Add(meshesToWeld[i-1].vertices[z]);
                }
            }
        }

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to create duplicated points template");

        NativeArray<int> triangles = new NativeArray<int>(meshesToWeld[0].triangles.Length, Allocator.Persistent);
        NativeArray<int> indexes = new NativeArray<int>(meshesToWeld[0].vertexCount, Allocator.Persistent);

        
        for (int i = 0; i < meshesToWeld.Count; i++)
        {
            combinedVertices.AddRange(meshesToWeld[i].vertices);
            combinedNormals.AddRange(meshesToWeld[i].normals);
            combinedUvs.AddRange(meshesToWeld[i].uv);
            combinedTangents.AddRange(meshesToWeld[i].tangents);

            triangles.CopyFrom(meshesToWeld[i].triangles);

            if (i != 0)
            {
                indexes.CopyFrom(defaultMeshIdxs.ToArray());
            }
            else
            {
                indexes.CopyFrom(meshZeroIdxs.ToArray());
            }

            UpdateMeshTrianglesJob updateMeshTrianglesJob = new UpdateMeshTrianglesJob
            {
                triangles = triangles,
                indexes = indexes,
                indexesOffset = vertexOffset
            };

            JobHandle updateMeshTrianglesJobHandle = updateMeshTrianglesJob.Schedule(triangles.Length, 1024);
            updateMeshTrianglesJobHandle.Complete();

            combinedTriangles.AddRange(triangles);

            if (i != 0) vertexOffset += meshesToWeld[i].vertexCount;
        }
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to first remap of triangles");

        weldedMesh = new Mesh();
        weldedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        weldedMesh.vertices = combinedVertices.ToArray();
        weldedMesh.normals = combinedNormals.ToArray();
        weldedMesh.tangents = combinedTangents.ToArray();
        weldedMesh.uv = combinedUvs.ToArray();
        weldedMesh.triangles = combinedTriangles.ToArray();

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to create combined mesh");

        if(recalculateNormals) NormalSolver.AdvancedRecalculateNormals(weldedMesh, maxAngleDelta, true);
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to recalculate normals ");

        var vertexArray = CreateVertexArray(weldedMesh);
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to create the vertex array");
        NativeArray<Vertex> vertexNA = new NativeArray<Vertex>(vertexArray.Length, Allocator.TempJob);
        NativeArray<int> newIndexes = new NativeArray<int>(vertexArray.Length, Allocator.TempJob);
        NativeList<Vertex> newVertices = new NativeList<Vertex>(vertexArray.Length, Allocator.TempJob);
        vertexNA.CopyFrom(vertexArray);
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to populate vertices");

        GetDuplicatedVertices getDuplicatedVerticesJob = new GetDuplicatedVertices
        {
            vertices = vertexNA,
            indices = newIndexes,
            newVertices = newVertices,
            maxPosDiff = 1e-6f,
            maxAngleDiff = 1e-6f,
            vertexOffset = meshesToWeld[0].triangles.Length,
            modelsCount = meshesToWeld.Count,
        };

        JobHandle getDuplicateVerticesJobHandle = getDuplicatedVerticesJob.Schedule();
        getDuplicateVerticesJobHandle.Complete();

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to get duplicated vertices");

        var newIndicesArray = new int[newIndexes.Length];
        newIndexes.CopyTo(newIndicesArray);
        RemapTriangles(weldedMesh, newIndicesArray);
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to second triangles remap");

        var newVerticesList = new List<Vertex>();
        for (int i = 0; i < newVertices.Length; i++)
        {
            newVerticesList.Add(newVertices[i]);
        }

        weldedMesh.vertices = newVerticesList.Select(v => v.position).ToArray();
        weldedMesh.normals = newVerticesList.Select(v => v.normal).ToArray();
        weldedMesh.uv = newVerticesList.Select(v => v.uv).ToArray();

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to mesh data replacement");

        if (showWeldedVertices) MeshVerticesUtils.DrawVerticesAsSpheres(duplicatedVertices, 0.01f, nameof(duplicatedVertices));
       

        skinnedMeshRenderer.sharedMesh = weldedMesh;
        skinnedMeshRenderer.sharedMesh.name = "Generated by " + GetType().Name;
        skinnedMeshRenderer.sharedMaterials = renderersToCombine[0].sharedMaterials;
        skinnedMeshRenderer.sharedMesh.RecalculateBounds();
        skinnedMeshRenderer.localBounds = skinnedMeshRenderer.sharedMesh.bounds;

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " smr data replacement");


        points.Dispose();
        prevPoints.Dispose();
        isDuplicatedPoints.Dispose();
        prevPointOfDuplicates.Dispose();
        triangles.Dispose();
        indexes.Dispose();
        vertexNA.Dispose();
        newIndexes.Dispose();
        newVertices.Dispose();

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to container disposal");
    }

    public List<SubmodelData> ParallelWeld(SkinnedMeshRenderer skinnedMeshRenderer, SkinnedMeshRenderer[] renderersToCombine, float maxPosDelta, float maxAngleDelta, bool bakeRenderers = false, bool showWeldedVertices = false)
    {
        float startTime = Time.realtimeSinceStartup;
        meshesToWeld.Clear();
        combinedVertices.Clear();
        combinedNormals.Clear();
        combinedTangents.Clear();
        combinedUvs.Clear();
        combinedTriangles.Clear();
        meshZeroIdxs.Clear();
        defaultMeshIdxs.Clear();
        duplicatedVertices.Clear();
        vertexCount = 0;
        vertexOffset = 0;
        List<SubmodelData> submodels = new List<SubmodelData>();

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds for clears");

        var tempMesh = new Mesh();
        for (int i = 0; i < renderersToCombine.Length; i++)
        {
            tempMesh.Clear();
            renderersToCombine[i].BakeMesh(tempMesh);
            meshesToWeld.Add(tempMesh);
        }
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds for bake meshes");

        NativeArray<float4> points = new NativeArray<float4>(meshesToWeld[1].vertices.Length, Allocator.TempJob);
        NativeArray<float4> prevPoints = new NativeArray<float4>(meshesToWeld[0].vertices.Length, Allocator.TempJob);
        NativeArray<bool> isDuplicatedPoints = new NativeArray<bool>(meshesToWeld[1].vertices.Length, Allocator.TempJob);
        NativeArray<float4> prevPointOfDuplicates = new NativeArray<float4>(meshesToWeld[1].vertices.Length, Allocator.TempJob);
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to declare array points");

        points.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToWeld[1].vertices));
        prevPoints.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToWeld[0].vertices));
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds for populate points");

        FindDuplicatedPositions findDuplicatedPoints = new FindDuplicatedPositions
        {
            points = points,
            prevPoints = prevPoints,
            maxPosDiff = maxPosDelta,
            isDuplicatedPoints = isDuplicatedPoints,
            prevPointOfDuplicates = prevPointOfDuplicates
        };

        JobHandle findDuplicatedPointsJob = findDuplicatedPoints.Schedule(meshesToWeld[1].vertices.Length, 1);
        findDuplicatedPointsJob.Complete();
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to find duplicated points");

        for (int l = 0; l < meshesToWeld[0].vertexCount; l++)
        {
            meshZeroIdxs.Add(l);
            vertexCount++;
        }

        for (int z = 0; z < isDuplicatedPoints.Length; z++)
        {
            if (isDuplicatedPoints[z])
            {
                duplicatedVertices.Add(JobsTypesConversions.Float4ToVector3(prevPointOfDuplicates[z]));
                defaultMeshIdxs.Add(prevPoints.IndexOf(prevPointOfDuplicates[z]));
                vertexCount++;
            }
            else
            {
                defaultMeshIdxs.Add(vertexCount++);
            }
        }
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to create duplicated points template");

        NativeArray<int> triangles = new NativeArray<int>(meshesToWeld[0].triangles.Length, Allocator.Persistent);
        NativeArray<int> indexes = new NativeArray<int>(meshesToWeld[0].vertexCount, Allocator.Persistent);



        for (int i = 0; i < meshesToWeld.Count; i++)
        {
            var submodel = new SubmodelData();
            submodel.Init(meshesToWeld[i]);
     
            triangles.CopyFrom(meshesToWeld[i].triangles);

            if (i != 0)
            {
                indexes.CopyFrom(defaultMeshIdxs.ToArray());
            }
            else
            {
                indexes.CopyFrom(meshZeroIdxs.ToArray());
            }

            UpdateMeshTrianglesJob updateMeshTrianglesJob = new UpdateMeshTrianglesJob
            {
                triangles = triangles,
                indexes = indexes,
                indexesOffset = vertexOffset
            };

            JobHandle updateMeshTrianglesJobHandle = updateMeshTrianglesJob.Schedule(triangles.Length, 1024);
            updateMeshTrianglesJobHandle.Complete();

            //combinedTriangles.AddRange(triangles);
            submodel.triangles.AddRange(triangles);

            if (i != 0) vertexOffset += meshesToWeld[i].vertexCount;
            submodels.Add(submodel);
        }
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to first remap of triangles");

        var newSubmodels = new List<SubmodelData>();

        for(int i = 0; i < submodels.Count; i++)
        {
            if(i + 1 < submodels.Count)
            {
                var submodel = CombineSubmodelsData(submodels[i], submodels[i + 1]);
                newSubmodels.Add(submodel);
            }
        }
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to create paired submodels");

        var pairedMeshes = new List<Mesh>();

        for (int i = 0; i < newSubmodels.Count; i++)
        {
            tempMesh.Clear();
            tempMesh.vertices = newSubmodels[i].vertices.ToArray();
            tempMesh.normals = newSubmodels[i].normals.ToArray();
            tempMesh.uv = newSubmodels[i].uvs.ToArray();
            tempMesh.tangents = newSubmodels[i].tangents.ToArray();
            tempMesh.triangles = newSubmodels[0].triangles.ToArray();
            NormalSolver.AdvancedRecalculateNormals(tempMesh, maxAngleDelta, true);
            pairedMeshes.Add(tempMesh);
        }
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to create paired meshes");


        //NormalSolver.AdvancedRecalculateNormals(weldedMesh, maxAngleDelta, true);
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to recalculate normals ");

        NativeParallelMultiHashMap<int, Vertex> verticesMap = new NativeParallelMultiHashMap<int, Vertex>(pairedMeshes[0].vertexCount * pairedMeshes.Count, Allocator.TempJob);
        NativeParallelMultiHashMap<int, int> newIndexesMap = new NativeParallelMultiHashMap<int, int>(pairedMeshes[0].triangles.Length * pairedMeshes.Count, Allocator.TempJob);
        NativeParallelMultiHashMap<int, Vertex> newVerticesMap = new NativeParallelMultiHashMap<int, Vertex>(pairedMeshes[0].vertexCount * pairedMeshes.Count, Allocator.TempJob);
        for (int i = 0; i < pairedMeshes.Count; i++)
        {
            var vertexArray = CreateVertexArray(pairedMeshes[i]);
            for(int j = 0; j < vertexArray.Length; j++)
            {
                verticesMap.Add(i, vertexArray[j]);
            }
        }
        
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to create the vertex array");
        
        GetDuplicatedVerticesParallel getDuplicatedVerticesParallelJob = new GetDuplicatedVerticesParallel
        {
            verticesMap = verticesMap,
            newIndexesMap = newIndexesMap.AsParallelWriter(),
            newVerticesMap = newVerticesMap.AsParallelWriter(),
            maxPosDiff = 1e-6f,
            maxAngleDiff = 1e-6f,
            vertexOffset = meshesToWeld[0].triangles.Length,
        };

        JobHandle getDuplicatedVerticesParallelJobHandle = getDuplicatedVerticesParallelJob.Schedule(pairedMeshes.Count, 1);
        getDuplicatedVerticesParallelJobHandle.Complete();

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to get duplicated vertices");

        for(int i = 0; i < pairedMeshes.Count; i++)
        {
            var verticesIterator = newVerticesMap.GetValuesForKey(i);
            while (verticesIterator.MoveNext())
            {

            }
        }

        /*var newIndicesArray = new int[newIndexes.Length];
        newIndexes.CopyTo(newIndicesArray);
        RemapTriangles(weldedMesh, newIndicesArray);
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to second triangles remap");

        var newVerticesList = new List<Vertex>();
        for (int i = 0; i < newVertices.Length; i++)
        {
            newVerticesList.Add(newVertices[i]);
        }

        weldedMesh.vertices = newVerticesList.Select(v => v.position).ToArray();
        weldedMesh.normals = newVerticesList.Select(v => v.normal).ToArray();
        weldedMesh.uv = newVerticesList.Select(v => v.uv).ToArray();*/

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to mesh data replacement");
        

        if (showWeldedVertices) MeshVerticesUtils.DrawVerticesAsSpheres(duplicatedVertices, 0.001f, nameof(duplicatedVertices));



        weldedMesh = new Mesh();
        weldedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        weldedMesh.vertices = combinedVertices.ToArray();
        weldedMesh.normals = combinedNormals.ToArray();
        weldedMesh.tangents = combinedTangents.ToArray();
        weldedMesh.uv = combinedUvs.ToArray();
        weldedMesh.triangles = combinedTriangles.ToArray();

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to create combined mesh");

        skinnedMeshRenderer.sharedMesh = weldedMesh;
        skinnedMeshRenderer.sharedMesh.name = "Generated by " + GetType().Name;
        skinnedMeshRenderer.sharedMaterials = renderersToCombine[0].sharedMaterials;
        skinnedMeshRenderer.sharedMesh.RecalculateBounds();
        skinnedMeshRenderer.localBounds = skinnedMeshRenderer.sharedMesh.bounds;

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " smr data replacement");


        points.Dispose();
        prevPoints.Dispose();
        isDuplicatedPoints.Dispose();
        prevPointOfDuplicates.Dispose();
        triangles.Dispose();
        indexes.Dispose();
        verticesMap.Dispose();
        newIndexesMap.Dispose();
        newVerticesMap.Dispose();

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds to container disposal");
        return newSubmodels;
    }

    private Vertex[] CreateVertexArray(Mesh mesh)
    {
        var Positions = mesh.vertices;
        var Normals = mesh.normals;
        var UVs = mesh.uv;

        var vertices = new Vertex[Positions.Length];
        for (int i = 0; i < Positions.Length; i++)
        {
            var v = new Vertex();
            v.position = Positions[i];
            v.normal = Normals[i];
            v.uv = UVs[i];
            v.index = i;
            vertices[i] = v;
        }
        return vertices;
    }

    private void RemapTriangles(Mesh mesh, int[] indices)
    {
        for (int n = 0; n < mesh.subMeshCount; n++)
        {
            var tris = mesh.GetTriangles(n);
            for (int i = 0; i < tris.Length; i++)
            {
                tris[i] = indices[tris[i]];
            }
            mesh.SetTriangles(tris, n);
        }
    }

    [BurstCompile]
    public struct FindDuplicatedPositions : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> points;
        [ReadOnly] public NativeArray<float4> prevPoints;
        [ReadOnly] public float maxPosDiff;
        [NativeDisableParallelForRestriction] public NativeArray<bool> isDuplicatedPoints;
        [NativeDisableParallelForRestriction] public NativeArray<float4> prevPointOfDuplicates;
        public int vertexIdxOffset;
        public int vertexCount;

        public void Execute(int index)
        {
            bool isDuplicated = false;
            for (int i = 0; i < prevPoints.Length; i++)
            {
                if (isDuplicated) break;
                var prevPosition = prevPoints[i];
                var posDiff = (points[index] - prevPoints[i]);
                var sqrPosDiff = math.sqrt((math.pow(posDiff.x, 2) + math.pow(posDiff.y, 2) + math.pow(posDiff.z, 2)));
                if (sqrPosDiff <= maxPosDiff)
                {
                    isDuplicatedPoints[index] = isDuplicated = true;
                    prevPointOfDuplicates[index] = prevPosition;
                }
                else
                {
                    isDuplicatedPoints[index] = false;
                    prevPointOfDuplicates[index] = points[index];
                }
            }
        }
    }

    [BurstCompile]
    public struct UpdateMeshTrianglesJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<int> triangles;
        [ReadOnly] public NativeArray<int> indexes;
        [ReadOnly] public int indexesOffset;

        public void Execute(int index)
        {
            triangles[index] = indexes[triangles[index]] + indexesOffset;
        }
    }

    [BurstCompile]
    public struct GetDuplicatedVertices : IJob
    {
        [ReadOnly] public NativeArray<Vertex> vertices;
        public NativeArray<int> indices;
        public NativeList<Vertex> newVertices;
        [ReadOnly] public float maxPosDiff;
        [ReadOnly] public float maxAngleDiff;
        [ReadOnly] public int vertexOffset;
        [ReadOnly] public int modelsCount; 
 
        public void Execute()
        {
            int vertexCounter = 0;
            int modelsCounter = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                //if(vertexCounter == 8640)
                if(vertexCounter == 8424)
                {
                    modelsCounter++;
                    vertexCounter = 0;
                }
                //var startingIdx = modelsCounter * 1656;
                var startingIdx = modelsCounter * 1953;

                bool duplicate = false;
                for (int j = startingIdx; j < newVertices.Length; j++)
                {
                    if (Compare(vertices[i], newVertices[j]))
                    {
                        indices[i] = j;
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    indices[i] = newVertices.Length;
                    newVertices.Add(vertices[i]);
                }
                vertexCounter++;
            } 
        }

        public bool Compare(Vertex v1, Vertex v2)
        {
            if ((v1.position - v2.position).sqrMagnitude > maxPosDiff) return false;
            if (Vector3.Angle(v1.normal, v2.normal) > maxAngleDiff) return false;

            return true;
        }
    }

    [BurstCompile]
    public struct GetDuplicatedVerticesParallel : IJobParallelFor
    {
        [ReadOnly] public NativeParallelMultiHashMap<int, Vertex> verticesMap;
        public NativeParallelMultiHashMap<int, int>.ParallelWriter newIndexesMap;
        public NativeParallelMultiHashMap<int, Vertex>.ParallelWriter newVerticesMap;
        [ReadOnly] public float maxPosDiff;
        [ReadOnly] public float maxAngleDiff;
        [ReadOnly] public int vertexOffset;

        public void Execute(int index)
        {
            var verticesIterator = verticesMap.GetValuesForKey(index);
            NativeList<Vertex> vertices = new NativeList<Vertex>(vertexOffset, Allocator.Temp);
            NativeList<Vertex> newVertices = new NativeList<Vertex>(vertexOffset, Allocator.Temp);
            NativeList<int> indices = new NativeList<int>(vertexOffset, Allocator.Temp);
            while(verticesIterator.MoveNext())
            { 
                vertices.Add(verticesIterator.Current);
            } 

            for (int i = 0; i < vertices.Length; i++)
            { 

                bool duplicate = false;
                for (int j = 0; j < newVertices.Length; j++)
                {
                    if (Compare(vertices[i], newVertices[j]))
                    {
                        indices.Add(j);
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    indices.Add(newVertices.Length);
                    newVertices.Add(vertices[i]);
                } 
            }
            for(int i = 0; i < newVertices.Length; i++)
            {
                newVerticesMap.Add(index, newVertices[i]);
            }
            for (int i = 0; i < indices.Length; i++)
            {
                newIndexesMap.Add(index, indices[i]);
            }

            vertices.Dispose();
            newVertices.Dispose();
            indices.Dispose();
        }

        public bool Compare(Vertex v1, Vertex v2)
        {
            if ((v1.position - v2.position).sqrMagnitude > maxPosDiff) return false;
            if (Vector3.Angle(v1.normal, v2.normal) > maxAngleDiff) return false;

            return true;
        }
    }

    public void WelderTest(SkinnedMeshRenderer skinnedMeshRenderer, SkinnedMeshRenderer[] renderersToCombine, float maxPosDiff, WelderTest welderTest, bool bakeRenderers = false, bool showWeldedVertices = false)
    {
        welderTest.smrs = renderersToCombine;
        welderTest.WeldWithoutUnwelding();
    }

    #region FIRST FUNTIONAL VERSION OF THE WELDER
    /*
     * INITIAL FUNTIONAL VERSION OF THE WELDER, COMMENT BLENDSHAPE SECTIONS IF NOT NEEDED
     * 
     * 
     * public void OldWeld(SkinnedMeshRenderer skinnedMeshRenderer, SkinnedMeshRenderer[] renderersToCombine, float maxPosDiff, bool bakeRenderers = false, bool showWeldedVertices = false)
    {
        Debug.LogWarning("LI_MeshWelder.Weld");
        if (renderersToCombine.Length == 0) return;

        var combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var shapeDict = new Dictionary<string, BlendShapeData>();
        var combinedVertices = new List<Vector3>();
        var combinedNormals = new List<Vector3>();
        var combinedUvs = new List<Vector2>();
        var combinedSubMeshes = new Dictionary<Material, List<int>>();
        var combinedMaterials = new List<Material>();
        var vertexOffsetMap = new Dictionary<Mesh, int>();
        int currentRendererIdx = 0;
        var duplicatedIndexes = new List<int>();
        int vertexCount = 0;
        int prevVertexOffset = 0;
        int totalMaxVertexCount = 0;
        int indexesOffset = 0;
        var duplicatedHolder = CommonUtils.Create("Duplicated", null);

        foreach (var renderer in renderersToCombine)
        {
            totalMaxVertexCount += renderer.sharedMesh.vertexCount;
        }

        NativeList<int> indexes = new NativeList<int>(totalMaxVertexCount, Allocator.TempJob);

        for (int r = 0; r < renderersToCombine.Length; r++)
        {
            var smr = renderersToCombine[r];
            var mesh = new Mesh();
            if (bakeRenderers) smr.BakeMesh(mesh);
            else mesh = smr.sharedMesh;
            for (int s = 0; s < smr.sharedMesh.subMeshCount; s++)
            {
                var subMeshMaterial = smr.sharedMaterials[s];
                duplicatedIndexes.Clear();
                if (!vertexOffsetMap.TryGetValue(mesh, out int currentVertexOffset))
                {
                    if (currentRendererIdx != 0)
                    {
                        var prevMesh = new Mesh();
                        if (bakeRenderers) renderersToCombine[r - 1].BakeMesh(prevMesh);
                        else prevMesh = renderersToCombine[r - 1].sharedMesh;
                        NativeArray<float4> vertices = new NativeArray<float4>(mesh.vertices.Length, Allocator.TempJob);
                        NativeArray<float4> prevVertices = new NativeArray<float4>(prevMesh.vertices.Length, Allocator.TempJob);
                        NativeArray<bool> isDuplicatedArray = new NativeArray<bool>(mesh.vertices.Length, Allocator.TempJob);
                        NativeArray<float4> prevPosOfDuplicates = new NativeArray<float4>(mesh.vertices.Length, Allocator.TempJob);

                        vertices.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(mesh.vertices));
                        prevVertices.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(prevMesh.vertices));

                        GetDuplicatedVerticesJob getDuplicatedVerticesJob = new GetDuplicatedVerticesJob
                        {
                            vertices = vertices,
                            prevVertices = prevVertices,
                            maxPosDiff = maxPosDiff,
                            isDuplicatedArray = isDuplicatedArray,
                            prevPosOfDuplicates = prevPosOfDuplicates
                        };

                        JobHandle getDuplicatedVerticesJobHandle = getDuplicatedVerticesJob.Schedule(mesh.vertices.Length, 1000);
                        getDuplicatedVerticesJobHandle.Complete();

                        for (int z = 0; z < isDuplicatedArray.Length; z++)
                        {
                            if (isDuplicatedArray[z])
                            {
                                duplicatedIndexes.Add(z);
                                indexes.Add(combinedVertices.IndexOf(JobsTypesConversions.Float4ToVector3(prevPosOfDuplicates[z])));
                            }
                            else
                            {
                                combinedVertices.Add(mesh.vertices[z]);
                                combinedNormals.Add(mesh.normals[z]);
                                combinedUvs.Add(mesh.uv[z]);
                                indexes.Add(vertexCount);
                                vertexCount++;
                            }
                        }

                        vertexOffsetMap[mesh] = currentVertexOffset = prevVertexOffset;
                        prevVertexOffset = vertexCount;

                        vertices.Dispose();
                        prevVertices.Dispose();
                        isDuplicatedArray.Dispose();
                        prevPosOfDuplicates.Dispose();
                    }
                    else
                    {
                        var vertices = mesh.vertices;
                        var normals = mesh.normals;
                        var uv = mesh.uv;
                        for (int i = 0; i < mesh.vertices.Length; i++)
                        {
                            vertices[s] = TransformVector(smr.transform, vertices[s]);
                            normals[s] = TransformVector(smr.transform, normals[s]);
                            uv[s] = TransformVector(smr.transform, uv[s]);
                            indexes.Add(i);
                        }
                        combinedVertices.AddRange(mesh.vertices);
                        combinedNormals.AddRange(mesh.normals);
                        combinedUvs.AddRange(mesh.uv);
                        vertexOffsetMap[mesh] = 0;
                        vertexCount = prevVertexOffset += mesh.vertexCount;
                    }
                }

                var tris = mesh.GetTriangles(s);
                NativeArray<int> triangles = new NativeArray<int>(tris.Length, Allocator.TempJob);
                triangles.CopyFrom(tris);

                UpdateMeshTrianglesJob updateMeshTrianglesJob = new UpdateMeshTrianglesJob
                {
                    triangles = triangles,
                    indexes = indexes,
                    indexesOffset = indexesOffset
                };

                JobHandle updateMeshTrianglesJobHandle = updateMeshTrianglesJob.Schedule(triangles.Length, 1000);
                updateMeshTrianglesJobHandle.Complete();

                if (!combinedSubMeshes.TryGetValue(subMeshMaterial, out List<int> trisList))
                {
                    combinedSubMeshes[subMeshMaterial] = trisList = new List<int>();
                }
                trisList.AddRange(triangles);
                triangles.Dispose();

                //float startTime2 = Time.realtimeSinceStartup;

                ShapeFrameData frame_data;
                for (int j = 0; j < mesh.blendShapeCount; j++)
                {
                    var shape_name = mesh.GetBlendShapeName(j) + "_" + currentRendererIdx;
                    if (currentRendererIdx != 0 && duplicatedIndexes.Count != 0)
                    {
                        frame_data = new ShapeFrameData(mesh.vertexCount - duplicatedIndexes.Count);
                        var deltaVerts_list = new List<Vector3>();
                        var deltaNormals_list = new List<Vector3>();
                        var deltaTangents_list = new List<Vector3>();
                        var temp_frame_data = new ShapeFrameData(mesh.vertexCount);
                        mesh.GetBlendShapeFrameVertices(j, mesh.GetBlendShapeFrameCount(j) - 1, temp_frame_data.deltaVerts, temp_frame_data.deltaNormals, temp_frame_data.deltaTangents);
                        for (int b = 0; b < temp_frame_data.deltaVerts.Length; b++)
                        {
                            if (!duplicatedIndexes.Contains(b))
                            {
                                deltaVerts_list.Add(temp_frame_data.deltaVerts[b]);
                                deltaNormals_list.Add(temp_frame_data.deltaNormals[b]);
                                deltaTangents_list.Add(temp_frame_data.deltaTangents[b]);
                            }
                        }
                        frame_data.deltaVerts = deltaVerts_list.ToArray();
                        frame_data.deltaNormals = deltaNormals_list.ToArray();
                        frame_data.deltaTangents = deltaTangents_list.ToArray();
                    }
                    else
                    {
                        frame_data = new ShapeFrameData(mesh.vertexCount);
                        mesh.GetBlendShapeFrameVertices(j, mesh.GetBlendShapeFrameCount(j) - 1, frame_data.deltaVerts, frame_data.deltaNormals, frame_data.deltaTangents);
                    }
                    shapeDict[shape_name] = new BlendShapeData(smr.GetBlendShapeWeight(j), currentVertexOffset, frame_data);
                }
                //Debug.Log(((Time.realtimeSinceStartup - startTime2) * 1f) + " seconds for blends");

                if (showWeldedVertices)
                {
                    foreach (var duplicatedIdx in duplicatedIndexes)
                    {
                        var weldedVertGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        weldedVertGo.transform.parent = duplicatedHolder.transform;
                        weldedVertGo.transform.position = smr.sharedMesh.vertices[duplicatedIdx];
                        weldedVertGo.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    }
                }
            }
            currentRendererIdx++;
            indexesOffset += mesh.vertexCount;
        }
        indexes.Dispose();

        combinedMesh.vertices = combinedVertices.ToArray();
        combinedMesh.normals = combinedNormals.ToArray();
        combinedMesh.uv = combinedUvs.ToArray();
        combinedMesh.subMeshCount = combinedSubMeshes.Count;

        int subMeshIdx = 0;
        foreach (var combinedSubMesh in combinedSubMeshes)
        {
            combinedMesh.SetTriangles(combinedSubMesh.Value, subMeshIdx++);
            combinedMaterials.Add(combinedSubMesh.Key);
        }
        //combinedMesh.triangles = combinedMesh.GetTriangles(0);
        //combinedMesh.triangles = MeshUtils.GetReversedTriangles(combinedMesh);

        float startTime = Time.realtimeSinceStartup;

        foreach(var shape in shapeDict)
        {
            var frameData = new ShapeFrameData(combinedVertices.Count);
            combinedMesh.AddBlendShapeFrame(shape.Key, 0, frameData.deltaVerts, frameData.deltaNormals, frameData.deltaTangents);
            var shapeData = shape.Value;
            NativeArray<float4> fullDeltaVertices = new NativeArray<float4>(combinedVertices.Count, Allocator.TempJob);
            NativeArray<float4> fullDeltaNormals = new NativeArray<float4>(combinedVertices.Count, Allocator.TempJob);
            NativeArray<float4> fullDeltaTangents = new NativeArray<float4>(combinedVertices.Count, Allocator.TempJob);
            NativeArray<float4> shapeDeltaVertices = new NativeArray<float4>(shapeData.frameData.deltaVerts.Length, Allocator.TempJob);
            NativeArray<float4> shapeDeltaNormals = new NativeArray<float4>(shapeData.frameData.deltaVerts.Length, Allocator.TempJob);
            NativeArray<float4> shapeDeltaTangents = new NativeArray<float4>(shapeData.frameData.deltaVerts.Length, Allocator.TempJob);
            shapeDeltaVertices.CopyFrom(ParallelTypesConversions.Vector3ArrayToFloat4Array(shapeData.frameData.deltaVerts));
            shapeDeltaNormals.CopyFrom(ParallelTypesConversions.Vector3ArrayToFloat4Array(shapeData.frameData.deltaNormals));
            shapeDeltaTangents.CopyFrom(ParallelTypesConversions.Vector3ArrayToFloat4Array(shapeData.frameData.deltaTangents));

            CreateFullBlendsJob createFullBlendsJob = new CreateFullBlendsJob
            {
                fullDeltaVertices = fullDeltaVertices,
                fullDeltaNormals = fullDeltaNormals,
                fullDeltaTangents = fullDeltaTangents,
                shapeDeltaVertices = shapeDeltaVertices,
                shapeDeltaNormals = shapeDeltaNormals,
                shapeDeltaTangents = shapeDeltaTangents,
                frameVertexOffset = shapeData.frameVertsOffset
            };

            JobHandle createFullBlendsJobJobHandle = createFullBlendsJob.Schedule(shapeData.frameData.deltaVerts.Length, 100);
            createFullBlendsJobJobHandle.Complete();

            float4[] deltaVertices = new float4[combinedVertices.Count];
            float4[] deltaNormals = new float4[combinedVertices.Count];
            float4[] deltaTangents = new float4[combinedVertices.Count];
            fullDeltaVertices.CopyTo(deltaVertices);
            fullDeltaNormals.CopyTo(deltaNormals);
            fullDeltaTangents.CopyTo(deltaTangents);

            combinedMesh.AddBlendShapeFrame(shape.Key, 100, ParallelTypesConversions.Float4ArrayToVector3Array(deltaVertices),
                ParallelTypesConversions.Float4ArrayToVector3Array(deltaNormals), ParallelTypesConversions.Float4ArrayToVector3Array(deltaTangents));

            fullDeltaVertices.Dispose();
            fullDeltaNormals.Dispose();
            fullDeltaTangents.Dispose();
            shapeDeltaVertices.Dispose();
            shapeDeltaNormals.Dispose();
            shapeDeltaTangents.Dispose();
        }

        foreach (var shape in shapeDict)
        {
            var frameData = new ShapeFrameData(combinedVertices.Count);
            combinedMesh.AddBlendShapeFrame(shape.Key, 0, frameData.deltaVerts, frameData.deltaNormals, frameData.deltaTangents);
            var shape_data = shape.Value;
            Array.Copy(shape_data.frameData.deltaVerts, 0, frameData.deltaVerts, shape_data.frameVertsOffset, shape_data.frameData.deltaVerts.Length);
            //Array.Copy(shape_data.frameData.deltaNormals, 0, frameData.deltaNormals, shape_data.frameVertsOffset, shape_data.frameData.deltaNormals.Length);
            //Array.Copy(shape_data.frameData.deltaTangents, 0, frameData.deltaTangents, shape_data.frameVertsOffset, shape_data.frameData.deltaTangents.Length);
            combinedMesh.AddBlendShapeFrame(shape.Key, 100, frameData.deltaVerts, frameData.deltaNormals, frameData.deltaTangents);
        }

        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds for blend COPY");

        skinnedMeshRenderer.sharedMesh = combinedMesh;
        skinnedMeshRenderer.sharedMesh.name = "Generated by " + GetType().Name;
        skinnedMeshRenderer.sharedMaterials = combinedMaterials.ToArray();
        skinnedMeshRenderer.sharedMesh.RecalculateNormals();
        skinnedMeshRenderer.sharedMesh.RecalculateTangents();
        skinnedMeshRenderer.sharedMesh.RecalculateBounds();
        skinnedMeshRenderer.localBounds = skinnedMeshRenderer.sharedMesh.bounds;

        int shape_index = 0;
        foreach (var shape_data in shapeDict.Values)
        {
            skinnedMeshRenderer.SetBlendShapeWeight(shape_index, shape_data.shapeWeight);
            shape_index++;
        }
    }*/
    #endregion

    [Serializable]
    public struct SubmodelData
    {
        public List<Vector3> vertices;
        public List<Vector3> normals;
        public List<Vector2> uvs;
        public List<Vector4> tangents;
        public List<int> triangles;

        public void Init(Mesh mesh)
        {
            SetUp();
            vertices.AddRange(mesh.vertices);
            normals.AddRange(mesh.normals);
            uvs.AddRange(mesh.uv);
            tangents.AddRange(mesh.tangents);
            //triangles.AddRange(mesh.triangles);
        }

        public void SetUp()
        {
            vertices = new List<Vector3>();
            normals = new List<Vector3>();
            uvs = new List<Vector2>();
            tangents = new List<Vector4>();
            triangles = new List<int>();
        }


    }

    public SubmodelData CombineSubmodelsData(SubmodelData s1, SubmodelData s2)
    {
        var s0 = new SubmodelData();
        s0.SetUp();

        s0.vertices.AddRange(s1.vertices);
        s0.normals.AddRange(s1.normals);
        s0.uvs.AddRange(s1.uvs);
        s0.tangents.AddRange(s1.tangents);
        s0.triangles.AddRange(s1.triangles);

        s0.vertices.AddRange(s2.vertices);
        s0.normals.AddRange(s2.normals);
        s0.uvs.AddRange(s2.uvs);
        s0.tangents.AddRange(s2.tangents);
        s0.triangles.AddRange(s2.triangles);

        return s0;
    }
}