// ============================================================================
// ModelRotationHandleController.cs
//
// Enforces mutual exclusion between the model rotation handle and spline node
// interactables across all edition modes.
//
// Key Features:
//   - While the rotation handle is grabbed: node interaction is disabled so nodes
//     cannot be grabbed (nodes remain visible), and the reset button is disabled.
//   - While any spline node is grabbed: the rotation handle XRGrabInteractable
//     is disabled and the reset button is disabled.
//   - Always active — not tied to any specific edition mode. Mutual exclusion
//     is naturally inert in modes where nodes are hidden (no grabs can occur).
//
// Dependencies:
//   - ISplineNodesInteractablesController: for enabling/disabling node interaction
//     and receiving OnAnyNodeGrabStart / OnAnyNodeGrabEnd events
//   - XRGrabInteractable: the rotation source object grabbed by the user
//   - TransformRotationFollower: drives the model rotation (serialized for scene
//     wiring; not called directly by this controller)
//   - ResponsiveInteractiveElement: the reset-rotation button to enable/disable
//
// Usage:
//   1. Add as a component anywhere under the ModelEditorManager GameObject.
//   2. Assign the three serialized fields in the Inspector.
//   3. ModelEditorManager calls Initialize() during ConfigureControllers().
// ============================================================================

using System;
using CustomUI;
using LoGiViT.ModelEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ModelEditor
{
    /// <summary>
    /// Enforces mutual exclusion between the model rotation handle and spline
    /// node interactables. Always active — not tied to any specific edition mode.
    /// </summary>
    public class ModelRotationHandleController : MonoBehaviour
    {
        #region Serialized Fields

        [Tooltip("XRGrabInteractable on the rotation source object grabbed by the user.")]
        [SerializeField] private XRGrabInteractable _rotationSourceInteractable;

        [Tooltip("Drives the model rotation. Serialized here so the scene wires the " +
                 "relationship; RestoreRotation is called directly by the reset button's UnityEvent.")]
        [SerializeField] private XRGrabRotationFollower _xrGrabRotationFollower;

        [Tooltip("The UI reset-rotation button. Blocked while any grab (handle or node) is active.")]
        [SerializeField] private ResponsiveInteractiveElement _resetButton;

        #endregion

        #region Private Fields

        private ISplineNodesInteractablesController _nodesController;

        public event Action OnRotationStarted;
        public event Action OnRotationReleased;
        public event Action OnRotationRestored;

        #endregion

        #region Initialization

        /// <summary>
        /// Wires all event subscriptions. Called by the scene manager during controller configuration.
        /// </summary>
        /// <param name="nodesController">
        /// The spline node interactables controller, or <c>null</c> in scenes without spline nodes
        /// (e.g. Coverage/Polyp Training). When null, node mutual-exclusion is skipped and the
        /// rotation handle is always interactable.
        /// </param>
        public void Initialize(ISplineNodesInteractablesController nodesController = null)
        {
            _nodesController = nodesController;

            _rotationSourceInteractable.selectEntered.AddListener(OnRotationGrabStart);
            _rotationSourceInteractable.selectExited.AddListener(OnRotationGrabEnd);

            if (_nodesController != null)
            {
                _nodesController.OnAnyNodeGrabStart += OnNodeGrabStart;
                _nodesController.OnAnyNodeGrabEnd += OnNodeGrabEnd;
            }

            _resetButton.OnSelected += OnResetButtonClicked;
        }

        private void OnDestroy()
        {
            _rotationSourceInteractable.selectEntered.RemoveListener(OnRotationGrabStart);
            _rotationSourceInteractable.selectExited.RemoveListener(OnRotationGrabEnd);

            if (_nodesController != null)
            {
                _nodesController.OnAnyNodeGrabStart -= OnNodeGrabStart;
                _nodesController.OnAnyNodeGrabEnd -= OnNodeGrabEnd;
            }
        }

        #endregion

        #region Reset Button Handlers
        /// <summary>
        /// Called when the rotation reset button is clicked.
        /// Restores the model's original rotation via the TransformRotationFollower.
        /// </summary>
        private void OnResetButtonClicked()
        {
            _xrGrabRotationFollower.RestoreRotation();
            OnRotationRestored?.Invoke();
        }

        #endregion

        #region Rotation Handle Grab Handlers

        /// <summary>
        /// Called when the rotation handle starts being grabbed.
        /// Disables node interaction (if present) and the reset button.
        /// </summary>
        private void OnRotationGrabStart(SelectEnterEventArgs args)
        {
            _nodesController?.DisableNodeInteraction();
            _resetButton.SetInteractable(false);
            OnRotationStarted?.Invoke();
        }

        /// <summary>
        /// Called when the rotation handle is released.
        /// Re-enables node interaction (if present) and the reset button.
        /// </summary>
        private void OnRotationGrabEnd(SelectExitEventArgs args)
        {
            _nodesController?.EnableNodeInteraction();
            _resetButton.SetInteractable(true);
            OnRotationReleased?.Invoke();
        }

        #endregion

        #region Node Grab Handlers

        /// <summary>
        /// Called when the first spline node grab begins.
        /// Disables the rotation handle and the reset button.
        /// </summary>
        private void OnNodeGrabStart()
        {
            _rotationSourceInteractable.enabled = false;
            _resetButton.SetInteractable(false);
        }

        /// <summary>
        /// Called when all spline node grabs have ended.
        /// Re-enables the rotation handle and the reset button.
        /// </summary>
        private void OnNodeGrabEnd()
        {
            _rotationSourceInteractable.enabled = true;
            _resetButton.SetInteractable(true);
        }

        #endregion
    }
}
