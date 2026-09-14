// ============================================================================
// SceneFlowController.cs
//
// Manages scene loading, unloading, and transitions throughout the application.
// Provides event-driven communication between scenes and coordinates provider
// configurations for XR systems.
//
// Usage:
//   1. Access via SceneFlowController.Instance (singleton)
//   2. Subscribe to events for scene lifecycle notifications:
//      - OnSceneLoadedAction / OnSceneUnloadedAction (any scene)
//      - OnSecondaryRoomSceneLoadedAction (training/editor scenes)
//      - OnKeybindDisplayProfileReady, OnTeleportDestinationReady (XR providers)
//   3. Load scenes using:
//      - LoadSceneAsync(BuildScenes) for core scenes
//      - LoadSceneByIndex(int) / UnloadSceneByIndex(int) for organ-specific scenes
//   4. Organ-specific controllers define their own scene enums with build indices
//
// Build Scenes (must match Build Settings order):
//   0 = StartupScene, 1 = MainRoom
//   2+ = Organ-specific scenes (defined in organ transition controllers)
// ============================================================================

using System;
using System.Collections;
using CustomUI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Manages scene loading, unloading, and transitions throughout the application.
/// Provides event-driven communication between scenes and coordinates provider configurations.
/// </summary>
/// <remarks>
/// This singleton controller handles:
/// - Async scene loading/unloading with optional loading modals
/// - Event broadcasting for scene lifecycle (load/unload)
/// - Provider configuration for XR keybinds, teleportation, desktop mode
/// - Secondary room management and exit handling
/// </remarks>
public class SceneFlowController : MonoBehaviour
{
    #region Singleton
    /// <summary>
    /// Singleton instance accessible throughout the application.
    /// </summary>
    public static SceneFlowController Instance { get; private set; }
    #endregion

    #region Serialized Fields
    /// <summary>
    /// Logo GameObject displayed during startup, hidden after initial scene load.
    /// </summary>
    [SerializeField]
    public GameObject _logo;
    #endregion

    #region Events
    /// <summary>Invoked when any scene finishes loading.</summary>
    public event Action OnSceneLoadedAction;

    /// <summary>Invoked when any scene is unloaded.</summary>
    public event Action OnSceneUnloadedAction;

    /// <summary>Invoked when a secondary room scene (training/editor) is loaded.</summary>
    public event Action OnSecondaryRoomSceneLoadedAction;

    /// <summary>Invoked when a secondary room scene (training/editor) is unloaded.</summary>
    public event Action OnSecondaryRoomSceneUnloadedAction;

    /// <summary>Invoked when the model editor scene is loaded.</summary>
    public event Action OnModelEditorSceneLoadedAction;

    /// <summary>Invoked when the model editor scene is unloaded.</summary>
    public event Action OnModelEditorSceneUnloadedAction;

    /// <summary>Invoked when XR keybind display profile is ready from a loaded scene.</summary>
    public event Action<XRKeybindDisplayProfile> OnKeybindDisplayProfileReady;

    /// <summary>Invoked when a teleport destination anchor is ready from a loaded scene.</summary>
    public event Action<TeleportationAnchor> OnTeleportDestinationChange;

    /// <summary>Invoked when desktop mode state changes.</summary>
    public event Action<bool> OnDesktopModeChangeNotified;

    /// <summary>Invoked when user requests to exit the secondary room.</summary>
    public event Action OnExitSecondaryRoomRequested;
    #endregion

    #region Private Fields
    /// <summary>
    /// Tracks the last loaded scene's build index for unloading purposes.
    /// </summary>
    private int _lastLoadedSceneIndex;

    // Cached provider references for event cleanup on scene unload
    private IXRKeybindDisplayProfileProvider _currentKeybindProvider;
    private XRTeleportDestinationProvider _currentTeleportProvider;
    private IXRDesktopModeProvider _currentDesktopModeProvider;
    private ISecondaryRoomManager _currentSecondaryRoomManager;
    #endregion

