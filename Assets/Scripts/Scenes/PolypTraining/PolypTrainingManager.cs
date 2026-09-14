using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomUI;
using MainRoom;
using ModelEditor;
using UnityEngine;

namespace PolypTraining
{
    /// <summary>
    /// Central manager for the Polyp Training scene, coordinating polyp detection and identification training.
    /// 
    /// ARCHITECTURE OVERVIEW:
    /// ----------------------
    /// This manager follows a dependency injection pattern to orchestrate the polyp training experience.
    /// It manages initialization, controller coordination, training lifecycle, polyp detection, and scoring.
    /// 
    /// INITIALIZATION FLOW:
    /// --------------------
    /// 1. Awake() - Find and validate MonoBehaviour controllers via SetControllers()
    /// 2. Awake() - Subscribe to all controller events via SubscribeEvents()
    /// 3. Start() - Launch Initialize() coroutine
    /// 4. Initialize() - Wait for model generation, setup polyp meshes, configure UI
    /// 5. Initialize() - Call ConfigureControllers() to inject dependencies (future extensibility)
    /// 
    /// MANAGED CONTROLLERS:
    /// --------------------
    /// MonoBehaviour Controllers (found in Awake):
    /// - UIController: Manages all UI panels, windows, and HUD elements
    /// - InputController: Handles XR/Desktop input, keybinds, and endoscope control
    /// - PolypTrainingProgressController: Tracks training progress and triggers AI assistance
    /// - PolypIdentificationController: Manages polyp identification workflow and scoring
    /// - PolypDetectionController: Handles polyp detection mechanics and validation
    /// - OpenAIAudioClient: Manages AI voice assistance (STT, TTS, LLM)
    /// 
    /// TRAINING LIFECYCLE:
    /// -------------------
    /// 1. Configuration Phase: User configures time limits, stat visibility, AI assistance
    /// 2. Active Training: Timer runs, polyps detected, identification workflow triggered
    /// 3. Polyp Detection: User detects polyps via raycast interaction
    /// 4. Polyp Identification: Pauses training for detailed polyp classification
    /// 5. Pause/Resume: Training can be paused manually or automatically for identification
    /// 6. Completion: Either time limit reached or manually finished
    /// 7. Results: Final score calculated, all polyp answers reviewed, ranking updated
    /// 
    /// SCORING SYSTEM:
    /// ---------------
    /// Final score = (base score * completion ratio * 10 - time penalty), clamped [0-10]
    /// - Base Score: Average correctness of polyp identifications (0-1 scale)
    /// - Completion Ratio: Percentage of polyps that were identified
    /// - Time Penalty: Applied if time exceeds optimal range (10-20 minutes default)
    /// - Each polyp identification scored on: location, Paris classification, JNET classification, size
    /// 
    /// DEPENDENCIES:
    /// -------------
    /// External Services:
    /// - ApplicationManager: Singleton providing app-wide configuration
    /// - FileManager: Static class for loading/saving ranking data
    /// - TrainingsRankingLogic: Static class for ranking insertion logic
    /// 
    /// Scene Components (SerializeField):
    /// - modelGeneratorObject: GameObject containing ISplineModelGenerator component
    /// - PolypDetectionController: Manages polyp detection mechanics
    /// - XRTeleportDestinationProvider: Manages VR teleportation points
    /// - EndoscopePointLightDisplacer: Dynamic lighting for endoscope
    /// 
    /// EXTENSION POINTS:
    /// -----------------
    /// To add new training features:
    /// 1. Create a new controller component as a child of this GameObject
    /// 2. Add field and initialization in SetControllers()
    /// 3. Subscribe to events in SubscribeEvents()
    /// 4. Inject dependencies in ConfigureControllers() if needed
    /// 5. Add training configuration UI and state tracking as needed
    /// 
    /// TODO: Consider base class inheritance shared with CoverageTrainingManager for common functionality
    /// </summary>
    [DefaultExecutionOrder(-200)] // Must execute before dependent controllers
    public class PolypTrainingManager : MonoBehaviour, ISecondaryRoomManager, IXRDesktopModeProvider
    {
        [Header("Resources")]
        [SerializeField] private GameObject modelGeneratorObject;
        [SerializeField] private XRTeleportDestinationProvider _teleportDestinationProvider;
        [SerializeField] private PolypDetectionController _polypDetectionController;
        [SerializeField] private EndoscopePointLightDisplacer[] _endoscopePointLightDisplacers;
        [Header("Training configuration")]
        [SerializeField] private int _returnFromIdentificationCountdown = 5;

        [Header("Optimal time range for score calculation (in seconds)")]
        [SerializeField] private float _optimalTimeMin = 600f; // 10 minutes
        [SerializeField] private float _optimalTimeMax = 1200f; // 20 minutes
        [SerializeField] private float _timePenaltyFactor = 0.05f; // Penalty factor for time deviation

        [HideInInspector] public static PolypTrainingManager Instance { get; private set; }
        [HideInInspector] public UIController UIController { get; private set; }
        [HideInInspector] public InputController InputController { get; private set; }

        private ApplicationManager _applicationManager;
        private IAppSettingsProvider _appSettings;
        private ISplineModelGenerator _modelGenerator;
        private PolypTrainingProgressController _polypTrainingProgressController;
        private PolypIdentificationController _polypIdentificationController;
        private ModelRotationHandleController _modelRotationHandleController;
        private OpenAIAudioClient _openAIAudioClient;
        private List<PolypTrainingResult> _rankingTrainingResults = new List<PolypTrainingResult>();
        private bool _isPolypIdentificationOngoing => _polypIdentificationController._isPolypIdentificationOngoing;
        private bool _isTrainingEnabled = false;
        private bool _isTrainingPaused = false;
        public bool _useTimeLimit = false;
        public bool _showStatsDuringTraining = false;
        public bool _showPolypCountView = false;
        public bool _showAnswersDuringTraining = false;
        public bool _aiAssistanceEnabled = false;
        private bool _isDesktopModeEnabled = false;
        private bool _isNBIEnabled = false;

        private float _elapsedTime = 0f;
        private float _timerLimitInSeconds = 1500f; // Default to 25 minutes
        private int _currentIdentificationAnswerShowingIndex = 0;
        private Model _currentModel => _applicationManager.GetCurrentModel();
        public event Action OnExitRoomRequested;
        public event Action<bool> OnDesktopModeChange;

        /// <summary>
        /// Unity Awake lifecycle method. Initializes singleton, validates dependencies, and sets up controllers.
        /// </summary>
        /// <remarks>
        /// Execution order:
        /// 1. Singleton pattern validation
        /// 2. Find ApplicationManager singleton
        /// 3. SetControllers() - Find and validate all MonoBehaviour controllers
        /// 4. SubscribeEvents() - Wire up all event handlers
        /// </remarks>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null)
                throw new Exception("ApplicationManager not found.");

            SetControllers();
            SubscribeEvents();
        }

        /// <summary>
        /// Unity OnDestroy lifecycle method. Unsubscribes from all events to prevent memory leaks.
        /// </summary>
        /// <remarks>
        /// Critical for cleanup when:
        /// - Manager is destroyed while controllers remain alive
        /// - Scene is unloaded or reloaded
        /// - Multiple manager instances are created (singleton duplicate case)
        /// 
        /// Mirrors the subscription pattern in SubscribeEvents().
        /// </remarks>
        private void OnDestroy()
        {
            if (_appSettings != null)
            {
                _appSettings.OnUseGrabKeybindProfilesChanged -= UIController.SetUseGrabKeybindProfiles;
                _appSettings.OnUseGrabKeybindProfilesChanged -= InputController.SetUseGrabKeybindProfiles;
            }
            UnsubscribeEvents();
        }

        /// <summary>
        /// Unity Update lifecycle method. Updates training timer when training is active.
        /// </summary>
        /// <remarks>
        /// Only executes when training is enabled. Delegates to CheckTrainingTimer() for:
        /// - Elapsed time tracking
        /// - Time limit enforcement
        /// - UI updates
        /// 
        /// Does not update during polyp identification pause state.
        /// </remarks>
        private void Update()
        {
            if (_isTrainingEnabled)
            {
                CheckTrainingTimer();
            }
        }



