// ============================================================================
// IEditionModeController.cs
//
// Interface for edition mode controllers in the Model Editor.
// Each edition mode (Tract, Presets, Details, Diseases, Preview) implements
// this interface to provide consistent lifecycle management.
//
// Usage:
//   - Implement in concrete edition mode controllers
//   - Called by ModelEditorManager when switching modes
//
// Lifecycle:
//   1. Enter() - Called when switching TO this mode
//   2. Exit() - Called when switching FROM this mode
// ============================================================================

namespace ModelEditor.EditionModes
{
    /// <summary>
    /// Interface for edition mode controllers.
    /// Provides consistent lifecycle management for mode switching.
    /// </summary>
    public interface IEditionModeController
    {
        /// <summary>
        /// Called when entering this edition mode.
        /// Configure UI, input actions, and enable mode-specific features.
        /// </summary>
        void Enter();

        /// <summary>
        /// Called when exiting this edition mode.
        /// Clean up resources and disable mode-specific features.
        /// </summary>
        void Exit();
    }
}
