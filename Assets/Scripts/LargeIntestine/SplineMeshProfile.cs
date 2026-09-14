using System;
using System.Collections.Generic;
using System.Linq;
using GeometryUtils;
using UnityEngine;
using UnityEngine.Localization;

namespace LargeIntestine
{
    /// <summary>
    /// ScriptableObject that defines a mesh profile with its blendshapes metadata.
    /// This provides a stable reference for mesh configurations that remains valid
    /// even when blendshapes are reordered or renamed.
    /// </summary>
    [CreateAssetMenu(fileName = "MeshProfile", menuName = "LoGiViT/Large Intestine/Mesh Profile")]
    public class SplineMeshProfile : ScriptableObject
    {
        [SerializeField, HideInInspector] private string id;
        public string Id => id;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString();
        }

        [Header("Mesh Settings")]
        public Mesh mesh;
        public Material[] defaultMaterials;
        
        [Tooltip("The type of mesh this profile represents")]
        public MeshType meshType = MeshType.Default;
        
        [Header("Blendshape Information")]
        [Tooltip("Auto-populated from mesh. Use 'Refresh From Mesh' to update.")]
        public List<BlendshapeInfo> blendshapes = new List<BlendshapeInfo>();
        
        [Header("Blendshape Panels (for UI Sliders)")]
        [Tooltip("Blendshape panel definitions for UI display. Each panel contains 1-3 blendshapes and maps to a slider prefab.")]
        public List<BlendshapePanel> blendshapePanels = new List<BlendshapePanel>();
        
        [Header("UV Settings")]
        [Tooltip("Which UV channel (0–4) this section type uses for texturing. Applied via MaterialPropertyBlock at generation time.")]
        [Range(0, 4)]
        public int uvChannelIndex = 0;

        [Header("Metadata")]
        [TextArea(2, 4)]
        public string description;
        public bool isValid = false;
        
        /// <summary>
        /// Direction for blendshape linking between adjacent sections
        /// </summary>
        public enum LinkDirection
        {
            None,
            Previous,  // Links to the previous section (index - 1)
            Next,      // Links to the next section (index + 1)
            Same       // Links to another blendshape on the same section
        }
        
        /// <summary>
        /// Configuration for linking a blendshape to another blendshape on an adjacent section
        /// </summary>
        [Serializable]
        public class BlendshapeLinkConfig
        {
            [Tooltip("Direction of the adjacent section to link to")]
            public LinkDirection direction = LinkDirection.None;

            [Tooltip("Name of the blendshape to update on the adjacent section")]
            public string linkedBlendshapeName;

            [Tooltip("When true, the linked blendshape's own links will also be fired (chain propagation).")]
            public bool chainLinks = false;

            [Tooltip("Scale applied to this blendshape's value before writing to the linked blendshape. " +
                     "1 = identical value, 0.5 = half, 2 = double.")]
            public float linkScale = 1f;

            [Tooltip("Constant added after scaling. linkedValue = sourceValue * linkScale + linkOffset.\n" +
                     "Ignored if an offset modulator is configured.")]
            public float linkOffset = 0f;

            [Header("Dynamic Offset Modulation")]
            [Tooltip("Optional blendshape on the same section whose value drives the effective offset.\n" +
                     "When set, offset = lerp(offsetAtModulatorA, offsetAtModulatorB, t) where t is derived from the modulator value.")]
            public string offsetModulatorBlendshape;

            [Tooltip("Modulator value for state A")]
            public float modulatorValueA;
            [Tooltip("Effective offset when modulator is at state A")]
            public float offsetAtModulatorA;

            [Tooltip("Modulator value for state B")]
            public float modulatorValueB;
            [Tooltip("Effective offset when modulator is at state B")]
            public float offsetAtModulatorB;

            [Tooltip("When true, the offset stops changing once the modulator reaches State A or State B.\n" +
                     "When false, the offset keeps extrapolating linearly if the modulator goes beyond those values.")]
            public bool noExtrapolation = false;

            public bool HasOffsetModulator => !string.IsNullOrEmpty(offsetModulatorBlendshape);

