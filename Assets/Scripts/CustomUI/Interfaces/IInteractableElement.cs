using UnityEngine.EventSystems;

namespace CustomUI
{
    /// <summary>
    /// IInteractableElement is an interface that combines multiple pointer event handlers.
    /// It is used to define elements that can respond to pointer interactions such as entering, exiting, pressing, and releasing.
    /// </summary>
    /// <remarks>
    /// This interface is typically implemented by UI elements that need to handle user interactions in a custom way.
    /// It allows for a unified approach to managing pointer events across different UI components.
    /// </remarks>
    public interface IInteractableElement : IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler, IPointerDownHandler
    {
        bool IsInteractable { get; set; }
    }
}