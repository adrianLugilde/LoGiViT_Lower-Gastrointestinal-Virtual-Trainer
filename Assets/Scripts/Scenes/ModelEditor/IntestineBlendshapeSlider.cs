using UnityEngine.EventSystems;
using System;
using UnityEngine;

namespace ModelEditor
{
    /// <summary>
    /// Radial slider for blendshape control. Extends <see cref="SelectableRadialSlider"/> with
    /// blendshape-name parameterized events consumed by <see cref="DynamicBlendshapeSliderController"/>.
    /// </summary>
    public class SplineMeshBlendshapeSlider : SelectableRadialSlider
    {
        protected bool valueChanging = false;
        public string BlendshapeName;
        public Action<string, float> OnSliderValueChangedNotify;
        public Action<string, float, float> OnSliderPointerUpNotify;

        public void Initialize(string blendshapeName, float defaultValue = 0f)
        {
            BlendshapeName = blendshapeName;
            SliderValueRaw = defaultValue;
            UpdateUI();
        }

        /// <inheritdoc/>
        public override void SetSliderValue(float newValue)
        {
            base.SetSliderValue(newValue);
            Debug.Log($"SetSliderValue called for {BlendshapeName} with newValue: {newValue}");
            OnSliderValueChangedNotify?.Invoke(BlendshapeName, newValue);
        }

        public override void OnPointerUp(PointerEventData pointerEventData)
        {
            base.OnPointerUp(pointerEventData);
            if (!IsInteractable) return;
            OnSliderPointerUpNotify?.Invoke(BlendshapeName, SliderValueRaw, GetPreviousValue());
        }

        protected override void HandeSliderInput(PointerEventData eventData, bool allowValueWrap)
        {
            base.HandeSliderInput(eventData, allowValueWrap);
            if (HasValueChanged())
                OnSliderValueChangedNotify?.Invoke(BlendshapeName, SliderValueRaw);
        }
    }
}
