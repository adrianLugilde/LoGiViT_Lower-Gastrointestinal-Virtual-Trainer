using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace CustomUI
{
    public class RadialSliderController : MonoBehaviour, IInteractableElement, IDragHandler
    {
        [Header("Resources")]
        [SerializeField] private Image sliderImage;
        [SerializeField] private Image selectedSliderImage;
        [SerializeField] private Transform _indicatorPivot;
        [SerializeField] private Transform _selectedIndicatorPivot;
        [SerializeField] private GameObject _labelObject;
        [SerializeField] private LocalizedString _labelText;
        [SerializeField] private LocalizeStringEvent _labelLocalizeStringEvent;
        [SerializeField] private TextMeshProUGUI _valueTextField;
        [SerializeField] private GameObject _iconObject;
        [SerializeField] private Sprite _iconSprite;

        [Header("Settings")]
        public bool IsInteractable { get; set; } = true;
        [SerializeField] public bool UseNameLabel = true;
        [SerializeField] public bool UseValueLabel = true;
        [SerializeField] public bool UseIcon = true;
        public float currentValue = 50.0f;
        public float minValue = 0;
        public float maxValue = 100;
        [Range(0, 8)] public int decimals;
        public bool isPercent;
        public StartPoint startPoint = StartPoint.Left;

        public Action<float> OnValueChangedAction;
        public Action OnPointerEnterAction;
        public Action OnPointerExitAction;

        private GraphicRaycaster graphicRaycaster;
        private RectTransform _hitRectTransform;
        private bool _isPointerDown;
        private float _currentAngle;
        private float _currentAngleOnPointerDown;
        private float _valueDisplayPrecision = 1f;

        private Image _iconImage;

        private TextMeshProUGUI _valueText;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (_iconSprite != null && _iconObject != null)
            {
                _iconImage = _iconObject.GetComponentInChildren<Image>(true);
                if (_iconImage != null)
                {
                    _iconImage.sprite = _iconSprite;
                }
            }

            if (_valueTextField != null)
            {
                _valueTextField.text = string.Format("{0}{1}", currentValue, isPercent ? "%" : "");
            }

            if (sliderImage != null)
            {
                float normalizedValue = (currentValue - minValue) / (maxValue - minValue);
                sliderImage.fillAmount = normalizedValue;
            }

            if(selectedSliderImage != null)
            {
                float normalizedValue = (currentValue - minValue) / (maxValue - minValue);
                selectedSliderImage.fillAmount = normalizedValue;
            }

            if (_indicatorPivot != null)
            {
                float angle = (currentValue - minValue) / (maxValue - minValue) * 360.0f;
                _indicatorPivot.transform.localEulerAngles = new Vector3(180.0f, 0.0f, angle);
            }

            if (_selectedIndicatorPivot != null)
            {
                float angle = (currentValue - minValue) / (maxValue - minValue) * 360.0f;
                _selectedIndicatorPivot.transform.localEulerAngles = new Vector3(180.0f, 0.0f, angle);
            }
        }
#endif


        public enum StartPoint { Left, Right, Top, Down }

        public float SliderAngle
        {
            get { return _currentAngle; }
            set { _currentAngle = Mathf.Clamp(value, 0.0f, 360.0f); }
        }

        // Slider value with applied display precision, i.e. the number of decimals to show.
        public float SliderValue
        {
            get { return Mathf.Round(SliderValueRaw * _valueDisplayPrecision) / _valueDisplayPrecision; }
            set { SliderValueRaw = value; }
        }

        // Raw slider value, i.e. without any display precision applied to its value.
        public float SliderValueRaw
        {
            get { return SliderAngle / 360.0f * maxValue; }
            set { SliderAngle = value * 360.0f / maxValue; }
        }

        private void Awake()
        {
            _valueDisplayPrecision = Mathf.Pow(10, decimals);

            graphicRaycaster = GetComponentInParent<GraphicRaycaster>();

            if (graphicRaycaster == null)
                Debug.LogWarning("<b>[Radial Slider]</b> Could not find GraphicRaycaster component in parent.", this);
            SetupIcon();
            SetupLabel();
            SetupValueField();
        }

        private void SetupIcon()
        {
            if (!UseIcon)
            {
                _iconObject?.SetActive(false);
                return;
            }

            if (_iconObject != null && _iconSprite != null)
            {
                _iconObject.SetActive(true);
                _iconImage = _iconObject.GetComponent<Image>();
                if (_iconImage != null)
                {
                    _iconImage.sprite = _iconSprite;
                }
            }
        }

        private void SetupLabel()
        {
            bool hasLabel = UseNameLabel && _labelText != null;

            if (_labelObject != null)
            {
                _labelObject.SetActive(hasLabel);
            }

            if (hasLabel && _labelLocalizeStringEvent != null)
            {
                _labelLocalizeStringEvent.StringReference = _labelText;
            }
        }

        private void SetupValueField()
        {
            if (!UseValueLabel)
            {
                _valueTextField?.gameObject.SetActive(false);
                return;
            }

            if (_valueTextField != null)
            {
                _valueTextField.gameObject.SetActive(true);
                _valueTextField.text = string.Format("{0}{1}", currentValue, isPercent ? "%" : "");
            }
        }

        private void Start()
        {
            _valueDisplayPrecision = Mathf.Pow(10, decimals);

            SliderAngle = currentValue * 3.6f;

            SliderValue = currentValue;
            OnValueChangedAction?.Invoke(SliderValueRaw);
            UpdateUI();
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            _hitRectTransform = eventData.pointerCurrentRaycast.gameObject.GetComponent<RectTransform>();
            _isPointerDown = true;
            _currentAngleOnPointerDown = SliderAngle;
            HandeSliderInput(eventData, true);
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            if (HasValueChanged()) _hitRectTransform = null;
            _isPointerDown = false;
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            if (currentValue >= minValue) { HandeSliderInput(eventData, false); }
            else if (currentValue <= minValue) { SliderValueRaw = minValue; }
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            OnPointerEnterAction?.Invoke();
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            OnPointerExitAction?.Invoke();
        }

        public void UpdateUI()
        {
            if (SliderValueRaw >= minValue)
            {
                float normalizedAngle = SliderAngle / 360.0f;
                _indicatorPivot.transform.localEulerAngles = new Vector3(180.0f, 0.0f, SliderAngle);
                _selectedIndicatorPivot.transform.localEulerAngles = new Vector3(180.0f, 0.0f, SliderAngle);
                sliderImage.fillAmount = normalizedAngle;
                selectedSliderImage.fillAmount = normalizedAngle;

                _valueTextField.text = string.Format("{0}{1}", SliderValue, isPercent ? "%" : "");
                currentValue = SliderValue;
            }
        }

        protected bool HasValueChanged()
        {
            return SliderAngle != _currentAngleOnPointerDown;
        }

        protected virtual void HandeSliderInput(PointerEventData eventData, bool allowValueWrap)
        {
            if (!_isPointerDown)
                return;

            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_hitRectTransform, eventData.position, eventData.pressEventCamera, out localPos);
            float newAngle = Mathf.Atan2(-localPos.y, localPos.x) * Mathf.Rad2Deg + 180f;

            if (!allowValueWrap)
            {
                _currentAngle = SliderAngle;
                bool needsClamping = Mathf.Abs(newAngle - _currentAngle) >= 180;

                if (needsClamping)
                    newAngle = _currentAngle < newAngle ? 0.0f : 360.0f;
            }

            SliderAngle = newAngle;
            UpdateUI();

            if (HasValueChanged())
                OnValueChangedAction?.Invoke(SliderValueRaw);
        }
    }
}