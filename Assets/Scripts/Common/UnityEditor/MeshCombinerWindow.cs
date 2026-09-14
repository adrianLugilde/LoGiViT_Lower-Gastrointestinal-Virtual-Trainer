#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryUtils;
using UnityEditor;
using UnityEngine;

public class MeshCombinerWindow : EditorWindow
{
    private List<GameObject> _sourceObjects = new List<GameObject>();
    private bool _createSubmeshes = true;
    private string _savePath = "Assets/";
    private string _meshName = "CombinedMesh";
    private Vector2 _scroll;

    [MenuItem("Tools/Mesh Combiner")]
    public static void ShowWindow()
    {
        GetWindow<MeshCombinerWindow>("Mesh Combiner");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Mesh Combiner", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        DrawSourceList();

        EditorGUILayout.Space(6);
        _createSubmeshes = EditorGUILayout.Toggle("Preserve Submeshes", _createSubmeshes);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Save Folder");
        _savePath = EditorGUILayout.TextField(_savePath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            var chosen = EditorUtility.OpenFolderPanel("Save folder", _savePath, "");
            if (!string.IsNullOrEmpty(chosen))
                _savePath = FileUtil.GetProjectRelativePath(chosen);
        }
        EditorGUILayout.EndHorizontal();

        _meshName = EditorGUILayout.TextField("Asset Name", _meshName);

        EditorGUILayout.Space(8);

        var validSources = GetValidSources();
        using (new EditorGUI.DisabledScope(validSources.Count < 2))
        {
            if (GUILayout.Button("Bake & Combine", GUILayout.Height(30)))
                BakeAndCombine(validSources);
        }

        if (validSources.Count < 2)
            EditorGUILayout.HelpBox("Add at least 2 GameObjects with a MeshFilter or SkinnedMeshRenderer.", MessageType.Info);
    }

    private void DrawSourceList()
    {
        EditorGUILayout.LabelField("Source Objects (scene)", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(200));

        for (int i = 0; i < _sourceObjects.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            var prev = _sourceObjects[i];
            _sourceObjects[i] = (GameObject)EditorGUILayout.ObjectField(_sourceObjects[i], typeof(GameObject), true);

            // Show a warning icon if the assigned object has no usable renderer
            if (_sourceObjects[i] != null && !HasMeshSource(_sourceObjects[i]))
                EditorGUILayout.LabelField(EditorGUIUtility.IconContent("console.warnicon.sml"), GUILayout.Width(20));

            if (GUILayout.Button("-", GUILayout.Width(22)))
            {
                _sourceObjects.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Slot"))
            _sourceObjects.Add(null);
        if (GUILayout.Button("Add Selection"))
            AddSelectionObjects();
        if (GUILayout.Button("Clear All"))
            _sourceObjects.Clear();
        EditorGUILayout.EndHorizontal();

        // Drag-and-drop onto the list area
        var dropRect = GUILayoutUtility.GetLastRect();
        HandleDragAndDrop(dropRect);
    }

    private void AddSelectionObjects()
    {
        foreach (var go in Selection.gameObjects)
        {
            if (!_sourceObjects.Contains(go))
                _sourceObjects.Add(go);
        }
    }

    private void HandleDragAndDrop(Rect area)
    {
        var evt = Event.current;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            return;
        if (!area.Contains(evt.mousePosition))
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go && !_sourceObjects.Contains(go))
                    _sourceObjects.Add(go);
            }
            evt.Use();
        }
    }

    // --- helpers ---

    private static bool HasMeshSource(GameObject go)
    {
        return go.GetComponent<MeshFilter>() != null || go.GetComponent<SkinnedMeshRenderer>() != null;
    }

    private List<GameObject> GetValidSources()
    {
        return _sourceObjects.Where(go => go != null && HasMeshSource(go)).ToList();
    }

    /// <summary>
    /// Bakes mesh from a scene object, applying its world transform to vertices/normals
    /// so that the combined mesh lives in world space.
    /// </summary>
    private static Mesh BakeMeshWithTransform(GameObject go)
    {
        Mesh sourceMesh = null;
        bool needsTransformApplied = true;

        var smr = go.GetComponent<SkinnedMeshRenderer>();
        if (smr != null)
        {
            // BakeMesh captures blendshape/bone deformations in local space
            sourceMesh = new Mesh();
            smr.BakeMesh(sourceMesh);
        }
        else
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                // Deep copy so we don't modify the original asset
                sourceMesh = Object.Instantiate(mf.sharedMesh);
            }
        }

        if (sourceMesh == null)
            return null;

        ApplyTransformToMesh(sourceMesh, go.transform.localToWorldMatrix);
        return sourceMesh;
    }

    /// <summary>
    /// Transforms all vertices and normals of <paramref name="mesh"/> by <paramref name="matrix"/>
    /// (world transform). Tangents are rebuilt after combining.
    /// </summary>
    private static void ApplyTransformToMesh(Mesh mesh, Matrix4x4 matrix)
    {
        // Normal matrix = inverse transpose of the upper-left 3x3 (handles non-uniform scale)
        Matrix4x4 normalMatrix = matrix.inverse.transpose;

        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var tangents = mesh.tangents;

        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);

        for (int i = 0; i < normals.Length; i++)
            normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;

        // Tangent xyz is a direction; w encodes handedness — preserve w
        for (int i = 0; i < tangents.Length; i++)
        {
            var t = tangents[i];
            var transformed = normalMatrix.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
            tangents[i] = new Vector4(transformed.x, transformed.y, transformed.z, t.w);
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.RecalculateBounds();
    }

    private void BakeAndCombine(List<GameObject> sources)
    {
        var bakedMeshes = new List<Mesh>();
        foreach (var go in sources)
        {
            var baked = BakeMeshWithTransform(go);
            if (baked != null)
                bakedMeshes.Add(baked);
            else
                Debug.LogWarning($"[MeshCombiner] Could not bake mesh from '{go.name}' — skipped.");
        }

        if (bakedMeshes.Count < 2)
        {
            EditorUtility.DisplayDialog("Mesh Combiner", "Not enough valid meshes after baking.", "OK");
            CleanupTempMeshes(bakedMeshes);
            return;
        }

        var combined = MeshUtils.CombineMeshes(bakedMeshes, _createSubmeshes);
        CleanupTempMeshes(bakedMeshes);

        if (string.IsNullOrWhiteSpace(_savePath) || !Directory.Exists(_savePath))
        {
            EditorUtility.DisplayDialog("Invalid Path", $"Save folder does not exist:\n{_savePath}", "OK");
            return;
        }

        var name = string.IsNullOrWhiteSpace(_meshName) ? "CombinedMesh" : _meshName;
        var assetPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(_savePath, name + ".asset").Replace("\\", "/"));

        AssetDatabase.CreateAsset(combined, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(combined);
        Debug.Log($"[MeshCombiner] Saved '{assetPath}' — {combined.vertexCount} verts, {combined.subMeshCount} submeshes");
    }

    private static void CleanupTempMeshes(List<Mesh> meshes)
    {
        foreach (var m in meshes)
            Object.DestroyImmediate(m);
    }
}
#endif
