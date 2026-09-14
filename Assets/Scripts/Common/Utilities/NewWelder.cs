﻿/*
 * Optimized single-pass mesh welder + optional duplicate-vertex gizmos
 * ────────────────────────────────────────────────────────────────────
 * Extra: mirrors UVs on every 2nd segment (0-1 ► 1-0) so neighbouring
 *        segments share identical UVs at their welded borders.
 */

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;

namespace LI.MeshTools
{
    public static class NewWelder
    {
        #region PUBLIC API
        public static void Weld(
            SkinnedMeshRenderer   destination,
            SkinnedMeshRenderer[] sources,
            bool   bake                     = false,
            float  positionEpsilon          = 0f,
            bool   showDuplicatedVertices   = false,
            bool   flipAlternateUVSegments  = true, // mirror 1-D UV on every odd segment
            bool   flipUAxis                = true  // true = flip U, false = flip V
        )
        {
            if (sources == null || sources.Length == 0) return;

            // ── 0. cell size for spatial hash ────────────────────────────────
            var refMesh = bake ? new Mesh() : sources[0].sharedMesh;
            if (bake) sources[0].BakeMesh(refMesh);
            float cell = positionEpsilon > 0f
                       ? positionEpsilon
                       : math.max(1e-6f,
                                  math.max(math.max(refMesh.bounds.extents.x,
                                                    refMesh.bounds.extents.y),
                                           refMesh.bounds.extents.z) * 1e-4f);

            int vEstimate = sources.Length * refMesh.vertexCount;
            int tEstimate = sources.Length * refMesh.triangles.Length;

            var posL  = new List<Vector3>(vEstimate);
            var nrmL  = new List<Vector3>(vEstimate);
            var tanL  = new List<Vector4>(vEstimate);
            var uvL   = new List<Vector2>(vEstimate);
            var triL  = new List<int>(tEstimate);
            var dupL  = new List<Vector3>();

            var hash  = new NativeParallelHashMap<int,int>(vEstimate, Allocator.TempJob);
            var qNew  = new NativeQueue<NewVertexInfo>(Allocator.TempJob);
            var qDup  = new NativeQueue<float3>(Allocator.TempJob);

            NativeArray<float3> posNA  = default;
            NativeArray<float3> nrmNA  = default;
            NativeArray<float4> tanNA  = default;
            NativeArray<float2> uvNA   = default;
            NativeArray<int>    mapNA  = default;

            int seg = 0;
            foreach (var smr in sources)
            {
                var mesh = bake ? new Mesh() : smr.sharedMesh;
                if (bake) smr.BakeMesh(mesh);

                // --- mirror UVs for odd segments ----------------------------------
                Vector2[] uvSrc = mesh.uv;
                if (flipAlternateUVSegments && (seg & 1) == 1)
                {
                    uvSrc = (Vector2[])uvSrc.Clone();
                    if (flipUAxis)
                        for (int i = 0; i < uvSrc.Length; ++i) uvSrc[i].x = 1f - uvSrc[i].x;
                    else
                        for (int i = 0; i < uvSrc.Length; ++i) uvSrc[i].y = 1f - uvSrc[i].y;
                }

                // --- resize / copy -------------------------------------------------
                Resize(ref posNA, mesh.vertexCount);
                Resize(ref nrmNA, mesh.vertexCount);
                Resize(ref tanNA, mesh.vertexCount);
                Resize(ref uvNA,  mesh.vertexCount);
                Resize(ref mapNA, mesh.vertexCount);

                MemCpy(mesh.vertices, posNA);
                MemCpy(mesh.normals,  nrmNA);
                MemCpy(mesh.tangents, tanNA);
                MemCpy(uvSrc,         uvNA);

                // --- weld job ------------------------------------------------------
                new SpatialWeldJob
                {
                    positions       = posNA,
                    normals         = nrmNA,
                    tangents        = tanNA,
                    uvs             = uvNA,
                    cellSize        = cell,
                    hashToIndex     = hash,
                    localToGlobal   = mapNA,
                    newVertices     = qNew.AsParallelWriter(),
                    duplicateVerts  = qDup.AsParallelWriter()
                }.Schedule(mesh.vertexCount, 64).Complete();

                // --- flush queues --------------------------------------------------
                while (qNew.TryDequeue(out var nv))
                {
                    if (!hash.TryGetValue(nv.cellHash, out int g))
                    {
                        g = posL.Count;
                        posL.Add(nv.position);
                        nrmL.Add(nv.normal);
                        tanL.Add(nv.tangent);
                        uvL .Add(nv.uv);
                        hash.TryAdd(nv.cellHash, g);
                    }
                    mapNA[nv.localIndex] = g;
                }
                while (qDup.TryDequeue(out var p)) dupL.Add(p);

                // --- triangles -----------------------------------------------------
                var tri = mesh.triangles;
                for (int i = 0; i < tri.Length; ++i)
                    tri[i] = mapNA[tri[i]];
                triL.AddRange(tri);

                ++seg;
            }

            // ── 1. build final mesh via MeshData ───────────────────────────────
            var mdA = Mesh.AllocateWritableMeshData(1);
            var md  = mdA[0];
            md.SetVertexBufferParams(posL.Count,
                new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new(VertexAttribute.Normal,   VertexAttributeFormat.Float32, 3, 1),
                new(VertexAttribute.Tangent,  VertexAttributeFormat.Float32, 4, 2),
                new(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32, 2, 3));
            md.SetIndexBufferParams(triL.Count, IndexFormat.UInt32);

            md.GetVertexData<Vector3>(0).CopyFrom(posL.ToArray());
            md.GetVertexData<Vector3>(1).CopyFrom(nrmL.ToArray());
            md.GetVertexData<Vector4>(2).CopyFrom(tanL.ToArray());
            md.GetVertexData<Vector2>(3).CopyFrom(uvL .ToArray());
            md.GetIndexData<int>()      .CopyFrom(triL.ToArray());

            md.subMeshCount = 1;
            md.SetSubMesh(0, new(0, triL.Count), MeshUpdateFlags.DontRecalculateBounds);

            var welded = new Mesh { indexFormat = IndexFormat.UInt32 };
            Mesh.ApplyAndDisposeWritableMeshData(mdA, welded);
            welded.RecalculateBounds();
            NormalSolver.AdvancedRecalculateNormals(welded, 35f, true);

            destination.sharedMesh      = welded;
            destination.sharedMaterials = sources[0].sharedMaterials;
            destination.localBounds     = welded.bounds;

            if (showDuplicatedVertices && dupL.Count > 0)
                GeometryUtils.MeshVerticesUtils.DrawVerticesAsSpheres(dupL, 0.01f, "Duplicates");

            // dispose -------------------------------------------------------------
            posNA.Dispose(); nrmNA.Dispose(); tanNA.Dispose(); uvNA.Dispose(); mapNA.Dispose();
            hash.Dispose();  qNew.Dispose();  qDup.Dispose();
        }
        #endregion

