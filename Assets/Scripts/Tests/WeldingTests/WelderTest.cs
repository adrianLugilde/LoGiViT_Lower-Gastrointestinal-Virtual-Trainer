using GeometryUtils;
using Habrador_Computational_Geometry;
using JobsUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static LI_MeshWelder;
using Vertex = GeometryUtils.Vertex;

public class WelderTest : MonoBehaviour
{
    public SkinnedMeshRenderer[] smrs;
    public SkinnedMeshRenderer targetSmr;
    public bool showWeldedVertices = true;

    private LI_MeshWelder welder = new LI_MeshWelder();

    public bool FixedUpdateRecalculation = false;
    public float angle = 180f;
    public float maxPosDiff = 0.2f;

    public List<Mesh> meshesToCombine;
    public SkinnedMeshRenderer combineTarget;

    [Serialize]
    public List<SubmodelData> submodels = new List<SubmodelData>();

    public WelderTest sharedInstance;

    private bool welded = false;

    private void FixedUpdate()
    {
        if(Input.GetKeyDown(KeyCode.P))
        {
            if (welded) return;
            welded = true;
            Debug.Log("P");
        }
        if(FixedUpdateRecalculation)
        {
            NormalSolver.AdvancedRecalculateNormals(targetSmr.sharedMesh, angle);
            NormalSolver.RecalculateTangents(targetSmr.sharedMesh);
        }   
    }

    protected void Weld()
    {
        welder.Weld(targetSmr, smrs, maxPosDiff, angle, false, showWeldedVertices);
        if(targetSmr.TryGetComponent<MeshCollider>(out var mc)) mc.sharedMesh = targetSmr.sharedMesh;
        //MeshVerticesUtils.DrawVerticesAsSpheres(smrs[1].sharedMesh.vertices, 0.1f);
    }
    
    protected void RandomWeld()
    {
        var randomWelder = new RandomMeshWelder(targetSmr.sharedMesh);
        randomWelder.Weld();
        //MeshVerticesUtils.DrawVerticesAsSpheres(smrs[1].sharedMesh.vertices, 0.1f);
    }


    protected void RecalculateTangents()
    {
        NormalSolver.AdvancedRecalculateNormals(targetSmr.sharedMesh, angle, true);
        NormalSolver.RecalculateTangents(targetSmr.sharedMesh);
    }

    protected void ClearNormals()
    {
        targetSmr.sharedMesh.normals = new Vector3[targetSmr.sharedMesh.vertexCount];
    }

    protected void Combine()
    {
        if(meshesToCombine.Count == 0)
        {
            meshesToCombine = new List<Mesh>();
            meshesToCombine.Add(smrs[0].sharedMesh);
            meshesToCombine.Add(smrs[1].sharedMesh);
            //meshesToCombine.Add(smrs[2].sharedMesh);
        }
       
        var combinedVertices = new List<Vector3>();
        var combinedNormals = new List<Vector3>();
        var combinedTangents = new List<Vector4>();
        var combinedUv = new List<Vector2>();
        var combinedTriangles = new List<int>();
        var submeshTrianglesList = new List<List<int>>();
        var vertexOffsetMap = new Dictionary<Mesh, int>();
        var vertexOffset = 0;
        foreach (var mesh in meshesToCombine)
        {
            combinedVertices.AddRange(mesh.vertices);
            combinedNormals.AddRange(mesh.normals);
            combinedUv.AddRange(mesh.uv);
            combinedTangents.AddRange(mesh.tangents);
            combinedTriangles.AddRange(mesh.triangles.Select(triIdx => triIdx + vertexOffset));
            vertexOffset += mesh.vertexCount;
        }

        var res = new Mesh();
        res.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        res.vertices = combinedVertices.ToArray();
        res.normals = combinedNormals.ToArray();
        res.tangents = combinedTangents.ToArray();
        res.uv = combinedUv.ToArray();
        res.triangles = combinedTriangles.ToArray();
        res.RecalculateBounds();

        /*var welder = new MeshWelder(res);
        welder.Weld();*/


        combineTarget.sharedMesh = res;
        //res.RecalculateNormals();
        //res.RecalculateTangents();
    }

