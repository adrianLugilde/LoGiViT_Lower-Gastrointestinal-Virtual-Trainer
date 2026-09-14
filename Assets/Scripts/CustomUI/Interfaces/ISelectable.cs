namespace CustomUI
{
    /// <summary>
    /// Interface for hoverable UI elements.
    /// </summary>
    /// <remarks>
    /// This interface is used to define the behavior of UI elements that can be selected.
    /// Implement this interface to handle selection events in your custom UI components.
    /// </remarks>
    interface ISelectable
    {
        bool IsSelected { get; }
        void OnSelect();
        void OnDeselect();
    }
}