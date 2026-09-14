using System;
using System.Collections;
using System.Collections.Generic;
using SplineMesh;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PolypTraining
{
    public class InputController : MonoBehaviour, IXRViveDPadInteractor
    {
        [SerializeField] private VRSimpleSplineNavigator _vrSimpleSplineNavigator;
        [SerializeField] private InputActionReference _leftSelectAction;
        [SerializeField] private InputActionReference _rightSelectAction;
        [SerializeField] public List<XRGrabInteractable> GrabbableObjects;
        public GameObject VRLocomotionSystemObject;
        public event Action OnPauseControllerAction;
        public event Action OnToggleNBIAction;
        public event Action OnPolypDetectionAction;
        public event Action OnStartRecording;
        public event Action OnStopRecording;
        public event Action ToggleAITTS;
        public event Action DestkopModeChange;
        public event Action<AudioClip> OnAudioRecordedAction;
        private AudioRecorder _audioRecorder;
        private bool _recordingForAI = false;
        private VRUserActions.XRIRightHandGuidedInteractionActions _rightHandGuidedInteractionActions;
        private VRUserActions.XRILeftHandGuidedInteractionActions _leftHandGuidedInteractionActions;
        public event Action OnDpadPressed;
        public event Action OnDpadReleased;
        private bool _dpadPressed = false;
        private List<InputActionAsset> _actionAssets;
        public List<InputActionAsset> ActionAssets
        {
            get => _actionAssets;
            set => _actionAssets = value ?? throw new ArgumentNullException(nameof(value));
        }

        private void OnEnable()
        {
            //EnableInput();
        }

        public void EnableInput()
        {
            if (_actionAssets == null)
                return;

            foreach (var actionAsset in _actionAssets)
            {
                if (actionAsset != null)
                {
                    actionAsset.Enable();
                }
            }
        }

        private void OnDisable()
        {
            //DisableInput();
        }

        public void DisableInput()
        {
            if (_actionAssets == null)
                return;

            foreach (var actionAsset in _actionAssets)
            {
                if (actionAsset != null)
                {
                    actionAsset.Disable();
                }
            }
        }

        private void Awake()
        {

            /*_vrSimpleSplineNavigator = GetComponentInChildren<VRSimpleSplineNavigator>();
            if (_vrSimpleSplineNavigator == null)
                throw new Exception("VRSimpleSplineNavigator not found in children.");*/

            _audioRecorder = GetComponentInChildren<AudioRecorder>();
            if (_audioRecorder == null)
                throw new Exception("AudioRecorder not found in children.");


            VRLocomotionSystemObject = GameObject.FindGameObjectWithTag("XRLocomotion");

            SubscribeEvents();
            SetInputSystemActions();
        }

        public void InitializeSplineNavigator(Spline spline)
        {
            _vrSimpleSplineNavigator.TargetSpline = spline;
            _vrSimpleSplineNavigator.enabled = true;
        }

        private void SubscribeEvents()
        {
            _audioRecorder.OnAudioRecorded += (clip) => OnAudioRecordedAction?.Invoke(clip);
            GrabbableObjects = new List<XRGrabInteractable>(FindObjectsByType<XRGrabInteractable>());
            for (int i = 0; i < GrabbableObjects.Count; i++)
            {
                GrabbableObjects[i].selectEntered.AddListener((interactor) => _vrSimpleSplineNavigator.SetEndoscopeGrabSuppressed(true));
                GrabbableObjects[i].selectExited.AddListener((interactor) => _vrSimpleSplineNavigator.SetEndoscopeGrabSuppressed(false));
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
            {
                DestkopModeChange?.Invoke();
            }
        }

        private System.Action<InputAction.CallbackContext> _onDpadUpPerf, _onDpadUpCanc, _onDpadDownPerf, _onDpadDownCanc,
            _onDpadLeftHoldPerf, _onDpadLeftHoldCanc, _onDpadRightPerf, _onDpadRightCanc, _onDpadTouched,
            _onLeftBButtonPerf, _onRightBButtonPerf, _onRightTrackpadHoldPerf, _onRightTrackpadHoldCanc, _onRightTrackpadDoubleTapPerf;

        private void SetInputSystemActions()
        {
            VRUserActions xriDefaultInputActions = new VRUserActions();

            _rightHandGuidedInteractionActions = xriDefaultInputActions.XRIRightHandGuidedInteraction;
            _leftHandGuidedInteractionActions = xriDefaultInputActions.XRILeftHandGuidedInteraction;

            //For Vive Pro 2 controllers
            _onDpadUpPerf = ctx => RightDpadUpPressed();
            _onDpadUpCanc = ctx => RightDpadUpReleased();
            _rightHandGuidedInteractionActions.DpadUpPress.performed += _onDpadUpPerf;
            _rightHandGuidedInteractionActions.DpadUpPress.canceled += _onDpadUpCanc;
            _onDpadRightPerf = ctx => RightDpadRightPressed();
            _onDpadRightCanc = ctx => RightDpadRightReleased();
            _rightHandGuidedInteractionActions.DpadRightPress.performed += _onDpadRightPerf;
            _rightHandGuidedInteractionActions.DpadRightPress.canceled += _onDpadRightCanc;
            _onDpadLeftHoldPerf = ctx => RightDpadLeftHold();
            _onDpadLeftHoldCanc = ctx => RightDpadLeftReleased();
            _rightHandGuidedInteractionActions.DpadLeftHold.performed += _onDpadLeftHoldPerf;
            _rightHandGuidedInteractionActions.DpadLeftHold.canceled += _onDpadLeftHoldCanc;
            _onDpadDownPerf = ctx => RightDpadDownPressed();
            _onDpadDownCanc = ctx => RightDpadDownReleased();
            _rightHandGuidedInteractionActions.DpadDownPress.performed += _onDpadDownPerf;
            _rightHandGuidedInteractionActions.DpadDownPress.canceled += _onDpadDownCanc;
            _onDpadTouched = ctx => DpadTouched();
            _rightHandGuidedInteractionActions.DpadTouched.performed += _onDpadTouched;

            //For Valve Index controllers
            //_onLeftBButtonPerf += ctx => OnPausePressed();
            _onLeftBButtonPerf += ctx => OnToggleNBIPressed();
            _leftHandGuidedInteractionActions.BButtonPress.performed += _onLeftBButtonPerf;
            _onRightBButtonPerf += ctx => OnPolypDetectionAction?.Invoke();
            _rightHandGuidedInteractionActions.BButtonPress.performed += _onRightBButtonPerf;
            _onRightTrackpadHoldPerf += ctx => OnAIAssistancePressed();
            _rightHandGuidedInteractionActions.TrackpadHold.performed += _onRightTrackpadHoldPerf;
            _onRightTrackpadHoldCanc += ctx => OnAIAssistanceCanceled();
            _rightHandGuidedInteractionActions.TrackpadHold.canceled += _onRightTrackpadHoldCanc;
            _onRightTrackpadDoubleTapPerf += ctx => OnToggleAITTS();
            _rightHandGuidedInteractionActions.TrackpadDoubleTap.performed += _onRightTrackpadDoubleTapPerf;


            /*_rightHandGuidedInteractionActions.AButtonPress.performed += ctx => OnPauseControllerAction?.Invoke();
            _rightHandGuidedInteractionActions.BButtonPress.performed += ctx => OnPolypDetectionAction?.Invoke();
            _leftHandGuidedInteractionActions.BButtonHold.performed += ctx => OnAIAssistancePressed();
            _leftHandGuidedInteractionActions.BButtonHold.canceled += ctx => OnAIAssistanceCanceled();
            _leftHandGuidedInteractionActions.BButtonDoublePress.performed += ctx => ToggleAITTS?.Invoke();*/


            _rightHandGuidedInteractionActions.Enable();
            _leftHandGuidedInteractionActions.Enable();
        }

        private void DpadTouched()
        {
            VRLocomotionSystemObject.SetActive(false);
            OnDpadPressed?.Invoke();
            StopCoroutine(ReleaseTouchAction());
            StartCoroutine(ReleaseTouchAction());
        }

        private IEnumerator ReleaseTouchAction()
        {
            yield return new WaitForSeconds(0.5f);
            if (_dpadPressed) yield break;
            VRLocomotionSystemObject.SetActive(true);
            OnDpadReleased?.Invoke();
        }

        private void DpadPress()
        {
            VRLocomotionSystemObject.SetActive(false);
            OnDpadPressed?.Invoke();
            _dpadPressed = true;
        }

        private void DpadRelease()
        {
            StopCoroutine(DelayedDpadRelease());
            StartCoroutine(DelayedDpadRelease());
        }

        private IEnumerator DelayedDpadRelease()
        {
            yield return new WaitForSeconds(1f);
            VRLocomotionSystemObject.SetActive(true);
            OnDpadReleased?.Invoke();
            _dpadPressed = false;
        }

        private void RightDpadUpPressed()
        {
            DpadPress();
            OnPausePressed();
        }

        private void RightDpadUpReleased()
        {
            DpadRelease();
        }

        private void RightDpadDownPressed()
        {
            DpadPress();
            OnToggleAITTS();
        }

        private void RightDpadDownReleased()
        {
            DpadRelease();
        }

        private void RightDpadLeftHold()
        {
            DpadPress();
            OnAIAssistancePressed();
        }

        private void RightDpadLeftReleased()
        {
            OnAIAssistanceCanceled();
            DpadRelease();
        }

        private void RightDpadRightPressed()
        {
            DpadPress();
            OnPolypDetectionAction?.Invoke();
        }

        private void RightDpadRightReleased()
        {
            DpadRelease();
        }

        private void OnToggleNBIPressed()
        {
            OnToggleNBIAction?.Invoke();
        }

        private void OnPausePressed()
        {
            OnPauseControllerAction?.Invoke();
        }

        private void OnToggleAITTS()
        {
            ToggleAITTS?.Invoke();
        }

        private void OnAIAssistancePressed()
        {
            _audioRecorder.StartRecording();
            OnStartRecording?.Invoke();
            _recordingForAI = true;
        }

        private void OnAIAssistanceCanceled()
        {
            if (!_recordingForAI) return;
            _audioRecorder.StopRecording();
            OnStopRecording?.Invoke();
            _recordingForAI = false;
        }

        public void SetUseGrabKeybindProfiles(bool useGrab)
        {
            if (_leftSelectAction != null) ApplySelectBindingMask(_leftSelectAction.action, useGrab);
            if (_rightSelectAction != null) ApplySelectBindingMask(_rightSelectAction.action, useGrab);
        }

        private void ApplySelectBindingMask(InputAction selectAction, bool useGrab)
        {
            if (useGrab)
            {
                selectAction.ApplyBindingOverride(0, string.Empty); // disable primaryButton
                selectAction.RemoveBindingOverride(1);               // enable gripButton
            }
            else
            {
                selectAction.RemoveBindingOverride(0);               // enable primaryButton
                selectAction.ApplyBindingOverride(1, string.Empty); // disable gripButton
            }
        }

        public void SetEndoscopeMovementActionsEnable(bool enable)
        {
            _vrSimpleSplineNavigator.ToggleEndoscopeMovement(enable);
        }

        public void SetEndoscopeGrabSuppressed(bool suppressed)
        {
            _vrSimpleSplineNavigator.SetEndoscopeGrabSuppressed(suppressed);
        }

        public void ResetSplineNavigator()
        {
            _vrSimpleSplineNavigator.Reset();
        }

        public void SetInTrainingActionsEnable(bool enable)
        {
            SetPauseActionActive(enable);
            SetPolypDetectionActionActive(enable);
            SetAIAssistanceActionActive(enable);
            SetAITTSToggleActionActive(enable);
        }

        public void SetPauseActionActive(bool enable)
        {
            if (enable)
            {
                _rightHandGuidedInteractionActions.DpadUpPress.Enable();
                _leftHandGuidedInteractionActions.BButtonPress.Enable();
            }
            else
            {
                _rightHandGuidedInteractionActions.DpadUpPress.Disable();
                _leftHandGuidedInteractionActions.BButtonPress.Disable();
            }
        }

        public void SetPolypDetectionActionActive(bool status)
        {
            if (status)
            {
                _rightHandGuidedInteractionActions.DpadRightPress.Enable();
                _rightHandGuidedInteractionActions.BButtonPress.Enable();
            }
            else
            {
                _rightHandGuidedInteractionActions.DpadRightPress.Disable();
                _rightHandGuidedInteractionActions.BButtonPress.Disable();
            }
        }

        public void SetAIAssistanceActionActive(bool enable)
        {
            if (enable)
            {
                _rightHandGuidedInteractionActions.DpadLeftHold.Enable();
                _rightHandGuidedInteractionActions.TrackpadHold.Enable();
            }
            else
            {
                _rightHandGuidedInteractionActions.DpadLeftHold.Disable();
                _rightHandGuidedInteractionActions.TrackpadHold.Disable();
            }
        }

        public void SetAITTSToggleActionActive(bool enable)
        {
            if (enable)
            {
                _rightHandGuidedInteractionActions.DpadDownPress.Enable();
                _rightHandGuidedInteractionActions.TrackpadDoubleTap.Enable();
            }
            else
            {
                _rightHandGuidedInteractionActions.DpadDownPress.Disable();
                _rightHandGuidedInteractionActions.TrackpadDoubleTap.Disable();
            }
        }

        void OnDestroy()
        {
            Debug.Log("InputController OnDestroy");
            OnDpadReleased?.Invoke();
            VRLocomotionSystemObject.SetActive(true);

            _rightHandGuidedInteractionActions.DpadUpPress.performed -= _onDpadUpPerf;
            _rightHandGuidedInteractionActions.DpadUpPress.canceled -= _onDpadUpCanc;
            _rightHandGuidedInteractionActions.DpadRightPress.performed -= _onDpadRightPerf;
            _rightHandGuidedInteractionActions.DpadRightPress.canceled -= _onDpadRightCanc;
            _rightHandGuidedInteractionActions.DpadLeftHold.performed -= _onDpadLeftHoldPerf;
            _rightHandGuidedInteractionActions.DpadLeftHold.canceled -= _onDpadLeftHoldCanc;
            _rightHandGuidedInteractionActions.DpadDownPress.performed -= _onDpadDownPerf;
            _rightHandGuidedInteractionActions.DpadDownPress.canceled -= _onDpadDownCanc;
            _rightHandGuidedInteractionActions.DpadTouched.performed -= _onDpadTouched;

            _leftHandGuidedInteractionActions.BButtonPress.performed -= _onLeftBButtonPerf;
            _rightHandGuidedInteractionActions.BButtonPress.performed -= _onRightBButtonPerf;
            _rightHandGuidedInteractionActions.TrackpadHold.performed -= _onRightTrackpadHoldPerf;
            _rightHandGuidedInteractionActions.TrackpadHold.canceled -= _onRightTrackpadHoldCanc;
            _rightHandGuidedInteractionActions.TrackpadDoubleTap.performed -= _onRightTrackpadDoubleTapPerf;

            for (int i = 0; i < GrabbableObjects.Count; i++)
            {
                GrabbableObjects[i].selectEntered.RemoveListener((interactor) => _vrSimpleSplineNavigator.SetEndoscopeGrabSuppressed(true));
                GrabbableObjects[i].selectExited.RemoveListener((interactor) => _vrSimpleSplineNavigator.SetEndoscopeGrabSuppressed(false));
            }
        }
    }
}
