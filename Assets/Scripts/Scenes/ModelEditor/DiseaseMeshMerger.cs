// ============================================================================
// DiseaseMeshMerger.cs
//
// Merges a projected disease mesh into a cropped intestine mesh using
// Delaunay triangulation to bridge the seam between the two surfaces.
//
// Responsibilities:
//   - Crop the intestine mesh to the raycasted hit-triangle region
//   - Project all border vertices (intestine edge + disease base ring) onto a
//     shared 2D plane suitable for Delaunay input
//   - Triangulate the annular region (hull = intestine border, hole = disease ring)
//   - Unproject Delaunay results back to 3D via an exact-match lookup table
//   - Combine cropped intestine, projected disease, and Delaunay bridge into one mesh
//     with separate submeshes for each layer
//
// Architecture:
//   - Pure C# class (no MonoBehaviour). No Unity scene lifetime.
//   - All working buffers are instance fields reused across Merge() calls to avoid
//     per-call GC allocations.
//   - Owned and instantiated by DiseasePlacementController; one instance per session.
//   - Merge() is the sole public entry point.
//
// Dependency Flow:
//   DiseasePlacementController → DiseaseMeshMerger.Merge(...)
// ============================================================================

using Game.Utils.Math;
using Game.Utils.Triangulation;
using GeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MathUtils = Utilities.MathUtils;

namespace ModelEditor
{
    /// <summary>
    /// Pure-computation helper that merges a projected disease mesh into a welded intestine
    /// mesh by Delaunay-triangulating the annular gap between the cropped intestine border
    /// and the disease's base ring.
    /// </summary>
    internal sealed class DiseaseMeshMerger
    {
        #region Working Buffers

        // All lists are declared here and reused per Merge() call to avoid GC pressure.
        private readonly List<Triangle2D>              _outputTriangles         = new();
        private readonly List<Vector3>                 _disjoinedVertices       = new();
        private readonly List<Vector3>                 _edgeVertices            = new();
        private readonly List<Vector3>                 _xzDisjoinedVertices     = new();
        private readonly List<Vector3>                 _xzEdgeVertices          = new();
        private readonly List<Tuple<Vector3, Vector2>> _originalProjectedTuples = new();
        private readonly List<int[]>                   _submeshTriangles        = new();
        private readonly List<int>                     _combinedTriangles       = new();
        private readonly List<Vector2>                 _combinedUvs             = new();
        private readonly List<Vector3>                 _combinedNormals         = new();
        private readonly List<Vector3>                 _combinedVertices        = new();

        #endregion

        #region Public API