        #region WELD JOB
        [BurstCompile] private struct SpatialWeldJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public NativeArray<float3> normals;
            [ReadOnly] public NativeArray<float4> tangents;
            [ReadOnly] public NativeArray<float2> uvs;

            public float cellSize;
            [ReadOnly] public NativeParallelHashMap<int,int> hashToIndex;
            [NativeDisableParallelForRestriction] public NativeArray<int> localToGlobal;
            public NativeQueue<NewVertexInfo>.ParallelWriter newVertices;
            public NativeQueue<float3>.ParallelWriter       duplicateVerts;

            public void Execute(int i)
            {
                float3 p = positions[i];
                int3 c   = (int3)math.floor(p / cellSize);
                int  key = (c.x*73856093) ^ (c.y*19349663) ^ (c.z*83492791);

                if (hashToIndex.TryGetValue(key, out int g))
                {
                    localToGlobal[i] = g;
                    duplicateVerts.Enqueue(p);
                }
                else
                {
                    localToGlobal[i] = -1;
                    newVertices.Enqueue(new NewVertexInfo
                    {
                        localIndex = i,
                        cellHash   = key,
                        position   = p,
                        normal     = normals[i],
                        tangent    = tangents[i],
                        uv         = uvs[i]
                    });
                }
            }
        }

        private struct NewVertexInfo
        {
            public int    localIndex;
            public int    cellHash;
            public float3 position;
            public float3 normal;
            public float4 tangent;
            public float2 uv;
        }
        #endregion

        #region HELPERS
        private static void Resize<T>(ref NativeArray<T> arr, int len) where T:struct
        {
            if (!arr.IsCreated) arr = new NativeArray<T>(len, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            else if (arr.Length < len)
            {
                arr.Dispose();
                arr = new NativeArray<T>(len, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            }
        }

        private static unsafe void MemCpy(Vector3[] src, NativeArray<float3> dst)
        { fixed (Vector3* p = src) UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(dst), p, src.Length*sizeof(float3)); }
        private static unsafe void MemCpy(Vector4[] src, NativeArray<float4> dst)
        { fixed (Vector4* p = src) UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(dst), p, src.Length*sizeof(float4)); }
        private static unsafe void MemCpy(Vector2[] src, NativeArray<float2> dst)
        { fixed (Vector2* p = src) UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(dst), p, src.Length*sizeof(float2)); }
        #endregion
    }
}
