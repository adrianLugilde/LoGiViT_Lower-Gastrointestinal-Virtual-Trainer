using System;
using System.Collections.Generic;
using Messages;
using CustomUI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace ModelEditor
{
    /// <summary>
    /// Manages all UI elements and user interactions in the Model Editor.
    /// Provides UI configuration for different edition modes, modal dialogs,
    /// scroll areas, and interactive controls.
    /// </summary>
    /// <remarks>
    /// This controller acts as a facade for all UI operations, centralizing
    /// UI state management and providing a clean interface for edition mode controllers.
    /// Implements IXRKeybindDisplayProfileProvider for VR controller button mapping display.
    /// </remarks>
    public class UIController : MonoBehaviour, IXRKeybindDisplayProfileProvider, IModelEditorUIController
    {
        [Header("Resources")]
        //[SerializeField] private GameObject _xrKeyboard;
        [SerializeField] private XRKeybindDisplayProfile _defaultXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _defaultXRKeybindDisplayProfileGrabVariant;
        [SerializeField] private XRKeybindDisplayProfile _characterLocomotionXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _characterLocomotionXRKeybindDisplayProfileGrabVariant;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeLocomotionXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeLocomotionXRKeybindDisplayProfileGrabVariant;


        [SerializeField] private ModalView _modalView;
        [SerializeField] private SlideEffect _tractModeToolsSlideEffect;
        [SerializeField] private SlideEffect _diseasesModeToolsSlideEffect;
        [SerializeField] private SlideEffect _previewModeToolsSlideEffect;
        [SerializeField] private TMP_InputField[] _modelDataInputFields;
        [SerializeField] private TMP_InputField[] _presetDataInputFields;
        [SerializeField] private GameObject _modelEditorInterfaceObject;
        [SerializeField] private CustomUI.SplinePresetScrollArea _splinePresetScrollArea;
        [SerializeField] private CanvasGroup _detailsSlidersPanelCanvasGroup;
        [SerializeField] private CustomUI.DiseaseScrollArea _diseasesScrollArea;
        [SerializeField] private GameObject _guidedCameraViewMenuObject;
        [SerializeField] private GameObject _noiseSettingsPanelObject;

        [Header("Interactive elements")]
        [Header("Edition mode toggles")]
        [SerializeField] private ToggleController _tractModeToggle;
        [SerializeField] private ToggleController _presetsModeToggle;
        [SerializeField] private ToggleController _detailsModeToggle;
        [SerializeField] private ToggleController _diseasesModeToggle;
        [SerializeField] private ToggleController _previewModeToggle;

        [Header("Common controls")]
        [SerializeField] private ButtonController _undoButton;
        [SerializeField] private ButtonController _redoButton;
        [SerializeField] private ToggleController _multipleSelectionToggle;
        [SerializeField] private ButtonController _clearSelectionButton;
        [Header("Editor controls")]
        [SerializeField] private ButtonController _saveModelButton;
        [SerializeField] private ButtonController _saveModelAsButton;
        [SerializeField] private ButtonController _helpButton;
        [SerializeField] private ButtonController _exitButton;

        [Header("Tract mode controls")]
        [SerializeField] private ToggleController _joinedNodesToggle;
        [Header("Diseases mode controls")]
        [SerializeField] private ButtonController _enlargeDiseaseButton;
        [SerializeField] private ButtonController _shrinkDiseaseButton;
        [SerializeField] private ButtonController _placeDiseaseButton;
        [Header("Preset management controls")]
        [SerializeField] private ButtonController _applyPresetButton;
        [SerializeField] private ButtonController _createPresetButton;
        [SerializeField] private ButtonController _deletePresetButton;
        [SerializeField] private ButtonController _editPresetButton;
        [SerializeField] private ButtonController _importPresetButton;
        [SerializeField] private ButtonController _exportPresetButton;
        [Header("Presets creation controls")]
        [SerializeField] private ButtonController _presetEditionSaveButton;
        [SerializeField] private ButtonController _presetEditionSaveAsButton;
        [SerializeField] private ButtonController _exitPresetEditionButton;

        public event Func<int, bool> OnSplinePresetRead;
        public event Action<int> OnSplineFilterNoResults;
        public event Action<SplinePresetScrollAreaElement> OnSplinePresetScrollAreaElementSelected;
        public event Func<int, bool> OnDiseaseRead;
        public event Action<int> OnDiseaseFilterNoResults;
        public event Action<DiseaseScrollAreaElement> OnDiseaseScrollAreaElementSelected;
        public event Action<int> OnToggleGroupUpdate;
        public event Action<TMP_InputField[]> OnSaveModelModalShowed;
        public event Action<XRKeybindDisplayProfile> OnKeybindDisplayProfileReady;
        /****************Interactive elements Events****************/
        public event Action OnTractModeSelected;
        public event Action OnTractModeDeselected;
        public event Action OnDiseasesModeSelected;
        public event Action OnDiseasesModeDeselected;
        public event Action OnPreviewModeSelected;
        public event Action OnPreviewModeDeselected;

        public event Action OnUndoAction;
        public event Action OnRedoAction;
        public event Action OnMultipleSelectionSelected;
        public event Action OnMultipleSelectionDeselected;
        public event Action OnClearSelectionAction;
        public event Action OnFocusAction; //TODO review if this is still required
        public event Action OnSaveModelAction;
        public event Action OnSaveModelAsAction;
        public event Action OnExitRoomAction;
        public event Action OnHelpAction;
        public event Action OnConfirmExitRoomAction;

        public event Action OnJoinedNodesSelected;
        public event Action OnJoinedNodesDeselected;

        public event Action OnApplyPresetAction;
        public event Action OnCreatePresetAction;
        public event Action OnDeletePresetAction;
        public event Action OnEditPresetAction;
        public event Action OnImportPresetAction;
        public event Action OnExportPresetAction;

        public event Action OnPresetEditionSaveAction;
        public event Action OnPresetEditionSaveAsAction;
        public event Action OnPresetEditionExitAction;

        public event Action OnEnlargeDiseaseAction;
        public event Action OnShrinkDiseaseAction;
        public event Action OnPlaceDiseaseAction;

        public event Action OnChangeCameraAction;



        private bool _useGrabProfiles;
        private MessageStrings _messageStringsHelper;
        private ToggleGroupController _editionModesToggleGroupController;
        private HoverGroupController _hoverGroupController;
        private WindowNavigator _windowNavigator;
        private List<ToggleController> _editionModeToggles = new List<ToggleController>();


        #region MODAL CONFIGS
        private LocalizedString[] modelDataPlaceholdersLocalizedStrings =
        {
            new LocalizedString("ModelEditorBaseTable", "modelName"),
            new LocalizedString("ModelEditorBaseTable", "modelDescription"),
        };


        private LocalizedString[] presetDataPlaceholdersLocalizedStrings =
        {
            new LocalizedString("ModelEditorBaseTable", "presetName"),
            new LocalizedString("ModelEditorBaseTable", "presetDescription"),
        };
        #endregion

        [Flags]
        public enum DiseaseTool
        {
            None = 0,
            Shrink = 1 << 0,
            Enlarge = 1 << 1,
            Place = 1 << 2,
            All = Shrink | Enlarge | Place,
        }

        public enum EditionWindow
        {
            Default = 0,
            PresetsManagement = 1,
            PresetsCreation = 2,
            Details = 3,
            Diseases = 4,
        }

        private void Awake()
        {
            _editionModesToggleGroupController = GetComponentInChildren<ToggleGroupController>();
            if (_editionModesToggleGroupController == null)
                throw new Exception("EditionModesToggleGroupController not found in children.");

            _hoverGroupController = GetComponentInChildren<HoverGroupController>();
            if (_hoverGroupController == null)
                throw new Exception("HoverGroupController not found in children.");

            _windowNavigator = GetComponentInChildren<WindowNavigator>();
            if (_windowNavigator == null)
                throw new Exception("WindowController not found in children.");



            if (_modalView == null)
                throw new Exception("ModalController not assigned in editor.");

            _messageStringsHelper = new Messages.MessageStrings();

            _editionModeToggles = new List<ToggleController>
            {
                _tractModeToggle,
                _presetsModeToggle,
                _detailsModeToggle,
                _diseasesModeToggle,
                _previewModeToggle
            };

            SubscribeEvents();
        }

        private void Start()
        {
            GameObject myObject = GameObject.FindGameObjectWithTag("XRUIHolder");
            if (myObject != null)
            {
                _modelEditorInterfaceObject.transform.SetParent(myObject.transform, false);
            }
            else
            {
                Debug.LogError("No GameObject with tag 'XRUIHolder' found in the scene.");
            }
            /*_xrKeyboard = GameObject.FindGameObjectWithTag("XRKeyboard");
            var inputFields = GameObject.FindObjectsOfType<TMP_InputField>(true);
            foreach (var inputField in inputFields)
            {
                var keyboardDisplay = inputField.gameObject.GetComponent<XRKeyboardDisplay>();
                if (keyboardDisplay != null)
                {
                    keyboardDisplay.keyboard = _xrKeyboard.GetComponent<XRKeyboard>();
                }
            }
            SetXRKeyboardActive(false);*/
            SetBaseKeybindDisplayProfile();
        }

        public void SetUseGrabKeybindProfiles(bool useGrab)
        {
            _useGrabProfiles = useGrab;
            SetBaseKeybindDisplayProfile();
        }

        public void SetBaseKeybindDisplayProfile()
        {
            OnKeybindDisplayProfileReady?.Invoke(_useGrabProfiles ? _defaultXRKeybindDisplayProfileGrabVariant : _defaultXRKeybindDisplayProfile);
        }

        public void SetCharacterLocomotionKeybindDisplayProfile()
        {
            OnKeybindDisplayProfileReady?.Invoke(_useGrabProfiles ? _characterLocomotionXRKeybindDisplayProfileGrabVariant : _characterLocomotionXRKeybindDisplayProfile);
        }

        public void SetEndoscopeLocomotionKeybindDisplayProfile()
        {
            OnKeybindDisplayProfileReady?.Invoke(_useGrabProfiles ? _endoscopeLocomotionXRKeybindDisplayProfileGrabVariant : _endoscopeLocomotionXRKeybindDisplayProfile);
        }

        private void SubscribeEvents()
        {
            _splinePresetScrollArea.OnSplinePresetRead += OnSplinePresetRead;
            _splinePresetScrollArea.OnFilterEmptyResults += OnSplineFilterNoResults;
            _splinePresetScrollArea.OnScrollAreaElementClicked += OnSplinePresetScrollAreaElementSelected;

            _diseasesScrollArea.OnDiseaseRead += OnDiseaseRead;
            _diseasesScrollArea.OnFilterEmptyResults += OnDiseaseFilterNoResults;
            _diseasesScrollArea.OnScrollAreaElementClicked += OnDiseaseScrollAreaElementSelected;

            _editionModesToggleGroupController.BeforeUpdateToggleGroup += HandleToggleGroupUpdate;
            //TODO not happy with this
            for (int i = 0; i < _editionModeToggles.Count; i++)
            {
                _editionModesToggleGroupController.RegisterToggleController(_editionModeToggles[i]);
                var toggleController = _editionModeToggles[i];
            }

            _tractModeToggle.OnSelected += OnTractModeSelected;
            _tractModeToggle.OnDeselected += OnTractModeDeselected;
            _diseasesModeToggle.OnSelected += OnDiseasesModeSelected;
            _diseasesModeToggle.OnDeselected += OnDiseasesModeDeselected;
            _previewModeToggle.OnSelected += OnPreviewModeSelected;
            _previewModeToggle.OnDeselected += OnPreviewModeDeselected;


            _undoButton.OnSelected += OnUndoAction;
            _redoButton.OnSelected += OnRedoAction;
            _saveModelButton.OnSelected += OnSaveModelAction;
            _saveModelAsButton.OnSelected += OnSaveModelAsAction;
            _helpButton.OnSelected += OnHelpAction;
            _exitButton.OnSelected += OnExitRoomAction;

            _multipleSelectionToggle.OnSelected += OnMultipleSelectionSelected;
            _multipleSelectionToggle.OnDeselected += OnMultipleSelectionDeselected;
            _clearSelectionButton.OnSelected += OnClearSelectionAction;
            _joinedNodesToggle.OnSelected += OnJoinedNodesSelected;
            _joinedNodesToggle.OnDeselected += OnJoinedNodesDeselected;

            _applyPresetButton.OnSelected += OnApplyPresetAction;
            _createPresetButton.OnSelected += OnCreatePresetAction;
            _deletePresetButton.OnSelected += OnDeletePresetAction;
            _editPresetButton.OnSelected += OnEditPresetAction;
            _importPresetButton.OnSelected += OnImportPresetAction;
            _exportPresetButton.OnSelected += OnExportPresetAction;

            _presetEditionSaveAsButton.OnSelected += OnPresetEditionSaveAsAction;
            _presetEditionSaveButton.OnSelected += OnPresetEditionSaveAction;
            _exitPresetEditionButton.OnSelected += OnPresetEditionExitAction;

            _placeDiseaseButton.OnSelected += OnPlaceDiseaseAction;
            _enlargeDiseaseButton.OnSelected += OnEnlargeDiseaseAction;
            _shrinkDiseaseButton.OnSelected += OnShrinkDiseaseAction;
        }

        /*public void SetXRKeyboardActive(bool isActive)
        {
            if (_xrKeyboard != null)
            {
                _xrKeyboard.SetActive(isActive);
            }
        }*/

        /// <summary>
        /// Updates the toggle group to select the specified toggle by index.
        /// </summary>
        /// <param name="toggleIndex">Index of the toggle to select (corresponds to EditionMode enum).</param>
        /// <remarks>
        /// Used by ModelEditorManager to sync UI state when edition mode changes programmatically.
        /// Ensures the correct mode toggle button is visually selected.
        /// </remarks>
        public void UpdateToggleGroup(int toggleIndex)
        {
            _editionModesToggleGroupController.UpdateToggleGroup(_editionModesToggleGroupController.GetToggleControllerByIndex(toggleIndex));
        }

        private void HandleToggleGroupUpdate(ToggleController toggleController)
        {
            if (toggleController == null) return;
            OnToggleGroupUpdate?.Invoke(_editionModesToggleGroupController.GetToggleControllerIndex(toggleController));
        }

        /// <summary>
        /// Shows or hides the Tract mode tools panel with slide animation.
        /// </summary>
        /// <param name="isActive">True to slide in, false to slide out.</param>
        public void ToggleTractModeToolsSlideEffect(bool isActive) =>
            ToggleSlideEffect(_tractModeToolsSlideEffect, isActive);

        /// <summary>
        /// Shows or hides the Diseases mode tools panel with slide animation.
        /// </summary>
        /// <param name="isActive">True to slide in, false to slide out.</param>
        public void ToggleDiseasesModeToolsSlideEffect(bool isActive) =>
            ToggleSlideEffect(_diseasesModeToolsSlideEffect, isActive);

        /// <summary>
        /// Shows or hides the Preview mode tools panel with slide animation.
        /// </summary>
        /// <param name="isActive">True to slide in, false to slide out.</param>
        public void TogglePreviewModeToolsSlideEffect(bool isActive) =>
            ToggleSlideEffect(_previewModeToolsSlideEffect, isActive);

        private void ToggleSlideEffect(SlideEffect slideEffect, bool isActive)
        {
            if (isActive)
            {
                slideEffect.SlideIn();

            }
            else
            {
                slideEffect.SlideOut();
            }
        }

        /// <summary>
        /// Updates the model data menu with information from the specified model.
        /// </summary>
        /// <param name="model">Model containing data to display.</param>
        /// <remarks>
        /// Populates input fields with model name and description.
        /// Used when loading an existing model for editing.
        /// </remarks>
        public void UpdateModelDataMenuContent(Model model)
        {
            model.PreviewDataIn(_modelDataInputFields);
        }

        /// <summary>
        /// Shows a warning modal with the specified message.
        /// </summary>
        /// <param name="resultCode">Message code to display (from Messages namespace).</param>
        /// <remarks>
        /// Warning modals are non-blocking informational messages.
        /// User acknowledges by clicking OK.
        /// </remarks>
        public void ShowWarningModal(int resultCode)
        {
            _modalView.ShowModal(
                        ModalType.Warning,
                        _messageStringsHelper.GetMessageLocalizedString(resultCode));
        }

        /// <summary>
        /// Shows an error modal with the specified message and optional confirm action.
        /// </summary>
        /// <param name="resultCode">Error message code to display.</param>
        /// <param name="confirmAction">Optional action to execute when user confirms.</param>
        /// <remarks>
        /// Error modals indicate operation failures or validation errors.
        /// User must acknowledge before continuing.
        /// </remarks>
        public void ShowErrorModal(int resultCode, Action confirmAction = null)
        {
            _modalView.ShowModal(
                        ModalType.Error,
                        _messageStringsHelper.GetMessageLocalizedString(resultCode),
                        confirmAction);
        }

        /// <summary>
        /// Shows a confirmation modal with confirm and cancel actions.
        /// </summary>
        /// <param name="resultCode">Message code for the confirmation question.</param>
        /// <param name="confirmAction">Action to execute if user confirms.</param>
        /// <param name="cancelAction">Optional action to execute if user cancels.</param>
        /// <remarks>
        /// Confirmation modals require explicit user choice (Yes/No).
        /// Used for destructive actions like deleting presets or exiting with unsaved changes.
        /// </remarks>
        public void ShowConfirmModal(int resultCode, Action confirmAction, Action cancelAction = null)
        {
            _modalView.ShowModal(
                ModalType.Confirm,
                _messageStringsHelper.GetMessageLocalizedString(resultCode),
                confirmAction,
                cancelAction
            );
        }

        /// <summary>
        /// Shows the settings modal for application configuration.
        /// </summary>
        /// <remarks>
        /// Settings modal allows user to adjust preferences like language, graphics quality, etc.
        /// </remarks>
        public void ShowSettingsModal()
        {
            _modalView.ShowModal(
               ModalType.Settings,
               null,
               null,
               null,
               null,
               0,
               modelDataPlaceholdersLocalizedStrings);
        }

        /// <summary>
        /// Shows the save model modal for entering file name and description.
        /// </summary>
        /// <param name="saveAction">Action to execute when user confirms save.</param>
        /// <param name="messageKey">Message code for the modal prompt.</param>
        /// <remarks>
        /// Displays input fields for model name and description.
        /// Fires OnSaveModelModalShowed event with input field references.
        /// </remarks>
        public void ShowSaveModelModal(Action saveAction, int messageKey)
        {
            _modalView.ShowModal(
                ModalType.NameDescriptionInput,
                _messageStringsHelper.GetMessageLocalizedString(messageKey),
                saveAction,
                null,
                null,
                0,
                modelDataPlaceholdersLocalizedStrings
            );
            OnSaveModelModalShowed?.Invoke(GetModalInputFields());
        }

        /// <summary>
        /// Shows the disease placement confirmation modal with location selection.
        /// </summary>
        /// <param name="confirmAction">Action to execute when user confirms placement.</param>
        /// <param name="dropdownOptions">Available intestine locations for dropdown.</param>
        /// <param name="intestineLocation">Pre-selected location index (auto-detected).</param>
        /// <remarks>
        /// Allows user to confirm or override the auto-detected intestine section
        /// where the disease will be placed (cecum, colon, rectum, etc.).
        /// </remarks>
        public void ShowConfirmDiseasePlacementModal(Action confirmAction, LocalizedString[] dropdownOptions, int intestineLocation)
        {
            _modalView.ShowModal(
                ModalType.DropdownSelection,
                _messageStringsHelper.GetMessageLocalizedString(ModelEditorMessages.ConfirmDiseasePlacementLocation),
                confirmAction,
                null,
                dropdownOptions,
                intestineLocation);
        }

        /// <summary>
        /// Shows the exit room confirmation modal.
        /// </summary>
        /// <remarks>
        /// Final confirmation before exiting Model Editor and returning to main room.
        /// Fires OnConfirmExitRoomAction event if user confirms.
        /// </remarks>
        public void ShowConfirmExitRoomModal()
        {
            _modalView.ShowModal(
                ModalType.Confirm,
                _messageStringsHelper.GetMessageLocalizedString(ModelEditorMessages.ConfirmExitEditor),
                () => OnConfirmExitRoomAction?.Invoke()
            );
        }


        /// <summary>
        /// Gets the selected index from the modal's dropdown.
        /// </summary>
        /// <returns>Index of selected dropdown option.</returns>
        /// <remarks>
        /// Used after disease placement modal to get user's selected intestine location.
        /// </remarks>
        public int GetModalDropwdownSelectedIndex()
        {
            return _modalView.GetDropdownSelectedIndex();
        }

        /// <summary>
        /// Switches to the specified window in the UI navigator.
        /// </summary>
        /// <param name="windowIndex">Index of window to display (use EditionWindow enum).</param>
        /// <remarks>
        /// Windows include: Default, PresetsManagement, PresetsCreation, Details, Diseases.
        /// Controls which UI panel is visible in the editor interface.
        /// </remarks>
        public void SwitchToWindow(int windowIndex)
        {
            _windowNavigator.OpenWindowByIndex(windowIndex);
        }

        /// <summary>
        /// Gets the input fields from the currently displayed modal.
        /// </summary>
        /// <returns>Array of input fields (typically name and description).</returns>
        public TMP_InputField[] GetModalInputFields() => _modalView.GetTMPInputFields();

        /// <summary>
        /// Gets the text from the file name input field in the modal.
        /// </summary>
        /// <returns>File name entered by user.</returns>
        public string GetModalFileNameInputFieldText() => _modalView.GetTMPInputFields()[0].text;

        #region PRESETS MODE

        /// <summary>
        /// Fills the spline presets scroll area with all available presets.
        /// </summary>
        /// <remarks>
        /// Loads presets from storage and populates the scrollable list.
        /// Called when entering Presets mode.
        /// </remarks>
        public void FillSplinePresetsScrollArea()
        {
            _splinePresetScrollArea.Fill();
        }

        /// <summary>
        /// Updates the content of a preset in the scroll area.
        /// </summary>
        /// <param name="splinePreset">Preset with updated data to display.</param>
        /// <remarks>
        /// Refreshes the UI display for an existing preset after edits.
        /// </remarks>
        public void UpdateSplinePresetScrollAreaContent(SplinePreset splinePreset)
        {
            _splinePresetScrollArea.UpdateContent(splinePreset);
        }

        /// <summary>
        /// Deletes the currently selected preset element from the scroll area.
        /// </summary>
        /// <remarks>
        /// Removes the preset from the UI list after deletion is confirmed.
        /// </remarks>
        public void DeleteSplinePresetScrollAreaSelectedElement()
        {
            _splinePresetScrollArea.DeleteSelectedElement();
        }

        /// <summary>
        /// Clears the selection in the spline presets scroll area.
        /// </summary>
        /// <remarks>
        /// Deselects any currently selected preset, returning UI to neutral state.
        /// </remarks>
        public void ClearSplinePresetsScrollAreaSelection()
        {
            _splinePresetScrollArea.ClearSelection();
        }

        /// <summary>
        /// Updates the presets creation menu with data from a preset.
        /// </summary>
        /// <param name="splinePreset">Preset to load into creation menu.</param>
        /// <remarks>
        /// Populates input fields when editing an existing preset.
        /// </remarks>
        public void UpdatePresetsCreationDataMenuContent(SplinePreset splinePreset)
        {
            splinePreset.PreviewDataIn(_presetDataInputFields);
        }

        /// <summary>
        /// Resets the presets management menu to initial state.
        /// </summary>
        /// <remarks>
        /// Clears selection and returns UI to default state.
        /// Called when exiting preset mode or completing an operation.
        /// </remarks>
        public void ResetPresetsManagementMenu()
        {
            ResetPresetsManagementMenuSelection();
        }

        /// <summary>
        /// Resets the selection in the presets management menu.
        /// </summary>
        /// <remarks>
        /// Deselects the currently selected preset element if any.
        /// </remarks>
        public void ResetPresetsManagementMenuSelection()
        {
            if (_splinePresetScrollArea.SelectedElement != null)
            {
                _splinePresetScrollArea.SelectedElement.SetAsNotSelected();
            }
        }

        /// <summary>
        /// Resets the presets creation data menu to initial state.
        /// </summary>
        /// <remarks>
        /// Clears all input fields (name, description).
        /// Called when starting new preset creation or canceling.
        /// </remarks>
        public void ResetPresetsCreationDataMenu()
        {
            foreach (var inputField in _presetDataInputFields)
            {
                inputField.text = string.Empty;
            }
        }

        /// <summary>
        /// Gets the currently selected spline preset from the scroll area.
        /// </summary>
        /// <returns>Selected preset data, or null if no selection.</returns>
        public SplinePreset GetSelectedSplinePreset()
        {
            if (_splinePresetScrollArea.SelectedElement != null)
            {
                return _splinePresetScrollArea.SelectedElement.Data;
            }
            return null;
        }

        /// <summary>
        /// Gets the currently selected spline preset scroll area element.
        /// </summary>
        /// <returns>Selected UI element, or null if no selection.</returns>
        public SplinePresetScrollAreaElement GetSelectedSplinePresetElement()
        {
            return _splinePresetScrollArea.SelectedElement;
        }

        /// <summary>
        /// Shows or hides the preset preview flag indicator.
        /// </summary>
        /// <param name="isActive">True to show flag, false to hide.</param>
        /// <remarks>
        /// The preview flag visually indicates that a preset is currently being previewed
        /// (applied temporarily to the model without committing).
        /// </remarks>
        public void SetPresetsPreviewFlagObjectActive(bool isActive)
        {
            _splinePresetScrollArea.PreviewFlagObject.SetActive(isActive);
        }

        /// <summary>
        /// Gets the input fields for preset data entry.
        /// </summary>
        /// <returns>Array of input fields for name and description.</returns>
        public TMP_InputField[] GetPresetDataInputFields()
        {
            return _presetDataInputFields;
        }
        #endregion

        #region INTERACTIVE ELEMENTS MANAGEMENT
        /// <summary>
        /// Shows the UI controls for Tract edition mode.
        /// </summary>
        /// <param name="isMultipleSelectionEnabled">Whether multiple node selection is currently enabled.</param>
        /// <remarks>
        /// Enables undo/redo and multiple selection toggle.
        /// Clear selection button enabled only when multiple selection is active.
        /// </remarks>
        public void ShowTractModeControls(bool isMultipleSelectionEnabled)
        {
            _undoButton.SetInteractable(true);
            _redoButton.SetInteractable(true);
            //_clearSelectionButton.SetInteractable(true);
            _multipleSelectionToggle.SetInteractable(true);
            _clearSelectionButton.SetInteractable(isMultipleSelectionEnabled);
        }

        /// <summary>
        /// Shows the UI controls for Presets management mode.
        /// </summary>
        /// <remarks>
        /// Displays preset list with load/edit/delete actions.
        /// Hides preset creation menu and other mode controls.
        /// </remarks>
        public void ShowPresetsModeManagementControls()
        {
            //TODO multipleselectiontoogle enable if it was enabled before?
            _undoButton.SetInteractable(true);
            _redoButton.SetInteractable(true);
            //_clearSelectionButton.SetInteractable(true);
            _multipleSelectionToggle.SetInteractable(false);
            _clearSelectionButton.SetInteractable(false);
        }

        /// <summary>
        /// Shows the UI controls for Presets edition mode.
        /// </summary>
        /// <param name="isMultipleSelectionEnabled">Whether multiple node selection is currently enabled.</param>
        /// <remarks>
        /// Displays input fields for preset name/description and save/cancel actions.
        /// Enables undo/redo and multiple selection toggle for editing preset nodes.
        /// </remarks>
        public void ShowPresetsModeEditionControls(bool isMultipleSelectionEnabled)
        {
            _undoButton.SetInteractable(true);
            _redoButton.SetInteractable(true);
            //_clearSelectionButton.SetInteractable(true);
            _multipleSelectionToggle.SetInteractable(true);
            _clearSelectionButton.SetInteractable(isMultipleSelectionEnabled);
        }
        
        /// <summary>
        /// Sets the interactability of preset action buttons (Edit/Delete).
        /// </summary>
        /// <param name="isInteractive">True to enable buttons, false to disable.</param>
        /// <remarks>
        /// Buttons are enabled when a preset is selected, disabled when no selection.
        /// </remarks>
        public void SetPresetSelectedButtonsInteractable(bool isInteractable)
        {
            _applyPresetButton.SetInteractable(isInteractable);
            _deletePresetButton.SetInteractable(isInteractable);
            _editPresetButton.SetInteractable(isInteractable);
            _importPresetButton.SetInteractable(isInteractable);
            _exportPresetButton.SetInteractable(isInteractable);
        }

        /// <summary>
        /// Shows the UI controls for Details edition mode.
        /// </summary>
        /// <param name="isMultipleSelectionEnabled">Whether multiple section selection is currently enabled.</param>
        /// <remarks>
        /// Displays blendshape sliders for section width/height adjustments.
        /// Enables undo/redo and multiple selection toggle.
        /// </remarks>
        public void ShowDetailsModeControls(bool isMultipleSelectionEnabled)
        {
            _undoButton.SetInteractable(true);
            _redoButton.SetInteractable(true);
            //_clearSelectionButton.SetInteractable(true);
            _multipleSelectionToggle.SetInteractable(true);
            _clearSelectionButton.SetInteractable(isMultipleSelectionEnabled);
        }

        /// <summary>
        /// Shows the UI controls for Diseases edition mode.
        /// </summary>
        /// <remarks>
        /// Displays disease list and placement tools.
        /// Shows guided camera controls for internal navigation.
        /// </remarks>
        public void ShowDiseasesModeControls()
        {
            _undoButton.SetInteractable(true);
            _redoButton.SetInteractable(true);
            //_clearSelectionButton.SetInteractable(false);
            _multipleSelectionToggle.SetInteractable(false);
            _clearSelectionButton.SetInteractable(false);
        }

        /// <summary>
        /// Sets the disease mode tools controls based on disease tool state.
        /// </summary>
        /// <param name="tools">Active disease tool (Select or Place).</param>
        /// <param name="interactive">Whether controls should be interactive.</param>
        /// <remarks>
        /// Toggles between disease selection UI (list) and placement UI (confirmation).
        /// When in Place tool, shows "Place" and "Cancel" buttons.
        /// </remarks>
        public void SetDiseaseModeToolsControls(DiseaseTool tools, bool interactive)
        {
            _shrinkDiseaseButton.SetInteractable(tools.HasFlag(DiseaseTool.Shrink) ? interactive : _shrinkDiseaseButton.IsInteractable);
            _enlargeDiseaseButton.SetInteractable(tools.HasFlag(DiseaseTool.Enlarge) ? interactive : _enlargeDiseaseButton.IsInteractable);
            _placeDiseaseButton.SetInteractable(tools.HasFlag(DiseaseTool.Place) ? interactive : _placeDiseaseButton.IsInteractable);
        }

        /// <summary>
        /// Shows the UI controls for Preview mode.
        /// </summary>
        /// <remarks>
        /// Displays guided camera controls for final model inspection.
        /// Minimal UI for focused viewing experience.
        /// </remarks>
        /// <summary>
        /// Shows the UI controls for Preview mode.
        /// </summary>
        /// <remarks>
        /// Disables editing controls (undo/redo, selection, etc.).
        /// Preview mode is view-only for final model inspection.
        /// </remarks>
        public void ShowPreviewModeControls()
        {
            _undoButton.SetInteractable(false);
            _redoButton.SetInteractable(false);
            _multipleSelectionToggle.SetInteractable(false);
            _clearSelectionButton.SetInteractable(false);
        }

        /// <summary>
        /// Sets the interactability of the clear selection button.
        /// </summary>
        /// <param name="isInteractable">True to enable button, false to disable.</param>
        /// <remarks>
        /// Button is enabled when multiple elements are selected.
        /// </remarks>
        public void SetClearSelectionButtonInteractable(bool isInteractable)
        {
            _clearSelectionButton.SetInteractable(isInteractable);
        }
        #endregion

        #region DETAILS MODE
        
        /// <summary>
        /// Sets the interactability of the details panel (blendshape sliders).
        /// </summary>
        /// <param name="isInteractive">True to enable panel, false to disable.</param>
        /// <remarks>
        /// Panel is enabled when a section is selected for editing.
        /// When disabled, appears dimmed (0.5 alpha) and blocks raycasts.
        /// </remarks>
        public void SetDetailsPanelInteractive(bool isInteractive)
        {
            _detailsSlidersPanelCanvasGroup.alpha = isInteractive ? 1f : 0.5f;
            _detailsSlidersPanelCanvasGroup.interactable = isInteractive;
            _detailsSlidersPanelCanvasGroup.blocksRaycasts = isInteractive;
        }

        #endregion

        #region DISEASES MODE
        /// <summary>
        /// Fills the diseases scroll area with all available diseases.
        /// </summary>
        /// <remarks>
        /// Loads disease definitions and populates the scrollable list.
        /// Called when entering Diseases mode.
        /// </remarks>
        public void FillDiseasesScrollArea()
        {
            _diseasesScrollArea.Fill();
        }

        /// <summary>
        /// Clears the selection in the diseases scroll area.
        /// </summary>
        /// <remarks>
        /// Deselects any currently selected disease.
        /// </remarks>
        public void ClearDiseasesScrollAreaSelection()
        {
            _diseasesScrollArea.ClearSelection();
        }

        /// <summary>
        /// Resets the diseases selection menu to initial state.
        /// </summary>
        /// <remarks>
        /// Deselects the currently selected disease element if any.
        /// Called when exiting disease mode or completing placement.
        /// </remarks>
        public void ResetDiseasesSelectionMenu()
        {
            if (_diseasesScrollArea.SelectedElement != null)
            {
                _diseasesScrollArea.SelectedElement.SetAsNotSelected();
            }
        }

        /// <summary>
        /// Gets the currently selected disease from the scroll area.
        /// </summary>
        /// <returns>Selected disease data, or null if no selection.</returns>
        public Disease GetSelectedDisease()
        {
            if (_diseasesScrollArea.SelectedElement != null)
            {
                return _diseasesScrollArea.SelectedElement.Data;
            }
            return null;
        }

        /// <summary>
        /// Gets the currently selected disease scroll area element.
        /// </summary>
        /// <returns>Selected UI element, or null if no selection.</returns>
        public DiseaseScrollAreaElement GetSelectedDiseaseElement()
        {
            return _diseasesScrollArea.SelectedElement;
        }
        #endregion

        #region GUIDED CAMERA 
        /// <summary>
        /// Shows or hides the guided camera view menu.
        /// </summary>
        /// <param name="isActive">True to show menu, false to hide.</param>
        /// <remarks>
        /// The guided camera view menu provides controls for internal camera navigation
        /// in Diseases and Preview modes (centering, FOV adjustment, etc.).
        /// </remarks>
        public void SetGuidedCameraViewMenuActive(bool isActive)
        {
            _guidedCameraViewMenuObject.SetActive(isActive);
        }

        public void SetNoisePanelActive(bool isActive)
        {
            _noiseSettingsPanelObject.SetActive(isActive);
        }
        #endregion

        #region HELPERS
        /// <summary>
        /// Sets the interactability of multiple responsive interactive elements.
        /// </summary>
        /// <param name="isInteractive">True to enable elements, false to disable.</param>
        /// <param name="elements">Variable number of elements to update.</param>
        /// <remarks>
        /// Utility method for batch updating element interactability.
        /// Responsive elements handle visual feedback when disabled (dimming, etc.).
        /// </remarks>
        public void SetInteractable(bool isInteractive, params ResponsiveInteractiveElement[] elements)
        {
            foreach (var element in elements)
            {
                element.SetInteractable(isInteractive);
            }
        }
        #endregion

        private void OnDestroy()
        {
            Destroy(_modelEditorInterfaceObject);
        }
    }
}