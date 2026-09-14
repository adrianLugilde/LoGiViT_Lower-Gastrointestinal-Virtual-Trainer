// ============================================================================
// PresetEditionModeController.cs
//
// Controls the Preset edition mode in the Model Editor.
// Manages spline presets for saving and loading tract configurations.
//
// Responsibilities:
//   - Preset management (create, edit, delete, apply)
//   - Preset preview functionality
//   - Coordinate spline preset save/load operations
//   - Handle preset import/export
//
// Dependencies:
//   - UIController (for UI configuration)
//   - InputController (for input actions)
//   - SplinePresetsController (for preset operations)
//   - SplineNodeInteractablesController (for node editing in preset mode)
//   - LargeIntestineModelGenerator (for model access)
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomUI;
using LargeIntestine;
using Messages;
using TMPro;
using UnityEngine;

namespace ModelEditor.EditionModes
{
    /// <summary>
    /// Controls the Preset edition mode for spline preset management.
    /// </summary>
    public class PresetEditionModeController : IEditionModeController
    {
        #region Dependencies

        private readonly IModelEditorUIController _uiController;
        private readonly IModelEditorInputController _inputController;
        private readonly ISplinePresetsController _splinePresetsController;
        private readonly ISplineNodesInteractablesController _splineNodeInteractableController;
        private readonly ISplineModelGenerator _modelGenerator;
        private readonly SelectionController _selectionController;
        private readonly Func<int, bool> _messageCodeHandler;

        #endregion

        #region State

        /// <summary>Whether preset editing mode is active (creating/editing a preset).</summary>
        public bool IsEditionEnabled { get; private set; }
        
        /// <summary>Whether there are unsaved changes to the current preset.</summary>
        public bool HasUnsavedChanges { get; set; }
        
        /// <summary>Whether a preset is currently being previewed.</summary>
        public bool IsPreviewActive { get; private set; }
        
        private SplinePreset _tempCurrentSplinePreset;

        #endregion

        #region Events

        /// <summary>Fired when preset changes are saved.</summary>
        public event Action OnPresetSaved;
        
        /// <summary>Fired when preset changes need notification.</summary>
        public event Action<bool> OnPresetChangesStateChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new PresetEditionModeController with required dependencies.
        /// </summary>
        public PresetEditionModeController(
            UIController uiController,
            InputController inputController,
            ISplinePresetsController splinePresetsController,
            ISplineNodesInteractablesController splineNodeInteractableController,
            ISplineModelGenerator modelGenerator,
            SelectionController selectionController,
            Func<int, bool> messageCodeHandler)
        {
            _uiController = uiController;
            _inputController = inputController;
            _splinePresetsController = splinePresetsController;
            _splineNodeInteractableController = splineNodeInteractableController;
            _modelGenerator = modelGenerator;
            _selectionController = selectionController;
            _messageCodeHandler = messageCodeHandler;
        }

        #endregion

        #region IEditionModeController Implementation

        /// <summary>
        /// Enters preset edition mode.
        /// Shows preset management UI and stores current spline state.
        /// </summary>
        public void Enter()
        {
            // Clear any existing selection
            ClearSelection();
            
            // Show segmented model
            _modelGenerator.SegmentedModelGO.SetActive(true);
            
            // Store current spline nodes for restoration
            _splinePresetsController.StoreCurrentSplineNodes();
            
            // Configure UI for preset management
            ConfigureForManagement();
            
            HasUnsavedChanges = false;
        }

        /// <summary>
        /// Exits preset edition mode.
        /// Restores original spline state if needed and cleans up.
        /// </summary>
        public void Exit()
        {
            // Hide and disable node editing
            _splineNodeInteractableController.HideNodes();
            _splineNodeInteractableController.DisableNodeInteraction();
            
            // Restore original spline if a preset was selected or editing
            if (_uiController.GetSelectedSplinePreset() != null || IsEditionEnabled)
            {
                _splinePresetsController.RestoreSplineNodes();
                _splineNodeInteractableController.AddSplineNodesInteractables();
            }
            
            // Reset UI
            _uiController.ResetPresetsManagementMenu();
            
            if (IsEditionEnabled)
            {
                _uiController.ResetPresetsCreationDataMenu();
                IsEditionEnabled = false;
            }
            
            ClearSelection();
        }

