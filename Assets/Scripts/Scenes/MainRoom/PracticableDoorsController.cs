// ============================================================================
// PracticableDoorsController.cs
//
// Controls animated doors between the main room and secondary rooms.
// Handles door opening/closing animations, trigger colliders, and XR button.
//
// Door Flow (Entering secondary room):
//   1. Entering() called → doors open toward secondary room
//   2. Player walks through → entering trigger fires → CloseDoors()
//   3. Doors close behind player
//
// Door Flow (Leaving secondary room):
//   1. Player presses door button → Leaving() + OnDoorButtonPressed
//   2. Doors open toward main room
//   3. Player walks through → leaving trigger fires → CloseDoors()
//
// Public API:
//   - Entering() - Open doors for entering secondary room
//   - Leaving() - Open doors for leaving secondary room  
//   - CloseDoors() - Close doors (auto-detects animation direction)
//   - SetDoorCallback(action) - Set action for door button press
//   - ClearDoorCallback() - Remove door button callback
//
// Animation States (must match Animator controller):
//   EnterOpening → EnterOpened → EnterClosing
//   LeaveOpening → LeaveOpened → LeaveClosing
//
// Configuration (Inspector):
//   - _doorButtonInteractable: XR button to request exit
//   - _animator: Animator with door states
//   - _enteringCollider: Trigger on secondary room side
//   - _leavingCollider: Trigger on main room side
//
// Dependencies:
//   - DoorTriggerZone on each collider GameObject
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Controls the animated doors between the preparation room and training rooms.
/// Manages door animations, colliders, and XR button interactions.
/// </summary>
public class PracticableDoorsController : MonoBehaviour
{
    #region Constants

    // Animation state names - must match Animator controller
    private const string ANIM_ENTER_OPENING = "EnterOpening";
    private const string ANIM_ENTER_OPENED = "EnterOpened";
    private const string ANIM_ENTER_CLOSING = "EnterClosing";
    private const string ANIM_LEAVE_OPENING = "LeaveOpening";
    private const string ANIM_LEAVE_OPENED = "LeaveOpened";
    private const string ANIM_LEAVE_CLOSING = "LeaveClosing";

    // Tags
    private const string TAG_PLAYER = "Player";

    #endregion

    #region Serialized Fields

    /// <summary>
    /// XR interactable for the door button.
    /// </summary>
    [SerializeField]
    private XRSimpleInteractable _doorButtonInteractable;

    /// <summary>
    /// Animator controlling door animations.
    /// </summary>
    [SerializeField] 
    private Animator _animator;

    /// <summary>
    /// Collider that triggers door close when entering.
    /// </summary>
    [SerializeField] 
    private BoxCollider _enteringCollider;

    /// <summary>
    /// Collider that triggers door close when leaving.
    /// </summary>
    [SerializeField] 
    private BoxCollider _leavingCollider;

    #endregion

    #region Events

    /// <summary>
    /// Fired when the door button is pressed.
    /// Subscribers should handle scene unloading or other exit actions.
    /// </summary>
    public event Action OnDoorButtonPressed;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Subscribes to the door button interaction event.
    /// </summary>
    private void Awake()
    {
        if (_doorButtonInteractable != null)
        {
            _doorButtonInteractable.selectEntered.AddListener(HandleDoorButtonSelect);
        }
        else
        {
            Debug.LogWarning("[PracticableDoorsController] Door button interactable not assigned.");
        }
    }

    /// <summary>
    /// Cleans up event listeners to prevent memory leaks.
    /// </summary>
    private void OnDestroy()
    {
        if (_doorButtonInteractable != null)
        {
            _doorButtonInteractable.selectEntered.RemoveListener(HandleDoorButtonSelect);
        }
    }

