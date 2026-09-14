// ============================================================================
// ISplinePresetsController.cs
// 
// Interface for spline preset management controllers.
// Provides preset preview, application, and undo/redo capabilities.
// ============================================================================

using LargeIntestine;
using System;

namespace ModelEditor
{
    /// <summary>
    /// Interface for controllers that manage spline preset operations.
    /// </summary>
    public interface ISplinePresetsController : IEditorActionController
    {
        /// <summary>
        /// Event fired when a record is loaded (undo/redo operation).
        /// </summary>
        event Action OnRecordLoad;
        
        /// <summary>
        /// Previews a spline preset by applying it to the model.
        /// </summary>
        /// <param name="splinePreset">The preset to preview</param>
        void PreviewSplinePreset(SplinePreset splinePreset);
        
        /// <summary>
        /// Restores spline nodes to the backed-up state.
        /// </summary>
        void RestoreSplineNodes();
        
        /// <summary>
        /// Stores the current spline nodes for later restoration.
        /// </summary>
        void StoreCurrentSplineNodes();
        
        /// <summary>
        /// Creates a new record of the current spline state.
        /// </summary>
        void CreateSplinePresetRecord();
        
        /// <summary>
        /// Ensures a base record exists before making changes.
        /// </summary>
        void EnsureBaseRecordExists();

        /// <summary>
        /// Applies a preset to the model, updates up vectors, stores nodes, and creates a record.
        /// </summary>
        void ApplySplinePreset(SplinePreset splinePreset);
    }
}