        /// <summary>
        /// Finds and validates all required MonoBehaviour controllers in the scene hierarchy.
        /// </summary>
        /// <remarks>
        /// Controllers found via GetComponentInChildren:
        /// - UIController: UI management
        /// - InputController: Input handling
        /// - PolypTrainingProgressController: Progress tracking
        /// - PolypIdentificationController: Polyp identification workflow
        /// - OpenAIAudioClient: AI assistance
        /// 
        /// Interface components:
        /// - ISplineModelGenerator: Obtained from modelGeneratorObject GameObject
        /// 
        /// SerializeField validation:
        /// - PolypDetectionController: Polyp detection mechanics
        /// - EndoscopePointLightDisplacer: Lighting
        /// - modelGeneratorObject: Model generator GameObject
        /// </remarks>
        /// <exception cref="Exception">Thrown if any required controller is not found</exception>
        private void SetControllers()
        {
            if (modelGeneratorObject == null)
                throw new Exception("Model Generator GameObject not assigned.");

            _modelGenerator = modelGeneratorObject.GetComponent<ISplineModelGenerator>();
            if (_modelGenerator == null)
                throw new Exception("Model Generator must implement ISplineModelGenerator interface.");

            UIController = GetComponentInChildren<UIController>();
            if (UIController == null)
                throw new Exception("UIController not found in children.");

            InputController = GetComponentInChildren<InputController>();
            if (InputController == null)
                throw new Exception("InputController not found in children.");

            _polypTrainingProgressController = GetComponentInChildren<PolypTrainingProgressController>();
            if (_polypTrainingProgressController == null)
                throw new Exception("PolypTrainingProgressController not found in children.");

            if (_polypDetectionController == null)
                throw new Exception("PolypDetectionController not found in children.");

            _polypIdentificationController = GetComponentInChildren<PolypIdentificationController>();
            if (_polypIdentificationController == null)
                throw new Exception("PolypIdentificationController not found in children.");

            _openAIAudioClient = GetComponentInChildren<OpenAIAudioClient>();
            if (_openAIAudioClient == null)
                throw new Exception("OpenAIAudioClient not found in children.");

            _modelRotationHandleController = GetComponentInChildren<ModelRotationHandleController>();
            if (_modelRotationHandleController == null)
                throw new Exception("ModelRotationHandleController not assigned.");
        }

        /// <summary>
        /// Subscribes to all controller events to coordinate training flow and user interactions.
        /// </summary>
        /// <remarks>
        /// Event subscriptions organized by controller:
        /// 
        /// UIController Events:
        /// - Configuration switches (time limit, stats visibility, AI assistance)
        /// - Training lifecycle buttons (start, pause, resume, restart, finish)
        /// - Polyp identification workflow (identify, continue, navigation)
        /// - Results management (save, ranking display)
        /// - Room navigation (exit confirmation)
        /// - Localization changes
        /// 
        /// InputController Events:
        /// - XR input actions (pause, polyp detection, locomotion)
        /// - AI voice input (recording start/stop, audio clips)
        /// - Desktop mode toggle
        /// 
        /// OpenAIAudioClient Events:
        /// - LLM responses (text and streaming)
        /// - Speech-to-text transcriptions
        /// - TTS enable state changes
        /// 
        /// PolypDetectionController Events:
        /// - Invalid detection attempts
        /// 
        /// PolypTrainingProgressController Events:
        /// - LLM request triggers for contextual assistance
        /// </remarks>
        private void SubscribeEvents()
        {
            UIController.OnTimeLimitSwitchPressedAction += SetTimeLimitState;
            UIController.OnShowStatsDuringTrainingSwitchPressedAction += SetStatsDuringTrainingState;
            UIController.OnShowPolypCountSwitchPressedAction += SetShowPolypCountView;
            UIController.OnShowAnswersDuringTrainingSwitchPressedAction += SetShowAnswersDuringTraining;
            UIController.OnEnableAIAssistanceSwitchPressedAction += SetAIAssistanceEnable;
            UIController.OnStartTrainingButtonPressedAction += ValidateStartTraining;
            UIController.OnPauseTrainingButtonPressedAction += StandardPauseTraining;
            UIController.OnResumeTrainingButtonPressedAction += ResumeTraining;
            UIController.OnRestartTrainingButtonPressedAction += RestartTraining;
            UIController.OnEndTrainingButtonPressedAction += FinishTraining;
            UIController.OnExitRoomButtonPressedAction += ConfirmExitTrainingRoom;
            UIController.OnConfirmExitRoomButtonPressedAction += ExitTrainingRoom;
            UIController.OnSaveTrainingResultsButtonPressedAction += ConfirmSaveTrainingResults;
            UIController.OnConfirmSaveTrainingResultsButtonPressedAction += SaveTrainingResults;
            UIController.OnShowRankingButtonPressedAction += ShowRanking;
            UIController.OnReturnFromRankingButtonPressedAction += HandleReturnFromRanking;
            UIController.OnIdentifyPolypButtonPressedAction += IdentifyPolyp;
            UIController.OnContinueIdentificationButtonPressedAction += ContinueIdentification;
            UIController.OnReturnFromIdentificationCountdownEnd += ResumeTraining;
            UIController.OnNextPolypIdentificationAnswerButtonPressedAction += SetNextPolypIdentificationAnswerToShow;
            UIController.OnPreviousPolypIdentificationAnswerButtonPressedAction += SetPreviousPolypIdentificationAnswerToShow;
            UIController.LocaleChangedAction += LocaleChanged;
            
            InputController.OnToggleNBIAction += HandleNBIToggle;
            InputController.OnPauseControllerAction += StandardPauseTraining;
            InputController.OnPolypDetectionAction += DetectPolyp;
            InputController.OnAudioRecordedAction += HandleAudioRecorded;
            InputController.OnStartRecording += HandleStartRecording;
            InputController.OnStopRecording += HandleStopRecording;
            InputController.ToggleAITTS += ToggleAIVoiceAssistance;
            InputController.DestkopModeChange += ManageDesktopMode;

            _openAIAudioClient.OnLLMResponseReceived += HandleLLMResponse;
            _openAIAudioClient.OnSTTResponseReceived += HandleSTTResponse;
            _openAIAudioClient.TTSEnableChanged += HandleAIAssistanceEnableChange;

            _polypDetectionController.OnPolypInvalidDetection += HandleInvalidPolypDetection;

            _polypTrainingProgressController.SendLLMRequest += SendLLMRequest;

            // TODO: Refactor to remove direct MainRoomManager.Instance access
            // Consider: dependency injection or event-based communication
            InputController.OnDpadPressed += HandleDpadPressed;
            InputController.OnDpadReleased += HandleDpadReleased;
        }

        /// <summary>
        /// Unsubscribes from all controller events. Called in OnDestroy() to prevent memory leaks.
        /// </summary>
        /// <remarks>
        /// Mirrors the subscription pattern in SubscribeEvents().
        /// Null-conditional operators handle cases where controllers were already destroyed.
        /// </remarks>
        private void UnsubscribeEvents()
        {
            if (UIController != null)
            {
                UIController.OnTimeLimitSwitchPressedAction -= SetTimeLimitState;
                UIController.OnShowStatsDuringTrainingSwitchPressedAction -= SetStatsDuringTrainingState;
                UIController.OnShowPolypCountSwitchPressedAction -= SetShowPolypCountView;
                UIController.OnShowAnswersDuringTrainingSwitchPressedAction -= SetShowAnswersDuringTraining;
                UIController.OnEnableAIAssistanceSwitchPressedAction -= SetAIAssistanceEnable;
                UIController.OnStartTrainingButtonPressedAction -= ValidateStartTraining;
                UIController.OnPauseTrainingButtonPressedAction -= StandardPauseTraining;
                UIController.OnResumeTrainingButtonPressedAction -= ResumeTraining;
                UIController.OnRestartTrainingButtonPressedAction -= RestartTraining;
                UIController.OnEndTrainingButtonPressedAction -= FinishTraining;
                UIController.OnExitRoomButtonPressedAction -= ConfirmExitTrainingRoom;
                UIController.OnConfirmExitRoomButtonPressedAction -= ExitTrainingRoom;
                UIController.OnSaveTrainingResultsButtonPressedAction -= ConfirmSaveTrainingResults;
                UIController.OnConfirmSaveTrainingResultsButtonPressedAction -= SaveTrainingResults;
                UIController.OnShowRankingButtonPressedAction -= ShowRanking;
                UIController.OnReturnFromRankingButtonPressedAction -= HandleReturnFromRanking;
                UIController.OnIdentifyPolypButtonPressedAction -= IdentifyPolyp;
                UIController.OnContinueIdentificationButtonPressedAction -= ContinueIdentification;
                UIController.OnReturnFromIdentificationCountdownEnd -= ResumeTraining;
                UIController.OnNextPolypIdentificationAnswerButtonPressedAction -= SetNextPolypIdentificationAnswerToShow;
                UIController.OnPreviousPolypIdentificationAnswerButtonPressedAction -= SetPreviousPolypIdentificationAnswerToShow;
                UIController.LocaleChangedAction -= LocaleChanged;
            }

            if (InputController != null)
            {
                InputController.OnPauseControllerAction -= StandardPauseTraining;
                InputController.OnPolypDetectionAction -= DetectPolyp;
                InputController.OnAudioRecordedAction -= HandleAudioRecorded;
                InputController.OnStartRecording -= HandleStartRecording;
                InputController.OnStopRecording -= HandleStopRecording;
                InputController.ToggleAITTS -= ToggleAIVoiceAssistance;
                InputController.DestkopModeChange -= ManageDesktopMode;
                InputController.OnDpadPressed -= HandleDpadPressed;
                InputController.OnDpadReleased -= HandleDpadReleased;
            }

            if (_openAIAudioClient != null)
            {
                _openAIAudioClient.OnLLMResponseReceived -= HandleLLMResponse;
                _openAIAudioClient.OnSTTResponseReceived -= HandleSTTResponse;
                _openAIAudioClient.TTSEnableChanged -= HandleAIAssistanceEnableChange;
            }

            if (_polypDetectionController != null)
            {
                _polypDetectionController.OnPolypInvalidDetection -= HandleInvalidPolypDetection;
            }

            if (_polypTrainingProgressController != null)
            {
                _polypTrainingProgressController.SendLLMRequest -= SendLLMRequest;
            }

            if (_modelRotationHandleController != null)
            {
                _modelRotationHandleController.OnRotationStarted -= OnRotationHandleGrabStart;
                _modelRotationHandleController.OnRotationReleased -= OnRotationHandleGrabEnd;
            }
        }

