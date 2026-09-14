// ============================================================================
// ApplicationManager.cs
//
// Central application manager responsible for initialization, scene flow 
// coordination, and maintaining global application state across scene transitions.
// This singleton persists across all scenes via DontDestroyOnLoad.
//
// Usage:
//   - Automatically initializes on scene load (DefaultExecutionOrder -100)
//   - Access via ApplicationManager.Instance
//   - Set/Get current model for training sessions using SetCurrentModel/GetCurrentModel
//   - Configure file encryption via Inspector (_encryptFiles)
//
// Dependencies:
//   - SceneFlowController (must exist in startup scene)
//   - FileManager (initialized on Start)
// ============================================================================

using UnityEngine;

/// <summary>
/// Central application manager responsible for initialization, scene flow coordination,
/// and maintaining global application state across scene transitions.
/// </summary>
/// <remarks>
/// This singleton persists across all scenes and manages:
/// - File encryption settings initialization
/// - Scene flow controller coordination
/// - Current model state for training/editing sessions
/// - Canvas camera configuration after scene loads
/// </remarks>
[DefaultExecutionOrder(-100)]
public class ApplicationManager : MonoBehaviour
{
    #region Singleton
    /// <summary>
    /// Singleton instance accessible throughout the application.
    /// </summary>
    public static ApplicationManager Instance { get; private set; }
    public IAppSettingsProvider AppSettings => _appSettingsController;
    #endregion

    #region Serialized Fields
    /// <summary>
    /// Whether to encrypt saved files for security.
    /// </summary>
    [SerializeField] private bool _encryptFiles = false;
    [SerializeField] private bool _enableRenderingDebugger = false;

    /// <summary>
    /// Duration in seconds to display the startup logo before transitioning.
    /// </summary>
    [SerializeField]
    private float _startupLogoScreenTime = 1f;
    #endregion

    #region Private Fields
    /// <summary>
    /// Reference to the scene flow controller for managing scene transitions.
    /// </summary>
    private SceneFlowController _sceneFlowController;
    private AppSettingsController _appSettingsController;

    /// <summary>
    /// The currently active model being used in training or editing.
    /// </summary>
    private Model _currentModel;
    #endregion

    #region Unity Lifecycle
    /// <summary>
    /// Initializes the singleton instance and ensures persistence across scenes.
    /// </summary>
    private void Awake()
    {
        // Enforce singleton pattern - destroy duplicate instances
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _appSettingsController = GetComponentInChildren<AppSettingsController>();
        if (_appSettingsController == null)
            Debug.LogError("[ApplicationManager] AppSettingsController not found in child objects.");
    }

    /// <summary>
    /// Initializes the file manager, subscribes to scene events, and begins the startup sequence.
    /// </summary>
    private void Start()
    {
        // Requires: UnityEngine.Rendering.DebugManager
        UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI = _enableRenderingDebugger;
        // Initialize file encryption settings
        FileManager.init(_encryptFiles);

        // Get scene flow controller and subscribe to scene load events
        _sceneFlowController = SceneFlowController.Instance;
        if (_sceneFlowController != null)
        {
            _sceneFlowController.OnSceneLoadedAction += SetUpCanvasCamera;
            StartCoroutine(_sceneFlowController.LoadMainRoomScene(_startupLogoScreenTime));
        }
        else
        {
            Debug.LogError("[ApplicationManager] SceneFlowController instance not found!");
        }
    }
    #endregion

    #region Model Management
    /// <summary>
    /// Sets the current model for use in training or editing sessions.
    /// </summary>
    /// <param name="model">The model to set as active.</param>
    public void SetCurrentModel(Model model)
    {
        _currentModel = model;
    }

    /// <summary>
    /// Gets the currently active model.
    /// </summary>
    /// <returns>The current model, or null if none is set.</returns>
    public Model GetCurrentModel()
    {
        return _currentModel;
    }

    /// <summary>
    /// Clears the current model reference.
    /// </summary>
    /// <remarks>
    /// Call this when exiting training/editing sessions to free the model reference.
    /// </remarks>
    public void ClearCurrentModel()
    {
        _currentModel = null;
    }
    #endregion

    #region Canvas Configuration
    /// <summary>
    /// Configures all world-space canvases to use the main camera after a scene load.
    /// </summary>
    /// <remarks>
    /// This ensures UI elements in world space render correctly with the new scene's camera.
    /// Called automatically when a scene finishes loading.
    /// </remarks>
    private void SetUpCanvasCamera()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("[ApplicationManager] No main camera found for canvas configuration.");
            return;
        }

        // Assign main camera to all world-space canvases
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                canvas.worldCamera = mainCamera;
            }
        }
    }
    #endregion
}