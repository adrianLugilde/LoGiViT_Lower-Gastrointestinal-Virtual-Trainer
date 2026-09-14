using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using ModelEditor;

namespace LargeIntestine
{
    [Serializable]
    public class LI_GenerationConfiguration : JsonData, IGenerationConfiguration
    {
        /**** JsonData Implementation ****/
        public override string DataPath => Application.streamingAssetsPath;
        public override string FileExtension => FileManager.LIGenerationConfigurationFileExtension;
        public override JsonSerializerSettings JsonSettings => null;

        /// <summary>
        /// Override to use FileID as filename for this type
        /// </summary>
        public override string GetFullPath(string overrideFileName = null)
        {
            var name = overrideFileName ?? FileID.ToString();
            return Path.Combine(DataPath, name + FileExtension);
        }

        public List<LI_SegmentConfiguration> intestineSegments = new List<LI_SegmentConfiguration>();

        [Header("Global Blendshape Settings")]
        [Tooltip("The blendshape name used for global width control across all sections")]
        public string globalWidthBlendshapeName = "";

        public LI_GenerationConfiguration()
        {
            FileID = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            intestineSegments = new List<LI_SegmentConfiguration>();
        }

        public LI_GenerationConfiguration(string filePath)
        {
            ReadFromFile(filePath);
        }

        /// <summary>
        /// Override to load mesh profiles after deserialization
        /// </summary>
        public override int ReadFromFile(string sourcePath)
        {
            var res = base.ReadFromFile(sourcePath);
            if (res == Messages.BasicMessages.None)
            {
                foreach (var segment in intestineSegments)
                {
                    foreach (var section in segment.liSections)
                    {
                        section.LoadMeshProfile();
                    }
                }
            }
            else
            {
                Debug.LogError($"Failed to read LI_GenerationConfiguration from {sourcePath}: {res}");
            }
            return res;
        }

        /// <summary>
        /// Gets all unique mesh profiles used in this configuration
        /// </summary>
        public List<SplineMeshProfile> GetUsedMeshProfiles()
        {
            var profiles = new HashSet<SplineMeshProfile>();

            foreach (var segment in intestineSegments)
            {
                if (segment.liSections == null) continue;

                foreach (var section in segment.liSections)
                {
                    if (section.meshProfile != null)
                    {
                        profiles.Add(section.meshProfile);
                    }
                }
            }

            return profiles.ToList();
        }

        /// <summary>
        /// Gets blendshape names that are shared across ALL mesh profiles used in this configuration
        /// </summary>
        public List<string> GetSharedBlendshapeNames()
        {
            var profiles = GetUsedMeshProfiles();

            if (profiles.Count == 0)
                return new List<string>();

            // Start with all blendshapes from the first profile
            HashSet<string> sharedNames = null;

            foreach (var profile in profiles)
            {
                if (profile.blendshapes == null || profile.blendshapes.Count == 0)
                    continue;

                var profileBlendshapeNames = new HashSet<string>(
                    profile.blendshapes.Select(b => b.name)
                );

                if (sharedNames == null)
                {
                    sharedNames = profileBlendshapeNames;
                }
                else
                {
                    // Keep only blendshapes that exist in both sets
                    sharedNames.IntersectWith(profileBlendshapeNames);
                }
            }

            return sharedNames?.OrderBy(n => n).ToList() ?? new List<string>();
        }

        /// <summary>
        /// Validates that the global width blendshape is valid (exists in all profiles)
        /// </summary>
        public bool IsGlobalWidthBlendshapeValid()
        {
            if (string.IsNullOrEmpty(globalWidthBlendshapeName))
                return false;

            var sharedNames = GetSharedBlendshapeNames();
            return sharedNames.Contains(globalWidthBlendshapeName);
        }

        #region IGenerationConfiguration Implementation
        
        /// <summary>
        /// Explicit interface implementation for generic access to global width blendshape name
        /// </summary>
        string IGenerationConfiguration.GlobalWidthBlendshapeName => globalWidthBlendshapeName;
        
        /// <summary>
        /// Gets all section configurations as a flat list (interface implementation)
        /// </summary>
        List<IModelSectionConfiguration> IGenerationConfiguration.GetAllSectionConfigurations()
        {
            return intestineSegments
                .SelectMany(segment => segment.liSections)
                .Cast<IModelSectionConfiguration>()
                .ToList();
        }
        
        /// <summary>
        /// Gets a specific section configuration by flat index (interface implementation)
        /// </summary>
        IModelSectionConfiguration IGenerationConfiguration.GetSectionConfiguration(int index)
        {
            int currentIndex = 0;
            foreach (var segment in intestineSegments)
            {
                foreach (var section in segment.liSections)
                {
                    if (currentIndex == index)
                        return section;
                    currentIndex++;
                }
            }
            return null;
        }
        
        #endregion
    }
}