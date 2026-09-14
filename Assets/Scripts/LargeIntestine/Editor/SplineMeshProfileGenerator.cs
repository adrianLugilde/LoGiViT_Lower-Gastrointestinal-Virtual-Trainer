using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LargeIntestine
{
    /// <summary>
    /// Editor window for auto-generating mesh profiles from project assets
    /// </summary>
    public class SplineMeshProfileGenerator : EditorWindow
    {
        private Vector2 scrollPos;
        private List<MeshInfo> discoveredMeshes = new List<MeshInfo>();
        private string searchPath = "Assets";
        private bool autoDetectMaterials = true;
        private bool autoConfigureLinks = true;
        private const string profileOutputPath = "Assets/Resources/LI_SectionMeshProfiles";
        private bool includeEmptyMeshes = false;
        
        private class MeshInfo
        {
            public Mesh mesh;
            public string assetPath;
            public bool selected = true;
            public SplineMeshProfile.MeshType meshType = SplineMeshProfile.MeshType.Default;
            public SplineMeshProfile existingProfile;
            public Material[] materials;
        }
        
        [MenuItem("Tools/Large Intestine/Generate Mesh Profiles")]
        public static void ShowWindow()
        {
            var window = GetWindow<SplineMeshProfileGenerator>("Mesh Profile Generator");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Mesh Profile Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool scans your project for meshes with blendshapes and generates mesh profiles for them. " +
                "Mesh profiles make it easy to manage blendshape configurations and keep them in sync with your meshes.",
                MessageType.Info
            );
            
            EditorGUILayout.Space(10);
            
            // Settings
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            searchPath = EditorGUILayout.TextField("Search Path", searchPath);
            EditorGUILayout.LabelField("Output Path", profileOutputPath);
            EditorGUILayout.HelpBox("Profiles will be saved to: " + profileOutputPath, MessageType.Info);
            autoDetectMaterials = EditorGUILayout.Toggle("Auto-detect Materials", autoDetectMaterials);
            autoConfigureLinks = EditorGUILayout.Toggle("Auto-configure Blendshape Links", autoConfigureLinks);
            if (autoConfigureLinks)
            {
                EditorGUILayout.HelpBox("Will automatically link _inf blendshapes to _sup on adjacent sections.", MessageType.None);
            }
            includeEmptyMeshes = EditorGUILayout.Toggle("Include Meshes Without Blendshapes", includeEmptyMeshes);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Action Buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Scan for Meshes", GUILayout.Height(30)))
            {
                ScanForMeshes();
            }
            
            GUI.enabled = discoveredMeshes.Count > 0;
            if (GUILayout.Button($"Generate Selected ({discoveredMeshes.Count(m => m.selected)})", GUILayout.Height(30)))
            {
                GenerateProfiles();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Results
            if (discoveredMeshes.Count > 0)
            {
                EditorGUILayout.LabelField($"Discovered Meshes ({discoveredMeshes.Count})", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", GUILayout.Width(100)))
                {
                    foreach (var mesh in discoveredMeshes)
                        mesh.selected = true;
                }
                if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
                {
                    foreach (var mesh in discoveredMeshes)
                        mesh.selected = false;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                
                foreach (var meshInfo in discoveredMeshes)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    meshInfo.selected = EditorGUILayout.Toggle(meshInfo.selected, GUILayout.Width(20));
                    
                    EditorGUILayout.LabelField(meshInfo.mesh.name, EditorStyles.boldLabel, GUILayout.Width(200));
                    
                    EditorGUILayout.LabelField($"{meshInfo.mesh.blendShapeCount} blendshapes", GUILayout.Width(120));
                    
                    if (meshInfo.existingProfile != null)
                    {
                        GUI.color = Color.yellow;
                        EditorGUILayout.LabelField("(Update)", GUILayout.Width(60));
                        GUI.color = Color.white;
                    }
                    else
                    {
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("(New)", GUILayout.Width(60));
                        GUI.color = Color.white;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUI.indentLevel++;
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Type:", GUILayout.Width(80));
                    meshInfo.meshType = (SplineMeshProfile.MeshType)EditorGUILayout.EnumPopup(meshInfo.meshType);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Path:", GUILayout.Width(80));
                    EditorGUILayout.LabelField(meshInfo.assetPath, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                    
                    if (meshInfo.materials != null && meshInfo.materials.Length > 0)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Materials:", GUILayout.Width(80));
                        EditorGUILayout.LabelField(string.Join(", ", meshInfo.materials.Select(m => m?.name ?? "null")), EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUI.indentLevel--;
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
                
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No meshes discovered yet. Click 'Scan for Meshes' to begin.", MessageType.Info);
            }
        }
        
        private void ScanForMeshes()
        {
            discoveredMeshes.Clear();
            
            // Find all mesh assets in the project
            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { searchPath });
            
            int scanned = 0;
            int withBlendshapes = 0;
            
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                
                if (mesh == null)
                    continue;
                
                scanned++;
                
                // Skip meshes without blendshapes unless includeEmptyMeshes is true
                if (mesh.blendShapeCount == 0 && !includeEmptyMeshes)
                    continue;
                
                withBlendshapes++;
                
                var meshInfo = new MeshInfo
                {
                    mesh = mesh,
                    assetPath = assetPath,
                    meshType = DetectMeshType(mesh.name)
                };
                
                // Check for existing profile
                meshInfo.existingProfile = FindExistingProfile(mesh);
                
                // Auto-detect materials if enabled
                if (autoDetectMaterials)
                {
                    meshInfo.materials = FindMaterialsForMesh(mesh);
                }
                
                discoveredMeshes.Add(meshInfo);
            }
            
            Debug.Log($"Scanned {scanned} meshes, found {withBlendshapes} with blendshapes.");
        }
        
        private SplineMeshProfile.MeshType DetectMeshType(string meshName)
        {
            string lowerName = meshName.ToLower();
            
            if (lowerName.Contains("rectum"))
                return SplineMeshProfile.MeshType.Rectum;
            
            if (lowerName.Contains("cecum"))
                return SplineMeshProfile.MeshType.Cecum;
            
            if (lowerName.Contains("default"))
                return SplineMeshProfile.MeshType.Default;
            
            return SplineMeshProfile.MeshType.Default;
        }
        
        private SplineMeshProfile FindExistingProfile(Mesh mesh)
        {
            string[] guids = AssetDatabase.FindAssets("t:SplineMeshProfile");
            
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                SplineMeshProfile profile = AssetDatabase.LoadAssetAtPath<SplineMeshProfile>(assetPath);
                
                if (profile != null && profile.mesh == mesh)
                {
                    return profile;
                }
            }
            
            return null;
        }
        
        private Material[] FindMaterialsForMesh(Mesh mesh)
        {
            // Try to find prefabs or scene objects using this mesh
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            
            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                
                if (prefab == null)
                    continue;
                
                var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in meshFilters)
                {
                    if (mf.sharedMesh == mesh)
                    {
                        var renderer = mf.GetComponent<MeshRenderer>();
                        if (renderer != null && renderer.sharedMaterials.Length > 0)
                        {
                            return renderer.sharedMaterials;
                        }
                    }
                }
                
                var skinnedMeshRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in skinnedMeshRenderers)
                {
                    if (smr.sharedMesh == mesh)
                    {
                        if (smr.sharedMaterials.Length > 0)
                        {
                            return smr.sharedMaterials;
                        }
                    }
                }
            }
            
            return null;
        }
        
        private void GenerateProfiles()
        {
            if (!Directory.Exists(profileOutputPath))
            {
                Directory.CreateDirectory(profileOutputPath);
                AssetDatabase.Refresh();
            }
            
            int created = 0;
            int updated = 0;
            
            foreach (var meshInfo in discoveredMeshes)
            {
                if (!meshInfo.selected)
                    continue;
                
                SplineMeshProfile profile = meshInfo.existingProfile;
                
                if (profile == null)
                {
                    // Create new profile
                    profile = ScriptableObject.CreateInstance<SplineMeshProfile>();
                    string assetPath = Path.Combine(profileOutputPath, $"{meshInfo.mesh.name}_Profile.asset");
                    AssetDatabase.CreateAsset(profile, assetPath);
                    created++;
                }
                else
                {
                    updated++;
                }
                
                // Configure profile
                profile.mesh = meshInfo.mesh;
                profile.meshType = meshInfo.meshType;
                
                if (meshInfo.materials != null && meshInfo.materials.Length > 0)
                {
                    profile.defaultMaterials = meshInfo.materials;
                }
                
                // Refresh blendshapes
                profile.RefreshFromMesh();
                
                // Auto-configure blendshape links if enabled
                if (autoConfigureLinks)
                {
                    AutoConfigureBlendshapeLinks(profile);
                }
                
                EditorUtility.SetDirty(profile);
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog(
                "Profile Generation Complete",
                $"Created {created} new profiles and updated {updated} existing profiles.\n\nProfiles saved to: {profileOutputPath}",
                "OK"
            );
            
            Debug.Log($"Generated mesh profiles: {created} created, {updated} updated.");
        }
        
        /// <summary>
        /// Automatically configures links between _inf and _sup blendshape pairs
        /// </summary>
        private void AutoConfigureBlendshapeLinks(SplineMeshProfile profile)
        {
            if (profile.blendshapes == null)
                return;
            
            foreach (var bs in profile.blendshapes)
            {
                string bsNameLower = bs.name.ToLower();

                if (bsNameLower.Contains("_inf"))
                {
                    string linkedName = ReplaceTokenIgnoreCase(bs.name, "_inf", "_Sup");

                    var linkedBs = profile.blendshapes.FirstOrDefault(b =>
                        StripDiacritics(b.name).Equals(StripDiacritics(linkedName), System.StringComparison.OrdinalIgnoreCase));

                    if (linkedBs != null)
                    {
                        bs.links.Add(new SplineMeshProfile.BlendshapeLinkConfig
                        {
                            direction = SplineMeshProfile.LinkDirection.Previous,
                            linkedBlendshapeName = linkedBs.name,
                            linkScale = 1f
                        });
                    }
                }
                else if (bsNameLower.Contains("_sup"))
                {
                    string linkedName = ReplaceTokenIgnoreCase(bs.name, "_sup", "_Inf");

                    var linkedBs = profile.blendshapes.FirstOrDefault(b =>
                        StripDiacritics(b.name).Equals(StripDiacritics(linkedName), System.StringComparison.OrdinalIgnoreCase));

                    if (linkedBs != null)
                    {
                        bs.links.Add(new SplineMeshProfile.BlendshapeLinkConfig
                        {
                            direction = SplineMeshProfile.LinkDirection.Next,
                            linkedBlendshapeName = linkedBs.name,
                            linkScale = 1f
                        });
                    }
                }
            }
        }

        private static string StripDiacritics(string text)
        {
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(normalized.Length);
            foreach (char c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                    System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private static string ReplaceTokenIgnoreCase(string source, string oldToken, string newToken)
        {
            int idx = source.IndexOf(oldToken, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return source;
            return source[..idx] + newToken + source[(idx + oldToken.Length)..];
        }
    }
}