    public void WeldWithoutUnwelding()
    {
        var combinedVertices = new List<Vector3>();
        var combinedNormals = new List<Vector3>();
        var combinedTangents = new List<Vector4>();
        var combinedUVs = new List<Vector2>();
        var combinedTriangles = new List<int>();
        var indexList = new List<int>();
        var vertexOffset = 0;
        var maxPosOffset = maxPosDiff;
        var vertexCount = 0;
        var duplicatedVertices = new List<Vector3>();
        meshesToCombine.Clear();
        
        foreach (var smr in smrs)
        {
            var temp = new Mesh();
            smr.BakeMesh(temp);
            meshesToCombine.Add(temp);
        }

        for (int i = 0; i < meshesToCombine.Count; i++)
        {
            NormalSolver.AdvancedRecalculateNormals(meshesToCombine[i], angle, false);

            if (i != 0)
            {
                NativeArray<float4> vertices = new NativeArray<float4>(meshesToCombine[i].vertices.Length, Allocator.TempJob);
                NativeArray<float4> prevVertices = new NativeArray<float4>(meshesToCombine[i - 1].vertices.Length, Allocator.TempJob);
                NativeArray<bool> isDuplicatedArray = new NativeArray<bool>(meshesToCombine[i].vertices.Length, Allocator.TempJob);
                NativeArray<float4> prevPosOfDuplicates = new NativeArray<float4>(meshesToCombine[i].vertices.Length, Allocator.TempJob);

                vertices.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToCombine[i].vertices));
                prevVertices.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToCombine[i - 1].vertices));

                FindDuplicatedPositions getDuplicatedVerticesJob = new FindDuplicatedPositions
                {
                    points = vertices,
                    prevPoints = prevVertices,
                    maxPosDiff = maxPosOffset,
                    isDuplicatedpOINTS = isDuplicatedArray,
                    prevPointOfDuplicates = prevPosOfDuplicates
                };

                JobHandle getDuplicatedVerticesJobHandle = getDuplicatedVerticesJob.Schedule(meshesToCombine[i].vertices.Length, 1000);
                getDuplicatedVerticesJobHandle.Complete();

                for (int z = 0; z < isDuplicatedArray.Length; z++)
                {
                    if (isDuplicatedArray[z])
                    {
                        duplicatedVertices.Add(JobsTypesConversions.Float4ToVector3(prevPosOfDuplicates[z]));
                        indexList.Add(combinedVertices.IndexOf(JobsTypesConversions.Float4ToVector3(prevPosOfDuplicates[z])));
                        vertexCount++;
                        //indexList.Add(vertexCount++);
                    }
                    else
                    {
                       indexList.Add(vertexCount++);
                    }
                }

                vertices.Dispose();
                prevVertices.Dispose();
                isDuplicatedArray.Dispose();
                prevPosOfDuplicates.Dispose();

                /*foreach (var triangleIdx in meshesToCombine[i].triangles)
                {
                    combinedTriangles.Add(indexList[triangleIdx + vertexOffset]);
                }
                vertexOffset += meshesToCombine[i].vertexCount;*/
            } else
            {
                
                /*for(int l = 0; l < meshesToCombine[i].triangles.Length; l++)
                {
                    combinedTriangles.Add(meshesToCombine[i].triangles[l]);
                }*/
                for (int l = 0; l < meshesToCombine[i].vertexCount; l++)
                {
                    indexList.Add(l);
                }
                NormalSolver.AdvancedRecalculateNormals(meshesToCombine[i], angle, false);
                //vertexOffset += meshesToCombine[i].vertexCount;
                //vertexCount = vertexOffset;
            }
            
            combinedVertices.AddRange(meshesToCombine[i].vertices);
            combinedNormals.AddRange(meshesToCombine[i].normals);
            combinedUVs.AddRange(meshesToCombine[i].uv);
            combinedTangents.AddRange(meshesToCombine[i].tangents);

            NativeArray<int> triangles = new NativeArray<int>(meshesToCombine[i].triangles.Length, Allocator.TempJob);
            NativeArray<int> indexes = new NativeArray<int>(indexList.Count, Allocator.TempJob);
            triangles.CopyFrom(meshesToCombine[i].triangles);
            indexes.CopyFrom(indexList.ToArray());

            UpdateMeshTrianglesJob updateMeshTrianglesJob = new UpdateMeshTrianglesJob
            {
                triangles = triangles,
                indexes = indexes,
                indexesOffset = vertexOffset
            };

            JobHandle updateMeshTrianglesJobHandle = updateMeshTrianglesJob.Schedule(triangles.Length, 1000);
            updateMeshTrianglesJobHandle.Complete();

            combinedTriangles.AddRange(triangles);
            triangles.Dispose();
            indexes.Dispose();
            /*foreach (var triangleIdx in meshesToCombine[i].triangles)
            {
                combinedTriangles.Add(indexList[triangleIdx + vertexOffset]);
            }*/


            vertexCount = vertexOffset += meshesToCombine[i].vertexCount;
        }


        var res = new Mesh();
        res.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        res.vertices = combinedVertices.ToArray();
        res.normals = combinedNormals.ToArray();
        res.tangents = combinedTangents.ToArray();
        res.uv = combinedUVs.ToArray();
        res.triangles = combinedTriangles.ToArray();
        //res.RecalculateBounds();
        //res.RecalculateNormals();
        //res.RecalculateTangents();
        Debugger.PrintIEnumMsg(indexList, Debugger.MessageType.Warn, nameof(indexList));
        /*Debugger.PrintIEnumMsg(indexList.GetRange(2820, indexList.Count - 2820), Debugger.MessageType.Warn, nameof(indexList));
        Debugger.PrintIEnumMsg(res.triangles.ToList().GetRange(8600, res.triangles.Length - 8600), Debugger.MessageType.Log, nameof(res.triangles));
        Debugger.PrintIEnumMsg(combinedTriangles.GetRange(8600, combinedTriangles.Count - 8600), Debugger.MessageType.Error, nameof(res.triangles));*/
        if (showWeldedVertices) MeshVerticesUtils.DrawVerticesAsSpheres(duplicatedVertices, 0.001f, nameof(duplicatedVertices));
        //MeshVerticesUtils.DrawVerticesAsSpheres(combinedVertices, 0.1f);
        if (combineTarget.TryGetComponent<MeshCollider>(out var mc)) mc.sharedMesh = res;
        combineTarget.sharedMesh = res;
        
    }

    protected void WeldWithUnwelding()
    {
        var combinedVertices = new List<Vector3>();
        var combinedNormals = new List<Vector3>();
        var combinedTangents = new List<Vector4>();
        var combinedUVs = new List<Vector2>();
        var combinedTriangles = new List<int>();
        var indexList = new List<int>();
        var vertexOffset = 0;
        var maxPosOffset = maxPosDiff;
        var vertexCount = 0;
        var duplicatedVertices = new List<Vector3>();
        meshesToCombine.Clear();

        foreach(var smr in smrs)
        {
            var temp = new Mesh();
            smr.BakeMesh(temp);
            meshesToCombine.Add(temp);
        }

        for (int i = 0; i < meshesToCombine.Count; i++)
        {
            //TESTING THIS 
            /*NormalSolver.AdvancedRecalculateNormals(meshesToCombine[i], angle, false);
            var randomWelder = new MeshWelder(meshesToCombine[i]);
            randomWelder.Weld();*/

            if (i != 0)
            {
                NativeArray<float4> vertices = new NativeArray<float4>(meshesToCombine[i].vertices.Length, Allocator.TempJob);
                NativeArray<float4> prevVertices = new NativeArray<float4>(meshesToCombine[i - 1].vertices.Length, Allocator.TempJob);
                NativeArray<bool> isDuplicatedArray = new NativeArray<bool>(meshesToCombine[i].vertices.Length, Allocator.TempJob);
                NativeArray<float4> prevPosOfDuplicates = new NativeArray<float4>(meshesToCombine[i].vertices.Length, Allocator.TempJob);

                vertices.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToCombine[i].vertices));
                prevVertices.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToCombine[i - 1].vertices));

                FindDuplicatedPositions getDuplicatedPositionsJob = new FindDuplicatedPositions
                {
                    points = vertices,
                    prevPoints = prevVertices,
                    maxPosDiff = maxPosOffset,
                    isDuplicatedpOINTS = isDuplicatedArray,
                    prevPointOfDuplicates = prevPosOfDuplicates
                };

                JobHandle getDuplicatedPositionsJobHandle = getDuplicatedPositionsJob.Schedule(meshesToCombine[i].vertices.Length, 1000);
                getDuplicatedPositionsJobHandle.Complete();

                for (int z = 0; z < isDuplicatedArray.Length; z++)
                {
                    if (isDuplicatedArray[z])
                    {
                        duplicatedVertices.Add(JobsTypesConversions.Float4ToVector3(prevPosOfDuplicates[z]));
                        indexList.Add(combinedVertices.IndexOf(JobsTypesConversions.Float4ToVector3(prevPosOfDuplicates[z])));
                        vertexCount++;
                    }
                    else
                    {
                        indexList.Add(vertexCount++);
                    }
                }
                vertices.Dispose();
                prevVertices.Dispose();
                isDuplicatedArray.Dispose();
                prevPosOfDuplicates.Dispose();
            }
            else
            {
                for (int l = 0; l < meshesToCombine[i].vertexCount; l++)
                {
                    indexList.Add(l);
                }
            }

            combinedVertices.AddRange(meshesToCombine[i].vertices);
            combinedNormals.AddRange(meshesToCombine[i].normals);
            combinedUVs.AddRange(meshesToCombine[i].uv);
            combinedTangents.AddRange(meshesToCombine[i].tangents);

            NativeArray<int> triangles = new NativeArray<int>(meshesToCombine[i].triangles.Length, Allocator.TempJob);
            NativeArray<int> indexes = new NativeArray<int>(indexList.Count, Allocator.TempJob);
            triangles.CopyFrom(meshesToCombine[i].triangles);
            indexes.CopyFrom(indexList.ToArray());

            UpdateMeshTrianglesJob updateMeshTrianglesJob = new UpdateMeshTrianglesJob
            {
                triangles = triangles,
                indexes = indexes,
                indexesOffset = vertexOffset
            };

            JobHandle updateMeshTrianglesJobHandle = updateMeshTrianglesJob.Schedule(triangles.Length, 1000);
            updateMeshTrianglesJobHandle.Complete();

            combinedTriangles.AddRange(triangles);
            triangles.Dispose();
            indexes.Dispose();
            /*foreach (var triangleIdx in meshesToCombine[i].triangles)
            {
                combinedTriangles.Add(indexList[triangleIdx + vertexOffset]);
            }*/


            vertexCount = vertexOffset += meshesToCombine[i].vertexCount;

            /*var tempMesh = new Mesh();
            tempMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            tempMesh.vertices = combinedVertices.ToArray();
            tempMesh.normals = combinedNormals.ToArray();
            tempMesh.tangents = combinedTangents.ToArray();
            tempMesh.uv = combinedUVs.ToArray();
            tempMesh.triangles = combinedTriangles.ToArray();

            NormalSolver.AdvancedRecalculateNormals(tempMesh, angle, true);

            var randomWelder2 = new MeshWelder(tempMesh);
            CommonUtils.TestMethod( () => randomWelder2.Weld(),"random weld");

            combinedVertices = tempMesh.vertices.ToList();
            combinedNormals = tempMesh.normals.ToList();
            combinedUVs = tempMesh.uv.ToList();
            combinedTangents = tempMesh.tangents.ToList();
            combinedTriangles = tempMesh.triangles.ToList();

            indexList.Clear();
            for (int l = 0; l < combinedVertices.Count; l++)
            {
                indexList.Add(l);
            }*/

        }   

        var res = new Mesh();
        res.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        res.vertices = combinedVertices.ToArray();
        res.normals = combinedNormals.ToArray();
        res.tangents = combinedTangents.ToArray();
        res.uv = combinedUVs.ToArray();
        res.triangles = combinedTriangles.ToArray();

        NormalSolver.AdvancedRecalculateNormals(res, angle, true);
        var randomWelder2 = new RandomMeshWelder(res);
        randomWelder2.Weld();
        //CommonUtils.TestMethod(() => randomWelder2.Weld(), "random weld");

        if (showWeldedVertices) MeshVerticesUtils.DrawVerticesAsSpheres(duplicatedVertices, 0.001f, nameof(duplicatedVertices));
        if (combineTarget.TryGetComponent<MeshCollider>(out var mc)) mc.sharedMesh = res;
        combineTarget.sharedMesh = res;
    }

    protected void WeldWithUnweldingAndWeldAgain()
    {
        var combinedVertices = new List<Vector3>();
        var combinedNormals = new List<Vector3>();
        var combinedTangents = new List<Vector4>();
        var combinedUVs = new List<Vector2>();
        var combinedTriangles = new List<int>();
        var indexList = new List<int>();
        var vertexOffset = 0;
        var maxPosOffset = maxPosDiff;
        var vertexCount = 0;
        var duplicatedVertices = new List<Vector3>();
        var duplicatedDict = new Dictionary<int, bool[]>();
        submodels.Clear();
        meshesToCombine.Clear();

        foreach (var smr in smrs)
        {
            var temp1 = new Mesh();
            smr.BakeMesh(temp1);
            meshesToCombine.Add(temp1);
        }

        for (int i = 0; i < meshesToCombine.Count; i++)
        {
            

            var submodel = new SubmodelData();
            submodel.SetUp();

            if (i != 0)
            {
                NativeArray<float4> vertices = new NativeArray<float4>(meshesToCombine[i].vertices.Length, Allocator.TempJob);
                NativeArray<float4> prevVertices = new NativeArray<float4>(meshesToCombine[i - 1].vertices.Length, Allocator.TempJob);
                NativeArray<bool> isDuplicatedArray = new NativeArray<bool>(meshesToCombine[i].vertices.Length, Allocator.TempJob);
                NativeArray<float4> prevPosOfDuplicates = new NativeArray<float4>(meshesToCombine[i].vertices.Length, Allocator.TempJob);

                vertices.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToCombine[i].vertices));
                prevVertices.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToCombine[i - 1].vertices));

                FindDuplicatedPositions getDuplicatedVerticesJob = new FindDuplicatedPositions
                {
                    points = vertices,
                    prevPoints = prevVertices,
                    maxPosDiff = maxPosOffset,
                    isDuplicatedpOINTS = isDuplicatedArray,
                    prevPointOfDuplicates = prevPosOfDuplicates
                };

                JobHandle getDuplicatedVerticesJobHandle = getDuplicatedVerticesJob.Schedule(meshesToCombine[i].vertices.Length, 1000);
                getDuplicatedVerticesJobHandle.Complete();

                for (int z = 0; z < isDuplicatedArray.Length; z++)
                {
                    if (isDuplicatedArray[z])
                    {
                        var prevPos = JobsTypesConversions.Float4ToVector3(prevPosOfDuplicates[z]);
                        var prevIdxInSubmodel = submodels[i - 1].vertices.IndexOf(prevPos);
                        var prevIdxTris = submodels[i - 1].vertices.IndexOf(prevPos) + vertexOffset - submodels[i - 1].vertices.Count;
                        duplicatedVertices.Add(prevPos);
                        indexList.Add(prevIdxTris);

                        vertexCount++;


                        submodel.vertices.Add(prevPos);
                        submodel.normals.Add(submodels[i - 1].normals[prevIdxInSubmodel]);
                        submodel.uvs.Add(submodels[i - 1].uvs[prevIdxInSubmodel]);
                        submodel.tangents.Add(submodels[i - 1].tangents[prevIdxInSubmodel]);
                    }
                    else
                    {
                        indexList.Add(vertexCount++);

                        submodel.vertices.Add(meshesToCombine[i].vertices[z]);
                        submodel.normals.Add(meshesToCombine[i].normals[z]);
                        submodel.uvs.Add(meshesToCombine[i].uv[z]);
                        submodel.tangents.Add(meshesToCombine[i].tangents[z]);
                    }
                }

                submodels.Add(submodel);

                var tempList = new bool[isDuplicatedArray.Length];
                isDuplicatedArray.CopyTo(tempList);
                duplicatedDict[i] = tempList;

                vertices.Dispose();
                prevVertices.Dispose();
                isDuplicatedArray.Dispose();
                prevPosOfDuplicates.Dispose();
            }
            else
            {
                for (int l = 0; l < meshesToCombine[i].vertexCount; l++)
                {
                    indexList.Add(l);
                }
                submodel.Init(meshesToCombine[i]);
                submodels.Add(submodel);
            }

            combinedVertices.AddRange(meshesToCombine[i].vertices);
            combinedNormals.AddRange(meshesToCombine[i].normals);
            combinedUVs.AddRange(meshesToCombine[i].uv);
            combinedTangents.AddRange(meshesToCombine[i].tangents);

            NativeArray<int> triangles = new NativeArray<int>(meshesToCombine[i].triangles.Length, Allocator.TempJob);
            NativeArray<int> indexes = new NativeArray<int>(indexList.Count, Allocator.TempJob);
            triangles.CopyFrom(meshesToCombine[i].triangles);
            indexes.CopyFrom(indexList.ToArray());

            UpdateMeshTrianglesJob updateMeshTrianglesJob = new UpdateMeshTrianglesJob
            {
                triangles = triangles,
                indexes = indexes,
                indexesOffset = vertexOffset
            };

            JobHandle updateMeshTrianglesJobHandle = updateMeshTrianglesJob.Schedule(triangles.Length, 1000);
            updateMeshTrianglesJobHandle.Complete();

            if(i != 0) submodel.triangles.AddRange(triangles);

            combinedTriangles.AddRange(triangles);
            triangles.Dispose();
            indexes.Dispose();
            vertexCount = vertexOffset += meshesToCombine[i].vertexCount;
        }

        var s0 = submodels[0];
        var s1 = submodels[1];
        var s2 = submodels[2];

        
        var s3 = CombineSubmodelsData(s0, s1);
        submodels.Add(s3);

        var temp = new Mesh();
        temp.vertices = s3.vertices.ToArray();
        temp.normals = s3.normals.ToArray();
        temp.uv = s3.uvs.ToArray();
        temp.tangents = s3.tangents.ToArray();
        temp.triangles = s3.triangles.ToArray();

        NormalSolver.AdvancedRecalculateNormals(temp, angle, true);
        Debug.Log(temp.vertexCount);
        var randomWelder = new RandomMeshWelder(temp);
        randomWelder.Weld();
        var s4 = new SubmodelData();
        s4.SetUp();
        s4.Init(temp);
        submodels.Add(s4);

        var s5 = CombineSubmodelsData(s1, s2);
        submodels.Add(s5);

        temp.vertices = s5.vertices.ToArray();
        temp.normals = s5.normals.ToArray();
        temp.uv = s5.uvs.ToArray();
        temp.tangents = s5.tangents.ToArray();
        temp.triangles = s4.triangles.ToArray(); //usamos los triangulos de la primera union, son meshes iguales por lo que la reconstrucci�n debe ser la misma

        NormalSolver.AdvancedRecalculateNormals(temp, angle, true);
        Debug.Log(temp.vertexCount);
        randomWelder = new RandomMeshWelder(temp);
        randomWelder.Weld();

        var s6 = new SubmodelData();
        s6.SetUp();
        s6.Init(temp);
        s6.triangles = s5.triangles;
        submodels.Add(s6);






        var res = new Mesh();
        res.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        res.vertices = combinedVertices.ToArray();
        res.normals = combinedNormals.ToArray();
        res.tangents = combinedTangents.ToArray();
        res.uv = combinedUVs.ToArray();
        res.triangles = combinedTriangles.ToArray();
        //Debugger.PrintIEnumMsg(res.triangles, Debugger.MessageType.Error, "res.triangles");
        if (showWeldedVertices) MeshVerticesUtils.DrawVerticesAsSpheres(duplicatedVertices, 0.001f, nameof(duplicatedVertices));
        if (combineTarget.TryGetComponent<MeshCollider>(out var mc)) mc.sharedMesh = res;
        combineTarget.sharedMesh = res;
        Debug.LogError(duplicatedDict[1].SequenceEqual(duplicatedDict[2]));
    }

    public void LI_ParallelWeld()
    {
        var welder = new LI_MeshWelder();
        submodels = welder.ParallelWeld(combineTarget, smrs, 1e-6f, 30f, true, false);
    }

    public void LI_WeldDoingThingsOnlyOnce()
    {
        var welder = new LI_MeshWelder();
        welder.Weld(combineTarget, smrs, 1e-6f, 30f, true, false);
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

    public void WeldDoingThingsOnlyOnce()
    {
        var combinedVertices = new List<Vector3>();
        var combinedNormals = new List<Vector3>();
        var combinedTangents = new List<Vector4>();
        var combinedUVs = new List<Vector2>();
        var combinedTriangles = new List<int>();
        var indexList = new List<int>();
        var vertexOffset = 0;
        var maxPosOffset = maxPosDiff;
        var vertexCount = 0;
        var duplicatedVertices = new List<Vector3>();
        meshesToCombine.Clear();
         
        foreach (var smr in smrs)
        {
            var temp = new Mesh();
            smr.BakeMesh(temp);
            meshesToCombine.Add(temp);
        }
        
        NativeArray<float4> points = new NativeArray<float4>(meshesToCombine[1].vertices.Length, Allocator.TempJob);
        NativeArray<float4> prevPoints = new NativeArray<float4>(meshesToCombine[0].vertices.Length, Allocator.TempJob);
        NativeArray<bool> isDuplicatedPoints = new NativeArray<bool>(meshesToCombine[1].vertices.Length, Allocator.TempJob);
        NativeArray<float4> prePointOfDuplicates = new NativeArray<float4>(meshesToCombine[1].vertices.Length, Allocator.TempJob);

        points.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToCombine[1].vertices));
        prevPoints.CopyFrom(JobsTypesConversions.Vector3ArrayToFloat4Array(meshesToCombine[0].vertices));

        FindDuplicatedPositions findDuplicatedPoints = new FindDuplicatedPositions
        {
            points = points,
            prevPoints = prevPoints,
            maxPosDiff = maxPosOffset,
            isDuplicatedpOINTS = isDuplicatedPoints,
            prevPointOfDuplicates = prePointOfDuplicates
        };

        JobHandle findDuplicatedPointsJob = findDuplicatedPoints.Schedule(meshesToCombine[1].vertices.Length, 1000);
        findDuplicatedPointsJob.Complete();

        for (int l = 0; l < meshesToCombine[0].vertexCount; l++)
        {
            indexList.Add(l);
            vertexCount++;
        }
       
        for (int z = 0; z < isDuplicatedPoints.Length; z++)
        {
            if (isDuplicatedPoints[z])
            {
                var prevPoint = JobsTypesConversions.Float4ToVector3(prePointOfDuplicates[z]);
                var prevIdx = prevPoints.IndexOf(prePointOfDuplicates[z]);
                duplicatedVertices.Add(prevPoint);
                indexList.Add(prevIdx);
                vertexCount++;
            }
            else
            {
                indexList.Add(vertexCount++);
            }
        }

        var templateIdxsList = indexList.GetRange(meshesToCombine[0].vertexCount, meshesToCombine[0].vertexCount);
        var originIdxsList = indexList.GetRange(0, meshesToCombine[0].vertexCount);

        NativeArray<int> triangles = new NativeArray<int>(meshesToCombine[0].triangles.Length, Allocator.Persistent);
        NativeArray<int> indexes = new NativeArray<int>(meshesToCombine[0].vertexCount, Allocator.Persistent);

        for (int i = 0; i < meshesToCombine.Count; i++)
        {
            combinedVertices.AddRange(meshesToCombine[i].vertices);
            combinedNormals.AddRange(meshesToCombine[i].normals);
            combinedUVs.AddRange(meshesToCombine[i].uv);
            combinedTangents.AddRange(meshesToCombine[i].tangents);

            triangles.CopyFrom(meshesToCombine[i].triangles);

            if (i != 0)
            {
                indexes.CopyFrom(templateIdxsList.ToArray());
            } else
            {
                indexes.CopyFrom(originIdxsList.ToArray());
            }

            UpdateMeshTrianglesJob2 updateMeshTrianglesJob2 = new UpdateMeshTrianglesJob2
            {
                triangles = triangles,
                indexes = indexes,
                indexesOffset = vertexOffset
            };

            JobHandle updateMeshTrianglesJob2Handle = updateMeshTrianglesJob2.Schedule(triangles.Length, 1000);
            updateMeshTrianglesJob2Handle.Complete();

            combinedTriangles.AddRange(triangles);

            if(i != 0) vertexOffset += meshesToCombine[i].vertexCount;
        }

        var res = new Mesh();
        res.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        res.vertices = combinedVertices.ToArray();
        res.normals = combinedNormals.ToArray();
        res.tangents = combinedTangents.ToArray();
        res.uv = combinedUVs.ToArray();
        res.triangles = combinedTriangles.ToArray();

        NormalSolver.AdvancedRecalculateNormals(res, angle, true);

        var vertexArray = CreateVertexArray(res);
        NativeArray<Vertex> vertexNA = new NativeArray<Vertex>(vertexArray.Length, Allocator.TempJob);
        NativeArray<int> newIndexes = new NativeArray<int>(vertexArray.Length, Allocator.TempJob);
        NativeList<Vertex> newVertices = new NativeList<Vertex>(vertexArray.Length, Allocator.TempJob);
        vertexNA.CopyFrom(vertexArray);

        GetDuplicatedVertices2 getDuplicatedVerticesJob2 = new GetDuplicatedVertices2
        {
            vertices = vertexNA,
            indices = newIndexes,
            newVertices = newVertices,
            maxPosDiff = 1e-6f,
            maxAngleDiff = 1e-6f,
            vertexOffset = meshesToCombine[0].triangles.Length
        };

        JobHandle getDuplicateVerticesJobHandle = getDuplicatedVerticesJob2.Schedule();
        getDuplicateVerticesJobHandle.Complete();

        var newIndicesArray = new int[newIndexes.Length];
        newIndexes.CopyTo(newIndicesArray);
        RemapTriangles(res, newIndicesArray);

        var newVerticesList = new List<Vertex>();
        for(int i = 0; i < newVertices.Length; i++)
        {
            newVerticesList.Add(newVertices[i]);
        }

        res.vertices = newVerticesList.Select(v => v.position).ToArray();
        res.normals = newVerticesList.Select(v => v.normal).ToArray();
        res.uv = newVerticesList.Select(v => v.uv).ToArray();

        if (showWeldedVertices) MeshVerticesUtils.DrawVerticesAsSpheres(duplicatedVertices, 0.001f, nameof(duplicatedVertices));
        if (combineTarget.TryGetComponent<MeshCollider>(out var mc)) mc.sharedMesh = res;
        combineTarget.sharedMesh = res;

        points.Dispose();
        prevPoints.Dispose();
        isDuplicatedPoints.Dispose();
        prePointOfDuplicates.Dispose();
        triangles.Dispose();
        indexes.Dispose();
        vertexNA.Dispose();
        newIndexes.Dispose();
        newVertices.Dispose();
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
        [NativeDisableParallelForRestriction] public NativeArray<bool> isDuplicatedpOINTS;
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
                    isDuplicatedpOINTS[index] = isDuplicated = true;
                    prevPointOfDuplicates[index] = prevPosition;
                }
                else
                {
                    isDuplicatedpOINTS[index] = false;
                    prevPointOfDuplicates[index] = points[index];
                }
            }
        }
    }

    
    //[BurstCompile]
    public struct GetDuplicateVertices : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vertex> vertices;
        [NativeDisableParallelForRestriction] public NativeArray<int> indices;
        public NativeParallelMultiHashMap<int, Vertex>.ParallelWriter newVertices;
        [ReadOnly] public float maxPosDiff;
        [ReadOnly] public float maxAngleDiff;
        [ReadOnly] public int vertexOffset;//17280

        public void Execute(int index)
        {
            var currentVertices = new List<Vertex>();
            var startingIdx = index * vertexOffset;
            var endingIdx = (index + 1) * vertexOffset;
            Debug.Log(index + " // " + startingIdx + " .. " + endingIdx);
            for (int i = startingIdx; i < endingIdx; i++)
            {
                bool duplicate = false;
                for(int  j = 0; j < currentVertices.Count; j++)
                {
                    Debug.Log(vertices[i].position +  " - " + currentVertices[j].position);
                    duplicate = true;
                    if ((vertices[i].position - currentVertices[j].position).sqrMagnitude > maxPosDiff) duplicate = false;
                    if (Vector3.Angle(vertices[i].normal, currentVertices[j].normal) > maxAngleDiff) duplicate = false;
                    if (duplicate) break;
                }
                if(!duplicate)
                {
                    Debug.LogError("!duplicate " + vertices[i]);
                    indices[i] = vertices[i].index;
                    currentVertices.Add(vertices[i]);
                } else
                {

                }
            }
            Debug.LogWarning(currentVertices.Count);
            for(int i = 0; i < currentVertices.Count; i++)
            {
                newVertices.Add(index, currentVertices[i]);
            }
        }
    }

    [BurstCompile]
    public struct GetDuplicatedVertices2 : IJob
    {
        [ReadOnly] public NativeArray<Vertex> vertices;
        public NativeArray<int> indices;
        public NativeList<Vertex> newVertices;
        [ReadOnly] public float maxPosDiff;
        [ReadOnly] public float maxAngleDiff;
        [ReadOnly] public int vertexOffset;//17280

        public void Execute()
        {
            for(int i = 0; i < vertices.Length; i++)
            {
                bool duplicate = false;
                for(int j = 0; j < newVertices.Length; j++)
                {
                    if (Compare(vertices[i], newVertices[j]))
                    {
                        indices[i] = j;
                        duplicate = true;
                        break;
                    }
                }
                if(!duplicate)
                {
                    indices[i] = newVertices.Length;
                    newVertices.Add(vertices[i]);
                }
            }
        }

        public bool Compare(Vertex v1, Vertex v2)
        {
            if ((v1.position - v2.position).sqrMagnitude > maxPosDiff) return false;
            if (Vector3.Angle(v1.normal, v2.normal) > maxAngleDiff) return false;

            return true;
        }
    }

    public struct GetDuplicateVertices2 : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vertex> vertices;
        [NativeDisableParallelForRestriction] public NativeArray<int> indices;
        [ReadOnly] public float maxPosDiff;
        [ReadOnly] public float maxAngleDiff;
        [ReadOnly] public int vertexOffset;//17280

        public void Execute(int index)
        {
            /*List<Vector3> newVerts
            var startingIdx = index * vertexOffset;
            for(int i = index * vertexOffset; i < vertexOffset; i++)
            {
                if ((vertices[i] - vertices[i].pos).sqrMagnitude > maxPosDel) res = false;
                if (Vector3.Angle(verts[index].normal, verts[index].normal) > maxAngDel) res = false;
            }*/
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
            triangles[index] = indexes[triangles[index] + indexesOffset];
        }
    }


    [BurstCompile]
    public struct UpdateMeshTrianglesJob2 : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<int> triangles;
        [ReadOnly] public NativeArray<int> indexes;
        [ReadOnly] public int indexesOffset;

        public void Execute(int index)
        {
            triangles[index] = indexes[triangles[index]] + indexesOffset;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(WelderTest))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            WelderTest myScript = (WelderTest)target;
            if (GUILayout.Button("Weld"))
            {
                myScript.Weld();
            }
            else if (GUILayout.Button("Recalculate Tangents"))
            {
                myScript.RecalculateTangents();
            }
            else if (GUILayout.Button("Clear Normals"))
            {
                myScript.ClearNormals();
            }
            else if (GUILayout.Button("Combine meshes"))
            {
                myScript.Combine();
            }
            else if (GUILayout.Button("Weld without unwelding"))
            {
                CommonUtils.TestMethod(() => myScript.WeldWithoutUnwelding(), "WeldWithoutUnwelding");
            }
            else if (GUILayout.Button("Weld with unwelding"))
            {
                CommonUtils.TestMethod( () =>  myScript.WeldWithUnwelding(), "WeldWithUnwelding");
            }
            else if (GUILayout.Button("Weld With Unwelding And  Weld Again"))
            {
                CommonUtils.TestMethod(() => myScript.WeldWithUnweldingAndWeldAgain(), "WeldWithUnweldingAndWeldAgain");
            }
            else if (GUILayout.Button("WeldDoingThingsOnlyOnce"))
            {
                CommonUtils.TestMethod(() => myScript.WeldDoingThingsOnlyOnce(), "WeldDoingThingsOnlyOnce");
            }
            else if (GUILayout.Button("LI_WeldDoingThingsOnlyOnce"))
            {
                myScript.LI_WeldDoingThingsOnlyOnce();
                //CommonUtils.TestMethod(() => myScript.LI_WeldDoingThingsOnlyOnce(), "LI_WeldDoingThingsOnlyOnce");
            }
            else if (GUILayout.Button("LI_ParallelWeld"))
            {
                CommonUtils.TestMethod(() => myScript.LI_ParallelWeld(), "LI_ParallelWeld");
            }
            else if (GUILayout.Button("Random weld"))
            {
                myScript.RandomWeld();
            }
        }
    }
#endif
}
