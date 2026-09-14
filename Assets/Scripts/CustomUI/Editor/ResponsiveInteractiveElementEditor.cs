#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace CustomUI
{
    [CustomEditor(typeof(ResponsiveInteractiveElement), true)]
    public class ResponsiveInteractiveElementEditor : Editor
    {
        private static readonly HashSet<string> SkippedProperties = new HashSet<string>
        {
            "_normalIconSprite","_normalIconObject", "_highlightIconSprite","_highlightIconObject",
            "_tooltipLocalizeStringEvent", "_tooltipText", "_tooltipObject",
            "_normalLabelLocalizeStringEvent", "_highlightLabelLocalizeStringEvent", "_labelText",
            "_normalLabelObject","_highlightLabelObject", "_highlightedCG", "_selectedCG", "_normalCG",
            "_transitionSpeed", "HoverGroupController", "_iconSize"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (prop.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(prop);
                    continue;
                }

                if(prop.name == "IsDynamic") 
                {
                    DrawToggleSection(prop, new[]
                    {
                        ("_highlightedCG", "Highlighted Canvas Group"),
                        ("_selectedCG", "Selected Canvas Group"),
                        ("_normalCG", "Normal Canvas Group"),
                        ("_transitionSpeed", "Transition Speed"),
                        ("_normalizedAlpha", "Normalized Alpha"),
                        ("HoverGroupController", "Hover Group Controller"),
                        });
                    continue;
                }

                if (prop.name == "UseLabel")
                {
                    DrawToggleSection(prop, new[]
                    {
                        ("_normalLabelLocalizeStringEvent", "Normal Label Localize String Event"),
                        ("_normalLabelObject", "Normal Label Object"),
                        ("_highlightLabelLocalizeStringEvent", "Highlight Label Localize String Event"),
                        ("_highlightLabelObject", "Highlight Label Object"),
                        ("_labelText", "Label Text")
                        });
                    continue;
                }

                if (prop.name == "UseIcon")
                {
                    DrawToggleSection(prop, new[]
                    {
                        ("_normalIconSprite", "Normal Icon Sprite"),
                        ("_normalIconObject", "Normal Icon Object"),
                        ("_highlightIconSprite", "Highlight Icon Sprite"),
                        ("_highlightIconObject", "Highlight Icon Object"),
                        ("_iconSize", "Icon Size"),

                    });
                    continue;
                }

                if (prop.name == "UseTooltip")
                {
                    DrawToggleSection(prop, new[]
                    {
                        ("_tooltipLocalizeStringEvent", "Tooltip Localize String Event"),
                        ("_tooltipText", "Tooltip Text"),
                        ("_tooltipObject", "Tooltip Object")
                    });
                    continue;
                }

                if (SkippedProperties.Contains(prop.name))
                    continue;

                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawToggleSection(SerializedProperty toggleProp, (string propName, string displayName)[] properties)
        {
            EditorGUILayout.PropertyField(toggleProp);

            if (toggleProp.boolValue)
            {
                EditorGUI.indentLevel++;

                foreach (var (propName, displayName) in properties)
                {
                    var property = serializedObject.FindProperty(propName);
                    if (property != null)
                        EditorGUILayout.PropertyField(property, new GUIContent(displayName));
                }

                EditorGUI.indentLevel--;
            }
        }
    }
}
#endif
