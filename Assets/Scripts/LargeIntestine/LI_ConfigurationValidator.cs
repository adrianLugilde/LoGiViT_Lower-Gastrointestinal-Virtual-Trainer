using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LargeIntestine
{
    /// <summary>
    /// Utility class for validating large intestine configurations
    /// </summary>
    public static class LI_ConfigurationValidator
    {
        /// <summary>
        /// Validates an entire generation configuration
        /// </summary>
        public static ValidationResult ValidateConfiguration(LI_GenerationConfiguration config)
        {
            var result = new ValidationResult();
            
            if (config == null)
            {
                result.AddError("Configuration is null");
                return result;
            }
            
            if (config.intestineSegments == null || config.intestineSegments.Count == 0)
            {
                result.AddError("Configuration has no segments");
                return result;
            }
            
            // Validate each segment
            foreach (var segment in config.intestineSegments)
            {
                var segmentResult = ValidateSegment(segment);
                if (!segmentResult.IsValid || segmentResult.HasWarnings)
                {
                    result.AddInfo($"Segment {segment.liSegment}:");
                    result.Merge(segmentResult);
                }
            }
            
            // Check for duplicate segments
            var segmentTypes = config.intestineSegments.Select(s => s.liSegment).ToList();
            var duplicates = segmentTypes.GroupBy(s => s)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            
            foreach (var duplicate in duplicates)
            {
                result.AddWarning($"Duplicate segment type: {duplicate}");
            }
            
            // Check segment order
            var expectedOrder = new[] 
            { 
                LI_Segment.Rectum, 
                LI_Segment.Sigmoid, 
                LI_Segment.Descending, 
                LI_Segment.Transverse, 
                LI_Segment.Ascending, 
                LI_Segment.Cecum 
            };
            
            bool orderCorrect = true;
            int lastIndex = -1;
            
            foreach (var segment in config.intestineSegments)
            {
                int currentIndex = System.Array.IndexOf(expectedOrder, segment.liSegment);
                if (currentIndex <= lastIndex)
                {
                    orderCorrect = false;
                    break;
                }
                lastIndex = currentIndex;
            }
            
            if (!orderCorrect)
            {
                result.AddWarning("Segments are not in anatomical order (Rectum → Sigmoid → Descending → Transverse → Ascending → Cecum)");
            }
            
            return result;
        }
        
        /// <summary>
        /// Validates a single segment configuration
        /// </summary>
        public static ValidationResult ValidateSegment(LI_SegmentConfiguration segment)
        {
            var result = new ValidationResult();
            
            if (segment == null)
            {
                result.AddError("Segment is null");
                return result;
            }
            
            if (segment.liSections == null || segment.liSections.Count == 0)
            {
                result.AddError($"Segment {segment.liSegment} has no sections");
                return result;
            }
            
            // Validate each section
            for (int i = 0; i < segment.liSections.Count; i++)
            {
                var sectionResult = ValidateSection(segment.liSections[i]);
                if (!sectionResult.IsValid || sectionResult.HasWarnings)
                {
                    result.AddInfo($"  Section {i}:");
                    result.Merge(sectionResult);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Validates a single section configuration
        /// </summary>
        public static ValidationResult ValidateSection(LI_SectionConfiguration section)
        {
            var result = new ValidationResult();
            
            if (section == null)
            {
                result.AddError("Section is null");
                return result;
            }
            
            // Use the section's own validation method
            var sectionResult = section.Validate();
            result.Merge(sectionResult);
            
            return result;
        }
        
        /// <summary>
        /// Gets a summary report of the configuration
        /// </summary>
        public static string GetConfigurationSummary(LI_GenerationConfiguration config)
        {
            if (config == null)
                return "Configuration is null";
            
            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"Configuration: {config.FileName ?? "Unnamed"}");
            summary.AppendLine($"File ID: {config.FileID}");
            summary.AppendLine($"Total Segments: {config.intestineSegments?.Count ?? 0}");
            summary.AppendLine();
            
            if (config.intestineSegments != null)
            {
                int totalSections = 0;
                var meshProfiles = new HashSet<string>();
                var materials = new HashSet<string>();
                
                foreach (var segment in config.intestineSegments)
                {
                    int sectionCount = segment.liSections?.Count ?? 0;
                    totalSections += sectionCount;
                    summary.AppendLine($"  {segment.liSegment}: {sectionCount} sections");
                    
                    if (segment.liSections != null)
                    {
                        foreach (var section in segment.liSections)
                        {
                            if (section.meshProfile != null)
                            {
                                meshProfiles.Add(section.meshProfile.name);
                            }
                            
                            var mats = section.GetMaterials();
                            if (mats != null)
                            {
                                foreach (var mat in mats)
                                {
                                    if (mat != null)
                                        materials.Add(mat.name);
                                }
                            }
                        }
                    }
                }
                
                summary.AppendLine();
                summary.AppendLine($"Total Sections: {totalSections}");
                summary.AppendLine($"Unique Mesh Profiles: {meshProfiles.Count}");
                if (meshProfiles.Count > 0)
                {
                    summary.AppendLine($"  Profiles: {string.Join(", ", meshProfiles)}");
                }
                
                summary.AppendLine($"Unique Materials: {materials.Count}");
                if (materials.Count > 0)
                {
                    summary.AppendLine($"  Materials: {string.Join(", ", materials)}");
                }
            }
            
            return summary.ToString();
        }
        
        /// <summary>
        /// Finds all mesh profiles used in a configuration
        /// </summary>
        public static List<SplineMeshProfile> GetUsedMeshProfiles(LI_GenerationConfiguration config)
        {
            var profiles = new HashSet<SplineMeshProfile>();
            
            if (config?.intestineSegments != null)
            {
                foreach (var segment in config.intestineSegments)
                {
                    if (segment.liSections != null)
                    {
                        foreach (var section in segment.liSections)
                        {
                            if (section.meshProfile != null)
                            {
                                profiles.Add(section.meshProfile);
                            }
                        }
                    }
                }
            }
            
            return profiles.ToList();
        }
        
        /// <summary>
        /// Finds all materials used in a configuration
        /// </summary>
        public static List<Material> GetUsedMaterials(LI_GenerationConfiguration config)
        {
            var materials = new HashSet<Material>();
            
            if (config?.intestineSegments != null)
            {
                foreach (var segment in config.intestineSegments)
                {
                    if (segment.liSections != null)
                    {
                        foreach (var section in segment.liSections)
                        {
                            var mats = section.GetMaterials();
                            if (mats != null)
                            {
                                foreach (var mat in mats)
                                {
                                    if (mat != null)
                                        materials.Add(mat);
                                }
                            }
                        }
                    }
                }
            }
            
            return materials.ToList();
        }
        
        /// <summary>
        /// Checks for missing references in the configuration
        /// </summary>
        public static ValidationResult CheckMissingReferences(LI_GenerationConfiguration config)
        {
            var result = new ValidationResult();
            
            if (config?.intestineSegments == null)
            {
                result.AddError("Configuration has no segments");
                return result;
            }
            
            int sectionsWithoutProfile = 0;
            int sectionsWithoutMesh = 0;
            int sectionsWithoutMaterials = 0;
            
            foreach (var segment in config.intestineSegments)
            {
                if (segment.liSections == null)
                    continue;
                
                foreach (var section in segment.liSections)
                {
                    if (section.meshProfile == null)
                    {
                        sectionsWithoutProfile++;
                    }
                    else if (section.meshProfile.mesh == null)
                    {
                        sectionsWithoutMesh++;
                    }
                    
                    var mats = section.GetMaterials();
                    if (mats == null || mats.Length == 0 || mats.Any(m => m == null))
                    {
                        sectionsWithoutMaterials++;
                    }
                }
            }
            
            if (sectionsWithoutProfile > 0)
            {
                result.AddError($"{sectionsWithoutProfile} sections missing mesh profile reference");
            }
            
            if (sectionsWithoutMesh > 0)
            {
                result.AddError($"{sectionsWithoutMesh} mesh profiles missing mesh reference");
            }
            
            if (sectionsWithoutMaterials > 0)
            {
                result.AddWarning($"{sectionsWithoutMaterials} sections missing material references");
            }
            
            return result;
        }
    }
}
