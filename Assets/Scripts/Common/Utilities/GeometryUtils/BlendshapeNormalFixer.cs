using System.Collections.Generic;
using UnityEngine;

namespace GeometryUtils
{
    /// <summary>
    /// Fixes blendshape delta normals that are inconsistent with the mesh's custom
    /// (explicitly authored) base normals.
    ///
    /// Root cause: 3D tools (Blender, Maya) compute blendshape delta normals from
    /// geometry at export time. If the base mesh has custom/explicit normals that
    /// differ from its geometry normals, the delta is relative to the geometry base,
    /// not the custom base. Adding it to the custom base pushes some normals past 90°.
    ///
    /// Fix: for any vertex whose blended normal (base + delta) would flip past 90°,
    /// zero out the delta so the vertex keeps the custom base normal unchanged.
    /// </summary>
    public static class BlendshapeNormalFixer
    {
        /// <summary>
        /// Corrects the blendshape frames on <paramref name="mesh"/> in-place.
        /// The mesh must be a writable instance (not a direct reference to an imported asset).
        /// </summary>
        public static void FixBlendshapeNormals(Mesh mesh)
        {
            if (mesh == null || mesh.blendShapeCount == 0) return;

            int vc = mesh.vertexCount;
            var baseNormals = mesh.normals;

            int shapeCount = mesh.blendShapeCount;
            var names = new string[shapeCount];
            var frames = new List<(float weight, Vector3[] dv, Vector3[] dn, Vector3[] dt)>[shapeCount];

            for (int s = 0; s < shapeCount; s++)
            {
                names[s] = mesh.GetBlendShapeName(s);
                int frameCount = mesh.GetBlendShapeFrameCount(s);
                frames[s] = new List<(float, Vector3[], Vector3[], Vector3[])>(frameCount);

                for (int f = 0; f < frameCount; f++)
                {
                    float weight = mesh.GetBlendShapeFrameWeight(s, f);
                    var dv = new Vector3[vc];
                    var dn = new Vector3[vc];
                    var dt = new Vector3[vc];
                    mesh.GetBlendShapeFrameVertices(s, f, dv, dn, dt);

                    for (int i = 0; i < vc; i++)
                    {
                        if (Vector3.Dot(baseNormals[i] + dn[i], baseNormals[i]) < 0f)
                            dn[i] = Vector3.zero;
                    }

                    frames[s].Add((weight, dv, dn, dt));
                }
            }

            mesh.ClearBlendShapes();
            for (int s = 0; s < shapeCount; s++)
                foreach (var (weight, dv, dn, dt) in frames[s])
                    mesh.AddBlendShapeFrame(names[s], weight, dv, dn, dt);
        }
    }
}
