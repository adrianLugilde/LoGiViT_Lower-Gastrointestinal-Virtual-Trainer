using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;

public static class MeshModelIO
{
    // Matches your JSON fields, but in binary. Versioned for future-proofing.
    const uint MAGIC = 0x4A4D4553; // 'JMES' (JsonMesh -> binary)
    const int VERSION = 1;

    // ---------- SAVE ----------
    public static void Save(string path, Mesh mesh, IReadOnlyList<Material> submeshMaterials = null, bool gzip = false)
    {
        if (mesh == null) throw new ArgumentNullException(nameof(mesh));
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Stream s = gzip ? (Stream)new GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: false) : fs;
        using var bw = new BinaryWriter(s);

        // Header
        bw.Write(MAGIC);
        bw.Write(VERSION);

        // meshName, indexFormat
        bw.Write(mesh.name ?? string.Empty);
        bw.Write((int)mesh.indexFormat);

        // Vertices & attributes
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var tangents = mesh.tangents;

        bw.Write(vertices.Length);

        // Presence flags
        bool hasNormals = normals != null && normals.Length == vertices.Length;
        bool hasTangents = tangents != null && tangents.Length == vertices.Length;
        bw.Write(hasNormals);
        bw.Write(hasTangents);

        // UV sets (1..8)
        var uv1 = mesh.uv; var uv2 = mesh.uv2; var uv3 = mesh.uv3; var uv4 = mesh.uv4;
        var uv5 = mesh.uv5; var uv6 = mesh.uv6; var uv7 = mesh.uv7; var uv8 = mesh.uv8;
        var uvs = new[] { uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8 };
        for (int c = 0; c < 8; c++) bw.Write(uvs[c] != null && uvs[c].Length == vertices.Length);

        // Write vertex streams
        for (int i = 0; i < vertices.Length; i++) { Write(bw, vertices[i]); }
        if (hasNormals) for (int i = 0; i < vertices.Length; i++) { Write(bw, normals[i]); }
        if (hasTangents) for (int i = 0; i < vertices.Length; i++) { Write(bw, tangents[i]); }
        for (int c = 0; c < 8; c++)
        {
            var uv = uvs[c];
            bool present = uv != null && uv.Length == vertices.Length;
            if (present) for (int i = 0; i < vertices.Length; i++) { Write(bw, uv[i]); }
        }

        // Triangles: single big array (like your JsonMesh.triangles)
        // Compact to 16-bit if indexFormat == UInt16
        var bigTriangles = mesh.triangles;
        bool use16 = mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16;
        bw.Write(use16);
        bw.Write(bigTriangles.Length);
        if (use16) { for (int i = 0; i < bigTriangles.Length; i++) bw.Write((ushort)bigTriangles[i]); }
        else { for (int i = 0; i < bigTriangles.Length; i++) bw.Write(bigTriangles[i]); }

