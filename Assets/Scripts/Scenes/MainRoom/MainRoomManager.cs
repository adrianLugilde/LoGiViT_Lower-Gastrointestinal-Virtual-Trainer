// ============================================================================
// MainRoomManager.cs
//
// Central manager for the main room scene. Coordinates communication between
// UI, XR systems, and environment-specific handlers.
//
// Usage:
//   - Access via MainRoomManager.Instance (singleton)
//   - Automatically initializes controllers and subscribes to events on Awake
//   - Delegates environment-specific work to IEnvironmentHandler
//
// Dependencies:
//   - ApplicationManager (must exist before this)
//   - SceneFlowController (must exist before this)
//   - UIController (child component)
//   - InputController (child component)
//   - IEnvironmentHandler (assigned via Inspector)
// ============================================================================

using System;
using CustomUI;
using Messages;
using UnityEngine;

namespace MainRoom
{
    /// <summary>
    /// Central manager for the main room.
    /// Coordinates UI interactions, scene transitions, and XR teleportation.
    /// Delegates environment-specific logic to IEnvironmentHandler.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MainRoomManager : MonoBehaviour
    {
        #region Singleton

        /// <summary>
        /// Singleton instance accessible throughout the application.
        /// </summary>
        public static MainRoomManager Instance { get; private set; }

        #endregion

        #region Public Properties

        /// <summary>
        /// UI controller for the main room interface.
        /// </summary>
        public UIController UIController { get; private set; }

        /// <summary>
        /// Input controller for XR locomotion and HMD management.
        /// </summary>
        public InputController InputController { get; private set; }

        /// <summary>
        /// Controller for model file import/export operations.
        /// </summary>
        public ModelFileBrowserController ModelFileBrowserController { get; private set; }

        /// <summary>
        /// Environment handler for organ-specific logic.
        /// </summary>
        public IEnvironmentHandler EnvironmentHandler { get; private set; }

        #endregion

        #region Private Fields

        private ApplicationManager _applicationManager;
        private SceneFlowController _sceneFlowController;
        private IAppSettingsProvider _appSettings;

        /// <summary>
        /// GameObject containing the environment handler component.
        /// </summary>
        [SerializeField]
        private GameObject _environmentHandlerObject;

        private string _savedLocaleCode;
        private bool _savedGrabValue;

        /// <summary>
        /// Tracks whether the secondary room window (vs model editing window) is active.
        /// Used to determine button behavior when selecting models.
        /// </summary>
        private bool _isSecondaryRoomWindowEnabled = false;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Initializes singleton, gets dependencies, and subscribes to events.
        /// </summary>
        private void Awake()
        {
            // Enforce singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Get application manager dependency
            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null)
                throw new Exception("ApplicationManager not found.");

            SetControllers();
            ConfigureControllers();
            SubscribeEvents();
        }

        /// <summary>
        /// Subscribes to delayed events that require other components to be initialized.
        /// </summary>
        private void Start()
        {
            ApplyAppSettings();
            SubscribeToDelayedEvents();
        }

        private void ApplyAppSettings()
        {
            _appSettings = _applicationManager.AppSettings;
            if (_appSettings != null)
            {
                UIController.SetUseGrabKeybindProfiles(_appSettings.UseGrabKeybindProfiles);
                _appSettings.OnUseGrabKeybindProfilesChanged += UIController.SetUseGrabKeybindProfiles;
                InputController.SetUseGrabKeybindProfiles(_appSettings.UseGrabKeybindProfiles);
                _appSettings.OnUseGrabKeybindProfilesChanged += InputController.SetUseGrabKeybindProfiles;
                UIController.InitializeSettingsDropdowns();
            }
            else
            {
                Debug.LogError("[MainRoomManager] IAppSettingsProvider not available from ApplicationManager.");
            }
        }

        private void ConfigureControllers()
        {
            // Initialize environment handler with dependencies
            EnvironmentHandler.Initialize(_applicationManager, UIController);
            ConfigureModelFileBrowser();
        }