            /// <summary>Evaluates the effective offset given the current modulator value.</summary>
            public float EvaluateOffset(float modulatorValue)
            {
                float t = Mathf.InverseLerp(modulatorValueA, modulatorValueB, modulatorValue);
                return noExtrapolation
                    ? Mathf.Lerp(offsetAtModulatorA, offsetAtModulatorB, t)
                    : Mathf.LerpUnclamped(offsetAtModulatorA, offsetAtModulatorB, t);
            }

            public bool IsValid => direction != LinkDirection.None && !string.IsNullOrEmpty(linkedBlendshapeName);
        }

        public enum ClampType { Min, Max, Fixed }

        /// <summary>
        /// Defines a min, max, or fixed constraint on a blendshape value.
        /// Supports both static (constant) limits and dynamic limits driven by another blendshape.
        ///
        /// ── STATIC LIMIT (most common) ──────────────────────────────────────────────────────────
        ///   Leave <see cref="modulatorBlendshape"/> as empty / (none).
        ///   Only <see cref="limitAtA"/> is used — it becomes the fixed limit value.
        ///   State B fields (modulatorValueA/B, limitAtB) and noExtrapolation are ignored.
        ///
        ///   Example — clamp blendshape to [30, 60]:
        ///     Rule 1 → clampType = Min,  modulatorBlendshape = (none),  limitAtA = 30
        ///     Rule 2 → clampType = Max,  modulatorBlendshape = (none),  limitAtA = 60
        ///
        /// ── DYNAMIC LIMIT (modulator-driven) ────────────────────────────────────────────────────
        ///   Set <see cref="modulatorBlendshape"/> to another blendshape on the same section.
        ///   The limit is linearly interpolated between two known states as the modulator changes:
        ///     State A: when modulator == modulatorValueA  → limit is limitAtA
        ///     State B: when modulator == modulatorValueB  → limit is limitAtB
        ///   Values in between are lerped; values outside are extrapolated (unless noExtrapolation = true).
        ///
        ///   Example — max width shrinks as stenosis increases:
        ///     clampType = Max,  modulator = "stenosis"
        ///     State A: Mod=0,   Limit=80   (no stenosis → max width 80)
        ///     State B: Mod=100, Limit=30   (full stenosis → max width 30)
        ///     noExtrapolation = true  (prevents limit going below 30 if stenosis > 100)
        ///
        /// ── FIXED / LOCKED VALUE ────────────────────────────────────────────────────────────────
        ///   clampType = Fixed locks the blendshape to exactly the computed limit (acts as both min and max).
        ///   Any attempt to set the blendshape is overridden — it cannot move at all.
        ///   Works with both static and dynamic modes.
        ///
        ///   Static Fixed: permanently freezes the blendshape at limitAtA.
        ///     Example → lock aperture to 0 forever: clampType=Fixed, modulator=(none), limitAtA=0
        ///
        ///   Dynamic Fixed: locks the blendshape to a value that itself changes with the modulator.
        ///     Example → aperture must equal stenosis/2:
        ///       clampType=Fixed, modulator="stenosis", StateA=(Mod=0, Limit=0), StateB=(Mod=100, Limit=50)
        ///     The blendshape is always forced to exactly the interpolated value — no user control.
        ///
        /// ── NO EXTRAPOLATION ────────────────────────────────────────────────────────────────────
        ///   Only relevant when a modulator is set.
        ///   true  → limit clamps at limitAtA/limitAtB if modulator goes outside [A, B] range (safe default).
        ///   false → limit keeps changing linearly beyond State A and B (use only if intentional).
        ///   For static limits (no modulator) this field does nothing.
        /// </summary>
        [Serializable]
        public class BlendshapeClampRule
        {
            [Tooltip("Min: blendshape cannot go below the computed limit.\n" +
                     "Max: blendshape cannot go above the computed limit.\n" +
                     "Fixed: blendshape is locked to exactly the computed limit.")]
            public ClampType clampType = ClampType.Min;

            [Tooltip("Optional. Blendshape on the same section whose value drives the limit dynamically.\n" +
                     "Leave empty for a static (constant) limit — only limitAtA (State A → Limit) is used in that case.")]
            public string modulatorBlendshape;

