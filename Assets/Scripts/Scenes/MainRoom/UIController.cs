// ============================================================================
// UIController.cs
//
// Manages the UI for the colonoscopy preparation room. Handles window navigation,
// button interactions, modal dialogs, and model scroll area.
//
// Usage:
//   - Accessed via ColonoscopyRoomManager.UIController
//   - Subscribe to button events (OnTrainingsButtonPressed, etc.) for user actions
//   - Use high-level methods for window switching:
//     - ShowTrainingsWindow() / ShowModelEditingWindow()
//     - SwitchToMainMenuWindow() / SwitchToSettingsWindow()
//   - Use ShowBasicModal() for message display
//
// Window Indices (PreparationRoomWindow enum):
//   0 = MainMenu, 1 = TrainingsOrModelEdition, 2 = Settings, 3 = RoomOccupied
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using Messages;
using CustomUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

namespace MainRoom
{
    /// <summary>
    /// Controls the UI for the colonoscopy preparation room.
    /// Manages window navigation, button events, modals, and model selection.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        #region Serialized Fields - Resources

        [Header("Resources")]

        /// <summary>
        /// Default keybind profile for this scene.
        /// </summary>
        [SerializeField] private XRKeybindDisplayProfile _xrKeybindDefaultDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _xrKeybindDefaultDisplayProfileGrabVariant;

        /// <summary>
        /// Scroll area displaying available models.
        /// </summary>
        [SerializeField] private ModelScrollArea _modelScrollArea;

        /// <summary>
        /// Localized title for trainings/model editing shared window.
        /// </summary>
        [SerializeField] private LocalizeStringEvent _trainingsModelEditingTitleLocalizeStringEvent;

        [SerializeField] private LocalizedString _trainingsTitleLocalizedString;

        [SerializeField] private LocalizedString _modelEditingTitleLocalizedString;

        /// <summary>
        /// Container for training-specific buttons.
        /// </summary>
        [SerializeField] private GameObject _trainingsButtonsContainerObject;

        /// <summary>
        /// Container for model editing-specific buttons.
        /// </summary>
        [SerializeField] private GameObject _modelEditingButtonsContainerObject;

        /// <summary>
        /// Modal view for dialogs and confirmations.
        /// </summary>
        [SerializeField] private ModalView _modalView;

        #endregion

        #region Serialized Fields - Buttons

        [Header("Main Menu Buttons")]
        [SerializeField] private ButtonController _trainingsButton;
        [SerializeField] private ButtonController _modelEditingButton;
        [SerializeField] private ButtonController _settingsButton;
        [SerializeField] private ButtonController _exitApplicationButton;

        [Header("Training Buttons")]
        [SerializeField] private ButtonController _coverageTrainingButton;
        [SerializeField] private ButtonController _polypDetectionButton;
        [SerializeField] private ButtonController _returnFromTrainingsButton;

        [Header("Model Editing Buttons")]
        [SerializeField] private ButtonController _createModelButton;
        [SerializeField] private ButtonController _editModelButton;
        [SerializeField] private ButtonController _deleteModelButton;
        [SerializeField] private ButtonController _importModelButton;
        [SerializeField] private ButtonController _exportModelButton;
        [SerializeField] private ButtonController _returnFromModelEditionButton;

        [Header("Settings Buttons")]
        [SerializeField] private ButtonController _saveSettingsButton;
        [SerializeField] private ButtonController _returnFromSettingsButton;

        [Header("Settings Dropdowns")]
        [SerializeField] private TMP_Dropdown _localeDropdown;
        [SerializeField] private LocaleDropdownOption[] _localeOptions;
        [SerializeField] private TMP_Dropdown _grabControlDropdown;
        [SerializeField] private GrabDropdownOption[] _grabOptions;

        [Header("In-Training Buttons")]
        [SerializeField] private ButtonController _enterTrainingRoomButton;

        #endregion

        #region Events - Button Actions