        /// <summary>
        /// Cleans up all event subscriptions to prevent memory leaks.
        /// </summary>
        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Gets references to all required controllers and validates they exist.
        /// </summary>
        private void SetControllers()
        {
            UIController = GetComponentInChildren<UIController>();
            if (UIController == null)
                throw new Exception("UIController not found in children.");

            InputController = GetComponentInChildren<InputController>();
            if (InputController == null)
                throw new Exception("InputController not found in children.");

            _sceneFlowController = SceneFlowController.Instance;
            if (_sceneFlowController == null)
                throw new Exception("SceneFlowController instance not found.");

            if (_environmentHandlerObject == null)
                throw new Exception("Environment handler GameObject reference not set in Inspector.");

            EnvironmentHandler = _environmentHandlerObject.GetComponent<IEnvironmentHandler>();
            if (EnvironmentHandler == null)
                throw new Exception("IEnvironmentHandler component not found on assigned GameObject.");
        }

        /// <summary>
        /// Configures the model file browser controller with required callbacks.
        /// </summary>
        private void ConfigureModelFileBrowser()
        {
            ModelFileBrowserController = new ModelFileBrowserController
            {
                GetCurrentModel = _applicationManager.GetCurrentModel,
                GetSelectedModel = UIController.GetSelectedModel
            };
            ModelFileBrowserController.SetCurrentModel += _applicationManager.SetCurrentModel;
        }

        /// <summary>
        /// Subscribes to transition controller events via environment handler.
        /// </summary>
        private void SubscribeToTransitionEvents()
        {
            EnvironmentHandler.SubscribeToTransitionEvents(HandleReturnFromSecondaryRoom);
        }

        #endregion

        #region Event Subscription

        /// <summary>
        /// Subscribes to all required events from controllers and UI.
        /// </summary>
        private void SubscribeEvents()
        {
            SubscribeToTransitionEvents();
            SubscribeToSceneFlowEvents();
            SubscribeToUIEvents();
            EnvironmentHandler.SubscribeToUIEvents();
        }

        /// <summary>
        /// Subscribes to events that need other components to be fully initialized.
        /// </summary>
        private void SubscribeToDelayedEvents()
        {
            _sceneFlowController.OnSceneLoadedAction += HandleSceneLoaded;
            _sceneFlowController.OnSceneUnloadedAction += UIController.SwitchToPreviousWindow;
        }

        /// <summary>
        /// Subscribes to scene flow controller events.
        /// </summary>
        private void SubscribeToSceneFlowEvents()
        {
            _sceneFlowController.OnModelEditorSceneUnloadedAction += HandleModelEditorUnloaded;
            _sceneFlowController.OnKeybindDisplayProfileReady += HandleKeybindProfileReady;
            _sceneFlowController.OnDesktopModeChangeNotified += HandleDesktopModeChange;
        }

        /// <summary>
        /// Subscribes to generic UI controller button events.
        /// Environment-specific events are handled by IEnvironmentHandler.
        /// </summary>
        private void SubscribeToUIEvents()
        {
            // Main menu navigation
            UIController.OnTrainingsButtonPressed += ShowTrainingsWindow;
            UIController.OnModelEditingButtonPressed += ShowModelEditingWindow;
            UIController.OnSettingsButtonPressed += ShowSettingsWindow;
            UIController.OnExitAppButtonPressed += ExitApplication;

            // Return buttons (generic navigation)
            UIController.OnReturnFromTrainingsButtonPressed += ShowMainMenuWindow;
            UIController.OnReturnFromModelEditionButtonPressed += ShowMainMenuWindow;
            UIController.OnSaveSettingsButtonPressed += SaveSettings;
            UIController.OnReturnFromSettingsButtonPressed += ValidateSettingsReturn;

            // Model file operations (generic)
            UIController.OnImportModelButtonPressed += ModelFileBrowserController.ImportModelFile;
            UIController.OnExportModelButtonPressed += ModelFileBrowserController.ExportModelFile;
            UIController.OnDeleteModelButtonPressed += DeleteModel;

            // Model scroll area (generic)
            UIController.OnModelRead += HandleModelReadResult;
            UIController.OnModelFilterNoResults += HandleModelFilterNoResults;
            UIController.OnModelScrollAreaElementSelected += HandleModelElementSelected;

            // Secondary room entry (generic)
            UIController.OnEnterSecondaryRoomButtonPressed += TeleportToSecondaryRoom;
        }

        /// <summary>
        /// Unsubscribes from all events to prevent memory leaks.
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (_sceneFlowController != null)
            {
                _sceneFlowController.OnSceneLoadedAction -= HandleSceneLoaded;
                _sceneFlowController.OnSceneUnloadedAction -= UIController.SwitchToPreviousWindow;
                _sceneFlowController.OnModelEditorSceneUnloadedAction -= HandleModelEditorUnloaded;
                _sceneFlowController.OnKeybindDisplayProfileReady -= HandleKeybindProfileReady;
                _sceneFlowController.OnDesktopModeChangeNotified -= HandleDesktopModeChange;
            }

