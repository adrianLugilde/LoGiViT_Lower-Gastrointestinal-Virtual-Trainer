// ============================================================================
// TractEditionModeController.cs
//
// Controls the Tract edition mode in the Model Editor.
// Manages spline node editing for shaping the intestinal tract.
//
// Responsibilities:
//   - Show/hide spline nodes and enable/disable their interaction
//   - Configure UI for tract editing
//   - Manage joined node movement
//
// Dependencies:
//   - UIController (for UI configuration)
//   - InputController (for input actions)
//   - SplineNodeInteractablesController (for node manipulation)
//   - LargeIntestineModelGenerator (for model access)
// ============================================================================


namespace ModelEditor.EditionModes
{
    /// <summary>
    /// Controls the Tract edition mode for spline-based tract shaping.
    /// </summary>
    public class TractEditionModeController : IEditionModeController
    {
        #region Dependencies

        private readonly IModelEditorUIController _uiController;
        private readonly IModelEditorInputController _inputController;
        private readonly ISplineNodesInteractablesController _splineNodeInteractableController;
        private readonly ISplineModelGenerator _modelGenerator;
        private readonly CustomUI.SelectionController _selectionController;
        private readonly ModelNoiseSettingsController _noiseConfigPanel;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new TractEditionModeController with required dependencies.
        /// </summary>
        public TractEditionModeController(
            IModelEditorUIController uiController,
            IModelEditorInputController inputController,
            ISplineNodesInteractablesController splineNodeInteractableController,
            ISplineModelGenerator modelGenerator,
            CustomUI.SelectionController selectionController,
            ModelNoiseSettingsController noiseConfigPanel)
        {
            _uiController = uiController;
            _inputController = inputController;
            _splineNodeInteractableController = splineNodeInteractableController;
            _modelGenerator = modelGenerator;
            _selectionController = selectionController;
            _noiseConfigPanel = noiseConfigPanel;
        }

        #endregion

        #region IEditionModeController Implementation

        /// <summary>
        /// Enters tract edition mode.
        /// Enables spline node editing and configures the UI.
        /// </summary>
        public void Enter()
        {
            // Show and enable spline node editing
            _splineNodeInteractableController.ShowNodes();
            _splineNodeInteractableController.EnableNodeInteraction();

            // Show segmented model for visual feedback
            _modelGenerator.SegmentedModelGO.SetActive(true);

            // Configure UI and input
            ConfigureUI();
            _uiController.SetNoisePanelActive(true);
            _noiseConfigPanel.SyncUIFromModelNoiseSettings();
        }

        /// <summary>
        /// Exits tract edition mode.
        /// Hides and disables spline node interactables.
        /// </summary>
        public void Exit()
        {
            _splineNodeInteractableController.HideNodes();
            _splineNodeInteractableController.DisableNodeInteraction();
            _uiController.SetNoisePanelActive(false);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Enables solidary (joined) node movement.
        /// Moving one node will affect connected nodes.
        /// </summary>
        public void EnableJoinedNodes()
        {
            _splineNodeInteractableController.EnableSolidaryNodeMovement();
        }

        /// <summary>
        /// Disables solidary node movement.
        /// Each node moves independently.
        /// </summary>
        public void DisableJoinedNodes()
        {
            _splineNodeInteractableController.DisableSolidaryNodeMovement();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Configures UI controls for tract mode.
        /// </summary>
        private void ConfigureUI()
        {
            _uiController.ShowTractModeControls(_selectionController.MultipleSelectionEnabled);
            _inputController.SetTractModeActions();
            _uiController.SwitchToWindow((int)UIController.EditionWindow.Default);
        }

        #endregion
    }
}
