// ============================================================================
// IBlendshapesController.cs
// 
// Interface for blendshape manipulation controllers.
// Provides section selection management and blendshape editing capabilities.
// ============================================================================

using CustomUI;
using System;

namespace ModelEditor
{
    /// <summary>
    /// Interface for controllers that manage blendshape editing operations.
    /// </summary>
    public interface IBlendshapesController : IEditorActionController
    {
        /// <summary>
        /// Event fired when section selection changes.
        /// </summary>
        event Action OnSectionSelectionChanged;
        
        /// <summary>
        /// Event fired when section selection is cleared.
        /// </summary>
        event Action OnSectionSelectionCleared;
        
        /// <summary>
        /// Whether any sections are currently selected.
        /// </summary>
        bool HasSelection { get; }
        
        /// <summary>
        /// Adds a section to the current selection.
        /// </summary>
        /// <param name="section">The section to add</param>
        void AddSectionToSelection(IModelSectionInteractable section);
        
        /// <summary>
        /// Removes a section from the current selection.
        /// </summary>
        /// <param name="section">The section to remove</param>
        void RemoveSectionFromSelection(IModelSectionInteractable section);
        
        /// <summary>
        /// Clears all selected sections.
        /// </summary>
        void ClearSelection();
        
        /// <summary>
        /// Updates selected sliders by a delta value (for VR controller input).
        /// </summary>
        /// <param name="delta">The delta value to apply</param>
        void UpdateSelectedSlidersByDelta(float delta);
        
        /// <summary>
        /// Stores previous values for selected sliders (for undo support).
        /// </summary>
        void StoreSelectedBlendshapeSlidersPreviousValues();
        
        /// <summary>
        /// Manages pending slider value changes (creates undo record if needed).
        /// </summary>
        void ManageSlidersValueChange();
    }
}
