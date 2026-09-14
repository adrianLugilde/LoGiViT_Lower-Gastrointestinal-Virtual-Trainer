// ============================================================================
// BlendshapeUpdateService.cs
// 
// Service for data-driven blendshape management on the large intestine model.
// Handles automatic linking between adjacent sections and global blendshapes.
// 
// Key Features:
//   - Linked blendshapes: Changes propagate to adjacent sections automatically
//   - Global blendshapes: Single control affects all sections (e.g., width)
//   - Configuration-driven: Reads blendshape metadata from mesh profiles
// 
// Usage:
//   var service = new BlendshapeUpdateService(modelGenerator, config);
//   service.UpdateBlendshape(sectionIndex, "BlendshapeName", value);
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ModelEditor;

namespace LargeIntestine
{
    /// <summary>
    /// Service for managing blendshape updates across model sections.
    /// Handles linked blendshapes (adjacent section sync) and global blendshapes.
    /// </summary>
    /// <remarks>
    /// <para>This service is configuration-driven:</para>
    /// <list type="bullet">
    ///   <item>Blendshape metadata comes from <see cref="SplineMeshProfile"/></item>
    ///   <item>Links define which blendshapes sync with adjacent sections</item>
    ///   <item>Global blendshapes apply the same value to all sections</item>
    /// </list>
    /// </remarks>
    public class BlendshapeUpdateService
    {
        #region Dependencies

        private readonly ISplineModelGenerator _modelGenerator;
        private readonly IGenerationConfiguration _config;
        private readonly Dictionary<int, Dictionary<string, int>> _sectionBlendshapeIndices = new();

        /// <summary>
        /// Fired when a blendshape value is corrected by clamp revalidation (not by a direct slider update).
        /// Parameters: sectionIndex, blendshapeName, correctedValue.
        /// </summary>
        public event System.Action<int, string, float> OnBlendshapeRevalidated;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new BlendshapeUpdateService.
        /// </summary>
        /// <param name="modelGenerator">The model generator with section data</param>
        /// <param name="config">The configuration with blendshape metadata</param>
        public BlendshapeUpdateService(ISplineModelGenerator modelGenerator, IGenerationConfiguration config)
        {
            _modelGenerator = modelGenerator;
            _config = config;
        }

        /// <summary>
        /// Clears the cached runtime blendshape index maps.
        /// Call this when sections are regenerated or meshes are replaced.
        /// </summary>
        public void InvalidateRuntimeIndexCache()
        {
            _sectionBlendshapeIndices.Clear();
        }
        
        #endregion
        
        #region Blendshape Updates
        
        /// <summary>
        /// Updates a blendshape on the specified section.
        /// Automatically handles linked blendshapes and global application.
        /// </summary>
        /// <param name="sectionIndex">Index of the section to update (0-based)</param>
        /// <param name="blendshapeName">Name of the blendshape to update</param>
        /// <param name="value">New value for the blendshape (0-100)</param>
        public void UpdateBlendshape(int sectionIndex, string blendshapeName, float value)
        {
            value = Mathf.Clamp(value, 0f, 100f);

            var sections = _modelGenerator.Sections;

            // Validate section index
            if (sectionIndex < 0 || sectionIndex >= sections.Count)
            {
                Debug.LogWarning($"BlendshapeUpdateService: Invalid section index {sectionIndex}");
                return;
            }

            // Global blendshapes apply to all sections
            if (IsGlobalBlendshape(blendshapeName))
            {
                ApplyGlobalBlendshape(blendshapeName, value);
                return;
            }
            
            // Get section configuration and mesh profile
            var sectionConfig = GetSectionConfiguration(sectionIndex);
            if (sectionConfig == null)
            {
                Debug.LogWarning($"BlendshapeUpdateService: No configuration for section {sectionIndex}");
                return;
            }
            
            var profile = sectionConfig.MeshProfile;
            if (profile == null)
            {
                Debug.LogWarning($"BlendshapeUpdateService: No mesh profile for section {sectionIndex}");
                return;
            }
            
            // Get blendshape metadata from profile
            var blendshapeInfo = profile.GetBlendshape(blendshapeName);
            if (blendshapeInfo == null)
            {
                Debug.LogWarning($"BlendshapeUpdateService: Blendshape '{blendshapeName}' not found in profile");
                return;
            }
            
            // Apply dynamic clamp rules defined in the profile
            if (blendshapeInfo.HasClampRules)
                value = ApplyClampRules(sectionIndex, blendshapeInfo, value);

            // Hard clamp to valid blendshape range (link formulas can produce out-of-range values)
            value = Mathf.Clamp(value, 0f, 100f);

            // Apply to the current section's renderer — look up index by name on the
            // actual mesh so filtered meshes (with shifted indices) are handled correctly.
            var renderer = sections[sectionIndex].Renderer;
            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                int runtimeIndex = GetBlendshapeIndex(sectionIndex, blendshapeName);
                if (runtimeIndex >= 0)
                    skinnedRenderer.SetBlendShapeWeight(runtimeIndex, value);
            }
            
