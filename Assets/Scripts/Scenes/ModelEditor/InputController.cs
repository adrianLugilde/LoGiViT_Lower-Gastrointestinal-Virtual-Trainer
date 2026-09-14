// ============================================================================
// InputController.cs
//
// Handles XR input and guided camera navigation for the Model Editor scene.
// Manages controller actions for different edition modes and provides
// internal camera navigation along the intestine spline.
//
// Key Features:
//   - Guided camera locomotion along spline path
//   - XR controller input mapping for different modes
//   - Camera frustum visualization for guided navigation
//   - Locomotion mode switching (walking vs guided camera)
//   - Vive Pro 2 D-pad and Index controller support
//
// Dependencies:
//   - IModelEditorStateProvider: For querying edition mode and spline data
//   - VRUserActions: Generated input action maps
//   - Camera objects for diseases and preview modes
//
// Input Profiles:
//   - Tract Mode: Node manipulation actions
//   - Presets Mode: Preset management actions
//   - Details Mode: Blendshape editing with trackpad
//   - Diseases Mode: Guided camera + disease placement
//   - Preview Mode: Guided camera navigation
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ModelEditor
{
    /// <summary>
    /// Manages XR input and guided camera navigation in the Model Editor.
    /// Handles controller input mapping for different edition modes and
    /// provides internal camera navigation along the intestine spline path.
    /// </summary>
    public class InputController : MonoBehaviour, IXRViveDPadInteractor, IModelEditorInputController
    {
        #region Serialized Fields

        [Header("Cameras")]
        [SerializeField] private GameObject _diseasesGuidedCameraObject;
        [SerializeField] private GameObject _previewGuidedCameraObject;

        [Header("Settings")]
        [SerializeField] private float _movementSensitivity = 0.1f;
        [SerializeField] private float _guidedCameraRateMultiplier = 0.1f;
        [SerializeField] private float _guidedCameraRotationSensitivity = 0.1f;
        [SerializeField] private float _guidedCameraSpinSensitivity = 0.1f;
        [SerializeField] private Vector3 focusElementOffset = Vector3.zero;

        [SerializeField] private List<LineRenderer> _previewGuidedCameraFustrumLineRenderers;
        [SerializeField] private List<LineRenderer> _diseasePlacerGuidedCameraFustrumLineRenderers;

        #endregion

        #region Private Fields - Camera State

        private List<LineRenderer> _currentCameraFustrumLineRenderers;
        private Camera _diseasesGuidedCamera;
        private Camera _previewGuidedCamera;
        private Camera currentCamera;
        private Transform _currentCameraTransform;

        private bool _isGuidedLocomotionEnabled = false;
        private float _diseasesGuidedCameraRate = 0f;
        private float _previewGuidedCameraRate = 0f;

        #endregion

        #region Private Fields - Input State

        private Vector2 _cameraMovementInput = Vector2.zero;
        private Vector2 _cameraRotationInput = Vector2.zero;
        private Vector2 _cameraSpinInput = Vector2.zero;
        private float _cameraRollInput = 0;
        private float _totalPitch = 0f;
        private float _totalYaw = 0f;
        private float _totalRoll = 0f;
        private int _prevTargetSplineIdx = 0;
        public float _lineRenderersScaler = 1f;

        #endregion

        #region Events

        /// <summary>Fired when locomotion mode changes between walking and guided camera.</summary>
        public event Action<bool> LocomotionChanged;
        /// <summary>Fired when D-pad is pressed (for Vive controllers).</summary>
        public event Action OnDpadPressed;
        /// <summary>Fired when D-pad is released.</summary>
        public event Action OnDpadReleased;
        /// <summary>Fired when right hand trackpad provides vertical input for blendshape editing.</summary>
        public event Action<float> OnRightHandTrackpadInput;
        /// <summary>Fired when trackpad touch begins.</summary>
        public event Action RightHandTrackpadTouch;
        /// <summary>Fired when trackpad touch ends.</summary>
        public event Action RightHandTrackpadTouchCanceled;
        public event Action OnToggleNBIAction;

        #endregion
        /// <summary>
        /// State provider for Model Editor queries. 
        /// Replaces legacy Func<> events for cleaner dependency injection.
        /// </summary>
        private IModelEditorStateProvider _stateProvider;

        #region State Provider Helpers

        private bool CheckIsPreviewOrDiseasesMode() => _stateProvider.IsInPreviewOrDiseasesMode;
        private bool CheckIsDiseasesMode() => _stateProvider.IsInDiseasesMode;
        private bool CheckIsPreviewMode() => _stateProvider.IsInPreviewMode;
        private int GetSplineNodeCountFromProvider() => _stateProvider.SplineNodeCount;
        private Quaternion GetSampleRotationFromProvider(float rate) => _stateProvider.GetSampleRotationAtRate(rate);
        private Vector3 GetSampleLocationFromProvider(float rate) => _stateProvider.GetSampleLocationAtRate(rate);

        #endregion


        [SerializeField] private InputActionReference _leftSelectAction;
        [SerializeField] private InputActionReference _rightSelectAction;

        /****************XR****************/
        [SerializeField]
        [Tooltip("Input action assets to affect when inputs are enabled or disabled.")]
        private List<InputActionAsset> m_ActionAssets;
        public List<InputActionAsset> actionAssets
        {
            get => m_ActionAssets;
            set => m_ActionAssets = value ?? throw new ArgumentNullException(nameof(value));
        }
        public GameObject VRLocomotionSystemObject;
        private VRUserActions.XRILeftHandLocomotionActions _leftHandLocomotionActions;
        private VRUserActions.XRILeftHandGuidedLocomotionActions _leftHandGuidedLocomotionActions;
        private VRUserActions.XRIRightHandLocomotionActions _rightHandLocomotionActions;
        private VRUserActions.XRIRightHandGuidedLocomotionActions _rightHandGuidedLocomotionActions;
        private VRUserActions.XRILeftHandGuidedInteractionActions _leftHandGuidedInteractionActions;
        private VRUserActions.XRIRightHandGuidedInteractionActions _rightHandGuidedInteractionActions;
        private VRUserActions.XRIHeadActions _headActions;
        private VRUserActions.XRILeftHandInteractionActions _leftHandInteractionActions;
        private VRUserActions.XRIRightHandInteractionActions _rightHandInteractionActions;

        private bool _dpadPressed = false;


        protected void OnEnable()
        {
            //EnableInput();
        }

        /// <summary>
        /// Enables all input action assets.
        /// Activates controller input processing for the Model Editor.
        /// </summary>
        /// <remarks>
        /// Iterates through all registered action assets and enables them.
        /// Called during initialization or when returning from pause.
        /// </remarks>
        public void EnableInput()
        {
            if (m_ActionAssets == null)
                return;

            foreach (var actionAsset in m_ActionAssets)
            {
                if (actionAsset != null)
                {
                    actionAsset.Enable();
                    //SetActions();
                }
            }
        }

        /// <summary>
        /// Sets the state provider for Model Editor state queries.
        /// Should be called during initialization by ModelEditorManager.
        /// </summary>
        /// <param name="stateProvider">The state provider implementation.</param>
        /// <remarks>
        /// State provider is used to query current edition mode and spline data.
        /// </remarks>
        public void SetStateProvider(IModelEditorStateProvider stateProvider)
        {
            _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
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
                selectAction.ApplyBindingOverride(0, string.Empty);
                selectAction.RemoveBindingOverride(1);
            }
            else
            {
                selectAction.RemoveBindingOverride(0);
                selectAction.ApplyBindingOverride(1, string.Empty);
            }
        }

        protected void OnDisable()
        {
            //DisableInput();
        }

        /// <summary>
        /// Disables all input action assets.
        /// Deactivates controller input processing.
        /// </summary>
        /// <remarks>
        /// Iterates through all registered action assets and disables them.
        /// Called during cleanup or when pausing.
        /// </remarks>
        public void DisableInput()
        {
            if (m_ActionAssets == null)
                return;

            foreach (var actionAsset in m_ActionAssets)
            {
                if (actionAsset != null)
                {
                    actionAsset.Disable();
                }
            }
        }

        private void Awake()
        {
            VRLocomotionSystemObject = GameObject.FindGameObjectWithTag("XRLocomotion");
            SetInputSystemActions();
        }


        public IEnumerator Start()
        {
            _diseasesGuidedCamera = _diseasesGuidedCameraObject.GetComponentInChildren<Camera>(true);
            _previewGuidedCamera = _previewGuidedCameraObject.GetComponentInChildren<Camera>(true);

            yield return new WaitUntil(() => _stateProvider.IsInitialized);
        }

        private System.Action<InputAction.CallbackContext> _onDpadDownPerf, _onDpadDownCanc, _onDpadTouched, _onMoveGuidedCameraPerf,
        _onMoveGuidedCameraCanc, _onRotateGuidedCameraPerf, _onRotateGuidedCameraCanc, _onRollGuidedCameraPerf, _onRollGuidedCameraCanc, _onLeftHandBButtonPerf,
        _onRightHandBButtonPerf, _onRightHandTrackpadPerf, _onRightHandTrackpadTouchPerf, _onRightHandTrackpadTouchCanc;

        //TODO review for new actions
        private void SetInputSystemActions()
        {
            VRUserActions xriDefaultInputActions = new VRUserActions();

            _leftHandGuidedLocomotionActions = xriDefaultInputActions.XRILeftHandGuidedLocomotion;
            _rightHandGuidedLocomotionActions = xriDefaultInputActions.XRIRightHandGuidedLocomotion;
            _leftHandLocomotionActions = xriDefaultInputActions.XRILeftHandLocomotion;
            _rightHandLocomotionActions = xriDefaultInputActions.XRIRightHandLocomotion;
            _leftHandGuidedInteractionActions = xriDefaultInputActions.XRILeftHandGuidedInteraction;
            _rightHandGuidedInteractionActions = xriDefaultInputActions.XRIRightHandGuidedInteraction;
            _headActions = xriDefaultInputActions.XRIHead;
            _leftHandInteractionActions = xriDefaultInputActions.XRILeftHandInteraction;
            _leftHandInteractionActions.Enable();
            _rightHandInteractionActions = xriDefaultInputActions.XRIRightHandInteraction;
            _rightHandInteractionActions.Enable();


            //For Vive Pro 2 controllers
            _onDpadDownPerf = ctx => DpadDownPress(ctx);
            _onDpadDownCanc = ctx => DpadDownRelease(ctx);
            _onDpadTouched = ctx => DpadTouched();
            _leftHandGuidedInteractionActions.DpadDownPress.performed += _onDpadDownPerf;
            _leftHandGuidedInteractionActions.DpadDownPress.canceled += _onDpadDownCanc;
            _leftHandGuidedInteractionActions.DpadTouched.performed += _onDpadTouched;
            _leftHandGuidedInteractionActions.DpadDownPress.Disable();

            //For Valve Index controllers
            _onLeftHandBButtonPerf = ctx => OnToggleNBIPressed();
            _onRightHandBButtonPerf = ctx => OnChangeLocomotionPressed(ctx);
            _leftHandGuidedInteractionActions.BButtonPress.performed += _onLeftHandBButtonPerf;
            _leftHandGuidedInteractionActions.BButtonPress.Disable();
            _rightHandGuidedInteractionActions.BButtonPress.performed += _onRightHandBButtonPerf;
            _rightHandGuidedInteractionActions.BButtonPress.Disable();

            _rightHandInteractionActions.BasicSelect.Disable();
            _leftHandInteractionActions.BasicSelect.Disable();

            _onRightHandTrackpadPerf = ctx => HandleRightHandTrackpad(ctx);
            _rightHandGuidedInteractionActions.Trackpad.performed += _onRightHandTrackpadPerf;
            _onRightHandTrackpadTouchPerf = ctx => HandleRightHandTrackpadTouch(ctx);
            _rightHandGuidedInteractionActions.TrackpadTouch.performed += _onRightHandTrackpadTouchPerf;
            _onRightHandTrackpadTouchCanc = ctx => HandleRightHandTrackpadCanceled(ctx);
            _rightHandGuidedInteractionActions.TrackpadTouch.canceled += _onRightHandTrackpadTouchCanc;
            _rightHandGuidedInteractionActions.Trackpad.Enable();
            _rightHandGuidedInteractionActions.TrackpadTouch.Enable();

            _leftHandGuidedInteractionActions.MouseWheel.performed += ctx => HandleMouseWheel(ctx);
            _leftHandGuidedInteractionActions.MouseWheel.Enable();
            _leftHandGuidedInteractionActions.TestSliders.performed += ctx => HandleSlidersTest(ctx);
            _leftHandGuidedInteractionActions.TestSliders.Enable();

            _onMoveGuidedCameraPerf = ctx => MoveGuidedCamera(ctx);
            _onMoveGuidedCameraCanc = ctx => _cameraMovementInput = Vector2.zero;
            _onRotateGuidedCameraPerf = ctx => _cameraRotationInput = ctx.ReadValue<Vector2>();
            _onRotateGuidedCameraCanc = ctx => _cameraRotationInput = Vector2.zero;
            _onRollGuidedCameraPerf = ctx => _cameraSpinInput = ctx.ReadValue<Vector2>();
            _onRollGuidedCameraCanc = ctx => _cameraSpinInput = Vector2.zero;

            _rightHandGuidedLocomotionActions.MoveGuidedCamera.performed += _onMoveGuidedCameraPerf;
            _rightHandGuidedLocomotionActions.MoveGuidedCamera.canceled += _onMoveGuidedCameraCanc;
            _leftHandGuidedLocomotionActions.RotateGuidedCamera.performed += _onRotateGuidedCameraPerf;
            _leftHandGuidedLocomotionActions.RotateGuidedCamera.canceled += _onRotateGuidedCameraCanc;
            _leftHandGuidedLocomotionActions.RollGuidedCamera.performed += _onRollGuidedCameraPerf;
            _leftHandGuidedLocomotionActions.RollGuidedCamera.canceled += _onRollGuidedCameraCanc;
            SetGuidedCameraLocomotionActive(false);
        }

        private void HandleRightHandTrackpadTouch(InputAction.CallbackContext ctx)
        {
            RightHandTrackpadTouch?.Invoke();
        }

        private void HandleRightHandTrackpad(InputAction.CallbackContext ctx)
        {
            Vector2 input = ctx.ReadValue<Vector2>();
            OnRightHandTrackpadInput?.Invoke(input.y);
        }

        private void HandleRightHandTrackpadCanceled(InputAction.CallbackContext ctx)
        {
            RightHandTrackpadTouchCanceled?.Invoke();
        }

        private void HandleMouseWheel(InputAction.CallbackContext ctx)
        {
            Vector2 input = ctx.ReadValue<Vector2>();
            OnRightHandTrackpadInput?.Invoke(input.y);
        }

        private void HandleSlidersTest(InputAction.CallbackContext ctx)
        {
            Debug.Log($"TestSliders input: {ctx.ReadValue<Vector2>()}");
            Vector2 input = ctx.ReadValue<Vector2>();
            OnRightHandTrackpadInput?.Invoke(input.y);
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
            yield return new WaitForSeconds(ModelEditorConstants.DpadTouchReleaseDelay);
            if (_dpadPressed) yield break;
            VRLocomotionSystemObject.SetActive(true);
            OnDpadReleased?.Invoke();
        }

        private void OnToggleNBIPressed()
        {
            OnToggleNBIAction?.Invoke();
        }

        private void DpadDownPress(InputAction.CallbackContext ctx)
        {
            OnDpadPressed?.Invoke();
            _dpadPressed = true;
            VRLocomotionSystemObject.SetActive(false);
            Debug.Log("DpadDownPress");
            OnChangeLocomotionPressed(ctx);
        }

        private void DpadDownRelease(InputAction.CallbackContext ctx)
        {
            Debug.Log("DpadDownRelease");
            StopCoroutine(DelayedDpadRelease());
            StartCoroutine(DelayedDpadRelease());
        }

        private IEnumerator DelayedDpadRelease()
        {
            yield return new WaitForSeconds(ModelEditorConstants.DpadReleaseDelay);
            _dpadPressed = false;
            OnDpadReleased?.Invoke();
            if (!_isGuidedLocomotionEnabled) VRLocomotionSystemObject.SetActive(true);
        }

        private void MoveGuidedCamera(InputAction.CallbackContext ctx)
        {
            //Debug.Log($"MoveGuidedCamera: {ctx.ReadValue<Vector2>()}");
            if (_dpadPressed)
            {
                Debug.Log("Dpad is pressed, ignoring MoveGuidedCamera input");
                _cameraMovementInput = Vector2.zero;
                return;
            }
            _cameraMovementInput = ctx.ReadValue<Vector2>();
        }

        private void FixedUpdate()
        {
            if (_isGuidedLocomotionEnabled)
            {
                if (_cameraMovementInput != Vector2.zero)
                {
                    MoveCamera();
                }
                if (_cameraRotationInput != Vector2.zero)
                {
                    RotateGuidedCamera();
                }
                if (_cameraSpinInput != Vector2.zero)
                {
                    SpinGuidedCamera();
                }
                if (CheckIsPreviewOrDiseasesMode())
                {
                    DrawFrustum(currentCamera);
                }
            }
        }

        /// <summary>
        /// Centers the camera based on the current edition mode.
        /// </summary>
        /// <remarks>
        /// Behavior varies by mode:
        /// - Diseases: Centers guided camera on spline
        /// - Preview (guided): Centers guided camera on spline
        /// - Preview (normal): Centers editor camera
        /// - Other modes: Centers editor camera
        /// </remarks>
        public void OnCenterCamera()
        {
            if (CheckIsDiseasesMode())
            {
                CenterGuidedCamera();
            }
            else if (CheckIsPreviewMode())
            {
                if (_isGuidedLocomotionEnabled)
                {
                    CenterGuidedCamera();
                }
                else
                {
                    CenterEditorCamera();
                }
            }
            else
            {
                CenterEditorCamera();
            }
        }


        #region TRACT MODE
        /// <summary>
        /// Configures input actions for Tract edition mode.
        /// Enables node manipulation controls.
        /// </summary>
        /// <remarks>
        /// TODO: Currently not implemented.
        /// Should enable grab/select actions for spline node manipulation.
        /// </remarks>
        public void SetTractModeActions()
        {
            Debugger.PrintNotImplemented(this, Debugger.GetCurrentMethodName());
        }
        #endregion

        #region PRESETS MODE
        /// <summary>
        /// Configures input actions for Presets management mode.
        /// Enables preset selection and management controls.
        /// </summary>
        /// <remarks>
        /// TODO: Currently not implemented.
        /// Should enable actions for browsing and selecting presets.
        /// </remarks>
        public void SetPresetsModeManagementActions()
        {
            Debugger.PrintNotImplemented(this, Debugger.GetCurrentMethodName());
        }

        /// <summary>
        /// Configures input actions for Presets edition mode.
        /// Enables node manipulation while editing/creating presets.
        /// </summary>
        /// <remarks>
        /// TODO: Currently not implemented.
        /// Should enable grab/select actions similar to Tract mode.
        /// </remarks>
        public void SetPresetsModeEditionActions()
        {
            Debugger.PrintNotImplemented(this, Debugger.GetCurrentMethodName());
        }
        #endregion

        #region DETAILS MODEL
        /// <summary>
        /// Configures input actions for Details edition mode.
        /// Enables section selection and blendshape editing controls.
        /// </summary>
        /// <remarks>
        /// Sets input to select action mode for ray-based section selection.
        /// Enables trackpad input for blendshape slider adjustment.
        /// </remarks>
        public void SetDetailsModeActions()
        {
            SetSelectActionMode();
        }
        #endregion

        #region DISEASES MODE

        /// <summary>
        /// Configures input actions for Diseases edition mode.
        /// Enables guided camera and disease placement controls.
        /// </summary>
        /// <remarks>
        /// Activates the diseases guided camera for internal navigation
        /// along the spline path for precise disease placement.
        /// </remarks>
        public void SetDiseasesModeActions()
        {
            SetDiseasesGuidedCameraActionsActive(true);
        }

        #region DISEASES GUIDED CAMERA
        /// <summary>
        /// Resets the diseases guided camera's rate (position along spline) to zero.
        /// </summary>
        /// <remarks>
        /// Rate represents the camera's position along the spline path (0 = start).
        /// Called when entering disease mode or when resetting camera position.
        /// </remarks>
        public void ResetDiseasesGuidedCameraRate()
        {
            _diseasesGuidedCameraRate = 0;
        }

        /// <summary>
        /// Resets the diseases guided camera to its initial state.
        /// Resets position, rotation, and rate to starting values.
        /// </summary>
        /// <remarks>
        /// Called when entering or exiting disease mode.
        /// Ensures consistent starting state for disease placement operations.
        /// </remarks>
        public void ResetDiseasesGuidedCamera()
        {
            _diseasesGuidedCameraObject.transform.position = Vector3.zero;
            _diseasesGuidedCameraObject.transform.hasChanged = false;
            ResetDiseasesGuidedCameraRate();
        }

        /// <summary>
        /// Marks the diseases guided camera transform as changed or unchanged.
        /// </summary>
        /// <param name="hasChanged">True if transform has changed, false otherwise.</param>
        /// <remarks>
        /// Used to detect camera movement for disease projection updates.
        /// When true, the disease placer knows to update the projected mesh.
        /// </remarks>
        public void SetDiseasesGuidedCameraTransformHasChanged(bool hasChanged)
        {
            if (hasChanged)
            {
                _diseasesGuidedCamera.transform.hasChanged = true;
            }
            else
            {
                _diseasesGuidedCamera.transform.hasChanged = false;
            }
        }

        /// <summary>
        /// Activates or deactivates the diseases guided camera controls.
        /// </summary>
        /// <param name="isActive">True to enable guided camera, false to disable.</param>
        /// <remarks>
        /// The diseases guided camera allows internal navigation along the spline
        /// for precise disease placement. When active, enables movement, rotation,
        /// and spin controls via VR controllers.
        /// </remarks>
        public void SetDiseasesGuidedCameraActionsActive(bool isActive)
        {
            //Debug.Log($"SetDiseasesGuidedCameraActionsActive: {isActive}");
            SetGuidedCameraActionsActive(isActive, GuidedCamera.DiseasesCamera);
        }
        #endregion
        #endregion

        #region PREVIEW MODE
        /// <summary>
        /// Configures input actions for Preview mode.
        /// Enables guided camera navigation controls.
        /// </summary>
        /// <remarks>
        /// Activates the preview guided camera for internal model inspection.
        /// Similar to diseases mode but without disease placement controls.
        /// </remarks>
        public void SetPreviewModeActions()
        {
            SetPreviewGuidedCameraActionsActive(true);
            _leftHandGuidedInteractionActions.BButtonPress.Enable();
        }

        public void UnsetPreviewModeActions()
        {
            SetPreviewGuidedCameraActionsActive(false);
            _leftHandGuidedInteractionActions.BButtonPress.Disable();
        }

        /// <summary>
        /// Activates or deactivates the preview guided camera controls.
        /// </summary>
        /// <param name="isActive">True to enable guided camera, false to disable.</param>
        /// <remarks>
        /// The preview guided camera provides internal model inspection.
        /// When active, enables movement, rotation, and spin controls.
        /// </remarks>
        public void SetPreviewGuidedCameraActionsActive(bool isActive)
        {
            SetGuidedCameraActionsActive(isActive, GuidedCamera.PreviewCamera);
        }

        #endregion

        #region  COMMON GUIDED CAMERA
        void MoveCamera()
        {
            if (CheckIsDiseasesMode())
            {
                _diseasesGuidedCameraRate = MoveGuidedCamera(_diseasesGuidedCameraRate);
            }
            else if (CheckIsPreviewMode())
            {
                _previewGuidedCameraRate = MoveGuidedCamera(_previewGuidedCameraRate);
            }
        }

        private float MoveGuidedCamera(float cameraRate)
        {
            var splineNodeCount = GetSplineNodeCountFromProvider();
            cameraRate += Time.deltaTime * _movementSensitivity * _cameraMovementInput.y * _guidedCameraRateMultiplier;
            // -2 because last node is the appendix, we don't want to cross it
            if (!CommonUtils.IsInRange(cameraRate, 0, splineNodeCount - ModelEditorConstants.SplineNavigationEndNodeOffset))
            {
                cameraRate = 0f;
            }
            if (cameraRate > splineNodeCount - 1)
                cameraRate += splineNodeCount - 1;
            UpdateGuidedCameraPosition(cameraRate);
            return cameraRate;
        }

        private void RotateGuidedCamera()
        {
            //pitch, yaw, roll
            var pitch = -_cameraRotationInput.y * _guidedCameraRotationSensitivity;
            var yaw = _cameraRotationInput.x * _guidedCameraRotationSensitivity;
            var newPitch = _totalPitch;
            var newYaw = _totalYaw;

            newPitch += pitch;
            newYaw += yaw;

            newPitch = Mathf.Clamp(newPitch, ModelEditorConstants.MinPitchAngle, ModelEditorConstants.MaxPitchAngle);
            newYaw = Mathf.Repeat(newYaw, 360);
            _currentCameraTransform.RotateAround(_currentCameraTransform.position, _currentCameraTransform.right, pitch);
            _currentCameraTransform.RotateAround(_currentCameraTransform.position, _currentCameraTransform.up, yaw);
            _totalPitch += pitch;
            _totalYaw += yaw;
        }

        private void SpinGuidedCamera()
        {
            _cameraRollInput = _cameraSpinInput.y * _guidedCameraSpinSensitivity;
            _currentCameraTransform.RotateAround(_currentCameraTransform.position, _currentCameraTransform.forward, _cameraRollInput);
            _totalRoll += _cameraRollInput;
        }

        private void CenterGuidedCamera()
        {
            UpdateGuidedCameraRotation(currentCamera == _diseasesGuidedCamera ? _diseasesGuidedCameraRate : _previewGuidedCameraRate);
        }

        protected void DrawFrustum(Camera cam)
        {
            //Vector3[] nearCorners = new Vector3[4]; //Approx'd nearplane corners
            Vector3[] farCorners = new Vector3[4]; //Approx'd farplane corners
            Plane[] camPlanes = GeometryUtility.CalculateFrustumPlanes(cam); //get planes from matrix
            Plane temp = camPlanes[1]; camPlanes[1] = camPlanes[2]; camPlanes[2] = temp; //swap [1] and [2] so the order is better for the loop

            for (int i = 0; i < 4; i++)
            {
                //nearCorners[i] = Plane3Intersect(camPlanes[4], camPlanes[i], camPlanes[(i + 1) % 4]); //near corners on the created projection matrix
                farCorners[i] = Plane3Intersect(camPlanes[5], camPlanes[i], camPlanes[(i + 1) % 4]); //far corners on the created projection matrix
                farCorners[i] = Vector3.MoveTowards(cam.transform.position, transform.InverseTransformPoint(farCorners[i]), _lineRenderersScaler);
            }


            int lineRendererIdx = 0;

            for (int i = 0; i < 4; i++)
            {
                _currentCameraFustrumLineRenderers[lineRendererIdx].SetPosition(0, cam.transform.position);
                _currentCameraFustrumLineRenderers[lineRendererIdx++].SetPosition(1, farCorners[i]);
                _currentCameraFustrumLineRenderers[lineRendererIdx].SetPosition(0, farCorners[i]);
                _currentCameraFustrumLineRenderers[lineRendererIdx++].SetPosition(1, farCorners[(i + 1) % 4]);
            }
        }

        private Vector3 Plane3Intersect(Plane p1, Plane p2, Plane p3)
        {
            return ((-p1.distance * Vector3.Cross(p2.normal, p3.normal)) +
                    (-p2.distance * Vector3.Cross(p3.normal, p1.normal)) +
                    (-p3.distance * Vector3.Cross(p1.normal, p2.normal))) /
                (Vector3.Dot(p1.normal, Vector3.Cross(p2.normal, p3.normal)));
        }

        private void SetGuidedCameraActionsActive(bool isActive, GuidedCamera guidedCamera, int targetSplineIdx = -1)
        {
            //Debug.Log($"SetGuidedCameraActionsActive: {isActive} for {guidedCamera}");
            if (isActive)
            {
                EnableGuidedCameraMovement(guidedCamera, targetSplineIdx);
                _leftHandGuidedInteractionActions.DpadDownPress.Enable();
                _rightHandGuidedInteractionActions.BButtonPress.Enable();
            }
            else
            {
                DisableGuidedCameraMovement(guidedCamera);
                _leftHandGuidedInteractionActions.DpadDownPress.Disable();
                _rightHandGuidedInteractionActions.BButtonPress.Disable();
            }
        }

        private void EnableGuidedCameraMovement(GuidedCamera targetGuidedCamera, int targetSplineIdx = -1)
        {
            var cameraConfig = GetGuidedCameraConfig(targetGuidedCamera, targetSplineIdx);
            //Debug.Log($"EnableGuidedCameraMovement: {targetGuidedCamera} from {cameraConfig.Camera.name}");
            SetCurrentCamera(cameraConfig.Camera);

            SetGuidedCameraLocomotionActive(true);
            SetWalkingLocomotionActive(false);

            _currentCameraFustrumLineRenderers = cameraConfig.FustrumLineRenderers;

            //editorCamera.gameObject.SetActive(false);
            cameraConfig.CameraObject.SetActive(true);

            UpdateGuidedCameraBasedOnMode(cameraConfig.CameraRate);

            var rotEuler = _currentCameraTransform.rotation.eulerAngles;
            _totalPitch = rotEuler.x;
            _totalYaw = rotEuler.y;
            _totalRoll = rotEuler.z;
            //ToggleCameraZoomAction(false);
        }

        private void DisableGuidedCameraMovement(GuidedCamera targetGuidedCamera)
        {
            Debug.Log($"DisableGuidedCameraMovement: {targetGuidedCamera}");
            var targetCameraObject = GetGuidedCameraObject(targetGuidedCamera);
            targetCameraObject.SetActive(false);

            //old ChangeLocomotionActions
            SetGuidedCameraLocomotionActive(false);
            SetWalkingLocomotionActive(true);

            _leftHandGuidedInteractionActions.DpadDownPress.Disable();
        }

        private (GameObject CameraObject, Camera Camera, float CameraRate, List<LineRenderer> FustrumLineRenderers) GetGuidedCameraConfig(GuidedCamera targetGuidedCamera, int targetSplineIdx = -1)
        {
            switch (targetGuidedCamera)
            {
                case GuidedCamera.DiseasesCamera:
                    return (
                        _diseasesGuidedCameraObject,
                        _diseasesGuidedCamera,
                        _diseasesGuidedCameraRate,
                        _diseasePlacerGuidedCameraFustrumLineRenderers
                    );
                default:
                    float targetCameraRate;
                    if (targetSplineIdx != -1)
                    {
                        if (_prevTargetSplineIdx != targetSplineIdx)
                        {
                            _prevTargetSplineIdx = targetSplineIdx;
                            targetCameraRate = _previewGuidedCameraRate = targetSplineIdx;
                        }
                        else
                        {
                            targetCameraRate = _previewGuidedCameraRate;
                        }
                    }
                    else
                    {
                        _prevTargetSplineIdx = 0;
                        targetCameraRate = _previewGuidedCameraRate = 0;
                    }

                    return (
                        _previewGuidedCameraObject,
                        _previewGuidedCamera,
                        targetCameraRate,
                        _previewGuidedCameraFustrumLineRenderers
                    );
            }
        }

        private GameObject GetGuidedCameraObject(GuidedCamera targetGuidedCamera)
        {
            return targetGuidedCamera == GuidedCamera.DiseasesCamera
                ? _diseasesGuidedCameraObject
                : _previewGuidedCameraObject;
        }

        private void UpdateGuidedCameraBasedOnMode(float targetCameraRate)
        {
            if (CheckIsDiseasesMode())
            {
                // Offset from 0 to prevent camera at exact spline start
                var cameraRate = targetCameraRate == 0.0f
                    ? targetCameraRate + ModelEditorConstants.DiseasesGuidedCameraStartOffset
                    : targetCameraRate;
                UpdateGuidedCameraPosition(cameraRate);
            }
            else if (CheckIsPreviewMode())
            {
                UpdateGuidedCameraPosition(targetCameraRate);
                UpdateGuidedCameraRotation(targetCameraRate);
            }
        }

        private void SetGuidedCameraLocomotionActive(bool isActive)
        {
            Debug.Log($"SetGuidedCameraLocomotionActive: {isActive}");
            _isGuidedLocomotionEnabled = isActive;
            if (isActive)
            {
                _leftHandGuidedLocomotionActions.Enable();
                _rightHandGuidedLocomotionActions.Enable();
            }
            else
            {
                _leftHandGuidedLocomotionActions.Disable();
                _rightHandGuidedLocomotionActions.Disable();
            }
        }

        private void SetWalkingLocomotionActive(bool isActive)
        {
            Debug.Log($"SetWalkingLocomotionActive: {isActive}");
            VRLocomotionSystemObject.SetActive(isActive);
            if (isActive)
            {
                _leftHandLocomotionActions.Enable();
                _rightHandLocomotionActions.Enable();
            }
            else
            {
                _leftHandLocomotionActions.Disable();
                _rightHandLocomotionActions.Disable();
            }
        }

        private void UpdateGuidedCameraPosition(float cameraRate)
        {
            _currentCameraTransform.parent.position = GetSampleLocationFromProvider(cameraRate);
        }

        private void UpdateGuidedCameraRotation(float cameraRate)
        {
            _currentCameraTransform.rotation = GetSampleRotationFromProvider(cameraRate);
        }

        public enum GuidedCamera
        {
            DiseasesCamera,
            PreviewCamera
        }

        private void SetCurrentCamera(Camera camera)
        {
            //Debug.Log($"SetCurrentCamera: {camera.name}");
            currentCamera = camera;
            _currentCameraTransform = camera.transform;
        }

        public void OnChangeLocomotionPressed(InputAction.CallbackContext ctx)
        {
            ChangeLocomotion();
        }

        public void ChangeLocomotion()
        {
            Debug.Log("ChangeLocomotion");
            SetWalkingLocomotionActive(_isGuidedLocomotionEnabled);
            SetGuidedCameraLocomotionActive(!_isGuidedLocomotionEnabled);
            LocomotionChanged?.Invoke(_isGuidedLocomotionEnabled);
        }
        #endregion

        private void CenterEditorCamera()
        {
            Debugger.PrintNotImplemented(this, Debugger.GetCurrentMethodName());
        }

        /// <summary>
        /// Sets input to select action mode.
        /// Enables ray-based selection instead of grabbing.
        /// </summary>
        /// <remarks>
        /// Used in Details mode for selecting sections to edit.
        /// Disables grab actions on both hands and enables select (ray) interaction.
        /// </remarks>
        //old ChangeSelectActionMode
        public void SetSelectActionMode()
        {
            //TODO maybe not required anymore with the new UI
            /*grabablesHandControllersGO.SetActive(false);
            selectablesHandControllerseGO.SetActive(true);
            xrInputModalityManager.leftController = selectablesLeftHandControllerGO;
            xrInputModalityManager.rightController = selectablesRightHandControllerGO;
            var rayInteractors = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>().ToList();
            rayInteractors.ForEach(i => i.lineBendRatio = 1f);*/
        }

        /// <summary>
        /// Sets input to grab action mode.
        /// Enables direct object grabbing and manipulation.
        /// </summary>
        /// <remarks>
        /// Default mode for Tract and Preset node editing.
        /// Enables grab actions on both hands for direct manipulation of nodes.
        /// </remarks>
        //old ChangeSelectActionMode
        public void SetGrabActionMode()
        {
            //TODO maybe not required anymore with the new UI
            /*grabablesHandControllersGO.SetActive(true);
            selectablesHandControllerseGO.SetActive(false);
            xrInputModalityManager.leftController = grabablesLeftHandControllerGO;
            xrInputModalityManager.rightController = grabablesRightHandControllerGO;
            var rayInteractors = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>().ToList();
            rayInteractors.ForEach(i => i.lineBendRatio = 0.5f);*/
        }

        void OnDestroy()
        {
            SetWalkingLocomotionActive(true);

            _leftHandGuidedInteractionActions.DpadDownPress.performed -= _onDpadDownPerf;
            _leftHandGuidedInteractionActions.DpadDownPress.canceled -= _onDpadDownCanc;
            _leftHandGuidedInteractionActions.DpadTouched.performed -= _onDpadTouched;

            _rightHandGuidedLocomotionActions.MoveGuidedCamera.performed -= _onMoveGuidedCameraPerf;
            _rightHandGuidedLocomotionActions.MoveGuidedCamera.canceled -= _onMoveGuidedCameraCanc;
            _leftHandGuidedLocomotionActions.RotateGuidedCamera.performed -= _onRotateGuidedCameraPerf;
            _leftHandGuidedLocomotionActions.RotateGuidedCamera.canceled -= _onRotateGuidedCameraCanc;
            _leftHandGuidedLocomotionActions.RollGuidedCamera.performed -= _onRollGuidedCameraPerf;
            _leftHandGuidedLocomotionActions.RollGuidedCamera.canceled -= _onRollGuidedCameraCanc;

            _leftHandGuidedInteractionActions.BButtonPress.performed -= _onLeftHandBButtonPerf;
            _rightHandGuidedInteractionActions.BButtonPress.performed -= _onRightHandBButtonPerf;
            _rightHandGuidedInteractionActions.Trackpad.performed -= _onRightHandTrackpadPerf;
            _rightHandGuidedInteractionActions.TrackpadTouch.performed -= _onRightHandTrackpadTouchPerf;
            _rightHandGuidedInteractionActions.TrackpadTouch.canceled -= _onRightHandTrackpadTouchCanc;

        }
    }
}