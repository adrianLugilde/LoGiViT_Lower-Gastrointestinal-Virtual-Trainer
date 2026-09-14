using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomUI;
using MainRoom;
using ModelEditor;
using UnityEngine;

namespace CoverageTraining
{
    /// <summary>
    /// Central manager for the Coverage Training scene, coordinating all training subsystems.
    /// 
    /// ARCHITECTURE OVERVIEW:
    /// ----------------------
    /// This manager follows a dependency injection pattern to orchestrate the coverage training experience.
    /// It manages initialization, controller coordination, training lifecycle, and scoring.
    /// 
    /// INITIALIZATION FLOW:
    /// --------------------
    /// 1. Awake() - Find and validate MonoBehaviour controllers via SetControllers()
    /// 2. Awake() - Subscribe to all controller events via SubscribeEvents()
    /// 3. Start() - Launch Initialize() coroutine
    /// 4. Initialize() - Wait for model generation, configure camera coverage, setup room
    /// 5. Initialize() - Call ConfigureControllers() to inject dependencies (future extensibility)
    /// 
    /// MANAGED CONTROLLERS:
    /// --------------------
    /// MonoBehaviour Controllers (found in Awake):
    /// - UIController: Manages all UI panels, windows, and HUD elements
    /// - InputController: Handles XR/Desktop input, keybinds, and endoscope control
    /// - CoverageTrainingProgressController: Tracks exploration progress and triggers AI assistance
    /// - OpenAIAudioClient: Manages AI voice assistance (STT, TTS, LLM)
    /// 
    /// TRAINING LIFECYCLE:
    /// -------------------
    /// 1. Configuration Phase: User configures time limits, coverage view, AI assistance
    /// 2. Active Training: Timer runs, coverage tracked, AI assistance available
    /// 3. Pause/Resume: Training can be paused and resumed
    /// 4. Completion: Either time limit reached or manually finished
    /// 5. Results: Final score calculated, ranking updated, results saved
    /// 
    /// SCORING SYSTEM:
    /// ---------------
    /// Final score = (weighted coverage score - time penalty), clamped [0-10]
    /// - Coverage Score: Weighted combination of total + per-section coverage
    /// - Time Penalty: Applied if time exceeds optimal range (10-20 minutes default)
    /// - Sections Tracked: Rectum, Sigmoid, Descending, Transverse, Ascending, Cecum
    /// 
    /// DEPENDENCIES:
    /// -------------
    /// External Services:
    /// - ApplicationManager: Singleton providing app-wide configuration
    /// - FileManager: Static class for loading/saving ranking data
    /// - TrainingsRankingLogic: Static class for ranking insertion logic
    /// 
    /// Scene Components (SerializeField):
    /// - modelGeneratorObject: GameObject containing ISplineModelGenerator component (generates colonoscopy mesh)
    /// - NewCameraCoverage: Tracks camera coverage of the mesh
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
    /// </summary>
    [DefaultExecutionOrder(-200)] // Must execute before dependent controllers
    public class CoverageTrainingManager : MonoBehaviour, ISecondaryRoomManager, IXRDesktopModeProvider
    {
        [Header("Resources")]
        [SerializeField] private GameObject modelGeneratorObject;
        [SerializeField] private XRTeleportDestinationProvider _teleportDestinationProvider;
        [SerializeField] private EndoscopePointLightDisplacer[] _endoscopePointLightDisplacers;
        public NewCameraCoverage CameraCoverage;


        [Header("Training configuration")]
        [Header("Weights for final score calculation (total must be 1)")]
        [SerializeField] private float totalCoverageWeight = 0.4f;
        [SerializeField] private float sigmoidWeight = 0.1f;
        [SerializeField] private float rectumWeight = 0.1f;
        [SerializeField] private float descendingWeight = 0.1f;
        [SerializeField] private float ascendingWeight = 0.1f;
        [SerializeField] private float transverseWeight = 0.1f;
        [SerializeField] private float cecumWeight = 0.1f;

        [Header("Optimal time range for score calculation (in seconds)")]
        public float optimalTimeMin = 600f; // 10 minutes
        public float optimalTimeMax = 1200f; // 20 minutes
        public float timePenaltyFactor = 0.05f; // Penalty factor for time deviation

        [HideInInspector] public UIController UIController { get; private set; }
        [HideInInspector] public InputController InputController { get; private set; }

