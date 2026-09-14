// ============================================================================
// IDiseaseEditionStateProvider.cs
//
// Interface for querying disease edition mode state.
// Implemented by DiseaseEditionModeController to provide state to other
// controllers that need to coordinate with disease edition mode.
//
// This follows the same pattern as IModelEditorStateProvider, providing
// a clean interface for cross-controller state queries without tight coupling.
// ============================================================================

namespace ModelEditor.EditionModes
{
    /// <summary>
    /// Provides disease edition state to other controllers.
    /// Implemented by <see cref="DiseaseEditionModeController"/>.
    /// </summary>
    public interface IDiseaseEditionStateProvider
    {
        /// <summary>
        /// Whether disease edition mode is currently active.
        /// When true, affects behavior of other edition modes.
        /// </summary>
        bool IsDiseaseModeEnabled { get; }
        
        /// <summary>
        /// Whether welded model selection is enabled.
        /// Used by Details mode to coordinate material handling.
        /// </summary>
        bool WeldedModelSelectionEnabled { get; }
    }
}