        /// <summary>
        /// Unity Start lifecycle method. Launches the initialization coroutine.
        /// </summary>
        /// <remarks>
        /// Delegation pattern: Start() → Initialize() coroutine
        /// This allows asynchronous initialization with proper dependency waiting.
        /// </remarks>
        private void Start()
        {
            ApplyAppSettings();
            StartCoroutine(Initialize());
        }

        /// <summary>
        /// Applies application-wide settings from the ApplicationManager's IAppSettingsProvider to relevant controllers.
        /// Currently applies the "Use Grab Keybind Profiles" setting to both UIController and InputController.
        /// This method is called during Start() after controllers are initialized, ensuring that settings are applied before user interaction.
        /// </summary>
        private void ApplyAppSettings()
        {
            _appSettings = _applicationManager.AppSettings;
            if (_appSettings != null)
            {
                UIController.SetUseGrabKeybindProfiles(_appSettings.UseGrabKeybindProfiles);
                _appSettings.OnUseGrabKeybindProfilesChanged += UIController.SetUseGrabKeybindProfiles;
                InputController.SetUseGrabKeybindProfiles(_appSettings.UseGrabKeybindProfiles);
                _appSettings.OnUseGrabKeybindProfilesChanged += InputController.SetUseGrabKeybindProfiles;
            }
            else
            {
                Debug.LogError("[PolypTrainingManager] IAppSettingsProvider not available from ApplicationManager.");
            }
        }

        /// <summary>
        /// Handles the toggle of NBI (Narrow Band Imaging) mode from input. Updates the welded model's material to reflect the NBI state.
        /// </summary>
        private void HandleNBIToggle()
        {
            SetWeldedModelNBI(_isNBIEnabled ? 0f : 1f);
        }

        /// <summary>
        /// Sets the NBI (Narrow Band Imaging) state on the welded model's material by updating a shader property.
        /// This method assumes the material used by the welded model has a float property named "_NBI" that controls the NBI effect.
        /// The value is typically 0 for normal view and 1 for NBI view, but this depends on the shader implementation.
        /// The method retrieves the MeshRenderer from the ISplineModelGenerator, gets the current MaterialPropertyBlock, sets the "_NBI" property, and applies the block back to the renderer.
        /// This allows dynamic toggling of the NBI effect without needing to switch materials or reload the mesh, enabling smooth transitions between views during training.
        /// </summary>
        /// <param name="value"></param>
        private void SetWeldedModelNBI(float value)
        {
            var renderer = _modelGenerator?.WeldedModelRenderer;
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat("_NBI", value);
            renderer.SetPropertyBlock(block);
            _isNBIEnabled = value > 0 ? true : false;
        }

        /// <summary>
        /// Asynchronous initialization coroutine that waits for model generation and configures the training environment.
        /// </summary>
        /// <remarks>
        /// Initialization sequence:
        /// 1. Wait for ISplineModelGenerator.IsSegmentedModelGenerated
        /// 2. Load current model data from ApplicationManager
        /// 3. Initialize spline from model data
        /// 4. Load model into skinned mesh renderer
        /// 5. Apply HDRP material to mesh
        /// 6. Setup polyp meshes on the model
        /// 7. Disable endoscope input until training starts
        /// 8. Populate polyp identification dropdowns with classification options
        /// 9. Initialize UI with stats visible
        /// 10. Configure VR teleportation destinations
        /// 11. Attach dynamic lighting to endoscope
        /// 12. Call ConfigureControllers() for dependency injection
        /// </remarks>
        /// <returns>Coroutine enumerator</returns>
        private IEnumerator Initialize()
        {
            if (_currentModel == null)
            {
                _applicationManager.SetCurrentModel(new Model());
            }
            else
            {
                _modelGenerator.InitializeSplineFromModelData(_currentModel);
            }
            _modelGenerator.LoadGenerationConfiguration(_currentModel);
            ConfigureControllers();
            _modelGenerator.InitializeWeldedModelGO();
            _currentModel.LoadIntoSMR(_modelGenerator.WeldedModelRenderer, _modelGenerator.WeldedModelCollider, null);
            //_modelGenerator.WeldedModelRenderer.sharedMaterials = new Material[] { Resources.Load<Material>(Path.Combine(FileManager.materialResourcesPath, "LI_HDRP_Material")) };
            _polypDetectionController.SetUpPolypMeshes(_currentModel.Diseases, _modelGenerator.WeldedModelRenderer);
            InputController.InitializeSplineNavigator(_modelGenerator.Spline);
            InputController.SetEndoscopeMovementActionsEnable(false);
            InputController.SetInTrainingActionsEnable(false);
            PopulatePolypIdentificationDropdowns();
            //SetStatsDuringTrainingState(true);
            SetRoomTeleportDestination();
            for (int i = 0; i < _endoscopePointLightDisplacers.Length; i++)
            {
                _endoscopePointLightDisplacers[i].targetMesh = _modelGenerator.WeldedModelRenderer.gameObject;
            }
            yield return new WaitUntil(() => true);

            SetWeldedModelNBI(0f);
        }

        /// <summary>
        /// Configures all controllers with their dependencies after initialization completes.
        /// </summary>
        /// <remarks>
        /// This method provides a centralized location for dependency injection and controller configuration.
        /// Called after model generation and polyp setup are complete.
        /// 
        /// Current architecture:
        /// - All controllers are MonoBehaviour components found in SetControllers()
        /// - Controllers are already wired via event subscriptions in SubscribeEvents()
        /// - No additional DI needed at present, but method exists for future extensibility
        /// 
        /// Future extension pattern:
        /// If pure C# business logic controllers are added (similar to ModelEditorManager's edition mode controllers):
        /// 1. Create controller instance with constructor DI:
        ///    _someController = new SomeController(UIController, InputController, dependencies...);
        /// 2. Call Initialize() on the controller if needed
        /// 3. Store as private field for lifecycle management
        /// 
        /// Example:
        /// <code>
        /// // Future: Training scoring calculator with pure business logic
        /// _scoringController = new PolypScoringController(
        ///     _polypDetectionController,
        ///     _polypIdentificationController);
        /// </code>
        /// </remarks>
        private void ConfigureControllers()
        {
            _modelRotationHandleController.Initialize();
            _modelRotationHandleController.OnRotationStarted += OnRotationHandleGrabStart;
            _modelRotationHandleController.OnRotationReleased += OnRotationHandleGrabEnd;
        }

        private void OnRotationHandleGrabStart() => InputController.SetEndoscopeGrabSuppressed(true);
        private void OnRotationHandleGrabEnd() => InputController.SetEndoscopeGrabSuppressed(false);

        /// <summary>
        /// Handler for return from ranking window.
        /// </summary>
        private void HandleReturnFromRanking()
        {
            UIController.ShowTrainingDataWindow();
        }

        /// <summary>
        /// Handler for D-pad pressed event. Disables character locomotion in main room.
        /// </summary>
        /// <remarks>
        /// TODO: This creates tight coupling with MainRoomManager singleton.
        /// Consider refactoring to use dependency injection or event-based communication.
        /// </remarks>
        private void HandleDpadPressed()
        {
            if (MainRoomManager.Instance != null)
            {
                MainRoomManager.Instance.InputController?.SetCharacterLocomotionEnabled(false);
            }
        }

