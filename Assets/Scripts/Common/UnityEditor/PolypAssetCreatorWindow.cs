#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class PolypAssetCreatorWindow : EditorWindow
{
    private const string ProfilesFolder = "Assets/LargeIntestine/PolypProfiles";

    // Left panel — profile list
    private List<PolypProfile> _profiles = new List<PolypProfile>();
    private Vector2 _listScroll;
    private PolypProfile _selectedProfile;

    // Right panel — editable fields
    private string _name;
    private string _description;
    private Polyp.JNETClassification _jnetClass;
    private int _jnetClassIndex;
    private int _parisClassIndex;
    private Polyp.ParisClassification _parisClass;
    private Mesh _mesh;
    private Material[] _materials = new Material[1];
    private Sprite _meshSprite;
    private Texture _image;
    private Vector2 _fieldsScroll;

    private bool _isDirty;

    [MenuItem("Tools/Large Intestine/Polyp Profile Builder")]
    public static void ShowWindow()
    {
        GetWindow<PolypAssetCreatorWindow>("Polyp Profile Builder");
    }

    private void OnEnable()
    {
        RefreshProfileList();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawDivider();
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();
    }

    // ── Left panel ────────────────────────────────────────────────────────────

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(180));
        GUILayout.Label("Polyp Profiles", EditorStyles.boldLabel);

        if (GUILayout.Button("+ New Profile"))
            StartNewProfile();

        if (GUILayout.Button("Refresh"))
            RefreshProfileList();

        GUILayout.Space(4);

        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        foreach (var profile in _profiles)
        {
            bool isSelected = profile == _selectedProfile;
            var style = isSelected ? EditorStyles.selectionRect : EditorStyles.label;

            if (GUILayout.Toggle(isSelected, profile.displayName, style) && !isSelected)
                SelectProfile(profile);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ── Right panel ───────────────────────────────────────────────────────────

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();

        bool isEditing = _selectedProfile != null;
        GUILayout.Label(isEditing ? $"Editing: {_selectedProfile.displayName}" : "New Profile", EditorStyles.boldLabel);

        if (isEditing && !string.IsNullOrEmpty(_selectedProfile.Id))
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("ID", _selectedProfile.Id);
            EditorGUI.EndDisabledGroup();
        }

        GUILayout.Space(4);
        _fieldsScroll = EditorGUILayout.BeginScrollView(_fieldsScroll);

        EditorGUI.BeginChangeCheck();

        _name        = EditorGUILayout.TextField("Name", _name);
        _description = EditorGUILayout.TextField("Description", _description);
        _jnetClassIndex = EditorGUILayout.Popup("JNET Classification", _jnetClassIndex, Polyp.GetJnetClassificationStrings().ToArray());
        _jnetClass = (Polyp.JNETClassification)_jnetClassIndex;

        _parisClassIndex = EditorGUILayout.Popup("Paris Classification", _parisClassIndex, Polyp.GetParisClassificationStrings().ToArray());
        _parisClass = (Polyp.ParisClassification)_parisClassIndex;

        _mesh = (Mesh)EditorGUILayout.ObjectField("Mesh", _mesh, typeof(Mesh), false);

        int materialsCount = EditorGUILayout.IntField("Number of Materials", _materials.Length);
        if (materialsCount != _materials.Length)
        {
            var newMaterials = new Material[Mathf.Max(1, materialsCount)];
            for (int i = 0; i < Mathf.Min(materialsCount, _materials.Length); i++)
                newMaterials[i] = _materials[i];
            _materials = newMaterials;
        }

        for (int i = 0; i < _materials.Length; i++)
            _materials[i] = (Material)EditorGUILayout.ObjectField($"Material {i + 1}", _materials[i], typeof(Material), false);

        _meshSprite = (Sprite)EditorGUILayout.ObjectField("Mesh Sprite", _meshSprite, typeof(Sprite), false);
        _image      = (Texture)EditorGUILayout.ObjectField("Polyp Image", _image, typeof(Texture), false);

        if (EditorGUI.EndChangeCheck())
            _isDirty = true;

        EditorGUILayout.EndScrollView();

        GUILayout.Space(8);
        DrawActionButtons();
        EditorGUILayout.EndVertical();
    }

    private void DrawActionButtons()
    {
        var errors = GetValidationErrors();
        bool isValid = errors.Count == 0;

        if (!isValid)
        {
            EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Warning);
        }

        EditorGUILayout.BeginHorizontal();

        if (_selectedProfile != null)
        {
            EditorGUI.BeginDisabledGroup(!_isDirty || !isValid);
            if (GUILayout.Button("Save Changes"))
                SaveChangesToSelected();
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Discard"))
                SelectProfile(_selectedProfile);

            GUILayout.FlexibleSpace();

            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("Delete", GUILayout.Width(70)))
                DeleteSelected();
            GUI.color = Color.white;
        }
        else
        {
            EditorGUI.BeginDisabledGroup(!isValid);
            if (GUILayout.Button("Create Profile"))
                CreateNewProfile();
            EditorGUI.EndDisabledGroup();
        }

        EditorGUILayout.EndHorizontal();
    }

    private List<string> GetValidationErrors()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(_name))
            errors.Add("Name is required.");
        if (_mesh == null)
            errors.Add("Mesh is required.");
        if (_materials == null || _materials.Length == 0)
            errors.Add("At least one material is required.");
        else
        {
            for (int i = 0; i < _materials.Length; i++)
                if (_materials[i] == null)
                    errors.Add($"Material {i + 1} is not assigned.");
        }
        if (_meshSprite == null)
            errors.Add("Mesh Sprite is required.");
        if (_image == null)
            errors.Add("Polyp Image is required.");
        return errors;
    }

    private static void DrawDivider()
    {
        var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void RefreshProfileList()
    {
        _profiles.Clear();
        var guids = AssetDatabase.FindAssets("t:PolypProfile", new[] { ProfilesFolder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var profile = AssetDatabase.LoadAssetAtPath<PolypProfile>(path);
            if (profile != null)
                _profiles.Add(profile);
        }
        _profiles = _profiles.OrderBy(p => p.displayName).ToList();

        if (_selectedProfile != null && !_profiles.Contains(_selectedProfile))
        {
            _selectedProfile = null;
            ClearFields();
        }
    }

    private void SelectProfile(PolypProfile profile)
    {
        _selectedProfile = profile;
        _name            = profile.displayName;
        _description     = profile.description;
        _jnetClass       = profile.jnetClass;
        _jnetClassIndex  = (int)profile.jnetClass;
        _parisClass      = profile.parisClass;
        _parisClassIndex = (int)profile.parisClass;
        _mesh            = profile.mesh;
        _materials       = profile.materials != null ? (Material[])profile.materials.Clone() : new Material[1];
        _meshSprite      = profile.meshSprite;
        _image           = profile.image;
        _isDirty         = false;
    }

    private void StartNewProfile()
    {
        _selectedProfile = null;
        ClearFields();
    }

    private void ClearFields()
    {
        _name            = "New Polyp";
        _description     = "";
        _jnetClass       = Polyp.JNETClassification.Class1;
        _jnetClassIndex  = 0;
        _parisClass      = Polyp.ParisClassification.Ip;
        _parisClassIndex = 0;
        _mesh            = null;
        _materials       = new Material[1];
        _meshSprite      = null;
        _image           = null;
        _isDirty         = false;
    }

    private void CreateNewProfile()
    {
        EnsureFolder();

        var profile = ScriptableObject.CreateInstance<PolypProfile>();
        ApplyFieldsTo(profile);

        var assetPath = Path.Combine(ProfilesFolder, _name + ".asset").Replace('\\', '/');
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
        AssetDatabase.CreateAsset(profile, assetPath);
        AssetDatabase.SaveAssets();

        GetCatalog()?.Add(profile);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PolypProfileBuilder] Created '{_name}' at {assetPath} (id: {profile.Id})");

        RefreshProfileList();
        SelectProfile(profile);

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = profile;
    }

    private void SaveChangesToSelected()
    {
        ApplyFieldsTo(_selectedProfile);

        var oldPath = AssetDatabase.GetAssetPath(_selectedProfile);
        var newPath = Path.Combine(ProfilesFolder, _name + ".asset").Replace('\\', '/');
        if (oldPath != newPath)
        {
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
            AssetDatabase.MoveAsset(oldPath, newPath);
        }

        EditorUtility.SetDirty(_selectedProfile);
        AssetDatabase.SaveAssets();

        _isDirty = false;
        RefreshProfileList();
        Debug.Log($"[PolypProfileBuilder] Saved '{_name}'");
    }

    private void DeleteSelected()
    {
        if (!EditorUtility.DisplayDialog(
            "Delete Profile",
            $"Delete '{_selectedProfile.displayName}'? This cannot be undone.",
            "Delete", "Cancel"))
            return;

        GetCatalog()?.Remove(_selectedProfile);
        AssetDatabase.SaveAssets();

        var path = AssetDatabase.GetAssetPath(_selectedProfile);
        _selectedProfile = null;
        ClearFields();
        AssetDatabase.DeleteAsset(path);
        RefreshProfileList();
    }

    private static PolypProfileCatalog GetCatalog()
    {
        var catalog = PolypProfileCatalog.Load();
        if (catalog == null)
            Debug.LogWarning("[PolypProfileBuilder] PolypProfileCatalog not found in Resources — profile not registered.");
        return catalog;
    }

    private void ApplyFieldsTo(PolypProfile profile)
    {
        profile.displayName = _name;
        profile.description = _description;
        profile.jnetClass   = _jnetClass;
        profile.parisClass  = _parisClass;
        profile.mesh        = _mesh;
        profile.materials   = _materials;
        profile.meshSprite  = _meshSprite;
        profile.image       = _image;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/LargeIntestine"))
            AssetDatabase.CreateFolder("Assets", "LargeIntestine");
        if (!AssetDatabase.IsValidFolder(ProfilesFolder))
            AssetDatabase.CreateFolder("Assets/LargeIntestine", "PolypProfiles");
    }
}
#endif
