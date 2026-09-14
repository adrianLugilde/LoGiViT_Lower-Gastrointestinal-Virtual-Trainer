// ============================================================================
// IEnvironmentHandler.cs
//
// Interface for organ-specific environment handlers in the main room.
// Handles organ-specific UI events, scene transitions, and training modes.
//
// Purpose:
//   - Abstracts organ-specific logic from MainRoomManager
//   - Enables swapping environments without modifying MainRoomManager
//   - Each organ (colonoscopy, gastroscopy, etc.) has its own handler
//
// Implementations:
//   - ColonoscopyEnvironmentHandler: Large intestine/colonoscopy environment
//   - (Future: GastroscopyEnvironmentHandler, etc.)
//
// Usage Pattern:
//   1. MainRoomManager gets IEnvironmentHandler from serialized GameObject
//   2. Calls Initialize() to set up dependencies
//   3. Calls SubscribeToUIEvents() during setup
//   4. Handler manages all organ-specific interactions
// ============================================================================

using System;

namespace MainRoom
{
    /// <summary>
    /// Interface for organ-specific environment handlers.
    /// Implementations handle organ-specific UI events, trainings, and scene transitions.
    /// </summary>
    public interface IEnvironmentHandler
    {
        #region Properties

        /// <summary>
        /// The transition controller for this environment's secondary rooms.
        /// </summary>
        ISecondaryRoomTransitionController TransitionController { get; }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the handler with required dependencies.
        /// </summary>
        /// <param name="applicationManager">Application manager for model state.</param>
        /// <param name="uiController">UI controller for this environment.</param>
        void Initialize(ApplicationManager applicationManager, UIController uiController);

        #endregion

        #region Event Subscription

        /// <summary>
        /// Subscribes to organ-specific UI events.
        /// Called by MainRoomManager during setup.
        /// </summary>
        void SubscribeToUIEvents();

        /// <summary>
        /// Unsubscribes from all organ-specific UI events.
        /// Called by MainRoomManager during cleanup.
        /// </summary>
        void UnsubscribeFromUIEvents();

        /// <summary>
        /// Subscribes to transition controller events.
        /// </summary>
        /// <param name="onReturnStarted">Callback for when return from secondary room starts.</param>
        void SubscribeToTransitionEvents(Action onReturnStarted);

        /// <summary>
        /// Unsubscribes from transition controller events.
        /// </summary>
        /// <param name="onReturnStarted">Callback to unsubscribe.</param>
        void UnsubscribeFromTransitionEvents(Action onReturnStarted);

        #endregion

        #region Model Selection Handling

        /// <summary>
        /// Handles model element selection based on environment-specific rules.
        /// Updates button states for trainings or model editing.
        /// </summary>
        /// <param name="isTrainingWindowActive">Whether the training window is currently active.</param>
        /// <param name="wasDeselected">Whether the element was deselected (toggled off).</param>
        void HandleModelSelectionChanged(bool isTrainingWindowActive, bool wasDeselected);

        #endregion
    }
}
