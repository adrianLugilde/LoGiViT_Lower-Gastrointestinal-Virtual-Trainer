// ============================================================================
// ISplineModelGenerator.cs
// 
// Interface for spline-based organ model generators.
// Provides abstraction for different organ types that follow a spline path.
// 
// Implementations:
//   - LargeIntestineModelGenerator
//   - Future: StomachModelGenerator, SmallIntestineModelGenerator, etc.
// 
// Usage:
//   Editor tools and controllers depend on this interface to work with
//   any spline-based organ model without knowing the specific type.
// ============================================================================

using System.Collections.Generic;
using LargeIntestine;
using SplineMesh;
using UnityEngine;

namespace ModelEditor
{
    /// <summary>
    /// Interface for spline-based model generators.
    /// Allows different organ generators (large intestine, stomach, etc.) to be used
    /// interchangeably with spline editing tools.
    /// </summary>
    /// <remarks>
    /// <para>Provides access to:</para>
    /// <list type="bullet">
    ///   <item>The spline defining the organ's path</item>
    ///   <item>The sections that make up the model</item>
    ///   <item>Visual and collision GameObjects</item>
    ///   <item>Methods for spline manipulation and mesh updates</item>
    /// </list>
    /// </remarks>
    public interface ISplineModelGenerator
    {
        /// <summary>
        /// The spline that defines the organ's path/shape
        /// </summary>
        Spline Spline { get; }

        /// <summary>
        /// The number of nodes in the spline
        /// </summary>
        int SplineNodeCount { get; }

        /// <summary>
        /// The Transform component of the root GameObject
        /// </summary>
        Transform transform { get; }

        /// <summary>
        /// Whether the segmented model generation is complete 
        /// </summary>
        bool IsSegmentedModelGenerated { get; }

        /// <summary>
        /// Whether the welded model generation is complete 
        /// </summary>
        bool IsWeldedModelGenerated { get; }

        /// <summary>
        /// The root GameObject of this generator (used for parenting edition nodes)
        /// </summary>
        GameObject RootGameObject { get; }

        /// <summary>
        /// The segmented model GameObject (sections for detailed editing)
        /// </summary>
        GameObject SegmentedModelGO { get; }

        /// <summary>
        /// The welded model GameObject (single combined mesh)
        /// </summary>
        GameObject WeldedModelGO { get; }

        /// <summary>
        /// The welded model collider (for interaction when not editing sections)
        /// </summary>
        MeshCollider WeldedModelCollider { get; }

        /// <summary>
        /// The welded model renderer (for disease placement operations)
        /// </summary>
        SkinnedMeshRenderer WeldedModelRenderer { get; }

        /// <summary>
        /// Starting value in the spline for segments (for blendshape operations)
        /// </summary>
        List<float> SegmentsStartValueInSpline { get; }

        /// <summary>
        /// List of all sections in the model
        /// </summary>
        IReadOnlyList<IModelSection> Sections { get; }

        /// <summary>
        /// Replaces the spline nodes with new data (for undo/redo and preset loading)
        /// </summary>
        /// <param name="splineNodes">The new spline nodes to apply</param>
        void ReplaceSplineNodesData(List<SplineNode> splineNodes);

        /// <summary>
        /// Applies a spline preset to the model
        /// </summary>
        /// <param name="splinePreset">The preset to apply</param>
        void ApplySplinePreset(SplinePreset splinePreset);

        /// <summary>
        /// Initializes the spline from model data
        /// </summary>
        /// <param name="model">The model data to initialize from</param>
        void InitializeSplineFromModelData(Model model);

        /// <summary>
        /// Loads generation configuration from model data
        /// </summary>
        /// <param name="model">The model containing the configuration</param>
        void LoadGenerationConfiguration(Model model);

        /// <summary>
        /// Generates the segmented model from the spline.
        /// Pass <c>destroyExisting: false</c> when only noise settings changed — section count
        /// and names are stable so the factory can reuse existing GameObjects, preserving any
        /// components added on top (e.g. XR interactables).
        /// </summary>
        void GenerateSegmentedModel(bool destroyExisting = true);

        /// <summary>
        /// Gets the blendshape dictionaries for all renderers
        /// </summary>
        /// <returns>List of blendshape dictionaries for each renderer</returns>
        List<SerializableDictionary<string, float>> GetRenderersBlendshapesDicts();

        /// <summary>
        /// Gets the bounds of all intestine sections
        /// </summary>
        /// <returns>List of bounds for each section</returns>
        List<Bounds> GetIntestineSectionsBounds();

        /// <summary>
        /// Gets the global width factor for blendshapes
        /// </summary>
        /// <returns>The width factor</returns>
        float GetBlendshapeGlobalWidthFactor();

        /// <summary>
        /// Gets a component in children (Unity method pass-through)
        /// </summary>
        /// <typeparam name="T">The component type to find</typeparam>
        /// <param name="includeInactive">Whether to include inactive GameObjects</param>
        /// <returns>The component found</returns>
        T GetComponentInChildren<T>(bool includeInactive = false) where T : Component;

        /// <summary>
        /// Generates the welded (combined) model from sections
        /// </summary>
        /// <param name="keepSegmentedModel">Whether to keep the segmented model visible</param>
        /// <param name="generateCombinedVersion">Whether to generate a combined mesh version</param>
        void GenerateWeldedModel(bool keepSegmentedModel = false, bool generateCombinedVersion = false);

        /// <summary>
        /// Creates the welded model GameObject and initializes WeldedModelRenderer/WeldedModelCollider
        /// without generating the mesh. Used when loading a saved mesh into the renderer directly.
        /// </summary>
        void InitializeWeldedModelGO();

        /// <summary>
        /// Destroys the welded model GameObject and nulls the internal reference immediately,
        /// so GenerateWeldedModel will recreate it on next call.
        /// </summary>
        void DestroyWeldedModelGO();

        /// <summary>
        /// Recalculates the mesh after spline changes
        /// </summary>
        void ComputeMeshRecalculations();

        /// <summary>
        /// Updates the up vectors along the spline (for consistent orientation)
        /// </summary>
        void UpdateUpVectors();

        /// <summary>
        /// Gets the generation configuration for this model.
        /// Provides access to section configurations and blendshape metadata.
        /// </summary>
        IGenerationConfiguration GenerationConfiguration { get; }

        /// <summary>Returns a copy of the current per-model noise settings.</summary>
        NoiseSettings GetNoiseSettings();

        /// <summary>Replaces the current noise settings. Call <see cref="GenerateSegmentedModel"/> to apply.</summary>
        void ApplyNoiseSettings(NoiseSettings settings);
    }
}