            [Tooltip("Modulator value for state A. Ignored when no modulator is set.")]
            public float modulatorValueA;
            [Tooltip("Limit value when modulator is at state A.\n" +
                     "When no modulator is set, this is the fixed limit (the only field that matters).")]
            public float limitAtA;

            [Tooltip("Modulator value for state B. Ignored when no modulator is set.")]
            public float modulatorValueB;
            [Tooltip("Limit value when modulator is at state B. Ignored when no modulator is set.")]
            public float limitAtB;

            [Tooltip("Only relevant when a modulator blendshape is set.\n" +
                     "true  → limit stops changing once modulator reaches State A or B (recommended).\n" +
                     "false → limit keeps extrapolating linearly beyond those values.\n" +
                     "Has no effect when no modulator is set.")]
            public bool noExtrapolation = false;

            public bool HasModulator => !string.IsNullOrEmpty(modulatorBlendshape);

            public float EvaluateLimit(float modulatorValue)
            {
                float t = Mathf.InverseLerp(modulatorValueA, modulatorValueB, modulatorValue);
                return noExtrapolation
                    ? Mathf.Lerp(limitAtA, limitAtB, t)
                    : Mathf.LerpUnclamped(limitAtA, limitAtB, t);
            }

            public float Apply(float value, float limit) => clampType switch
            {
                ClampType.Min   => Mathf.Max(value, limit),
                ClampType.Max   => Mathf.Min(value, limit),
                ClampType.Fixed => limit,
                _               => value
            };
        }
        
        /// <summary>
        /// Information about a single blendshape in the mesh
        /// </summary>
        [Serializable]
        public class BlendshapeInfo
        {
            public string name;
            public int index;

            [Tooltip("When disabled, this blendshape is excluded from mesh copies at generation time")]
            public bool isEnabled = true;

            [Range(0, 100)]
            [Tooltip("Default value for this blendshape when creating new sections")]
            public float defaultValue;
            
            [Header("Linking Configuration")]
            [Tooltip("Each entry drives another blendshape (same section, previous, or next) when this one changes.")]
            public List<BlendshapeLinkConfig> links = new List<BlendshapeLinkConfig>();

            [Header("Clamp Rules")]
            [Tooltip("Dynamic min/max constraints on this blendshape's own value, driven by another blendshape.")]
            public List<BlendshapeClampRule> clampRules = new List<BlendshapeClampRule>();

            public BlendshapeInfo(string name, int index)
            {
                this.name = name;
                this.index = index;
                this.defaultValue = 0f;
                this.links = new List<BlendshapeLinkConfig>();
                this.clampRules = new List<BlendshapeClampRule>();
            }

            public bool HasLink => links != null && links.Any(l => l.IsValid);
            public bool HasClampRules => clampRules != null && clampRules.Count > 0;
        }
        
        public enum MeshType
        {
            Default,
            Rectum,
            Cecum,
            Custom
        }
        
        /// <summary>
        /// A blendshape panel for UI display. Contains 1-3 blendshapes and maps to a slider prefab based on count.
        /// </summary>
        [Serializable]
        public class BlendshapePanel
        {
            [Tooltip("Display title for this panel in the UI")]
            public LocalizedString panelTitle;
            
            [Tooltip("Names of blendshapes in this panel (1-3 blendshapes supported)")]
            public List<string> blendshapeNames = new List<string>();
            
            public int Count => blendshapeNames?.Count ?? 0;
            
            public BlendshapePanel()
            {
                blendshapeNames = new List<string>();
            }
            
            public BlendshapePanel(params string[] names)
            {
                // Note: panelTitle should be configured in inspector with localization table reference
                blendshapeNames = new List<string>(names);
            }
        }
        