            // Propagate to all configured link targets
            if (blendshapeInfo.HasLink)
            {
                foreach (var link in blendshapeInfo.links.Where(l => l.IsValid))
                    ApplyLinkedBlendshape(sectionIndex, link, value);
            }

            // Re-validate blendshapes whose clamp rules reference this blendshape as a modulator
            RevalidateClampedBy(sectionIndex, blendshapeName);
        }
        
        /// <summary>
        /// Updates a blendshape on multiple sections simultaneously.
        /// </summary>
        /// <param name="sectionIndices">Indices of sections to update</param>
        /// <param name="blendshapeName">Name of the blendshape to update</param>
        /// <param name="value">New value for the blendshape (0-100)</param>
        public void UpdateBlendshapeMultiple(IEnumerable<int> sectionIndices, string blendshapeName, float value)
        {
            foreach (var sectionIndex in sectionIndices)
            {
                UpdateBlendshape(sectionIndex, blendshapeName, value);
            }
        }
        
        #endregion
        
        #region Linked Blendshapes
        
        /// <summary>
        /// Applies a linked blendshape to an adjacent section.
        /// Links are defined in the mesh profile and sync values across section boundaries.
        /// </summary>
        /// <param name="currentIndex">Index of the section being edited</param>
        /// <param name="blendshapeInfo">Blendshape info with link configuration</param>
        /// <param name="value">Value to apply</param>
        private void ApplyLinkedBlendshape(int currentIndex, SplineMeshProfile.BlendshapeLinkConfig link, float value)
        {
            var sections = _modelGenerator.Sections;

            // Resolve target section index
            int adjacentIndex = link.direction switch
            {
                SplineMeshProfile.LinkDirection.Previous => currentIndex - 1,
                SplineMeshProfile.LinkDirection.Next     => currentIndex + 1,
                SplineMeshProfile.LinkDirection.Same     => currentIndex,
                _                                        => currentIndex
            };

            if (adjacentIndex < 0 || adjacentIndex >= sections.Count)
                return;

            // Get the target section's profile
            var adjacentConfig = GetSectionConfiguration(adjacentIndex);
            if (adjacentConfig?.MeshProfile == null)
                return;

            // Find the target blendshape in that profile
            var linkedBlendshapeInfo = adjacentConfig.MeshProfile.GetBlendshape(link.linkedBlendshapeName);
            if (linkedBlendshapeInfo == null)
            {
                Debug.LogWarning($"BlendshapeUpdateService: Linked blendshape '{link.linkedBlendshapeName}' not found in section {adjacentIndex}");
                return;
            }

            float effectiveOffset = link.linkOffset;
            if (link.HasOffsetModulator)
            {
                int modIdx = GetBlendshapeIndex(currentIndex, link.offsetModulatorBlendshape);
                if (modIdx >= 0 && sections[currentIndex].Renderer is SkinnedMeshRenderer modSmr)
                    effectiveOffset = link.EvaluateOffset(modSmr.GetBlendShapeWeight(modIdx));
            }
            float scaledValue = Mathf.Clamp(value * link.linkScale + effectiveOffset, 0f, 100f);

            if (link.chainLinks)
                UpdateBlendshapeChained(adjacentIndex, link.linkedBlendshapeName, scaledValue);
            else
            {
                var adjacentRenderer = sections[adjacentIndex].Renderer;
                if (adjacentRenderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    int runtimeIndex = GetBlendshapeIndex(adjacentIndex, link.linkedBlendshapeName);
                    if (runtimeIndex >= 0)
                        skinnedRenderer.SetBlendShapeWeight(runtimeIndex, scaledValue);
                }
            }
        }
        
        #endregion
        
        #region Global Blendshapes
        
        /// <summary>
        /// Checks if a blendshape is configured as global (applies to all sections).
        /// </summary>
        /// <param name="blendshapeName">Name of the blendshape to check</param>
        /// <returns>True if this is a global blendshape</returns>
        private bool IsGlobalBlendshape(string blendshapeName)
        {
            return !string.IsNullOrEmpty(_config.GlobalWidthBlendshapeName)
                   && _config.GlobalWidthBlendshapeName == blendshapeName;
        }
        
