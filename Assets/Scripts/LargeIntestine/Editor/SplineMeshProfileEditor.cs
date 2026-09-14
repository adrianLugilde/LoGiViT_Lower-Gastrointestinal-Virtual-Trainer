using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace LargeIntestine
{
    [CustomEditor(typeof(SplineMeshProfile))]
    public class SplineMeshProfileEditor : Editor
    {
        private SplineMeshProfile profile;
        private Vector2 blendshapeScrollPos;
        private Vector2 groupScrollPos;
        private bool showBlendshapes = true;
        private bool showGroups = true;
        private string searchFilter = "";
        private bool[] blendshapeFoldouts;
        private bool[] groupFoldouts;
        private int selectedGroupIndex = -1;
        
        private ReorderableList panelsReorderableList;
        
        private void OnEnable()
        {
            profile = (SplineMeshProfile)target;
            InitializeFoldouts();
            InitializeGroupFoldouts();
            InitializePanelsReorderableList();
        }
        
        private void InitializePanelsReorderableList()
        {
            var panelsProp = serializedObject.FindProperty("blendshapePanels");
            
            panelsReorderableList = new ReorderableList(serializedObject, panelsProp, true, true, true, true);
            
            // Header drawing
            panelsReorderableList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, $"Blendshape Panels ({panelsProp.arraySize})");
            };
            
            // Element drawing
            panelsReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                DrawPanelElement(rect, index);
            };
            
            // Element height
            panelsReorderableList.elementHeightCallback = (int index) =>
            {
                return GetPanelElementHeight(index);
            };
            
            // Add callback
            panelsReorderableList.onAddCallback = (ReorderableList list) =>
            {
                int index = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                list.index = index;
                
                // Get the newly added element and clear it to create an empty panel
                var newElement = list.serializedProperty.GetArrayElementAtIndex(index);
                var panelTitleProp = newElement.FindPropertyRelative("panelTitle");
                var blendshapeNamesProp = newElement.FindPropertyRelative("blendshapeNames");
                
                // Clear the title (set to empty LocalizedString)
                if (panelTitleProp != null)
                {
                    panelTitleProp.FindPropertyRelative("m_TableReference").FindPropertyRelative("m_TableCollectionName").stringValue = "";
                    panelTitleProp.FindPropertyRelative("m_TableEntryReference").FindPropertyRelative("m_KeyId").longValue = 0;
                    panelTitleProp.FindPropertyRelative("m_TableEntryReference").FindPropertyRelative("m_Key").stringValue = "";
                }
                
                // Clear the blendshape names list
                if (blendshapeNamesProp != null)
                {
                    blendshapeNamesProp.ClearArray();
                }
                
                serializedObject.ApplyModifiedProperties();
                InitializeGroupFoldouts();
            };
            
            // Remove callback
            panelsReorderableList.onRemoveCallback = (ReorderableList list) =>
            {
                if (EditorUtility.DisplayDialog("Remove Panel", "Are you sure you want to remove this panel?", "Yes", "No"))
                {
                    ReorderableList.defaultBehaviours.DoRemoveButton(list);
                    serializedObject.ApplyModifiedProperties();
                    InitializeGroupFoldouts();
                }
            };
        }
        
        private void InitializeFoldouts()
        {
            if (profile.blendshapes != null)
            {
                blendshapeFoldouts = new bool[profile.blendshapes.Count];
            }
        }
        
        private void InitializeGroupFoldouts()
        {
            if (profile.blendshapePanels != null)
            {
                groupFoldouts = new bool[profile.blendshapePanels.Count];
            }
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Header
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Mesh Profile Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Profile ID (read-only, copyable)
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Profile ID", profile.Id);
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
            {
                EditorGUIUtility.systemCopyBuffer = profile.Id;
                Debug.Log($"[SplineMeshProfile] Copied ID: {profile.Id}");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();

            var meshProp = serializedObject.FindProperty("mesh");
            EditorGUILayout.PropertyField(meshProp);
            
            var materialsProp = serializedObject.FindProperty("defaultMaterials");
            EditorGUILayout.PropertyField(materialsProp, true);
            
            var meshTypeProp = serializedObject.FindProperty("meshType");
            EditorGUILayout.PropertyField(meshTypeProp);

            var uvChannelProp = serializedObject.FindProperty("uvChannelIndex");
            uvChannelProp.intValue = EditorGUILayout.IntField(new GUIContent("UV Channel", "UV channel (0–4) used for texturing this section type"), uvChannelProp.intValue);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
            
            EditorGUILayout.Space(10);
            
            // Validation Status
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Validation Status", EditorStyles.boldLabel);
            
            bool isValid = profile.isValid && profile.mesh != null && profile.blendshapes.Count == profile.mesh.blendShapeCount;
            
            if (isValid)
            {
                EditorGUILayout.HelpBox($"✓ Valid - {profile.blendshapes.Count} blendshapes", MessageType.Info);
            }
            else if (profile.mesh == null)
            {
                EditorGUILayout.HelpBox("⚠ No mesh assigned", MessageType.Warning);
            }
            else if (profile.blendshapes.Count == 0)
            {
                EditorGUILayout.HelpBox("⚠ No blendshapes defined. Click 'Refresh From Mesh'", MessageType.Warning);
            }
            else if (profile.blendshapes.Count != profile.mesh.blendShapeCount)
            {
                EditorGUILayout.HelpBox($"⚠ Blendshape count mismatch: Profile={profile.blendshapes.Count}, Mesh={profile.mesh.blendShapeCount}", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            // Action Buttons
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = profile.mesh != null;
            if (GUILayout.Button("Refresh From Mesh", GUILayout.Height(30)))
            {
                profile.RefreshFromMesh();
                EditorUtility.SetDirty(profile);
            }
            GUI.enabled = true;
            
            if (GUILayout.Button("Validate", GUILayout.Height(30)))
            {
                var validationResult = profile.Validate();
                if (validationResult.IsValid)
                {
                    EditorUtility.DisplayDialog("Validation Result", "✓ Mesh profile is valid!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Validation Result", validationResult.ToString(), "OK");
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Description
            EditorGUILayout.LabelField("Metadata", EditorStyles.boldLabel);
            var descProp = serializedObject.FindProperty("description");
            EditorGUILayout.PropertyField(descProp);
            
            EditorGUILayout.Space(10);
            
            // Blendshapes Section
            if (profile.blendshapes != null && profile.blendshapes.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Blendshapes header with foldout
                EditorGUILayout.BeginHorizontal();
                int enabledCount = profile.blendshapes.Count(b => b.isEnabled);
                string blendshapeLabel = enabledCount == profile.blendshapes.Count
                    ? $"Blendshapes ({profile.blendshapes.Count})"
                    : $"Blendshapes ({enabledCount}/{profile.blendshapes.Count} enabled)";
                showBlendshapes = EditorGUILayout.Foldout(showBlendshapes, blendshapeLabel, true, EditorStyles.foldoutHeader);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset Defaults", GUILayout.Width(100)))
                {
                    if (EditorUtility.DisplayDialog("Reset Defaults", "Set all default values to 0?", "Yes", "No"))
                    {
                        foreach (var bs in profile.blendshapes)
                            bs.defaultValue = 0f;
                        EditorUtility.SetDirty(profile);
                    }
                }

                if (GUILayout.Button("Enable All", GUILayout.Width(75)))
                {
                    foreach (var bs in profile.blendshapes) bs.isEnabled = true;
                    EditorUtility.SetDirty(profile);
                }

                if (GUILayout.Button("Disable All", GUILayout.Width(75)))
                {
                    foreach (var bs in profile.blendshapes) bs.isEnabled = false;
                    EditorUtility.SetDirty(profile);
                }

                EditorGUILayout.EndHorizontal();

                // Link buttons on their own row
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Auto-Link inf/sup", GUILayout.Width(110)))
                {
                    if (EditorUtility.DisplayDialog("Auto-Configure Links",
                        "This will automatically configure links between _inf and _sup blendshape pairs.\n\n" +
                        "For example: 'union_border_inf' will link to 'union_border_sup' on the Previous section.",
                        "Configure", "Cancel"))
                    {
                        AutoConfigureInfSupLinks();
                        EditorUtility.SetDirty(profile);
                    }
                }

                if (GUILayout.Button("Clear All Links", GUILayout.Width(100)))
                {
                    if (EditorUtility.DisplayDialog("Clear Links", "Remove all blendshape link configurations?", "Yes", "No"))
                    {
                        foreach (var bs in profile.blendshapes)
                            bs.links.Clear();
                        EditorUtility.SetDirty(profile);
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                if (showBlendshapes)
                {
                    EditorGUILayout.Space(5);
                    
                    // Search Filter
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
                    searchFilter = EditorGUILayout.TextField(searchFilter);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.Space(5);
                    
                    // Blendshapes List
                    blendshapeScrollPos = EditorGUILayout.BeginScrollView(blendshapeScrollPos, GUILayout.MaxHeight(400));
                    
                    for (int i = 0; i < profile.blendshapes.Count; i++)
                    {
                        var bs = profile.blendshapes[i];
                        
                        // Apply search filter
                        if (!string.IsNullOrEmpty(searchFilter) && 
                            !bs.name.ToLower().Contains(searchFilter.ToLower()))
                        {
                            continue;
                        }
                        
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        
                        // Blendshape Header with foldout
                        // Ensure foldouts array is properly sized
                        if (blendshapeFoldouts == null || blendshapeFoldouts.Length <= i)
                        {
                            InitializeFoldouts();
                        }

                        // Reserve one full-width row; position checkbox and foldout manually
                        // to avoid the foldout arrow overlapping the checkbox.
                        Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

                        // Checkbox: 16px on the far left
                        Rect checkRect = new Rect(rowRect.x, rowRect.y + 1, 16, rowRect.height);
                        Color prevColor = GUI.color;
                        if (!bs.isEnabled) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                        bool newEnabled = EditorGUI.Toggle(checkRect, bs.isEnabled);
                        if (newEnabled != bs.isEnabled)
                        {
                            bs.isEnabled = newEnabled;
                            if (!newEnabled)
                            {
                                // Remove this blendshape from every panel it belongs to
                                foreach (var panel in profile.blendshapePanels)
                                    panel.blendshapeNames.Remove(bs.name);
                            }
                            EditorUtility.SetDirty(profile);
                        }
                        GUI.color = prevColor;

                        // Build header label — append link summary inline so nothing overlaps
                        string linkSummary = "";
                        if (bs.HasLink)
                        {
                            int validCount = bs.links.Count(l => l.IsValid);
                            linkSummary = validCount == 1
                                ? $"  {DirectionArrow(bs.links.First(l => l.IsValid).direction)} {bs.links.First(l => l.IsValid).linkedBlendshapeName}"
                                : $"  ({validCount} links)";
                        }
                        string headerLabel = $"[{bs.index}] {bs.name}{linkSummary}";

                        Rect foldoutRect = new Rect(rowRect.x + 30, rowRect.y, rowRect.width - 30, rowRect.height);
                        GUI.color = bs.HasLink ? Color.cyan : Color.white;
                        blendshapeFoldouts[i] = EditorGUI.Foldout(foldoutRect, blendshapeFoldouts[i], headerLabel, true, EditorStyles.foldoutHeader);
                        GUI.color = Color.white;
                        
                        if (blendshapeFoldouts[i])
                        {
                            EditorGUI.indentLevel++;
                            
                            // Default Value
                            bs.defaultValue = EditorGUILayout.Slider("Default Value", bs.defaultValue, 0f, 100f);
                            
                            EditorGUILayout.Space(5);
                            
                            // Link Configuration
                            EditorGUILayout.LabelField("Blendshape Links", EditorStyles.boldLabel);
                            EditorGUILayout.HelpBox(
                                "Each link drives another blendshape when this one changes.",
                                MessageType.None
                            );

                            if (bs.links == null)
                                bs.links = new List<SplineMeshProfile.BlendshapeLinkConfig>();

                            int linkToRemove = -1;
                            for (int li = 0; li < bs.links.Count; li++)
                            {
                                var link = bs.links[li];
                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField($"Link {li + 1}", EditorStyles.boldLabel);
                                if (GUILayout.Button("✕", GUILayout.Width(22)))
                                    linkToRemove = li;
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Direction", GUILayout.Width(100));
                                link.direction = (SplineMeshProfile.LinkDirection)EditorGUILayout.EnumPopup(link.direction);
                                EditorGUILayout.EndHorizontal();

                                if (link.direction != SplineMeshProfile.LinkDirection.None)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.LabelField("Blendshape", GUILayout.Width(100));
                                    var bsNames = profile.blendshapes.Select(b => b.name).ToList();
                                    bsNames.Insert(0, "(Select)");
                                    int curIdx = string.IsNullOrEmpty(link.linkedBlendshapeName) ? 0 : bsNames.IndexOf(link.linkedBlendshapeName);
                                    if (curIdx < 0) curIdx = 0;
                                    int newIdx = EditorGUILayout.Popup(curIdx, bsNames.ToArray());
                                    link.linkedBlendshapeName = newIdx > 0 ? bsNames[newIdx] : "";
                                    EditorGUILayout.EndHorizontal();

                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.LabelField(new GUIContent("Chain Links", "When enabled, the linked blendshape's own links are also fired in sequence."), GUILayout.Width(100));
                                    link.chainLinks = EditorGUILayout.Toggle(link.chainLinks);
                                    EditorGUILayout.EndHorizontal();

                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.LabelField(new GUIContent("Scale", "Multiplier applied to the source value before writing to the linked blendshape.\nlinkedValue = sourceValue × scale + offset"), GUILayout.Width(100));
                                    link.linkScale = EditorGUILayout.FloatField(link.linkScale);
                                    if (GUILayout.Button("Reset", GUILayout.Width(45)))
                                        link.linkScale = 1f;
                                    EditorGUILayout.EndHorizontal();

                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.LabelField(new GUIContent("Offset", "Constant added after scaling.\nlinkedValue = sourceValue × scale + offset\nIgnored when a dynamic offset modulator is set."), GUILayout.Width(100));
                                    link.linkOffset = EditorGUILayout.FloatField(link.linkOffset);
                                    if (GUILayout.Button("Reset", GUILayout.Width(45)))
                                        link.linkOffset = 0f;
                                    EditorGUILayout.EndHorizontal();

                                    EditorGUILayout.LabelField(new GUIContent("Dynamic Offset Modulator", "Optional blendshape whose current value drives the effective offset via linear interpolation between two known states."), EditorStyles.boldLabel);

                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.LabelField(new GUIContent("Modulator", "Blendshape on the same section whose value is used to interpolate the offset between State A and State B."), GUILayout.Width(100));
                                    var bsNamesWithNone = profile.blendshapes.Select(b => b.name).Prepend("(none)").ToList();
                                    int curModIdx = string.IsNullOrEmpty(link.offsetModulatorBlendshape)
                                        ? 0 : bsNamesWithNone.IndexOf(link.offsetModulatorBlendshape);
                                    if (curModIdx < 0) curModIdx = 0;
                                    int newModIdx = EditorGUILayout.Popup(curModIdx, bsNamesWithNone.ToArray());
                                    link.offsetModulatorBlendshape = newModIdx > 0 ? bsNamesWithNone[newModIdx] : "";
                                    EditorGUILayout.EndHorizontal();

                                    if (link.HasOffsetModulator)
                                    {
                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUILayout.LabelField(new GUIContent("State A", "When the modulator equals 'Mod', the effective offset will be 'Offset'."), GUILayout.Width(100));
                                        EditorGUILayout.LabelField("Mod=", GUILayout.Width(30));
                                        link.modulatorValueA = EditorGUILayout.FloatField(link.modulatorValueA, GUILayout.Width(50));
                                        EditorGUILayout.LabelField("Offset=", GUILayout.Width(42));
                                        link.offsetAtModulatorA = EditorGUILayout.FloatField(link.offsetAtModulatorA, GUILayout.Width(50));
                                        EditorGUILayout.EndHorizontal();

                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUILayout.LabelField(new GUIContent("State B", "Second known state. The offset is linearly interpolated between State A and State B."), GUILayout.Width(100));
                                        EditorGUILayout.LabelField("Mod=", GUILayout.Width(30));
                                        link.modulatorValueB = EditorGUILayout.FloatField(link.modulatorValueB, GUILayout.Width(50));
                                        EditorGUILayout.LabelField("Offset=", GUILayout.Width(42));
                                        link.offsetAtModulatorB = EditorGUILayout.FloatField(link.offsetAtModulatorB, GUILayout.Width(50));
                                        EditorGUILayout.EndHorizontal();

                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUILayout.LabelField(new GUIContent("No Extrapolation", "When enabled, the offset stops changing once the modulator reaches State A or State B.\nWhen disabled, the offset keeps extrapolating linearly beyond those values."), GUILayout.Width(100));
                                        link.noExtrapolation = EditorGUILayout.Toggle(link.noExtrapolation);
                                        EditorGUILayout.EndHorizontal();
                                    }

                                    if (link.IsValid)
                                    {
                                        string dirText = link.direction switch
                                        {
                                            SplineMeshProfile.LinkDirection.Previous => "previous",
                                            SplineMeshProfile.LinkDirection.Next     => "next",
                                            SplineMeshProfile.LinkDirection.Same     => "same",
                                            _                                        => "?"
                                        };
                                        string offsetNote = link.HasOffsetModulator
                                            ? $"offset = lerp({link.offsetAtModulatorA:F2}, {link.offsetAtModulatorB:F2}) via '{link.offsetModulatorBlendshape}'"
                                            : Mathf.Approximately(link.linkOffset, 0f) ? "" : $" + {link.linkOffset:F2}";
                                        string formula = $"value × {link.linkScale:F2}" + (string.IsNullOrEmpty(offsetNote) ? "" : $", {offsetNote}");
                                        EditorGUILayout.HelpBox(
                                            $"→ '{link.linkedBlendshapeName}' on {dirText} section ({formula})",
                                            MessageType.Info);
                                    }
                                }

                                EditorGUILayout.EndVertical();
                            }

                            if (linkToRemove >= 0)
                            {
                                bs.links.RemoveAt(linkToRemove);
                                EditorUtility.SetDirty(profile);
                            }

                            if (GUILayout.Button("+ Add Link"))
                            {
                                bs.links.Add(new SplineMeshProfile.BlendshapeLinkConfig { linkScale = 1f });
                                EditorUtility.SetDirty(profile);
                            }

                            EditorGUILayout.Space(4);
                            EditorGUILayout.LabelField("Clamp Rules", EditorStyles.boldLabel);
                            EditorGUILayout.HelpBox("Dynamic min/max constraints on this blendshape's own value, driven by another blendshape.", MessageType.None);

                            if (bs.clampRules == null)
                                bs.clampRules = new List<SplineMeshProfile.BlendshapeClampRule>();

                            int clampToRemove = -1;
                            for (int ci = 0; ci < bs.clampRules.Count; ci++)
                            {
                                var rule = bs.clampRules[ci];
                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField($"Rule {ci + 1}", EditorStyles.boldLabel);
                                if (GUILayout.Button("✕", GUILayout.Width(22)))
                                    clampToRemove = ci;
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(new GUIContent("Type", "Whether this rule enforces a minimum or maximum value."), GUILayout.Width(100));
                                rule.clampType = (SplineMeshProfile.ClampType)EditorGUILayout.EnumPopup(rule.clampType);
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(new GUIContent("Modulator", "Blendshape on the same section whose value drives the limit."), GUILayout.Width(100));
                                var modNames = profile.blendshapes.Select(b => b.name).Prepend("(none)").ToList();
                                int curMod = string.IsNullOrEmpty(rule.modulatorBlendshape) ? 0 : modNames.IndexOf(rule.modulatorBlendshape);
                                if (curMod < 0) curMod = 0;
                                int newMod = EditorGUILayout.Popup(curMod, modNames.ToArray());
                                rule.modulatorBlendshape = newMod > 0 ? modNames[newMod] : "";
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(new GUIContent("State A", "First known valid state. When the modulator equals 'Mod', the clamp limit will be 'Limit'. Interpolation starts here."), GUILayout.Width(100));
                                EditorGUILayout.LabelField("Mod=", GUILayout.Width(30));
                                rule.modulatorValueA = EditorGUILayout.FloatField(rule.modulatorValueA, GUILayout.Width(50));
                                EditorGUILayout.LabelField("Limit=", GUILayout.Width(38));
                                rule.limitAtA = EditorGUILayout.FloatField(rule.limitAtA, GUILayout.Width(50));
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(new GUIContent("State B", "Second known valid state. The clamp limit is linearly interpolated between State A and State B based on the modulator's current value."), GUILayout.Width(100));
                                EditorGUILayout.LabelField("Mod=", GUILayout.Width(30));
                                rule.modulatorValueB = EditorGUILayout.FloatField(rule.modulatorValueB, GUILayout.Width(50));
                                EditorGUILayout.LabelField("Limit=", GUILayout.Width(38));
                                rule.limitAtB = EditorGUILayout.FloatField(rule.limitAtB, GUILayout.Width(50));
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(new GUIContent("No Extrapolation", "When enabled, the limit stops changing once the modulator reaches State A or State B.\nWhen disabled, the limit keeps extrapolating linearly beyond those values."), GUILayout.Width(100));
                                rule.noExtrapolation = EditorGUILayout.Toggle(rule.noExtrapolation);
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.EndVertical();
                            }

                            if (clampToRemove >= 0)
                            {
                                bs.clampRules.RemoveAt(clampToRemove);
                                EditorUtility.SetDirty(profile);
                            }

                            if (GUILayout.Button("+ Add Clamp Rule"))
                            {
                                bs.clampRules.Add(new SplineMeshProfile.BlendshapeClampRule());
                                EditorUtility.SetDirty(profile);
                            }

                            EditorGUI.indentLevel--;
                        }
                        
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(2);
                    }
                    
                    EditorGUILayout.EndScrollView();
                }
                
                EditorGUILayout.EndVertical();
            }
            else if (profile.mesh != null)
            {
                EditorGUILayout.HelpBox("No blendshapes defined. Click 'Refresh From Mesh' to populate.", MessageType.Info);
            }
            
            EditorGUILayout.Space(10);
            
            // Blendshape Panels Section
            DrawBlendshapePanelsSection();
            
            serializedObject.ApplyModifiedProperties();
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(profile);
            }
        }
        
        private void DrawBlendshapePanelsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header toolbar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Blendshape Panels", EditorStyles.boldLabel);
            
            GUI.enabled = profile.blendshapes != null && profile.blendshapes.Count > 0;
            if (GUILayout.Button("Auto-Generate", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("Auto-Generate Panels", 
                    "This will clear existing panels and auto-generate based on naming patterns.\n\n" +
                    "Blendshapes with same base name (e.g., width_superior, width_inferior) will be grouped together.", 
                    "Generate", "Cancel"))
                {
                    profile.AutoGeneratePanels();
                    InitializeGroupFoldouts();
                    serializedObject.Update();
                    EditorUtility.SetDirty(profile);
                }
            }
            
            if (GUILayout.Button("Clear All", GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Clear Panels", "Remove all blendshape panels?", "Yes", "No"))
                {
                    profile.blendshapePanels.Clear();
                    InitializeGroupFoldouts();
                    serializedObject.Update();
                    EditorUtility.SetDirty(profile);
                }
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Show ungrouped blendshapes warning
            var ungrouped = profile.GetUngroupedBlendshapes();
            if (ungrouped.Count > 0)
            {
                EditorGUILayout.HelpBox($"⚠ {ungrouped.Count} blendshape(s) not in any panel: {string.Join(", ", ungrouped.Take(5).Select(b => b.name))}{(ungrouped.Count > 5 ? "..." : "")}", MessageType.Warning);
            }
            
            // Draw reorderable list
            if (panelsReorderableList != null)
            {
                panelsReorderableList.DoLayoutList();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// Automatically configures links between _inf and _sup blendshape pairs.
        /// Matches any blendshape whose name *contains* _inf or _sup (anywhere, case-insensitive),
        /// so names like Ancho_Sup_X, Rugosidad_SupF, etc. are all handled.
        /// _inf blendshapes link to the _sup counterpart on the Previous section.
        /// _sup blendshapes link to the _inf counterpart on the Next section.
        /// </summary>
        private void AutoConfigureInfSupLinks()
        {
            int configuredCount = 0;

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
                        configuredCount++;
                        Debug.Log($"Configured link: {bs.name} → {linkedBs.name} (Previous)");
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
                        configuredCount++;
                        Debug.Log($"Configured link: {bs.name} → {linkedBs.name} (Next)");
                    }
                }
            }
            
            Debug.Log($"Auto-configured {configuredCount} blendshape links.");
            EditorUtility.DisplayDialog("Auto-Configure Complete", 
                $"Configured {configuredCount} blendshape links based on _inf/_sup naming convention.", 
                "OK");
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

        private static string DirectionArrow(SplineMeshProfile.LinkDirection direction) => direction switch
        {
            SplineMeshProfile.LinkDirection.Previous => "←",
            SplineMeshProfile.LinkDirection.Next     => "→",
            SplineMeshProfile.LinkDirection.Same     => "=",
            _                                        => "?"
        };

        private static string ReplaceTokenIgnoreCase(string source, string oldToken, string newToken)
        {
            int idx = source.IndexOf(oldToken, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return source;
            return source.Substring(0, idx) + newToken + source.Substring(idx + oldToken.Length);
        }

        /// <summary>
        /// Draws a single panel element in the reorderable list
        /// </summary>
        private void DrawPanelElement(Rect rect, int index)
        {
            if (index < 0 || index >= profile.blendshapePanels.Count)
                return;
            
            var panel = profile.blendshapePanels[index];
            var panelsProp = serializedObject.FindProperty("blendshapePanels");
            var panelProp = panelsProp.GetArrayElementAtIndex(index);
            
            // Ensure foldouts array is properly sized
            if (groupFoldouts == null || groupFoldouts.Length <= index)
            {
                InitializeGroupFoldouts();
            }
            
            float yPos = rect.y + 2;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            
            // Panel header with foldout — offset by 15px to clear the reorder drag handle
            Rect foldoutRect = new Rect(rect.x + 15, yPos, rect.width - 95, lineHeight);
            string panelDisplayName = GetPanelDisplayName(panel, index);
            
            // Show prefab type indicator
            Color indicatorColor = panel.Count switch
            {
                1 => Color.green,
                2 => Color.cyan,
                3 => Color.yellow,
                _ => Color.red
            };
            
            Rect labelRect = new Rect(rect.x + rect.width - 75, yPos, 75, lineHeight);
            Color oldColor = GUI.color;
            GUI.color = indicatorColor;
            EditorGUI.LabelField(labelRect, panel.Count <= 3 ? $"[{panel.Count}-Slider]" : "[Invalid]", EditorStyles.miniLabel);
            GUI.color = oldColor;
            
            groupFoldouts[index] = EditorGUI.Foldout(foldoutRect, groupFoldouts[index], $"{panelDisplayName} ({panel.Count} slider{(panel.Count != 1 ? "s" : "")})", true);
            
            yPos += lineHeight + spacing;
            
            if (groupFoldouts[index])
            {
                EditorGUI.indentLevel++;
                
                // Panel title — use actual property height so LocalizedString expands correctly
                var titleProp = panelProp.FindPropertyRelative("panelTitle");
                if (titleProp != null)
                {
                    float titleHeight = EditorGUI.GetPropertyHeight(titleProp, true);
                    Rect titleRect = new Rect(rect.x, yPos, rect.width, titleHeight);
                    EditorGUI.PropertyField(titleRect, titleProp, new GUIContent("Title"), true);
                    yPos += titleHeight + spacing;
                }
                
                yPos += spacing * 2;
                
                // Blendshapes label
                Rect blendshapesLabelRect = new Rect(rect.x, yPos, rect.width, lineHeight);
                EditorGUI.LabelField(blendshapesLabelRect, "Blendshapes in Panel:", EditorStyles.boldLabel);
                yPos += lineHeight + spacing;
                
                // List of blendshapes
                var blendshapeNamesProp = panelProp.FindPropertyRelative("blendshapeNames");
                int blendshapeToRemove = -1;
                
                for (int j = 0; j < panel.blendshapeNames.Count; j++)
                {
                    Rect itemRect = new Rect(rect.x + 15, yPos, rect.width - 50, lineHeight);
                    Rect numberRect = new Rect(rect.x, yPos, 30, lineHeight);
                    Rect removeRect = new Rect(rect.x + rect.width - 25, yPos, 20, lineHeight);
                    
                    EditorGUI.LabelField(numberRect, $"{j + 1}.");
                    
                    // Dropdown to select blendshape — only show enabled ones
                    var allNames = profile.blendshapes.Where(b => b.isEnabled).Select(b => b.name).ToList();
                    allNames.Insert(0, "(Select)");
                    
                    int currentIdx = string.IsNullOrEmpty(panel.blendshapeNames[j]) 
                        ? 0 
                        : allNames.IndexOf(panel.blendshapeNames[j]);
                    if (currentIdx < 0) currentIdx = 0;
                    
                    int newIdx = EditorGUI.Popup(itemRect, currentIdx, allNames.ToArray());
                    panel.blendshapeNames[j] = newIdx > 0 ? allNames[newIdx] : "";
                    
                    if (GUI.Button(removeRect, "-"))
                    {
                        blendshapeToRemove = j;
                    }
                    
                    yPos += lineHeight + spacing;
                }
                
                if (blendshapeToRemove >= 0)
                {
                    panel.blendshapeNames.RemoveAt(blendshapeToRemove);
                    EditorUtility.SetDirty(profile);
                }
                
                // Add blendshape button
                GUI.enabled = panel.Count < 3;
                Rect addButtonRect = new Rect(rect.x, yPos, rect.width, lineHeight);
                if (GUI.Button(addButtonRect, "+ Add Blendshape to Panel"))
                {
                    panel.blendshapeNames.Add("");
                    EditorUtility.SetDirty(profile);
                }
                GUI.enabled = true;
                yPos += lineHeight + spacing;
                
                // Warning if > 3
                if (panel.Count > 3)
                {
                    Rect helpBoxRect = new Rect(rect.x, yPos, rect.width, lineHeight * 2);
                    EditorGUI.HelpBox(helpBoxRect, "Panels can have max 3 blendshapes (limited by prefab options)", MessageType.Error);
                }
                
                EditorGUI.indentLevel--;
            }
        }
        
        /// <summary>
        /// Calculates the height needed for a panel element
        /// </summary>
        private float GetPanelElementHeight(int index)
        {
            if (index < 0 || index >= profile.blendshapePanels.Count)
                return EditorGUIUtility.singleLineHeight;
            
            if (groupFoldouts == null || groupFoldouts.Length <= index || !groupFoldouts[index])
            {
                // Collapsed: just the header
                return EditorGUIUtility.singleLineHeight + 4;
            }
            
            var panel = profile.blendshapePanels[index];
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            
            float height = lineHeight + 4; // Header

            // Title: use actual property height so LocalizedString expanded state is respected
            var panelsProp = serializedObject.FindProperty("blendshapePanels");
            var titleProp = panelsProp.GetArrayElementAtIndex(index).FindPropertyRelative("panelTitle");
            height += (titleProp != null ? EditorGUI.GetPropertyHeight(titleProp, true) : lineHeight) + spacing;

            height += spacing * 2; // Extra spacing
            height += lineHeight + spacing; // "Blendshapes in Panel:" label
            height += (lineHeight + spacing) * panel.blendshapeNames.Count; // Blendshape list
            height += lineHeight + spacing; // Add button
            
            if (panel.Count > 3)
            {
                height += lineHeight * 2 + spacing; // Warning box
            }
            
            height += spacing * 2; // Bottom padding
            
            return height;
        }
        
        /// <summary>
        /// Gets a user-friendly display name for a blendshape panel
        /// </summary>
        private string GetPanelDisplayName(SplineMeshProfile.BlendshapePanel panel, int index)
        {
            return $"Panel {index + 1}";
        }
    }
}