        private ApplicationManager _applicationManager;
        private IAppSettingsProvider _appSettings;
        private ISplineModelGenerator _modelGenerator;
        private CoverageTrainingProgressController _coverageTrainingProgressController;
        private ModelRotationHandleController _modelRotationHandleController;
        private OpenAIAudioClient _openAIAudioClient;
        private List<CoverageTrainingResult> _rankingTrainingResults = new List<CoverageTrainingResult>();
        private bool _isTrainingEnabled = false;
        private bool _isTrainingPaused = false;
        public bool _useTimeLimit = false;
        public bool _allowCoverageStatsView = false;
        public bool _allowExploredOrganView = false;
        public bool _aiAssistanceEnabled = false;
        private bool _isInsideCoverageViewActive = false;
        private bool _isDesktopModeEnabled = false;
        private bool _isNBIEnabled = false;
        private float _elapsedTime = 0f;
        private float _timerLimitInSeconds = 1500f; // Default to 25 minutes
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
        /// Unity Update lifecycle method. Updates training timer and progress when training is active.
        /// </summary>
        /// <remarks>
        /// Only executes when training is enabled. Delegates to:
        /// - CheckTrainingTimer() for elapsed time and time limit enforcement
        /// - CoverageTrainingProgressController.UpdateProgress() for coverage tracking
        /// </remarks>
        private void Update()
        {
            if (_isTrainingEnabled)
            {
                CheckTrainingTimer();
                _coverageTrainingProgressController.UpdateProgress();
            }
        }

        /// <summary>
        /// Finds and validates all required MonoBehaviour controllers in the scene hierarchy.
        /// </summary>
        /// <remarks>
        /// Controllers found via GetComponentInChildren:
        /// - UIController: UI management
        /// - InputController: Input handling
        /// - CoverageTrainingProgressController: Progress tracking
        /// - OpenAIAudioClient: AI assistance
        /// 
        /// Interface components:
        /// - ISplineModelGenerator: Obtained from modelGeneratorObject GameObject
        /// 
        /// SerializeField validation:
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

            _coverageTrainingProgressController = GetComponentInChildren<CoverageTrainingProgressController>();
            if (_coverageTrainingProgressController == null)
                throw new Exception("CoverageTrainingProgressController not found in children.");

            _openAIAudioClient = GetComponentInChildren<OpenAIAudioClient>();
            if (_openAIAudioClient == null)
                throw new Exception("OpenAIAudioClient not found in children.");

            _modelRotationHandleController = GetComponentInChildren<ModelRotationHandleController>();
            if (_modelRotationHandleController == null)
                throw new Exception("ModelRotationHandleController not found in children.");
        }

        /// <summary>
        /// Subscribes to all controller events to coordinate training flow and user interactions.
        /// </summary>
        /// <remarks>
        /// Event subscriptions organized by controller:
        /// 
        /// UIController Events:
        /// - Configuration switches (time limit, coverage stats, AI assistance)
        /// - Training lifecycle buttons (start, pause, resume, restart, finish)
        /// - Results management (save, ranking display)
        /// - Room navigation (exit confirmation)
        /// 
        /// InputController Events:
        /// - XR input actions (pause, toggle views, locomotion)
        /// - AI voice input (recording start/stop, audio clips)
        /// - Desktop mode toggle
        /// 
        /// OpenAIAudioClient Events:
        /// - LLM responses (text and streaming)
        /// - Speech-to-text transcriptions
        /// - TTS enable state changes
        /// 
        /// CoverageTrainingProgressController Events:
        /// - LLM request triggers for contextual assistance
        /// </remarks>
        private void SubscribeEvents()
        {
            UIController.OnTimeLimitSwitchPressedAction += SetTimeLimitState;
            UIController.OnExplorationDataSwitchPressedAction += SetCoverageStatsViewState;
            UIController.OnExploredOrganViewSwitchPressedAction += HandleExploredOrganViewToggle;
            UIController.OnEnableAIAssistanceSwitchPressedAction += SetAIAssistanceEnable;
            UIController.OnStartTrainingButtonPressedAction += ValidateStartTraining;
            UIController.OnPauseTrainingButtonPressedAction += PauseTraining;
            UIController.OnResumeTrainingButtonPressedAction += ResumeTraining;
            UIController.OnRestartTrainingButtonPressedAction += RestartTraining;
            UIController.OnEndTrainingButtonPressedAction += FinishTraining;
            UIController.OnExitRoomButtonPressedAction += ConfirmExitTrainingRoom;
            UIController.OnConfirmExitRoomButtonPressedAction += ExitTrainingRoom;
            UIController.OnSaveTrainingResultsButtonPressedAction += ConfirmSaveTrainingResults;
            UIController.OnConfirmSaveTrainingResultsButtonPressedAction += SaveTrainingResults;
            UIController.OnShowRankingButtonPressedAction += ShowRanking;
            UIController.OnReturnFromRankingButtonPressedAction += HandleReturnFromRanking;

            InputController.OnToggleNBIAction += HandleNBIToggle;
            InputController.OnPauseControllerAction += PauseTraining;
            InputController.OnToggleExploredOrganViewAction += ToggleInsideCoverageView;
            InputController.OnAudioRecordedAction += HandleAudioRecorded;
            InputController.OnStartRecording += HandleStartRecording;
            InputController.OnStopRecording += HandleStopRecording;
            InputController.ToggleAITTS += ToggleAIVoiceAssistance;
            InputController.DestkopModeChange += ManageDesktopMode;

            _openAIAudioClient.OnLLMResponseReceived += HandleLLMResponse;
            _openAIAudioClient.OnSTTResponseReceived += HandleSTTResponse;
            _openAIAudioClient.TTSEnableChanged += ManageAIAssistanceEnableChange;

            _coverageTrainingProgressController.SendLLMRequest += SendLLMRequest;

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
                UIController.OnExplorationDataSwitchPressedAction -= SetCoverageStatsViewState;
                UIController.OnExploredOrganViewSwitchPressedAction -= HandleExploredOrganViewToggle;
                UIController.OnEnableAIAssistanceSwitchPressedAction -= SetAIAssistanceEnable;
                UIController.OnStartTrainingButtonPressedAction -= ValidateStartTraining;
                UIController.OnPauseTrainingButtonPressedAction -= PauseTraining;
                UIController.OnResumeTrainingButtonPressedAction -= ResumeTraining;
                UIController.OnRestartTrainingButtonPressedAction -= RestartTraining;
                UIController.OnEndTrainingButtonPressedAction -= FinishTraining;
                UIController.OnExitRoomButtonPressedAction -= ConfirmExitTrainingRoom;
                UIController.OnConfirmExitRoomButtonPressedAction -= ExitTrainingRoom;
                UIController.OnSaveTrainingResultsButtonPressedAction -= ConfirmSaveTrainingResults;
                UIController.OnConfirmSaveTrainingResultsButtonPressedAction -= SaveTrainingResults;
                UIController.OnShowRankingButtonPressedAction -= ShowRanking;
                UIController.OnReturnFromRankingButtonPressedAction -= HandleReturnFromRanking;
            }