        /// <summary>
        /// Applies a blendshape value to all sections that have it.
        /// </summary>
        /// <param name="blendshapeName">Name of the blendshape to apply</param>
        /// <param name="value">Value to set (0-100)</param>
        private void ApplyGlobalBlendshape(string blendshapeName, float value)
        {
            var sections = _modelGenerator.Sections;
            var sectionConfigs = GetAllSectionConfigurations();
            
            for (int i = 0; i < sections.Count && i < sectionConfigs.Count; i++)
            {
                var config = sectionConfigs[i];
                if (config?.MeshProfile == null)
                    continue;
                
                var blendshapeInfo = config.MeshProfile.GetBlendshape(blendshapeName);
                if (blendshapeInfo == null) continue;

                if (sections[i].Renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    int runtimeIndex = GetBlendshapeIndex(i, blendshapeName);
                    if (runtimeIndex >= 0)
                        skinnedRenderer.SetBlendShapeWeight(runtimeIndex, value);
                }

                if (blendshapeInfo.HasLink)
                {
                    foreach (var link in blendshapeInfo.links.Where(l => l.IsValid))
                        ApplyLinkedBlendshape(i, link, value);
                }

                RevalidateClampedBy(i, blendshapeName);
            }
        }

        #endregion

        #region Blendshape Queries
        
        /// <summary>
        /// Gets the current value of a blendshape on a section.
        /// </summary>
        /// <param name="sectionIndex">Index of the section</param>
        /// <param name="blendshapeName">Name of the blendshape</param>
        /// <returns>Current value (0-100), or 0 if not found</returns>
        public float GetBlendshapeValue(int sectionIndex, string blendshapeName)
        {
            var sections = _modelGenerator.Sections;
            if (sectionIndex < 0 || sectionIndex >= sections.Count)
                return 0f;
            
            var sectionConfig = GetSectionConfiguration(sectionIndex);
            if (sectionConfig?.MeshProfile == null)
                return 0f;
            
            var blendshapeInfo = sectionConfig.MeshProfile.GetBlendshape(blendshapeName);
            if (blendshapeInfo == null)
                return 0f;
            
            if (sections[sectionIndex].Renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                int runtimeIndex = GetBlendshapeIndex(sectionIndex, blendshapeName);
                if (runtimeIndex >= 0)
                    return skinnedRenderer.GetBlendShapeWeight(runtimeIndex);
            }
            return 0f;
        }
        
        /// <summary>
        /// Gets all blendshape values for a section as a dictionary.
        /// </summary>
        /// <param name="sectionIndex">Index of the section</param>
        /// <returns>Dictionary mapping blendshape names to values</returns>
        public Dictionary<string, float> GetAllBlendshapeValues(int sectionIndex)
        {
            var result = new Dictionary<string, float>();
            var sections = _modelGenerator.Sections;
            
            if (sectionIndex < 0 || sectionIndex >= sections.Count)
                return result;
            
            var sectionConfig = GetSectionConfiguration(sectionIndex);
            if (sectionConfig?.MeshProfile == null)
                return result;
            
            // Collect values for all blendshapes in the profile
            var renderer = sections[sectionIndex].Renderer;
            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                foreach (var bs in sectionConfig.MeshProfile.blendshapes)
                {
                    int runtimeIndex = GetBlendshapeIndex(sectionIndex, bs.name);
                    if (runtimeIndex >= 0)
                        result[bs.name] = skinnedRenderer.GetBlendShapeWeight(runtimeIndex);
                }
            }
            
