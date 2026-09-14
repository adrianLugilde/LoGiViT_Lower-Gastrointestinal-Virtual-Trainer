using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class VRSimpleSplineNavigator : SimpleSplineNavigator
{
    private bool isRightGrabbed = false;
    private bool isLeftGrabbed = false;
    private bool _endoscopeGrabSuppressed = false;
    private Vector3 previousRightControllerPosition;
    private Quaternion previousRightControllerRotation;
    [Header("Controllers")]
    public Transform leftHand;
    public Transform rightHand;
    public CharacterController characterController;
    public float movementDeltaMultipler = 500f;
    public float torqueDeltaMultiplier = 0.5f;
    public float angulationTrackpadDelta = 0.01f;
    public float angulationMultiplier = 10f;
    public float trackpadReleaseDelay = 0.35f;
    public float cameraRotationInputThreshold = 0.05f; //5 percent change

    public bool useStaticInput = true;
    public bool Ready = false;
    public bool useKeyboardInput = false;
    public float fakeGrabMovement = 0.2f;

    public List<InputActionAsset> actionAssets
    {
        get => m_ActionAssets;
        set => m_ActionAssets = value ?? throw new ArgumentNullException(nameof(value));
    }
    [SerializeField]
    List<InputActionAsset> m_ActionAssets;

    private VRUserActions.XRILeftHandInteractionActions leftHandInteractionActions;
    private VRUserActions.XRIRightHandInteractionActions rightHandInteractionActions;
    private VRUserActions.XRILeftHandGuidedInteractionActions leftHandGuidedInteractionActions;
    private VRUserActions.XRIRightHandGuidedInteractionActions rightHandGuidedInteractionActions;
    private float previousTrackpadValue = 0f;
    private float trackpadFixedFrameValueDiff = 0f;
    private bool justReleasedTrackpad = false;

    protected override void Awake()
    {
        float movementMultiplier = PlayerPrefs.GetFloat("VREndoscopeMovementMultiplier");
        if (movementMultiplier != 0)
        {
            movementDeltaMultipler = movementMultiplier;
        }

        float torqueMultiplier = PlayerPrefs.GetFloat("VREndoscopeTorqueMultiplier");
        if (torqueMultiplier != 0)
        {
            torqueDeltaMultiplier = torqueMultiplier;
        }

        if (leftHand == null)
        {
            leftHand = CommonUtils.FindByTagInScene(SceneFlowController.BuildScenes.MainRoom.ToString(), "XRLeftHand").transform;
            if (leftHand == null)
            {
                Debug.LogError("No GameObject with tag 'XRLeftHand' found in the scene.");
                enabled = false;
            }
        }

        if (rightHand == null)
        {
            rightHand = CommonUtils.FindByTagInScene(SceneFlowController.BuildScenes.MainRoom.ToString(), "XRRightHand").transform;
            if (rightHand == null)
            {
                Debug.LogError("No GameObject with tag 'XRRightHand' found in the scene.");
                enabled = false;
            }
        }
        base.Awake();
    }

    protected override void SetActions()
    {
        VRUserActions xriDefaultInputActions = new VRUserActions();
        leftHandInteractionActions = xriDefaultInputActions.XRILeftHandInteraction;
        rightHandInteractionActions = xriDefaultInputActions.XRIRightHandInteraction;
        leftHandGuidedInteractionActions = xriDefaultInputActions.XRILeftHandGuidedInteraction;
        rightHandGuidedInteractionActions = xriDefaultInputActions.XRIRightHandGuidedInteraction;
        rightHandInteractionActions.BasicSelect.Disable();
        leftHandInteractionActions.BasicSelect.Disable();

        leftHandGuidedInteractionActions.Trackpad.performed += OnEndoscopeTipAngulation;
        leftHandGuidedInteractionActions.Trackpad.canceled += OnEndoscopeTipRelease;
        leftHandGuidedInteractionActions.Trigger.performed += ctx => isAngulationLocked = true;
        leftHandGuidedInteractionActions.Trigger.canceled += ctx => isAngulationLocked = false;
        //rightHandGuidedInteractionActions.Trigger.performed += OnRightGrab;
        //rightHandGuidedInteractionActions.Trigger.canceled += OnRightRelease;
        rightHandInteractionActions.Select.performed += OnRightGrab;
        rightHandInteractionActions.Select.canceled += OnRightRelease;

        //leftHandInteractionActions.Select.performed += OnLeftGrab;
        //leftHandInteractionActions.Select.canceled += OnLeftRelease;

        leftHandInteractionActions.Enable();
        rightHandInteractionActions.Enable();
        Ready = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            useKeyboardInput = !useKeyboardInput;
            if (useKeyboardInput)
            {
                Debug.LogWarning("Keyboard controls enabled");
            }
            else
            {
                Debug.LogWarning("Keyboard controls disabled");
            }
        }
        if (Input.GetKeyDown(KeyCode.F11))
        {
            undoAngulations = !undoAngulations;
            if (undoAngulations)
            {
                Debug.LogWarning("Undo Angulations enabled");
            }
            else
            {
                Debug.LogWarning("Undo Angulations disabled");
            }
        }
        if (useKeyboardInput)
        {
            if (Input.GetKey(KeyCode.H))
            {
                cameraMovementInput = new Vector2(0, fakeGrabMovement);
            }
            else if (Input.GetKey(KeyCode.N))
            {
                cameraMovementInput = new Vector2(0, -fakeGrabMovement);
            }
            else
            {
                cameraRollInput = 0;
                cameraMovementInput = Vector2.zero;
            }
            if (Input.GetKey(KeyCode.B))
            {
                cameraRotationInput = new Vector2(0, fakeGrabMovement);
            }
            else if (Input.GetKey(KeyCode.M))
            {
                cameraRotationInput = new Vector2(0, -fakeGrabMovement);
            }
            else
            {
                cameraRotationInput = Vector2.zero;
            }
            if (Input.GetKey(KeyCode.G))
            {
                cameraRollInput = -fakeGrabMovement;
            } else if (Input.GetKey(KeyCode.J))
            {
                cameraRollInput = fakeGrabMovement;
            } else
            {
                cameraRollInput = 0;
            }
            return;
        }
        if (isRightGrabbed && !_endoscopeGrabSuppressed /*&& isLeftGrabbed && characterController.velocity.magnitude < 0.01f*/)
        {
            Vector3 rightControllerDelta = rightHand.position - previousRightControllerPosition;

            Vector3 worldForwardDelta = Vector3.Project(rightControllerDelta, rightHand.forward);
            Vector3 forwardMovement = new Vector3(0, 0, worldForwardDelta.magnitude * Mathf.Sign(Vector3.Dot(rightControllerDelta, rightHand.forward)));

            cameraMovementInput = new Vector2(0f, forwardMovement.z * movementDeltaMultipler);

            //Quaternion rightControllerDeltaRotation = Quaternion.Inverse(previousRightControllerRotation) * rightHand.rotation;

            Vector3 localForward = rightHand.forward;
            Vector3 previousLocalUp = previousRightControllerRotation * Vector3.up;
            Vector3 currentLocalUp = rightHand.rotation * Vector3.up;

            float deltaZSpin = Vector3.SignedAngle(previousLocalUp, currentLocalUp, localForward);
            cameraRollInput = deltaZSpin * torqueDeltaMultiplier;
        }
        else
        {
            cameraRollInput = 0;
            cameraMovementInput = Vector2.zero;
        }
        // Update the previous position and rotation of the right controller
        previousRightControllerPosition = rightHand.position;
        previousRightControllerRotation = rightHand.rotation;
    }

    protected override void GetPitchAndYaw()
    {
        // Calculate the percentage changes
        float deltaPitch = Mathf.Abs(cameraRotationInput.y - pitch) / Mathf.Max(Mathf.Abs(pitch), 1f);
        float deltaYaw = Mathf.Abs(cameraRotationInput.x - yaw) / Mathf.Max(Mathf.Abs(yaw), 1f);

        // Only update pitch if the variation exceeds the threshold percentage
        if (deltaPitch > cameraRotationInputThreshold)
        {
            pitch = -cameraRotationInput.y;
        }

        // Only update yaw if the variation exceeds the threshold percentage
        if (deltaYaw > cameraRotationInputThreshold)
        {
            yaw = cameraRotationInput.x;
        }
    }

    protected override void BottomTopAngulation()
    {
        if (useStaticInput)
        {
            float targetRotation = Mathf.Lerp(-bottomTopAngulationLimit, bottomTopAngulationLimit, (pitch + 1) / 2f);
            float rotationDifference = targetRotation - bottomTopAngulationValue;
            cameraTransform.RotateAround(cameraTransform.position, pitchAxis, rotationDifference);
            bottomTopAngulationValue = targetRotation;
        }
        else
        {
            var newBottomTopAngulationValue = bottomTopAngulationValue + pitch;
            if (!CommonUtils.IsInRange(newBottomTopAngulationValue, -bottomTopAngulationLimit, bottomTopAngulationLimit, true))
            {
                var diff = Mathf.Abs(newBottomTopAngulationValue) - Mathf.Abs(bottomTopAngulationLimit);
                pitch = pitch < 0 ? pitch + diff : pitch - diff;
            }
            bottomTopAngulationValue += pitch;
            cameraTransform.RotateAround(cameraTransform.position, pitchAxis, pitch);
            if (pitch != 0) UpdateCameraPositioDueToAngulation();
        }
    }

    protected override void LeftRightAngulation()
    {
        if (useStaticInput)
        {
            float targetRotation = Mathf.Lerp(-leftRightAngulationLimit, leftRightAngulationLimit, (yaw + 1) / 2f);
            float rotationDifference = targetRotation - leftRightAngulationValue;
            cameraTransform.RotateAround(cameraTransform.position, yawAxis, rotationDifference);
            leftRightAngulationValue = targetRotation;
        }
        else
        {
            var newLeftRightAngulationValue = leftRightAngulationValue + yaw;
            if (!CommonUtils.IsInRange(newLeftRightAngulationValue, -leftRightAngulationLimit, leftRightAngulationLimit, true))
            {
                var diff = Mathf.Abs(newLeftRightAngulationValue) - Mathf.Abs(leftRightAngulationLimit);
                yaw = yaw < 0 ? yaw + diff : yaw - diff;
            }
            leftRightAngulationValue += yaw;
            cameraTransform.RotateAround(cameraTransform.position, yawAxis, yaw);
        }
    }


    private void OnEndoscopeTipAngulation(InputAction.CallbackContext context)
    {
        /*if (isAngulationLocked)
        {
            return;
        }*/
        CancelInvoke("StartUndoAngulation");
        var currentTrackpadValue = context.ReadValue<Vector2>().y;
        if (justReleasedTrackpad)
        {
            previousTrackpadValue = currentTrackpadValue;
            justReleasedTrackpad = false;
        }
        if (Mathf.Abs(currentTrackpadValue - previousTrackpadValue) >= angulationTrackpadDelta)
        {
            var difference = Mathf.Abs(currentTrackpadValue - previousTrackpadValue);
            var direction = Mathf.Sign(currentTrackpadValue - previousTrackpadValue);
            trackpadFixedFrameValueDiff += (int)direction * difference;
        }
        else
        {
            //No hubo cambio mayor que delta, pero hay que mantener la angulación porque no se liberó el trackpad
            //cameraRotationInput a cero para que no angule más, pero keepAngulation para que no se desaga la angulación
            cameraRotationInput = Vector2.zero;
        }
        keepAngulation = true;
        previousTrackpadValue = currentTrackpadValue;

    }

    private void OnEndoscopeTipRelease(InputAction.CallbackContext context)
    {
        /*if (isAngulationLocked)
        {
            return;
        }*/
        cameraRotationInput = Vector2.zero;
        GetPitchAndYaw();
        Invoke("StartUndoAngulation", trackpadReleaseDelay);
        justReleasedTrackpad = true;
    }

    protected override void FixedUpdate()
    {
        if (!useKeyboardInput)
        {
            if (trackpadFixedFrameValueDiff != 0f)
            {
                cameraRotationInput = new Vector2(0, trackpadFixedFrameValueDiff * angulationMultiplier);
                trackpadFixedFrameValueDiff = 0f;
            }
            else
            {
                cameraRotationInput = Vector2.zero;
            }
        }
        base.FixedUpdate();
    }

    private void StartUndoAngulation()
    {
        keepAngulation = false;
    }

    private void OnRightGrab(InputAction.CallbackContext context)
    {
        Debug.LogWarning("OnRightGrab");
        isRightGrabbed = true;
        previousRightControllerPosition = rightHand.position; // Initialize the previous position when grabbing starts
        previousRightControllerRotation = rightHand.rotation; // Initialize the previous rotation when grabbing starts
    }

    private void OnRightRelease(InputAction.CallbackContext context)
    {
        Debug.LogWarning("OnRightRelease");
        isRightGrabbed = false;
    }

    private void OnLeftGrab(InputAction.CallbackContext context)
    {
        isLeftGrabbed = true;
    }

    private void OnLeftRelease(InputAction.CallbackContext context)
    {
        Debug.LogWarning("OnLeftRelease");
        isLeftGrabbed = false;
    }


    private void SetCameraRollInput(InputAction.CallbackContext ctx)
    {
        var value = ctx.ReadValue<Vector2>();
        cameraRollInput = value.x != 0 ? value.x : value.y;
    }

    public override void ToggleMovementInputActions(bool status)
    {
        Debug.LogWarning("ToggleEndoscopeMovementInputActions " + status);
        if (status)
        {
            //leftHandGuidedInteractionActions.Enable();
            leftHandGuidedInteractionActions.Trackpad.Enable();
            leftHandGuidedInteractionActions.Trigger.Enable();
            rightHandGuidedInteractionActions.Trigger.Enable();
            leftHandInteractionActions.Select.Enable();
            rightHandInteractionActions.Select.Enable();

        }
        else
        {
            leftHandGuidedInteractionActions.Trackpad.Disable();
            leftHandGuidedInteractionActions.Trigger.Disable();
            rightHandGuidedInteractionActions.Trigger.Disable();
            leftHandInteractionActions.Select.Disable();
            rightHandInteractionActions.Select.Disable();
        }
    }

    protected void OnEnable()
    {
        //EnableInput();
    }

    protected void OnDisable()
    {
        //DisableInput();
    }

    public void EnableInput()
    {
        if (m_ActionAssets == null)
            return;

        foreach (var actionAsset in m_ActionAssets)
        {
            if (actionAsset != null)
            {
                actionAsset.Enable();
            }
        }
    }

    public void ToggleEndoscopeMovement(bool status)
    {
        ToggleMovementInputActions(status);
        undoAngulations = status;
    }

    public void SetEndoscopeGrabSuppressed(bool suppressed)
    {
        _endoscopeGrabSuppressed = suppressed;
        if (suppressed)
        {
            cameraMovementInput = Vector2.zero;
            cameraRollInput = 0f;
        }
    }


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
}