            if (InputController != null)
            {
                InputController.OnPauseControllerAction -= PauseTraining;
                InputController.OnToggleExploredOrganViewAction -= ToggleInsideCoverageView;
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
                _openAIAudioClient.TTSEnableChanged -= ManageAIAssistanceEnableChange;
            }

            if (_coverageTrainingProgressController != null)
            {
                _coverageTrainingProgressController.SendLLMRequest -= SendLLMRequest;
            }

            if (_modelRotationHandleController != null)
            {
                _modelRotationHandleController.OnRotationStarted -= OnRotationHandleGrabStart;
                _modelRotationHandleController.OnRotationReleased -= OnRotationHandleGrabEnd;
                _modelRotationHandleController.OnRotationReleased -= CameraCoverage.NotifyTransformChanged;
                _modelRotationHandleController.OnRotationRestored -= CameraCoverage.NotifyTransformChanged;
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
                Debug.LogError("[CoverageTrainingManager] IAppSettingsProvider not available from ApplicationManager.");
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
        /// 4. Generate welded colonoscopy mesh model
        /// 5. Apply HDRP material to mesh
        /// 6. Wait for SegmentedModelGenerated flag
        /// 7. Recalculate mesh bounds for proper rendering
        /// 8. Configure CameraCoverage with source mesh
        /// 9. Disable endoscope input until training starts
        /// 10. Load ranking data from disk
        /// 11. Initialize UI with coverage stats visible
        /// 12. Configure VR teleportation destinations
        /// 13. Attach dynamic lighting to endoscope
        /// 14. Call ConfigureControllers for dependency injection
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
            _modelGenerator.GenerateWeldedModel(false, true);
            ConfigureControllers();
            //_modelGenerator.WeldedModelRenderer.sharedMaterials = new Material[] { Resources.Load<Material>(Path.Combine(FileManager.materialResourcesPath, "LI_HDRP_Material")) };

            yield return new WaitUntil(() => _modelGenerator.IsWeldedModelGenerated);
            _modelGenerator.WeldedModelRenderer.sharedMesh.RecalculateBounds();
            CameraCoverage.SourceMeshRenderer = _modelGenerator.WeldedModelRenderer;
            InputController.InitializeSplineNavigator(_modelGenerator.Spline);
            yield return new WaitUntil(() => InputController.IsReady);

            InputController.SetEndoscopeMovementActionsEnable(false);
            InputController.SetInTrainingActionsEnable(false);
            LoadRankingData();
            //SetCoverageStatsViewState(true);
            SetRoomTeleportDestination();
            for (int i = 0; i < _endoscopePointLightDisplacers.Length; i++)
            {
                _endoscopePointLightDisplacers[i].targetMesh = _modelGenerator.WeldedModelRenderer.gameObject;
            }
            SetWeldedModelNBI(0f); // Ensure model starts in normal view
        }

