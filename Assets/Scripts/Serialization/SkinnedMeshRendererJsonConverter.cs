using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class SkinnedMeshRendererJsonConverter : JsonConverter<SkinnedMeshRenderer>
{
    public override void WriteJson(JsonWriter writer, SkinnedMeshRenderer value, JsonSerializer serializer)
    {
        Mesh mesh = value.sharedMesh;
        Material[] materials = value.sharedMaterials;

        JObject jo = new JObject
        {
            { "meshName", mesh.name },
            { "indexFormat", (int)mesh.indexFormat },
            { "vertices", JArray.FromObject(mesh.vertices) },
            { "normals", JArray.FromObject(mesh.normals) },
            { "tangents", JArray.FromObject(mesh.tangents) },
            { "triangles", JArray.FromObject(mesh.triangles) },
            { "uv", JArray.FromObject(mesh.uv) },
            { "uv2", JArray.FromObject(mesh.uv2) },
            { "uv3", JArray.FromObject(mesh.uv3) },
            { "uv4", JArray.FromObject(mesh.uv4) }
        };

        // Serialize submeshes and their material names
        JArray submeshesArray = new JArray();
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            var submesh = mesh.GetSubMesh(i);
            JObject submeshObj = new JObject
            {
                { "indexStart", submesh.indexStart },
                { "indexCount", submesh.indexCount },
                { "materialName", materials[i].name }
            };
            submeshesArray.Add(submeshObj);
        }
        jo.Add("submeshes", submeshesArray);

        jo.WriteTo(writer);
    }

    public override SkinnedMeshRenderer ReadJson(JsonReader reader, Type objectType, SkinnedMeshRenderer existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject jo = JObject.Load(reader);

        Mesh mesh = new Mesh
        {
            name = jo["meshName"]?.ToString(),
            indexFormat = (UnityEngine.Rendering.IndexFormat)(int)jo["indexFormat"]
        };

        mesh.vertices = jo["vertices"]?.ToObject<Vector3[]>();
        mesh.normals = jo["normals"]?.ToObject<Vector3[]>();
        mesh.tangents = jo["tangents"]?.ToObject<Vector4[]>();
        mesh.triangles = jo["triangles"]?.ToObject<int[]>();
        mesh.uv = jo["uv"]?.ToObject<Vector2[]>();
        mesh.uv2 = jo["uv2"]?.ToObject<Vector2[]>();
        mesh.uv3 = jo["uv3"]?.ToObject<Vector2[]>();
        mesh.uv4 = jo["uv4"]?.ToObject<Vector2[]>();

        // Deserialize submeshes and their material names
        JArray submeshesArray = (JArray)jo["submeshes"];
        mesh.subMeshCount = submeshesArray.Count;
        Material[] materials = new Material[submeshesArray.Count];
        for (int i = 0; i < submeshesArray.Count; i++)
        {
            JObject submeshObj = (JObject)submeshesArray[i];
            int indexStart = (int)submeshObj["indexStart"];
            int indexCount = (int)submeshObj["indexCount"];
            string materialName = submeshObj["materialName"].ToString();

            int[] indices = new int[indexCount];
            Array.Copy(mesh.triangles, indexStart, indices, 0, indexCount);
            mesh.SetTriangles(indices, i);

            materials[i] = Resources.Load<Material>(Path.Combine(FileManager.materialResourcesPath, materialName));
        }

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        SkinnedMeshRenderer renderer = new GameObject().AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.sharedMaterials = materials;

        return renderer;
    }
}
