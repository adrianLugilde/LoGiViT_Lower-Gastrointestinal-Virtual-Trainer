// ============================================================================
// IModelSectionConfiguration.cs
// 
// Base interface for section configurations in organ model generators.
// Provides abstraction for accessing mesh profiles and blendshape data.
// ============================================================================

using LargeIntestine;

namespace ModelEditor
{
    /// <summary>
    /// Interface for section configurations.
    /// Provides generic access to section data needed by editor systems.
    /// </summary>
    public interface IModelSectionConfiguration
    {
        /// <summary>
        /// The mesh profile that defines this section's mesh and available blendshapes.
        /// </summary>
        SplineMeshProfile MeshProfile { get; }
        
        /// <summary>
        /// Gets a blendshape value by name.
        /// </summary>
        /// <param name="blendshapeName">Name of the blendshape</param>
        /// <param name="defaultValue">Default value to return if not found</param>
        /// <returns>The blendshape value, or defaultValue if not found</returns>
        float GetBlendshapeValue(string blendshapeName, float defaultValue = 0f);
        
        /// <summary>
        /// Sets a blendshape value by name.
        /// </summary>
        /// <param name="blendshapeName">Name of the blendshape</param>
        /// <param name="value">Value to set (0-100)</param>
        void SetBlendshapeValue(string blendshapeName, float value);
    }
}
