// ============================================================================
// IModelEditorUIController.cs
//
// Interface for UI control in the Model Editor.
// Provides UI state management, window navigation, modal dialogs,
// and control configuration for different edition modes.
//
// Implemented by: UIController
//
// Used by: Edition mode controllers (Tract, Preset, Details, Disease, Preview)
//
// Responsibilities:
//   - Configure UI controls for each edition mode
//   - Manage window navigation
//   - Display modal dialogs (confirm, error, warning)
//   - Manage scroll areas (presets, diseases)
//   - Control guided camera view menu
//   - Manage keybind display profiles
//
// Architecture Note:
//   This interface abstracts UIController for mode controllers, enabling
//   dependency injection and improving testability. Mode controllers depend
//   on this interface rather than the concrete MonoBehaviour.
// ============================================================================

using System;
using Messages;
using CustomUI;
using TMPro;

namespace ModelEditor
{
    /// <summary>
    /// Interface for UI control and state management in Model Editor.
    /// Provides edition mode-specific UI configuration and interaction.
    /// </summary>
    public interface IModelEditorUIController
    {
        #region Mode-Specific UI Configuration

        /// <summary>
        /// Configures UI controls for Tract edition mode.
        /// Shows undo/redo buttons and multiple selection toggle.
        /// </summary>
        /// <param name="isMultipleSelectionEnabled">Whether multiple selection is currently enabled.</param>
        void ShowTractModeControls(bool isMultipleSelectionEnabled);

        /// <summary>
        /// Configures UI controls for Presets management mode.
        /// Shows undo/redo buttons, hides selection controls.
        /// </summary>
        void ShowPresetsModeManagementControls();

        /// <summary>
        /// Configures UI controls for Presets edition mode.
        /// Shows undo/redo buttons and multiple selection toggle.
        /// </summary>
        /// <param name="isMultipleSelectionEnabled">Whether multiple selection is currently enabled.</param>
        void ShowPresetsModeEditionControls(bool isMultipleSelectionEnabled);

        /// <summary>
        /// Configures UI controls for Details edition mode.
        /// Shows undo/redo buttons and multiple selection toggle.
        /// </summary>
        /// <param name="isMultipleSelectionEnabled">Whether multiple selection is currently enabled.</param>
        void ShowDetailsModeControls(bool isMultipleSelectionEnabled);

        /// <summary>
        /// Configures UI controls for Diseases edition mode.
        /// Shows undo/redo buttons, hides selection controls.
        /// </summary>
        void ShowDiseasesModeControls();

        /// <summary>
        /// Configures UI controls for Preview mode.
        /// Disables most editing controls.
        /// </summary>
        void ShowPreviewModeControls();

        #endregion

        #region Window Navigation

        /// <summary>
        /// Switches to the specified window in the UI.
        /// </summary>
        /// <param name="windowIndex">Index of the window to display (corresponds to EditionWindow enum).</param>
        void SwitchToWindow(int windowIndex);

        #endregion

        #region Guided Camera View Menu

        /// <summary>
        /// Shows or hides the guided camera view menu.
        /// </summary>
        /// <param name="isActive">True to show menu, false to hide.</param>
        /// <remarks>
        /// The guided camera view menu provides controls for internal camera navigation
        /// in Diseases and Preview modes.
        /// </remarks>
        void SetGuidedCameraViewMenuActive(bool isActive);
        void SetNoisePanelActive(bool isActive);

        #endregion

        #region Keybind Display Profiles

        /// <summary>
        /// Sets the base keybind display profile.
        /// Shows default controller button mappings.
        /// </summary>
        void SetBaseKeybindDisplayProfile();

        /// <summary>
        /// Sets the character locomotion keybind display profile.
        /// Shows button mappings for walking navigation.
        /// </summary>
        void SetCharacterLocomotionKeybindDisplayProfile();

        /// <summary>
        /// Sets the endoscope locomotion keybind display profile.
        /// Shows button mappings for guided camera navigation.
        /// </summary>
        void SetEndoscopeLocomotionKeybindDisplayProfile();

        #endregion

        #region Preset Mode - Management

        /// <summary>
        /// Fills the spline presets scroll area with available presets.
        /// </summary>
        void FillSplinePresetsScrollArea();

        /// <summary>
        /// Gets the currently selected spline preset.
        /// </summary>
        /// <returns>Selected preset, or null if none selected.</returns>
        SplinePreset GetSelectedSplinePreset();

