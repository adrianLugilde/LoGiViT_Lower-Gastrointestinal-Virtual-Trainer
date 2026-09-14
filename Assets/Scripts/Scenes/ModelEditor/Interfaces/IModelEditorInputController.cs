// ============================================================================
// IModelEditorInputController.cs
//
// Interface for XR input control in the Model Editor.
// Provides input action configuration and guided camera control for different
// edition modes.
//
// Implemented by: InputController
//
// Used by: Edition mode controllers (Tract, Preset, Details, Disease, Preview)
//
// Responsibilities:
//   - Configure input actions for each edition mode
//   - Control guided camera navigation (diseases and preview modes)
//   - Manage action modes (select vs grab)
//   - Handle guided camera state (position, rotation, reset)
//
// Architecture Note:
//   This interface abstracts InputController for mode controllers, enabling
//   dependency injection and improving testability. Mode controllers depend
//   on this interface rather than the concrete MonoBehaviour.
// ============================================================================

namespace ModelEditor
{
    /// <summary>
    /// Interface for XR input control and guided camera navigation in Model Editor.
    /// Provides edition mode-specific input configuration and camera management.
    /// </summary>
    public interface IModelEditorInputController
    {
        #region Mode-Specific Input Actions

        /// <summary>
        /// Configures input actions for Tract edition mode.
        /// Enables node manipulation controls.
        /// </summary>
        void SetTractModeActions();

        /// <summary>
        /// Configures input actions for Presets management mode.
        /// Enables preset selection and management controls.
        /// </summary>
        void SetPresetsModeManagementActions();

        /// <summary>
        /// Configures input actions for Presets edition mode.
        /// Enables node manipulation while editing/creating presets.
        /// </summary>
        void SetPresetsModeEditionActions();

        /// <summary>
        /// Configures input actions for Details edition mode.
        /// Enables section selection and blendshape editing controls.
        /// </summary>
        void SetDetailsModeActions();

        /// <summary>
        /// Configures input actions for Diseases edition mode.
        /// Enables guided camera and disease placement controls.
        /// </summary>
        void SetDiseasesModeActions();

        /// <summary>
        /// Configures input actions for Preview mode.
        /// Enables guided camera navigation controls.
        /// </summary>
        void SetPreviewModeActions();

        /// <summary>
        /// Unsets input actions specific to Preview mode.
        void UnsetPreviewModeActions();

        #endregion

        #region Action Modes

        /// <summary>
        /// Sets input to select action mode.
        /// Enables ray-based selection instead of grabbing.
        /// </summary>
        /// <remarks>
        /// Used in Details mode for selecting sections to edit.
        /// </remarks>
        void SetSelectActionMode();

        /// <summary>
        /// Sets input to grab action mode.
        /// Enables direct object grabbing and manipulation.
        /// </summary>
        /// <remarks>
        /// Default mode for Tract and Preset node editing.
        /// </remarks>
        void SetGrabActionMode();

        #endregion

        #region Diseases Guided Camera

        /// <summary>
        /// Activates or deactivates the diseases guided camera controls.
        /// </summary>
        /// <param name="isActive">True to enable guided camera, false to disable.</param>
        /// <remarks>
        /// The diseases guided camera allows internal navigation along the spline
        /// for precise disease placement.
        /// </remarks>
        void SetDiseasesGuidedCameraActionsActive(bool isActive);

        /// <summary>
        /// Resets the diseases guided camera position to the start of the spline.
        /// </summary>
        void ResetDiseasesGuidedCamera();

        /// <summary>
        /// Resets the diseases guided camera's rate (position along spline) to zero.
        /// </summary>
        void ResetDiseasesGuidedCameraRate();

        /// <summary>
        /// Marks the diseases guided camera transform as changed or unchanged.
        /// </summary>
        /// <param name="hasChanged">True if transform has changed, false otherwise.</param>
        /// <remarks>
        /// Used to detect camera movement for disease projection updates.
        /// </remarks>
        void SetDiseasesGuidedCameraTransformHasChanged(bool hasChanged);

        #endregion

        #region Preview Guided Camera

        /// <summary>
        /// Activates or deactivates the preview guided camera controls.
        /// </summary>
        /// <param name="isActive">True to enable guided camera, false to disable.</param>
        /// <remarks>
        /// The preview guided camera allows internal model inspection
        /// for viewing the intestine from inside.
        /// </remarks>
        void SetPreviewGuidedCameraActionsActive(bool isActive);

        #endregion
    }
}