        /// <summary>
        /// Configures all controllers with their dependencies after initialization completes.
        /// </summary>
        /// <remarks>
        /// This method provides a centralized location for dependency injection and controller configuration.
        /// Called after model generation and basic setup are complete.
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
        /// // Future: Training mode controllers with business logic
        /// _trainingModeController = new TrainingModeController(
        ///     UIController,
        ///     InputController,
        ///     _coverageTrainingProgressController);
        /// </code>
        /// </remarks>
        private void ConfigureControllers()
        {
            _modelRotationHandleController.Initialize();
            _modelRotationHandleController.OnRotationStarted += OnRotationHandleGrabStart;
            _modelRotationHandleController.OnRotationReleased += OnRotationHandleGrabEnd;
            _modelRotationHandleController.OnRotationReleased += CameraCoverage.NotifyTransformChanged;
            _modelRotationHandleController.OnRotationRestored += CameraCoverage.NotifyTransformChanged;
        }

        private void OnRotationHandleGrabStart() => InputController.SetEndoscopeGrabSuppressed(true);
        private void OnRotationHandleGrabEnd() => InputController.SetEndoscopeGrabSuppressed(false);

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
        /// Shows the exit room confirmation modal to prevent accidental exits.
        /// </summary>
        private void ConfirmExitTrainingRoom()
        {
            UIController.ShowConfirmExitRoomModal();
        }

        /// <summary>
        /// Triggers the exit room process, notifying the application manager to return to main room.
        /// </summary>
        /// <remarks>
        /// Invokes OnExitRoomRequested event, typically handled by scene management system.
        /// Implements ISecondaryRoomManager interface.
        /// </remarks>
        private void ExitTrainingRoom()
        {
            OnExitRoomRequested?.Invoke();
        }

        /// <summary>
        /// Handler for explored organ view toggle from UI.
        /// </summary>
        /// <param name="state">New toggle state</param>
        private void HandleExploredOrganViewToggle(bool state)
        {
            _allowExploredOrganView = state;
        }

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
        /// Shows the save training results modal to prompt user for name entry.
        /// </summary>
        private void ConfirmSaveTrainingResults()
        {
            UIController.ShowSaveTrainingResultsModal();
        }

        /// <summary>
        /// Loads saved training rankings from disk and populates the ranking UI.
        /// </summary>
        /// <remarks>
        /// Uses FileManager to load persisted CoverageTrainingResult list.
        /// Casts results to base TrainingResult type for UI population.
        /// </remarks>
        private void LoadRankingData()
        {
            _rankingTrainingResults = FileManager.LoadTrainingRankingData<CoverageTrainingResult>();
            UIController.PopulateTrainingRanking(_rankingTrainingResults.Cast<TrainingResult>().ToList());
        }

