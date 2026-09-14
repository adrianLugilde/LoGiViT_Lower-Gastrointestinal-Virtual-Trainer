// ============================================================================
// DiseaseEditionModeController.cs
//
// Controls the Disease edition mode in the Model Editor.
// Manages disease placement, sizing, and positioning on the model.
//
// Responsibilities:
//   - Configure disease placement controller
//   - Manage guided camera for disease positioning
//   - Handle disease tool interactions (place, enlarge, shrink)
//   - Coordinate with welded model generation
//
// Dependencies:
//   - UIController (for UI configuration)
//   - InputController (for camera and input controls)
//   - DiseasePlacementController (for disease manipulation)
//   - LargeIntestineModelGenerator (for model access)
// ============================================================================

using System;
using LargeIntestine;
using UnityEngine;

namespace ModelEditor.EditionModes
{
    /// <summary>
    /// Controls the Disease edition mode for placing and editing diseases.
    /// Also implements <see cref="IDiseaseEditionStateProvider"/> to share state with other controllers.
    /// </summary>
    public class DiseaseEditionModeController : IEditionModeController, IDiseaseEditionStateProvider
    {
        #region Dependencies

        private readonly IModelEditorUIController _uiController;
        private readonly IModelEditorInputController _inputController;
        private readonly DiseasePlacementController _diseasePlacementController;
        private readonly ISplineModelGenerator _modelGenerator;
        private readonly LargeIntestineSectionsInteractablesController _liSectionInteractablesController;

        #endregion

        #region State

        /// <summary>Whether disease edition is currently active (affects other modes).</summary>
        public bool IsDiseaseModeEnabled { get; private set; }
        
        /// <summary>Whether welded model selection is enabled.</summary>
        public bool WeldedModelSelectionEnabled { get; private set; }
        
        /// <summary>Whether the welded model needs regeneration.</summary>
        public bool WeldedModelNeedsUpdate { get; set; } = true;

        #endregion

        #region Events

        /// <summary>Fired when disease edition mode is enabled/disabled.</summary>
        public event Action<bool> OnDiseaseEditionStateChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new DiseaseEditionModeController with required dependencies.
        /// </summary>
        public DiseaseEditionModeController(
            UIController uiController,
            InputController inputController,
            DiseasePlacementController diseasePlacementController,
            ISplineModelGenerator modelGenerator,
            LargeIntestineSectionsInteractablesController sectionsInteractablesController)
        {
            _uiController = uiController;
            _inputController = inputController;
            _diseasePlacementController = diseasePlacementController;
            _modelGenerator = modelGenerator;
            _liSectionInteractablesController = sectionsInteractablesController;

            // Inject dependencies into the placement controller, replacing its former singleton grab.
            _diseasePlacementController.Initialize(modelGenerator, uiController);
        }

        #endregion

        #region IEditionModeController Implementation

        /// <summary>
        /// Enters disease edition mode.
        /// Configures disease placement and guided camera.
        /// </summary>
        public void Enter()
        {
            ConfigureUI();
            _inputController.SetDiseasesModeActions();
        }

        /// <summary>
        /// Exits disease edition mode.
        /// Disables guided camera and cleans up if fully exiting disease editing.
        /// </summary>
        public void Exit()
        {
            // Disable guided camera
            _inputController.SetDiseasesGuidedCameraActionsActive(false);
            _uiController.SetGuidedCameraViewMenuActive(false);
            
            // Only reset diseases menu if disease edition is being disabled
            if (!IsDiseaseModeEnabled)
            {
                _uiController.ResetDiseasesSelectionMenu();
            }
        }

        #endregion

        #region Public Methods - Disease Edition Lifecycle

        /// <summary>
        /// Enables disease edition mode.
        /// Generates welded model if needed and initializes disease placement.
        /// </summary>
        public void EnableDiseaseEdition()
        {
            // Generate welded model if needed
            if (WeldedModelNeedsUpdate)
            {
                UpdateWeldedModel();
            }
            
            // Hide segmented model, show welded
            _modelGenerator.SegmentedModelGO.SetActive(false);
            
            // Initialize disease placement controller with the current welded model.
            _diseasePlacementController.Init(
                _modelGenerator.WeldedModelRenderer,
                _modelGenerator.WeldedModelCollider);
            
            // Reset camera
            _inputController.ResetDiseasesGuidedCameraRate();
            
            // Update state
            IsDiseaseModeEnabled = true;
            WeldedModelSelectionEnabled = true;
            
            // Notify camera needs update
            _inputController.SetDiseasesGuidedCameraTransformHasChanged(true);
            
            // Set endoscope locomotion profile
            _uiController.SetEndoscopeLocomotionKeybindDisplayProfile();
            
            OnDiseaseEditionStateChanged?.Invoke(true);
        }