    /// <summary>
    /// Called by <see cref="DoorTriggerZone"/> when the player enters a trigger.
    /// Only closes doors if the triggering collider is currently enabled.
    /// </summary>
    /// <param name="triggerCollider">The collider that detected the player.</param>
    /// <remarks>
    /// This two-step validation (trigger zone reports → controller validates) ensures
    /// that disabled colliders don't accidentally trigger door closure.
    /// </remarks>
    public void OnPlayerEnteredTrigger(BoxCollider triggerCollider)
    {
        // Validate: only respond to our managed colliders when they're enabled
        if (triggerCollider == _enteringCollider && _enteringCollider.enabled)
        {
            Debug.Log("[PracticableDoorsController] Player entered ENTERING trigger - closing doors.");
            CloseDoors();
        }
        else if (triggerCollider == _leavingCollider && _leavingCollider.enabled)
        {
            Debug.Log("[PracticableDoorsController] Player entered LEAVING trigger - closing doors.");
            CloseDoors();
        }
        // Ignore triggers from disabled colliders (shouldn't happen, but defensive)
    }

    #endregion

    #region Public Methods - Door Control

    /// <summary>
    /// Opens doors for entering a secondary room (from main room).
    /// Enables the entering collider so doors close after player passes through.
    /// </summary>
    public void Entering()
    {
        // Play animation: doors swing open toward secondary room
        _animator.Play(ANIM_ENTER_OPENING);
        
        // Enable entering trigger (secondary room side), disable leaving trigger
        SetColliders(isEntering: true);
    }

    /// <summary>
    /// Opens doors for leaving a secondary room (back to main room).
    /// Enables the leaving collider so doors close after player passes through.
    /// </summary>
    public void Leaving()
    {
        Debug.Log("[PracticableDoorsController] Leaving - playing leave opening animation.");
        
        // Play animation: doors swing open toward main room
        _animator.Play(ANIM_LEAVE_OPENING);
        
        // Enable leaving trigger (main room side), disable entering trigger
        SetColliders(isEntering: false);
    }

    /// <summary>
    /// Closes the doors based on current animation state.
    /// Automatically selects the correct closing animation (enter vs leave).
    /// </summary>
    /// <remarks>
    /// Called automatically by <see cref="DoorTriggerZone"/> when player 
    /// passes through, or manually by <see cref="SecondaryRoomTransitionController"/>.
    /// </remarks>
    public void CloseDoors()
    {
        var currentState = _animator.GetCurrentAnimatorStateInfo(0);
        Debug.Log("[PracticableDoorsController] CloseDoors - current state: " + currentState.fullPathHash);
        
        // Determine which closing animation to play based on current open state
        if (currentState.IsName(ANIM_ENTER_OPENED))
        {
            _animator.Play(ANIM_ENTER_CLOSING);
        }
        else if (currentState.IsName(ANIM_LEAVE_OPENED))
        {
            _animator.Play(ANIM_LEAVE_CLOSING);
        }

        // Disable both trigger colliders to prevent re-triggering
        _enteringCollider.enabled = false;
        _leavingCollider.enabled = false;
    }

    #endregion

    #region Public Methods - Callback Management

    /// <summary>
    /// Sets the callback to execute when the door button is pressed.
    /// Clears any existing callback before setting the new one.
    /// </summary>
    /// <param name="callback">The action to execute on door button press.</param>
    public void SetDoorCallback(Action callback)
    {
        // Clear existing subscribers to ensure only one callback
        OnDoorButtonPressed = null;
        OnDoorButtonPressed += callback;
    }

    /// <summary>
    /// Clears the door button callback.
    /// </summary>
    public void ClearDoorCallback()
    {
        OnDoorButtonPressed = null;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Handles the door button being selected via XR interaction.
    /// </summary>
    private void HandleDoorButtonSelect(SelectEnterEventArgs args)
    {
        Debug.Log("[PracticableDoorsController] HandleDoorButtonSelect.");
        Leaving();
        OnDoorButtonPressed?.Invoke();
    }

    /// <summary>
    /// Configures collider states based on door direction.
    /// </summary>
    /// <param name="isEntering">True if entering training room, false if leaving.</param>
    private void SetColliders(bool isEntering)
    {
        _enteringCollider.enabled = isEntering;
        _leavingCollider.enabled = !isEntering;
    }

    #endregion
}
