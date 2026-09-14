// ============================================================================
// ColonoscopyRoomsTransitionController.cs
//
// Manages transitions between the main room and colonoscopy secondary rooms
// (training, model editor). Coordinates teleportation, door animations, and
// scene lifecycle for the colonoscopy/large intestine training environment.
//
// Transition Flow (Enter):
//   1. EnterRoom(sceneIndex) called by ColonoscopyEnvironmentHandler
//   2. Scene loads → OnSecondaryRoomSceneLoaded → doors open
//   3. Teleport destination ready → OnTransitionCompleted fired
//   4. TeleportToSecondaryRoom() teleports player, closes doors
//
// Transition Flow (Exit):
//   1. Door button pressed OR OnExitSecondaryRoomRequested event
//   2. Player teleported back to main room anchor
//   3. Scene unloads → OnReturnCompleted fired
//
// Scene Indices (must match Unity Build Settings):
//   - ModelEditor = 2
//   - CoverageTraining = 3
//   - PolypTraining = 4
//
// Configuration (Inspector):
//   - _mainRoomTeleportAnchor: XR anchor to teleport back to main room
//   - _doorsController: Controller for door open/close animations
//
// Events:
//   - OnTransitionStarted: Scene loading begun
//   - OnTransitionCompleted: Ready to teleport into secondary room
//   - OnReturnStarted: Beginning exit sequence
//   - OnReturnCompleted: Fully returned, scene unloaded
//
// Dependencies:
//   - SceneFlowController (singleton, must exist)
//   - PracticableDoorsController (assigned in Inspector)
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace MainRoom
{
    /// <summary>
    /// Manages transitions between main room and colonoscopy secondary rooms.
    /// Coordinates teleportation, door animations, and scene loading.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="ISecondaryRoomTransitionController"/> for the colonoscopy
    /// environment. Used by <see cref="ColonoscopyEnvironmentHandler"/> to load
    /// coverage training, polyp training, and model editor scenes.
    /// </remarks>
    public class ColonoscopyRoomsTransitionController : MonoBehaviour, ISecondaryRoomTransitionController
    {
        #region Scene Types

        /// <summary>
        /// Colonoscopy secondary room scenes with their Unity build indices.
        /// </summary>
        /// <remarks>
        /// Values must match the build index in Unity's Build Settings.
        /// Indices 0-1 are reserved for core scenes (Startup, MainRoom).
        /// </remarks>
        public enum SecondaryRoomType
        {
            ModelEditor = 2,
            CoverageTraining = 3,
            PolypTraining = 4
        }

        #endregion

        #region Serialized Fields

        /// <summary>
        /// Teleport anchor for returning to the main room.
        /// </summary>
        [SerializeField]
        private TeleportationAnchor _mainRoomTeleportAnchor;

        /// <summary>
        /// Controller for the animated doors between rooms.
        /// </summary>
        [SerializeField]
        private PracticableDoorsController _doorsController;

        #endregion

        #region Events

        /// <summary>Fired when starting to enter a secondary room (scene loading).</summary>
        public event Action OnTransitionStarted;

        /// <summary>Fired when transition is complete and player can teleport.</summary>
        public event Action OnTransitionCompleted;

        /// <summary>Fired when starting to return to main room.</summary>
        public event Action OnReturnStarted;

        /// <summary>Fired when fully returned to main room.</summary>
        public event Action OnReturnCompleted;

        #endregion

        #region Properties

        /// <summary>
        /// Whether currently inside a secondary room.
        /// </summary>
        public bool IsInSecondaryRoom { get; private set; }

        /// <summary>
        /// Whether the secondary room is ready for teleportation.
        /// </summary>
        public bool IsSecondaryRoomReady => _secondaryRoomTeleportAnchor != null;

        /// <summary>
        /// The current secondary room type, if any.
        /// </summary>
        public SecondaryRoomType? CurrentRoomType { get; private set; }

        #endregion

        #region Private Fields

        private SceneFlowController _sceneFlowController;
        private TeleportationAnchor _secondaryRoomTeleportAnchor;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Validates that all required Inspector references are assigned.
        /// </summary>
        private void Awake()
        {
            ValidateReferences();
        }

        /// <summary>
        /// Initializes the controller by caching the SceneFlowController singleton
        /// and subscribing to scene lifecycle events.
        /// </summary>
        private void Start()
        {
            _sceneFlowController = SceneFlowController.Instance;
            if (_sceneFlowController == null)
            {
                Debug.LogError("[ColonoscopyRoomsTransitionController] SceneFlowController not found.");
                enabled = false;
                return;
            }

            SubscribeToEvents();
        }

        /// <summary>
        /// Cleans up event subscriptions to prevent memory leaks.
        /// </summary>
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Validates that required serialized references are assigned.
        /// Disables the component if any critical reference is missing.
        /// </summary>
        private void ValidateReferences()
        {
            if (_mainRoomTeleportAnchor == null)
            {
                Debug.LogError("[ColonoscopyRoomsTransitionController] MainRoomTeleportAnchor not assigned.");
                enabled = false;
            }

            if (_doorsController == null)
            {
                Debug.LogError("[ColonoscopyRoomsTransitionController] DoorsController not assigned.");
                enabled = false;
            }
        }

        #endregion

        #region Event Subscription

        /// <summary>
        /// Subscribes to SceneFlowController events for scene lifecycle management.
        /// </summary>
        private void SubscribeToEvents()
        {
            _sceneFlowController.OnSecondaryRoomSceneLoadedAction += ManageSecondaryRoomLoaded;
            _sceneFlowController.OnSecondaryRoomSceneUnloadedAction += ManageSecondaryRoomUnloaded;
            _sceneFlowController.OnTeleportDestinationChange += ManageTeleportDestinationReady;
            _sceneFlowController.OnExitSecondaryRoomRequested += ManageExitSecondaryRoomRequest;
        }

        /// <summary>
        /// Unsubscribes from all SceneFlowController events.
        /// Called on destroy to prevent memory leaks and null reference exceptions.
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (_sceneFlowController == null) return;

            _sceneFlowController.OnSecondaryRoomSceneLoadedAction -= ManageSecondaryRoomLoaded;
            _sceneFlowController.OnSecondaryRoomSceneUnloadedAction -= ManageSecondaryRoomUnloaded;
            _sceneFlowController.OnTeleportDestinationChange -= ManageTeleportDestinationReady;
            _sceneFlowController.OnExitSecondaryRoomRequested -= ManageExitSecondaryRoomRequest;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Enters a secondary room by its build index.
        /// </summary>
        /// <param name="sceneIndex">Build index of the scene to load (use SecondaryRoomType enum values).</param>
        /// <remarks>
        /// Valid scene indices for colonoscopy:
        /// - 2 = ModelEditor
        /// - 3 = CoverageTraining  
        /// - 4 = PolypTraining
        /// 
        /// After calling this method, wait for <see cref="OnTransitionCompleted"/> 
        /// before calling <see cref="TeleportToSecondaryRoom"/>.
        /// </remarks>
        public void EnterRoom(int sceneIndex)
        {
            if (IsInSecondaryRoom)
            {
                Debug.LogWarning("[ColonoscopyRoomsTransitionController] Already in secondary room.");
                return;
            }

            // Validate scene index is valid for colonoscopy rooms
            if (!Enum.IsDefined(typeof(SecondaryRoomType), sceneIndex))
            {
                Debug.LogError($"[ColonoscopyRoomsTransitionController] Invalid scene index: {sceneIndex}");
                return;
            }

            SecondaryRoomType roomType = (SecondaryRoomType)sceneIndex;
            CurrentRoomType = roomType;
            OnTransitionStarted?.Invoke();

            // Configure door button to trigger scene unload when pressed from inside
            _doorsController.SetDoorCallback(GetSceneUnloadAction(roomType));

            // Begin async scene load - ManageSecondaryRoomLoaded will be called when ready
            LoadScene(roomType);
        }

        /// <summary>
        /// Teleports the player into the secondary room and closes the doors behind them.
        /// </summary>
        /// <remarks>
        /// Call after <see cref="OnTransitionCompleted"/> fires or when 
        /// <see cref="IsSecondaryRoomReady"/> is true. The teleport anchor is provided
        /// by the secondary room's <see cref="XRTeleportDestinationProvider"/>.
        /// </remarks>
        public void TeleportToSecondaryRoom()
        {
            if (!IsSecondaryRoomReady)
            {
                Debug.LogWarning("[ColonoscopyRoomsTransitionController] Secondary room not ready for teleport.");
                return;
            }

            // Teleport player to secondary room anchor
            _secondaryRoomTeleportAnchor.RequestTeleport();
            
            // Close doors behind player (entering trigger will be disabled)
            _doorsController.CloseDoors();
            
            IsInSecondaryRoom = true;
        }

        /// <summary>
        /// Manually triggers return to main room.
        /// Usually called automatically via SceneFlowController events.
        /// </summary>
        public void ReturnToMainRoom()
        {
            ManageExitSecondaryRoomRequest();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Called when the secondary room scene finishes loading.
        /// Opens doors to allow player entry.
        /// </summary>
        private void ManageSecondaryRoomLoaded()
        {
            // Open doors toward the secondary room
            _doorsController.Entering();
        }

        /// <summary>
        /// Called when the secondary room scene is unloaded.
        /// Cleans up references and notifies listeners.
        /// </summary>
        private void ManageSecondaryRoomUnloaded()
        {
            Debug.Log("[ColonoscopyRoomsTransitionController] ManageSecondaryRoomUnloaded.");
            
            // Clear stale teleport reference
            _secondaryRoomTeleportAnchor = null;
            CurrentRoomType = null;
            
            // Notify listeners that return is complete
            OnReturnCompleted?.Invoke();
        }

        /// <summary>
        /// Called when the secondary room's teleport anchor is ready.
        /// Caches the anchor and notifies listeners that transition is complete.
        /// </summary>
        /// <param name="anchor">The teleport anchor from the secondary room.</param>
        private void ManageTeleportDestinationReady(TeleportationAnchor anchor)
        {
            Debug.Log("[ColonoscopyRoomsTransitionController] Secondary room teleport destination is ready.");
            _secondaryRoomTeleportAnchor = anchor;
            
            // Signal that player can now teleport into the secondary room
            OnTransitionCompleted?.Invoke();
        }

        /// <summary>
        /// Handles the exit request from a secondary room.
        /// Teleports player back, closes doors, and triggers scene unload.
        /// </summary>
        private void ManageExitSecondaryRoomRequest()
        {
            // Guard: ensure we're actually in a secondary room
            if (!IsInSecondaryRoom && _secondaryRoomTeleportAnchor == null)
            {
                Debug.LogWarning("[ColonoscopyRoomsTransitionController] Not in secondary room.");
                return;
            }

            // Notify listeners that return sequence is starting
            OnReturnStarted?.Invoke();

            // Step 1: Teleport player back to main room before unloading
            _mainRoomTeleportAnchor.RequestTeleport();
            
            // Step 2: Clean up door state (remove callback, close doors)
            _doorsController.ClearDoorCallback();
            _doorsController.CloseDoors();

            // Step 3: Unload the secondary scene (will trigger ManageSecondaryRoomUnloaded)
            _sceneFlowController.UnloadLastLoadedScene();

            IsInSecondaryRoom = false;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Loads the appropriate scene based on room type using generic scene loading.
        /// </summary>
        /// <param name="roomType">The type of secondary room to load.</param>
        private void LoadScene(SecondaryRoomType roomType)
        {
            // Use generic int-based scene loading
            _sceneFlowController.LoadSceneByIndex((int)roomType, useAdditiveLoad: true, useLoadingModal: true);
        }

        /// <summary>
        /// Gets the appropriate scene unload action for a room type.
        /// Used as the door button callback to unload the scene when exiting.
        /// </summary>
        /// <param name="roomType">The type of secondary room.</param>
        /// <returns>Action that unloads the corresponding scene.</returns>
        private Action GetSceneUnloadAction(SecondaryRoomType roomType)
        {
            // Capture the scene index to unload
            int sceneIndex = (int)roomType;
            return () => _sceneFlowController.UnloadSceneByIndex(sceneIndex, useLoadingModal: true);
        }

        #endregion
    }
}
