// ============================================================================
// SelectionController.cs
// 
// Centralized selection and hover management for the model editor.
// Handles visual feedback via outline layers and coordinates selection events.
// 
// Key Features:
//   - Single and multiple selection support
//   - Hover highlighting with layer-based outlines
//   - Selection events for UI updates and state changes
//   - Works with any IOutlineInteractable (nodes, sections, etc.)
// 
// Layer Setup:
//   - Default Layer (0): Normal rendering
//   - Selection Layer (26): Selected elements with outline
//   - Hover Layer (27): Hovered elements with outline
// ============================================================================

using MathUtils = Utilities.MathUtils;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace CustomUI
{
    /// <summary>
    /// Centralized controller for element selection and hover state management.
    /// Manages visual feedback and notifies subscribers of selection changes.
    /// </summary>
    /// <remarks>
    /// <para>Uses Unity layers for outline rendering:</para>
    /// <list type="bullet">
    ///   <item>Selected elements are moved to selectionLayer (26)</item>
    ///   <item>Hovered elements are moved to hoverLayer (27)</item>
    ///   <item>Outline post-processing renders these layers with colored outlines</item>
    /// </list>
    /// </remarks>
    public class SelectionController : MonoBehaviour
    {
        #region State Collections
        
        /// <summary>
        /// Set of currently selected renderers.
        /// </summary>
        public HashSet<Renderer> selectedElements;
        
        /// <summary>
        /// Set of currently hovered renderers.
        /// </summary>
        public HashSet<Renderer> hoveredElements;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Whether multiple elements can be selected simultaneously.
        /// </summary>
        public bool MultipleSelectionEnabled { get; private set; } = false;
        
        #endregion
        
        #region Configuration
        
        [Tooltip("Outline color for hovered elements")]
        public Color hoverOutlineColor = new Color(1f, 0.9f, 0.26f, 0.05f);
        
        [Tooltip("Outline color for selected elements")]
        public Color selectionOutlineColor = new Color(0f, 1f, 0f, 0f);
        
        [Tooltip("Default layer for elements not selected or hovered")]
        public int defaultLayer = 0;
        
        [Tooltip("Layer for selected elements (should have outline effect)")]
        public int selectionLayer = 26;
        
        [Tooltip("Layer for hovered elements (should have outline effect)")]
        public int hoverLayer = 27;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when all selections are cleared.
        /// </summary>
        public Action OnClearSelection;
        
        /// <summary>
        /// Fired when an element is added to selection.
        /// </summary>
        public Action<SelectionEventArgs> OnAddToSelection;
        
        /// <summary>
        /// Fired when an element is removed from selection.
        /// </summary>
        public Action<SelectionEventArgs> OnRemoveFromSelection;
        
        #endregion


        #region Unity Lifecycle

        private void Start()
        {
            selectedElements = new HashSet<Renderer>();
            hoveredElements = new HashSet<Renderer>();
        }
        
        #endregion
        
        #region Hover Management

        /// <summary>
        /// Adds an element to the hover set and applies hover visuals.
        /// </summary>
        /// <param name="renderer">The renderer to mark as hovered</param>
        public void AddHoveredElement(Renderer renderer)
        {
            hoveredElements.Add(renderer);
            
            // Only apply hover visuals if not already selected
            if (!selectedElements.Contains(renderer))
            {
                renderer.gameObject.layer = selectionLayer;
                SetColor(renderer, hoverOutlineColor);
            }
        }

        /// <summary>
        /// Removes an element from the hover set and clears hover visuals.
        /// </summary>
        /// <param name="renderer">The renderer to remove from hover</param>
        public void RemoveHoveredElement(Renderer renderer)
        {
            hoveredElements.Remove(renderer);
            
            // Only reset layer if not selected
            if (!selectedElements.Contains(renderer))
            {
                renderer.gameObject.layer = defaultLayer;
            }
        }
        
        #endregion
        
        #region Selection Logic

        /// <summary>
        /// Processes a selection change for an interactable element.
        /// </summary>
        /// <typeparam name="T">Type implementing IOutlineInteractable</typeparam>
        /// <param name="interactable">The interactable being selected/deselected</param>
        /// <param name="isSelectionAction">True if this is a select action, false if deselect</param>
        public void ChangeSelection<T>(T interactable, bool isSelectionAction) where T : IOutlineInteractable
        {
            ChangeSelection(interactable.GetRenderer(), interactable, isSelectionAction);
        }

        /// <summary>
        /// Internal selection change handler.
        /// </summary>
        private void ChangeSelection<T>(Renderer renderer, T interactable, bool isSelectionAction) where T : IOutlineInteractable
        {
            bool isSelected = selectedElements.Contains(renderer);

            if (isSelected)
            {
                HandleAlreadySelectedElementInteraction(renderer, interactable, isSelectionAction);
            }
            else
            {
                HandleNotCurrentlySelectedElementInteraction(renderer, interactable, isSelectionAction);
            }
        }

        /// <summary>
        /// Handles interaction with an already selected element.
        /// Behavior differs based on interactable type and multiple selection mode.
        /// </summary>
        private void HandleAlreadySelectedElementInteraction<T>(Renderer renderer, T interactable, bool isSelectionAction) where T : IOutlineInteractable
        {
            if (interactable.GetType() == typeof(XRSplineNodeInteractable))
            {
                // Spline nodes: keep selected in multiple selection mode
                if (MultipleSelectionEnabled)
                {
                    return;
                }
                else if (!isSelectionAction)
                {
                    // Deselect on release
                    RemoveFromSelectionAndNotify(renderer, interactable);
                }
            }
            else if (interactable is IModelSectionInteractable)
            {
                // Sections: toggle selection on click
                if (MultipleSelectionEnabled)
                {
                    return;
                }
                else if (isSelectionAction)
                {
                    // Deselect on re-click
                    RemoveFromSelectionAndNotify(renderer, interactable);
                }
            }
        }

        /// <summary>
        /// Handles interaction with a not currently selected element.
        /// </summary>
        private void HandleNotCurrentlySelectedElementInteraction<T>(Renderer renderer, T interactable, bool isSelectionAction) where T : IOutlineInteractable
        {
            // Only process selection actions, not hover
            if (!isSelectionAction) return;
            
            // Clear previous selection if not in multiple selection mode
            if (!MultipleSelectionEnabled)
            {
                ClearSelectionAndNotify();
            }

            // Add to selection
            AddToSelectionAndNotify(renderer, interactable);
        }
        
        #endregion
        
        #region Selection Notifications

        /// <summary>
        /// Clears all selections and notifies subscribers.
        /// </summary>
        public void ClearSelectionAndNotify()
        {
            // Notify for each element being removed
            for (int i = 0; i < selectedElements.Count; i++)
            {
                var interactable = new List<Renderer>(selectedElements)[i].GetComponent<IOutlineInteractable>();
                if (interactable != null)
                {
                    OnRemoveFromSelection?.Invoke(new SelectionEventArgs(interactable));
                }
            }
            ClearSelectionOutlines();   
            OnClearSelection?.Invoke();
        }

        /// <summary>
        /// Adds an element to selection and notifies subscribers.
        /// </summary>
        private void AddToSelectionAndNotify<T>(Renderer renderer, T interactable) where T : IOutlineInteractable
        {
            AddRendererToSelection(renderer);
            if (interactable != null)
            {
                OnAddToSelection?.Invoke(new SelectionEventArgs(interactable));
            }
        }

        /// <summary>
        /// Removes an element from selection and notifies subscribers.
        /// </summary>
        private void RemoveFromSelectionAndNotify<T>(Renderer renderer, T interactable) where T : IOutlineInteractable
        {
            RemoveRendererFromSelection(renderer);
            if (interactable != null)
            {
                OnRemoveFromSelection?.Invoke(new SelectionEventArgs(interactable));
            }
        }
        
        #endregion
        
        #region Multiple Selection Mode

        /// <summary>
        /// Enables multiple selection mode.
        /// </summary>
        public void EnableMultipleSelection()
        {
            MultipleSelectionEnabled = true;
        }

        /// <summary>
        /// Disables multiple selection mode.
        /// </summary>
        public void DisableMultipleSelection()
        {
            MultipleSelectionEnabled = false;
        }
        
        #endregion
        
        #region Low-Level Selection Management

        /// <summary>
        /// Adds a renderer to the selection set with visual feedback.
        /// </summary>
        /// <param name="renderer">The renderer to select</param>
        public void AddRendererToSelection(Renderer renderer)
        {
            Debug.Log($"[SelectionController] Adding {renderer.gameObject.name} to selection");
            selectedElements.Add(renderer);
            renderer.gameObject.layer = selectionLayer;
            SetColor(renderer, selectionOutlineColor);
        }

        /// <summary>
        /// Removes a renderer from the selection set.
        /// </summary>
        /// <param name="renderer">The renderer to deselect</param>
        public void RemoveRendererFromSelection(Renderer renderer)
        {
            selectedElements.Remove(renderer);
            
            // Keep hover color if still hovered, otherwise reset
            if (hoveredElements.Contains(renderer))
            {
                SetColor(renderer, hoverOutlineColor);
            }
            else
            {
                renderer.gameObject.layer = defaultLayer;
            }
        }

        /// <summary>
        /// Clears all selection outlines without notifications.
        /// </summary>
        public void ClearSelectionOutlines()
        {
            foreach (var renderer in selectedElements)
            {
                renderer.gameObject.layer = defaultLayer;
            }
            selectedElements.Clear();
        }

        /// <summary>
        /// Clears all hover outlines.
        /// </summary>
        public void ClearHoverOutlines()
        {
            foreach (var renderer in hoveredElements)
            {
                renderer.gameObject.layer = defaultLayer;
            }
            hoveredElements.Clear();
        }

        /// <summary>
        /// Clears both selection and hover outlines.
        /// </summary>
        public void ClearAllOutlines()
        {
            ClearSelectionOutlines();
            ClearHoverOutlines();
        }
        
        #endregion
        
        #region Utility Methods

        /// <summary>
        /// Calculates the centroid (center point) of all selected elements.
        /// </summary>
        /// <returns>The centroid position in world space</returns>
        public Vector3 GetSelectionCentroid()
        {
            var selectionPositions = new List<Vector3>();
            foreach (var item in selectedElements)
            {
                selectionPositions.Add(item.transform.position);
            }
            return MathUtils.GetCentroid(selectionPositions);
        }

        /// <summary>
        /// Gets all selected XRSplineNodeInteractable components.
        /// </summary>
        /// <returns>List of selected spline node interactables</returns>
        public List<XRSplineNodeInteractable> GetSelectedXRSplineNodeInteractables()
        {
            var sniList = new List<XRSplineNodeInteractable>();
            foreach (var selectedElement in selectedElements)
            {
                sniList.Add(selectedElement.GetComponent<XRSplineNodeInteractable>());
            }
            return sniList;
        }

        /// <summary>
        /// Sets the outline color on a renderer via MaterialPropertyBlock.
        /// </summary>
        /// <param name="renderer">Target renderer</param>
        /// <param name="color">Outline color to set</param>
        private void SetColor(Renderer renderer, Color color)
        {
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_ContourColor", color);
            renderer.SetPropertyBlock(propertyBlock);
        }
        
        #endregion
    }

    #region Event Args

    /// <summary>
    /// Event arguments for selection change events.
    /// </summary>
    public class SelectionEventArgs : EventArgs
    {
        /// <summary>
        /// The selected/deselected item (implements IOutlineInteractable).
        /// </summary>
        public object SelectedItem { get; }
        
        /// <summary>
        /// The concrete type of the selected item.
        /// </summary>
        public Type ItemType { get; }

        /// <summary>
        /// Creates a new SelectionEventArgs.
        /// </summary>
        /// <param name="item">The item being selected/deselected</param>
        public SelectionEventArgs(object item)
        {
            SelectedItem = item;
            ItemType = item?.GetType();
        }

        /// <summary>
        /// Gets the selected item cast to a specific type.
        /// </summary>
        /// <typeparam name="T">Type to cast to</typeparam>
        /// <returns>The item as type T, or null if cast fails</returns>
        public T GetItem<T>() where T : class => SelectedItem as T;
    }
    
    #endregion
}
