// ============================================================================
// XRIntestineSectionInteractable.cs
// 
// XR-based interactable component for intestine model sections.
// Handles VR controller selection, hover events, and visual feedback.
// Implements IModelSectionInteractable for integration with the Model Editor.
// 
// Dependencies:
//   - XRSimpleInteractable (auto-added via RequireComponent)
//   - SkinnedMeshRenderer (on the same GameObject)
//   - MeshCollider (for interaction raycasting)
// ============================================================================

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CustomUI
{
    /// <summary>
    /// XR interactable component for organ model sections.
    /// Enables VR selection and hover events on individual sections of the large intestine model.
    /// </summary>
    /// <remarks>
    /// <para>This class implements <see cref="IModelSectionInteractable"/> for use with XR input systems.</para>
    /// <para>It handles:</para>
    /// <list type="bullet">
    ///   <item>Selection via XR controllers (trigger press)</item>
    ///   <item>Hover feedback when pointing at sections</item>
    ///   <item>Collider updates after blendshape changes</item>
    ///   <item>Material swapping for editing mode visualization</item>
    /// </list>
    /// </remarks>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
    public class XRIntestineSectionInteractable : MonoBehaviour, IModelSectionInteractable
    {
        #region Component References
        
        [Tooltip("XR Toolkit interactable component for VR interactions")]
        public UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable xrSimpleInteractable;
        
        [Tooltip("Skinned mesh renderer for this section")]
        public SkinnedMeshRenderer skinnedMeshRenderer;
        
        [Tooltip("Mesh collider for interaction raycasting")]
        public MeshCollider meshCollider;
        
        [Tooltip("Cached original materials for restoration")]
        public Material[] originalMaterials;
        
        #endregion
        
        #region Private Fields
        
        /// <summary>
        /// Reference to the selection controller for handling selection state.
        /// </summary>
        protected SelectionController _selectionController;
        
        #endregion
        
        #region IModelSectionInteractable Implementation
        
        /// <inheritdoc/>
        public bool IsSelected { get; set; }
        
        /// <inheritdoc/>
        public int SectionIndex { get; private set; } = -1;
        
        /// <inheritdoc/>
        public bool ColliderNeedsUpdate { get; set; }
        
        /// <inheritdoc/>
        public GameObject GameObject => gameObject;
        
        /// <inheritdoc/>
        public Renderer GetRenderer() => skinnedMeshRenderer;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            // Cache component references
            xrSimpleInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            meshCollider = GetComponent<MeshCollider>();
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        }

        protected virtual void Start()
        {
            // Store original materials for later restoration
            originalMaterials = skinnedMeshRenderer.sharedMaterials;
        }
        
        private void OnEnable()
        {
            SubscribeInteractableEvents();
        }

        private void OnDisable()
        {
            UnsubscribeInteractableEvents();
        }
        
        #endregion
        
        #region Initialization

        /// <inheritdoc/>
        public void Initialize(SelectionController selectionController, int sectionIndex)
        {
            _selectionController = selectionController;
            SectionIndex = sectionIndex;
        }
        
        #endregion
        
        #region Collider Management

        /// <summary>
        /// Updates the mesh collider to match the current deformed mesh state.
        /// Call this after blendshape changes to ensure accurate hit detection.
        /// </summary>
        public void UpdateCollider()
        {
            meshCollider.enabled = false;
            
            // Bake the current skinned mesh state into a static mesh
            var tempMesh = new Mesh();
            skinnedMeshRenderer.BakeMesh(tempMesh);
            meshCollider.sharedMesh = tempMesh;
            
            meshCollider.enabled = true;
        }
        
        #endregion
        
        #region Material Management

        /// <inheritdoc/>
        public void SetMaterial(Material newMaterial)
        {
            skinnedMeshRenderer.sharedMaterials = new Material[] { newMaterial };
        }

        /// <inheritdoc/>
        public void RestoreOriginalMaterials()
        {
            skinnedMeshRenderer.sharedMaterials = originalMaterials;
        }
        
        #endregion

        #region XR Event Handlers
        
        /// <summary>
        /// Called when the user selects (triggers) this section with an XR controller.
        /// </summary>
        /// <param name="args">Event arguments from XR Interaction Toolkit</param>
        public void OnSectionSelect(SelectEnterEventArgs args)
        {
            _selectionController.ChangeSelection(this, isSelectionAction: true);
        }

        /// <summary>
        /// Called when an XR controller starts hovering over this section.
        /// </summary>
        /// <param name="arg0">Event arguments from XR Interaction Toolkit</param>
        private void OnSectionHoverEnter(HoverEnterEventArgs arg0)
        {
            _selectionController.AddHoveredElement(skinnedMeshRenderer);
        }

        /// <summary>
        /// Called when an XR controller stops hovering over this section.
        /// </summary>
        /// <param name="arg0">Event arguments from XR Interaction Toolkit</param>
        public void OnSectionHoverExit(HoverExitEventArgs arg0)
        {
            _selectionController.RemoveHoveredElement(skinnedMeshRenderer);
        }
        
        #endregion
        
        #region Interaction Control

        /// <inheritdoc/>
        public void EnableInteractable()
        {
            // Update collider to match current mesh state before enabling
            UpdateCollider();
        }

        /// <inheritdoc/>
        public void DisableInteractable()
        {
            // Disable collider to prevent interaction
            meshCollider.enabled = false;
        }
        
        #endregion
        
        #region Event Subscription

        /// <summary>
        /// Subscribes to XR Interaction Toolkit events.
        /// Called automatically in OnEnable.
        /// </summary>
        public void SubscribeInteractableEvents()
        {
            xrSimpleInteractable.selectEntered.AddListener(OnSectionSelect);
            xrSimpleInteractable.hoverEntered.AddListener(OnSectionHoverEnter);
            xrSimpleInteractable.hoverExited.AddListener(OnSectionHoverExit);
        }

        /// <summary>
        /// Unsubscribes from XR Interaction Toolkit events.
        /// Called automatically in OnDisable.
        /// </summary>
        public void UnsubscribeInteractableEvents()
        {
            xrSimpleInteractable.selectEntered.RemoveAllListeners();
            xrSimpleInteractable.hoverEntered.RemoveAllListeners();
            xrSimpleInteractable.hoverExited.RemoveAllListeners();
        }
        
        #endregion
    }
}