        /// <summary>
        /// Saves training results to the ranking board if score qualifies for top 10.
        /// </summary>
        /// <param name="name">Player name entered by user</param>
        /// <remarks>
        /// Process:
        /// 1. Creates CoverageTrainingResult with current stats
        /// 2. Attempts insertion using CoverageTrainingRankingPolicy (bounded at 10 entries)
        /// 3. Persists updated ranking to disk if accepted
        /// 4. Updates UI with success/failure message
        /// 5. Disables save button to prevent duplicate saves
        /// </remarks>
        private void SaveTrainingResults(string name)
        {
            var trainingResult = new CoverageTrainingResult
            {
                PlayerName = name,
                Score = CalculateFinalScore(),
                TimeInSeconds = _elapsedTime,
                CoveragePercent = CameraCoverage.coveragePercentage,
            };


            bool accepted = TrainingsRankingLogic.TryInsertBounded(
                _rankingTrainingResults,
                trainingResult,
                new CoverageTrainingRankingPolicy(),
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
        /// Displays the ranking window showing top 10 training results.
        /// </summary>
        private void ShowRanking()
        {
            UIController.ShowRankingWindow();
        }

        /// <summary>
        /// Sets whether AI assistance is enabled for the current training session.
        /// </summary>
        /// <param name="isEnabled">True to enable AI assistance (voice commands, contextual help)</param>
        private void SetAIAssistanceEnable(bool isEnabled)
        {
            _aiAssistanceEnabled = isEnabled;
        }

        /// <summary>
        /// Configures VR teleportation destinations for room navigation.
        /// </summary>
        /// <remarks>
        /// Notifies XRTeleportDestinationProvider that the room is ready for teleportation.
        /// </remarks>
        private void SetRoomTeleportDestination()
        {
            _teleportDestinationProvider?.NotifyReady();
        }

        /// <summary>
        /// Handles recorded audio clips from voice input, sending them for speech-to-text processing.
        /// </summary>
        /// <param name="clip">Recorded AudioClip from microphone input</param>
        private void HandleAudioRecorded(AudioClip clip)
        {
            _openAIAudioClient.SendSTTRequest(clip);
        }

        /// <summary>
        /// Handles the start of voice recording, showing UI feedback.
        /// </summary>
        private void HandleStartRecording()
        {
            UIController.ShowAIStartRecordingMessage();
        }

        /// <summary>
        /// Handles the end of voice recording, showing UI feedback.
        /// </summary>
        private void HandleStopRecording()
        {
            UIController.ShowAIStopRecordingMessage();
        }

        /// <summary>
        /// Toggles AI text-to-speech voice responses on/off.
        /// </summary>
        /// <remarks>
        /// Public method callable from input actions or UI buttons.
        /// Delegates to OpenAIAudioClient to manage TTS state.
        /// </remarks>
        public void ToggleAIVoiceAssistance()
        {
            _openAIAudioClient.ToggleTTS();
        }

        /// <summary>
        /// Sends a text prompt to the LLM for contextual assistance (only if AI is enabled).
        /// </summary>
        /// <param name="message">User message or context prompt</param>
        /// <param name="systemPrompt">System-level instructions for the LLM</param>
        /// <remarks>
        /// Guarded by _aiAssistanceEnabled flag to prevent unauthorized API calls.
        /// Used by CoverageTrainingProgressController to provide contextual help.
        /// </remarks>
        private void SendLLMRequest(string message, string systemPrompt)
        {
            if (_aiAssistanceEnabled) _openAIAudioClient?.SendLLMRequest(message, systemPrompt);
        }

        /// <summary>
        /// Updates UI to reflect AI assistance toggle state change.
        /// </summary>
        /// <param name="state">New TTS enabled state</param>
        public void ManageAIAssistanceEnableChange(bool state)
        {
            UIController.ShowAIToggleMessage(state);
        }

        /// <summary>
        /// Handles LLM text responses (streaming or final) and displays them in the chat UI.
        /// </summary>
        /// <param name="response">LLM response text</param>
        /// <param name="isFinal">True if this is the final response; false if streaming partial response</param>
        /// <remarks>
        /// If TTS is enabled, also shows a message toast with the response.
        /// </remarks>
        private void HandleLLMResponse(string response, bool isFinal)
        {
            UIController.CreateAIChatMessage(response, isFinal);
            if (_openAIAudioClient.IsTTSEnabled()) UIController.ShowLLMResponseMessage(response);
        }

        /// <summary>
        /// Handles speech-to-text transcription results from voice input.
        /// </summary>
        /// <param name="response">Transcribed text from user's voice command</param>
        /// <remarks>
        /// Process:
        /// 1. Display transcription in chat UI
        /// 2. Show message toast if TTS enabled
        /// 3. Forward to CoverageTrainingProgressController for command processing
        /// </remarks>
        private void HandleSTTResponse(string response)
        {
            UIController.CreateAIChatMessage(response, true);
            if (_openAIAudioClient.IsTTSEnabled()) UIController.ShowLLMResponseMessage(response);
            _coverageTrainingProgressController.OnSTTResponseReceived(response);
        }

        /// <summary>
        /// Sets whether the training session has a time limit.
        /// </summary>
        /// <param name="state">True to enable time limit; false for unlimited time</param>
        /// <remarks>
        /// Updates UI to show/hide the time limit input field.
        /// </remarks>
        private void SetTimeLimitState(bool state)
        {
            _useTimeLimit = state;
            UIController.SetTimeLimitFieldTextActive(_useTimeLimit);
        }

        /// <summary>
        /// Sets whether real-time coverage statistics are visible during training.
        /// </summary>
        /// <param name="state">True to show coverage stats; false to hide them until training ends</param>
        /// <remarks>
        /// Affects HUD visibility during active training.
        /// Stats are always shown after training completion regardless of this setting.
        /// </remarks>
        private void SetCoverageStatsViewState(bool state)
        {
            _allowCoverageStatsView = state;
            UIController.SetCoverageStatsElementsActive(_allowCoverageStatsView);
        }

        /// <summary>
        /// Monitors training timer, enforces time limits, and updates UI with current stats.
        /// </summary>
        /// <remarks>
        /// Called every frame from Update() when training is active.
        /// 
        /// Behavior:
        /// - Increments elapsed time by Time.deltaTime
        /// - If time limit enabled and exceeded, automatically finishes training
        /// - Otherwise updates UI with current training statistics
        /// 
        /// Does nothing if training is paused or disabled.
        /// </remarks>
        private void CheckTrainingTimer()
        {
            if (!_isTrainingEnabled || _isTrainingPaused) return;

            _elapsedTime += Time.deltaTime;

            if (_useTimeLimit && _elapsedTime > _timerLimitInSeconds)
            {
                FinishTraining(); // Time limit reached, finish training, we already update data there
                return;
            }

            UpdateTrainingData();
        }

        /// <summary>
        /// Validates training configuration and starts the training session if valid.
        /// </summary>
        /// <remarks>
        /// Validation:
        /// - If time limit enabled, checks that time value is positive
        /// - Shows error modal if validation fails
        /// - Stores validated time limit value
        /// 
        /// Delegates to StartTraining() if validation passes.
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
        /// Selects appropriate keybind display profile based on enabled features.
        /// </summary>
        /// <remarks>
        /// Profiles determined by combination of _allowExploredOrganView and _aiAssistanceEnabled:
        /// - Both enabled: Complete profile with all keybinds
        /// - Both disabled: Minimal profile
        /// - Only AI: AI keybinds without organ view
        /// - Only organ view: Organ view keybinds without AI
        /// 
        /// Updates UI controller to show appropriate keybind hints during training.
        /// </remarks>
        private void SelectEndoscopeKeybindDisplayProfile()
        {
            switch ((_allowExploredOrganView, _aiAssistanceEnabled))
            {
                case (true, true):
                    UIController.SetEndoscopeCompleteKeybindDisplayProfile();
                    break;
                case (false, false):
                    UIController.SetEndoscopeNoOrganViewNoAIKeybindDisplayProfile();
                    break;
                case (false, true):
                    UIController.SetEndoscopeNoOrganViewKeybindDisplayProfile();
                    break;
                case (true, false):
                    UIController.SetEndoscopeNoAIKeybindDisplayProfile();
                    break;
            }
        }

        /// <summary>
        /// Starts a new training session with validated configuration.
        /// </summary>
        /// <remarks>
        /// Initialization sequence:
        /// 1. Show in-progress UI controls
        /// 2. Configure UI elements based on training settings:
        ///    - Time limit display (if enabled)
        ///    - Coverage stats (if allowed during training)
        ///    - AI data panel (if AI assistance enabled)
        /// 3. Configure HUD and training screen layout
        /// 4. Enable appropriate input actions:
        ///    - AI assistance actions (if enabled)
        ///    - Explored organ view toggle (if allowed)
        /// 5. Select keybind display profile
        /// 6. Activate training state (enables timer, coverage tracking, input)
        /// 7. Show training started message
        /// </remarks>
        private void StartTraining()
        {
            UIController.ShowTrainingInProgressUIControls();
            UIController.SetTrainingStateText(UIController.TrainingStateUITitle.InProgress);
            UIController.ConfigureTimeLimitUI(_useTimeLimit);
            UIController.SetCoverageStatsElementsActive(_allowCoverageStatsView);
            UIController.SetAIDataPanelActive(_aiAssistanceEnabled);
            UIController.ConfigureTrainingScreenHud(_allowCoverageStatsView);
            UIController.ShowTrainingDataWindow();
            UIController.ShowTrainingStartedMessage();
            InputController.SetAIAssistanceActionActive(_aiAssistanceEnabled);
            InputController.SetAITTSToggleActionActive(_aiAssistanceEnabled);
            InputController.SetExploredOrganViewActionActive(_allowExploredOrganView);
            SelectEndoscopeKeybindDisplayProfile();
            SetTrainingState(true);
            //_modalView.ShowTempModal();

            //ColonoscopyRoomManager.Instance.InputController.SetCharacterTurnEnabled(false);
        }

        /// <summary>
        /// Pauses the current training session, freezing timer and coverage tracking.
        /// </summary>
        /// <remarks>
        /// Pause behavior:
        /// - Sets _isTrainingPaused flag
        /// - Disables training state (stops timer and coverage)
        /// - Shows pause UI controls (resume/restart buttons)
        /// - Resets keybind display to base profile
        /// - Shows pause message toast
        /// 
        /// Training can be resumed via ResumeTraining() without losing progress.
        /// </remarks>
        private void PauseTraining()
        {
            SetTrainingState(false);
            _isTrainingPaused = true;
            UIController.SetTrainingStateText(UIController.TrainingStateUITitle.Paused);
            UIController.ShowTrainingPausedUIControls();
            UIController.ShowTrainingPausedMessage();
            UIController.SetBaseKeybindDisplayProfile();

            //ColonoscopyRoomManager.Instance.InputController.SetCharacterTurnEnabled(true);
        }

        /// <summary>
        /// Resumes a paused training session, continuing from where it left off.
        /// </summary>
        /// <remarks>
        /// Resume behavior:
        /// - Clears _isTrainingPaused flag
        /// - Re-enables training state (timer and coverage resume)
        /// - Shows in-progress UI controls
        /// - Restores appropriate keybind display profile
        /// - Updates state text to "In Progress"
        /// </remarks>
        private void ResumeTraining()
        {
            UIController.SetTrainingStateText(UIController.TrainingStateUITitle.InProgress);
            UIController.ShowTrainingInProgressUIControls();
            SetTrainingState(true);
            SelectEndoscopeKeybindDisplayProfile();
            _isTrainingPaused = false;

            //ColonoscopyRoomManager.Instance.InputController.SetCharacterTurnEnabled(false);
        }

        /// <summary>
        /// Finishes the current training session and displays final results.
        /// </summary>
        /// <remarks>
        /// Completion sequence:
        /// 1. Disable training state (stops timer and coverage tracking)
        /// 2. Update state text to "Finished - Results"
        /// 3. Force coverage stats visible if they were hidden during training
        /// 4. Update UI with final training statistics
        /// 5. Show finished UI controls (save, restart, exit)
        /// 6. Activate final score panel
        /// 7. Enable camera coverage visualization (inside view)
        /// 8. Reset keybind display to base profile
        /// 
        /// Called either manually by user or automatically when time limit reached.
        /// </remarks>
        private void FinishTraining()
        {
            SetTrainingState(false);
            UIController.SetTrainingStateText(UIController.TrainingStateUITitle.FinishedResults);
            if (!_allowCoverageStatsView) UIController.SetCoverageStatsElementsActive(true);
            UpdateTrainingData();
            UIController.ShowTrainingFinishedUIControls();
            UIController.SetFinalScorePanelActive(true);
            ToggleCameraCoverageControllerResults(true);
            UIController.SetBaseKeybindDisplayProfile();

            //ColonoscopyRoomManager.Instance.InputController.SetCharacterTurnEnabled(true);
        }

        /// <summary>
        /// Resets the training session to initial state, clearing all progress.
        /// </summary>
        /// <remarks>
        /// Reset sequence:
        /// 1. Clear pause flag and elapsed time
        /// 2. Disable inside coverage view
        /// 3. Disable training state
        /// 4. Hide final score panel
        /// 5. Reset UI stats bindings to zero
        /// 6. Disable camera coverage visualization
        /// 7. Reset endoscope navigator to start position
        /// 8. Reset CameraCoverage tracking data
        /// 9. Reset progress controller state
        /// 10. Show configuration window for new session setup
        /// </remarks>
        private void RestartTraining()
        {
            _isTrainingPaused = false;
            _elapsedTime = 0f;
            _isInsideCoverageViewActive = false;
            SetTrainingState(false);
            UIController.SetFinalScorePanelActive(false);
            UIController.ResetTrainingStatsBindings();
            ToggleCameraCoverageControllerResults(false);
            InputController.ResetSplineNavigator();
            CameraCoverage.Reset();
            _coverageTrainingProgressController.Reset();
            UIController.ShowTrainingConfigurationWindow();
            SetWeldedModelNBI(0f);
        }

        /// <summary>
        /// Sets the core training state, enabling/disabling coverage tracking and input.
        /// </summary>
        /// <param name="state">True to enable training; false to disable</param>
        /// <remarks>
        /// Controls affected:
        /// - CameraCoverage component enabled state
        /// - Endoscope movement input actions
        /// - Pause action availability
        /// - Explored organ view action (if allowed in config)
        /// - CoverageTrainingProgressController init/stop
        /// 
        /// Sets _isTrainingEnabled flag which gates Update() execution.
        /// </remarks>
        private void SetTrainingState(bool state)
        {
            CameraCoverage.enabled = state;
            InputController.SetEndoscopeMovementActionsEnable(state);
            InputController.SetPauseActionActive(state);
            InputController.SetExploredOrganViewActionActive(state && _allowExploredOrganView);
            if (state)
            {
                _coverageTrainingProgressController.Init();
            }
            else
            {
                _coverageTrainingProgressController.Stop();
            }
            _isTrainingEnabled = state;
        }

        /// <summary>
        /// Toggles between normal mesh view and inside camera coverage visualization.
        /// </summary>
        /// <param name="state">True to show coverage visualization; false to show normal mesh</param>
        /// <remarks>
        /// Visual modes:
        /// - Normal (state=false): Shows welded mesh renderer with standard material
        /// - Coverage (state=true): Shows color-coded coverage map from inside
        /// 
        /// Used at training completion to visualize explored areas.
        /// </remarks>
        private void ToggleCameraCoverageControllerResults(bool state)
        {
            _modelGenerator.WeldedModelRenderer.enabled = !state;
            CameraCoverage.SetInsideCameraCoverageViewActive(state);
        }

        /// <summary>
        /// Toggles the inside coverage view on/off during active training.
        /// </summary>
        /// <remarks>
        /// Allows user to preview coverage visualization during training (if allowed in config).
        /// Toggled via input action (e.g., XR controller button).
        /// </remarks>
        private void ToggleInsideCoverageView()
        {
            _isInsideCoverageViewActive = !_isInsideCoverageViewActive;
            CameraCoverage.SetInsideCameraCoverageViewActive(_isInsideCoverageViewActive);
        }

        /// <summary>
        /// Updates UI with current training statistics snapshot.
        /// </summary>
        /// <remarks>
        /// Calls GetTrainingStatsSnapshot() to capture current state.
        /// Passes snapshot to UIController for display.
        /// Respects visibility settings (_allowCoverageStatsView, _isTrainingEnabled).
        /// </remarks>
        private void UpdateTrainingData()
        {
            var trainingStatsSnapshot = GetTrainingStatsSnapshot();
            UIController.UpdateTrainingStatsUI(isCoverageStatsActive: _allowCoverageStatsView, isFinalScoreActive: !_isTrainingEnabled, in trainingStatsSnapshot);
        }

        /// <summary>
        /// Captures current training statistics as an immutable snapshot.
        /// </summary>
        /// <returns>CoverageTrainingStatsSnapshot containing all current metrics</returns>
        /// <remarks>
        /// Captured metrics:
        /// - Elapsed time (seconds)
        /// - Total organ coverage percentage
        /// - Per-section coverage percentages (rectum, sigmoid, descending, transverse, ascending, cecum)
        /// - Final score (0 if training active; calculated if finished)
        /// </remarks>
        private CoverageTrainingStatsSnapshot GetTrainingStatsSnapshot()
        {
            var snapshot = new CoverageTrainingStatsSnapshot(
                timerSeconds: _elapsedTime,
                organExplored: CameraCoverage.coveragePercentage,
                rectumExplored: CameraCoverage.rectumCoveragePercentage,
                sigmoidExplored: CameraCoverage.sigmoidCoveragePercentage,
                descendingExplored: CameraCoverage.descendingColonCoveragePercentage,
                transverseExplored: CameraCoverage.transverseColonCoveragePercentage,
                ascendingExplored: CameraCoverage.ascendingColonCoveragePercentage,
                cecumExplored: CameraCoverage.cecumCoveragePercentage,
                finalScore: _isTrainingEnabled == true ? 0f : CalculateFinalScore()
            );
            return snapshot;
        }

        /// <summary>
        /// Calculates the final training score based on weighted coverage and time penalty.
        /// </summary>
        /// <returns>Final score clamped to range [0, 10]</returns>
        /// <remarks>
        /// Score formula:
        /// 1. Coverage Score (weighted sum of section percentages, normalized to 10-point scale):
        ///    - Total coverage: 40% weight
        ///    - Rectum: 10% weight
        ///    - Sigmoid: 10% weight
        ///    - Descending: 10% weight
        ///    - Transverse: 10% weight
        ///    - Ascending: 10% weight
        ///    - Cecum: 10% weight
        /// 
        /// 2. Time Penalty (applied if time exceeds optimal range):
        ///    - No penalty if time ≤ optimalTimeMin (default: 10 minutes)
        ///    - Proportional penalty if optimalTimeMin < time ≤ optimalTimeMax
        ///    - Maximum penalty if time > optimalTimeMax (default: 20 minutes)
        ///    - Penalty = (time deviation) * timePenaltyFactor (default: 0.05)
        /// 
        /// 3. Final Score = Clamp(CoverageScore - TimePenalty, 0, 10)
        /// </remarks>
        private float CalculateFinalScore()
        {
            float coverageScore = ((CameraCoverage.coveragePercentage * totalCoverageWeight) +
                                 (CameraCoverage.rectumCoveragePercentage * rectumWeight) +
                                 (CameraCoverage.sigmoidCoveragePercentage * sigmoidWeight) +
                                 (CameraCoverage.descendingColonCoveragePercentage * descendingWeight) +
                                 (CameraCoverage.transverseColonCoveragePercentage * transverseWeight) +
                                 (CameraCoverage.ascendingColonCoveragePercentage * ascendingWeight) +
                                 (CameraCoverage.cecumCoveragePercentage * cecumWeight)) / 10f;

            // Calculate time penalty
            float timePenalty = 0.0f;
            // Apply penalty only if elapsed time exceeds the optimal time min
            if (_elapsedTime > optimalTimeMin)
            {
                // If elapsed time is between optimalTimeMin and optimalTimeMax, apply proportional penalty
                if (_elapsedTime <= optimalTimeMax)
                {
                    timePenalty = (_elapsedTime - optimalTimeMin) * timePenaltyFactor;
                }
                // If elapsed time exceeds optimalTimeMax, apply maximum penalty
                else if (_elapsedTime > optimalTimeMax)
                {
                    timePenalty = (optimalTimeMax - optimalTimeMin) * timePenaltyFactor;
                }
            }

            // Calculate final score, ensuring it's within the range [0, 10]
            return Mathf.Clamp(coverageScore - timePenalty, 0f, 10f);
        }
    }
}