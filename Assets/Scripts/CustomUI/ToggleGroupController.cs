using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CustomUI
{
    /// <summary>
    /// ToggleGroupController is a Unity component that manages a group of toggle buttons.
    /// It allows only one toggle to be selected at a time and updates the UI accordingly.
    /// </summary>
    /// <remarks>
    /// This class is used to create toggle groups in Unity UI.
    /// It provides methods for registering and unregistering toggle controllers, as well as updating the selection state of toggles in the group.
    /// </remarks>
    
    [DefaultExecutionOrder(-250)]
    public class ToggleGroupController : MonoBehaviour
    {
        [Header("Resources")]
        public List<ToggleController> ToggleControllers = new List<ToggleController>();
        [Header("Events")]
        [SerializeField] public UnityEvent<ToggleController> OnToggleGroupSelectionChanged;
        public event Action<ToggleController> BeforeUpdateToggleGroup;

        void Awake()
        {
            for (int i = 0; i < ToggleControllers.Count; ++i)
            {
                RegisterToggleController(ToggleControllers[i]);
            }
        }

        public void RegisterToggleController(ToggleController toggleController)
        {
            if (toggleController != null)
            {
                toggleController.ToggleGroupController = this;
                //toggleController.OnToggleGroupSelection.AddListener(ProcessToggleGroupSelection);
            }
        }

        public void UnregisterToggleController(ToggleController toggleController)
        {
            if (toggleController != null && ToggleControllers.Contains(toggleController))
            {
                toggleController.ToggleGroupController = null;
                //toggleController.OnToggleGroupSelection.RemoveListener(ProcessToggleGroupSelection);
            }
        }

        public void ProcessToggleGroupSelection(ToggleController toggleController)
        {
            if (BeforeUpdateToggleGroup != null)
            {
                BeforeUpdateToggleGroup(toggleController);
            }
            else
            {
                UpdateToggleGroup(toggleController);
            }
        }


        public ToggleController GetToggleControllerByIndex(int index)
        {
            if (index < 0 || index >= ToggleControllers.Count)
            {
                Debug.LogError($"Index {index} is out of bounds for edition model toggles.");
                return null;
            }
            return ToggleControllers[index];
        }

        public int GetToggleControllerIndex(ToggleController toggleController)
        {
            if (toggleController == null)
            {
                Debug.LogError("ToggleController is null.");
                return -1;
            }
            return ToggleControllers.IndexOf(toggleController);
        }

        public void UpdateToggleGroup(ToggleController newSelectedElement = null)
        {
            for (int i = 0; i < ToggleControllers.Count; ++i)
            {
                var toggleController = ToggleControllers[i];
                if (toggleController == newSelectedElement)
                {
                    toggleController.OnClick(null);
                    continue;
                }
                else
                {
                    if (toggleController.IsSelected)
                    {
                        toggleController.OnDeselect();
                    }
                    toggleController.Normalize();
                }
            }
            OnToggleGroupSelectionChanged?.Invoke(newSelectedElement);
        }
    }
}

