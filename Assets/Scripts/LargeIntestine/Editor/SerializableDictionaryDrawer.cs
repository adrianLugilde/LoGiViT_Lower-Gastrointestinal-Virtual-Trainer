using UnityEditor;
using UnityEngine;

namespace LargeIntestine
{
    /// <summary>
    /// Custom property drawer for SerializableDictionary that displays keys as read-only
    /// </summary>
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>))]
    public class SerializableDictionaryDrawer : PropertyDrawer
    {
        private const float KEY_LABEL_WIDTH = 200f;
        private const float SPACING = 2f;
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;
            
            var keysProperty = property.FindPropertyRelative("keys");
            if (keysProperty == null)
                return EditorGUIUtility.singleLineHeight;
            
            // Header + each key-value pair
            return EditorGUIUtility.singleLineHeight * (1 + keysProperty.arraySize) + 
                   SPACING * keysProperty.arraySize;
        }
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            // Foldout header
            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);
            
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                
                var keysProperty = property.FindPropertyRelative("keys");
                var valuesProperty = property.FindPropertyRelative("values");
                
                if (keysProperty != null && valuesProperty != null)
                {
                    float yOffset = headerRect.yMax + SPACING;
                    
                    for (int i = 0; i < keysProperty.arraySize; i++)
                    {
                        var keyProperty = keysProperty.GetArrayElementAtIndex(i);
                        var valueProperty = valuesProperty.GetArrayElementAtIndex(i);
                        
                        var lineRect = new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight);
                        
                        // Draw key as read-only label
                        var keyRect = new Rect(lineRect.x, lineRect.y, KEY_LABEL_WIDTH, lineRect.height);
                        EditorGUI.LabelField(keyRect, keyProperty.stringValue, EditorStyles.boldLabel);
                        
                        // Draw value as editable field
                        var valueRect = new Rect(keyRect.xMax + 5, lineRect.y, 
                                                 lineRect.width - keyRect.width - 5, lineRect.height);
                        EditorGUI.PropertyField(valueRect, valueProperty, GUIContent.none);
                        
                        yOffset += EditorGUIUtility.singleLineHeight + SPACING;
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUI.EndProperty();
        }
    }
}
