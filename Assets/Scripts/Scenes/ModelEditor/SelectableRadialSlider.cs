using System;
using CustomUI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ModelEditor
{
    /// <summary>
    /// Base class for radial sliders that support toggle-based selection and previous-value tracking.
    /// Provides value-change and pointer-up events without blendshape-name awareness.
    /// <para>
    /// <see cref="SplineMeshBlendshapeSlider"/> extends this with blendshape-name parameterized events
    /// used by <see cref="DynamicBlendshapeSliderController"/>.
    /// <see cref="ModelNoiseSettingsController"/> uses this type directly for noise sliders.
    /// </para>
    /// </summary>
    public class SelectableRadialSlider : RadialSliderController
    {
        /// <summary>Toggle that drives selection for VR trackpad input. Auto-resolved in Awake.</summary>
        public ToggleController ToggleController;

        /// <summary>When false, slider starts non-interactable until enabled explicitly.</summary>
        public bool IsOn = false;

        /// <summary>Fired on every drag tick when the slider value changes.</summary>
        public event Action<float> OnSliderValueChanged;

        /// <summary>Fired when the pointer is released. Parameters: newValue, previousValue.</summary>
        public event Action<float, float> OnSliderPointerUp;

        /// <summary>Fired when this component is disabled (e.g. owning panel hidden). Use to auto-clear selection.</summary>
        public Action OnDisableNotified;

        private float _previousValue;

        protected virtual void Awake()
        {
            if (!IsOn) IsInteractable = false;
            ToggleController = GetComponent<ToggleController>();
        }

        /// <summary>Snapshots the current value for later diffing (called on trackpad touch-start).</summary>
        public void StorePreviousValue() => _previousValue = SliderValueRaw;

        /// <summary>Returns the value snapshotted by the last <see cref="StorePreviousValue"/> call.</summary>
        public float GetPreviousValue() => _previousValue;

        /// <summary>Sets the slider value, updates the UI, and fires <see cref="OnSliderValueChanged"/>.</summary>
        public virtual void SetSliderValue(float newValue)
        {
            SliderValueRaw = Mathf.Clamp(newValue, minValue, maxValue);
            UpdateUI();
            OnSliderValueChanged?.Invoke(SliderValueRaw);
        }

        /// <summary>Sets the slider value and updates the UI without firing any events.</summary>
        public void SetSliderValueSilent(float newValue)
        {
            SliderValueRaw = newValue;
            UpdateUI();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _previousValue = SliderValueRaw;
        }

        public override void OnPointerUp(PointerEventData pointerEventData)
        {
            base.OnPointerUp(pointerEventData);
            if (!IsInteractable) return;
            OnSliderPointerUp?.Invoke(SliderValueRaw, _previousValue);
        }

        protected override void HandeSliderInput(PointerEventData eventData, bool allowValueWrap)
        {
            base.HandeSliderInput(eventData, allowValueWrap);
            if (HasValueChanged())
                OnSliderValueChanged?.Invoke(SliderValueRaw);
        }

        private void OnDisable()
        {
            OnDisableNotified?.Invoke();
        }
    }
}