        #endregion

        #region Public Methods - Preset Management

        /// <summary>
        /// Clears the preview state (used when canceling a preset preview).
        /// </summary>
        public void ClearPreviewState()
        {
            IsPreviewActive = false;
        }

        /// <summary>
        /// Applies the currently selected preset to the model.
        /// </summary>
        public void ApplySelectedPreset()
        {
            var selectedPreset = _uiController.GetSelectedSplinePreset();
            if (selectedPreset == null) return;

            _uiController.SetPresetsPreviewFlagObjectActive(false);
            _splinePresetsController.ApplySplinePreset(selectedPreset);
            _splineNodeInteractableController.AddSplineNodesInteractables();
            IsPreviewActive = false;
        }

        /// <summary>
        /// Configures editor for creating a new preset.
        /// </summary>
        public void ConfigureForNewPresetCreation()
        {
            var selectedPreset = _uiController.GetSelectedSplinePreset();
            if (selectedPreset != null)
            {
                _splinePresetsController.RestoreSplineNodes();
            }
            
            ConfigureForPresetCreation(isNewPreset: true);
        }

        /// <summary>
        /// Configures editor for editing an existing preset.
        /// </summary>
        public void ConfigureForExistingPresetEdition()
        {
            ConfigureForPresetCreation(isNewPreset: false);
            ClearSelection();
        }

        /// <summary>
        /// Exits preset editing/creation mode.
        /// Shows confirmation if there are unsaved changes.
        /// </summary>
        public void ExitEditionOrCreation()
        {
            if (HasUnsavedChanges)
            {
                _uiController.ShowConfirmModal(
                    FileMessages.ChangesNotSaved,
                    DisableEditionOrCreation
                );
            }
            else
            {
                DisableEditionOrCreation();
            }
        }

        /// <summary>
        /// Confirms deletion of the selected preset.
        /// </summary>
        public void ConfirmDeletePreset()
        {
            _uiController.ShowConfirmModal(
                FileMessages.ConfirmDelete,
                DeleteSelectedPreset
            );
        }

        #endregion

        #region Public Methods - Preset Save

        /// <summary>
        /// Saves the current preset selection.
        /// </summary>
        public void SavePreset()
        {
            if (!IsValidPresetSaveSelection())
            {
                _uiController.ShowErrorModal(ModelEditorMessages.InvalidPresetSaveSelection);
                return;
            }
            
            var selection = _selectionController.selectedElements;
            var indexes = selection
                .Select(sn => sn.GetComponent<XRSplineNodeInteractable>().idxInSpline)
                .ToList();
            indexes.Sort();
            
            var result = _tempCurrentSplinePreset.Build(
                _uiController.GetPresetDataInputFields(),
                _modelGenerator.Spline.nodes.GetRange(indexes[0], indexes[1] - (indexes[0] - 1)),
                indexes[0]);
            
            if (_messageCodeHandler(result))
            {
                _tempCurrentSplinePreset.Save(OnPresetEditionSaved, _tempCurrentSplinePreset.Overwrite);
            }
        }

        /// <summary>
        /// Saves the preset as a new file.
        /// </summary>
        public void SavePresetAs()
        {
            _tempCurrentSplinePreset = new SplinePreset();
            SavePreset();
        }

        /// <summary>
        /// Checks if the current selection is valid for preset save.
        /// </summary>
        public bool IsValidPresetSaveSelection()
        {
            var selection = _selectionController.selectedElements;
            return selection != null && selection.Count == 2;
        }

        public bool IsPresetEditionEnabledWithUnsavedChanges()
        {
            return IsEditionEnabled && HasUnsavedChanges;
        }

        #endregion

        #region Public Methods - Scroll Area Interaction

