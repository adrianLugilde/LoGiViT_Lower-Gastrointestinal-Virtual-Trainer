// ============================================================================
// XRTeleportDestinationProvider.cs
//
// Provides a teleport destination anchor for XR scene transitions. This
// component decouples teleportation logic from scene managers, allowing
// flexible configuration via the Inspector.
//
// Usage:
//   1. Attach to any GameObject in a secondary room scene
//   2. Assign the TeleportationAnchor in the Inspector
//   3. Configure auto-notification:
//      - AutoNotifyOnStart = true: Fires event automatically in Start()
//      - AutoNotifyOnStart = false: Call NotifyReady() manually when ready
//
// Configuration (Inspector):
//   - _teleportAnchor: The TeleportationAnchor destination for this room
//   - _autoNotifyOnStart: Whether to automatically notify on Start
//
// Events:
//   - DestinationReady: Fired when teleport destination is available
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace CustomUI
{
    /// <summary>
    /// Provides a teleport destination anchor for XR scene transitions.
    /// Can be configured to auto-notify or require manual notification.
    /// </summary>
    public class XRTeleportDestinationProvider : MonoBehaviour
    {
        #region Serialized Fields

        /// <summary>
        /// The teleportation anchor that serves as the destination for this room.
        /// </summary>
        [SerializeField]
        [Tooltip("The teleportation anchor destination for this room.")]
        private TeleportationAnchor _teleportAnchor;

        /// <summary>
        /// If true, automatically fires DestinationReady in Start().
        /// If false, call NotifyReady() manually when the scene is ready.
        /// </summary>
        [SerializeField]
        [Tooltip("If true, automatically notifies on Start. If false, call NotifyReady() manually.")]
        private bool _autoNotifyOnStart = true;

        #endregion

        #region Public Properties

        /// <summary>
        /// The teleportation anchor for this room. Can be null if not assigned.
        /// </summary>
        public TeleportationAnchor TeleportAnchor => _teleportAnchor;

        /// <summary>
        /// Whether the teleport destination has been notified as ready.
        /// </summary>
        public bool IsReady { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Invoked when the teleport destination is ready for use.
        /// Subscribers receive the TeleportationAnchor reference.
        /// </summary>
        public event Action<TeleportationAnchor> DestinationReady;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Validates the teleport anchor assignment.
        /// </summary>
        private void Awake()
        {
            if (_teleportAnchor == null)
            {
                Debug.LogError($"[XRTeleportDestinationProvider] TeleportAnchor not assigned on {gameObject.name}.");
            }
        }

        /// <summary>
        /// Automatically notifies if configured to do so.
        /// </summary>
        private void Start()
        {
            if (_autoNotifyOnStart)
            {
                NotifyReady();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Notifies subscribers that the teleport destination is ready.
        /// Call this manually when AutoNotifyOnStart is false.
        /// </summary>
        /// <remarks>
        /// Safe to call multiple times - only fires event on first call.
        /// </remarks>
        public void NotifyReady()
        {
            if (IsReady)
            {
                Debug.LogWarning("[XRTeleportDestinationProvider] NotifyReady() called but already ready.");
                return;
            }

            if (_teleportAnchor == null)
            {
                Debug.LogError("[XRTeleportDestinationProvider] Cannot notify - TeleportAnchor is null.");
                return;
            }

            IsReady = true;
            Debug.Log("[XRTeleportDestinationProvider] Teleport destination is ready.");
            DestinationReady?.Invoke(_teleportAnchor);
        }

        /// <summary>
        /// Resets the ready state. Useful if the scene needs to re-initialize.
        /// </summary>
        public void ResetReadyState()
        {
            IsReady = false;
        }

        #endregion
    }
}