        /// <summary>
        /// Merges <paramref name="projectedDiseaseMesh"/> into <paramref name="fullIntestineMesh"/>
        /// by cropping the intestine around the hit area and bridging the seam with a Delaunay
        /// triangle fan.
        /// </summary>
        /// <param name="fullIntestineMesh">
        /// Current welded intestine mesh. Read-only; the returned mesh is a new object.
        /// </param>
        /// <param name="hitTriangleVertices">
        /// World-space positions of every intestine triangle vertex hit during projection raycasting.
        /// Defines the region to crop and the hull for triangulation.
        /// </param>
        /// <param name="projectedDiseaseMesh">
        /// Disease mesh after surface projection; vertices are in world space.
        /// </param>
        /// <param name="diseaseBaseRingIndices">
        /// Vertex indices into <paramref name="projectedDiseaseMesh"/> that form the disease's
        /// base ring — typically the Y ≈ 0 vertices in the source mesh's local space.
        /// </param>
        /// <param name="projectionForward">
        /// Camera-forward direction used during projection, needed to align the 2D triangulation plane.
        /// </param>
        /// <returns>
        /// A new combined mesh. Submesh order matches expected material slot order:
        /// [intestine submeshes…] [disease submeshes…] [Delaunay bridge].
        /// </returns>
        public Mesh Merge(
            Mesh               fullIntestineMesh,
            HashSet<Vector3>   hitTriangleVertices,
            Mesh               projectedDiseaseMesh,
            IEnumerable<int>   diseaseBaseRingIndices,
            Vector3            projectionForward)
        {
            ClearBuffers();

            if (hitTriangleVertices.Count == 0)
            {
                Debug.LogError("[DiseaseMeshMerger] No hit-triangle vertices — cannot crop intestine.");
                return fullIntestineMesh;
            }

            // --- Step 1: Crop intestine to the hit region; collect the bordering ring of vertices. ---
            Mesh intestineCroppedMesh = MeshUtils.GenerateCroppedMesh2(fullIntestineMesh, hitTriangleVertices);

            _disjoinedVertices.AddRange(
                MeshVerticesUtils.GetNeighborVertices(fullIntestineMesh, hitTriangleVertices));

#if UNITY_EDITOR
            //MeshUtils.CreateGOFromMesh(fullIntestineMesh, "originalMesh", true);
            //Debug.LogWarning($"[DiseaseMeshMerger] {fullIntestineMesh.subMeshCount} submeshes in source intestine mesh");
            //MeshUtils.CreateGOFromMesh(intestineCroppedMesh, "intestineCroppedMesh", true);
            //MeshVerticesUtils.DrawVerticesAsSpheres(hitTriangleVertices, 0.005f);
#endif

            // --- Step 2: Collect disease base-ring vertices (Y ≈ 0 in source mesh local space). ---
            var projectedVerts = projectedDiseaseMesh.vertices;
            foreach (int idx in diseaseBaseRingIndices)
                _edgeVertices.Add(projectedVerts[idx]);

            // Floating-point snapping during projection can produce duplicate edge vertices.
            var distinct = _edgeVertices.Distinct().ToList();
            _edgeVertices.Clear();
            _edgeVertices.AddRange(distinct);

            // --- Step 3: Flatten all border vertices onto a shared 2D plane. ---
            // We rotate each vertex so the projection direction aligns with the Y-axis, then
            // use X and Z as the planar coordinates for Delaunay triangulation input.
            Vector3 undoRotation = ComputeUndoRotation(projectionForward);

            foreach (var vertex in _disjoinedVertices)
            {
                var flat = MeshVerticesUtils.RotateVertex(
                    Vector3.ProjectOnPlane(vertex, projectionForward),
                    Quaternion.Euler(undoRotation));
                _xzDisjoinedVertices.Add(flat);
                _originalProjectedTuples.Add(
                    new Tuple<Vector3, Vector2>(vertex, new Vector2(flat.x, flat.z)));
            }

            foreach (var vertex in _edgeVertices)
            {
                var flat = MeshVerticesUtils.RotateVertex(
                    Vector3.ProjectOnPlane(vertex, projectionForward),
                    Quaternion.Euler(undoRotation));
                _xzEdgeVertices.Add(flat);
                _originalProjectedTuples.Add(
                    new Tuple<Vector3, Vector2>(vertex, new Vector2(flat.x, flat.z)));
            }

            // --- Step 4: Sort hull (intestine border) and hole (disease ring) clockwise. ---
            Vector2[] hullPoints = _xzDisjoinedVertices.Select(v => new Vector2(v.x, v.z)).ToArray();
            Array.Sort(hullPoints, new Vector2ClockwiseComparer(MathUtils.GetCentroid(hullPoints)));

            Vector2[] holePoints = _xzEdgeVertices.Select(v => new Vector2(v.x, v.z)).ToArray();
            Array.Sort(holePoints, new Vector2ClockwiseComparer(MathUtils.GetCentroid(holePoints)));

            // --- Step 5: Triangulate the annular region between hull and hole. ---
            var triangulation = new DelaunayTriangulation();
            triangulation.Triangulate(
                hullPoints.ToList(), 0.0f,
                new List<List<Vector2>> { holePoints.ToList() });
            triangulation.GetTrianglesDiscardingHoles(_outputTriangles);

            Mesh delaunayMesh = MeshUtils.CreateMeshFromTriangles(_outputTriangles);

            // --- Step 6: Unproject Delaunay vertices back to 3D. ---
            // Floating-point drift through the project → flatten → triangulate round-trip prevents
            // an analytical inverse transform. Instead we match each Delaunay vertex by exact 2D
            // coordinate to the stored 3D counterpart in _originalProjectedTuples.
            var recoveredVertices = new Vector3[delaunayMesh.vertexCount];
            var rawDelaunayVerts  = delaunayMesh.vertices;
            for (int i = 0; i < rawDelaunayVerts.Length; i++)
            {
                var target = (Vector2)rawDelaunayVerts[i];
                foreach (var (v3, v2) in _originalProjectedTuples)
                {
                    if (v2 == target) { recoveredVertices[i] = v3; break; }
                }
            }
            delaunayMesh.vertices = recoveredVertices;

            // --- Step 7: Assign planar UVs to the Delaunay bridge mesh. ---
            var delaunayVerts = delaunayMesh.vertices; // snapshot after 3D recovery
            var delaunayUvs   = new Vector2[delaunayVerts.Length];
            var meshBounds    = delaunayMesh.bounds;
            for (int i = 0; i < delaunayVerts.Length; i++)
            {
                delaunayUvs[i] = new Vector2(
                    delaunayVerts[i].x / meshBounds.size.x,
                    delaunayVerts[i].y / meshBounds.size.y);
            }
            delaunayMesh.uv = delaunayUvs;
            delaunayMesh.RecalculateBounds();

            // --- Step 8: Combine cropped intestine + projected disease into one mesh. ---
            Mesh combinedMesh = MeshUtils.CombineMeshes(
                new List<Mesh> { intestineCroppedMesh, projectedDiseaseMesh });
            combinedMesh.RecalculateTangents();

            for (int i = 0; i < combinedMesh.subMeshCount; i++)
                _submeshTriangles.Add(combinedMesh.GetTriangles(i));

            _combinedVertices .AddRange(combinedMesh.vertices);
            _combinedNormals  .AddRange(combinedMesh.normals);
            _combinedTriangles.AddRange(combinedMesh.triangles);
            _combinedUvs      .AddRange(combinedMesh.uv);

            // --- Step 9: Remap Delaunay triangles to combined-buffer vertex indices. ---
            // The combined mesh already contains all border vertices from both the cropped
            // intestine and the disease base ring. Build a position → index map once (O(n))
            // rather than searching per triangle vertex (O(n²) with IndexOf).
            var vertexIndexMap  = BuildVertexIndexMap(_combinedVertices);
            var delaunayTris    = delaunayMesh.triangles;
            var delaunayVerts2  = delaunayMesh.vertices;
            var newDelaunayTris = new List<int>(delaunayTris.Length);

            for (int i = 0; i < delaunayTris.Length; i++)
            {
                var vert = delaunayVerts2[delaunayTris[i]];
                if (!vertexIndexMap.TryGetValue(vert, out int idx))
                {
                    Debug.LogError("[DiseaseMeshMerger] Delaunay vertex not found in combined mesh — bridge may have holes.");
                    idx = 0;
                }
                newDelaunayTris.Add(idx);
            }

            // Overwrite UVs for the bridge vertices inside the combined UV buffer.
            for (int i = 0; i < delaunayVerts2.Length; i++)
            {
                if (vertexIndexMap.TryGetValue(delaunayVerts2[i], out int uvIdx))
                    _combinedUvs[uvIdx] = delaunayUvs[i];
            }

            // --- Step 10: Add the Delaunay bridge as its own submesh and finalize. ---
            _combinedTriangles.AddRange(newDelaunayTris);
            _submeshTriangles.Add(newDelaunayTris.ToArray());

            int finalSubmeshCount = _submeshTriangles.Count;
            combinedMesh.triangles    = _combinedTriangles.ToArray();
            combinedMesh.normals      = _combinedNormals.ToArray();
            combinedMesh.uv           = _combinedUvs.ToArray();
            combinedMesh.subMeshCount = finalSubmeshCount;
            for (int i = 0; i < finalSubmeshCount; i++)
                combinedMesh.SetTriangles(_submeshTriangles[i], i);

            combinedMesh.RecalculateBounds();
            combinedMesh.RecalculateTangents();
            combinedMesh.RecalculateNormals();

            return combinedMesh;
        }

