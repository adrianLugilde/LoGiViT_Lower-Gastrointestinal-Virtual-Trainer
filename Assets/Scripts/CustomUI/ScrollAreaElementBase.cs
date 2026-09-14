using UnityEngine;
using Lean.Gui;
using System;
using UnityEngine.UI;

namespace CustomUI
{
    // Base class for scrollable UI elements that can be used in a scroll area.
    // This class is generic to allow different types of scroll areas and data.
    [Serializable]
    public abstract class ScrollAreaElementBase<TScrollArea, TData> : MonoBehaviour
        where TScrollArea : MonoBehaviour
        where TData : class
    {
        protected TScrollArea ScrollArea;
        [SerializeField] public TData Data;
        [SerializeField] private Outline _outline;
        public Action OnSetAsSelected;
        public Action OnSetAsNotSelected;

        public virtual void Init(TScrollArea scrollArea, TData data)
        {
            this.ScrollArea = scrollArea;
            this.Data = data;
            OnInitComplete();
        }

        public virtual void SetAsSelected()
        {
            Debug.Log("SetAsSelected");
            if (_outline != null) _outline.enabled = true;
            SetSelectedElement(this);
            OnSetAsSelected?.Invoke();
            OnSetAsSelectedComplete();
        }

        public virtual void SetAsNotSelected()
        {
            Debug.Log("SetAsNotSelected");
            if (_outline != null) _outline.enabled = false;
            OnSetAsNotSelected?.Invoke();
            OnSetAsNotSelectedComplete();
            SetSelectedElement(null);
        }

        // Abstract methods to be implemented by derived classes
        public abstract void UpdateContent();
        protected abstract void OnInitComplete();
        protected abstract void SetSelectedElement(ScrollAreaElementBase<TScrollArea, TData> element);
        protected virtual void OnSetAsSelectedComplete() { }
        protected virtual void OnSetAsNotSelectedComplete() { }
    }
}