// ============================================================================
// ISplineNodesInteractablesController.cs
// 
// Interface for spline node interactables management.
// Provides node manipulation, undo/redo, and preset edition support.
// ============================================================================

using System;

namespace ModelEditor
{
    /// <summary>
    /// Interface for controllers that manage spline node interactables.
    /// </summary>
    public interface ISplineNodesInteractablesController : IEditorActionController
    {
        /// <summary>
        /// Event fired when any node's transform changes.
        /// </summary>
        event Action OnNodeTransformChanged;

        /// <summary>
        /// Event fired when a spline node interactable is released after interaction.
        /// </summary>
        event Action OnSplineNodeInteractableSelectExitNotify;

        /// <summary>
        /// Fired when the first node grab begins (transitions from zero to one active grab).
        /// </summary>
        event Action OnAnyNodeGrabStart;

        /// <summary>
        /// Fired when the last active node grab ends (transitions from one to zero active grabs).
        /// </summary>
        event Action OnAnyNodeGrabEnd;
        
        /// <summary>
        /// Creates XR interactables for all spline nodes.
        /// </summary>
        void AddSplineNodesInteractables();
        
        /// <summary>
        /// Enables interactability on all spline nodes (colliders / XR component).
        /// Does not affect node visibility.
        /// </summary>
        void EnableNodeInteraction();

        /// <summary>
        /// Disables interactability on all spline nodes (colliders / XR component).
        /// Does not affect node visibility.
        /// </summary>
        void DisableNodeInteraction();

        /// <summary>
        /// Makes the node holder GameObject active, showing all nodes.
        /// </summary>
        void ShowNodes();

        /// <summary>
        /// Makes the node holder GameObject inactive, hiding all nodes.
        /// </summary>
        void HideNodes();
        
        /// <summary>
        /// Enables solidary movement mode where adjacent nodes move together.
        /// </summary>
        void EnableSolidaryNodeMovement();
        
        /// <summary>
        /// Disables solidary movement mode.
        /// </summary>
        void DisableSolidaryNodeMovement();
        
        /// <summary>
        /// Enters preset edition mode, backing up current records.
        /// </summary>
        void EnableRecordsForPresetEdition();
        
        /// <summary>
        /// Exits preset edition mode, restoring original records.
        /// </summary>
        void DisableRecordsForPresetEdition();
    }
}
