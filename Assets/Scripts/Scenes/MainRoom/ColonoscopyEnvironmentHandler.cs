// ============================================================================
// ColonoscopyEnvironmentHandler.cs
//
// Environment handler for colonoscopy/large intestine training environment.
// Manages LI-specific UI events, scene transitions, and training modes.
//
// Responsibilities:
//   - Subscribe/unsubscribe to colonoscopy-specific UI events
//   - Start coverage training and polyp detection training
//   - Create/edit models using LI model editor
//   - Handle model selection for LI-specific training requirements
//
// Scene Indices (from ColonoscopyRoomsTransitionController):
//   - ModelEditor = 2
//   - CoverageTraining = 3
//   - PolypTraining = 4
//
// Dependencies:
//   - ColonoscopyRoomsTransitionController (sibling or child component)
//   - UIController with colonoscopy-specific buttons
// ============================================================================

using System;
using UnityEngine;

namespace MainRoom
{
    /// <summary>
    /// Environment handler for colonoscopy/large intestine training.
    /// Manages LI-specific UI events, trainings, and model editing.
    /// </summary>
    public class ColonoscopyEnvironmentHandler : MonoBehaviour, IEnvironmentHandler
    {
        #region Serialized Fields

        /// <summary>
        /// GameObject containing the LI transition controller.
        /// </summary>
        [SerializeField]
        private GameObject _transitionControllerObject;

        #endregion

        #region Private Fields

        private ApplicationManager _applicationManager;
        private UIController _uiController;
        private ISecondaryRoomTransitionController _transitionController;

        #endregion

        #region Properties

