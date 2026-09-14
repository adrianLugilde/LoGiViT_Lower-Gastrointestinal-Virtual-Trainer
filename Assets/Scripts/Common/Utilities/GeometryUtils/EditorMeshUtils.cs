using ImgSpc.Exporters;
using Parabox.Stl;
using Utility.MeshHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace GeometryUtils
{
    [ExecuteInEditMode]
    public class EditorMeshUtils : MonoBehaviour
    {
        [Header("Geometry Modification Parameters")]
        public Vector3 translation;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
        public float normalsAngleRecalculation = 35f; //35f is the default used to generate the intestine haustra meshes
        [Header("File path for the asset/prefab creation, including filename without extension.")]
        public string filePath = "";

        public int blendshapeCopyStartIdx = 0;
        public int blendshapeCopyEndIdx = 0;

        [Header("Debug")]
        public bool drawCurrentMeshGeometry = false;
        public float gizmosScale = 0.1f;
        public float verticesAtY0Threshold = 0.00001f;
        private Mesh originalMesh;
        private MeshData meshData;
        private SkinnedMeshRenderer smr;
        private MeshCollider mc;

        [Header("Export parameters")]
        public ImgSpcExporter exporter;
        public string exportMethod = string.Empty;
        public string exportPath = string.Empty;
        private bool meshDataBuilt = false;

        [Header("Import paramenters")]
        public string sourcePath = "";

        private void Update()
        {
            //if (meshData == null) BuildMeshData();
        }

        private void Start()
        {
            smr = GetComponent<SkinnedMeshRenderer>();
            mc = GetComponent<MeshCollider>();
            if (originalMesh == null) originalMesh = smr.sharedMesh;
            //BuildMeshData();
        }

        public void BuildMeshData()
        {
            meshDataBuilt = false;
            meshData = new MeshData(originalMesh,
                           translation + transform.position,
                           Quaternion.Euler(rotation + transform.rotation.eulerAngles),
                           scale,
                           transform.GetComponent<SkinnedMeshRenderer>().sharedMaterials.ToList(), true);
            meshData.BuildData();
            smr.sharedMesh = meshData.GetTransformedMesh();
            if (mc != null) mc.sharedMesh = smr.sharedMesh;
            meshDataBuilt = true;
        }

        void UpdateMeshCollider()
        {
            Mesh bakeMesh = new Mesh();
            smr.BakeMesh(bakeMesh);
            mc.sharedMesh = bakeMesh;
        }

        void UpdateBounds()
        {
            smr = GetComponent<SkinnedMeshRenderer>();
            MeshUtils.GenerateMeshBounds(smr.sharedMesh, transform);
            smr.localBounds = smr.sharedMesh.bounds;
        }

        void ModifyMeshGeometry()
        {
            meshData.UpdateData(translation +
                transform.position,
                Quaternion.Euler(rotation),
                scale);
            smr.sharedMesh = meshData.GetTransformedMesh();
            if (mc != null) mc.sharedMesh = smr.sharedMesh;
            UpdateBounds();
        }

        private void ResetMeshGeometry()
        {
            smr.sharedMesh = originalMesh;
            if (mc != null) mc.sharedMesh = originalMesh;
            UpdateBounds();
        }

        private void GetVerticesAtY0()
        {
            Mesh bakeMesh = new Mesh();
            smr.BakeMesh(bakeMesh);

            HashSet<int> verticesAtY0 = new HashSet<int>();
            var vertices = bakeMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                var distance = (Vector3.zero - new Vector3(0f, vertices[i].y, 0f)).sqrMagnitude;
                if (distance <= verticesAtY0Threshold) verticesAtY0.Add(i);
            }
            var vertexList = new List<Vector3>();
            foreach (var vertex in verticesAtY0)
            {
                vertexList.Add(meshData.OriginalMesh.vertices[vertex]);
            }
            vertexList = vertexList.Distinct().ToList();
            Debug.Log(meshData.OriginalMesh.vertexCount + " || " + vertexList.Count);
            MeshVerticesUtils.DrawVerticesAsSpheres(vertexList, gizmosScale);
        }


        private void MeshAsNewAsset()
        {
#if UNITY_EDITOR
            AssetDatabase.CreateAsset(smr.sharedMesh, filePath + ".asset");
            AssetDatabase.SaveAssets();
#endif
        }

        private void MeshAsNewAssetWithSpecificShapes()
        {
            var mesh = new Mesh();
            MeshUtils.CopyMesh(mesh, smr.sharedMesh, false);
            MeshUtils.CopyBlendshapes(smr.sharedMesh, mesh, blendshapeCopyStartIdx, blendshapeCopyEndIdx);
            //MeshUtils.CopyBlendshapes(smr.sharedMesh, mesh, 16, 26); //rectum
            //MeshUtils.CopyBlendshapes(smr.sharedMesh, mesh, 26, 30); //cecum
#if UNITY_EDITOR
            AssetDatabase.CreateAsset(mesh, filePath + ".asset");
            AssetDatabase.SaveAssets();
#endif
        }

        private void CreateAllSectionVariationsWithBlendshapes()
        {
            var fileNames = new List<string>() { "defaultSection", "cecum", "rectum" };
            var originaluvs = smr.sharedMesh.uv;
            var defaultSectionmesh = new Mesh();
            var sharedBlendshapes = new Vector2(0, 16);
            for (int i = 0; i < fileNames.Count; i++)
            {
                var mesh = new Mesh();
                var fileName = fileNames[i];
                MeshUtils.CopyMesh(mesh, smr.sharedMesh, false);
                MeshUtils.CopyBlendshapes(smr.sharedMesh, mesh, (int)sharedBlendshapes.x, (int)sharedBlendshapes.y);
                if (fileName.Equals("cecum")) MeshUtils.CopyBlendshapes(smr.sharedMesh, mesh, 26, 30);
                if (fileName.Equals("defaultSection")) MeshUtils.CopyMesh(defaultSectionmesh, mesh, false);
                if (fileName.Equals("rectum")) MeshUtils.CopyBlendshapes(smr.sharedMesh, mesh, 16, 26);
#if UNITY_EDITOR
                AssetDatabase.CreateAsset(mesh, filePath + "/" + fileName + ".asset");
                AssetDatabase.SaveAssets();
#endif

            }
            var duvs = defaultSectionmesh.uv;
            var equals = true;
            for (int i = 0; i < originaluvs.Length; i++)
            {
                if (equals == false) break;
                if (originaluvs[i] != duvs[i])
                {
                    equals = false;
                }
            }

            if (equals == false) Debug.LogWarning("uvs not the same");
            else Debug.LogWarning("uvs the same");
        }

        private void BakeMeshAsNewAsset()
        {
#if UNITY_EDITOR
            var mesh = new Mesh();
            smr.BakeMesh(mesh);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, filePath + ".asset");
            AssetDatabase.SaveAssets();
#endif
        }

        private void BakeMeshFromGPUAsNewAsset()
        {
#if UNITY_EDITOR
            var mesh = smr.sharedMesh;
            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            var vertexBuffer = mesh.GetVertexBuffer(0);
            byte[] vertexData = new byte[vertexBuffer.count * (vertexBuffer.stride / sizeof(byte))];
            vertexBuffer.GetData(vertexData);
            var floatArray = ConvertByteArrayToFloat(vertexData);
            var newVerts = new List<Vector3>();
            for (int i = 0; i < floatArray.Length; i += 3)
            {
                newVerts.Add(new Vector3(floatArray[i], floatArray[i + 1], floatArray[i + 2]));
            }
            var newMesh = new Mesh();
            MeshUtils.CopyMesh(newMesh, mesh);
            var go = new GameObject("GPUMesh");
            go.AddComponent<SkinnedMeshRenderer>().sharedMesh = newMesh;
#endif
        }

        private float[] ConvertByteArrayToFloat(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");

            if (bytes.Length % 4 != 0)
                throw new ArgumentException
                      ("bytes does not represent a sequence of floats");

            return Enumerable.Range(0, bytes.Length / 4)
                             .Select(i => BitConverter.ToSingle(bytes, i * 4))
                             .ToArray();
        }

        private void CreateAsNewPrefab()
        {
#if UNITY_EDITOR
            PrefabUtility.SaveAsPrefabAsset(gameObject, filePath + ".prefab");
#endif
        }


        private void OnDrawGizmos()
        {
            if (drawCurrentMeshGeometry && meshDataBuilt)
            {
                foreach (var vertex in meshData.Vertices)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawSphere(vertex.position, gizmosScale);
                }
            }
        }

        private void ExportMesh()
        {
            MeshUtils.ExportMesh(exporter, exportMethod, exportPath);
        }

        public void SimplifyAndBakeMeshWithNormals()
        {
#if UNITY_EDITOR
            var mesh = new Mesh();
            smr.BakeMesh(mesh);
            NormalSolver.AdvancedRecalculateNormals(mesh, normalsAngleRecalculation, true);
            var welder = new MeshWelder(mesh);
            welder.Weld();
            AssetDatabase.CreateAsset(mesh, filePath + ".asset");
            AssetDatabase.SaveAssets();
#endif
        }

        public void FixSplittedNormalsAndVertices()
        {
#if UNITY_EDITOR
            var mesh = new Mesh();
            MeshUtils.CopyMesh(mesh, smr.sharedMesh, true);
            // Round normals to fixed precision so float-precision duplicates become exactly equal,
            // while genuinely different normals (hard edges, typically 30°+ apart) remain distinct.
            // This lets the welder merge truly duplicate vertices without averaging different smooth groups.
            RoundNormals(mesh, decimals: 3);
            var welder = new MeshWelder(mesh);
            welder.Weld();
            MeshUtils.CreateGOFromMesh(mesh, "FixedMesh");
            AssetDatabase.CreateAsset(mesh, filePath + ".asset");
            AssetDatabase.SaveAssets();
#endif
        }

        // Rounds each normal component to `decimals` decimal places and re-normalizes.
        // Precision of 3 (0.001) ≈ 0.06° — enough to make float-precision duplicates identical
        // while keeping genuinely different normals (hard edges, 30°+) distinct.
        private static void RoundNormals(Mesh mesh, int decimals = 3)
        {
            float factor = Mathf.Pow(10f, decimals);
            var ns = mesh.normals;
            for (int i = 0; i < ns.Length; i++)
            {
                ns[i] = new Vector3(
                    Mathf.Round(ns[i].x * factor) / factor,
                    Mathf.Round(ns[i].y * factor) / factor,
                    Mathf.Round(ns[i].z * factor) / factor).normalized;
            }
            mesh.normals = ns;
        }

        public void ImportAndRenderSTL()
        {
            var meshes = Importer.Import(sourcePath, CoordinateSpace.Left, UpAxis.Z);
            smr.sharedMesh = MeshUtils.CombineMeshes(meshes.ToList(), false);
            smr.sharedMesh.RecalculateBounds();
            smr.localBounds = smr.sharedMesh.bounds;
            mc.sharedMesh = smr.sharedMesh;
        }

        public void SimplifyAndBakeMesh()
        {
#if UNITY_EDITOR
            var mesh = new Mesh();
            MeshUtils.BuildMesh(mesh, smr.sharedMesh);
            NormalSolver.AdvancedRecalculateNormals(mesh, normalsAngleRecalculation);
            var welder = new MeshWelder(mesh);
            welder.Weld();
            Debug.Log(mesh.blendShapeCount);
            AssetDatabase.CreateAsset(mesh, filePath + ".asset");
            AssetDatabase.SaveAssets();
#endif
        }

        public void RecalculateNormals()
        {
            NormalSolver.AdvancedRecalculateNormals(smr.sharedMesh, normalsAngleRecalculation, true);
        }

        public void WeldNormals()
        {
            var welder = new MeshWelder(smr.sharedMesh);
            welder.Weld();
        }

        public void FlipTriangles()
        {
            smr.sharedMesh.triangles = smr.sharedMesh.triangles.Reverse().ToArray();
        }


