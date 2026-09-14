using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CustomUI
{
    public class SwitchController : ResponsiveInteractiveElement
    {
        [Header("Settings")]
        [SerializeField] private Animator _animator;

        private bool _isOn;
        public bool IsOn => _isOn;
        public bool StartAsSelected = false;
        public bool SilentStartAsSelected = true;

        private void Start()
        {
            if (StartAsSelected)
            {
                _animator.Play("Switch On");
                if (SilentStartAsSelected)
                {
                    SilentSelect();
                }
                else
                {
                    Select();
                }
            }
        }

        public override void OnSelect()
        {
            _animator.Play("Switch On");
            base.OnSelect();
        }

        public override void OnDeselect()
        {
            _animator.Play("Switch Off");
            base.OnDeselect();
        }

        public override void OnClick(PointerEventData eventData)
        {
            if (_isSelected)
            {
                OnDeselect();
                Normalize();
            }
            else
            {
                Select();
            }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable) return;
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

        public override void OnPointerExit(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            OnHoverExit?.Invoke();
            Normalize();
        }

        protected override IEnumerator NormalizeEffect()
        {
            while (_normalCG.alpha > _normalizedAlpha)
            {
                _normalCG.alpha -= Time.unscaledDeltaTime * _transitionSpeed;
                _highlightedCG.alpha -= Time.unscaledDeltaTime * _transitionSpeed;
                yield return null;
            }

            //_highlightedCG.alpha = 0;
            //_normalCG.alpha = _normalizedAlpha;
            //_highlightedCG.gameObject.SetActive(false);
            //if (!_isSelected) _selectedCG.gameObject.SetActive(false);

            SetCanvasGroupState(_highlightedCG, isVisible: false);
            _normalCG.alpha = _normalizedAlpha;
            if (!_isSelected) SetCanvasGroupState(_selectedCG, isVisible: false);

        }

        protected override IEnumerator HighlightEffect()
        {
            //if (!_isSelected) _selectedCG.gameObject.SetActive(false);
            //_highlightedCG.gameObject.SetActive(true);
            if (!_isSelected) SetCanvasGroupState(_selectedCG, isVisible: false);
            SetCanvasGroupState(_highlightedCG, isVisible: true);

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

        public void Deselect()
        {
            OnDeselect();
            Normalize();
        }
    }
}