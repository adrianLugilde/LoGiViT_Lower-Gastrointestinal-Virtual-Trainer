// ============================================================================
// XRSplineNodeInteractable.cs
// 
// XR-based interactable component for spline nodes.
// Handles VR controller grabbing, position/rotation updates, and linked movement.
// 
// Key Features:
//   - Grabbable via XRGrabInteractable
//   - Updates spline node position/rotation in real-time
//   - Supports solidary movement (adjacent nodes move together)
//   - Multiple selection support
// 
// Dependencies:
//   - XRGrabInteractable (auto-added via RequireComponent)
//   - MeshCollider and MeshRenderer for visual feedback
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SplineMesh;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CustomUI
{
    /// <summary>
    /// XR interactable component for spline nodes.
    /// Enables VR-based manipulation of spline control points.
    /// </summary>
    /// <remarks>
    /// <para>Implements <see cref="IOutlineInteractable"/> for selection system integration.</para>
    /// <para>Key behaviors:</para>
    /// <list type="bullet">
    ///   <item>Grab to move the node, release to apply changes</item>
    ///   <item>Rotation updates the node's up vector</item>
    ///   <item>Solidary mode moves adjacent nodes proportionally</item>
    ///   <item>Multiple selection allows moving several nodes at once</item>
    /// </list>
    /// </remarks>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class XRSplineNodeInteractable : MonoBehaviour, IOutlineInteractable
    {
        #region Component References

        [Tooltip("XR Toolkit grab interactable component")]
        public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable xrGrabInteractable;

        [Tooltip("Mesh collider for grab detection")]
        public MeshCollider meshCollider;

        [Tooltip("Renderer for visual feedback")]
        public Renderer meshRenderer;

        #endregion

        #region Movement Configuration

        /// <summary>
        /// Whether this node is the origin of a multi-node movement.
        /// </summary>
        public bool isMovementOrigin = false;

        /// <summary>
        /// Whether adjacent nodes should move together with this node.
        /// </summary>
        public bool SolidaryHandleTargets = false;

        /// <summary>
        /// Whether this node initiated the current solidary movement.
        /// </summary>
        public bool isSolidaryMovementOrigin = false;

        /// <summary>
        /// Maximum percentage variation allowed in distance between adjacent nodes.
        /// </summary>
        public float SplineNodesDistVarPercent = 0.1f;

        /// <summary>
        /// Displacement factor applied to adjacent nodes in solidary mode (0-1).
        /// </summary>
        public float SplineNodesSolidaryDisplacement = 0.5f;

        #endregion

        #region Epsilon Values

        [Header("Epsilon Values")]
        [Tooltip("Minimum position change to trigger a record (units)")]
        public float positionEpsilon = 0.0001f;

        [Tooltip("Minimum rotation change to trigger a record (degrees)")]
        public float rotationEpsilon = 2f;

        #endregion

        #region Private Fields

        protected SelectionController _selectionController;
        private Transform _splineTransform;
        private SplineNode target;
        private Vector3 _prevLocalPosition;
        private Quaternion _prevLocalRotation;
        private Vector3 _onGrabPosition;
        private Quaternion _onGrabRotation;
        private Vector3 baseNodeUp;

        #endregion

        #region Public Fields

        /// <summary>
        /// Links to adjacent nodes for solidary movement calculations.
        /// </summary>
        public List<SplineGizmoEditionNodeLink> splineGizmoEditionNodeLinks;

        /// <summary>
        /// Index of this node in the spline (0 = rectum, increases toward cecum).
        /// </summary>
        public int idxInSpline = 0;

        #endregion

        #region Events

        /// <summary>
        /// Fired when the node's transform changes significantly.
        /// </summary>
        public Action OnNodeTransformChanged;

        /// <summary>
        /// Fired when the node is released after being grabbed.
        /// </summary>
        public Action<XRSplineNodeInteractable> OnNodeSelectExitNotify;

        #endregion

        #region Properties

        /// <summary>
        /// The spline node this interactable controls.
        /// </summary>
        public SplineNode Target
        {
            get { return target; }
            set
            {
                target = value;
                OnTargetSet();
            }
        }

        /// <inheritdoc/>
        public Renderer GetRenderer() => meshRenderer;

        #endregion
        #region Unity Lifecycle

        private void Awake()
        {
            xrGrabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();
            SubscribeInteractableEvents();
        }
        private void Update()
        {
            // Compare in local space so parent rotations/translations don't cause
            // spurious updates every frame.
            if (transform.localPosition != _prevLocalPosition)
            {
                UpdateTargetPosition();
            }

            if (transform.localRotation != _prevLocalRotation)
            {
                UpdateTargetRotation();
            }
        }

        private void OnEnable()
        {
            // Event subscription handled in Awake
        }

        private void OnDisable()
        {
            // Event unsubscription handled in OnDestroy
        }

        private void OnDestroy()
        {
            UnsubscribeInteractableEvents();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the interactable with required dependencies.
        /// </summary>
        /// <param name="selectionController">Selection controller for handling selection state</param>
        /// <param name="targetNode">The spline node to control</param>
        /// <param name="indexInSpline">Index of the node in the spline</param>
        public void Initialize(SelectionController selectionController, SplineNode targetNode, int indexInSpline, Transform splineTransform)
        {
            _selectionController = selectionController;
            _splineTransform = splineTransform;
            Target = targetNode;
            idxInSpline = indexInSpline;
        }

        /// <summary>
        /// Called when the target spline node is set.
        /// Initializes tracking variables.
        /// </summary>
        private void OnTargetSet()
        {
            _prevLocalPosition = transform.localPosition;
            _prevLocalRotation = transform.localRotation;
            baseNodeUp = Target.Up;
        }

        #endregion

        #region Node Links

        /// <summary>
        /// Sets up links to adjacent nodes for solidary movement.
        /// </summary>
        /// <param name="previousNode">The node before this one (toward rectum), or null</param>
        /// <param name="followingNode">The node after this one (toward cecum), or null</param>
        public void SetNodeLinks(XRSplineNodeInteractable previousNode = null, XRSplineNodeInteractable followingNode = null)
        {
            splineGizmoEditionNodeLinks = new List<SplineGizmoEditionNodeLink>();
            if (previousNode != null)
            {
                splineGizmoEditionNodeLinks.Add(new SplineGizmoEditionNodeLink(this, previousNode));
            }
            if (followingNode != null)
            {
                splineGizmoEditionNodeLinks.Add(new SplineGizmoEditionNodeLink(this, followingNode));
            }
        }

        /// <summary>
        /// Sets whether this node is the origin of a solidary movement.
        /// </summary>
        /// <param name="enabled">True to mark as origin</param>
        public void SetAsSolidaryMovementOrigin(bool enabled)
        {
            isSolidaryMovementOrigin = enabled;
        }

        /// <summary>
        /// Gets the maximum allowed displacement based on adjacent node distances.
        /// </summary>
        /// <returns>Maximum displacement distance</returns>
        public float GetMaxDistanceDisplacement()
        {
            var maxDistance = float.PositiveInfinity;
            foreach (var nodeLink in splineGizmoEditionNodeLinks)
            {
                var distance = nodeLink.defaultDistance + (nodeLink.defaultDistance * SplineNodesDistVarPercent);
                if (distance < maxDistance) maxDistance = distance;
            }
            return maxDistance;
        }

        #endregion

        #region XR Event Handlers

        /// <summary>
        /// Called when the node is grabbed by an XR controller.
        /// </summary>
        /// <param name="args">Event arguments from XR Interaction Toolkit</param>
        public void OnNodeSelect(SelectEnterEventArgs args)
        {
            // Store initial state for change detection
            _onGrabPosition = transform.position;
            _onGrabRotation = transform.rotation;

            _selectionController.ChangeSelection(this, isSelectionAction: true);
            isMovementOrigin = true;
        }

        /// <summary>
        /// Called when the node is released by an XR controller.
        /// </summary>
        /// <param name="args">Event arguments from XR Interaction Toolkit</param>
        public void OnNodeDeselect(SelectExitEventArgs args)
        {
            _selectionController.ChangeSelection(this, isSelectionAction: false);

            // Check if position or rotation changed significantly
            bool positionChanged = (_onGrabPosition - transform.position).magnitude > positionEpsilon;
            bool rotationChanged = Quaternion.Angle(_onGrabRotation, transform.rotation) > rotationEpsilon;

            if (positionChanged || rotationChanged)
            {
                Debug.Log($"{gameObject.name} position changed by more than {positionEpsilon} units: {(_onGrabPosition - transform.position).magnitude}");
                OnNodeTransformChanged?.Invoke();
                OnNodeSelectExitNotify?.Invoke(this);
            }
            else
            {
                Debug.Log($"{gameObject.name} deselected without creating a record");
            }

            isMovementOrigin = false;
        }

        /// <summary>
        /// Called when an XR controller starts hovering over this node.
        /// </summary>
        private void OnNodeHoverEnter(HoverEnterEventArgs arg0)
        {
            _selectionController.AddHoveredElement(meshRenderer);
        }

        /// <summary>
        /// Called when an XR controller stops hovering over this node.
        /// </summary>
        public void OnNodeHoverExit(HoverExitEventArgs arg0)
        {
            _selectionController.RemoveHoveredElement(meshRenderer);
        }

        #endregion

        #region Interaction Control

        /// <summary>
        /// Enables interaction with this node.
        /// </summary>
        public void EnableInteractable()
        {
            meshCollider.enabled = true;
            //enabled = true;
        }

        /// <summary>
        /// Disables interaction with this node.
        /// </summary>
        public void DisableInteractable()
        {
            meshCollider.enabled = false;
            //enabled = false;
        }

        #endregion

        #region Event Subscription

        /// <summary>
        /// Subscribes to XR Interaction Toolkit events.
        /// </summary>
        private void SubscribeInteractableEvents()
        {
            xrGrabInteractable.selectEntered.AddListener(OnNodeSelect);
            xrGrabInteractable.selectExited.AddListener(OnNodeDeselect);
            xrGrabInteractable.hoverEntered.AddListener(OnNodeHoverEnter);
            xrGrabInteractable.hoverExited.AddListener(OnNodeHoverExit);
        }

        /// <summary>
        /// Unsubscribes from XR Interaction Toolkit events.
        /// </summary>
        private void UnsubscribeInteractableEvents()
        {
            xrGrabInteractable.selectEntered.RemoveAllListeners();
            xrGrabInteractable.selectExited.RemoveAllListeners();
            xrGrabInteractable.hoverEntered.RemoveAllListeners();
            xrGrabInteractable.hoverExited.RemoveAllListeners();
        }

        #endregion

        #region Position/Rotation Updates

        /// <summary>
        /// Updates the node's up vector based on the interactable's rotation.
        /// </summary>
        public void UpdateTargetRotation()
        {
            Target.Up = transform.rotation * baseNodeUp;
            _prevLocalRotation = transform.localRotation;
        }

        /// <summary>
        /// Updates the spline node's position based on the interactable's world position.
        /// Handles multiple selection and solidary movement modes.
        /// </summary>
        public void UpdateTargetPosition()
        {
            var transformedTargetPosition = _splineTransform.TransformPoint(Target.Position);
            var offset = transform.position - transformedTargetPosition;

            // In multiple selection mode, move all selected nodes together
            if (_selectionController.MultipleSelectionEnabled && isMovementOrigin)
            {
                var selectedElements = _selectionController.GetSelectedXRSplineNodeInteractables();
                selectedElements = selectedElements.Except(new List<XRSplineNodeInteractable> { this }).ToList();

                foreach (var sni in selectedElements)
                {
                    sni.transform.position += offset;
                }
            }

            // Update this node's position in local space
            Target.Position = _splineTransform.InverseTransformPoint(transform.position);
            _prevLocalPosition = transform.localPosition;

            // In solidary mode, move adjacent nodes proportionally
            if (SolidaryHandleTargets && isSolidaryMovementOrigin && !_selectionController.MultipleSelectionEnabled)
            {
                foreach (var nodeLink in splineGizmoEditionNodeLinks)
                {
                    nodeLink.linkedNode.transform.position += offset * SplineNodesSolidaryDisplacement;
                }
            }

            // Update distance calculations for linked nodes
            for (int i = 0; i < splineGizmoEditionNodeLinks.Count; i++)
            {
                splineGizmoEditionNodeLinks[i].UpdateDefaultDistance();
            }
        }

        #endregion
    }

    #region Helper Classes

    /// <summary>
    /// Represents a link between two spline node interactables for distance tracking.
    /// Used for solidary movement calculations.
    /// </summary>
    public class SplineGizmoEditionNodeLink
    {
        /// <summary>
        /// Default distance between the linked nodes.
        /// </summary>
        public float defaultDistance;

        /// <summary>
        /// The adjacent node that is linked.
        /// </summary>
        public XRSplineNodeInteractable linkedNode;

        /// <summary>
        /// The node that owns this link.
        /// </summary>
        public XRSplineNodeInteractable referenceNode;

        /// <summary>
        /// Creates a new link between two nodes.
        /// </summary>
        /// <param name="referenceNode">The owning node</param>
        /// <param name="linkedNode">The adjacent node</param>
        public SplineGizmoEditionNodeLink(XRSplineNodeInteractable referenceNode, XRSplineNodeInteractable linkedNode)
        {
            this.linkedNode = linkedNode;
            this.referenceNode = referenceNode;
            defaultDistance = Vector3.Distance(referenceNode.Target.Position, linkedNode.Target.Position);
        }

        /// <summary>
        /// Recalculates the default distance based on current positions.
        /// </summary>
        public void UpdateDefaultDistance()
        {
            defaultDistance = Vector3.Distance(referenceNode.Target.Position, linkedNode.Target.Position);
        }
    }

    #endregion
}