        /// <summary>
        /// Gets the currently selected spline preset scroll area element.
        /// </summary>
        /// <returns>Selected element, or null if none selected.</returns>
        SplinePresetScrollAreaElement GetSelectedSplinePresetElement();

        /// <summary>
        /// Clears the selection in the spline presets scroll area.
        /// </summary>
        void ClearSplinePresetsScrollAreaSelection();

        /// <summary>
        /// Resets the presets management menu to its initial state.
        /// </summary>
        void ResetPresetsManagementMenu();

        /// <summary>
        /// Sets the interactability of preset-selected buttons (apply, delete, edit, etc.).
        /// </summary>
        /// <param name="isInteractable">True to enable buttons, false to disable.</param>
        void SetPresetSelectedButtonsInteractable(bool isInteractable);

        /// <summary>
        /// Shows or hides the preset preview flag object.
        /// </summary>
        /// <param name="isActive">True to show flag, false to hide.</param>
        /// <remarks>
        /// The preview flag indicates that a preset is currently being previewed.
        /// </remarks>
        void SetPresetsPreviewFlagObjectActive(bool isActive);

        #endregion

        #region Preset Mode - Creation/Edition

        /// <summary>
        /// Gets the input fields for preset data (name, description).
        /// </summary>
        /// <returns>Array of input fields.</returns>
        TMP_InputField[] GetPresetDataInputFields();

        /// <summary>
        /// Resets the presets creation data menu to its initial state.
        /// Clears all input fields.
        /// </summary>
        void ResetPresetsCreationDataMenu();

        /// <summary>
        /// Updates the content of a preset in the scroll area.
        /// </summary>
        /// <param name="splinePreset">Preset with updated data to display.</param>
        void UpdateSplinePresetScrollAreaContent(SplinePreset splinePreset);

        /// <summary>
        /// Deletes the currently selected preset element from the scroll area.
        /// </summary>
        void DeleteSplinePresetScrollAreaSelectedElement();

        /// <summary>
        /// Updates the presets creation menu with data from a preset.
        /// </summary>
        /// <param name="splinePreset">Preset to load into creation menu.</param>
        void UpdatePresetsCreationDataMenuContent(SplinePreset splinePreset);

        /// <summary>
        /// Resets the selection in the presets management menu.
        /// </summary>
        void ResetPresetsManagementMenuSelection();

        #endregion

        #region Details Mode

        /// <summary>
        /// Sets the interactability of the details panel (blendshape sliders).
        /// </summary>
        /// <param name="isInteractive">True to enable panel, false to disable.</param>
        /// <remarks>
        /// Panel is enabled when a section is selected, disabled when no selection.
        /// </remarks>
        void SetDetailsPanelInteractive(bool isInteractive);

        #endregion

        #region Diseases Mode

        /// <summary>
        /// Fills the diseases scroll area with available diseases.
        /// </summary>
        void FillDiseasesScrollArea();

        /// <summary>
        /// Resets the diseases selection menu to its initial state.
        /// </summary>
        void ResetDiseasesSelectionMenu();

        /// <summary>
        /// Sets the interactability of disease mode tool controls.
        /// </summary>
        /// <param name="tools">Which tools to affect (can be combined flags).</param>
        /// <param name="interactive">True to enable tools, false to disable.</param>
        void SetDiseaseModeToolsControls(UIController.DiseaseTool tools, bool interactive);

        #endregion

        #region Modal Dialogs

        /// <summary>
        /// Shows a warning modal with the specified message.
        /// </summary>
        /// <param name="resultCode">Warning message code.</param>
        void ShowWarningModal(int resultCode);

        /// <summary>
        /// Shows a confirmation modal with confirm and cancel actions.
        /// </summary>
        /// <param name="resultCode">Message code for the modal text.</param>
        /// <param name="confirmAction">Action to execute on confirm.</param>
        /// <param name="cancelAction">Optional action to execute on cancel.</param>
        void ShowConfirmModal(int resultCode, Action confirmAction, Action cancelAction = null);

        /// <summary>
        /// Shows an error modal with an optional confirm action.
        /// </summary>
        /// <param name="resultCode">Error message code.</param>
        /// <param name="confirmAction">Optional action to execute on confirm.</param>
        void ShowErrorModal(int resultCode, Action confirmAction = null);

        #endregion
    }
}
