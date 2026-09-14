using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*
	Originally created by Bunny83.
	Was streamlined and modified a bit by Terrev to better fit the needs of this project,
	then overhauled by grappigegovert for major speed improvements.
	Bunny83's original version:
	https://www.dropbox.com/s/u0wfq42441pkoat/MeshWelder.cs?dl=0
	Which was posted here:
	http://answers.unity3d.com/questions/1382854/welding-vertices-at-runtime.html
*/

namespace Utility.MeshHelper
{
    public enum EVertexAttribute
    {
        Position = 0x0001,
        Normal   = 0x0002,
        UV1      = 0x0010,  // mesh.uv  (channel 0)
        UV2      = 0x0020,  // mesh.uv2 (channel 1)
        UV3      = 0x0040,  // mesh.uv3 (channel 2)
        UV4      = 0x0080,  // mesh.uv4 (channel 3)
        UV5      = 0x0100,  // mesh.uv5 (channel 4)
        UV6      = 0x0200,  // mesh.uv6 (channel 5)
        UV7      = 0x0400,  // mesh.uv7 (channel 6)
        UV8      = 0x0800,  // mesh.uv8 (channel 7)
    }

    public class Vertex
    {
        public Vector3 pos;
        public Vector3 normal;
        public Vector2 uv1;
        public Vector2 uv2;
        public Vector2 uv3;
        public Vector2 uv4;
        public Vector2 uv5;
        public Vector2 uv6;
        public Vector2 uv7;
        public Vector2 uv8;

        public Vertex(Vector3 aPos) { pos = aPos; }

        public override bool Equals(object obj)
        {
            if (obj is Vertex other)
            {
                return other.pos    == pos
                    && other.normal == normal
                    && other.uv1    == uv1
                    && other.uv2    == uv2
                    && other.uv3    == uv3
                    && other.uv4    == uv4
                    && other.uv5    == uv5
                    && other.uv6    == uv6
                    && other.uv7    == uv7
                    && other.uv8    == uv8;
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = pos.x.GetHashCode();
                hashCode = (hashCode * 397) ^ pos.y.GetHashCode();
                hashCode = (hashCode * 397) ^ pos.z.GetHashCode();
                hashCode = (hashCode * 397) ^ normal.x.GetHashCode();
                hashCode = (hashCode * 397) ^ normal.y.GetHashCode();
                hashCode = (hashCode * 397) ^ normal.z.GetHashCode();
                return hashCode;
            }
        }
    }

    public class MeshWelder
    {
        Vertex[] vertices;
        Dictionary<Vertex, List<int>> newVerts;
        int[] map;
        int[] reverseMap; // newIndex → one representative oldIndex

        EVertexAttribute m_Attributes;
        public Mesh customMesh;

        public MeshWelder(Mesh mesh) { customMesh = mesh; }

        private bool HasAttr(EVertexAttribute aAttr) => (m_Attributes & aAttr) != 0;