        /// <summary>
        /// Handler for D-pad released event. Re-enables character locomotion in main room.
        /// </summary>
        /// <remarks>
        /// TODO: This creates tight coupling with MainRoomManager singleton.
        /// Consider refactoring to use dependency injection or event-based communication.
        /// </remarks>
        private void HandleDpadReleased()
        {
            if (MainRoomManager.Instance != null)
            {
                MainRoomManager.Instance.InputController?.SetCharacterLocomotionEnabled(true);
            }
        }

        /// <summary>
        /// Toggles between VR and Desktop viewing modes.
        /// </summary>
        /// <remarks>
        /// Notifies subscribers via OnDesktopModeChange event.
        /// Updates UI controller to show/hide desktop-specific controls.
        /// Implements IXRDesktopModeProvider interface.
        /// </remarks>
        private void ManageDesktopMode()
        {
            _isDesktopModeEnabled = !_isDesktopModeEnabled;
            OnDesktopModeChange?.Invoke(_isDesktopModeEnabled);
            UIController.ToggleDesktopModeView(_isDesktopModeEnabled);
        }

        /// <summary>
        /// Handles user confirmation to exit the training room.
        /// </summary>
        /// <remarks>
        /// Action sequence:
        /// 1. If training is active, finish the training session
        /// 2. Navigate to main room scene via ApplicationManager
        /// </remarks>
        private void ConfirmExitTrainingRoom()
        {
            UIController.ShowConfirmExitRoomModal();
        }

        /// <summary>
        /// Immediately exits the training room and returns to main room.
        /// </summary>
        /// <remarks>
        /// Called when user confirms exit or when emergency exit is triggered.
        /// Does not save training results - use ConfirmExitTrainingRoom for normal exit flow.
        /// </remarks>
        private void ExitTrainingRoom()
        {
            OnExitRoomRequested?.Invoke();
        }

        /// <summary>
        /// Prompts user to confirm saving training results to the ranking leaderboard.
        /// </summary>
        /// <remarks>
        /// Displays modal with text input for player name.
        /// Results include score, time, polyps detected/identified.
        /// </remarks>
        private void ConfirmSaveTrainingResults()
        {
            UIController.ShowSaveTrainingResultsModal();
        }

        /// <summary>
        /// Loads training ranking data from persistent storage and populates UI.
        /// </summary>
        /// <remarks>
        /// Reads ranked list of PolypTrainingResult from FileManager.
        /// Updates ranking window with top performers.
        /// Called when user navigates to ranking view.
        /// </remarks>
        private void LoadRankingData()
        {
            _rankingTrainingResults = FileManager.LoadTrainingRankingData<PolypTrainingResult>();
            UIController.PopulateTrainingRanking(_rankingTrainingResults.Cast<TrainingResult>().ToList());
        }

        /// <summary>
        /// Saves current training results to the ranking leaderboard with player name.
        /// </summary>
        /// <param name="name">Player name to associate with the training result</param>
        /// <remarks>
        /// Save logic:
        /// 1. Create PolypTrainingResult with score, time, polyps detected/identified
        /// 2. Attempt insertion into bounded ranking list (capacity: 10)
        /// 3. If accepted (score high enough), persist to file and update UI
        /// 4. If rejected (score too low), show not-saved message
        /// 5. Disable save button to prevent duplicate submissions
        /// 
        /// Uses PolypTrainingRankingPolicy to determine ranking order.
        /// </remarks>
        private void SaveTrainingResults(string name)
        {
            var trainingResult = new PolypTrainingResult
            {
                PlayerName = name,
                Score = CalculateFinalScore(),
                TimeInSeconds = _elapsedTime,
                PolypsIdentified = _polypIdentificationController.IdentifiedPolypCount,
                TotalPolypsCount = _polypDetectionController.TotalPolypCount,
            };


            bool accepted = TrainingsRankingLogic.TryInsertBounded(
                _rankingTrainingResults,
                trainingResult,
                new PolypTrainingRankingPolicy(),
                capacity: 10);

            if (accepted)
            {
                FileManager.SaveTrainingRankingData(_rankingTrainingResults);
                UIController.PopulateTrainingRanking(_rankingTrainingResults.Cast<TrainingResult>().ToList());
                UIController.ShowTrainingResultsSavedMessage();
            }
            else
            {
                UIController.ShowTrainingResultsNotSavedMessage();
            }
            UIController.SetSaveTrainingResultsButtonActive(false);
        }

        /// <summary>
        /// Displays the training ranking leaderboard window.
        /// </summary>
        /// <remarks>
        /// Shows top 10 training results ordered by PolypTrainingRankingPolicy.
        /// Results loaded via LoadRankingData() before display.
        /// </remarks>
        private void ShowRanking()
        {
            UIController.ShowRankingWindow();
        }

        /// <summary>
        /// Configures VR teleportation system to prevent player from teleporting outside training area.
        /// </summary>
        /// <remarks>
        /// Sets teleport area constraint to _teleportDestination transform.
        /// Only applies when VR input controller is active.
        /// </remarks>
        private void SetRoomTeleportDestination()
        {
            _teleportDestinationProvider?.NotifyReady();
        }

        /// <summary>
        /// Enables or disables AI voice assistance functionality during training.
        /// </summary>
        /// <param name="isEnabled">True to enable AI assistance, false to disable</param>
        /// <remarks>
        /// When enabled:
        /// - Trainee can ask questions via voice input
        /// - OpenAI STT/LLM/TTS pipeline provides spoken responses
        /// - AI can provide hints about polyp characteristics
        /// - Cannot directly reveal correct answers
        /// Requires OpenAIAudioClient to be configured.
        /// </remarks>
        private void SetAIAssistanceEnable(bool isEnabled)
        {
            _aiAssistanceEnabled = isEnabled;
        }

        /// <summary>
        /// Toggles text-to-speech (TTS) output for AI responses.
        /// </summary>
        /// <remarks>
        /// When TTS enabled: AI responses spoken aloud via OpenAI TTS
        /// When TTS disabled: AI responses shown as text only
        /// Allows trainee to prefer text/audio based on environment.
        /// </remarks>
        private void ToggleAIVoiceAssistance()
        {
            _openAIAudioClient.ToggleTTS();
        }

        /// <summary>
        /// Handles TTS enable/disable state changes from OpenAIAudioClient.
        /// </summary>
        /// <param name="state">True if TTS is now enabled, false if disabled</param>
        /// <remarks>
        /// Updates UI to show current TTS state to user.
        /// Called when OpenAIAudioClient.TTSEnableChanged event fires.
        /// </remarks>
        private void HandleAIAssistanceEnableChange(bool state)
        {
            UIController.ShowAIToggleMessage(state);
        }

        /// <summary>
        /// Handles audio recording completion and sends to speech-to-text service.
        /// </summary>
        /// <param name="clip">Recorded audio clip from trainee's voice input</param>
        /// <remarks>
        /// Pipeline: Voice → AudioClip → OpenAI Whisper STT → HandleSTTResponse
        /// Called when InputController.OnAudioRecordedAction fires.
        /// </remarks>
        private void HandleAudioRecorded(AudioClip clip)
        {
            _openAIAudioClient.SendSTTRequest(clip);
        }

        /// <summary>
        /// Handles LLM response from OpenAI GPT model.
        /// </summary>
        /// <param name="response">Generated text response from LLM</param>
        /// <param name="isFinal">True if response is complete, false if streaming chunk</param>
        /// <remarks>
        /// Response handling:
        /// 1. Display response in AI chat UI (streaming or complete)
        /// 2. If TTS disabled, show response as text message
        /// 3. If TTS enabled, OpenAIAudioClient handles spoken output
        /// Supports both streaming and complete responses for better UX.
        /// </remarks>
        private void HandleLLMResponse(string response, bool isFinal)
        {
            UIController.CreateAIChatMessage(response, isFinal);
            if (!_openAIAudioClient.IsTTSEnabled()) UIController.ShowLLMResponseMessage(response);
        }

        /// <summary>
        /// Handles speech-to-text response from OpenAI Whisper.
        /// </summary>
        /// <param name="response">Transcribed text from trainee's voice input</param>
        /// <remarks>
        /// STT response handling:
        /// 1. Display transcription in AI chat UI
        /// 2. If TTS disabled, show transcription as text message
        /// 3. Forward to PolypTrainingProgressController for contextual LLM prompt generation
        /// STT response triggers LLM request with training context.
        /// </remarks>
        private void HandleSTTResponse(string response)
        {
            UIController.CreateAIChatMessage(response, true);
            if (!_openAIAudioClient.IsTTSEnabled()) UIController.ShowLLMResponseMessage(response);
            _polypTrainingProgressController.OnSTTResponseReceived(response);
        }

