using UnityEngine;
using UnityEngine.EventSystems;

namespace CustomUI
{
    /// <summary>
    /// ButtonController is a Unity component that handles button interactions.
    /// It extends InteractiveElement to manage hover and click functionality for toggle buttons.
    /// </summary>
    /// <remarks>
    /// This class is used to create toggle buttons in Unity UI.
    /// It provides methods for handling pointer clicks and ensuring that only one toggle can be active at a time within a group.
    ///</remarks>
    public class ToggleController : ResponsiveInteractiveElement
    {
        [Header("Toggle Resources")]
        [SerializeField] public ToggleGroupController ToggleGroupController;
        [Header("Toggle Settings")]
        [SerializeField] private bool _allowSelfDeselect = false;
        //[Header("Toggle Events")]
        //[SerializeField] public UnityEvent<ToggleController> OnToggleGroupSelection;

        /// <summary>
        /// Handles pointer down events for the toggle button.      
        /// If a ToggleGroupController is assigned, it updates the toggle group selection.
        /// Otherwise, it calls the base method to handle the default behavior.
        /// </summary>
        /// <param name="eventData">The event data associated with the pointer down event.</param>
        public override void OnPointerDown(PointerEventData eventData)
        {
            //Debug.Log("Pointer Down on " + name);
            if (_allowSelfDeselect && _isSelected)
            {
                Deselect();
                return;
            }
            if (ToggleGroupController != null)
            {
                ToggleGroupController.ProcessToggleGroupSelection(this);
                return;
            }
            base.OnPointerDown(eventData);
        }

        void OnDestroy()
        {
            if (ToggleGroupController != null)
            {
                ToggleGroupController.UnregisterToggleController(this);
            }
        }

        public void Deselect()
        {
            OnDeselect();
            Normalize();
        }
    }
}