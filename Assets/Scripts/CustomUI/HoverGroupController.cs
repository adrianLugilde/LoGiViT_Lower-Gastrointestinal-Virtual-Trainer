using System.Collections.Generic;
using UnityEngine;

namespace CustomUI
{
    /// <summary>
    /// HoverSwayGroupController is a MonoBehaviour that manages hover sway effects for UI elements.
    /// </summary>
    /// <remarks>
    /// This class is used to control the hover sway behavior of UI elements in a group.
    /// It can be extended to implement specific hover sway logic.
    /// </remarks>
    public class HoverGroupController : MonoBehaviour
    {
        List<ResponsiveInteractiveElement> interactiveElements = new List<ResponsiveInteractiveElement>();
        int groupCount = 0;
        
        public void RegisterInteractiveElement(ResponsiveInteractiveElement interactiveElement)
        {
            if (interactiveElement != null && !interactiveElements.Contains(interactiveElement))
            {
                interactiveElements.Add(interactiveElement);
                groupCount++;
            }
        }

        public void UnregisterInteractiveElement(ResponsiveInteractiveElement interactiveElement)
        {
            if (interactiveElement != null && interactiveElements.Contains(interactiveElement))
            {
                interactiveElements.Remove(interactiveElement);
                groupCount--;
            }
        }

        public void SetGroupHover(ResponsiveInteractiveElement currentElement = null)
        {
            for (int i = 0; i < groupCount; ++i)
            {
                if (interactiveElements[i] == currentElement)
                {
                    interactiveElements[i].Highlight();
                    continue;
                }
                else if (!interactiveElements[i].IsSelected && interactiveElements[i].isActiveAndEnabled && interactiveElements[i].IsInteractable)
                {
                    interactiveElements[i].Normalize();
                }
            }
        }
    }
}
