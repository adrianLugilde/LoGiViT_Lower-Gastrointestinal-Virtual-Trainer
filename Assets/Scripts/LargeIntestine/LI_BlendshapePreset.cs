using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LargeIntestine
{
    /// <summary>
    /// Reusable preset for blendshape configurations.
    /// Allows saving and applying common blendshape combinations across multiple sections.
    /// </summary>
    [CreateAssetMenu(fileName = "BlendshapePreset", menuName = "LoGiViT/Large Intestine/Blendshape Preset")]
    public class LI_BlendshapePreset : ScriptableObject
    {
        [Header("Preset Information")]
        public string presetName;
        
        [TextArea(2, 4)]
        public string description;
        
        [Header("Target Mesh Profile")]
        [Tooltip("The mesh profile this preset is designed for")]
        public SplineMeshProfile targetMeshProfile;
        
        [Header("Blendshape Values")]
        [Tooltip("The blendshape values to apply")]
        public List<BlendshapeValue> blendshapeValues = new List<BlendshapeValue>();
        
        [Header("Application Mode")]
        [Tooltip("How this preset should be applied")]
        public ApplicationMode applicationMode = ApplicationMode.Override;
        
        [Serializable]
        public class BlendshapeValue
        {
            public string blendshapeName;
            
            [Range(0, 100)]
            public float value;
            
            [Tooltip("If true, this value will be applied even in Additive mode")]
            public bool forceApply = false;
            
            public BlendshapeValue(string name, float value)
            {
                this.blendshapeName = name;
                this.value = value;
                this.forceApply = false;
            }
        }
        
        public enum ApplicationMode
        {
            /// <summary>
            /// Replace all blendshape values with preset values
            /// </summary>
            Override,
            
            /// <summary>
            /// Add preset values to existing values (clamped to 0-100)
            /// </summary>
            Additive,
            
            /// <summary>
            /// Only apply values that don't exist in the target
            /// </summary>
            FillMissing
        }
        
        /// <summary>
        /// Creates a preset from an existing section configuration
        /// </summary>
        public void CaptureFromSection(LI_SectionConfiguration section)
        {
            if (section.meshProfile == null)
            {
                Debug.LogWarning("Cannot capture from section: No mesh profile assigned.");
                return;
            }
            
            targetMeshProfile = section.meshProfile;
            blendshapeValues.Clear();
            
            foreach (var kvp in section.blendshapeValues)
            {
                blendshapeValues.Add(new BlendshapeValue(kvp.Key, kvp.Value));
            }
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            
            Debug.Log($"Captured {blendshapeValues.Count} blendshape values from section.");
        }
        
        /// <summary>
        /// Applies this preset to a section configuration
        /// </summary>
        public bool ApplyToSection(LI_SectionConfiguration section, bool forceCompatibility = false)
        {
            if (section.meshProfile == null)
            {
                Debug.LogWarning("Cannot apply preset: Section has no mesh profile assigned.");
                return false;
            }
            
            // Check compatibility
            if (!forceCompatibility && targetMeshProfile != null && section.meshProfile != targetMeshProfile)
            {
                Debug.LogWarning($"Preset '{name}' is designed for '{targetMeshProfile.name}' but applying to '{section.meshProfile.name}'. Use forceCompatibility=true to override.");
                return false;
            }
            
            int appliedCount = 0;
            
            switch (applicationMode)
            {
                case ApplicationMode.Override:
                    // Clear existing values and apply preset
                    section.blendshapeValues.Clear();
                    foreach (var bsValue in blendshapeValues)
                    {
                        section.blendshapeValues[bsValue.blendshapeName] = bsValue.value;
                        appliedCount++;
                    }
                    break;
                
                case ApplicationMode.Additive:
                    // Add values to existing (or force apply)
                    foreach (var bsValue in blendshapeValues)
                    {
                        if (bsValue.forceApply)
                        {
                            section.blendshapeValues[bsValue.blendshapeName] = bsValue.value;
                        }
                        else if (section.blendshapeValues.ContainsKey(bsValue.blendshapeName))
                        {
                            float newValue = Mathf.Clamp(section.blendshapeValues[bsValue.blendshapeName] + bsValue.value, 0f, 100f);
                            section.blendshapeValues[bsValue.blendshapeName] = newValue;
                        }
                        else
                        {
                            section.blendshapeValues[bsValue.blendshapeName] = bsValue.value;
                        }
                        appliedCount++;
                    }
                    break;
                
                case ApplicationMode.FillMissing:
                    // Only apply values that don't exist
                    foreach (var bsValue in blendshapeValues)
                    {
                        if (!section.blendshapeValues.ContainsKey(bsValue.blendshapeName))
                        {
                            section.blendshapeValues[bsValue.blendshapeName] = bsValue.value;
                            appliedCount++;
                        }
                    }
                    break;
            }
            
            Debug.Log($"Applied preset '{name}' to section ({applicationMode} mode): {appliedCount} values applied.");
            return true;
        }
        
        /// <summary>
        /// Validates that the preset is compatible with a mesh profile
        /// </summary>
        public ValidationResult ValidateCompatibility(SplineMeshProfile profile)
        {
            var result = new ValidationResult();
            
            if (profile == null)
            {
                result.AddError("Cannot validate: Mesh profile is null");
                return result;
            }
            
            if (targetMeshProfile != null && targetMeshProfile != profile)
            {
                result.AddWarning($"Preset designed for '{targetMeshProfile.name}' but checking against '{profile.name}'");
            }
            
            // Get all blendshape names from the profile
            var profileBlendshapeNames = profile.blendshapes.Select(b => b.name).ToHashSet();
            
            // Check each preset value
            foreach (var bsValue in blendshapeValues)
            {
                if (!profileBlendshapeNames.Contains(bsValue.blendshapeName))
                {
                    result.AddWarning($"Blendshape '{bsValue.blendshapeName}' not found in mesh profile '{profile.name}'");
                }
            }
            
            // Check for missing blendshapes
            int missingCount = profileBlendshapeNames.Count - blendshapeValues.Count;
            if (missingCount > 0)
            {
                result.AddInfo($"Preset covers {blendshapeValues.Count}/{profileBlendshapeNames.Count} blendshapes ({missingCount} not included)");
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets the value for a specific blendshape
        /// </summary>
        public float GetValue(string blendshapeName, float defaultValue = 0f)
        {
            var bsValue = blendshapeValues.FirstOrDefault(b => b.blendshapeName == blendshapeName);
            return bsValue != null ? bsValue.value : defaultValue;
        }
        
        /// <summary>
        /// Sets or updates a blendshape value
        /// </summary>
        public void SetValue(string blendshapeName, float value, bool forceApply = false)
        {
            var existing = blendshapeValues.FirstOrDefault(b => b.blendshapeName == blendshapeName);
            if (existing != null)
            {
                existing.value = Mathf.Clamp(value, 0f, 100f);
                existing.forceApply = forceApply;
            }
            else
            {
                blendshapeValues.Add(new BlendshapeValue(blendshapeName, Mathf.Clamp(value, 0f, 100f))
                {
                    forceApply = forceApply
                });
            }
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// Removes a blendshape value from the preset
        /// </summary>
        public bool RemoveValue(string blendshapeName)
        {
            int removed = blendshapeValues.RemoveAll(b => b.blendshapeName == blendshapeName);
            
            if (removed > 0)
            {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }
            
            return removed > 0;
        }
        
        /// <summary>
        /// Syncs the preset with the target mesh profile, adding missing blendshapes
        /// </summary>
        public void SyncWithProfile()
        {
            if (targetMeshProfile == null)
            {
                Debug.LogWarning("Cannot sync: No target mesh profile assigned.");
                return;
            }
            
            var existingNames = blendshapeValues.Select(b => b.blendshapeName).ToHashSet();
            int added = 0;
            
            foreach (var bs in targetMeshProfile.blendshapes)
            {
                if (!existingNames.Contains(bs.name))
                {
                    blendshapeValues.Add(new BlendshapeValue(bs.name, bs.defaultValue));
                    added++;
                }
            }
            
            if (added > 0)
            {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
                
                Debug.Log($"Synced preset with profile: Added {added} new blendshapes.");
            }
        }
        
        /// <summary>
        /// Creates a copy of this preset
        /// </summary>
        public LI_BlendshapePreset Clone()
        {
            var clone = CreateInstance<LI_BlendshapePreset>();
            clone.presetName = presetName + " (Copy)";
            clone.description = description;
            clone.targetMeshProfile = targetMeshProfile;
            clone.applicationMode = applicationMode;
            
            foreach (var bsValue in blendshapeValues)
            {
                clone.blendshapeValues.Add(new BlendshapeValue(bsValue.blendshapeName, bsValue.value)
                {
                    forceApply = bsValue.forceApply
                });
            }
            
            return clone;
        }
    }
}
