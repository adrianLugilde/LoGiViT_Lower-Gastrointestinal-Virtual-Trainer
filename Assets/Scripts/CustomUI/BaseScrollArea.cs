

using UnityEngine;
using UnityEngine.UI;

namespace CustomUI
{
    /// <summary>
    /// BaseScrollArea is an abstract class that serves as a base for scrollable UI areas.
    /// It contains a reference to the content GameObject and a scrollbar.
    /// </summary>
    /// <remarks>
    /// This class is used to create scrollable areas in Unity UI.
    /// It provides a method to reset the scrollbar value to the top.
    /// </remarks>
    public abstract class BaseScrollArea : MonoBehaviour
    {
        public GameObject ContentGameObject;
        public Scrollbar Scrollbar;

        public void Reset()
        {
            Scrollbar.value = 1;
        }
    }
}