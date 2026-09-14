#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Ica.Normal;

namespace LoGiViT.Editor
{
    public static class SkinnedMeshRendererContextMenu
    {
        [MenuItem("CONTEXT/SkinnedMeshRenderer/Recalculate Normals (Custom)")]
        static void RecalculateNormalsCustom(MenuCommand command)
        {
            Mesh mesh = GetInstancedMesh((SkinnedMeshRenderer)command.context);
            if (mesh == null) return;
            Undo.RecordObject(mesh, "Recalculate Normals");
            NormalSolver2.RecalculateNormals(mesh, 180f);
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/Recalculate Normals (ICA)")]
        static void RecalculateNormalsIca(MenuCommand command)
        {
            Mesh mesh = GetInstancedMesh((SkinnedMeshRenderer)command.context);
            if (mesh == null) return;
            Undo.RecordObject(mesh, "Recalculate Normals");
            mesh.RecalculateNormalsIca(180f);
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/Recalculate Normals")]
        static void RecalculateNormals(MenuCommand command)
        {
            Mesh mesh = GetInstancedMesh((SkinnedMeshRenderer)command.context);
            if (mesh == null) return;
            Undo.RecordObject(mesh, "Recalculate Normals");
            mesh.RecalculateNormals();
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/Recalculate Tangents")]
        static void RecalculateTangents(MenuCommand command)
        {
            Mesh mesh = GetInstancedMesh((SkinnedMeshRenderer)command.context);
            if (mesh == null) return;
            Undo.RecordObject(mesh, "Recalculate Tangents");
            mesh.RecalculateTangents();
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/Recalculate Bounds")]
        static void RecalculateBounds(MenuCommand command)
        {
            Mesh mesh = GetInstancedMesh((SkinnedMeshRenderer)command.context);
            if (mesh == null) return;
            Undo.RecordObject(mesh, "Recalculate Bounds");
            mesh.RecalculateBounds();
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/Recalculate Normals, Tangents & Bounds")]
        static void RecalculateAll(MenuCommand command)
        {
            Mesh mesh = GetInstancedMesh((SkinnedMeshRenderer)command.context);
            if (mesh == null) return;
            Undo.RecordObject(mesh, "Recalculate Normals, Tangents & Bounds");
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/Fix Blendshape Delta Normals")]
        static void FixBlendshapeDeltaNormals(MenuCommand command)
        {
            var smr = (SkinnedMeshRenderer)command.context;
            Mesh mesh = GetInstancedMesh(smr);
            if (mesh == null) return;
            if (mesh.blendShapeCount == 0)
            {
                Debug.LogWarning("[SkinnedMeshRenderer] Mesh has no blendshapes.", smr);
                return;
            }
            Undo.RecordObject(mesh, "Fix Blendshape Delta Normals");
            RecomputeBlendshapeDeltaNormals(mesh);
            Debug.Log($"[SkinnedMeshRenderer] Fixed blendshape delta normals on '{mesh.name}'.", smr);
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/Save Mesh as Asset")]
        static void SaveMeshAsAsset(MenuCommand command)
        {
            var smr = (SkinnedMeshRenderer)command.context;
            if (smr == null) return;

            Mesh mesh = smr.sharedMesh;
            if (mesh == null)
            {
                Debug.LogWarning("[SkinnedMeshRenderer] No sharedMesh assigned.", smr);
                return;
            }

            string defaultName = mesh.name.Replace(" (Instance)", "");
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Mesh as Asset",
                defaultName,
                "asset",
                "Choose a location to save the mesh asset.");

            if (string.IsNullOrEmpty(path)) return;

            string assetName = System.IO.Path.GetFileNameWithoutExtension(path);

            if (AssetDatabase.Contains(mesh))
            {
                // Mesh is already a project asset — save a copy and redirect the renderer to it.
                Mesh copy = Object.Instantiate(mesh);
                copy.name = assetName;
                NormalSolver2.RecalculateTangents(copy); // ensure tangents are valid for the new asset
                AssetDatabase.CreateAsset(copy, path);
                Undo.RecordObject(smr, "Save Mesh as Asset");
                smr.sharedMesh = copy;
            }
            else
            {
                // Mesh is an in-memory instance — save it in place (renderer reference stays valid).
                mesh.name = assetName;
                NormalSolver2.RecalculateTangents(mesh); // ensure tangents are valid for the new asset
                AssetDatabase.CreateAsset(mesh, path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SkinnedMeshRenderer] Saved mesh as '{path}'.", smr);
        }

        // Recomputes blendshape delta normals from geometry.
        //
        // Root cause: Blender exports delta normals as (blendshape_geometry_normal - base_geometry_normal).
        // Unity's base normals are custom/authored normals, not geometry normals, so the delta is wrong.
        //
        // Fix: for each frame, compute smooth geometry normals of the deformed mesh, orient them to match
        // the authored base normals, then take the difference: dn = oriented_deformedGeomNormal - customBase.
        //
        // Orientation flip: geometry normals from cross products may point opposite to authored normals
        // (e.g. inside-out meshes like the colon). The flip decision is made using the position-averaged
        // authored normal (posBaseNormal) so that all UV-seam split vertices at the same 3D position
        // make the same flip decision, avoiding per-index disagreement at seam boundaries.
        static void RecomputeBlendshapeDeltaNormals(Mesh mesh)
        {
            int vc = mesh.vertexCount;
            Vector3[] basePositions = mesh.vertices;
            Vector3[] baseNormals   = mesh.normals;   // custom/authored normals

            // Identify boundary vertices (x = minX or x = maxX).
            // These are the seam rings shared with adjacent sections. Zeroing their delta
            // ensures both sides of a seam always retain the same base normal → no visible seam.
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < vc; i++)
            {
                float x = basePositions[i].x;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }
            const float kBoundaryEps = 1e-4f;
            var isBoundary = new bool[vc];
            for (int i = 0; i < vc; i++)
            {
                float x = basePositions[i].x;
                isBoundary[i] = (x <= minX + kBoundaryEps) || (x >= maxX - kBoundaryEps);
            }

            // Cache triangle indices for all submeshes once
            var allTriangles = new int[mesh.subMeshCount][];
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                allTriangles[sub] = mesh.GetTriangles(sub);

            // Position-averaged authored normal: keyed by 3D position so UV-seam split vertices
            // (same position, different UV) accumulate together. Used only for the orientation flip
            // decision — the actual delta uses the per-vertex authored normal.
            var posBaseNormal = new Dictionary<Vector3, Vector3>(vc);
            for (int i = 0; i < vc; i++)
            {
                posBaseNormal.TryGetValue(basePositions[i], out var acc);
                posBaseNormal[basePositions[i]] = acc + baseNormals[i];
            }

            int shapeCount = mesh.blendShapeCount;
            var names  = new string[shapeCount];
            var frames = new List<(float weight, Vector3[] dv, Vector3[] dn, Vector3[] dt)>[shapeCount];

            for (int s = 0; s < shapeCount; s++)
            {
                names[s]  = mesh.GetBlendShapeName(s);
                int fc    = mesh.GetBlendShapeFrameCount(s);
                frames[s] = new List<(float, Vector3[], Vector3[], Vector3[])>(fc);

                for (int f = 0; f < fc; f++)
                {
                    float weight = mesh.GetBlendShapeFrameWeight(s, f);
                    var dv = new Vector3[vc];
                    var dn = new Vector3[vc];
                    var dt = new Vector3[vc];
                    mesh.GetBlendShapeFrameVertices(s, f, dv, dn, dt);

                    // Deformed positions for this frame
                    var deformedPos = new Vector3[vc];
                    for (int i = 0; i < vc; i++)
                        deformedPos[i] = basePositions[i] + dv[i];

                    // Smooth normals of the deformed geometry (position-welded for seam consistency)
                    var deformedGeomNormals = ComputeSmoothNormals(deformedPos, allTriangles, vc);

                    for (int i = 0; i < vc; i++)
                    {
                        // Boundary vertices are the seam rings shared between adjacent sections.
                        // Zero the delta so both sides of every seam always keep the same base normal.
                        if (isBoundary[i])
                        {
                            dn[i] = Vector3.zero;
                            continue;
                        }

                        Vector3 dgn = deformedGeomNormals[i];

                        // Degenerate vertex: not covered by any triangle, or all adjacent faces have
                        // near-zero area. Zero the delta so the vertex retains its custom base normal.
                        if (dgn.sqrMagnitude < 1e-6f)
                        {
                            dn[i] = Vector3.zero;
                            continue;
                        }

                        // Flip the deformed geometry normal into the same hemisphere as the authored
                        // base normals. Use the position-averaged base normal for the decision so all
                        // split vertices at the same position flip consistently.
                        if (posBaseNormal.TryGetValue(basePositions[i], out var pbm) && Vector3.Dot(pbm, dgn) < 0f)
                            dgn = -dgn;

                        dn[i] = dgn - baseNormals[i];
                    }

                    frames[s].Add((weight, dv, dn, dt));
                }
            }

            mesh.ClearBlendShapes();
            for (int s = 0; s < shapeCount; s++)
                foreach (var (weight, dv, dn, dt) in frames[s])
                    mesh.AddBlendShapeFrame(names[s], weight, dv, dn, dt);
        }

        // Area-weighted smooth normals with position welding.
        //
        // Meshes from Blender often have UV-seam split vertices: two or more vertex indices
        // at the exact same 3D position. Accumulating normals per vertex index gives each
        // split vertex only the faces on its own side of the seam → wrong half-normals.
        // Fix: accumulate per unique position, then distribute back to all indices sharing it.
        static Vector3[] ComputeSmoothNormals(Vector3[] positions, int[][] allTriangles, int vertexCount)
        {
            // Accumulate area-weighted face normals keyed by position.
            // Split vertices at the same position will share the accumulated result.
            var posAccum = new Dictionary<Vector3, Vector3>(vertexCount);

            foreach (var triangles in allTriangles)
            {
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int i0 = triangles[i], i1 = triangles[i + 1], i2 = triangles[i + 2];
                    Vector3 fn = Vector3.Cross(positions[i1] - positions[i0], positions[i2] - positions[i0]);

                    posAccum.TryGetValue(positions[i0], out var a0); posAccum[positions[i0]] = a0 + fn;
                    posAccum.TryGetValue(positions[i1], out var a1); posAccum[positions[i1]] = a1 + fn;
                    posAccum.TryGetValue(positions[i2], out var a2); posAccum[positions[i2]] = a2 + fn;
                }
            }

            var normals = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                if (posAccum.TryGetValue(positions[i], out var n))
                    normals[i] = n.normalized;
            return normals;
        }

        // Always works on an instanced (non-shared) copy of the mesh.
        // If sharedMesh is already an instance (not in AssetDatabase), it is used directly.
        // Otherwise a copy is instantiated and assigned back to the renderer.
        static Mesh GetInstancedMesh(SkinnedMeshRenderer smr)
        {
            if (smr == null) return null;

            Mesh mesh = smr.sharedMesh;
            if (mesh == null)
            {
                Debug.LogWarning("[SkinnedMeshRenderer] No sharedMesh assigned.", smr);
                return null;
            }

            if (!AssetDatabase.Contains(mesh))
                return mesh;

            Mesh copy = Object.Instantiate(mesh);
            copy.name = mesh.name + " (Instance)";
            Undo.RecordObject(smr, "Instance Mesh for Editing");
            smr.sharedMesh = copy;
            return copy;
        }
    }
}
#endif