        /// <summary>
        /// Handles selection of a preset in the scroll area.
        /// </summary>
        public void OnPresetScrollAreaElementSelect(SplinePresetScrollAreaElement element)
        {
            var selectedElement = _uiController.GetSelectedSplinePresetElement();
            
            if (selectedElement != element)
            {
                // New selection
                if (_uiController.GetSelectedSplinePreset() != null)
                {
                    _uiController.ClearSplinePresetsScrollAreaSelection();
                    _splinePresetsController.RestoreSplineNodes();
                }
                
                element.SetAsSelected();
                _uiController.SetPresetSelectedButtonsInteractable(true);
                _splinePresetsController.PreviewSplinePreset(element.Data);
                IsPreviewActive = true;
            }
            else
            {
                // Deselection
                element.SetAsNotSelected();
                _uiController.SetPresetSelectedButtonsInteractable(false);
                _splinePresetsController.RestoreSplineNodes();
                IsPreviewActive = false;
            }
        }

        /// <summary>
        /// Marks that spline nodes have changed (for preset tracking).
        /// </summary>
        public void MarkChangesNotSaved()
        {
            if (IsEditionEnabled)
            {
                HasUnsavedChanges = true;
                OnPresetChangesStateChanged?.Invoke(true);
            }
        }

        #endregion

        #region Public Methods - Mode Exit Handling

        /// <summary>
        /// Handles exit when in preset creation mode with unsaved changes.
        /// </summary>
        public void HandleExitWithUnsavedChanges(ModelEditorManager.EditionMode targetMode, Action<ModelEditorManager.EditionMode> enterModeCallback)
        {
            _uiController.ShowConfirmModal(
                ModelEditorMessages.PresetChangesNotSaved,
                () => enterModeCallback(targetMode));
        }

        /// <summary>
        /// Handles exit when a preset is being previewed.
        /// </summary>
        public void HandleExitWithPreview(
            ModelEditorManager.EditionMode targetMode, 
            Action<ModelEditorManager.EditionMode> enterModeCallback,
            MonoBehaviour coroutineRunner)
        {
            _uiController.ShowConfirmModal(
                ModelEditorMessages.ConfirmPresetPreviewApplyOnExit,
                () => coroutineRunner.StartCoroutine(ApplyPresetAndExit(targetMode, enterModeCallback)),
                () => OnCancelPresetPreviewExit(targetMode, enterModeCallback)
            );
        }

        #endregion

        #region Import/Export

        /// <summary>
        /// Initiates preset import.
        /// </summary>
        public void ImportPreset()
        {
            throw new NotImplementedException("SplinePresetImport method is not implemented yet.");
        }

        /// <summary>
        /// Initiates preset export.
        /// </summary>
        public void ExportPreset()
        {
            throw new NotImplementedException("SplinePresetExport method is not implemented yet.");
        }

        /// <summary>
        /// Handles file selection for import.
        /// </summary>
        public void OnFileSelectedForImport(string[] paths)
        {
            var sourcePath = paths[0];
            if (!File.Exists(sourcePath))
            {
                _messageCodeHandler(FileErrors.FileNotFound);
                return;
            }
            
            if (_tempCurrentSplinePreset == null)
            {
                _tempCurrentSplinePreset = new SplinePreset();
            }
            
            _tempCurrentSplinePreset.Import(sourcePath, OnPresetImported, _tempCurrentSplinePreset.Overwrite);
        }

        #endregion

        #region UI Configuration

        /// <summary>
        /// Configures UI for preset management view.
        /// </summary>
        public void ConfigureForManagement()
        {
            _uiController.ShowPresetsModeManagementControls();
            _uiController.SetPresetSelectedButtonsInteractable(false);
            _inputController.SetPresetsModeManagementActions();
            _uiController.FillSplinePresetsScrollArea();
            _uiController.SwitchToWindow((int)UIController.EditionWindow.PresetsManagement);
        }

        /// <summary>
        /// Configures UI for preset creation/editing view.
        /// </summary>
        public void ConfigureForCreation()
        {
            _uiController.ShowPresetsModeEditionControls(_selectionController.MultipleSelectionEnabled);
            _inputController.SetPresetsModeManagementActions();
            _uiController.SwitchToWindow((int)UIController.EditionWindow.PresetsCreation);
        }

