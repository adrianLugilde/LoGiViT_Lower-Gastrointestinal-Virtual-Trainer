using UnityEngine;

namespace CustomUI
{
    /// <summary>
    /// IOutlineInteractable is an interface that defines a contract for objects that can be interacted with in a way that allows for outline rendering.
    /// </summary>
    /// <remarks>
    /// This interface is used to ensure that any interactable object can provide its Renderer component for outline effects.
    /// </remarks>
    public interface IOutlineInteractable
    {
        Renderer GetRenderer();
    }
}