        private void CreateVertexList()
        {
            var positions = customMesh.vertices;
            var normals   = customMesh.normals;

            var uvArrays = new Vector2[8][];
            for (int c = 0; c < 8; c++)
            {
                var list = new List<Vector2>();
                customMesh.GetUVs(c, list);
                uvArrays[c] = list.Count == positions.Length ? list.ToArray() : null;
            }

            m_Attributes = EVertexAttribute.Position;
            if (normals    != null && normals.Length    > 0) m_Attributes |= EVertexAttribute.Normal;
            if (uvArrays[0] != null)                         m_Attributes |= EVertexAttribute.UV1;
            if (uvArrays[1] != null)                         m_Attributes |= EVertexAttribute.UV2;
            if (uvArrays[2] != null)                         m_Attributes |= EVertexAttribute.UV3;
            if (uvArrays[3] != null)                         m_Attributes |= EVertexAttribute.UV4;
            if (uvArrays[4] != null)                         m_Attributes |= EVertexAttribute.UV5;
            if (uvArrays[5] != null)                         m_Attributes |= EVertexAttribute.UV6;
            if (uvArrays[6] != null)                         m_Attributes |= EVertexAttribute.UV7;
            if (uvArrays[7] != null)                         m_Attributes |= EVertexAttribute.UV8;

            // --- DIAGNOSTIC ---
            for (int c = 0; c < 8; c++)
            {
                var list2 = new List<Vector2>(); customMesh.GetUVs(c, list2);
                bool active = uvArrays[c] != null;
                string first = active ? uvArrays[c][0].ToString("F4") : "N/A";
                Debug.LogWarning($"[MeshWelder] INPUT  UV{c}: GetUVs={list2.Count}  positions={positions.Length}  active={active}  first={first}");
            }

            vertices = new Vertex[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var v = new Vertex(positions[i]);
                if (HasAttr(EVertexAttribute.Normal)) v.normal = normals[i];
                if (HasAttr(EVertexAttribute.UV1))    v.uv1    = uvArrays[0][i];
                if (HasAttr(EVertexAttribute.UV2))    v.uv2    = uvArrays[1][i];
                if (HasAttr(EVertexAttribute.UV3))    v.uv3    = uvArrays[2][i];
                if (HasAttr(EVertexAttribute.UV4))    v.uv4    = uvArrays[3][i];
                if (HasAttr(EVertexAttribute.UV5))    v.uv5    = uvArrays[4][i];
                if (HasAttr(EVertexAttribute.UV6))    v.uv6    = uvArrays[5][i];
                if (HasAttr(EVertexAttribute.UV7))    v.uv7    = uvArrays[6][i];
                if (HasAttr(EVertexAttribute.UV8))    v.uv8    = uvArrays[7][i];
                vertices[i] = v;
            }
        }

        private void RemoveDuplicates()
        {
            newVerts = new Dictionary<Vertex, List<int>>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vertex v = vertices[i];
                if (newVerts.TryGetValue(v, out var originals))
                    originals.Add(i);
                else
                    newVerts.Add(v, new List<int> { i });
            }
        }

        // Pending arrays — built by BuildMap, applied by ApplyVertexArrays
        private Vector3[]   _newPositions;
        private Vector3[]   _newNormals;
        private Vector2[][] _uvOut;

        // Phase 1: build map/reverseMap and fill local arrays WITHOUT touching the mesh.
        // Must be called while the mesh still has the original (pre-weld) vertex count so
        // that RemapTriangles can read the triangles without them being invalidated.
        private void BuildMap()
        {
            int count = newVerts.Count;
            map = new int[vertices.Length];

            _newPositions = new Vector3[count];
            _newNormals   = new Vector3[count];

            _uvOut = new Vector2[8][];
            for (int c = 0; c < 8; c++)
                if (HasAttr((EVertexAttribute)(0x0010 << c)))
                    _uvOut[c] = new Vector2[count];

            reverseMap = new int[count];
            int i = 0;
            foreach (var kvp in newVerts)
            {
                reverseMap[i] = kvp.Value[0];
                foreach (int index in kvp.Value)
                    map[index] = i;

                _newPositions[i] = kvp.Key.pos;
                _newNormals[i]   = kvp.Key.normal;
                if (_uvOut[0] != null) _uvOut[0][i] = kvp.Key.uv1;
                if (_uvOut[1] != null) _uvOut[1][i] = kvp.Key.uv2;
                if (_uvOut[2] != null) _uvOut[2][i] = kvp.Key.uv3;
                if (_uvOut[3] != null) _uvOut[3][i] = kvp.Key.uv4;
                if (_uvOut[4] != null) _uvOut[4][i] = kvp.Key.uv5;
                if (_uvOut[5] != null) _uvOut[5][i] = kvp.Key.uv6;
                if (_uvOut[6] != null) _uvOut[6][i] = kvp.Key.uv7;
                if (_uvOut[7] != null) _uvOut[7][i] = kvp.Key.uv8;
                i++;
            }
        }

        // Phase 2: remap triangles while vertex count is still T (all old indices valid).
        private void RemapTriangles()
        {
            int[] tris = customMesh.triangles;
            for (int i = 0; i < tris.Length; i++)
                tris[i] = map[tris[i]];
            customMesh.triangles = tris;
        }

