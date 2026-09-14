// ============================================================================
// IModelSectionInteractable.cs
// 
// Interface for model section interactables.
// Extends IOutlineInteractable to add section-specific functionality.
// 
// Implementations:
//   - XRIntestineSectionInteractable (XR-based, large intestine)
//   - Future: PointerSectionInteractable, TouchSectionInteractable, etc.
// 
// Usage:
//   Systems like BlendshapesController and SelectionController use this
//   interface to work with any type of section interactable.
// ============================================================================

using UnityEngine;

namespace CustomUI
{
    /// <summary>
    /// Interface for model section interactables.
    /// Allows different input systems (XR, mouse/touch) and different organ types
    /// to be used interchangeably with the selection and blendshape systems.
    /// </summary>
    /// <remarks>
    /// <para>Extends <see cref="IOutlineInteractable"/> to provide section-specific functionality:</para>
    /// <list type="bullet">
    ///   <item>Section index tracking</item>
    ///   <item>Selection state management</item>
    ///   <item>Collider updates for deformed meshes</item>
    ///   <item>Material management for visual modes</item>
    /// </list>
    /// </remarks>
    public interface IModelSectionInteractable : IOutlineInteractable
    {
        /// <summary>
        /// The index of this section in the model
        /// </summary>
        int SectionIndex { get; }
        
        /// <summary>
        /// Whether this section is currently selected
        /// </summary>
        bool IsSelected { get; set; }
        
        /// <summary>
        /// Whether the collider needs to be updated (e.g., after blendshape changes)
        /// </summary>
        bool ColliderNeedsUpdate { get; set; }
        
        /// <summary>
        /// The GameObject this interactable is attached to
        /// </summary>
        GameObject GameObject { get; }
        
        /// <summary>
        /// Initializes the interactable with its dependencies
        /// </summary>
        /// <param name="selectionController">The selection controller to use</param>
        /// <param name="sectionIndex">The index of this section in the model</param>
        void Initialize(SelectionController selectionController, int sectionIndex);
        
        /// <summary>
        /// Enables interaction with this section
        /// </summary>
        void EnableInteractable();
        
        /// <summary>
        /// Disables interaction with this section
        /// </summary>
        void DisableInteractable();
        
        /// <summary>
        /// Updates the collider to match the current mesh state
        /// </summary>
        void UpdateCollider();
        
        /// <summary>
        /// Sets a material on this section
        /// </summary>
        void SetMaterial(Material material);
        
        /// <summary>
        /// Restores the original materials on this section
        /// </summary>
        void RestoreOriginalMaterials();
    }
}
