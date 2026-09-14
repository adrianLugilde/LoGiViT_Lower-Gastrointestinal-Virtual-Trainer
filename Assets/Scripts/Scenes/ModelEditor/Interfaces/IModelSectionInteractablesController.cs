// ============================================================================
// IModelSectionInteractablesController.cs
// 
// Interface for controllers that manage model section interactables.
// Provides abstraction for different organ types and input systems.
// 
// Implementations:
//   - LargeIntestineSectionInteractablesController (XR-based, large intestine)
//   - Future: StomachSectionInteractablesController, etc.
// 
// Usage:
//   Code that needs to work with any organ type should depend on this
//   interface rather than concrete implementations.
// ============================================================================

using System.Collections.Generic;
using CustomUI;

namespace ModelEditor
{
    /// <summary>
    /// Interface for controllers that manage model section interactables.
    /// Provides a consistent API for section interaction regardless of organ type or input system.
    /// </summary>
    /// <remarks>
    /// <para>Implementations handle:</para>
    /// <list type="bullet">
    ///   <item>Creating interactables for model sections</item>
    ///   <item>Enabling/disabling interaction</item>
    ///   <item>Managing visual appearance (materials)</item>
    /// </list>
    /// </remarks>
    public interface IModelSectionInteractablesController
    {
        /// <summary>
        /// The list of section interactables managed by this controller.
        /// </summary>
        IReadOnlyList<IModelSectionInteractable> SectionInteractables { get; }
        
        /// <summary>
        /// Initializes the controller with the model generator and selection controller.
        /// </summary>
        void Initialize(ISplineModelGenerator modelGenerator, SelectionController selectionController);
        
        /// <summary>
        /// Creates interactables for all sections in the model.
        /// </summary>
        void AddSectionsInteractables();

        /// <summary>
        /// Enables all section interactables for interaction.
        /// </summary>
        void EnableSectionInteractables();
        
        /// <summary>
        /// Disables all section interactables from interaction.
        /// </summary>
        void DisableSectionInteractables();
        
        /// <summary>
        /// Restores original materials to all sections and hides the segmented model.
        /// </summary>
        void RestoreSectionsOriginalMaterials();
        
        /// <summary>
        /// Sets transparent materials on all sections and shows the segmented model.
        /// </summary>
        void SetTransparentSectionsMaterials();
    }
}