    #region Build Scenes Enum
    /// <summary>
    /// Core scenes shared across all organs.
    /// Organ-specific scenes (editors, trainings) are defined in their respective controllers.
    /// </summary>
    /// <remarks>
    /// Values must match the build index in Unity's Build Settings.
    /// Indices 2+ are reserved for organ-specific implementations.
    /// </remarks>
    public enum BuildScenes
    {
        StartupScene = 0,
        MainRoom = 1
    }
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
    }

    /// <summary>
    /// Subscribes to Unity's scene management events.
    /// </summary>
    private void Start()
    {
        SetLastLoadedScene((int)BuildScenes.StartupScene);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    /// <summary>
    /// Cleans up event subscriptions when destroyed.
    /// </summary>
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }
    #endregion

    #region Scene State Management
    /// <summary>
    /// Records the last loaded scene's build index for tracking purposes.
    /// </summary>
    /// <param name="sceneIndex">The build index of the scene that was just loaded.</param>
    private void SetLastLoadedScene(int sceneIndex)
    {
        _lastLoadedSceneIndex = sceneIndex;
    }
    #endregion

    #region Scene Loading
    /// <summary>
    /// Asynchronously loads a core scene with optional additive loading and loading modal.
    /// </summary>
    /// <param name="targetScene">The core scene to load.</param>
    /// <param name="useAdditiveLoad">If true, loads additively; otherwise replaces current scene.</param>
    /// <param name="useLoadingModal">If true, displays a loading modal during the operation.</param>
    public void LoadSceneAsync(BuildScenes targetScene, bool useAdditiveLoad = false, bool useLoadingModal = false)
    {
        LoadSceneByIndex((int)targetScene, useAdditiveLoad, useLoadingModal);
    }

    /// <summary>
    /// Generic scene loading by build index. Organ-specific controllers use this for their scenes.
    /// </summary>
    /// <param name="sceneIndex">Build index of the scene to load.</param>
    /// <param name="useAdditiveLoad">If true, loads additively; otherwise replaces current scene.</param>
    /// <param name="useLoadingModal">If true, displays a loading modal during the operation.</param>
    public void LoadSceneByIndex(int sceneIndex, bool useAdditiveLoad = true, bool useLoadingModal = true)
    {
        var loadMode = useAdditiveLoad ? LoadSceneMode.Additive : LoadSceneMode.Single;
        var loadOperation = SceneManager.LoadSceneAsync(sceneIndex, loadMode);
        
        if (loadOperation == null)
        {
            Debug.LogError($"[SceneFlowController] Failed to load scene at index {sceneIndex}");
            return;
        }
        
        StartCoroutine(useLoadingModal 
            ? SceneChangeWithLoadingCoroutine(loadOperation) 
            : SceneChangeCoroutine(loadOperation));
        
        SetLastLoadedScene(sceneIndex);
    }

    /// <summary>
    /// Asynchronously unloads a core scene with optional loading modal and completion callback.
    /// </summary>
    /// <param name="targetScene">The core scene to unload.</param>
    /// <param name="useLoadingModal">If true, displays a loading modal during the operation.</param>
    /// <param name="onSceneUnloaded">Optional callback invoked when unload completes.</param>
    public void UnloadSceneAsync(BuildScenes targetScene, bool useLoadingModal = false, Action onSceneUnloaded = null)
    {
        UnloadSceneByIndex((int)targetScene, useLoadingModal, onSceneUnloaded);
    }

    /// <summary>
    /// Generic scene unloading by build index. Organ-specific controllers use this for their scenes.
    /// </summary>
    /// <param name="sceneIndex">Build index of the scene to unload.</param>
    /// <param name="useLoadingModal">If true, displays a loading modal during the operation.</param>
    /// <param name="onSceneUnloaded">Optional callback invoked when unload completes.</param>
    public void UnloadSceneByIndex(int sceneIndex, bool useLoadingModal = true, Action onSceneUnloaded = null)
    {
        var unloadOperation = SceneManager.UnloadSceneAsync(sceneIndex);
        
        if (unloadOperation == null)
        {
            Debug.LogWarning($"[SceneFlowController] Failed to unload scene at index {sceneIndex}");
            return;
        }

        StartCoroutine(useLoadingModal 
            ? SceneChangeWithLoadingCoroutine(unloadOperation) 
            : SceneChangeCoroutine(unloadOperation));
        
        unloadOperation.completed += _ => onSceneUnloaded?.Invoke();
    }

    /// <summary>
    /// Unloads the most recently loaded scene.
    /// </summary>
    /// <param name="onSceneUnloaded">Optional callback invoked when unload completes.</param>
    public void UnloadLastLoadedScene(Action onSceneUnloaded = null)
    {
        UnloadSceneByIndex(_lastLoadedSceneIndex, true, onSceneUnloaded);
    }
    #endregion

    #region Scene Loading Coroutines
    /// <summary>
    /// Coroutine that waits for an async scene operation to complete.
    /// </summary>
    /// <param name="asyncOperation">The async operation to monitor.</param>
    private IEnumerator SceneChangeCoroutine(AsyncOperation asyncOperation)
    {
        while (!asyncOperation.isDone)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Coroutine that waits for an async scene operation while displaying loading progress.
    /// </summary>
    /// <param name="asyncOperation">The async operation to monitor.</param>
    /// <remarks>
    /// Loading progress UI hooks are commented out - implement as needed.
    /// Progress is normalized: 0.9 represents 100% loaded but not yet activated.
    /// </remarks>
    private IEnumerator SceneChangeWithLoadingCoroutine(AsyncOperation asyncOperation)
    {
        // TODO: Implement loading modal UI
        // uiOverlayController.loadingModal.TurnOn();
        
        while (!asyncOperation.isDone)
        {
            // Normalize progress (Unity reports 0.9 as fully loaded, pending activation)
            float loadProgress = Mathf.Clamp01(asyncOperation.progress / 0.9f);
            // uiOverlayController.loadingProgressBar.SetFill(loadProgress);
            yield return null;
        }
        
        // uiOverlayController.loadingModal.TurnOff();
    }

    /// <summary>
    /// Utility coroutine that waits for a specified duration.
    /// </summary>
    /// <param name="secondsToWait">Duration to wait in seconds.</param>
    private IEnumerator Wait(float secondsToWait)
    {
        yield return new WaitForSeconds(secondsToWait);
    }
    #endregion

    #region Scene Event Handlers
    /// <summary>
    /// Callback invoked by Unity when a scene finishes loading.
    /// </summary>
    /// <param name="scene">The loaded scene.</param>
    /// <param name="mode">The load mode used.</param>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ManageSceneLoad(scene);
        OnSceneLoadedAction?.Invoke();
    }

    /// <summary>
    /// Callback invoked by Unity when a scene is unloaded.
    /// </summary>
    /// <param name="scene">The unloaded scene.</param>
    private void OnSceneUnloaded(Scene scene)
    {
        ManageSceneUnload(scene);
        OnSceneUnloadedAction?.Invoke();
    }
    #endregion

    #region Scene Load Management
    /// <summary>
    /// Performs scene-specific setup actions when a scene loads.
    /// </summary>
    /// <param name="scene">The loaded scene.</param>
    private void ManageSceneLoad(Scene scene)
    {
        // Handle core scenes
        if (scene.buildIndex == (int)BuildScenes.MainRoom)
        {
            // Hide startup logo once main room loads
            if (_logo != null)
                _logo.SetActive(false);
            return;
        }

        // All scenes with index >= 2 are organ-specific secondary rooms
        if (scene.buildIndex >= 2)
        {
            OnSecondaryRoomSceneLoadedAction?.Invoke();
            
            // Determine desktop mode support by naming convention
            // (Training scenes typically need it, editors don't)
            bool includeDesktopMode = scene.name.Contains("Training");
            ConfigureSceneProviders(scene, includeDesktopMode);
            
            // Fire model editor event if scene name indicates editor
            if (scene.name.Contains("Editor"))
            {
                OnModelEditorSceneLoadedAction?.Invoke();
            }
        }
    }

    /// <summary>
    /// Performs cleanup actions when a scene unloads.
    /// </summary>
    /// <param name="scene">The unloaded scene.</param>
    private void ManageSceneUnload(Scene scene)
    {
        // Clean up provider subscriptions to prevent memory leaks
        CleanupProviderSubscriptions();

        // Handle organ-specific scenes (index >= 2)
        if (scene.buildIndex >= 2)
        {
            // Fire model editor unload event if scene name indicates editor
            if (scene.name.Contains("Editor"))
            {
                OnModelEditorSceneUnloadedAction?.Invoke();
            }
            
            OnSecondaryRoomSceneUnloadedAction?.Invoke();
        }
    }
    #endregion

    #region Provider Configuration
    /// <summary>
    /// Configures all scene providers based on the loaded scene's requirements.
    /// </summary>
    /// <param name="scene">The loaded scene to search for providers.</param>
    /// <param name="includeDesktopMode">Whether to configure desktop mode provider.</param>
    private void ConfigureSceneProviders(Scene scene, bool includeDesktopMode)
    {
        ConfigureKeybindDisplayProfile(scene);
        ConfigureTeleportDestinationProvider(scene);
        ConfigureSecondaryRoomManager(scene);

        if (includeDesktopMode)
            ConfigureDesktopModeProvider(scene);
    }

    /// <summary>
    /// Finds and subscribes to the keybind display profile provider in the scene.
    /// </summary>
    /// <param name="scene">The scene to search.</param>
    private void ConfigureKeybindDisplayProfile(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var providers = root.GetComponentsInChildren<IXRKeybindDisplayProfileProvider>(true);
            if (providers != null && providers.Length > 0)
            {
                _currentKeybindProvider = providers[0];
                _currentKeybindProvider.OnKeybindDisplayProfileReady += OnKeybindDisplayProfileChange;
                return;
            }
        }
    }

    /// <summary>
    /// Finds and subscribes to the teleport destination provider in the scene.
    /// </summary>
    /// <param name="scene">The scene to search.</param>
    private void ConfigureTeleportDestinationProvider(Scene scene)
    {
        Debug.Log($"[SceneFlowController] Configuring TeleportDestinationProvider for scene: {scene.name}");
        foreach (var root in scene.GetRootGameObjects())
        {
            var provider = root.GetComponentInChildren<XRTeleportDestinationProvider>(true);
            Debug.Log($"[SceneFlowController] Found TeleportDestinationProvider: {provider?.gameObject.name ?? "null"}");
            if (provider != null)
            {
                _currentTeleportProvider = provider;
                _currentTeleportProvider.DestinationReady += ChangeTeleportDestination;
                return;
            }
        }
    }

    /// <summary>
    /// Finds and subscribes to the desktop mode provider in the scene.
    /// </summary>
    /// <param name="scene">The scene to search.</param>
    private void ConfigureDesktopModeProvider(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var providers = root.GetComponentsInChildren<IXRDesktopModeProvider>(true);
            if (providers != null && providers.Length > 0)
            {
                _currentDesktopModeProvider = providers[0];
                _currentDesktopModeProvider.OnDesktopModeChange += OnDesktopModeChange;
                return;
            }
        }
    }

    /// <summary>
    /// Finds and subscribes to the secondary room manager in the scene.
    /// </summary>
    /// <param name="scene">The scene to search.</param>
    private void ConfigureSecondaryRoomManager(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var providers = root.GetComponentsInChildren<ISecondaryRoomManager>(true);
            if (providers != null && providers.Length > 0)
            {
                _currentSecondaryRoomManager = providers[0];
                _currentSecondaryRoomManager.OnExitRoomRequested += ExitSecondaryRoom;
                return;
            }
        }
    }

    /// <summary>
    /// Unsubscribes from all cached provider events to prevent memory leaks.
    /// </summary>
    private void CleanupProviderSubscriptions()
    {
        if (_currentKeybindProvider != null)
        {
            _currentKeybindProvider.OnKeybindDisplayProfileReady -= OnKeybindDisplayProfileChange;
            _currentKeybindProvider = null;
        }

        if (_currentTeleportProvider != null)
        {
            _currentTeleportProvider.DestinationReady -= ChangeTeleportDestination;
            _currentTeleportProvider = null;
        }

        if (_currentDesktopModeProvider != null)
        {
            _currentDesktopModeProvider.OnDesktopModeChange -= OnDesktopModeChange;
            _currentDesktopModeProvider = null;
        }

        if (_currentSecondaryRoomManager != null)
        {
            _currentSecondaryRoomManager.OnExitRoomRequested -= ExitSecondaryRoom;
            _currentSecondaryRoomManager = null;
        }
    }
    #endregion

    #region Provider Event Handlers
    /// <summary>
    /// Relays keybind profile changes to subscribers.
    /// </summary>
    private void OnKeybindDisplayProfileChange(XRKeybindDisplayProfile profile)
    {
        OnKeybindDisplayProfileReady?.Invoke(profile);
    }

    /// <summary>
    /// Relays teleport destination changes to subscribers.
    /// </summary>
    private void ChangeTeleportDestination(TeleportationAnchor teleportationAnchor)
    {
        Debug.Log("[SceneFlowController] Relaying ChangeTeleportDestination event.");
        OnTeleportDestinationChange?.Invoke(teleportationAnchor);
    }

    /// <summary>
    /// Relays desktop mode changes to subscribers.
    /// </summary>
    private void OnDesktopModeChange(bool isEnabled)
    {
        OnDesktopModeChangeNotified?.Invoke(isEnabled);
    }

    /// <summary>
    /// Relays training room exit requests to subscribers.
    /// </summary>
    private void ExitSecondaryRoom()
    {
        OnExitSecondaryRoomRequested?.Invoke();
    }
    #endregion

    #region Public Scene Navigation
    /// <summary>
    /// Loads the main room after displaying the startup logo.
    /// </summary>
    /// <param name="logoTimeOnScreen">Duration to show the logo before transitioning.</param>
    public IEnumerator LoadMainRoomScene(float logoTimeOnScreen)
    {
        yield return StartCoroutine(Wait(logoTimeOnScreen));
        LoadSceneAsync(BuildScenes.MainRoom, true, false);
    }
    #endregion
}