        #endregion

        #region Private Methods

        private void ConfigureForPresetCreation(bool isNewPreset)
        {
            IsPreviewActive = false;
            ConfigureForCreation();
            
            _uiController.ResetPresetsManagementMenu();
            _splineNodeInteractableController.ShowNodes();
            _splineNodeInteractableController.EnableNodeInteraction();
            IsEditionEnabled = true;
            _splineNodeInteractableController.EnableRecordsForPresetEdition();
            
            if (isNewPreset)
            {
                _tempCurrentSplinePreset = new SplinePreset();
            }
            else
            {
                var selectedPreset = _uiController.GetSelectedSplinePreset();
                _splineNodeInteractableController.AddSplineNodesInteractables();
                _tempCurrentSplinePreset = new SplinePreset(selectedPreset);
                _uiController.UpdatePresetsCreationDataMenuContent(selectedPreset);
            }
        }

        /// <summary>
        /// Disables preset edition/creation mode and restores original state.
        /// </summary>
        public void DisableEditionOrCreation()
        {
            _splinePresetsController.RestoreSplineNodes();
            _splineNodeInteractableController.AddSplineNodesInteractables();
            
            var selectedPreset = _uiController.GetSelectedSplinePreset();
            if (selectedPreset != null)
            {
                _splinePresetsController.PreviewSplinePreset(selectedPreset);
            }
            
            ResetFromPresetCreation();
        }

        private void ResetFromPresetCreation()
        {
            _tempCurrentSplinePreset = null;
            _uiController.ResetPresetsCreationDataMenu();
            ConfigureForManagement();
            _splineNodeInteractableController.HideNodes();
            _splineNodeInteractableController.DisableNodeInteraction();
            _splineNodeInteractableController.DisableRecordsForPresetEdition();
            IsEditionEnabled = false;
        }

        private void DeleteSelectedPreset()
        {
            var selectedPreset = _uiController.GetSelectedSplinePreset();
            selectedPreset.Delete(selectedPreset.GetFullPath(), OnPresetDeleted);
        }

        private void OnPresetEditionSaved()
        {
            _uiController.ShowWarningModal(FileMessages.FileSaved);
            _uiController.UpdateSplinePresetScrollAreaContent(_tempCurrentSplinePreset);
            _uiController.UpdatePresetsCreationDataMenuContent(_tempCurrentSplinePreset);
            HasUnsavedChanges = false;
            OnPresetSaved?.Invoke();
        }

        private void OnPresetDeleted()
        {
            _uiController.ShowWarningModal(FileMessages.FileDeleted);
            _uiController.DeleteSplinePresetScrollAreaSelectedElement();
            _splinePresetsController.RestoreSplineNodes();
        }

        private void OnPresetImported()
        {
            _uiController.ShowWarningModal(FileMessages.FileImported);
            _uiController.UpdateSplinePresetScrollAreaContent(_tempCurrentSplinePreset);
            _tempCurrentSplinePreset = null;
        }

        private IEnumerator ApplyPresetAndExit(ModelEditorManager.EditionMode targetMode, Action<ModelEditorManager.EditionMode> enterModeCallback)
        {
            ApplySelectedPreset();
            yield return new WaitUntil(() => !IsPreviewActive);
            OnExitResult(targetMode, enterModeCallback);
        }

        private void OnCancelPresetPreviewExit(ModelEditorManager.EditionMode targetMode, Action<ModelEditorManager.EditionMode> enterModeCallback)
        {
            _uiController.ResetPresetsManagementMenuSelection();
            IsPreviewActive = false;
            _splinePresetsController.RestoreSplineNodes();
            _splineNodeInteractableController.AddSplineNodesInteractables();
            OnExitResult(targetMode, enterModeCallback);
        }

        private void OnExitResult(ModelEditorManager.EditionMode targetMode, Action<ModelEditorManager.EditionMode> enterModeCallback)
        {
            enterModeCallback(targetMode);
        }

        private void ClearSelection()
        {
            _selectionController.ClearAllOutlines();
        }

        #endregion
    }
}