        // Phase 3a: change vertex count to W and write positions/normals.
        // UVs are written in ApplyUVs, after blendshapes are fully committed, to avoid
        // Unity 6.4's vertex buffer reorganisation (triggered by AddBlendShapeFrame)
        // corrupting UV channels that were written before blendshapes.
        private void ApplyPositionsAndNormals()
        {
            customMesh.vertices = _newPositions;
            customMesh.normals  = _newNormals;
        }

        // Phase 3b: write UV channels — must run AFTER RemapBlendshapes.
        private void ApplyUVs()
        {
            for (int c = 0; c < 8; c++)
                if (_uvOut[c] != null)
                    customMesh.SetUVs(c, _uvOut[c]);

            // --- DIAGNOSTIC ---
            for (int c = 0; c < 8; c++)
            {
                var list = new List<Vector2>(); customMesh.GetUVs(c, list);
                bool wrote = _uvOut[c] != null;
                string expected = wrote ? _uvOut[c][0].ToString("F4") : "N/A";
                string actual   = list.Count > 0 ? list[0].ToString("F4") : "EMPTY";
                Debug.Log($"[MeshWelder] OUTPUT UV{c}: wrote={wrote}  expected[0]={expected}  actual[0]={actual}  count={list.Count}");
            }
        }

        private void RemapBlendshapes(
            string[] names, int[] frameCounts, float[][] frameWeights,
            Vector3[][] deltaVerts, Vector3[][] deltaNormals, Vector3[][] deltaTangents)
        {
            int newCount = reverseMap.Length;
            customMesh.ClearBlendShapes();

            int frameIdx = 0;
            for (int s = 0; s < names.Length; s++)
            {
                for (int f = 0; f < frameCounts[s]; f++, frameIdx++)
                {
                    var dv = new Vector3[newCount];
                    var dn = new Vector3[newCount];
                    var dt = new Vector3[newCount];

                    for (int i = 0; i < newCount; i++)
                    {
                        int old = reverseMap[i];
                        dv[i] = deltaVerts[frameIdx][old];
                        dn[i] = deltaNormals[frameIdx][old];
                        dt[i] = deltaTangents[frameIdx][old];
                    }

                    customMesh.AddBlendShapeFrame(names[s], frameWeights[s][f], dv, dn, dt);
                }
            }
        }

        public void Weld()
        {
            // Read blendshapes before vertex arrays are reassigned
            int shapeCount = customMesh.blendShapeCount;
            int oldVertCount = customMesh.vertexCount;
            var names        = new string[shapeCount];
            var frameCounts  = new int[shapeCount];
            var frameWeights = new float[shapeCount][];

            int totalFrames = 0;
            for (int s = 0; s < shapeCount; s++)
            {
                names[s]        = customMesh.GetBlendShapeName(s);
                frameCounts[s]  = customMesh.GetBlendShapeFrameCount(s);
                frameWeights[s] = new float[frameCounts[s]];
                for (int f = 0; f < frameCounts[s]; f++)
                    frameWeights[s][f] = customMesh.GetBlendShapeFrameWeight(s, f);
                totalFrames += frameCounts[s];
            }

            var deltaVerts   = new Vector3[totalFrames][];
            var deltaNormals = new Vector3[totalFrames][];
            var deltaTangents= new Vector3[totalFrames][];
            var dv = new Vector3[oldVertCount];
            var dn = new Vector3[oldVertCount];
            var dt = new Vector3[oldVertCount];

            int frameIdx = 0;
            for (int s = 0; s < shapeCount; s++)
            {
                for (int f = 0; f < frameCounts[s]; f++, frameIdx++)
                {
                    customMesh.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                    deltaVerts[frameIdx]    = (Vector3[])dv.Clone();
                    deltaNormals[frameIdx]  = (Vector3[])dn.Clone();
                    deltaTangents[frameIdx] = (Vector3[])dt.Clone();
                }
            }

            CreateVertexList();
            RemoveDuplicates();
            BuildMap();
            RemapTriangles();
            customMesh.ClearBlendShapes(); // clear before vertex count changes to avoid buffer conflicts
            ApplyPositionsAndNormals();
            if (shapeCount > 0)
                RemapBlendshapes(names, frameCounts, frameWeights, deltaVerts, deltaNormals, deltaTangents);
            ApplyUVs(); // write UVs last, after blendshape buffer is stable
        }
    }
}