            if (UIController != null)
            {
                UIController.OnTrainingsButtonPressed -= ShowTrainingsWindow;
                UIController.OnModelEditingButtonPressed -= ShowModelEditingWindow;
                UIController.OnSettingsButtonPressed -= ShowSettingsWindow;
                UIController.OnExitAppButtonPressed -= ExitApplication;
                UIController.OnReturnFromTrainingsButtonPressed -= ShowMainMenuWindow;
                UIController.OnReturnFromModelEditionButtonPressed -= ShowMainMenuWindow;
                UIController.OnSaveSettingsButtonPressed -= SaveSettings;
                UIController.OnReturnFromSettingsButtonPressed -= ValidateSettingsReturn;
                UIController.OnImportModelButtonPressed -= ModelFileBrowserController.ImportModelFile;
                UIController.OnExportModelButtonPressed -= ModelFileBrowserController.ExportModelFile;
                UIController.OnDeleteModelButtonPressed -= DeleteModel;
                UIController.OnModelRead -= HandleModelReadResult;
                UIController.OnModelFilterNoResults -= HandleModelFilterNoResults;
                UIController.OnModelScrollAreaElementSelected -= HandleModelElementSelected;
                UIController.OnEnterSecondaryRoomButtonPressed -= TeleportToSecondaryRoom;
            }

            if (_appSettings != null)
            {
                _appSettings.OnUseGrabKeybindProfilesChanged -= UIController.SetUseGrabKeybindProfiles;
                _appSettings.OnUseGrabKeybindProfilesChanged -= InputController.SetUseGrabKeybindProfiles;
            }

            if (ModelFileBrowserController != null)
            {
                ModelFileBrowserController.SetCurrentModel -= _applicationManager.SetCurrentModel;
            }