        /// <summary>
        /// Handles invalid polyp detection attempts (false positives).
        /// </summary>
        /// <remarks>
        /// Forwards event to PolypTrainingProgressController for tracking.
        /// Can trigger AI guidance if assistance enabled.
        /// Called when PolypDetectionController.OnPolypInvalidDetection fires.
        /// </remarks>
        private void HandleInvalidPolypDetection()
        {
            _polypTrainingProgressController.OnInvalidPolypDetection();
        }

        /// <summary>
        /// Handles start of audio recording for AI voice input.
        /// </summary>
        /// <remarks>
        /// Displays recording indicator in UI to provide feedback.
        /// Called when InputController.OnStartRecording fires.
        /// </remarks>
        private void HandleStartRecording()
        {
            UIController.ShowAIStartRecordingMessage();
        }

        /// <summary>
        /// Handles stop/completion of audio recording for AI voice input.
        /// </summary>
        /// <remarks>
        /// Hides recording indicator and shows processing state in UI.
        /// Recording will be sent to STT via HandleAudioRecorded.
        /// Called when InputController.OnStopRecording fires.
        /// </remarks>
        private void HandleStopRecording()
        {
            UIController.ShowAIStopRecordingMessage();
        }

        /// <summary>
        /// Handles locale/language changes and updates UI text.
        /// </summary>
        /// <remarks>
        /// Repopulates polyp identification dropdowns with localized classification terms.
        /// Called when LocalizationSettings.SelectedLocaleChanged event fires.
        /// </remarks>
        private void LocaleChanged()
        {
            PopulatePolypIdentificationDropdowns();
        }

        /// <summary>
        /// Populates polyp identification dropdown menus with classification options.
        /// </summary>
        /// <remarks>
        /// Populates three dropdown categories:
        /// 1. Location (anatomical location in colon)
        /// 2. Paris Classification (morphology)
        /// 3. JNET Classification (neoplastic prediction)
        /// Options retrieved from PolypIdentificationController and localized.
        /// Called during initialization and after locale changes.
        /// </remarks>
        private void PopulatePolypIdentificationDropdowns()
        {
            var locationOptions = _polypIdentificationController.GetDiseaseLocationDropdownOptions();
            var parisClassOptions = _polypIdentificationController.GetParisClassDropdownOptions();
            var jnetClassOptions = _polypIdentificationController.GetJnetClassDropdownOptions();
            UIController.PopulatePolypIdentificationDropdowns(locationOptions, parisClassOptions, jnetClassOptions);
        }

        /// <summary>
        /// Sends LLM request to OpenAI with training context and user message.
        /// </summary>
        /// <param name="message">User message/question for LLM</param>
        /// <param name="systemPrompt">System prompt providing training context and constraints</param>
        /// <remarks>
        /// Only sends request if AI assistance is enabled.
        /// System prompt typically includes:
        /// - Training state (time elapsed, polyps detected, etc.)
        /// - Guidance constraints (no direct answers)
        /// - Allowed hint types
        /// Called by PolypTrainingProgressController.SendLLMRequest event.
        /// </remarks>
        private void SendLLMRequest(string message, string systemPrompt)
        {
            if (_aiAssistanceEnabled) _openAIAudioClient?.SendLLMRequest(message, systemPrompt);
        }

        /// <summary>
        /// Enables or disables the timer limit for the training session.
        /// </summary>
        /// <param name="state">True to enable time limit, false for unlimited training time</param>
        /// <remarks>
        /// When enabled, training will automatically finish when time limit is reached.
        /// Timer state is updated via SetTrainingState() and displayed in UI.
        /// </remarks>
        private void SetTimeLimitState(bool state)
        {
            _useTimeLimit = state;
            UIController.SetTimeLimitFieldTextActive(_useTimeLimit);
        }

        /// <summary>
        /// Enables or disables the real-time statistics panel during training.
        /// </summary>
        /// <param name="state">True to show stats panel, false to hide</param>
        /// <remarks>
        /// Stats panel displays:
        /// - Current training time / time limit
        /// - Polyps detected  
        /// - Polyps correctly identified
        /// - Current score
        /// Also controls visibility of polyp count toggle switch.
        /// </remarks>
        private void SetStatsDuringTrainingState(bool state)
        {
            _showStatsDuringTraining = state;
            UIController.SetShowPolypCountSwitchActive(state);
        }

        /// <summary>
        /// Enables or disables the polyp count display showing total polyps in the model.
        /// </summary>
        /// <param name="state">True to show polyp count, false to hide</param>
        /// <remarks>
        /// When enabled, displays total number of polyps the trainee should find.
        /// Helps trainee track completion progress during training.
        /// </remarks>
        private void SetShowPolypCountView(bool state)
        {
            _showPolypCountView = state;
        }

        /// <summary>
        /// Enables or disables showing correct answers during polyp identification.
        /// </summary>
        /// <param name="state">True to show answers immediately, false to hide until training ends</param>
        /// <remarks>
        /// When enabled:
        /// - Correct classification/morphology/size shown after each identification
        /// - Helps trainee learn correct characteristics
        /// When disabled:
        /// - Answers only revealed in final results screen
        /// - More challenging training mode
        /// </remarks>
        private void SetShowAnswersDuringTraining(bool state)
        {
            _showAnswersDuringTraining = state;
        }

        /// <summary>
        /// Continuously updates training timer and checks for time limit expiration.
        /// </summary>
        /// <remarks>
        /// Called every frame via Update() when training is active.
        /// Behavior:
        /// - Does not run if training disabled, paused, or identification ongoing
        /// - Increments elapsed time by Time.deltaTime
        /// - If time limit enabled and exceeded, automatically finish training
        /// - Updates UI with current training data each frame
        /// </remarks>
        private void CheckTrainingTimer()
        {
            if (!_isTrainingEnabled || _isTrainingPaused || _isPolypIdentificationOngoing) return;

            _elapsedTime += Time.deltaTime;

            if (_useTimeLimit && _elapsedTime > _timerLimitInSeconds)
            {
                FinishTraining(); // Time limit reached, finish training, we already update data there
                return;
            }

            UpdateTrainingData();
        }

        /// <summary>
        /// Validates training configuration before starting training session.
        /// </summary>
        /// <remarks>
        /// Validation logic:
        /// 1. If time limit enabled, check that limit value > 0
        /// 2. If invalid, show configuration error modal and abort
        /// 3. If valid, store time limit and proceed to StartTraining()
        /// 
        /// Prevents training from starting with invalid configuration.
        /// Called when user presses Start Training button.
        /// </remarks>
        private void ValidateStartTraining()
        {
            var timeLimitValue = UIController.GetTimeLimitConfigurationTextInSeconds();
            if (_useTimeLimit && timeLimitValue <= 0)
            {
                UIController.ShowInvalidTrainingConfigurationModal();
                return;
            }
            else
            {
                _timerLimitInSeconds = timeLimitValue;
                StartTraining();
            }
        }

        /// <summary>
        /// Starts a new polyp detection training session with configured parameters.
        /// </summary>
        /// <remarks>
        /// Start sequence:
        /// 1. Configure UI (show in-progress controls, training state, time limit, stats, AI panel, HUD)
        /// 2. Enable input actions (endoscope movement, pause, polyp detection, AI assistance)
        /// 3. Enable polyp interactables in scene
        /// 4. Select appropriate keybind display profile (with/without AI)
        /// 5. Set training state to active
        /// 
        /// Called after ValidateStartTraining() confirms valid configuration.
        /// Training now active - timer starts, endoscope controls enabled, polyps detectable.
        /// </remarks>
        private void StartTraining()
        {
            UIController.ShowTrainingInProgressUIControls();
            UIController.SetTrainingStateText(UIController.TrainingStateUITitle.InProgress);
            UIController.ConfigureTimeLimitUI(_useTimeLimit);
            UIController.SetPolypStatsElementsActive(_showStatsDuringTraining);
            UIController.SetAIDataPanelActive(_aiAssistanceEnabled);
            UIController.ConfigureTrainingScreenHud(_showStatsDuringTraining);
            UIController.ShowTrainingDataWindow();
            UIController.ShowTrainingStartedMessage();
            InputController.SetAIAssistanceActionActive(_aiAssistanceEnabled);
            InputController.SetAITTSToggleActionActive(_aiAssistanceEnabled);
            _polypDetectionController.TogglePolypInteractables(true);
            SelectEndoscopeKeybindDisplayProfile();
            SetTrainingState(true);

            //ColonoscopyRoomManager.Instance.InputController.SetCharacterTurnEnabled(false);
        }

        /// <summary>
        /// Selects appropriate endoscope keybind display profile based on AI assistance state.
        /// </summary>
        /// <remarks>
        /// Profile selection:
        /// - AI enabled: Show complete keybind display (includes voice recording controls)
        /// - AI disabled: Show reduced keybind display (excludes AI-specific controls)
        /// 
        /// Keeps HUD clean and relevant to current training configuration.
        /// </remarks>
        private void SelectEndoscopeKeybindDisplayProfile()
        {
            if (_aiAssistanceEnabled)
            {
                UIController.SetEndoscopeCompleteKeybindDisplayProfile();
            }
            else
            {
                UIController.SetEndoscopeNoAIKeybindDisplayProfile();
            }
        }


