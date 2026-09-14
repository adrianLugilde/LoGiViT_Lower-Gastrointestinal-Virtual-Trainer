#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace CustomUI
{
    [CustomEditor(typeof(RadialSliderController), true)]
    public class RadialSliderControllerEditor : Editor
    {
        private static readonly HashSet<string> SkippedProperties = new HashSet<string>
        {
            "_valueTextField", "_iconObject", "_iconSprite",
            "_labelText","_labelObject", "_labelLocalizeStringEvent"
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

                if (prop.name == "UseNameLabel")
                {
                    DrawToggleSection(prop, new[]
                    {
                        ("_labelText", "Label Text"),
                        ("_labelObject", "Label Object"),
                        ("_labelLocalizeStringEvent", "Label Localize String Event")
                    });
                    continue;
                }

                if (prop.name == "UseValueLabel")
                {
                    DrawToggleSection(prop, new[]
                    {
                        ("_valueTextField", "Value Text Field")
                    });
                    continue;
                }

                if (prop.name == "UseIcon")
                {
                    DrawToggleSection(prop, new[]
                    {
                        ("_iconObject", "Icon Object"),
                        ("_iconSprite", "Icon Sprite")
                    });
                    continue;
                }

                if (SkippedProperties.Contains(prop.name))
                    continue;

                EditorGUILayout.PropertyField(prop, true);
            }

            // Add runtime value display
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Runtime Values", EditorStyles.boldLabel);
                
                RadialSliderController slider = (RadialSliderController)target;
                
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.FloatField("Current Angle", slider.SliderAngle);
                    EditorGUILayout.FloatField("Current Value", slider.SliderValue);
                    EditorGUILayout.FloatField("Raw Value", slider.SliderValueRaw);
                }
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