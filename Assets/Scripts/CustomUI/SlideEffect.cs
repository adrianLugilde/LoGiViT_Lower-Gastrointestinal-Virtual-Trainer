using UnityEngine;
using UnityEngine.Events;

namespace CustomUI
{
    /// <summary>
    /// SlideEffect is a Unity component that provides slide-in and slide-out animations for UI elements.
    /// It uses an Animator to control the animations and provides events for when the element slides in or out.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class SlideEffect : MonoBehaviour
    {
        public UnityEvent OnSlideIn;
        public UnityEvent OnSlideOut;
        private Animator _animator;
        private bool _isActive = false;

        void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }
        }

        public void SlideIn()
        {
            if (_isActive) return;
            _isActive = true;
            _animator.Play("In");
            OnSlideIn?.Invoke();
        }

        public void SlideOut()
        {
            if (!_isActive) return;
            _isActive = false;
            _animator.Play("Out");
            OnSlideOut?.Invoke();
        }
    }
}