            return result;
        }
        
        #endregion
        
        /// <summary>
        /// Applies a blendshape and fires its links recursively.
        /// Used only when propagating from a global blendshape.
        /// </summary>
        private void UpdateBlendshapeChained(int sectionIndex, string blendshapeName, float value)
        {
            var sections = _modelGenerator.Sections;
            if (sectionIndex < 0 || sectionIndex >= sections.Count) return;

            var profile = GetSectionConfiguration(sectionIndex)?.MeshProfile;
            if (profile == null) return;

            value = Mathf.Clamp(value, 0f, 100f);
            if (sections[sectionIndex].Renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                int runtimeIndex = GetBlendshapeIndex(sectionIndex, blendshapeName);
                if (runtimeIndex >= 0)
                    skinnedRenderer.SetBlendShapeWeight(runtimeIndex, value);
            }

            var blendshapeInfo = profile.GetBlendshape(blendshapeName);
            if (blendshapeInfo != null && blendshapeInfo.HasLink)
            {
                foreach (var link in blendshapeInfo.links.Where(l => l.IsValid))
                    ApplyLinkedBlendshape(sectionIndex, link, value);
            }
        }

        /// <summary>
        /// Re-evaluates clamp rules for any blendshape in the section whose clamp rules
        /// reference <paramref name="changedBlendshapeName"/> as a modulator.
        /// Only corrects values that are now out of range — valid values are left untouched.
        /// </summary>
        private void RevalidateClampedBy(int sectionIndex, string changedBlendshapeName)
        {
            var sections = _modelGenerator.Sections;
            var profile = GetSectionConfiguration(sectionIndex)?.MeshProfile;
            if (profile == null) return;

            foreach (var bs in profile.blendshapes)
            {
                if (!bs.HasClampRules) continue;
                if (!bs.clampRules.Any(r => r.HasModulator && r.modulatorBlendshape == changedBlendshapeName))
                    continue;
                if (sections[sectionIndex].Renderer is not SkinnedMeshRenderer smr) continue;

                int idx = GetBlendshapeIndex(sectionIndex, bs.name);
                if (idx < 0) continue;

                float current = smr.GetBlendShapeWeight(idx);
                float clamped = Mathf.Clamp(ApplyClampRules(sectionIndex, bs, current), 0f, 100f);
                if (!Mathf.Approximately(current, clamped))
                {
                    smr.SetBlendShapeWeight(idx, clamped);
                    OnBlendshapeRevalidated?.Invoke(sectionIndex, bs.name, clamped);

                    if (bs.HasLink)
                    {
                        foreach (var link in bs.links.Where(l => l.IsValid))
                            ApplyLinkedBlendshape(sectionIndex, link, clamped);
                    }
                }
            }
        }

        private float ApplyClampRules(int sectionIndex, SplineMeshProfile.BlendshapeInfo blendshapeInfo, float value)
        {
            var sections = _modelGenerator.Sections;
            if (sections[sectionIndex].Renderer is not SkinnedMeshRenderer smr)
                return value;

            foreach (var rule in blendshapeInfo.clampRules)
            {
                float limit;
                if (rule.HasModulator)
                {
                    int modIdx = GetBlendshapeIndex(sectionIndex, rule.modulatorBlendshape);
                    if (modIdx < 0) continue;
                    limit = rule.EvaluateLimit(smr.GetBlendShapeWeight(modIdx));
                }
                else
                {
                    limit = rule.limitAtA; // static limit — no modulator
                }
                value = rule.Apply(value, limit);
            }
            return value;
        }

        #region Runtime Index Cache

        private int GetBlendshapeIndex(int sectionIndex, string blendshapeName)
        {
            if (!_sectionBlendshapeIndices.TryGetValue(sectionIndex, out var map))
            {
                map = new Dictionary<string, int>();
                var sections = _modelGenerator.Sections;
                if (sectionIndex >= 0 && sectionIndex < sections.Count &&
                    sections[sectionIndex].Renderer is SkinnedMeshRenderer smr)
                {
                    for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                        map[smr.sharedMesh.GetBlendShapeName(i)] = i;
                }
                _sectionBlendshapeIndices[sectionIndex] = map;
            }
            return map.TryGetValue(blendshapeName, out var idx) ? idx : -1;
        }

        #endregion

        #region Profile Queries
        
        /// <summary>
        /// Gets the mesh profile for a given section index.
        /// </summary>
        /// <param name="sectionIndex">Index of the section</param>
        /// <returns>The mesh profile, or null if not found</returns>
        public SplineMeshProfile GetProfileForSection(int sectionIndex)
        {
            return GetSectionConfiguration(sectionIndex)?.MeshProfile;
        }
        
        /// <summary>
        /// Gets blendshapes that are common across all selected sections.
        /// Useful for multi-selection editing UI.
        /// </summary>
        /// <param name="sectionIndices">Indices of selected sections</param>
        /// <returns>List of blendshapes present in all selected sections</returns>
        public List<SplineMeshProfile.BlendshapeInfo> GetCommonBlendshapes(IEnumerable<int> sectionIndices)
        {
            var indexList = sectionIndices.ToList();
            if (indexList.Count == 0)
                return new List<SplineMeshProfile.BlendshapeInfo>();
            
            // Start with the first profile's blendshapes
            var firstProfile = GetProfileForSection(indexList[0]);
            if(firstProfile == null)
            {
                Debug.LogWarning($"BlendshapeUpdateService: Section {indexList[0]} has no profile");
                return new List<SplineMeshProfile.BlendshapeInfo>();
            }
            if(firstProfile.blendshapes == null || firstProfile.blendshapes.Count == 0)
            {
                Debug.LogWarning($"BlendshapeUpdateService: Section {indexList[0]} has no blendshapes");
                return new List<SplineMeshProfile.BlendshapeInfo>();
            }
            if (firstProfile == null || firstProfile.blendshapes == null || firstProfile.blendshapes.Count == 0)
            {
                Debug.LogWarning($"BlendshapeUpdateService: Section {indexList[0]} has no profile or no blendshapes");
                return new List<SplineMeshProfile.BlendshapeInfo>();
            }
            
            var commonNames = new HashSet<string>(firstProfile.blendshapes.Select(b => b.name));
            
            // Intersect with blendshapes from all other selected sections
            foreach (var index in indexList.Skip(1))
            {
                var profile = GetProfileForSection(index);
                if (profile == null || profile.blendshapes == null)
                    continue;
                
                var profileNames = new HashSet<string>(profile.blendshapes.Select(b => b.name));
                commonNames.IntersectWith(profileNames);
            }
            
            // Return blendshape infos from the first profile for common names
            return firstProfile.blendshapes
                .Where(b => commonNames.Contains(b.name))
                .ToList();
        }
        
        /// <summary>
        /// Gets all unique mesh profiles for the given section indices.
        /// </summary>
        /// <param name="sectionIndices">Indices of sections to query</param>
        /// <returns>List of distinct mesh profiles</returns>
        public List<SplineMeshProfile> GetUniqueProfilesForSections(IEnumerable<int> sectionIndices)
        {
            return sectionIndices
                .Select(idx => GetProfileForSection(idx))
                .Where(p => p != null)
                .Distinct()
                .ToList();
        }
        
        /// <summary>
        /// Collects all blendshape panels from multiple mesh profiles.
        /// Avoids duplicates by panel title (first occurrence wins).
        /// </summary>
        /// <param name="meshProfiles">Profiles to collect panels from</param>
        /// <returns>List of unique blendshape panels</returns>
        public List<SplineMeshProfile.BlendshapePanel> CollectPanelsFromProfiles(IEnumerable<SplineMeshProfile> meshProfiles)
        {
            var allPanels = new List<SplineMeshProfile.BlendshapePanel>();
            var seenPanelTitles = new HashSet<string>();
            
            foreach (var profile in meshProfiles)
            {
                if (profile?.blendshapePanels == null)
                    continue;
                
                foreach (var panel in profile.blendshapePanels)
                {
                    // Use table entry reference as stable identifier for deduplication
                    string panelKey = panel.panelTitle?.TableEntryReference.ToString();
                    if (string.IsNullOrEmpty(panelKey))
                        continue;
                    
                    // Skip duplicate panels by key
                    if (seenPanelTitles.Contains(panelKey))
                        continue;
                    
                    seenPanelTitles.Add(panelKey);
                    allPanels.Add(panel);
                }
            }
            
            return allPanels;
        }
        
        /// <summary>
        /// Collects all blendshape panels for the given section indices.
        /// Convenience method combining GetUniqueProfilesForSections and CollectPanelsFromProfiles.
        /// </summary>
        /// <param name="sectionIndices">Indices of sections</param>
        /// <returns>List of unique blendshape panels</returns>
        public List<SplineMeshProfile.BlendshapePanel> GetPanelsForSections(IEnumerable<int> sectionIndices)
        {
            var profiles = GetUniqueProfilesForSections(sectionIndices);
            return CollectPanelsFromProfiles(profiles);
        }
        
        #endregion
        
        #region Configuration Helpers
        
        /// <summary>
        /// Gets the section configuration for a given index by iterating through segments.
        /// </summary>
        /// <param name="sectionIndex">Flat index of the section</param>
        /// <returns>Section configuration, or null if not found</returns>
        private IModelSectionConfiguration GetSectionConfiguration(int sectionIndex)
        {
            return _config.GetSectionConfiguration(sectionIndex);
        }
        
        /// <summary>
        /// Gets all section configurations as a flat list.
        /// </summary>
        /// <returns>List of all section configurations</returns>
        private List<IModelSectionConfiguration> GetAllSectionConfigurations()
        {
            return _config.GetAllSectionConfigurations();
        }
        
        #endregion
    }
}