        /// <summary>
        /// The transition controller for colonoscopy secondary rooms.
        /// </summary>
        public ISecondaryRoomTransitionController TransitionController => _transitionController;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Validates serialized references on awake.
        /// </summary>
        private void Awake()
        {
            ValidateReferences();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Validates that required serialized references are assigned.
        /// </summary>
        private void ValidateReferences()
        {
            if (_transitionControllerObject == null)
            {
                Debug.LogError("[ColonoscopyEnvironmentHandler] TransitionControllerObject not assigned.");
                enabled = false;
                return;
            }

            _transitionController = _transitionControllerObject.GetComponent<ISecondaryRoomTransitionController>();
            if (_transitionController == null)
            {
                Debug.LogError("[ColonoscopyEnvironmentHandler] TransitionControllerObject must have ISecondaryRoomTransitionController.");
                enabled = false;
            }
        }

        /// <summary>
        /// Initializes the handler with required dependencies.
        /// </summary>
        /// <param name="applicationManager">Application manager for model state.</param>
        /// <param name="uiController">UI controller for colonoscopy environment.</param>
        public void Initialize(ApplicationManager applicationManager, UIController uiController)
        {
            _applicationManager = applicationManager;
            _uiController = uiController;

            if (_applicationManager == null)
            {
                Debug.LogError("[ColonoscopyEnvironmentHandler] ApplicationManager is null.");
                enabled = false;
            }

            if (_uiController == null)
            {
                Debug.LogError("[ColonoscopyEnvironmentHandler] UIController is null.");
                enabled = false;
            }
        }

        #endregion

        #region Event Subscription

        /// <summary>
        /// Subscribes to colonoscopy-specific UI events.
        /// </summary>
        public void SubscribeToUIEvents()
        {
            // Training events
            _uiController.OnCoverageTrainingButtonPressed += StartCoverageTraining;
            _uiController.OnPolypDetectionButtonPressed += StartPolypTraining;

            // Model editing events
            _uiController.OnNewModelButtonPressed += CreateModel;
            _uiController.OnEditModelButtonPressed += EditModel;
        }

        /// <summary>
        /// Unsubscribes from all colonoscopy-specific UI events.
        /// </summary>
        public void UnsubscribeFromUIEvents()
        {
            if (_uiController == null) return;

            _uiController.OnCoverageTrainingButtonPressed -= StartCoverageTraining;
            _uiController.OnPolypDetectionButtonPressed -= StartPolypTraining;
            _uiController.OnNewModelButtonPressed -= CreateModel;
            _uiController.OnEditModelButtonPressed -= EditModel;
        }

        /// <summary>
        /// Subscribes to transition controller events.
        /// </summary>
        /// <param name="onReturnStarted">Callback for when return from secondary room starts.</param>
        public void SubscribeToTransitionEvents(Action onReturnStarted)
        {
            if (_transitionController != null)
            {
                _transitionController.OnReturnStarted += onReturnStarted;
            }
        }

        /// <summary>
        /// Unsubscribes from transition controller events.
        /// </summary>
        /// <param name="onReturnStarted">Callback to unsubscribe.</param>
        public void UnsubscribeFromTransitionEvents(Action onReturnStarted)
        {
            if (_transitionController != null)
            {
                _transitionController.OnReturnStarted -= onReturnStarted;
            }
        }

        #endregion

        #region Training Actions

        /// <summary>
        /// Starts coverage training with the selected model.
        /// </summary>
        private void StartCoverageTraining()
        {
            _applicationManager.SetCurrentModel(_uiController.GetSelectedModel());
            _transitionController.EnterRoom((int)ColonoscopyRoomsTransitionController.SecondaryRoomType.CoverageTraining);
            _uiController.SwitchToRoomOccupiedWindow();
        }

        /// <summary>
        /// Starts polyp detection training with the selected model.
        /// </summary>
        private void StartPolypTraining()
        {
            _applicationManager.SetCurrentModel(_uiController.GetSelectedModel());
            _transitionController.EnterRoom((int)ColonoscopyRoomsTransitionController.SecondaryRoomType.PolypTraining);
            _uiController.SwitchToRoomOccupiedWindow();
        }

        #endregion

        #region Model Editing Actions

        /// <summary>
        /// Opens the model editor to create a new model.
        /// </summary>
        private void CreateModel()
        {
            _applicationManager.ClearCurrentModel();
            _transitionController.EnterRoom((int)ColonoscopyRoomsTransitionController.SecondaryRoomType.ModelEditor);
            _uiController.SwitchToRoomOccupiedWindow();
        }

        /// <summary>
        /// Opens the model editor to edit the selected model.
        /// </summary>
        private void EditModel()
        {
            _applicationManager.SetCurrentModel(_uiController.GetSelectedModel());
            _transitionController.EnterRoom((int)ColonoscopyRoomsTransitionController.SecondaryRoomType.ModelEditor);
            _uiController.SwitchToRoomOccupiedWindow();
        }

        #endregion

        #region Model Selection Handling

        /// <summary>
        /// Handles model selection changes for colonoscopy environment.
        /// Updates training button states based on model capabilities.
        /// </summary>
        /// <param name="isTrainingWindowActive">Whether the training window is active.</param>
        /// <param name="wasDeselected">Whether the model was deselected.</param>
        public void HandleModelSelectionChanged(bool isTrainingWindowActive, bool wasDeselected)
        {
            if (isTrainingWindowActive)
            {
                UpdateTrainingButtonStates(wasDeselected);
            }
            else
            {
                _uiController.SetModelEditingSelectionButtonsInteractable(!wasDeselected);
            }
        }

        /// <summary>
        /// Updates training button interactability based on selection state.
        /// Coverage training always available, polyp detection requires mesh.
        /// </summary>
        /// <param name="wasDeselected">Whether the model was deselected.</param>
        private void UpdateTrainingButtonStates(bool wasDeselected)
        {
            if (wasDeselected)
            {
                _uiController.SetTrainingSelectionButtonsInteractable(false);
            }
            else
            {
                bool hasValidMesh = _uiController.GetSelectedModel()?.HasMesh ?? false;
                _uiController.SetCoverageTrainingButtonInteractable(true);
                _uiController.SetPolypDetectionButtonInteractable(hasValidMesh);
            }
        }

        #endregion
    }
}
