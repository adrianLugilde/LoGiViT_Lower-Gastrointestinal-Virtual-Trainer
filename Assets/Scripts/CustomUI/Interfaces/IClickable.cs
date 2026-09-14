using UnityEngine.EventSystems;

namespace CustomUI
{
    /// <summary>
    /// Interface for clickable UI elements.
    /// </summary>
    /// <remarks>
    /// This interface is used to define the behavior of UI elements that can be clicked.
    /// Implement this interface to handle click events in your custom UI components.
    /// </remarks>}

    public interface IClickable
    {
        void OnClick(PointerEventData eventData = null);
    }
}