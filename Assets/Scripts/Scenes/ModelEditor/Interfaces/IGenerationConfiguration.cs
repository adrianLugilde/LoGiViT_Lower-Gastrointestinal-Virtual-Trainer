// ============================================================================
// IGenerationConfiguration.cs
// 
// Base interface for organ model generation configurations.
// Provides abstraction for different organ types while maintaining
// common functionality needed by the editor system.
// 
// Implementations:
//   - LI_GenerationConfiguration (Large Intestine)
//   - Future: StomachGenerationConfiguration, etc.
// ============================================================================

using System.Collections.Generic;

namespace ModelEditor
{
    /// <summary>
    /// Interface for model generation configurations.
    /// Provides generic access to configuration data needed by editor systems.
    /// </summary>
    public interface IGenerationConfiguration
    {
        /// <summary>
        /// The name of the blendshape used for global width control across all sections.
        /// Empty string if no global width blendshape is defined.
        /// </summary>
        string GlobalWidthBlendshapeName { get; }
        
        /// <summary>
        /// Gets all section configurations as a flat list.
        /// Used by systems that need to iterate over all sections regardless of segmentation.
        /// </summary>
        /// <returns>List of all section configurations</returns>
        List<IModelSectionConfiguration> GetAllSectionConfigurations();
        
        /// <summary>
        /// Gets the section configuration at a specific flat index.
        /// </summary>
        /// <param name="index">The flat index of the section (0-based)</param>
        /// <returns>The section configuration, or null if index is out of range</returns>
        IModelSectionConfiguration GetSectionConfiguration(int index);
    }
}