        /// <summary>
        /// Pauses the training session and updates UI with specified pause reason.
        /// </summary>
        /// <param name="trainingStateTitle">UI title indicating reason for pause (standard pause vs identification pause)</param>
        /// <remarks>
        /// Pause actions:
        /// 1. Disable training state (stops timer, disables endoscope input)
        /// 2. Set paused flag
        /// 3. Update UI state text
        /// 4. Show pause-specific UI controls
        /// 5. Display pause message to user
        /// 
        /// Called by StandardPauseTraining() or PauseTrainingForIdentification().
        /// </remarks>
        private void PauseTraining(UIController.TrainingStateUITitle trainingStateTitle)
        {
            SetTrainingState(false);
            _isTrainingPaused = true;
            UIController.SetTrainingStateText(trainingStateTitle);
            UIController.ShowTrainingPausedUIControls();
            UIController.ShowTrainingPausedMessage();
            UIController.SetBaseKeybindDisplayProfile();

            //ColonoscopyRoomManager.Instance.InputController.SetCharacterTurnEnabled(true);
        }

        /// <summary>
        /// Pauses training for polyp identification process.
        /// </summary>
        /// <remarks>
        /// Delegates to PauseTraining() with PausedForIdentification state.
        /// Used when trainee detects polyp(s) and enters identification phase.
        /// Timer stops, endoscope controls disabled, identification UI shown.
        /// </remarks>
        private void PauseTrainingForIdentification() => PauseTraining(UIController.TrainingStateUITitle.PausedForIdentification);

        /// <summary>
        /// Pauses training for manual user pause (not identification).
        /// </summary>
        /// <remarks>
        /// Delegates to PauseTraining() with Paused state.
        /// Used when trainee manually pauses via pause button/keybind.
        /// Timer stops, endoscope controls disabled, pause UI shown.
        /// </remarks>
        private void StandardPauseTraining() => PauseTraining(UIController.TrainingStateUITitle.Paused);

        /// <summary>
        /// Resumes training from paused state.
        /// </summary>
        /// <remarks>
        /// Resume actions:
        /// 1. Update UI state text to InProgress
        /// 2. Show in-progress UI controls
        /// 3. Restore appropriate keybind display profile
        /// 4. Enable training state (restart timer, enable endoscope input)
        /// 5. Clear paused flag
        /// 
        /// Called after manual pause or after identification phase completes with delay.
        /// Training continues from previous elapsed time.
        /// </remarks>
        private void ResumeTraining()
        {
            UIController.SetTrainingStateText(UIController.TrainingStateUITitle.InProgress);
            UIController.ShowTrainingInProgressUIControls();
            SelectEndoscopeKeybindDisplayProfile();
            SetTrainingState(true);
            _isTrainingPaused = false;

            //ColonoscopyRoomManager.Instance.InputController.SetCharacterTurnEnabled(false);
        }

        /// <summary>
        /// Finishes the training session and displays final results with polyp identification answers.
        /// </summary>
        /// <remarks>
        /// Finish sequence:
        /// 1. Disable training state (stop timer, disable endoscope)
        /// 2. Update UI to Finished/Results state
        /// 3. Update training data one final time
        /// 4. Show all undetected polyps on model (reveal what was missed)
        /// 5. Configure UI for results view (hide stats, show score, show answer navigation)
        /// 6. Register missing identification answers for undetected/unidentified polyps
        /// 7. Display first polyp identification answer
        /// 
        /// Called when: time limit reached, user manually finishes, or training completed.
        /// Results screen allows review of all polyp identification attempts.
        /// </remarks>
        private void FinishTraining()
        {
            SetTrainingState(false);
            UIController.SetTrainingStateText(UIController.TrainingStateUITitle.FinishedResults);
            UpdateTrainingData();
            _polypDetectionController.ShowUndetectedPolyps();
            UIController.ShowTrainingFinishedUIControls();
            UIController.SetPolypStatsElementsActive(false);
            UIController.SetFinalScorePanelActive(true);
            UIController.SetPolypIdentificationPanelActive(true);
            UIController.SetPolypIdentificationControlsPanelActive(false);
            UIController.SetPolypIdentificationAnswerScorePanelActive(true);
            UIController.SetPolypIdentificationHUDMsgActive(false);
            UIController.SetBaseKeybindDisplayProfile();
            RegisterMissingIdentificationAnswers();
            ShowCurrentPolypIdentificationAnswer();

            //ColonoscopyRoomManager.Instance.InputController.SetCharacterTurnEnabled(true);
        }

        /// <summary>
        /// Resets all training state and reloads the model to start a fresh training session.
        /// </summary>
        /// <remarks>
        /// Reset operations:
        /// 1. Clear training state (_isTrainingPaused, _elapsedTime, answer index)
        /// 2. Reset all UI bindings and panels
        /// 3. Reset input controller (spline navigator)
        /// 4. Reset all training controllers (progress, detection, identification)
        /// 5. Reload model into skinned mesh renderer (fixes blue tinting issue)
        /// 6. Recreate polyp meshes on the model
        /// 7. Return to training configuration window
        /// 
        /// Note: Model reload necessary to clear material state artifacts from previous session.
        /// </remarks>
        private void RestartTraining()
        {
            _isTrainingPaused = false;
            _elapsedTime = 0f;
            _currentIdentificationAnswerShowingIndex = 0;
            SetTrainingState(false);
            UIController.SetFinalScorePanelActive(false);
            ReturnToTrainingStatsViewFromPolypIdentification();
            UIController.ResetPolypIdentificationPanel();
            UIController.ResetTrainingStatsBindings();
            InputController.ResetSplineNavigator();
            _polypTrainingProgressController.Reset();
            _polypDetectionController.Reset();
            _polypIdentificationController.Reset();
            UIController.ShowTrainingConfigurationWindow();

            //DOING THIS CAUSE MESH HAS BLUE TINING AFTER A TRAINING, PROBABLY SOMETHING TO DO WITH THE MATERIALS
            var currentModel = _applicationManager.GetCurrentModel();
            //currentModel.LoadFromDefaultPath();
            currentModel.LoadIntoSMR(_modelGenerator.WeldedModelRenderer, _modelGenerator.WeldedModelCollider, null);
            _polypDetectionController.SetUpPolypMeshes(currentModel.Diseases, _modelGenerator.WeldedModelRenderer);
            SetWeldedModelNBI(0f);
        }

        /// <summary>
        /// Displays the polyp identification answer at the current result navigation index.
        /// </summary>
        /// <remarks>
        /// Display logic:
        /// 1. Get answer at current index from PolypIdentificationController
        /// 2. Get corresponding polyp
        /// 3. Show answer in UI (location, Paris, JNET, size, correctness)
        /// 4. Set panel title based on answer type:
        ///    - "Not Detected" if polyp wasn't detected
        ///    - "Not Identified" if detected but not identified
        ///    - Standard title if detected and identified
        /// 5. Enable/disable Next button (disabled if at last answer)
        /// 6. Enable/disable Previous button (disabled if at first answer)
        /// 
        /// Called after navigation (Next/Previous) or when first entering results.
        /// Provides comprehensive feedback on each polyp identification attempt.
        /// </remarks>
        private void ShowCurrentPolypIdentificationAnswer()
        {
            var currentPolypIdentificationAnswer = _polypIdentificationController.IdentificationAnswers[_currentIdentificationAnswerShowingIndex];
            var currentPolyp = _polypIdentificationController.IdentificationAnswers[_currentIdentificationAnswerShowingIndex].Polyp;
            var wasPolypDetected = _polypDetectionController.WasPolypDetected(currentPolyp);
            UIController.ShowPolypIdentificationAnswer(currentPolypIdentificationAnswer);
            UIController.SetPolypIdentificationImage(currentPolyp.Image != null ? currentPolyp.Image : Texture2D.blackTexture);
            if (!wasPolypDetected)
            {
                UIController.SetNotDetectedPolypIdentificationPanelTitle();
            }
            else if (currentPolypIdentificationAnswer.WasNotIdentified)
            {
                UIController.SetNotIdentifiedPolypIdentificationPanelTitle();
            }
            else
            {
                UIController.SetStandardPolypIdentificationPanelTitle();
            }
            UIController.SetNextPolypIdentificationAnswerInteractable(_polypDetectionController.TotalPolypCount > 1 && _currentIdentificationAnswerShowingIndex < _polypDetectionController.TotalPolypCount - 1);
            UIController.SetPreviousPolypIdentificationAnswerInteractable(_polypDetectionController.TotalPolypCount > 1 && _currentIdentificationAnswerShowingIndex > 0);
        }

