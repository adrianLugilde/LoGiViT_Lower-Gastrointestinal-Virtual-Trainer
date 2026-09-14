using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ModelEditor;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace LargeIntestine
{
    [Serializable]
    public class LI_SegmentConfiguration
    {
        public LI_Segment liSegment;
        public List<LI_SectionConfiguration> liSections;

        // Parameterless constructor for JSON deserialization
        public LI_SegmentConfiguration()
        {
            liSections = new List<LI_SectionConfiguration>();
        }

        public LI_SegmentConfiguration(LI_Segment liSegment)
        {
            this.liSegment = liSegment;
            liSections = new List<LI_SectionConfiguration>();
        }

        public LI_SegmentConfiguration(LI_SegmentConfiguration liSegmentConf)
        {
            this.liSegment = liSegmentConf.liSegment;
            this.liSections = liSegmentConf.liSections;
        }
    }

    [Serializable]
    public struct SectionBlendConfiguration
    {

        [Range(0.0f, 100)] public float union_border_inf;
        [Range(0.0f, 100)] public float union_border_sup;
        [Range(0.0f, 100)] public float union_wide_inf;
        [Range(0.0f, 100)] public float union_wide_sup;
        [Range(0.0f, 100)] public float tenia_inf;
        [Range(0.0f, 100)] public float tenia_sup;
    }

    [Serializable]
    public struct SectionShapeConfiguration
    {
        [Range(0.0f, 100)] public float rotation;
        [Range(0.0f, 100)] public float wide;
        [Range(0.0f, 100)] public float narrow;
        [Range(0.0f, 100)] public float wide_x;
        [Range(0.0f, 100)] public float wide_y;
        [Range(0.0f, 100)] public float narrow_x;
        [Range(0.0f, 100)] public float narrow_y;
        [Range(0.0f, 100)] public float wide_x_inf;
        [Range(0.0f, 100)] public float wide_x_med;
        [Range(0.0f, 100)] public float wide_x_sup;
        [Range(0.0f, 100)] public float wide_y_inf;
        [Range(0.0f, 100)] public float wide_y_med;
        [Range(0.0f, 100)] public float wide_y_sup;
        [Range(0.0f, 100)] public float narrow_x_inf;
        [Range(0.0f, 100)] public float narrow_x_med;
        [Range(0.0f, 100)] public float narrow_x_sup;
        [Range(0.0f, 100)] public float narrow_y_inf;
        [Range(0.0f, 100)] public float narrow_y_med;
        [Range(0.0f, 100)] public float narrow_y_sup;
        [Range(0.0f, 100)] public float rectum_start;
        [Range(0.0f, 100)] public float cecum_end;
    }

    [Serializable]
    public struct BlendshapesWeights
    {
        public SerializableDictionary<string, float> Values;

        public BlendshapesWeights SetValues(SkinnedMeshRenderer smr)
        {
            Values = new SerializableDictionary<string, float>();
            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
            {
                Values.Add(smr.sharedMesh.GetBlendShapeName(i), smr.GetBlendShapeWeight(i));
            }
            return this;
        }
    }

    /// <summary>
    /// The int value represents in which blendshape idx from the SubmodelBlendshape enum
    /// ends the blendshapes specific for each SubmodelType
    /// </summary>
    public enum SubmodelType
    {
        Default = 0,
        Rectum = 16,
        Cecum = 26,
    }

    [Serializable]
    public enum IntestineSectionBlendshape
    {
        union_border_inf = 0,
        union_border_sup = 1,
        union_wide_inf = 2,
        union_wide_sup = 3,
        tenia_inf = 4,
        tenia_sup = 5,
        forma = 6,
        union_soft = 7,
        ancho = 8,
        estrecho = 9,
        ancho_X_med = 10,
        ancho_Y_med = 11,
        ancho_X_inf = 12,
        ancho_Y_inf = 13,
        ancho_X_sup = 14,
        ancho_Y_sup = 15,
        R_recto = 16,
        R_canal = 17,
        R_houston_up_low_valve_inf = 18,
        R_houston_up_low_shape_inf = 19,
        R_houston_up_low_valve_sup = 20,
        R_houston_up_low_shape_sup = 21,
        R_houston_mid_valve_inf = 22,
        R_houston_mid_shape_inf = 23,
        R_houston_mid_valve_sup = 24,
        R_houston_mid_shape_sup = 25,
        C_cecum = 26,
        C_arrugas = 27,
        C_apendice = 28,
        C_forma = 29
    }

    [Serializable]
    public enum RectumSectionBlendshape
    {
        R_recto = 16,
        R_canal = 17,
        R_houston_up_low_valve_inf = 18,
        R_houston_up_low_shape_inf = 19,
        R_houston_up_low_valve_sup = 20,
        R_houston_up_low_shape_sup = 21,
        R_houston_mid_valve_inf = 22,
        R_houston_mid_shape_inf = 23,
        R_houston_mid_valve_sup = 24,
        R_houston_mid_shape_sup = 25
    }

    [Serializable]
    public enum CecumSectionBlendshape
    {
        C_cecum = 16,
        C_arrugas = 17,
        C_apendice = 18,
        C_forma = 19
    }

    /* BLENDSHAPES v0.02
     * 
     * 
    [Serializable]
    public enum DefaultSectionBlendshape
    {
        union_border_inf = 0,
        union_border_sup = 1,
        union_wide_inf = 2,
        union_wide_sup = 3,
        tenia_inf = 4,
        tenia_sup = 5,
        ancho = 6, //6 ancho y 7 estrecho, habr�a que fusionarlos en uno
        estrecho = 7,
        forma = 8,
        union_soft = 9,
        ancho_X_med = 10,
        ancho_Y_med = 11,
        ancho_X_inf = 12,
        ancho_Y_inf = 13,
        ancho_X_sup = 14,
        ancho_Y_sup = 15,
        redonde_fix = 16
    }

    [Serializable]
    public enum RectumSectionBlendshape
    {
        union_border_inf = 0,
        union_border_sup = 1,
        union_wide_inf = 2,
        union_wide_sup = 3,
        tenia_inf = 4,
        tenia_sup = 5,
        ancho = 6, //6 ancho y 7 estrecho, habr�a que fusionarlos en uno
        estrecho = 7,
        forma = 8,
        union_soft = 9,
        ancho_X_med = 10,
        ancho_Y_med = 11,
        ancho_X_inf = 12,
        ancho_Y_inf = 13,
        ancho_X_sup = 14,
        ancho_Y_sup = 15,
        R_recto = 16,
        R_canal = 17,
        redondez = 22,
        R_houston_up_low_valve_inf = 23,
        R_houston_up_low_shape_inf = 24,
        R_houston_up_low_valve_sup = 25,
        R_houston_up_low_shape_sup = 26,
        R_houston_mid_valve_inf = 27,
        R_houston_mid_shape_inf = 28,
        R_houston_mid_valve_sup = 29,
        R_houston_mid_shape_sup = 30,
    }

    [Serializable]
    public enum CecumSectionBlendshape
    {
        union_border_inf = 0,
        union_wide_inf = 1,
        tenia_inf = 2,
        ancho = 3, //3 ancho y 4 estrecho, habr�a que fusionarlos en uno
        estrecho = 4,
        ancho_X_inf = 5,
        ancho_Y_inf = 6,
        rotacion_x30_inf_mix = 7,
        CECUM_forma = 8,
        CECUM_arrugas = 9,
        APEN_eje = 10,
        APEN_preile_2 = 11,
        APEN_pevlic_5 = 12,
        APEN_retro_11 = 13
    } 


     * 
     * *

    /* BLENDSHAPES v0.01
     * 
    public enum DefaultSectionBlendshape
    {
        union_border_inf = 0,
        union_border_sup = 1,
        union_wide_inf = 2,
        union_wide_sup = 3,
        tenia_inf = 4,
        tenia_sup = 5,
        union_border = 6,
        union_wide = 7,
        tenia = 8,
        ancho = 9, //9 ancho y 10 estrecho, habr�a que fusionarlos en uno
        estrecho = 10,
        ancho_X_inf = 11,
        ancho_X_med = 12,
        ancho_X_sup = 13,
        ancho_Y_inf = 14,
        ancho_Y_med = 15,
        ancho_Y_sup = 16,
        rectum_start = 17,
        cecum_end = 18,
    }

    public enum RectumSectionBlendshape
    {
        union_border_sup = 0,
        union_wide_sup = 1,
        tenia_sup = 2,
        ancho = 3,
        R_upper_houston = 4,
        R_mid_houston = 5,
        R_lower_houston = 6,
        R_ano = 9,
    }

    public enum CecumSectionBlendshape
    {
        union_border_inf = 0,
        union_wide_inf = 1,
        tenia_inf = 2,
        ancho = 3, 
        estrecho = 4,
        ancho_X_inf = 5,
        ancho_Y_inf = 6
    }
    */

    /// <summary>
    /// Configuration for a single section of the large intestine.
    /// Uses dictionary-based blendshape storage for resilience to mesh changes.
    /// </summary>
    [Serializable]
    public class LI_SectionConfiguration : ModelEditor.IModelSectionConfiguration
    {
        [Header("Mesh Profile")]
        [Tooltip("The mesh profile that defines this section's mesh and available blendshapes")]
        [NonSerialized] // Don't serialize the Unity object reference directly
        public SplineMeshProfile meshProfile;

        [SerializeField]
        public string meshProfileId;

        [Header("Material Overrides")]
        [Tooltip("Optional material override names. These will be loaded via Resources.Load to override the mesh profile's default materials.")]
        public string[] materialOverrideNames;

        [Header("Blendshape Values")]
        [Tooltip("Blendshape values stored by name for resilience to reordering")]
        public SerializableDictionary<string, float> blendshapeValues = new SerializableDictionary<string, float>();

        #region IModelSectionConfiguration Implementation
        
        /// <summary>
        /// Explicit interface implementation for generic mesh profile access
        /// </summary>
        SplineMeshProfile ModelEditor.IModelSectionConfiguration.MeshProfile => meshProfile;
        
        #endregion


        /// <summary>
        /// Stores the profile ID before serialization.
        /// </summary>
        public void PrepareForSerialization()
        {
            if (meshProfile != null)
                meshProfileId = meshProfile.Id;
        }

        /// <summary>
        /// Resolves the mesh profile from the catalog using the stored ID.
        /// </summary>
        public void LoadMeshProfile()
        {
            if (string.IsNullOrEmpty(meshProfileId)) return;

            var catalog = SplineMeshProfileCatalog.Load();
            if (catalog == null)
            {
                Debug.LogError("[LI_SectionConfiguration] SplineMeshProfileCatalog not found in Resources.");
                return;
            }

            meshProfile = catalog.GetById(meshProfileId);
            if (meshProfile == null)
                Debug.LogWarning($"[LI_SectionConfiguration] No SplineMeshProfile found for id '{meshProfileId}'.");
        }

        /// <summary>
        /// Gets the materials for this section.
        /// If materialOverrideNames are specified, loads them via Resources.Load.
        /// Otherwise, returns default materials from mesh profile.
        /// </summary>
        public Material[] GetMaterials()
        {
            // If we have override names, load them from Resources
            if (materialOverrideNames != null && materialOverrideNames.Length > 0)
            {
                var materials = new Material[materialOverrideNames.Length];
                for (int i = 0; i < materialOverrideNames.Length; i++)
                {
                    materials[i] = Resources.Load<Material>(materialOverrideNames[i]);
                    if (materials[i] == null)
                    {
                        Debug.LogWarning($"Failed to load material '{materialOverrideNames[i]}' from Resources");
                    }
                }
                return materials;
            }

            // Otherwise use defaults from mesh profile
            return meshProfile?.defaultMaterials;
        }

        /// <summary>
        /// Initializes blendshape values from the mesh profile defaults
        /// </summary>
        public void InitializeFromProfile()
        {
            InitializeFromProfile(meshProfile, false);
        }
        
        /// <summary>
        /// Initializes blendshape values from a mesh profile, optionally importing matching blendshapes from current values
        /// </summary>
        /// <param name="newProfile">The new mesh profile to apply</param>
        /// <param name="importMatchingBlendshapes">If true, tries to preserve values for blendshapes that exist in both old and new profiles</param>
        /// <returns>True if any blendshapes were imported from the old profile</returns>
        public bool InitializeFromProfile(SplineMeshProfile newProfile, bool importMatchingBlendshapes)
        {
            if (newProfile == null)
            {
                Debug.LogWarning("Cannot initialize: No mesh profile provided.");
                return false;
            }

            bool didImport = false;
            var oldBlendshapeValues = new SerializableDictionary<string, float>();
            
            // Store old blendshape values if importing
            if (importMatchingBlendshapes && meshProfile != null && blendshapeValues.Count > 0)
            {
                foreach (var kvp in blendshapeValues)
                {
                    oldBlendshapeValues[kvp.Key] = kvp.Value;
                }
            }
            
            // Update mesh profile reference
            meshProfile = newProfile;

            // Clear and initialize with new profile defaults — only enabled blendshapes
            blendshapeValues.Clear();
            foreach (var bs in newProfile.blendshapes)
            {
                if (!bs.isEnabled) continue;

                // Check if this blendshape existed in the old profile
                if (importMatchingBlendshapes && oldBlendshapeValues.TryGetValue(bs.name, out float oldValue))
                {
                    blendshapeValues[bs.name] = oldValue;
                    didImport = true;
                }
                else
                {
                    blendshapeValues[bs.name] = bs.defaultValue;
                }
            }
            
            return didImport;
        }

        /// <summary>
        /// Gets a blendshape value by name
        /// </summary>
        public float GetBlendshapeValue(string blendshapeName, float defaultValue = 0f)
        {
            return blendshapeValues.TryGetValue(blendshapeName, out float value) ? value : defaultValue;
        }

        /// <summary>
        /// Sets a blendshape value by name
        /// </summary>
        public void SetBlendshapeValue(string blendshapeName, float value)
        {
            blendshapeValues[blendshapeName] = Mathf.Clamp(value, 0f, 100f);
        }

        /// <summary>
        /// Validates this section configuration
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            if (meshProfile == null)
            {
                result.AddError("No mesh profile assigned");
                return result;
            }

            // Validate the mesh profile itself
            var profileValidation = meshProfile.Validate();
            result.Merge(profileValidation);

            // Check for obsolete blendshapes
            var profileBlendshapeNames = meshProfile.blendshapes.Select(b => b.name).ToHashSet();
            foreach (var key in blendshapeValues.Keys)
            {
                if (!profileBlendshapeNames.Contains(key))
                {
                    result.AddWarning($"Obsolete blendshape '{key}' in section configuration");
                }
            }

            // Check for missing blendshapes
            foreach (var bs in meshProfile.blendshapes)
            {
                if (!blendshapeValues.ContainsKey(bs.name))
                {
                    result.AddInfo($"Blendshape '{bs.name}' not set, will use default ({bs.defaultValue})");
                }
            }

            return result;
        }

        /// <summary>
        /// Applies a preset to this section
        /// </summary>
        public bool ApplyPreset(LI_BlendshapePreset preset, bool forceCompatibility = false)
        {
            return preset.ApplyToSection(this, forceCompatibility);
        }
    }

    /// <summary>
    /// Serializable dictionary for Unity inspector compatibility
    /// </summary>
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new List<TKey>();

        [SerializeField]
        private List<TValue> values = new List<TValue>();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();

            foreach (var kvp in this)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();

            if (keys.Count != values.Count)
            {
                Debug.LogError($"SerializableDictionary: Key count ({keys.Count}) doesn't match value count ({values.Count})");
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                this[keys[i]] = values[i];
            }
        }
    }

    public enum LI_Segment
    {
        Rectum = 0,
        Sigmoid = 1,
        Descending = 2,
        Transverse = 3,
        Ascending = 4,
        Cecum = 5,
    }

    public enum LI_AnatomicalPosition
    {
        Supine = 0,
        LeftLateral = -90,
        Prone = -180,
        RightLateral = -270
    }

    public class LargeIntestineSection : IModelSection
    {
        private readonly SkinnedMeshRenderer _renderer;
        private readonly MeshCollider _collider;
        
        // IModelSection implementation
        Renderer IModelSection.Renderer => _renderer;
        Collider IModelSection.Collider => _collider;
        
        // Keep typed accessors for internal use
        public SkinnedMeshRenderer Renderer => _renderer;
        public MeshCollider Collider => _collider;

        public LargeIntestineSection(SkinnedMeshRenderer renderer, MeshCollider collider)
        {
            _renderer = renderer;
            _collider = collider;
        }

        public void SetRendererBlendshapeWeights(List<float> weights)
        {
            for (int i = 0; i < weights.Count; i++)
            {
                _renderer.SetBlendShapeWeight(i, weights[i]);
            }
        }

        public IDictionary<string, float> GetBlendshapeValues()
        {
            var res = new SerializableDictionary<string, float>();
            for (int i = 0; i < _renderer.sharedMesh.blendShapeCount; i++)
            {
                res.Add(_renderer.sharedMesh.GetBlendShapeName(i), _renderer.GetBlendShapeWeight(i));
            }
            return res;
        }
        
        // Keep for backwards compatibility
        public SerializableDictionary<string, float> GetRendererBlendshapeDict()
        {
            return (SerializableDictionary<string, float>)GetBlendshapeValues();
        }

        public float GetBlendShapeWeight(string name)
        {
            if (_renderer == null || _renderer.sharedMesh == null) return 0f;

            int index = _renderer.sharedMesh.GetBlendShapeIndex(name);
            if (index != -1)
            {
                return _renderer.GetBlendShapeWeight(index);
            }

            Debug.LogWarning($"BlendShape '{name}' not found on {_renderer.name}");
            return 0f;
        }
    }
}