#if UNITY_EDITOR
        [CustomEditor(typeof(EditorMeshUtils))]
        public class ObjectBuilderEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                EditorMeshUtils myScript = (EditorMeshUtils)target;
                if (GUILayout.Button("BuildMeshData"))
                {
                    myScript.BuildMeshData();
                }
                else if (GUILayout.Button("Update Mesh Collider"))
                {
                    myScript.UpdateMeshCollider();
                }
                else if (GUILayout.Button("Update Mesh Bounds"))
                {
                    myScript.UpdateBounds();
                }
                else if (GUILayout.Button("Modify Mesh Geometry"))
                {
                    myScript.ModifyMeshGeometry();
                }
                else if (GUILayout.Button("Reset to original geometry"))
                {
                    myScript.ResetMeshGeometry();
                }
                else if (GUILayout.Button("GetVerticesAt Y0"))
                {
                    myScript.GetVerticesAtY0();
                }
                else if (GUILayout.Button("Mesh as new asset"))
                {
                    myScript.MeshAsNewAsset();
                }
                else if (GUILayout.Button("Mesh as new asset copying specific shapes"))
                {
                    myScript.MeshAsNewAssetWithSpecificShapes();
                }
                else if (GUILayout.Button("Create All Section Variations With Blendshapes"))
                {
                    myScript.CreateAllSectionVariationsWithBlendshapes();
                }
                else if (GUILayout.Button("Bake Mesh as new asset"))
                {
                    myScript.BakeMeshAsNewAsset();
                }
                else if (GUILayout.Button("Simplify and bake mesh with normals as new asset"))
                {
                    myScript.SimplifyAndBakeMeshWithNormals();
                }
                else if (GUILayout.Button("Simplify and bake mesh as new asset"))
                {
                    myScript.SimplifyAndBakeMesh();
                }
                else if (GUILayout.Button("Bake Mesh From GPU As New Asset"))
                {
                    myScript.BakeMeshFromGPUAsNewAsset();
                }
                else if (GUILayout.Button("Create as new Prefab"))
                {
                    myScript.CreateAsNewPrefab();
                }
                else if (GUILayout.Button("ExportMesh"))
                {
                    myScript.ExportMesh();
                }
                else if (GUILayout.Button("Recalculate Normals"))
                {
                    myScript.RecalculateNormals();
                }
                else if (GUILayout.Button("Weld Normals"))
                {
                    myScript.WeldNormals();
                }
                else if (GUILayout.Button("Flip triangles"))
                {
                    myScript.FlipTriangles();
                }
                else if (GUILayout.Button("Import stl"))
                {
                    myScript.ImportAndRenderSTL();
                }
                else if (GUILayout.Button("Fix splitted normals and vertices"))
                {
                    myScript.FixSplittedNormalsAndVertices();
                }
            }
        }

#endif
    }
}