        /// <summary>
        /// Refreshes the blendshape list from the assigned mesh.
        /// Preserves existing values and settings for blendshapes that still exist.
        /// </summary>
        public void RefreshFromMesh()
        {
            if (mesh == null)
            {
                Debug.LogWarning($"Cannot refresh {name}: No mesh assigned.");
                isValid = false;
                return;
            }
            
            // Store existing blendshape data
            var existingData = new Dictionary<string, BlendshapeInfo>();
            foreach (var bs in blendshapes)
            {
                existingData[bs.name] = bs;
            }
            
            // Clear and rebuild
            blendshapes.Clear();
            
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string blendshapeName = mesh.GetBlendShapeName(i);
                
                // Restore existing data if available, otherwise create new
                if (existingData.TryGetValue(blendshapeName, out var existing))
                {
                    existing.index = i; // Update index in case of reordering
                    blendshapes.Add(existing);
                }
                else
                {
                    blendshapes.Add(new BlendshapeInfo(blendshapeName, i));
                }
            }
            
            isValid = true;
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            
            Debug.Log($"Refreshed {name}: Found {blendshapes.Count} blendshapes.");
        }
        
        /// <summary>
        /// Validates that the mesh profile matches the current mesh state
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();
            
            if (mesh == null)
            {
                result.AddError("No mesh assigned to profile");
                isValid = false;
                return result;
            }
            
            // Check if blendshape count matches
            if (blendshapes.Count != mesh.blendShapeCount)
            {
                result.AddWarning($"Blendshape count mismatch: Profile has {blendshapes.Count}, mesh has {mesh.blendShapeCount}. Consider refreshing.");
            }
            
            // Check if blendshape names and indices match
            for (int i = 0; i < Mathf.Min(blendshapes.Count, mesh.blendShapeCount); i++)
            {
                string meshBlendshapeName = mesh.GetBlendShapeName(i);
                var profileBlendshape = blendshapes.FirstOrDefault(b => b.index == i);
                
                if (profileBlendshape == null)
                {
                    result.AddWarning($"Missing blendshape at index {i} in profile");
                    continue;
                }
                
                if (profileBlendshape.name != meshBlendshapeName)
                {
                    result.AddWarning($"Blendshape name mismatch at index {i}: Profile='{profileBlendshape.name}', Mesh='{meshBlendshapeName}'");
                }
            }
            
            isValid = result.errors.Count == 0;
            return result;
        }
        
        /// <summary>
        /// Gets a blendshape by name
        /// </summary>
        public BlendshapeInfo GetBlendshape(string name)
        {
            return blendshapes.FirstOrDefault(b => b.name == name);
        }
        
        /// <summary>
        /// Gets a blendshape by index
        /// </summary>
        public BlendshapeInfo GetBlendshapeByIndex(int index)
        {
            return blendshapes.FirstOrDefault(b => b.index == index);
        }
        
        /// <summary>
        /// Creates default blendshape values dictionary
        /// </summary>
        public Dictionary<string, float> CreateDefaultValues()
        {
            var defaults = new Dictionary<string, float>();
            foreach (var bs in blendshapes)
            {
                defaults[bs.name] = bs.defaultValue;
            }
            return defaults;
        }
        
        /// <summary>
        /// Gets blendshapes that are not assigned to any panel
        /// </summary>
        public List<BlendshapeInfo> GetUngroupedBlendshapes()
        {
            var groupedNames = new HashSet<string>();
            foreach (var panel in blendshapePanels)
            {
                if (panel.blendshapeNames != null)
                {
                    foreach (var name in panel.blendshapeNames)
                    {
                        groupedNames.Add(name);
                    }
                }
            }
            
            return blendshapes.Where(b => !groupedNames.Contains(b.name)).ToList();
        }
        
        /// <summary>
        /// Auto-generates panels from blendshapes based on naming patterns.
        /// Groups blendshapes with same base name (e.g., width_superior, width_inferior) into panels
        /// </summary>
        public void AutoGeneratePanels()
        {
            blendshapePanels.Clear();
            
            // Group blendshapes by base name (before _sup/_inf/_superior/_inferior suffix)
            var groupedByBase = new Dictionary<string, List<string>>();
            
            foreach (var bs in blendshapes)
            {
                string baseName = GetBaseBlendshapeName(bs.name);
                
                if (!groupedByBase.ContainsKey(baseName))
                {
                    groupedByBase[baseName] = new List<string>();
                }
                groupedByBase[baseName].Add(bs.name);
            }
            
            // Create panels from the dictionary
            foreach (var kvp in groupedByBase.OrderBy(k => k.Key))
            {
                // Note: Panel titles should be configured manually in inspector with localization table references
                // Auto-generation creates panels without titles - set them up afterwards
                var panel = new BlendshapePanel
                {
                    // panelTitle left empty - configure in inspector with localization reference
                    blendshapeNames = kvp.Value.OrderBy(n => n).ToList()
                };
                blendshapePanels.Add(panel);
            }
        }
        
        private string GetBaseBlendshapeName(string name)
        {
            // Remove common suffixes to get base name
            string[] suffixes = { "_superior", "_inferior", "_sup", "_inf", "_top", "_bottom", "_left", "_right" };
            
            string lower = name.ToLower();
            foreach (var suffix in suffixes)
            {
                if (lower.EndsWith(suffix))
                {
                    return name.Substring(0, name.Length - suffix.Length);
                }
            }
            
            return name;
        }
        
        private string FormatPanelTitle(string baseName)
        {
            // Replace underscores with spaces and capitalize words
            var words = baseName.Split('_');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }
            return string.Join(" ", words);
        }
        
        /// <summary>
        /// Returns only blendshapes that have isEnabled = true.
        /// </summary>
        public List<BlendshapeInfo> GetEnabledBlendshapes()
        {
            return blendshapes.Where(b => b.isEnabled).ToList();
        }

        /// <summary>
        /// Returns a copy of the mesh containing only enabled blendshapes.
        /// If all blendshapes are enabled the original mesh is returned without copying.
        /// The caller is responsible for destroying the copy when it is no longer needed.
        /// </summary>
        public Mesh CreateFilteredMesh()
        {
            if (mesh == null) return null;

            var enabled = GetEnabledBlendshapes();

            // Nothing to filter — return original directly
            if (enabled.Count == mesh.blendShapeCount)
                return mesh;

            var filtered = UnityEngine.Object.Instantiate(mesh);
            //var filtered = new Mesh();
            //MeshUtils.CopyMesh(filtered, mesh, true);
            filtered.name = mesh.name + "_filtered";
            filtered.ClearBlendShapes();

            int vc = mesh.vertexCount;
            var dv = new Vector3[vc];
            var dn = new Vector3[vc];
            var dt = new Vector3[vc];

            foreach (var bs in enabled)
            {
                if (bs.index >= mesh.blendShapeCount) continue;
                int frames = mesh.GetBlendShapeFrameCount(bs.index);
                for (int f = 0; f < frames; f++)
                {
                    float fw = mesh.GetBlendShapeFrameWeight(bs.index, f);
                    mesh.GetBlendShapeFrameVertices(bs.index, f, dv, dn, dt);
                    filtered.AddBlendShapeFrame(bs.name, fw, dv, dn, dt);
                }
            }

            return filtered;
        }

        private void OnValidate()
        {
            if (mesh != null && blendshapes.Count != mesh.blendShapeCount)
                isValid = false;
        }
    }
    
    /// <summary>
    /// Result of validation operations
    /// </summary>
    [Serializable]
    public class ValidationResult
    {
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> info = new List<string>();
        
        public bool IsValid => errors.Count == 0;
        public bool HasWarnings => warnings.Count > 0;
        public bool HasInfo => info.Count > 0;
        
        public void AddError(string message)
        {
            errors.Add(message);
        }
        
        public void AddWarning(string message)
        {
            warnings.Add(message);
        }
        
        public void AddInfo(string message)
        {
            info.Add(message);
        }
        
        public void Merge(ValidationResult other)
        {
            errors.AddRange(other.errors);
            warnings.AddRange(other.warnings);
            info.AddRange(other.info);
        }
        
        public override string ToString()
        {
            var parts = new List<string>();
            
            if (errors.Count > 0)
                parts.Add($"Errors ({errors.Count}): {string.Join(", ", errors)}");
            
            if (warnings.Count > 0)
                parts.Add($"Warnings ({warnings.Count}): {string.Join(", ", warnings)}");
            
            if (info.Count > 0)
                parts.Add($"Info ({info.Count}): {string.Join(", ", info)}");
            
            return parts.Count > 0 ? string.Join(" | ", parts) : "Valid";
        }
    }
}
