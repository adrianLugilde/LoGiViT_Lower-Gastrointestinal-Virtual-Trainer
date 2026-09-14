using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LargeIntestine
{
    /// <summary>
    /// Visual editor window for creating and editing large intestine configurations
    /// </summary>
    public class LI_ConfigurationBuilderWindow : EditorWindow
    {
        private LI_GenerationConfiguration config;
        private Vector2 scrollPos;
        private Vector2 presetScrollPos;
        private string configPath = "";

        private LI_Segment? selectedSegment = null;
        private int selectedSectionIndex = -1;

        private LI_BlendshapePreset selectedPreset;
        private bool showPresets = false;
        private List<LI_BlendshapePreset> availablePresets = new List<LI_BlendshapePreset>();

        private bool showValidation = false;
        private ValidationResult lastValidation;

        // Global mesh profile for batch application
        private SplineMeshProfile globalMeshProfile;
        private List<SplineMeshProfile> availableMeshProfiles = new List<SplineMeshProfile>();

        // Per-segment mesh profile (transient, not serialized)
        private Dictionary<LI_Segment, SplineMeshProfile> _segmentMeshProfiles = new Dictionary<LI_Segment, SplineMeshProfile>();
        
        // Global materials for batch application
        private List<Material> globalMaterialOverrides = new List<Material>();
        private bool showGlobalMaterials = false;

        [MenuItem("Tools/Large Intestine/Configuration Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<LI_ConfigurationBuilderWindow>("Configuration Builder");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshPresetList();
            RefreshMeshProfileList();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Large Intestine Configuration Builder", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            // Top toolbar
            DrawToolbar();

            EditorGUILayout.Space(10);

            if (config == null)
            {
                DrawNoConfigurationView();
            }
            else
            {
                DrawConfigurationEditor();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                CreateNewConfiguration();
            }

            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                LoadConfiguration();
            }

            GUI.enabled = config != null;
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                SaveConfiguration();
            }

            if (GUILayout.Button("Save As", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                SaveConfigurationAs();
            }

            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ValidateConfiguration();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh Presets", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                RefreshPresetList();
            }

            if (GUILayout.Button("Refresh Profiles", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                RefreshMeshProfileList();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoConfigurationView()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(20);

            EditorGUILayout.LabelField("No configuration loaded", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Create New Configuration", GUILayout.Width(200), GUILayout.Height(30)))
            {
                CreateNewConfiguration();
            }

            GUILayout.Space(20);

            if (GUILayout.Button("Load Existing Configuration", GUILayout.Width(200), GUILayout.Height(30)))
            {
                LoadConfiguration();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);
            EditorGUILayout.EndVertical();
        }

        private void DrawConfigurationEditor()
        {
            // Global settings at the top
            DrawGlobalSettings();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();

            // Left panel - Segments tree
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            DrawSegmentsList();
            EditorGUILayout.EndVertical();

            // Right panel - Section editor
            EditorGUILayout.BeginVertical();
            DrawSectionEditor();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawGlobalSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Global Configuration Settings", EditorStyles.boldLabel);
            
            // Get shared blendshapes
            var sharedBlendshapes = config.GetSharedBlendshapeNames();
            
            if (sharedBlendshapes.Count == 0)
            {
                EditorGUILayout.HelpBox("Add sections with mesh profiles to configure global blendshape settings.", MessageType.Info);
            }
            else
            {
                // Global Width Blendshape dropdown
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Global Width Blendshape", GUILayout.Width(180));
                
                // Create dropdown options
                var options = new List<string> { "(None)" };
                options.AddRange(sharedBlendshapes);
                
                int currentIndex = 0;
                if (!string.IsNullOrEmpty(config.globalWidthBlendshapeName))
                {
                    int foundIndex = options.IndexOf(config.globalWidthBlendshapeName);
                    if (foundIndex >= 0)
                        currentIndex = foundIndex;
                }
                
                int newIndex = EditorGUILayout.Popup(currentIndex, options.ToArray());
                
                if (newIndex != currentIndex)
                {
                    config.globalWidthBlendshapeName = newIndex == 0 ? "" : options[newIndex];
                }
                
                EditorGUILayout.EndHorizontal();
                
                // Validation indicator
                if (!string.IsNullOrEmpty(config.globalWidthBlendshapeName))
                {
                    if (config.IsGlobalWidthBlendshapeValid())
                    {
                        EditorGUILayout.HelpBox($"'{config.globalWidthBlendshapeName}' is available in all {config.GetUsedMeshProfiles().Count} mesh profiles.", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox($"'{config.globalWidthBlendshapeName}' is NOT available in all mesh profiles!", MessageType.Warning);
                    }
                }
            }
            
            EditorGUILayout.Space(10);
            
            // Global Mesh Profile section
            DrawGlobalMeshProfileSection();
            
            EditorGUILayout.Space(10);
            
            // Global Materials section
            DrawGlobalMaterialsSection();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSegmentProfileSection(LI_SegmentConfiguration segment)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{segment.liSegment} — Segment Profile", EditorStyles.boldLabel);

            if (availableMeshProfiles.Count == 0)
            {
                EditorGUILayout.LabelField("No mesh profiles found. Click 'Refresh Profiles' in the toolbar.");
                EditorGUILayout.EndVertical();
                return;
            }

            _segmentMeshProfiles.TryGetValue(segment.liSegment, out var currentProfile);

            EditorGUILayout.BeginHorizontal();

            // Profile dropdown
            var profileNames = new List<string> { "(Select Profile)" };
            profileNames.AddRange(availableMeshProfiles.Select(p => p.name));

            int currentIndex = currentProfile != null ? availableMeshProfiles.IndexOf(currentProfile) + 1 : 0;
            int newIndex = EditorGUILayout.Popup(currentIndex, profileNames.ToArray(), GUILayout.Width(220));

            if (newIndex != currentIndex)
                _segmentMeshProfiles[segment.liSegment] = newIndex == 0 ? null : availableMeshProfiles[newIndex - 1];

            // Apply button
            GUI.enabled = currentProfile != null && segment.liSections != null && segment.liSections.Count > 0;
            if (GUILayout.Button($"Apply to {segment.liSegment}", GUILayout.Width(160)))
                ApplyProfileToSegment(segment, currentProfile);
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            if (currentProfile != null)
                EditorGUILayout.LabelField($"   {currentProfile.blendshapes?.Count ?? 0} blendshapes  |  UV channel {currentProfile.uvChannelIndex}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void ApplyProfileToSegment(LI_SegmentConfiguration segment, SplineMeshProfile profile)
        {
            if (profile == null || segment.liSections == null || segment.liSections.Count == 0)
                return;

            bool hasExisting = segment.liSections.Any(s => s.meshProfile != null);
            bool importMatching = false;

            if (hasExisting)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    $"Apply Profile to {segment.liSegment}",
                    $"Apply '{profile.name}' to all {segment.liSections.Count} section(s) in {segment.liSegment}.\n\n" +
                    "• Import Matching: preserve blendshape values shared with the old profile\n" +
                    "• Reset to Defaults: use the new profile's default values",
                    "Import Matching", "Cancel", "Reset to Defaults");

                if (choice == 1) return;
                importMatching = choice == 0;
            }
            else
            {
                if (!EditorUtility.DisplayDialog(
                    $"Apply Profile to {segment.liSegment}",
                    $"Apply '{profile.name}' to all {segment.liSections.Count} section(s) in {segment.liSegment}?",
                    "Apply", "Cancel"))
                    return;
            }

            foreach (var section in segment.liSections)
                section.InitializeFromProfile(profile, importMatching);

            Debug.Log($"[ConfigurationBuilder] Applied '{profile.name}' to {segment.liSections.Count} section(s) in {segment.liSegment}.");
            Repaint();
        }

        private void DrawGlobalMeshProfileSection()
        {
            EditorGUILayout.LabelField("Global Mesh Profile", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select a mesh profile to apply to all sections in all segments.", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            
            // Dropdown for available mesh profiles
            if (availableMeshProfiles.Count == 0)
            {
                EditorGUILayout.LabelField("No mesh profiles found. Click 'Refresh Profiles' in the toolbar.");
            }
            else
            {
                // Create dropdown options
                var profileNames = new List<string> { "(Select Profile)" };
                profileNames.AddRange(availableMeshProfiles.Select(p => p.name));
                
                int currentIndex = 0;
                if (globalMeshProfile != null)
                {
                    int foundIndex = availableMeshProfiles.IndexOf(globalMeshProfile);
                    if (foundIndex >= 0)
                        currentIndex = foundIndex + 1; // +1 for the "(Select Profile)" entry
                }
                
                int newIndex = EditorGUILayout.Popup(currentIndex, profileNames.ToArray(), GUILayout.Width(250));
                
                if (newIndex != currentIndex)
                {
                    globalMeshProfile = newIndex == 0 ? null : availableMeshProfiles[newIndex - 1];
                }
                
                // Apply button
                GUI.enabled = globalMeshProfile != null;
                if (GUILayout.Button("Apply to All Sections", GUILayout.Width(150)))
                {
                    ApplyGlobalMeshProfileToAllSections();
                }
                GUI.enabled = true;
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Show currently selected profile info
            if (globalMeshProfile != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Selected:", GUILayout.Width(60));
                EditorGUILayout.ObjectField(globalMeshProfile, typeof(SplineMeshProfile), false);
                EditorGUILayout.EndHorizontal();
                
                if (globalMeshProfile.blendshapes != null)
                {
                    EditorGUILayout.LabelField($"   Contains {globalMeshProfile.blendshapes.Count} blendshapes", EditorStyles.miniLabel);
                }
            }
        }
        
        private void ApplyGlobalMeshProfileToAllSections()
        {
            if (globalMeshProfile == null || config == null)
                return;
            
            int totalSections = 0;
            foreach (var segment in config.intestineSegments)
            {
                if (segment.liSections != null)
                    totalSections += segment.liSections.Count;
            }
            
            if (totalSections == 0)
            {
                EditorUtility.DisplayDialog("No Sections", "There are no sections to apply the mesh profile to.\n\nAdd segments and sections first.", "OK");
                return;
            }
            
            // Check if sections already have profiles
            bool hasExistingProfiles = false;
            foreach (var segment in config.intestineSegments)
            {
                if (segment.liSections != null)
                {
                    foreach (var section in segment.liSections)
                    {
                        if (section.meshProfile != null)
                        {
                            hasExistingProfiles = true;
                            break;
                        }
                    }
                    if (hasExistingProfiles) break;
                }
            }
            
            // Determine import option
            bool importMatchingBlendshapes = false;
            if (hasExistingProfiles)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Apply Global Mesh Profile",
                    $"This will set '{globalMeshProfile.name}' as the mesh profile for ALL {totalSections} section(s).\n\n" +
                    "Sections already have mesh profiles assigned. How should blendshapes be handled?\n\n" +
                    "• Import Matching: Try to preserve blendshape values that exist in both old and new profiles\n" +
                    "• Reset to Defaults: Initialize all blendshapes with new profile's default values",
                    "Import Matching",
                    "Cancel",
                    "Reset to Defaults");
                
                if (choice == 1) // Cancel
                    return;
                
                importMatchingBlendshapes = (choice == 0); // Import Matching
            }
            else
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "Apply Global Mesh Profile",
                    $"This will set '{globalMeshProfile.name}' as the mesh profile for ALL {totalSections} section(s) across all segments.\n\n" +
                    "This will also initialize blendshape values from the profile defaults.\n\n" +
                    "Are you sure?",
                    "Apply",
                    "Cancel");
                
                if (!confirm)
                    return;
            }
            
            int appliedCount = 0;
            int importedCount = 0;
            foreach (var segment in config.intestineSegments)
            {
                if (segment.liSections == null)
                    continue;
                
                foreach (var section in segment.liSections)
                {
                    bool imported = section.InitializeFromProfile(globalMeshProfile, importMatchingBlendshapes);
                    if (imported)
                        importedCount++;
                    appliedCount++;
                }
            }
            
            string message = $"Applied '{globalMeshProfile.name}' to {appliedCount} section(s).";
            if (importMatchingBlendshapes && importedCount > 0)
            {
                message += $"\n\nImported matching blendshapes for {importedCount} section(s).";
            }
            
            Debug.Log($"[ConfigurationBuilder] {message}");
            EditorUtility.DisplayDialog("Success", message, "OK");
            Repaint();
        }

        private void DrawGlobalMaterialsSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Global Material Overrides", EditorStyles.boldLabel);
            showGlobalMaterials = EditorGUILayout.Foldout(showGlobalMaterials, showGlobalMaterials ? "Hide" : "Show", true);
            EditorGUILayout.EndHorizontal();
            
            if (!showGlobalMaterials)
                return;
            
            EditorGUILayout.HelpBox("Drag and drop materials to apply to all sections. Leave slots empty to use mesh profile defaults.", MessageType.Info);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Size control
            int currentSize = globalMaterialOverrides.Count;
            int newSize = EditorGUILayout.IntField("Size", currentSize);
            
            if (newSize != currentSize)
            {
                while (globalMaterialOverrides.Count < newSize)
                    globalMaterialOverrides.Add(null);
                while (globalMaterialOverrides.Count > newSize)
                    globalMaterialOverrides.RemoveAt(globalMaterialOverrides.Count - 1);
            }
            
            // Material object fields
            for (int i = 0; i < globalMaterialOverrides.Count; i++)
            {
                globalMaterialOverrides[i] = (Material)EditorGUILayout.ObjectField(
                    $"  Material [{i}]", 
                    globalMaterialOverrides[i], 
                    typeof(Material), 
                    false);
            }
            
            EditorGUILayout.Space(5);
            
            // Apply button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUI.enabled = globalMaterialOverrides.Count > 0;
            if (GUILayout.Button("Apply Materials to All Sections", GUILayout.Width(200)))
            {
                ApplyGlobalMaterialsToAllSections();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void ApplyGlobalMaterialsToAllSections()
        {
            if (config == null)
                return;
            
            int totalSections = 0;
            foreach (var segment in config.intestineSegments)
            {
                if (segment.liSections != null)
                    totalSections += segment.liSections.Count;
            }
            
            if (totalSections == 0)
            {
                EditorUtility.DisplayDialog("No Sections", "There are no sections to apply materials to.\n\nAdd segments and sections first.", "OK");
                return;
            }
            
            // Convert Material objects to names
            var materialNames = globalMaterialOverrides.Select(m => m != null ? m.name : "").ToArray();
            
            string materialsPreview = materialNames.Length > 0 
                ? string.Join(", ", materialNames.Where(m => !string.IsNullOrEmpty(m)).DefaultIfEmpty("(all empty)"))
                : "(empty array)";
            
            bool confirm = EditorUtility.DisplayDialog(
                "Apply Global Materials",
                $"This will set the material overrides for ALL {totalSections} section(s) across all segments.\n\n" +
                $"Materials: {materialsPreview}\n\n" +
                "Are you sure?",
                "Apply",
                "Cancel");
            
            if (!confirm)
                return;
            
            int appliedCount = 0;
            foreach (var segment in config.intestineSegments)
            {
                if (segment.liSections == null)
                    continue;
                
                foreach (var section in segment.liSections)
                {
                    section.materialOverrideNames = materialNames;
                    appliedCount++;
                }
            }
            
            Debug.Log($"[ConfigurationBuilder] Applied global materials to {appliedCount} sections.");
            EditorUtility.DisplayDialog("Success", $"Applied material overrides to {appliedCount} section(s).", "OK");
            Repaint();
        }

        private void DrawSegmentsList()
        {
            EditorGUILayout.LabelField("Intestine Segments", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            foreach (var segment in config.intestineSegments)
            {
                bool isSelected = selectedSegment == segment.liSegment;

                EditorGUILayout.BeginVertical(isSelected ? GetSelectedStyle() : EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                // Make the entire row clickable
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.alignment = TextAnchor.MiddleLeft;
                buttonStyle.fontStyle = FontStyle.Bold;

                if (GUILayout.Button(segment.liSegment.ToString(), buttonStyle, GUILayout.ExpandWidth(true)))
                {
                    selectedSegment = segment.liSegment;
                    selectedSectionIndex = -1;
                    Repaint();
                }

                EditorGUILayout.LabelField($"({segment.liSections?.Count ?? 0})", GUILayout.Width(40));

                EditorGUILayout.EndHorizontal();

                // Draw sections under this segment
                if (isSelected && segment.liSections != null)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < segment.liSections.Count; i++)
                    {
                        bool isSectionSelected = selectedSectionIndex == i;
                        GUI.backgroundColor = isSectionSelected ? Color.cyan : Color.white;

                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button($"  Section {i}", EditorStyles.miniButton))
                        {
                            selectedSectionIndex = i;
                        }

                        var section = segment.liSections[i];
                        string meshName = section.meshProfile?.name ?? "(No profile)";
                        EditorGUILayout.LabelField(meshName, EditorStyles.miniLabel, GUILayout.Width(120));

                        EditorGUILayout.EndHorizontal();

                        GUI.backgroundColor = Color.white;
                    }
                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space(5);

                    // Add/Remove section buttons
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("+", GUILayout.Width(30)))
                    {
                        AddSection(segment);
                    }

                    GUI.enabled = segment.liSections.Count > 0;
                    if (GUILayout.Button("-", GUILayout.Width(30)))
                    {
                        RemoveSection(segment);
                    }
                    GUI.enabled = true;

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // Add segment button
            if (GUILayout.Button("Add Segment", GUILayout.Height(25)))
            {
                ShowAddSegmentMenu();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSectionEditor()
        {
            if (selectedSegment == null)
            {
                EditorGUILayout.HelpBox("Select a segment from the list to view sections", MessageType.Info);
                return;
            }

            var segment = config.intestineSegments.FirstOrDefault(s => s.liSegment == selectedSegment.Value);

            if (segment == null)
            {
                EditorGUILayout.HelpBox("Segment not found", MessageType.Error);
                return;
            }

            // Check if segment has no sections
            if (segment.liSections == null || segment.liSections.Count == 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.Space(20);

                EditorGUILayout.LabelField($"{selectedSegment} Segment", EditorStyles.boldLabel);
                EditorGUILayout.Space(10);

                EditorGUILayout.HelpBox(
                    $"This segment has no sections yet.\n\n" +
                    $"Click the '+' button in the segment list (left panel) to add sections.",
                    MessageType.Info
                );

                EditorGUILayout.Space(10);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Add First Section", GUILayout.Width(150), GUILayout.Height(30)))
                {
                    AddSection(segment);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(20);
                EditorGUILayout.EndVertical();
                return;
            }

            // Check if no section is selected
            if (selectedSectionIndex < 0 || selectedSectionIndex >= segment.liSections.Count)
            {
                DrawSegmentProfileSection(segment);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField($"{selectedSegment} Segment - {segment.liSections.Count} Section(s)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Click on a section in the segment list (left panel) to edit its properties.",
                    MessageType.Info
                );
                EditorGUILayout.Space(10);
                EditorGUILayout.EndVertical();
                return;
            }

            // Per-segment profile picker — shown whenever a valid segment is active
            DrawSegmentProfileSection(segment);

            EditorGUILayout.Space(5);

            var section = segment.liSections[selectedSectionIndex];

            EditorGUILayout.LabelField($"{selectedSegment} - Section {selectedSectionIndex}", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Mesh Profile
            EditorGUILayout.LabelField("Mesh Profile", EditorStyles.boldLabel);
            var newProfile = (SplineMeshProfile)EditorGUILayout.ObjectField("Profile", section.meshProfile, typeof(SplineMeshProfile), false);

            if (newProfile != section.meshProfile)
            {
                if (newProfile != null)
                {
                    bool importMatching = false;
                    if (section.meshProfile != null && section.blendshapeValues.Count > 0)
                    {
                        int choice = EditorUtility.DisplayDialogComplex(
                            "Change Mesh Profile",
                            $"Apply '{newProfile.name}' to this section.\n\n" +
                            "• Import Matching: preserve blendshape values shared with the old profile\n" +
                            "• Reset to Defaults: use the new profile's default values",
                            "Import Matching", "Cancel", "Reset to Defaults");

                        if (choice == 1) return;
                        importMatching = choice == 0;
                    }
                    section.InitializeFromProfile(newProfile, importMatching);
                }
                else
                {
                    section.meshProfile = newProfile;
                }
            }

            EditorGUILayout.Space(5);

            // Material Overrides
            EditorGUILayout.LabelField("Material Overrides", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Material names to load from Resources (leave empty to use mesh profile defaults)", MessageType.Info);

            int matCount = section.materialOverrideNames?.Length ?? 0;
            int newMatCount = EditorGUILayout.IntField("Size", matCount);

            if (newMatCount != matCount)
            {
                Array.Resize(ref section.materialOverrideNames, newMatCount);
            }

            if (section.materialOverrideNames != null)
            {
                for (int i = 0; i < section.materialOverrideNames.Length; i++)
                {
                    section.materialOverrideNames[i] = EditorGUILayout.TextField($"  [{i}]", section.materialOverrideNames[i] ?? "");
                }
            }

            EditorGUILayout.Space(10);

            // Presets
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Blendshape Presets", EditorStyles.boldLabel);
            showPresets = EditorGUILayout.Toggle(showPresets, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();

            if (showPresets)
            {
                DrawPresetSelector(section);
            }

            EditorGUILayout.Space(10);

            // Blendshapes
            if (section.meshProfile != null)
            {
                DrawBlendshapeEditor(section);
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a mesh profile to edit blendshapes", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresetSelector(LI_SectionConfiguration section)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            presetScrollPos = EditorGUILayout.BeginScrollView(presetScrollPos, GUILayout.Height(150));

            foreach (var preset in availablePresets)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(preset.name, GUILayout.Width(200)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Apply Preset",
                        $"Apply preset '{preset.name}' to this section?\n\nMode: {preset.applicationMode}",
                        "Apply",
                        "Cancel"))
                    {
                        section.ApplyPreset(preset, true);
                    }
                }

                EditorGUILayout.LabelField(preset.targetMeshProfile?.name ?? "Any", GUILayout.Width(120));
                EditorGUILayout.LabelField($"{preset.blendshapeValues.Count} values", GUILayout.Width(80));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawBlendshapeEditor(LI_SectionConfiguration section)
        {
            EditorGUILayout.LabelField("Blendshape Values", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (section.meshProfile.blendshapes == null || section.meshProfile.blendshapes.Count == 0)
            {
                EditorGUILayout.HelpBox("Mesh profile has no blendshapes. Refresh the profile from its mesh.", MessageType.Warning);

                if (GUILayout.Button("Refresh Profile"))
                {
                    section.meshProfile.RefreshFromMesh();
                }
            }
            else
            {
                // List only enabled blendshapes
                foreach (var bs in section.meshProfile.blendshapes)
                {
                    if (!bs.isEnabled) continue;

                    float currentValue = section.GetBlendshapeValue(bs.name, bs.defaultValue);
                    float newValue = EditorGUILayout.Slider(new GUIContent(bs.name), currentValue, 0f, 100f);

                    if (Math.Abs(newValue - currentValue) > 0.001f)
                        section.SetBlendshapeValue(bs.name, newValue);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateNewConfiguration()
        {
            config = new LI_GenerationConfiguration();
            configPath = "";
            selectedSegment = null;
            selectedSectionIndex = -1;

            Debug.Log("Created new configuration");
        }

        private void LoadConfiguration()
        {
            string path = EditorUtility.OpenFilePanel("Load Configuration", Application.streamingAssetsPath, "ligc");

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    // Use JsonData to read and deserialize
                    config = new LI_GenerationConfiguration();
                    config.ReadFromFile(path);

                    // Restore mesh profile references from stored paths
                    foreach (var segment in config.intestineSegments)
                    {
                        foreach (var section in segment.liSections)
                        {
                            section.LoadMeshProfile();
                        }
                    }

                    configPath = path;
                    selectedSegment = null;
                    selectedSectionIndex = -1;

                    Debug.Log($"Loaded configuration from: {path}");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to load configuration:\n{e.Message}", "OK");
                    Debug.LogError($"Failed to load configuration: {e}");
                }
            }
        }

        private void SaveConfiguration()
        {
            if (string.IsNullOrEmpty(configPath))
            {
                SaveConfigurationAs();
            }
            else
            {
                try
                {
                    // Prepare all sections for serialization by storing mesh profile paths
                    foreach (var segment in config.intestineSegments)
                    {
                        foreach (var section in segment.liSections)
                        {
                            section.PrepareForSerialization();
                        }
                    }

                    config.Save(destPath: configPath);
                    Debug.Log($"Saved configuration to: {configPath}");
                    EditorUtility.DisplayDialog("Success", "Configuration saved successfully!", "OK");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to save configuration:\n{e.Message}", "OK");
                    Debug.LogError($"Failed to save configuration: {e}");
                }
            }
        }

        private void SaveConfigurationAs()
        {
            string path = EditorUtility.SaveFilePanel("Save Configuration", Application.streamingAssetsPath, "configuration", "ligc");

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    configPath = path;

                    // Prepare all sections for serialization by storing mesh profile paths
                    foreach (var segment in config.intestineSegments)
                    {
                        foreach (var section in segment.liSections)
                        {
                            section.PrepareForSerialization();
                        }
                    }

                    config.Save(destPath: path);
                    Debug.Log($"Saved configuration to: {path}");
                    EditorUtility.DisplayDialog("Success", "Configuration saved successfully!", "OK");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to save configuration:\n{e.Message}", "OK");
                    Debug.LogError($"Failed to save configuration: {e}");
                }
            }
        }

        private void ValidateConfiguration()
        {
            lastValidation = LI_ConfigurationValidator.ValidateConfiguration(config);
            showValidation = true;

            string summary = LI_ConfigurationValidator.GetConfigurationSummary(config);

            EditorUtility.DisplayDialog(
                "Validation Result",
                $"{summary}\n\n{lastValidation}",
                "OK"
            );
        }

        private void AddSection(LI_SegmentConfiguration segment)
        {
            if (segment.liSections == null)
                segment.liSections = new List<LI_SectionConfiguration>();

            segment.liSections.Add(new LI_SectionConfiguration());
            selectedSectionIndex = segment.liSections.Count - 1;
        }

        private void RemoveSection(LI_SegmentConfiguration segment)
        {
            if (segment.liSections == null || segment.liSections.Count == 0)
                return;

            if (selectedSectionIndex >= 0 && selectedSectionIndex < segment.liSections.Count)
            {
                segment.liSections.RemoveAt(selectedSectionIndex);
                selectedSectionIndex = Mathf.Min(selectedSectionIndex, segment.liSections.Count - 1);
            }
            else
            {
                segment.liSections.RemoveAt(segment.liSections.Count - 1);
            }
        }

        private void ShowAddSegmentMenu()
        {
            GenericMenu menu = new GenericMenu();

            foreach (LI_Segment segmentType in Enum.GetValues(typeof(LI_Segment)))
            {
                bool exists = config.intestineSegments.Any(s => s.liSegment == segmentType);

                if (!exists)
                {
                    menu.AddItem(new GUIContent(segmentType.ToString()), false, () => AddSegment(segmentType));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(segmentType.ToString() + " (exists)"));
                }
            }

            menu.ShowAsContext();
        }

        private void AddSegment(LI_Segment segmentType)
        {
            var newSegment = new LI_SegmentConfiguration(segmentType);
            config.intestineSegments.Add(newSegment);

            // Sort segments in anatomical order
            config.intestineSegments = config.intestineSegments
                .OrderBy(s => (int)s.liSegment)
                .ToList();

            selectedSegment = segmentType;
            selectedSectionIndex = -1;
        }

        private void RefreshPresetList()
        {
            availablePresets.Clear();

            string[] guids = AssetDatabase.FindAssets("t:LI_BlendshapePreset");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                LI_BlendshapePreset preset = AssetDatabase.LoadAssetAtPath<LI_BlendshapePreset>(assetPath);

                if (preset != null)
                {
                    availablePresets.Add(preset);
                }
            }

            Debug.Log($"Found {availablePresets.Count} blendshape presets");
        }
        
        private void RefreshMeshProfileList()
        {
            availableMeshProfiles.Clear();
            
            string[] guids = AssetDatabase.FindAssets("t:SplineMeshProfile");
            
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                SplineMeshProfile profile = AssetDatabase.LoadAssetAtPath<SplineMeshProfile>(assetPath);
                
                if (profile != null)
                {
                    availableMeshProfiles.Add(profile);
                }
            }
            
            // Sort by name for easier selection
            availableMeshProfiles = availableMeshProfiles.OrderBy(p => p.name).ToList();
            
            Debug.Log($"Found {availableMeshProfiles.Count} mesh profiles");
        }

        private GUIStyle GetSelectedStyle()
        {
            var style = new GUIStyle(EditorStyles.helpBox);
            style.normal.background = MakeTex(2, 2, new Color(0.3f, 0.5f, 0.7f, 0.3f));
            return style;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }
    }
}