        /// <summary>
        /// Disables disease edition mode.
        /// Cleans up disease placement and restores segmented model.
        /// </summary>
        /// <param name="currentEditionMode">The current mode (for material restoration check).</param>
        public void DisableDiseaseEdition(ModelEditorManager.EditionMode currentEditionMode)
        {
            // Reset disease placement
            _diseasePlacementController.Reset();
            _inputController.ResetDiseasesGuidedCamera();
            
            // Destroy welded model and show segmented
            _modelGenerator.DestroyWeldedModelGO();
            _modelGenerator.SegmentedModelGO.SetActive(true);
            
            // Update state
            WeldedModelNeedsUpdate = true;
            WeldedModelSelectionEnabled = false;
            
            // Restore materials if coming from preview mode
            if (currentEditionMode == ModelEditorManager.EditionMode.Preview)
            {
                _liSectionInteractablesController.RestoreSectionsOriginalMaterials();
            }
            
            IsDiseaseModeEnabled = false;
            
            // Restore base keybind profile
            _uiController.SetBaseKeybindDisplayProfile();
            
            OnDiseaseEditionStateChanged?.Invoke(false);
        }

        #endregion

        #region Public Methods - Disease Tools

        /// <summary>
        /// Sets the disease placement controller enabled state.
        /// </summary>
        public void SetDiseasePlacementEnabled(bool enabled)
        {
            _diseasePlacementController.enabled = enabled;
        }

        /// <summary>
        /// Sets all disease mode tools interactive state.
        /// </summary>
        public void SetAllToolsInteractive(bool interactive)
        {
            _uiController.SetDiseaseModeToolsControls(UIController.DiseaseTool.All, interactive);
        }

        /// <summary>
        /// Sets shrink tool interactive state.
        /// </summary>
        public void SetShrinkToolInteractive(bool interactive)
        {
            _uiController.SetDiseaseModeToolsControls(UIController.DiseaseTool.Shrink, interactive);
        }

        /// <summary>
        /// Enlarges the currently projected disease mesh.
        /// </summary>
        public void EnlargeDiseaseMesh()
        {
            ScaleDiseaseMesh(1f, upscale: true);
        }

        /// <summary>
        /// Shrinks the currently projected disease mesh.
        /// </summary>
        public void ShrinkDiseaseMesh()
        {
            ScaleDiseaseMesh(-1f, upscale: false);
        }

        /// <summary>
        /// Places the disease at its current projected location.
        /// </summary>
        /// <returns>The intestine location for the disease.</returns>
        public Disease.IntestineLocation GetDiseasePlacementLocation()
        {
            return _diseasePlacementController.GetDiseasePlacementLocation();
        }

        /// <summary>
        /// Merges the disease at the specified location.
        /// </summary>
        public void MergeDisease(Disease.IntestineLocation location)
        {
            _diseasePlacementController.Merge(location);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Configures UI controls for disease mode.
        /// </summary>
        private void ConfigureUI()
        {
            _uiController.ShowDiseasesModeControls();
            _uiController.FillDiseasesScrollArea();
            _uiController.SetGuidedCameraViewMenuActive(true);
            SetAllToolsInteractive(false);
            _uiController.SwitchToWindow((int)UIController.EditionWindow.Diseases);
            _uiController.SetEndoscopeLocomotionKeybindDisplayProfile();
        }

        /// <summary>
        /// Updates the welded model from segmented model.
        /// </summary>
        private void UpdateWeldedModel()
        {
            _modelGenerator.GenerateWeldedModel(true, true);
            WeldedModelNeedsUpdate = false;
        }

        /// <summary>
        /// Scales the disease mesh by the given factor.
        /// </summary>
        private void ScaleDiseaseMesh(float scaleFactor, bool upscale)
        {
            _diseasePlacementController.diseaseScaleFactor += 
                Vector3.one * scaleFactor * _diseasePlacementController.diseaseScaleFactorMultiplier;
            _diseasePlacementController.UpdateProjectedMesh(upscale);
            _diseasePlacementController.UpdateDiseaseProjectionBaseBounds();
        }

        #endregion
    }
}
