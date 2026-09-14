// ============================================================================
// PreviewEditionModeController.cs
//
// Controls the Preview edition mode in the Model Editor.
// Provides internal camera navigation for model inspection.
//
// Responsibilities:
//   - Configure guided camera for internal preview
//   - Manage camera view menu visibility
//   - Handle locomotion profile switching
//   - Show welded model on enter / restore segmented on exit (when not in disease mode)
//
// Dependencies:
//   - UIController (for UI configuration)
//   - InputController (for camera controls)
//   - ISplineModelGenerator (for welded/segmented model management)
// ============================================================================

using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

namespace ModelEditor.EditionModes
{
    /// <summary>
    /// Controls the Preview edition mode for internal model inspection.
    /// </summary>
    public class PreviewEditionModeController : IEditionModeController
    {
        #region Dependencies

        private readonly IModelEditorUIController _uiController;
        private readonly IModelEditorInputController _inputController;
        private readonly IDiseaseEditionStateProvider _diseaseStateProvider;
        private readonly ISplineModelGenerator _modelGenerator;
        private readonly Action _resetNBI;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new PreviewEditionModeController with required dependencies.
        /// </summary>
        public PreviewEditionModeController(
            UIController uiController,
            InputController inputController,
            IDiseaseEditionStateProvider diseaseStateProvider,
            ISplineModelGenerator modelGenerator,
            Action resetNBI)
        {
            _uiController = uiController;
            _inputController = inputController;
            _diseaseStateProvider = diseaseStateProvider;
            _modelGenerator = modelGenerator;
            _resetNBI = resetNBI;
        }

        #endregion

        #region IEditionModeController Implementation

        /// <summary>
        /// Enters preview edition mode.
        /// Configures guided camera navigation.
        /// When disease mode is not active, generates and shows the welded model.
        /// </summary>
        public void Enter()
        {

            if (!_diseaseStateProvider.IsDiseaseModeEnabled)
            {
                ShowWeldedModel();
                _uiController.SetEndoscopeLocomotionKeybindDisplayProfile();
            }
            ConfigureUI();
            ConfigureInput();

        }

        /// <summary>
        /// Exits preview edition mode.
        /// Disables guided camera and restores locomotion profile.
        /// When disease mode is not active, destroys the welded model and restores the segmented one.
        /// </summary>
        public void Exit()
        {
            // Hide camera view menu
            _uiController.SetGuidedCameraViewMenuActive(false);

            // Disable guided camera
            //_inputController.SetPreviewGuidedCameraActionsActive(false);
            _inputController.UnsetPreviewModeActions();

            _resetNBI?.Invoke();

            if (!_diseaseStateProvider.IsDiseaseModeEnabled)
            {
                DestroyWeldedModel();
                _uiController.SetBaseKeybindDisplayProfile();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Configures UI controls for preview mode.
        /// </summary>
        private void ConfigureUI()
        {
            _uiController.ShowPreviewModeControls();
            _uiController.SetGuidedCameraViewMenuActive(true);
            _uiController.SwitchToWindow((int)UIController.EditionWindow.Default);
        }

        /// <summary>
        /// Generates welded model and hides segmented model for internal preview.
        /// </summary>
        private void ShowWeldedModel()
        {
            _modelGenerator.GenerateWeldedModel(true, true);
            _modelGenerator.SegmentedModelGO.SetActive(false);
        }

        /// <summary>
        /// Configures input actions for guided camera navigation in preview mode, delayed until the welded model is generated.
        /// </summary>
        private void ConfigureInput()
        {
            //yield return new WaitUntil(() => _modelGenerator.IsWeldedModelGenerated);
            _inputController.SetPreviewModeActions();

        }

        /// <summary>
        /// Destroys welded model and restores segmented model after preview.
        /// </summary>
        private void DestroyWeldedModel()
        {
            _modelGenerator.DestroyWeldedModelGO();
            _modelGenerator.SegmentedModelGO.SetActive(true);
        }

        #endregion
    }
}
