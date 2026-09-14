// ============================================================================
// ISecondaryRoomTransitionController.cs
//
// Interface for controllers that manage transitions between main room and
// organ-specific secondary rooms (training, model editor).
//
// Purpose:
//   - Abstracts scene loading/unloading for different organs
//   - Enables dependency injection in MainRoomManager
//   - Supports multiple organ types without modifying interface
//
// Implementations:
//   - LargeIntestineRoomsTransitionController: Large intestine rooms
//   - StomachTransitionController: Stomach/gastroscopy rooms
//   - (Future organs...)
//
// Usage Pattern:
//   1. Call EnterRoom(sceneIndex) → scene loads, doors open
//   2. Wait for OnTransitionCompleted event
//   3. Call TeleportToSecondaryRoom() → player enters, doors close
//   4. Exit via ReturnToMainRoom() or door button
//
// Scene Index Convention:
//   - Each organ controller defines its own scene enum with build indices
//   - Callers pass (int)OrgansEnum.SceneName to EnterRoom
// ============================================================================

using System;

namespace MainRoom
{
    /// <summary>
    /// Interface for controllers managing transitions to organ-specific secondary rooms.
    /// Implementations handle scene loading, teleportation, and door animations.
    /// </summary>
    public interface ISecondaryRoomTransitionController
    {
        #region Events

        /// <summary>Fired when starting to enter a secondary room (scene loading begins).</summary>
        event Action OnTransitionStarted;

        /// <summary>Fired when secondary room is loaded and ready for teleportation.</summary>
        event Action OnTransitionCompleted;

        /// <summary>Fired when starting to return to main room (before scene unload).</summary>
        event Action OnReturnStarted;

        /// <summary>Fired when fully returned to main room (scene unloaded).</summary>
        event Action OnReturnCompleted;

        #endregion

        #region Properties

        /// <summary>
        /// Whether currently inside a secondary room.
        /// </summary>
        bool IsInSecondaryRoom { get; }

        /// <summary>
        /// Whether the secondary room's teleport destination is ready.
        /// Check this before calling TeleportToSecondaryRoom().
        /// </summary>
        bool IsSecondaryRoomReady { get; }

        #endregion

        #region Room Entry Methods

        /// <summary>
        /// Enters a secondary room by its build index.
        /// </summary>
        /// <param name="sceneIndex">Build index of the scene to load.</param>
        /// <remarks>
        /// Wait for OnTransitionCompleted before calling TeleportToSecondaryRoom().
        /// 
        /// Each organ controller defines its own scene enum with build indices.
        /// Callers should use: EnterRoom((int)LargeIntestineRoomsTransitionController.SecondaryRoomType.ModelEditor)
        /// </remarks>
        void EnterRoom(int sceneIndex);

        #endregion

        #region Teleportation Methods

        /// <summary>
        /// Teleports the player into the secondary room and closes doors.
        /// Call after OnTransitionCompleted fires or when IsSecondaryRoomReady is true.
        /// </summary>
        void TeleportToSecondaryRoom();

        /// <summary>
        /// Returns to the main room, unloading the secondary scene.
        /// Usually called automatically via door button or scene events.
        /// </summary>
        void ReturnToMainRoom();

        #endregion
    }
}
