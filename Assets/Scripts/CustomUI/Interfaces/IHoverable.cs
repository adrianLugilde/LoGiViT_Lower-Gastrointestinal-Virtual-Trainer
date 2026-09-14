using UnityEngine.EventSystems;

namespace CustomUI
{
    /// <summary>
    /// Interface for hoverable UI elements.
    /// </summary>
    /// <remarks>
    /// This interface is used to define the behavior of UI elements that can be hovered over.
    /// Implement this interface to handle hover events in your custom UI components.
    /// </remarks>
    public interface IHoverable
    {
        void OnHoverEnter(PointerEventData eventData = null);
        void OnHoverExit(PointerEventData eventData = null);
    }
}