        // Submeshes: indexStart, indexCount, materialName
        int subCount = mesh.subMeshCount;
        bw.Write(subCount);
        for (int si = 0; si < subCount; si++)
        {
            var sm = mesh.GetSubMesh(si);
            bw.Write(sm.indexStart);
            bw.Write(sm.indexCount);

            string matName = (submeshMaterials != null && si < submeshMaterials.Count && submeshMaterials[si] != null)
                ? submeshMaterials[si].name
                : string.Empty;
            bw.Write(matName);
        }
    }

    // SkinnedMeshRenderer helper (parity with your JsonMesh ctor)
    public static void Save(string path, SkinnedMeshRenderer smr, bool gzip = false)
    {
        if (smr == null) throw new ArgumentNullException(nameof(smr));
        Save(path, smr.sharedMesh, smr.sharedMaterials, gzip);
    }

    // ---------- LOAD ----------
    // Add this overload next to LoadMesh(string path, bool gzipped = false)
    public static Mesh LoadMesh(string path, bool gzipped, out string[] materialNames)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Stream s = gzipped ? (Stream)new GZipStream(fs, CompressionMode.Decompress, leaveOpen: false) : fs;
        using var br = new BinaryReader(s);

        // --- existing header & geometry read (same as your LoadMesh) ---
        if (br.ReadUInt32() != MAGIC) throw new InvalidDataException("Not a CustomMeshIO (JMES) file.");
        int version = br.ReadInt32();
        if (version != VERSION) throw new NotSupportedException($"Unsupported JMES version {version}.");

        string meshName = br.ReadString();
        var indexFormat = (UnityEngine.Rendering.IndexFormat)br.ReadInt32();

        int vCount = br.ReadInt32();
        bool hasNormals = br.ReadBoolean();
        bool hasTangents = br.ReadBoolean();

        bool[] uvPresent = new bool[8];
        for (int c = 0; c < 8; c++) uvPresent[c] = br.ReadBoolean();

        var vertices = new Vector3[vCount];
        for (int i = 0; i < vCount; i++) vertices[i] = ReadVector3(br);

        Vector3[] normals = null;
        if (hasNormals) { normals = new Vector3[vCount]; for (int i = 0; i < vCount; i++) normals[i] = ReadVector3(br); }

        Vector4[] tangents = null;
        if (hasTangents) { tangents = new Vector4[vCount]; for (int i = 0; i < vCount; i++) tangents[i] = ReadVector4(br); }

        var uv = new Vector2[8][];
        for (int c = 0; c < 8; c++)
            if (uvPresent[c]) { var arr = new Vector2[vCount]; for (int i = 0; i < vCount; i++) arr[i] = ReadVector2(br); uv[c] = arr; }

        bool use16 = br.ReadBoolean();
        int triLen = br.ReadInt32();
        int[] triangles = new int[triLen];
        if (use16) { for (int i = 0; i < triLen; i++) triangles[i] = br.ReadUInt16(); }
        else { for (int i = 0; i < triLen; i++) triangles[i] = br.ReadInt32(); }

        int subCount = br.ReadInt32();
        var subIndexStart = new int[subCount];
        var subIndexCount = new int[subCount];
        var subMatNames = new string[subCount];
        for (int si = 0; si < subCount; si++)
        {
            subIndexStart[si] = br.ReadInt32();
            subIndexCount[si] = br.ReadInt32();
            subMatNames[si] = br.ReadString();
        }

        // Build mesh
        var mesh = new Mesh { name = meshName, indexFormat = indexFormat };
        mesh.SetVertices(vertices);
        if (hasNormals) mesh.SetNormals(normals);
        if (hasTangents) mesh.SetTangents(tangents);
        for (int c = 0; c < 8; c++) if (uv[c] != null) mesh.SetUVs(c, uv[c]);

        mesh.subMeshCount = subCount;
        for (int si = 0; si < subCount; si++)
        {
            var slice = new int[subIndexCount[si]];
            Array.Copy(triangles, subIndexStart[si], slice, 0, subIndexCount[si]);
            mesh.SetTriangles(slice, si, calculateBounds: false);
        }
        mesh.RecalculateBounds();

        materialNames = subMatNames;
        return mesh;
    }

    public static void LoadInto(SkinnedMeshRenderer renderer, string path, MeshCollider meshCollider = null, bool gzipped = false, Material[] fallbackMaterials = null)
    {
        if (!renderer) throw new ArgumentNullException(nameof(renderer));
        string[] materialNames;
        Mesh m = LoadMesh(path, gzipped: gzipped, out materialNames);

        var mats = new Material[m.subMeshCount];
        for (int i = 0; i < mats.Length; i++)
        {
            var matName = (materialNames != null && i < materialNames.Length) ? materialNames[i] : null;
            if (!string.IsNullOrEmpty(matName))
            {
                string resPath = string.IsNullOrEmpty(FileManager.materialResourcesPath)
                    ? matName
                    : FileManager.materialResourcesPath.TrimEnd('/', '\\') + "/" + matName;
                Debug.Log($"Loading material for submesh {i}: '{matName}' from Resources path: '{resPath}'");
                mats[i] = Resources.Load<Material>(resPath);
            }

            if (mats[i] == null && fallbackMaterials != null && i < fallbackMaterials.Length)
                mats[i] = fallbackMaterials[i];

            if (mats[i] == null)
                mats[i] = new Material(Shader.Find("Standard")) { name = matName ?? "DefaultMaterial" };
        }
        renderer.sharedMesh = m;
        renderer.sharedMaterials = mats;
        renderer.sharedMesh.RecalculateBounds();
        renderer.localBounds = m.bounds;
        meshCollider.sharedMesh = m;
    }

    // ---------- Helpers ----------
    static void Write(BinaryWriter bw, Vector3 v) { bw.Write(v.x); bw.Write(v.y); bw.Write(v.z); }
    static void Write(BinaryWriter bw, Vector4 v) { bw.Write(v.x); bw.Write(v.y); bw.Write(v.z); bw.Write(v.w); }
    static void Write(BinaryWriter bw, Vector2 v) { bw.Write(v.x); bw.Write(v.y); }

    static Vector3 ReadVector3(BinaryReader br) => new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
    static Vector4 ReadVector4(BinaryReader br) => new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
    static Vector2 ReadVector2(BinaryReader br) => new Vector2(br.ReadSingle(), br.ReadSingle());
}
