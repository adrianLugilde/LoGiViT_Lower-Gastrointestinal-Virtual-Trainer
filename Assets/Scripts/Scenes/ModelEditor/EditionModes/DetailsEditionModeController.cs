// ============================================================================
// DetailsEditionModeController.cs
//
// Controls the Details edition mode in the Model Editor.
// Manages blendshape editing for fine-tuning intestinal segment shapes.
//
// Responsibilities:
//   - Enable/disable section selection for blendshape editing
//   - Configure UI sliders and panels
//   - Manage blendshape controller state
//
// Dependencies:
//   - UIController (for UI configuration)
//   - InputController (for input actions)
//   - BlendshapesControllerV2 (for blendshape manipulation)
//   - LargeIntestineSectionInteractablesController (for section selection)
//   - LargeIntestineModelGenerator (for model access)
// ============================================================================

using LargeIntestine;
using UnityEngine;

namespace ModelEditor.EditionModes
{
    /// <summary>
    /// Controls the Details edition mode for blendshape-based shape refinement.
    /// </summary>
    public class DetailsEditionModeController : IEditionModeController
    {
        #region Dependencies

        private readonly IModelEditorUIController _uiController;
        private readonly IModelEditorInputController _inputController;
        private readonly IBlendshapesController _blendshapesController;
        private readonly LargeIntestineSectionsInteractablesController _liSectionInteractablesController;
        private readonly ISplineModelGenerator _modelGenerator;
        private readonly CustomUI.SelectionController _selectionController;
        private readonly IDiseaseEditionStateProvider _diseaseStateProvider;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new DetailsEditionModeController with required dependencies.
        /// </summary>
        /// <param name="diseaseStateProvider">Provider for disease edition state queries.</param>
        public DetailsEditionModeController(
            UIController uiController,
            InputController inputController,
            IBlendshapesController blendshapesController,
            LargeIntestineSectionsInteractablesController liSectionInteractablesController,
            ISplineModelGenerator modelGenerator,
            CustomUI.SelectionController selectionController,
            IDiseaseEditionStateProvider diseaseStateProvider)
        {
            _uiController = uiController;
            _inputController = inputController;
            _blendshapesController = blendshapesController;
            _liSectionInteractablesController = liSectionInteractablesController;
            _modelGenerator = modelGenerator;
            _selectionController = selectionController;
            _diseaseStateProvider = diseaseStateProvider;
        }

        #endregion

        #region IEditionModeController Implementation

        /// <summary>
        /// Enters details edition mode.
        /// Enables section selection and blendshape editing.
        /// </summary>
        public void Enter()
        {
            // Show segmented model
            _modelGenerator.SegmentedModelGO.SetActive(true);
            
            // Enable section selection
            EnableSegmentedModelSelection();
            
            // Configure UI and input
            ConfigureUI();
        }

        /// <summary>
        /// Exits details edition mode.
        /// Disables section selection and cleans up.
        /// </summary>
        public void Exit()
        {
            // Disable section selection
            DisableSegmentedModelSelection();
            
            // Clear blendshape selection
            _blendshapesController.ClearSelection();
            //_liSectionInteractablesController.DisableSectionInteractables();
            
            // Reset input mode
            _inputController.SetGrabActionMode();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Configures UI controls for details mode.
        /// </summary>
        private void ConfigureUI()
        {
            _uiController.ShowDetailsModeControls(_selectionController.MultipleSelectionEnabled);
            _inputController.SetDetailsModeActions();
            _inputController.SetSelectActionMode();
            _uiController.SetDetailsPanelInteractive(false);
            _uiController.SwitchToWindow((int)UIController.EditionWindow.Details);
        }

        /// <summary>
        /// Enables selection of segmented model sections.
        /// </summary>
        private void EnableSegmentedModelSelection()
        {
            _liSectionInteractablesController.EnableSectionInteractables();
            
            /*if (_diseaseStateProvider.WeldedModelSelectionEnabled)
            {
                _liSectionInteractablesController.SetTransparentSectionsMaterials();
            }*/
        }

        /// <summary>
        /// Disables selection of segmented model sections.
        /// </summary>
        private void DisableSegmentedModelSelection()
        {
            _liSectionInteractablesController.DisableSectionInteractables();
            
            /*if (_diseaseStateProvider.WeldedModelSelectionEnabled)
            {
                _liSectionInteractablesController.RestoreSectionsOriginalMaterials();
            }*/
        }

        #endregion
    }
}