        /// <summary>
        /// Advances to the next polyp identification answer in results view.
        /// </summary>
        /// <remarks>
        /// Navigation logic:
        /// 1. Check if not at last answer
        /// 2. Increment current answer index
        /// 3. Display answer at new index
        /// 
        /// Called when trainee presses Next button in training results screen.
        /// Allows review of all polyp identification attempts in sequence.
        /// </remarks>
        private void SetNextPolypIdentificationAnswerToShow()
        {
            if (_currentIdentificationAnswerShowingIndex < _polypDetectionController.TotalPolypCount - 1)
            {
                _currentIdentificationAnswerShowingIndex++;
                ShowCurrentPolypIdentificationAnswer();
            }
        }

        /// <summary>
        /// Returns to the previous polyp identification answer in results view.
        /// </summary>
        /// <remarks>
        /// Navigation logic:
        /// 1. Check if not at first answer
        /// 2. Decrement current answer index
        /// 3. Display answer at new index
        /// 
        /// Called when trainee presses Previous button in training results screen.
        /// Allows review of all polyp identification attempts in sequence.
        /// </remarks>
        private void SetPreviousPolypIdentificationAnswerToShow()
        {
            if (_currentIdentificationAnswerShowingIndex > 0)
            {
                _currentIdentificationAnswerShowingIndex--;
                ShowCurrentPolypIdentificationAnswer();
            }
        }

        /// <summary>
        /// Creates default identification answers for polyps that were not detected during training.
        /// </summary>
        /// <remarks>
        /// Missing answer logic:
        /// 1. Iterate through all polyp interactables
        /// 2. For each polyp not in identification answers list:
        ///    - Create PolypIdentificationAnswer with:
        ///      * All classifications set to incorrect defaults
        ///      * Score of 0
        ///      * WasNotIdentified flag = true
        /// 3. Add to identification answers list
        /// 
        /// Called at training finish to ensure every polyp has an answer entry.
        /// Allows results screen to show all polyps including undetected ones.
        /// Undetected polyps clearly marked in results UI.
        /// </remarks>
        private void RegisterMissingIdentificationAnswers()
        {
            var polypInteractables = _polypDetectionController.PolypInteractables;
            var polypIdentificationAnswers = _polypIdentificationController.IdentificationAnswers;
            for (int i = 0; i < polypInteractables.Count; i++)
            {
                var polyp = polypInteractables[i].Polyp;
                if (!polypIdentificationAnswers.Any(answer => answer.Polyp == polyp))
                {
                    polypIdentificationAnswers.Add(new PolypIdentificationAnswer(
                        polyp,
                        string.Empty,
                        false,
                        null,
                        false,
                        null,
                        false,
                        null,
                        false,
                        0f,
                        true)); // Mark as not identified
                }
            }
        }

        /// <summary>
        /// Enables or disables core training functionality and initializes/stops progress tracking.
        /// </summary>
        /// <param name="state">True to enable training, false to disable</param>
        /// <remarks>
        /// When enabled (true):
        /// - Enable endoscope movement input actions
        /// - Enable pause action
        /// - Enable polyp detection action
        /// - Initialize PolypTrainingProgressController
        /// - Set _isTrainingEnabled flag
        /// 
        /// When disabled (false):
        /// - Disable all training input actions
        /// - Stop PolypTrainingProgressController
        /// - Clear _isTrainingEnabled flag
        /// 
        /// Called during Start/Pause/Resume/Finish training transitions.
        /// </remarks>
        private void SetTrainingState(bool state)
        {
            InputController.SetEndoscopeMovementActionsEnable(state);
            InputController.SetPauseActionActive(state);
            InputController.SetPolypDetectionActionActive(state);
            if (state)
            {
                _polypTrainingProgressController.Init();
            }
            else
            {
                _polypTrainingProgressController.Stop();
            }
            _isTrainingEnabled = state;
        }

        /// <summary>
        /// Performs polyp detection based on endoscope camera raycast and handles results.
        /// </summary>
        /// <remarks>
        /// Detection flow:
        /// 1. Call PolypDetectionController.PolypDetection() with raycast from endoscope
        /// 2. Get detection result (success/failure/already detected) and list of newly detected polyps
        /// 3. Display detection result message in UI ("Polyp detected!", "Already detected", "No polyp found")
        /// 4. If new polyps detected, start identification phase
        /// 
        /// Called when user presses polyp detection button/key during training.
        /// Multiple polyps can be detected simultaneously if camera sees multiple.
        /// </remarks>
        private void DetectPolyp()
        {
            var polypDetectionResult = _polypDetectionController.PolypDetection(out List<Polyp> newlyDetectedPolyps);
            UIController.ShowPolypDetectionMessage(polypDetectionResult);
            if (newlyDetectedPolyps != null) StartPolypIdentification(in newlyDetectedPolyps);
        }

        /// <summary>
        /// Initiates polyp identification phase for newly detected polyps.
        /// </summary>
        /// <param name="polypsForIdentification">List of polyps that were just detected and need identification</param>
        /// <remarks>
        /// Identification setup:
        /// 1. Pause training for identification (timer stops)
        /// 2. Show identification-specific UI controls
        /// 3. Hide training stats panel, show identification panel
        /// 4. Pass polyps to PolypIdentificationController
        /// 5. Load first polyp for identification
        /// 6. Configure polyp-specific data (image, size dropdown)
        /// 
        /// Trainee must classify location, morphology (Paris), and neoplasia (JNET) for each polyp.
        /// Called from DetectPolyp() when new polyps are detected.
        /// </remarks>
        private void StartPolypIdentification(in List<Polyp> polypsForIdentification)
        {
            PauseTrainingForIdentification();
            UIController.ShowTrainingInPolypIdentificationUIControls();
            UIController.SetTrainingStatsPanelActive(false);
            UIController.SetPolypIdentificationPanelActive(true);
            UIController.SetPolypIdentificationHUDMsgActive(true);
            _polypIdentificationController.SetPolypsForIndentification(polypsForIdentification);
            _polypIdentificationController.SetUpNextPolypToIdentify();
            ConfigurePolypIdentificationSpecificData();
        }

        /// <summary>
        /// Configures UI with polyp-specific data for current identification.
        /// </summary>
        /// <remarks>
        /// Configuration actions:
        /// 1. Load polyp close-up image into identification panel
        /// 2. Populate size dropdown with available size options
        /// 
        /// Called when loading each new polyp in identification queue.
        /// Provides visual reference and contextual options for trainee.
        /// </remarks>
        private void ConfigurePolypIdentificationSpecificData()
        {
            UIController.SetPolypIdentificationImage(_polypIdentificationController.GetPolypInIdentificationImage());
            UIController.PopulatePolypSizeDropdown(_polypIdentificationController.GetSizeDropdownOptions());
        }

        /// <summary>
        /// Registers trainee's polyp identification answer and displays correctness feedback.
        /// </summary>
        /// <param name="snapshot">Snapshot containing trainee's classification choices (location, Paris, JNET, size)</param>
        /// <remarks>
        /// Identification flow:
        /// 1. Pass snapshot to PolypIdentificationController to validate against correct answer
        /// 2. Receive PolypIdentificationAnswer with correctness scores
        /// 3. If showing answers during training:
        ///    - Display answer with correct/incorrect indicators
        ///    - If incorrect, notify PolypTrainingProgressController for AI guidance
        /// 4. Swap UI buttons (hide Identify, show Continue)
        /// 
        /// Answer scored on 10-point scale based on correctness of 4 components.
        /// </remarks>
        private void IdentifyPolyp(PolypIdentificationSnapshot snapshot)
        {
            var polypIdentificationAnswer = _polypIdentificationController.RegisterPolypIdentificationAnswer(in snapshot);
            if (_showAnswersDuringTraining)
            {
                UIController.ShowPolypIdentificationAnswer(polypIdentificationAnswer);
                if (!polypIdentificationAnswer.IsIdentificationCorrect) _polypTrainingProgressController.OnIncorrectPolypIdentification(polypIdentificationAnswer);
            }
            UIController.SwapPolypIdentificationControlPanelActiveButtons(showIdentifyButton: false);
        }

