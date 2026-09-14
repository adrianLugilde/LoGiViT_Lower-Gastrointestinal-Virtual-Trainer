// ============================================================================
// IModelEditorStateProvider.cs
//
// Unified interface for providing Model Editor state information to controllers.
// Replaces the Func<> event pattern for cleaner dependency injection and testability.
//
// The Model Editor Manager implements this interface to act as a mediator,
// providing state queries that controllers need without creating direct
// controller-to-controller dependencies.
//
// Used by:
//   - InputController: For guided camera navigation and mode-specific behavior
//   - SplinePresetsController: For preset preview state queries
// ============================================================================

using LargeIntestine;
using UnityEngine;

namespace ModelEditor
{
    /// <summary>
    /// Interface for querying Model Editor state.
    /// Implemented by ModelEditorManager to provide state information
    /// to controllers without tight coupling (manager acts as mediator).
    /// </summary>
    public interface IModelEditorStateProvider
    {
        #region Initialization State

        /// <summary>
        /// Returns true if the Model Editor is fully initialized and ready.
        /// </summary>
        bool IsInitialized { get; }

        #endregion

        #region Edition Mode Queries

        /// <summary>
        /// Returns true if the current edition mode is Preview.
        /// </summary>
        bool IsInPreviewMode { get; }

        /// <summary>
        /// Returns true if the current edition mode is Diseases.
        /// </summary>
        bool IsInDiseasesMode { get; }

        /// <summary>
        /// Returns true if the current edition mode is Preview or Diseases.
        /// These modes share guided camera functionality.
        /// </summary>
        bool IsInPreviewOrDiseasesMode { get; }

        #endregion

        #region Preset Preview Queries

        /// <summary>
        /// Gets whether a preset is currently being previewed.
        /// Used by SplinePresetsController when loading records.
        /// </summary>
        bool IsPresetPreviewActive { get; }

        /// <summary>
        /// Gets the currently selected spline preset from the UI.
        /// Used by SplinePresetsController when re-applying preview after record load.
        /// </summary>
        /// <returns>The selected SplinePreset, or null if none is selected.</returns>
        SplinePreset GetSelectedPreset();

        #endregion

        #region Spline Data Access

        /// <summary>
        /// Returns the total number of spline nodes in the current model.
        /// </summary>
        int SplineNodeCount { get; }

        /// <summary>
        /// Gets the rotation sample from the spline at the specified rate (0-1).
        /// </summary>
        /// <param name="rate">Normalized position along the spline (0-1).</param>
        /// <returns>The rotation at the specified spline position.</returns>
        Quaternion GetSampleRotationAtRate(float rate);

        /// <summary>
        /// Gets the world-space location sample from the spline at the specified rate (0-1).
        /// </summary>
        /// <param name="rate">Normalized position along the spline (0-1).</param>
        /// <returns>World-space position at the specified spline position.</returns>
        Vector3 GetSampleLocationAtRate(float rate);

        #endregion
    }
}