        #endregion

        #region Private Helpers

        private void ClearBuffers()
        {
            _outputTriangles        .Clear();
            _disjoinedVertices      .Clear();
            _edgeVertices           .Clear();
            _xzDisjoinedVertices    .Clear();
            _xzEdgeVertices         .Clear();
            _originalProjectedTuples.Clear();
            _submeshTriangles       .Clear();
            _combinedTriangles      .Clear();
            _combinedUvs            .Clear();
            _combinedNormals        .Clear();
            _combinedVertices       .Clear();
        }

        /// <summary>
        /// Builds a position → first-occurrence-index dictionary for O(1) vertex lookup.
        /// When a position appears more than once, the smallest index wins
        /// (<see cref="Dictionary{K,V}.TryAdd"/> keeps the first insertion).
        /// </summary>
        private static Dictionary<Vector3, int> BuildVertexIndexMap(List<Vector3> vertices)
        {
            var map = new Dictionary<Vector3, int>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
                map.TryAdd(vertices[i], i);
            return map;
        }

        /// <summary>
        /// Returns the Euler-angle rotation that un-does the transform aligning the projection
        /// direction with the Y-axis, so that vertex XZ coordinates become correct 2D planar
        /// coordinates for Delaunay triangulation.
        /// Each component is normalized to [−180°, 180°] to avoid Quaternion.Euler discontinuities.
        /// </summary>
        private static Vector3 ComputeUndoRotation(Vector3 projectionForward)
        {
            var raw = -Quaternion.FromToRotation(-Vector3.up, projectionForward).eulerAngles;
            return new Vector3(
                NormalizeAngle(raw.x),
                NormalizeAngle(raw.y),
                NormalizeAngle(raw.z));
        }

        /// <summary>
        /// Folds an Euler-angle component into [−180°, 180°] so that values beyond ±180° are
        /// reflected back without changing the effective rotation.
        /// </summary>
        private static float NormalizeAngle(float angle) =>
            Mathf.Abs(angle) > 180f ? 360f - Mathf.Abs(angle) : angle;

        #endregion
    }
}
