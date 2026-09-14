using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace CustomUI
{
    /// <summary>
    /// InteractiveElement is a base class for creating interactive UI elements in Unity.
    /// It provides methods for highlighting, selecting, and normalizing the visual state of the element.
    /// </summary>
    /// <remarks>
    /// This class is used to create interactive UI components that can respond to user interactions.
    /// It handles visual transitions and events for hover and selection states.
    /// </remarks>
    public abstract class ResponsiveInteractiveElement : MonoBehaviour, IInteractableElement, ISelectable, IClickable
    {
        [Header("Resources")]
        [SerializeField] protected CanvasGroup _highlightedCG;
        [SerializeField] protected CanvasGroup _selectedCG;
        [SerializeField] protected CanvasGroup _normalCG;
        [SerializeField] protected Sprite _normalIconSprite;
        [SerializeField] protected GameObject _normalIconObject;
        [SerializeField] protected Sprite _highlightIconSprite;
        [SerializeField] protected GameObject _highlightIconObject;
        [SerializeField] protected LocalizeStringEvent _normalLabelLocalizeStringEvent;
        [SerializeField] protected LocalizeStringEvent _highlightLabelLocalizeStringEvent;
        [SerializeField] protected LocalizedString _labelText;
        [SerializeField] protected GameObject _normalLabelObject;
        [SerializeField] protected GameObject _highlightLabelObject;
        [SerializeField] protected LocalizeStringEvent _tooltipLocalizeStringEvent;
        [SerializeField] protected LocalizedString _tooltipText;
        [SerializeField] protected GameObject _tooltipObject;
        [SerializeField] protected AudioClip _selectedSound;
        [SerializeField] protected AudioClip _hoverSound;
        [SerializeField] public HoverGroupController HoverGroupController;


        [Header("Settings")]
        [SerializeField] private bool _isInteractable = true;
        [SerializeField] public bool _isStatePersistent = false;
        public bool IsInteractable 
        { 
            get => _isInteractable; 
            set => _isInteractable = value; 
        }
        [SerializeField] public bool IsDynamic = true;
        [SerializeField] public bool UseLabel = true;
        [SerializeField] public bool UseIcon = true;
        [SerializeField] public bool UseTooltip = true;
        [SerializeField] protected float _transitionSpeed = 8;
        [SerializeField][Range(0, 1)] protected float _normalizedAlpha = 0.5f;
        [SerializeField][Range(0, 1)] protected float _nonInteractableAlpha = 0.25f;
        [SerializeField][Range(0, 2)] protected float _iconSize = 1;

        [Header("Events")]
        [SerializeField] public Action OnHoverEnter;
        [SerializeField] public Action OnHoverExit;
        [SerializeField] public Action OnSelected;
        [SerializeField] public Action OnDeselected;


        private Image _normalIconImage;
        private Image _highlightIconImage;
        private AudioManager _audioManager;


        protected Coroutine _currentTransition;
        protected bool _isSelected;
        public bool IsSelected => _isSelected;



#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (_normalIconSprite != null)
            {
                var images = _normalCG.GetComponentsInChildren<Image>(true);
                if (images.Length > 1)
                {
                    // If there are multiple images, we assume the first is the background and the second the icon.
                    _normalIconImage = images[1];
                    _normalIconImage.sprite = _normalIconSprite;
                }
                else
                {
                    _normalIconImage = images[0];
                    _normalIconImage.sprite = _normalIconSprite;
                }
            }

            if (_highlightIconImage != null)
            {
                _highlightIconImage = _highlightedCG.GetComponentInChildren<Image>(true);
                _highlightIconImage.sprite = _highlightIconSprite;
            }

            if (IsDynamic)
            {
                _normalCG.alpha = _normalizedAlpha;
                _highlightedCG.alpha = 0;
                _selectedCG.alpha = 0;
            }

            if (_normalIconImage != null)
            {
                RectTransform rectTransform = _normalIconImage.rectTransform;
                rectTransform.localScale = Vector3.one * _iconSize;
            }

            if (_highlightIconImage != null)
            {
                RectTransform rectTransform = _highlightIconImage.rectTransform;
                rectTransform.localScale = Vector3.one * _iconSize;
            }
        }
#endif

        protected virtual void Awake()
        {
            HoverGroupController?.RegisterInteractiveElement(this);

            SetupLocalizedText(_tooltipText, _tooltipLocalizeStringEvent, _tooltipObject);
            SetupLocalizedText(_labelText, _normalLabelLocalizeStringEvent, _normalLabelObject);

            SetupIcon();
            SetupLabel();
            SetupTooltip();

            _audioManager = AudioManager.Instance;
        }

        void OnEnable()  { if(!_isStatePersistent) Normalize(); }

        void OnDisable()  { if(!_isStatePersistent) _isSelected = false; }

        private void SetupIcon()
        {
            if (!UseIcon)
            {
                if (_normalIconObject != null) _normalIconObject.SetActive(false);
                if (_highlightIconObject != null) _highlightIconObject.SetActive(false);
                return;
            }

            if (_normalIconObject != null && _normalIconSprite != null)
            {
                _normalIconImage = _normalIconObject.GetComponentInChildren<Image>(true);
                if (_normalIconImage != null)
                    _normalIconImage.sprite = _normalIconSprite;
            }

            if (_highlightIconObject != null)
            {
                _highlightIconImage = _highlightIconObject.GetComponentInChildren<Image>(true);
                if (_highlightIconImage != null)
                    _highlightIconImage.sprite = _highlightIconSprite ?? _normalIconSprite;
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (!UseIcon) return;
            _normalIconSprite = sprite;
            _highlightIconSprite = sprite;
            SetupIcon();
        }

        private void SetupLabel()
        {
            bool hasLabel = UseLabel && _labelText != null;
            if (_normalLabelObject != null) _normalLabelObject?.SetActive(hasLabel);

            if (hasLabel && _normalLabelLocalizeStringEvent != null)
            {
                _normalLabelLocalizeStringEvent.StringReference = _labelText;
            }

            if (_highlightLabelObject != null) _highlightLabelObject?.SetActive(hasLabel);
            if (hasLabel && _highlightLabelLocalizeStringEvent != null)
            {
                _highlightLabelLocalizeStringEvent.StringReference = _labelText;
            }
        }

        public void SetLabel(LocalizedString text)
        {
            if (!UseLabel) return;
            _labelText = text;
            SetupLabel();
        }

        private void SetupTooltip()
        {
            bool hasTooltip = UseTooltip && _tooltipText != null;
            if (_tooltipObject != null) _tooltipObject?.SetActive(hasTooltip);

            if (hasTooltip && _tooltipLocalizeStringEvent != null)
            {
                _tooltipLocalizeStringEvent.StringReference = _tooltipText;
            }
        }

        private void SetupLocalizedText(LocalizedString text, LocalizeStringEvent localizeEvent, GameObject targetObject)
        {
            if (text == null)
            {
                targetObject?.SetActive(false);
            }
            else
            {
                if (localizeEvent != null)
                    localizeEvent.StringReference = text;
            }
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            if (_isSelected) return;
            if (HoverGroupController == null)
            {
                Highlight();
            }
            else
            {
                HoverGroupController.SetGroupHover(this);
            }
            OnHoverEnter?.Invoke();
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            if (_isSelected) return;
            OnHoverExit?.Invoke();
            Normalize();
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            OnClick(eventData);
        }

        public virtual void OnClick(PointerEventData eventData)
        {
            if (_isSelected) return;
            Select();
        }

        public void Highlight()
        {
            if (!IsDynamic) return;
            StopCurrentTransition();
            _currentTransition = StartCoroutine(HighlightEffect());
            //Debug.Log("Highlighting element " + gameObject.name);
            _audioManager?.PlayHoverSound(_hoverSound);
        }

        public void Select()
        {
            OnSelect();
            if (!IsDynamic) return;
            StopCurrentTransition();
            //Debug.Log("Selecting element " + gameObject.name);
            _currentTransition = StartCoroutine(SelectEffect());
        }

        public void SilentSelect()
        {
            _isSelected = true;
            if (!IsDynamic) return;
            _currentTransition = StartCoroutine(SelectEffect());
        }

        public void Normalize()
        {
            if (!IsDynamic) return;
            StopCurrentTransition();
            //Debug.Log("Normalizing element " + gameObject.name);
            if (gameObject.activeInHierarchy) _currentTransition = StartCoroutine(NormalizeEffect());
        }

        protected void StopCurrentTransition()
        {
            if (_currentTransition != null)
                StopCoroutine(_currentTransition);
        }

        protected virtual IEnumerator HighlightEffect()
        {
            //_selectedCG.gameObject.SetActive(false);
            //_highlightedCG.gameObject.SetActive(true);
            //_selectedCG.alpha = 0;
            SetCanvasGroupState(_selectedCG, isVisible: false);

            while (_highlightedCG.alpha < 1)
            {
                _highlightedCG.alpha += Time.unscaledDeltaTime * _transitionSpeed;
                yield return null;
            }

            SetCanvasGroupState(_normalCG, isVisible: true);
            SetCanvasGroupState(_highlightedCG, isVisible: true);
            //_normalCG.alpha = 1;
            //_highlightedCG.alpha = 1;
        }

        protected IEnumerator SelectEffect()
        {
            //_highlightedCG.gameObject.SetActive(false);
            //_selectedCG.gameObject.SetActive(true);
            //_highlightedCG.alpha = 0;
            SetCanvasGroupState(_highlightedCG, isVisible: false);

            while (_selectedCG.alpha < 1)
            {
                _selectedCG.alpha += Time.unscaledDeltaTime * _transitionSpeed;
                yield return null;
            }

            //_selectedCG.alpha = 1;
            SetCanvasGroupState(_selectedCG, isVisible: true);
            //_highlightedCG.alpha = 0;
        }

        protected virtual IEnumerator NormalizeEffect()
        {
            while (_normalCG.alpha > _normalizedAlpha)
            {
                _normalCG.alpha -= Time.unscaledDeltaTime * _transitionSpeed;
                _highlightedCG.alpha -= Time.unscaledDeltaTime * _transitionSpeed;
                yield return null;
            }

            //_highlightedCG.alpha = 0;
            //_selectedCG.alpha = 0;
            SetCanvasGroupState(_highlightedCG, isVisible: false);
            SetCanvasGroupState(_selectedCG, isVisible: false);
            _normalCG.alpha = _normalizedAlpha;
            
            //_highlightedCG.gameObject.SetActive(false);
            //_selectedCG.gameObject.SetActive(false);
        }

        public virtual void OnSelect()
        {
            //Debug.Log("OnSelect: " + name);
            _isSelected = true;
            OnSelected?.Invoke();
            _audioManager?.PlayClickSound(_selectedSound);
        }

        public virtual void OnDeselect()
        {
            _isSelected = false;
            OnDeselected?.Invoke();
        }

        protected void SetCanvasGroupState(CanvasGroup cg, bool isVisible)
        {
            cg.alpha = isVisible ? 1 : 0;
            cg.blocksRaycasts = isVisible;
        }

        public void SetInteractable(bool interactable)
        {
            IsInteractable = interactable;
            _normalCG.alpha = interactable
                ? _normalizedAlpha
                : _nonInteractableAlpha;
        }

        void OnDestroy()
        {
            if (HoverGroupController != null)
            {
                HoverGroupController.UnregisterInteractiveElement(this);
            }
        }        

        public virtual void OnPointerUp(PointerEventData eventData)
        {

        }
    }
}