        /// <summary>Fired when trainings button is pressed.</summary>
        public event Action OnTrainingsButtonPressed;

        /// <summary>Fired when model editing button is pressed.</summary>
        public event Action OnModelEditingButtonPressed;

        /// <summary>Fired when settings button is pressed.</summary>
        public event Action OnSettingsButtonPressed;

        /// <summary>Fired when exit application button is pressed.</summary>
        public event Action OnExitAppButtonPressed;

        /// <summary>Fired when coverage training button is pressed.</summary>
        public event Action OnCoverageTrainingButtonPressed;

        /// <summary>Fired when polyp detection button is pressed.</summary>
        public event Action OnPolypDetectionButtonPressed;

        /// <summary>Fired when return from trainings button is pressed.</summary>
        public event Action OnReturnFromTrainingsButtonPressed;

        /// <summary>Fired when new model button is pressed.</summary>
        public event Action OnNewModelButtonPressed;

        /// <summary>Fired when edit model button is pressed.</summary>
        public event Action OnEditModelButtonPressed;

        /// <summary>Fired when import model button is pressed.</summary>
        public event Action OnImportModelButtonPressed;

        /// <summary>Fired when export model button is pressed.</summary>
        public event Action OnExportModelButtonPressed;

        /// <summary>Fired when delete model button is pressed.</summary>
        public event Action OnDeleteModelButtonPressed;

        /// <summary>Fired when return from model edition button is pressed.</summary>
        public event Action OnReturnFromModelEditionButtonPressed;

        /// <summary>Fired when save settings button is pressed.</summary>
        public event Action OnSaveSettingsButtonPressed;

        /// <summary>Fired when return from settings button is pressed.</summary>
        public event Action OnReturnFromSettingsButtonPressed;

        /// <summary>Fired when enter training room button is pressed.</summary>
        public event Action OnEnterSecondaryRoomButtonPressed;

        #endregion

        #region Events - Model Scroll Area

        /// <summary>Fired when a model is read, returning result code.</summary>
        public event Func<int, bool> OnModelRead;

        /// <summary>Fired when model filter returns no results.</summary>
        public event Action<int> OnModelFilterNoResults;

        /// <summary>Fired when a model element is selected.</summary>
        public event Action<ModelScrollAreaElement> OnModelScrollAreaElementSelected;

        #endregion

        #region Private Fields

        private WindowNavigator _windowNavigator;
        private MessageStrings _messageStringsHelper;
        private XRKeybindDisplayProfileView _keybindDisplayProfileView;
        private bool _useGrabProfiles;

        #endregion

        #region Structs

        [Serializable]
        public struct LocaleDropdownOption
        {
            public LocalizedString label;
            public string localeCode;
        }

        [Serializable]
        public struct GrabDropdownOption
        {
            public LocalizedString label;
            public bool value;
        }

        #endregion

        #region Enums

        /// <summary>
        /// Window indices for the preparation room UI.
        /// The trainings and model edition windows share the same window with different configurations.
        /// </summary>
        public enum PreparationRoomWindow
        {
            MainMenu = 0,
            TrainingsOrModelEdition = 1,
            Settings = 2,
            RoomOccupied = 3,
        }

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Initializes components and subscribes to button events.
        /// </summary>
        private void Awake()
        {
            InitializeComponents();
            SubscribeToButtonEvents();
            //SetBaseKeybindDisplayProfile();
        }