            if (EnvironmentHandler != null)
            {
                EnvironmentHandler.UnsubscribeFromUIEvents();
                EnvironmentHandler.UnsubscribeFromTransitionEvents(HandleReturnFromSecondaryRoom);
            }
        }

        #endregion

        #region Scene Flow Event Handlers

        /// <summary>
        /// Handles general scene load events.
        /// </summary>
        private void HandleSceneLoaded()
        {
            UIController.SwitchToRoomOccupiedWindow();
        }

        /// <summary>
        /// Handles model editor scene unload - refreshes model list.
        /// </summary>
        private void HandleModelEditorUnloaded()
        {
            UIController.RefreshModelList();
        }

        /// <summary>
        /// Handles keybind profile changes from loaded scenes.
        /// </summary>
        private void HandleKeybindProfileReady(XRKeybindDisplayProfile profile)
        {
            UIController.SetXRKeybindDisplayProfile(profile);
        }

        /// <summary>
        /// Handles return from secondary room - resets keybind profile.
        /// </summary>
        private void HandleReturnFromSecondaryRoom()
        {
            UIController.SetBaseKeybindDisplayProfile();
        }

        /// <summary>
        /// Handles desktop mode toggle from training scenes.
        /// </summary>
        private void HandleDesktopModeChange(bool isDesktopModeEnabled)
        {
            InputController.ToggleHMDLock(!isDesktopModeEnabled);
        }

        #endregion

        #region UI Event Handlers

        /// <summary>
        /// Handles model read result codes from scroll area.
        /// </summary>
        private bool HandleModelReadResult(int resultCode)
        {
            return HandleMessageCode(resultCode);
        }

        /// <summary>
        /// Handles empty filter results from model scroll area.
        /// </summary>
        private void HandleModelFilterNoResults(int resultCode)
        {
            HandleMessageCode(resultCode);
        }

        /// <summary>
        /// Handles model element selection in the scroll area.
        /// Updates button states based on selection and current window.
        /// </summary>
        private void HandleModelElementSelected(ModelScrollAreaElement element)
        {
            bool isCurrentlySelected = UIController.GetSelectedModelScrollAreaElement() == element;

            // Toggle selection state
            if (isCurrentlySelected)
            {
                element.SetAsNotSelected();
            }
            else
            {
                UIController.ClearModelsScrollAreaSelection();
                element.SetAsSelected();
            }

            // Delegate environment-specific button state updates to handler
            EnvironmentHandler.HandleModelSelectionChanged(_isSecondaryRoomWindowEnabled, isCurrentlySelected);
        }

        #endregion

        #region Window Navigation

        /// <summary>
        /// Switches to main menu window.
        /// </summary>
        private void ShowMainMenuWindow()
        {
            UIController.SwitchToMainMenuWindow();
            _isSecondaryRoomWindowEnabled = false;
        }

        /// <summary>
        /// Switches to trainings window with model selection.
        /// </summary>
        private void ShowTrainingsWindow()
        {
            UIController.ShowTrainingsWindow();
            _isSecondaryRoomWindowEnabled = true;
        }

        /// <summary>
        /// Switches to model editing window with model selection.
        /// </summary>
        private void ShowModelEditingWindow()
        {
            UIController.ShowModelEditingWindow();
            _isSecondaryRoomWindowEnabled = false;
        }

        /// <summary>
        /// Switches to settings window and syncs dropdowns to saved values.
        /// </summary>
        private void ShowSettingsWindow()
        {
            SyncSettingsFromSaved();
            UIController.SwitchToSettingsWindow();
        }

        private void SyncSettingsFromSaved()
        {
            if (_appSettings == null) return;
            var locale = _appSettings.Locale;
            _savedLocaleCode = locale != null ? locale.Identifier.Code : null;
            _savedGrabValue = _appSettings.UseGrabKeybindProfiles;
            UIController.SetSelectedLocaleCode(_savedLocaleCode);
            UIController.SetSelectedGrabValue(_savedGrabValue);
        }

        #endregion

        #region Message Handling

        /// <summary>
        /// Handles message result codes by showing appropriate modals.
        /// </summary>
        /// <param name="resultCode">The message result code.</param>
        /// <param name="onConfirm">Optional confirmation callback.</param>
        /// <returns>True if no error/warning needed to be shown.</returns>
        public bool HandleMessageCode(int resultCode, Action onConfirm = null)
        {
            switch (resultCode)
            {
                case BasicMessages.None:
                    return true;

                case FileErrors.InvalidExtension:
                case FileErrors.FileParsing:
                case BasicMessages.SearchWithoutResults:
                    UIController.ShowBasicModal(resultCode);
                    break;

                default:
                    UIController.ShowBasicModal(resultCode);
                    break;
            }
            return false;
        }

        #endregion

        #region Application Actions

        /// <summary>
        /// Exits the application.
        /// </summary>
        public void ExitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }

        #endregion

        #region Secondary Room Actions

        /// <summary>
        /// Teleports the player to the secondary room.
        /// </summary>
        private void TeleportToSecondaryRoom()
        {
            EnvironmentHandler.TransitionController.TeleportToSecondaryRoom();
        }

        #endregion

        #region Settings Actions

        private void SaveSettings()
        {
            if (_appSettings == null) return;

            string localeCode = UIController.GetSelectedLocaleCode();
            if (!string.IsNullOrEmpty(localeCode))
            {
                var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales
                    .GetLocale(localeCode);
                if (locale != null)
                    _appSettings.Locale = locale;
            }

            _appSettings.UseGrabKeybindProfiles = UIController.GetSelectedGrabValue();

            _savedLocaleCode = UIController.GetSelectedLocaleCode();
            _savedGrabValue = UIController.GetSelectedGrabValue();
        }

        private void ValidateSettingsReturn()
        {
            bool hasChanges = UIController.GetSelectedLocaleCode() != _savedLocaleCode
                || UIController.GetSelectedGrabValue() != _savedGrabValue;

            if (hasChanges)
                UIController.ShowSettingsChangesNotSavedModal(ShowMainMenuWindow);
            else
                ShowMainMenuWindow();
        }

        #endregion

        #region Model Actions

        /// <summary>
        /// Deletes the selected model after confirmation.
        /// </summary>
        public void DeleteModel()
        {
            var currentModel = UIController.GetSelectedModel();
            UIController.ShowConfirmDeleteModal(() =>
                currentModel._Delete(currentModel.GetFullPath(), HandleModelDeleted));
        }

        /// <summary>
        /// Handles successful model deletion.
        /// </summary>
        private void HandleModelDeleted()
        {
            UIController.ShowBasicModal(FileMessages.FileDeleted);
            UIController.DeleteModelScrollAreaSelectedElement();
            UIController.SetModelEditingSelectionButtonsInteractable(false);
        }

        #endregion
    }
}