        /// <summary>
        /// Continues polyp identification queue or ends identification phase if queue empty.
        /// </summary>
        /// <remarks>
        /// Continuation logic:
        /// 1. If more polyps in queue:
        ///    - Reset identification panel UI
        ///    - Load next polyp for identification
        ///    - Configure polyp-specific data (image, size)
        /// 2. If queue empty:
        ///    - End identification phase
        ///    - Return to training after countdown delay
        /// 
        /// Called when trainee presses Continue button after identifying a polyp.
        /// </remarks>
        private void ContinueIdentification()
        {
            if (_polypIdentificationController.PolypsToIdentifyCount > 0)
            {
                UIController.ResetPolypIdentificationPanel();
                _polypIdentificationController.SetUpNextPolypToIdentify();
                ConfigurePolypIdentificationSpecificData();
            }
            else
            {
                EndPolypIdentification();
                _polypIdentificationController.EndPolypIdentification(); //TODO maybe the variable in this method is not needed, if we pause training for the identification
            }
        }

        /// <summary>
        /// Resets and hides the identification panel, then shows the training stats panel.
        /// </summary>
        /// <remarks>
        /// Shared transition used when returning to active training view — both after all polyps
        /// are identified (<see cref="EndPolypIdentification"/>) and when restarting a session
        /// (<see cref="RestartTraining"/>).
        /// </remarks>
        private void ReturnToTrainingStatsViewFromPolypIdentification()
        {
            UIController.ResetPolypIdentificationPanel();
            UIController.SetPolypIdentificationPanelActive(false);
            UIController.SetTrainingStatsPanelActive(true);
        }

        /// <summary>
        /// Ends polyp identification phase and prepares to resume training.
        /// </summary>
        /// <remarks>
        /// End sequence:
        /// 1. Switch UI back to training stats view via <see cref="ReturnToTrainingStatsViewFromPolypIdentification"/>
        /// 2. Display countdown message (e.g., "Returning to training in 3...")
        /// 3. Start delayed resume coroutine
        ///
        /// Called when all detected polyps have been identified.
        /// Also calls PolypIdentificationController.EndPolypIdentification() for cleanup.
        /// </remarks>
        private void EndPolypIdentification()
        {
            ReturnToTrainingStatsViewFromPolypIdentification();
            UIController.ShowReturnFromIdentificationMessage(_returnFromIdentificationCountdown);
            UIController.SetPolypIdentificationHUDMsgActive(false);
            StartCoroutine(DelayedResumeTraining(_returnFromIdentificationCountdown));
        }


        /// <summary>
        /// Coroutine that waits for specified delay then resumes training.
        /// </summary>
        /// <param name="delay">Delay in seconds before resuming (countdown time)</param>
        /// <returns>Coroutine enumerator</returns>
        /// <remarks>
        /// Provides brief transition period between identification and training.
        /// Gives trainee time to prepare before endoscope controls re-enable.
        /// Called from EndPolypIdentification() with configured countdown duration.
        /// </remarks>
        private IEnumerator DelayedResumeTraining(float delay)
        {
            yield return new WaitForSeconds(delay);
            ResumeTraining();
        }

        /// <summary>
        /// Updates training statistics UI with current training progress data.
        /// </summary>
        /// <remarks>
        /// Update sequence:
        /// 1. Get current training progress UI state (time, detections, identifications, score)
        /// 2. Update UI controller with state and visibility flags
        /// 3. Respects configuration (show stats during training, show polyp count, etc.)
        /// 
        /// Called every frame during training via CheckTrainingTimer().
        /// Also called once at training finish for final score display.
        /// </remarks>
        private void UpdateTrainingData()
        {
            var trainingProgressUIState = GetTrainingProgressUIState();
            UIController.UpdateTrainingStatsUI(isTrainingStatsActive: _showStatsDuringTraining, isFinalScoreActive: !_isTrainingEnabled, in trainingProgressUIState);
        }

        /// <summary>
        /// Constructs UI state object with current training progress and display configuration.
        /// </summary>
        /// <returns>TrainingProgressUIState containing stats and display flags</returns>
        /// <remarks>
        /// UI state includes:
        /// - Training stats snapshot (time, polyps detected/identified, score)
        /// - Show polyp count view flag (from configuration)
        /// - Total polyp count
        /// - Force polyp count view flag (always show in results)
        /// 
        /// Called by UpdateTrainingData() before updating UI controller.
        /// </remarks>
        private UIController.TrainingProgressUIState GetTrainingProgressUIState()
        {
            var trainingStatsSnapshot = GetTrainingStatsSnapshot();
            return new UIController.TrainingProgressUIState(
                in trainingStatsSnapshot,
                showPolypCountView: _showStatsDuringTraining && _showPolypCountView,
                polypCount: _polypDetectionController.TotalPolypCount,
                forcePolypCountView: !_isTrainingEnabled
            );
        }

        /// <summary>
        /// Constructs snapshot of current training statistics.
        /// </summary>
        /// <returns>PolypTrainingStatsSnapshot with current metrics</returns>
        /// <remarks>
        /// Snapshot fields:
        /// - Timer seconds (elapsed training time)
        /// - Polyps detected (count of successfully detected polyps)
        /// - Polyps identified (count of polyps with identification attempts)
        /// - Global score (0 during training, calculated final score after finish)
        /// 
        /// Score only calculated when training finished to avoid performance impact.
        /// Called by GetTrainingProgressUIState().
        /// </remarks>
        private PolypTrainingStatsSnapshot GetTrainingStatsSnapshot()
        {
            var finalScore = 0f;
            if (!_isTrainingEnabled)
            {
                finalScore = CalculateFinalScore();
            }
            var snapshot = new PolypTrainingStatsSnapshot(
                timerSeconds: _elapsedTime,
                polypsDetected: _polypDetectionController.DetectedPolypCount,
                polypsIdentified: _polypIdentificationController.IdentifiedPolypCount,
                globalScore: finalScore
            );
            return snapshot;
        }

        /// <summary>
        /// Calculates final training score based on identification accuracy, completion, and time penalty.
        /// </summary>
        /// <returns>Final score as float (0.0 to 1.0 scale)</returns>
        /// <remarks>
        /// Scoring formula:
        /// - Base score = (sum of answer scores / 10) / answer count
        ///   * Each answer scored 0-10 based on classification correctness
        ///   * Normalized by number of answers given
        /// - Completion ratio = answers given / total polyps
        /// - Time penalty:
        ///   * No penalty if time ≤ optimal time min
        ///   * Proportional penalty if optimal time min < time ≤ optimal time max
        ///   * Max penalty if time > optimal time max
        /// - Final score = (base score × completion ratio) - time penalty
        /// 
        /// Edge cases:
        /// - Returns 0 if no polyps or no answers
        /// - Partial completion reduces final score proportionally
        /// 
        /// Called when training finishes and when saving results.
        /// </remarks>
        public float CalculateFinalScore()
        {
            var answers = _polypIdentificationController.IdentificationAnswers;
            var totalPolypCount = _polypDetectionController.TotalPolypCount;

            // Check for edge cases
            if (totalPolypCount == 0 || answers.Count == 0)
            {
                return 0f;
            }

            // Calculate the total number of correctly weighted answers
            float totalCorrectWeightedAnswers = answers.Sum(answer => answer.Score / 10f);
            Debug.Log($"Total Correct Weighted Answers: {totalCorrectWeightedAnswers}");

            // Calculate max score based on the number of answers given
            float maxScore = answers.Count; // One max score point per answered polyp

            // Now baseScore is the ratio of the correctly weighted answers to the number of answers given
            double baseScore = maxScore > 0 ? (double)totalCorrectWeightedAnswers / maxScore : 0;

            Debug.Log($"Max Score: {maxScore}");
            Debug.Log($"Base Score: {baseScore}");

            // Calculate completion ratio
            double completionRatio = (double)answers.Count / totalPolypCount;
            Debug.Log($"Completion Ratio: {completionRatio}");

            double timePenalty = 0.0;
            // Apply penalty only if elapsed time exceeds the optimal time min
            if (_elapsedTime > _optimalTimeMin)
            {
                // If elapsed time is between optimalTimeMin and optimalTimeMax, apply proportional penalty
                if (_elapsedTime <= _optimalTimeMax)
                {
                    timePenalty = (_elapsedTime - _optimalTimeMin) * _timePenaltyFactor;
                }
                // If elapsed time exceeds optimalTimeMax, apply maximum penalty
                else if (_elapsedTime > _optimalTimeMax)
                {
                    timePenalty = (_optimalTimeMax - _optimalTimeMin) * _timePenaltyFactor;
                }
            }

            Debug.Log($"Time Penalty: {timePenalty}");

            // Calculate initial final score
            double finalScore = baseScore * completionRatio * 10;
            Debug.Log($"Final Score Before Time Penalty: {finalScore}");

            // Apply time penalty and round
            finalScore = Math.Max(0, Math.Min(10, finalScore - timePenalty));
            Debug.Log($"Final Score After Time Penalty: {finalScore}");

            float roundedFinalScore = (float)Math.Round(finalScore, 2);
            Debug.Log($"Rounded Final Score: {roundedFinalScore}");

            return roundedFinalScore;
        }
    }
}