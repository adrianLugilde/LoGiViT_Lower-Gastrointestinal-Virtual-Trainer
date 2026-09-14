using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CustomUI
{
    /// <summary>
    /// ButtonController is a Unity component that handles button interactions. 
    /// It implements IPointerUpHandler to manage pointer up events and extends InteractiveElement for hover and click functionality.
    /// </summary>
    /// <remarks>
    /// This class is used to create interactive buttons in Unity UI.
    /// It provides methods for handling pointer clicks and pointer up events, allowing for a smooth user interaction experience.
    /// /// </remarks>
    public class ButtonController : ResponsiveInteractiveElement, IPointerUpHandler
    {
        [Header("Settings")]
        [SerializeField] private float pointerUpDelay = 0.2f;
        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            //Debug.Log("Pointer Up Detected");
            StartCoroutine(DelayedOnPointerUp());
        }

        private IEnumerator DelayedOnPointerUp()
        {
            yield return new WaitForSecondsRealtime(pointerUpDelay);
            Normalize();
            OnDeselect();
        }
    }
}