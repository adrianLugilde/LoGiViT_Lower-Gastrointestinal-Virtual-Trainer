// ============================================================================
// InputController.cs
//
// Manages XR input and locomotion settings for the colonoscopy room.
// Controls snap turn, continuous turn, and dynamic movement providers.
//
// Usage:
//   - Access via ColonoscopyRoomManager.InputController
//   - ToggleHMDLock(enabled) - Enable/disable all locomotion and head tracking
//   - SetCharacterLocomotionEnabled(enabled) - Enable/disable all movement
//   - SetCharacterTurnEnabled(enabled) - Enable/disable rotation only
//
// Dependencies:
//   - SnapTurnProvider, ContinuousTurnProvider, DynamicMoveProvider (XR Toolkit)
//   - TrackedPoseDriver (for HMD tracking)
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace MainRoom
{
    /// <summary>
    /// Controls XR input and locomotion for the colonoscopy room.
    /// Manages turn providers, movement, and HMD tracking state.
    /// </summary>
    public class InputController : MonoBehaviour
    {
        #region Serialized Fields

        /// <summary>
        /// Provider for snap (discrete) turning.
        /// </summary>
        [SerializeField]
        private SnapTurnProvider _snapTurnProvider;

        /// <summary>
        /// Provider for smooth continuous turning.
        /// </summary>
        [SerializeField]
        private ContinuousTurnProvider _continuousTurnProvider;

        /// <summary>
        /// Provider for dynamic movement (walk/teleport).
        /// </summary>
        [SerializeField]
        private DynamicMoveProvider _dynamicMoveProvider;

        /// <summary>
        /// Driver for HMD position/rotation tracking.
        /// </summary>
        [SerializeField]
        private TrackedPoseDriver _trackedPoseDriver;
        [SerializeField] private InputActionReference _leftSelectAction;
        [SerializeField] private InputActionReference _rightSelectAction;

        #endregion

        #region Public Methods - Locomotion Control

        /// <summary>
        /// Enables or disables all character locomotion (turn + movement).
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        public void SetCharacterLocomotionEnabled(bool enabled)
        {
            SetSnapTurnEnabled(enabled);
            SetContinuousTurnEnabled(enabled);
            SetDynamicMoveEnabled(enabled);
        }

        /// <summary>
        /// Enables or disables character turning only (snap and continuous).
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        public void SetCharacterTurnEnabled(bool enabled)
        {
            SetSnapTurnEnabled(enabled);
            SetContinuousTurnEnabled(enabled);
        }

        /// <summary>
        /// Toggles HMD lock state. When locked, disables locomotion and head tracking.
        /// Used for desktop mode or when user should not move.
        /// </summary>
        /// <param name="enabled">True to enable tracking/locomotion, false to lock.</param>
        public void ToggleHMDLock(bool enabled)
        {
            SetCharacterLocomotionEnabled(enabled);

            if (_trackedPoseDriver != null)
            {
                _trackedPoseDriver.enabled = enabled;
            }
        }

        #endregion

        #region Public Methods - Keybind Profile

        public void SetUseGrabKeybindProfiles(bool useGrab)
        {
            if (_leftSelectAction != null) ApplySelectBindingMask(_leftSelectAction.action, useGrab);
            if (_rightSelectAction != null) ApplySelectBindingMask(_rightSelectAction.action, useGrab);
        }

        private void ApplySelectBindingMask(InputAction selectAction, bool useGrab)
        {
            if (useGrab)
            {
                selectAction.ApplyBindingOverride(0, string.Empty); // disable primaryButton
                selectAction.RemoveBindingOverride(1);               // enable gripButton
            }
            else
            {
                selectAction.RemoveBindingOverride(0);               // enable primaryButton
                selectAction.ApplyBindingOverride(1, string.Empty); // disable gripButton
            }
        }

        #endregion

        #region Public Methods - Individual Provider Control

        /// <summary>
        /// Enables or disables snap turn provider.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        public void SetSnapTurnEnabled(bool enabled)
        {
            if (_snapTurnProvider != null)
            {
                _snapTurnProvider.enabled = enabled;
            }
        }

        /// <summary>
        /// Enables or disables continuous turn provider.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        public void SetContinuousTurnEnabled(bool enabled)
        {
            if (_continuousTurnProvider != null)
            {
                _continuousTurnProvider.enabled = enabled;
            }
        }

        /// <summary>
        /// Enables or disables dynamic move provider.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        public void SetDynamicMoveEnabled(bool enabled)
        {
            if (_dynamicMoveProvider != null)
            {
                _dynamicMoveProvider.enabled = enabled;
            }
        }

        #endregion
    }
}
