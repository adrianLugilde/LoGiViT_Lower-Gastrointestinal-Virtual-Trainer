// ============================================================================
// DoorTriggerZone.cs
//
// Trigger forwarder for door colliders. Attach to each door trigger zone
// (entering/leaving) to forward player trigger events to the doors controller.
//
// Why this exists:
//   Unity's OnTriggerEnter on a parent doesn't indicate which child collider
//   was triggered. This component sits on each trigger zone and forwards
//   the event with collider identity to PracticableDoorsController.
//
// Setup:
//   1. Create two child GameObjects under the doors (entering zone, leaving zone)
//   2. Add BoxCollider (Is Trigger = true) to each
//   3. Add this component to each
//   4. Assign _doorsController reference to the parent PracticableDoorsController
//   5. In PracticableDoorsController, assign these BoxColliders to the fields
//
// Dependencies:
//   - PracticableDoorsController (assigned in Inspector)
//   - BoxCollider on same GameObject (auto-detected)
// ============================================================================

using UnityEngine;

/// <summary>
/// Forwards player trigger events to <see cref="PracticableDoorsController"/>.
/// Required on each door trigger zone to identify which collider was entered.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DoorTriggerZone : MonoBehaviour
{
    #region Constants

    private const string TAG_PLAYER = "Player";

    #endregion

    #region Serialized Fields

    /// <summary>
    /// Reference to the doors controller that manages this trigger zone.
    /// </summary>
    [SerializeField]
    private PracticableDoorsController _doorsController;

    #endregion

    #region Private Fields

    /// <summary>
    /// Cached reference to this GameObject's BoxCollider.
    /// Passed to the controller to identify which zone was triggered.
    /// </summary>
    private BoxCollider _collider;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Caches the BoxCollider reference and validates configuration.
    /// </summary>
    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        
        if (_doorsController == null)
        {
            Debug.LogError($"[DoorTriggerZone] DoorsController not assigned on {gameObject.name}.");
            enabled = false;
        }
    }

    /// <summary>
    /// Forwards player trigger events to the doors controller.
    /// The controller will validate if this collider is currently active.
    /// </summary>
    /// <param name="other">The collider that entered the trigger.</param>
    private void OnTriggerEnter(Collider other)
    {
        // Only respond to player (XR rig or character controller)
        if (!other.CompareTag(TAG_PLAYER)) return;
        
        Debug.Log($"[DoorTriggerZone] Player entered trigger: {gameObject.name}, collider enabled: {_collider.enabled}");
        
        // Forward to controller with collider identity for validation
        _doorsController.OnPlayerEnteredTrigger(_collider);
    }

    #endregion
}