        /// <summary>
        /// Cleans up event subscriptions.
        /// </summary>
        private void OnDestroy()
        {
            UnsubscribeFromButtonEvents();
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Gets references to required components.
        /// </summary>
        private void InitializeComponents()
        {
            _windowNavigator = GetComponentInChildren<WindowNavigator>();
            if (_windowNavigator == null)
            {
                Debug.LogError("[UIController] WindowNavigator not found in children.");
                enabled = false;
                return;
            }

            if (_modalView == null)
            {
                Debug.LogError("[UIController] ModalView not assigned in editor.");
                enabled = false;
                return;
            }

            _keybindDisplayProfileView = GetComponentInChildren<XRKeybindDisplayProfileView>();
            if (_keybindDisplayProfileView == null)
            {
                Debug.LogError("[UIController] KeybindDisplayProfileView not found in children.");
                enabled = false;
                return;
            }

            _messageStringsHelper = new MessageStrings();
        }

        #endregion

        #region Event Subscription

        /// <summary>
        /// Subscribes to all button and scroll area events.
        /// </summary>
        private void SubscribeToButtonEvents()
        {
            // Model scroll area events
            _modelScrollArea.OnModelRead += OnModelRead;
            _modelScrollArea.OnFilterEmptyResults += OnModelFilterNoResults;
            _modelScrollArea.OnScrollAreaElementClicked += OnModelScrollAreaElementSelected;

            // Main menu buttons
            _trainingsButton.OnSelected += OnTrainingsButtonPressed;
            _modelEditingButton.OnSelected += OnModelEditingButtonPressed;
            _settingsButton.OnSelected += OnSettingsButtonPressed;
            _exitApplicationButton.OnSelected += OnExitAppButtonPressed;

            // Training buttons
            _coverageTrainingButton.OnSelected += OnCoverageTrainingButtonPressed;
            _polypDetectionButton.OnSelected += OnPolypDetectionButtonPressed;
            _returnFromTrainingsButton.OnSelected += OnReturnFromTrainingsButtonPressed;

            // Model editing buttons
            _createModelButton.OnSelected += OnNewModelButtonPressed;
            _editModelButton.OnSelected += OnEditModelButtonPressed;
            _importModelButton.OnSelected += OnImportModelButtonPressed;
            _exportModelButton.OnSelected += OnExportModelButtonPressed;
            _deleteModelButton.OnSelected += OnDeleteModelButtonPressed;
            _returnFromModelEditionButton.OnSelected += OnReturnFromModelEditionButtonPressed;

            // Settings buttons
            _saveSettingsButton.OnSelected += OnSaveSettingsButtonPressed;
            _returnFromSettingsButton.OnSelected += OnReturnFromSettingsButtonPressed;

            // Other buttons
            _enterTrainingRoomButton.OnSelected += OnEnterSecondaryRoomButtonPressed;
        }

        /// <summary>
        /// Unsubscribes from all button and scroll area events.
        /// </summary>
        private void UnsubscribeFromButtonEvents()
        {
            // Model scroll area events
            _modelScrollArea.OnModelRead -= OnModelRead;
            _modelScrollArea.OnFilterEmptyResults -= OnModelFilterNoResults;
            _modelScrollArea.OnScrollAreaElementClicked -= OnModelScrollAreaElementSelected;

            // Main menu buttons
            _trainingsButton.OnSelected -= OnTrainingsButtonPressed;
            _modelEditingButton.OnSelected -= OnModelEditingButtonPressed;
            _settingsButton.OnSelected -= OnSettingsButtonPressed;
            _exitApplicationButton.OnSelected -= OnExitAppButtonPressed;

            // Training buttons
            _coverageTrainingButton.OnSelected -= OnCoverageTrainingButtonPressed;
            _polypDetectionButton.OnSelected -= OnPolypDetectionButtonPressed;
            _returnFromTrainingsButton.OnSelected -= OnReturnFromTrainingsButtonPressed;

            // Model editing buttons
            _createModelButton.OnSelected -= OnNewModelButtonPressed;
            _editModelButton.OnSelected -= OnEditModelButtonPressed;
            _importModelButton.OnSelected -= OnImportModelButtonPressed;
            _exportModelButton.OnSelected -= OnExportModelButtonPressed;
            _deleteModelButton.OnSelected -= OnDeleteModelButtonPressed;
            _returnFromModelEditionButton.OnSelected -= OnReturnFromModelEditionButtonPressed;

            // Settings buttons
            _saveSettingsButton.OnSelected -= OnSaveSettingsButtonPressed;
            _returnFromSettingsButton.OnSelected -= OnReturnFromSettingsButtonPressed;
        }

        #endregion

        #region High-Level Window Methods

        /// <summary>
        /// Shows the trainings window with model selection configured for training.
        /// </summary>
        public void ShowTrainingsWindow()
        {
            ClearModelsScrollAreaSelection();
            ConfigureSharedWindowForTrainings();
            FillModelScrollArea();
            SetTrainingSelectionButtonsInteractable(false);
            SwitchToTrainingsOrModelEditionWindow();
        }

        /// <summary>
        /// Shows the model editing window with model selection configured for editing.
        /// </summary>
        public void ShowModelEditingWindow()
        {
            ClearModelsScrollAreaSelection();
            ConfigureSharedWindowForModelEditing();
            FillModelScrollArea();
            SetModelEditingSelectionButtonsInteractable(false);
            SwitchToTrainingsOrModelEditionWindow();
        }

        /// <summary>
        /// Refreshes the model list, forcing an update.
        /// </summary>
        public void RefreshModelList()
        {
            FillModelScrollArea(forceUpdate: true);
        }

        #endregion

        #region Window Navigation

        /// <summary>
        /// Switches to a window by index.
        /// </summary>
        private void SwitchToWindow(int windowIndex)
        {
            _windowNavigator.OpenWindowByIndex(windowIndex);
        }

        /// <summary>
        /// Switches to the main menu window.
        /// </summary>
        public void SwitchToMainMenuWindow() => SwitchToWindow((int)PreparationRoomWindow.MainMenu);

        /// <summary>
        /// Switches to the trainings/model edition shared window.
        /// </summary>
        public void SwitchToTrainingsOrModelEditionWindow() => SwitchToWindow((int)PreparationRoomWindow.TrainingsOrModelEdition);

        /// <summary>
        /// Switches to the settings window.
        /// </summary>
        public void SwitchToSettingsWindow() => SwitchToWindow((int)PreparationRoomWindow.Settings);

        /// <summary>
        /// Switches to the room occupied window.
        /// </summary>
        public void SwitchToRoomOccupiedWindow() => SwitchToWindow((int)PreparationRoomWindow.RoomOccupied);

        /// <summary>
        /// Switches to the previous window based on navigation history.
        /// </summary>
        public void SwitchToPreviousWindow()
        {
            var previousWindow = (PreparationRoomWindow)_windowNavigator.previousWindowIndex;

            switch (previousWindow)
            {
                case PreparationRoomWindow.TrainingsOrModelEdition:
                    SwitchToTrainingsOrModelEditionWindow();
                    break;
                case PreparationRoomWindow.Settings:
                    SwitchToSettingsWindow();
                    break;
                case PreparationRoomWindow.RoomOccupied:
                    SwitchToRoomOccupiedWindow();
                    break;
                default:
                    SwitchToMainMenuWindow();
                    break;
            }
        }

        #endregion

        #region Shared Window Configuration

        /// <summary>
        /// Configures the shared window for training mode.
        /// </summary>
        public void ConfigureSharedWindowForTrainings()
        {
            _trainingsModelEditingTitleLocalizeStringEvent.StringReference = _trainingsTitleLocalizedString;
            _trainingsButtonsContainerObject.SetActive(true);
            _modelEditingButtonsContainerObject.SetActive(false);
        }

        /// <summary>
        /// Configures the shared window for model editing mode.
        /// </summary>
        public void ConfigureSharedWindowForModelEditing()
        {
            _trainingsModelEditingTitleLocalizeStringEvent.StringReference = _modelEditingTitleLocalizedString;
            _trainingsButtonsContainerObject.SetActive(false);
            _modelEditingButtonsContainerObject.SetActive(true);
        }

        #endregion

        #region Keybind Display

        /// <summary>
        /// Sets the XR keybind display profile.
        /// </summary>
        public void SetXRKeybindDisplayProfile(XRKeybindDisplayProfile profile)
        {
            _keybindDisplayProfileView.UpdateKeybindDisplay(profile);
        }

        /// <summary>
        /// Resets to the base keybind display profile.
        /// </summary>
        public void SetBaseKeybindDisplayProfile()
        {
            SetXRKeybindDisplayProfile(_useGrabProfiles ? _xrKeybindDefaultDisplayProfileGrabVariant : _xrKeybindDefaultDisplayProfile);
        }

        public void SetUseGrabKeybindProfiles(bool useGrab)
        {
            _useGrabProfiles = useGrab;
            SetBaseKeybindDisplayProfile();
        }

        #endregion

        #region Button Interactability

        /// <summary>
        /// Sets the interactability of model editing selection buttons.
        /// </summary>
        public void SetModelEditingSelectionButtonsInteractable(bool isInteractive)
        {
            _editModelButton.SetInteractable(isInteractive);
            _deleteModelButton.SetInteractable(isInteractive);
            _exportModelButton.SetInteractable(isInteractive);
        }

        /// <summary>
        /// Sets the interactability of training selection buttons.
        /// </summary>
        public void SetTrainingSelectionButtonsInteractable(bool isInteractive)
        {
            SetCoverageTrainingButtonInteractable(isInteractive);
            SetPolypDetectionButtonInteractable(isInteractive);
        }

        /// <summary>
        /// Sets the interactability of the coverage training button.
        /// </summary>
        public void SetCoverageTrainingButtonInteractable(bool isInteractive)
        {
            _coverageTrainingButton.SetInteractable(isInteractive);
        }

        /// <summary>
        /// Sets the interactability of the polyp detection button.
        /// </summary>
        public void SetPolypDetectionButtonInteractable(bool isInteractive)
        {
            _polypDetectionButton.SetInteractable(isInteractive);
        }

        #endregion

        #region Settings Dropdowns

        public void InitializeSettingsDropdowns()
        {
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            StartCoroutine(InitSettingsDropdownsCoroutine());
        }

        private IEnumerator InitSettingsDropdownsCoroutine()
        {
            yield return LocalizationSettings.InitializationOperation;
            PopulateSettingsDropdownLabels();
        }

        private void OnSelectedLocaleChanged(Locale _)
        {
            PopulateSettingsDropdownLabels();
        }

        private void PopulateSettingsDropdownLabels()
        {
            int localeIdx = _localeDropdown.value;
            var localeList = new List<TMP_Dropdown.OptionData>(_localeOptions.Length);
            foreach (var o in _localeOptions)
                localeList.Add(new TMP_Dropdown.OptionData(o.label.GetLocalizedString()));
            _localeDropdown.options = localeList;
            _localeDropdown.SetValueWithoutNotify(localeIdx);

            int grabIdx = _grabControlDropdown.value;
            var grabList = new List<TMP_Dropdown.OptionData>(_grabOptions.Length);
            foreach (var o in _grabOptions)
                grabList.Add(new TMP_Dropdown.OptionData(o.label.GetLocalizedString()));
            _grabControlDropdown.options = grabList;
            _grabControlDropdown.SetValueWithoutNotify(grabIdx);
        }

        public string GetSelectedLocaleCode()
        {
            int idx = _localeDropdown.value;
            return idx >= 0 && idx < _localeOptions.Length ? _localeOptions[idx].localeCode : null;
        }

        public void SetSelectedLocaleCode(string code)
        {
            for (int i = 0; i < _localeOptions.Length; i++)
            {
                if (_localeOptions[i].localeCode == code)
                {
                    _localeDropdown.SetValueWithoutNotify(i);
                    return;
                }
            }
            _localeDropdown.SetValueWithoutNotify(0);
        }

        public bool GetSelectedGrabValue()
        {
            int idx = _grabControlDropdown.value;
            return idx >= 0 && idx < _grabOptions.Length && _grabOptions[idx].value;
        }

        public void SetSelectedGrabValue(bool value)
        {
            for (int i = 0; i < _grabOptions.Length; i++)
            {
                if (_grabOptions[i].value == value)
                {
                    _grabControlDropdown.SetValueWithoutNotify(i);
                    return;
                }
            }
            _grabControlDropdown.SetValueWithoutNotify(0);
        }

        #endregion

        #region Modal Dialogs

        /// <summary>
        /// Shows a modal dialog for the given result code.
        /// </summary>
        /// <param name="resultCode">The message/error code to display.</param>
        public void ShowBasicModal(int resultCode)
        {
            switch (resultCode)
            {
                case FileErrors.FileParsing:
                    ShowErrorModal(FileErrors.FileParsing);
                    break;

                case BasicMessages.SearchWithoutResults:
                    ShowWarningModal(BasicMessages.SearchWithoutResults);
                    break;

                case FileMessages.FileDeleted:
                    ShowWarningModal(FileMessages.FileDeleted);
                    break;

                default:
                    ShowErrorModal(BasicMessages.Unexpected);
                    break;
            }
        }

        /// <summary>
        /// Shows an error modal with the specified message code.
        /// </summary>
        private void ShowErrorModal(int resultCode)
        {
            _modalView.ShowModal(
                ModalType.Error,
                _messageStringsHelper.GetMessageLocalizedString(resultCode));
        }

        /// <summary>
        /// Shows a warning modal with the specified message code.
        /// </summary>
        private void ShowWarningModal(int resultCode)
        {
            _modalView.ShowModal(
                ModalType.Warning,
                _messageStringsHelper.GetMessageLocalizedString(resultCode));
        }

        /// <summary>
        /// Shows a confirmation modal for model deletion.
        /// </summary>
        /// <param name="onConfirm">Action to execute on confirmation.</param>
        public void ShowConfirmDeleteModal(Action onConfirm)
        {
            _modalView.ShowModal(
                ModalType.Confirm,
                _messageStringsHelper.GetMessageLocalizedString(FileMessages.ConfirmDelete),
                onConfirm);
        }

        /// <summary>
        /// Shows a confirmation modal warning that settings changes are unsaved.
        /// </summary>
        /// <param name="onConfirm">Action to execute if user confirms discarding changes.</param>
        public void ShowSettingsChangesNotSavedModal(Action onConfirm)
        {
            _modalView.ShowModal(
                ModalType.Confirm,
                _messageStringsHelper.GetMessageLocalizedString(BasicMessages.SettingsChangesNotSaved),
                onConfirm);
        }

        #endregion

        #region Model Scroll Area

        /// <summary>
        /// Gets the currently selected model.
        /// </summary>
        /// <returns>The selected model, or null if none selected.</returns>
        public Model GetSelectedModel()
        {
            return _modelScrollArea.SelectedElement?.Data;
        }

        /// <summary>
        /// Gets the currently selected model scroll area element.
        /// </summary>
        /// <returns>The selected element, or null if none selected.</returns>
        public ModelScrollAreaElement GetSelectedModelScrollAreaElement()
        {
            return _modelScrollArea.SelectedElement;
        }

        /// <summary>
        /// Fills the model scroll area with available models.
        /// </summary>
        /// <param name="forceUpdate">Whether to force a refresh of the model list.</param>
        public void FillModelScrollArea(bool forceUpdate = false)
        {
            if (forceUpdate)
            {
                _modelScrollArea.NeedsUpdate = true;
            }
            _modelScrollArea.Fill();
        }

        /// <summary>
        /// Clears the current selection in the model scroll area.
        /// </summary>
        public void ClearModelsScrollAreaSelection()
        {
            _modelScrollArea.ClearSelection();
        }

        /// <summary>
        /// Deletes the currently selected element from the model scroll area.
        /// </summary>
        public void DeleteModelScrollAreaSelectedElement()
        {
            _modelScrollArea.DeleteSelectedElement();
        }

        #endregion
    }
}
