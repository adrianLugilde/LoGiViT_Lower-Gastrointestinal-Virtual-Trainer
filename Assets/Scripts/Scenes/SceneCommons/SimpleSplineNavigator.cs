using SplineMesh;
using System.Collections.Generic;
using UnityEngine;

public abstract class SimpleSplineNavigator : MonoBehaviour
{
    [SerializeField] public Vector3 prevRotation = Vector3.zero;
    [SerializeField] public Vector3 postRotation = Vector3.zero;

    public Spline TargetSpline;
    [Tooltip("Number of the ending spline nodes excluded from navigation")]
    public int navitationExcludedNodesCount = 0;
    [Header("Navigation camera (local pos and rot must be 0)")]
    public Camera targetCamera;
    public Camera fustrumCamera;
    public float cameraRate = 0.01f;
    public float cameraRateMultiplier = 1f;
    public float movementSensitivity = 1f;
    public float rotationSensitivity = 1f;
    public Vector3 cameraRotationClamp = Vector3.zero;
    [Header("Camera frustrum")]
    public bool drawFrustum = false;
    public List<LineRenderer> fustrumLineRenderers;
    public float lineRenderersScale = 1f;
    [Header("Growing endoscope")]
    public GrowingSplineEndoscope growingSplineEndoscope;
    [Header("Endoscope angulation references")]
    public Transform angulationCore;
    public Transform leftAngulationRef;
    public Transform rightAngulationRef;
    public Transform bottomAngulationRef;
    public Transform topAngulationRef;
    public Transform centerAngulationCameraTarget;
    public Transform bottomAngulationPositionTarget;
    public Transform topAngulationPositionTarget;
    [Header("Endoscope angulation values")]
    public float leftRightAngulationValue = 0;
    public float bottomTopAngulationValue = 0;
    public float mixedAngulationValue = 0;
    public float leftRightAngulationLimit = 125f;
    public float bottomTopAngulationLimit = 125f;
    public float mixedAngulationLimit = 5f;
    public float deangulationMultiplier = 50f;
    public bool undoAngulations = true;
    public bool allowVerticalAngulation = true;
    public bool allowHorizontalAngulation = true;
    public float navigationEndingValue = 0f;
    protected Vector2 cameraMovementInput = Vector2.zero;
    protected Vector2 cameraRotationInput = Vector2.zero;
    protected float cameraRollInput = 0f;
    protected Transform cameraTransform;
    protected Transform cameraParentTransform;
    protected Vector3 pitchAxis;
    protected Vector3 yawAxis;
    protected float pitch;
    protected float yaw;
    public float totalRoll = 0f;
    public float totalRollLimits = 150f;
    public bool isAngulationLocked = false;
    protected bool keepAngulation = false;
    public Vector3 endoscopeForward;
    protected Vector3 prevEndoscopeForward;


    protected virtual void Start()
    {
        if (targetCamera == null) Debug.LogError("TargetCamera missing");
        if (TargetSpline == null) Debug.LogError("TargetSpline missing");
        cameraTransform = targetCamera.transform;
        cameraParentTransform = cameraTransform.parent;
        if (growingSplineEndoscope != null)
        {
            growingSplineEndoscope.targetSpline = TargetSpline;
            growingSplineEndoscope.Init();
        }
        UpdateGuidedCameraPosition();
        UpdateGuidedCameraRotation();
        ComputeNavigationEndingValue();
    }

    protected virtual void Awake()
    {
        SetActions();
    }

    protected abstract void SetActions();

    protected virtual void FixedUpdate()
    {
        if (cameraRollInput != 0f) SpinCamera();
        if (cameraMovementInput != Vector2.zero) MoveCamera();
        if (cameraRotationInput != Vector2.zero) RotateCamera();
        if (undoAngulations && !isAngulationLocked && !keepAngulation) UndoAngulations();
        //if (undoAngulations && !isAngulationLocked) UndoAngulations();
        if (drawFrustum) CameraUtils.DrawFustrum(ref fustrumCamera, ref fustrumLineRenderers, transform, lineRenderersScale);
    }

    protected void CameraRotationCanceled()
    {
        Debug.Log("CameraRotationCanceled");
        cameraRotationInput = Vector2.zero;
        GetPitchAndYaw();
    }

    protected void UpdateEndoscopeForwardVector()
    {
        prevEndoscopeForward = endoscopeForward;
        endoscopeForward = transform.rotation * (TargetSpline.GetSampleAtDistance(cameraRate + 0.25f).location - TargetSpline.GetSampleAtDistance(cameraRate).location);
    }

    public void MoveCamera()
    {
        cameraRate += Time.deltaTime * movementSensitivity * cameraMovementInput.y * cameraRateMultiplier;
        cameraRate = Mathf.Clamp(cameraRate, 0f, navigationEndingValue);
        UpdateGuidedCameraPosition();
    }

    protected void SpinCamera()
    {
        var tempTotalRoll = totalRoll + cameraRollInput;
        if (CommonUtils.IsInRange(tempTotalRoll, -totalRollLimits, totalRollLimits, true))
        {
            cameraTransform.RotateAround(cameraTransform.position, endoscopeForward, cameraRollInput);
            angulationCore.RotateAround(angulationCore.position, endoscopeForward, cameraRollInput);
            totalRoll = tempTotalRoll;
        }
        else
        {
            totalRoll = Mathf.Clamp(totalRoll, -totalRollLimits, totalRollLimits);
        }
    }

    private void UpdateGuidedCameraPosition()
    {
        UpdateEndoscopeForwardVector();
        Vector3 newDirection = Vector3.RotateTowards(angulationCore.forward, endoscopeForward, 360f, 360f);
        angulationCore.rotation = Quaternion.LookRotation(newDirection, angulationCore.up);
        cameraTransform.rotation = Quaternion.LookRotation(newDirection, angulationCore.up);
        cameraTransform.parent.localPosition = TargetSpline.GetSampleAtDistance(cameraRate).location;
        RestorePreviousCameraRotation();
        if (growingSplineEndoscope != null)
        {
            growingSplineEndoscope.SetRate(cameraRate);
        }
    }

    private void UpdateGuidedCameraRotation()
    {
        targetCamera.transform.rotation = TargetSpline.GetSample(cameraRate).Rotation;
    }

    protected virtual void GetPitchAndYaw()
    {
        pitch = -cameraRotationInput.y * rotationSensitivity; //vertical
        yaw = cameraRotationInput.x * rotationSensitivity; //horizontal
    }

    protected virtual void GetAngulationAxes()
    {
        pitchAxis = leftAngulationRef.position - rightAngulationRef.position;
        yawAxis = bottomAngulationRef.position - topAngulationRef.position;
    }

    protected virtual void RotateCamera()
    {
        GetPitchAndYaw();
        GetAngulationAxes();

        /*if (yaw >= pitch && allowHorizontalAngulation)
        {
            if (allowHorizontalAngulation && Mathf.Abs(bottomTopAngulationValue) < mixedAngulationLimit) LeftRightAngulation();
            if (allowVerticalAngulation && Mathf.Abs(leftRightAngulationValue) < mixedAngulationLimit) BottomTopAngulation();
        }
        else if (allowVerticalAngulation)
        {
            if(allowVerticalAngulation && Mathf.Abs(leftRightAngulationValue) < mixedAngulationLimit) BottomTopAngulation();
            if (allowHorizontalAngulation && Mathf.Abs(bottomTopAngulationValue) < mixedAngulationLimit) LeftRightAngulation();
        }*/

        if (allowHorizontalAngulation && !isAngulationLocked && Mathf.Abs(bottomTopAngulationValue) < mixedAngulationLimit)
            LeftRightAngulation();

        if (allowVerticalAngulation && !isAngulationLocked && Mathf.Abs(leftRightAngulationValue) < mixedAngulationLimit)
            BottomTopAngulation();
    }

    protected virtual void BottomTopAngulation()
    {
        var newBottomTopAngulationValue = bottomTopAngulationValue + pitch;
        if (!CommonUtils.IsInRange(newBottomTopAngulationValue, -bottomTopAngulationLimit, bottomTopAngulationLimit, true))
        {
            var diff = Mathf.Abs(newBottomTopAngulationValue) - Mathf.Abs(bottomTopAngulationLimit);
            pitch = pitch < 0 ? pitch + diff : pitch - diff;
        }
        bottomTopAngulationValue += pitch;

        cameraTransform.RotateAround(cameraTransform.position, pitchAxis, pitch);
    }


    protected virtual void UpdateCameraPositioDueToAngulation()
    {
        Transform target = centerAngulationCameraTarget;
        if (bottomTopAngulationValue < 0)
        {
            if (pitch >= 0)
            {
                target = centerAngulationCameraTarget;
            }
            else
            {
                target = topAngulationPositionTarget;
            }
        }
        else if (bottomTopAngulationValue > 0)
        {
            if (pitch <= 0)
            {
                target = centerAngulationCameraTarget;
            }
            else
            {
                target = bottomAngulationPositionTarget;
            }
        }

        float normalizedAngulation;
        if (target == centerAngulationCameraTarget)
        {
            if (bottomTopAngulationValue < 0)
            {
                normalizedAngulation = Mathf.InverseLerp(-bottomTopAngulationLimit, 0, bottomTopAngulationValue);
            }
            else
            {
                normalizedAngulation = Mathf.InverseLerp(bottomTopAngulationLimit, 0, bottomTopAngulationValue);
            }
        }
        else
        {
            normalizedAngulation = Mathf.InverseLerp(0, bottomTopAngulationLimit, Mathf.Abs(bottomTopAngulationValue));
        }


        cameraTransform.position = Vector3.Lerp(cameraTransform.position, target.position, normalizedAngulation);
    }


    protected virtual void LeftRightAngulation()
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

    private void RestorePreviousCameraRotation()
    {
        GetAngulationAxes();
        if (yaw >= pitch)
        {
            if (Mathf.Abs(bottomTopAngulationValue) < mixedAngulationLimit) cameraTransform.RotateAround(cameraTransform.position, yawAxis, leftRightAngulationValue);
            if (Mathf.Abs(leftRightAngulationValue) < mixedAngulationLimit) cameraTransform.RotateAround(cameraTransform.position, pitchAxis, bottomTopAngulationValue);
        }
        else
        {
            if (Mathf.Abs(leftRightAngulationValue) < mixedAngulationLimit) cameraTransform.RotateAround(cameraTransform.position, pitchAxis, bottomTopAngulationValue);
            if (Mathf.Abs(bottomTopAngulationValue) < mixedAngulationLimit) cameraTransform.RotateAround(cameraTransform.position, yawAxis, leftRightAngulationValue);
        }
    }

    private void UndoAngulations()
    {
        if (pitch == 0 && bottomTopAngulationValue != 0)
        {
            UndoAxisAngulation(ref bottomTopAngulationValue, pitchAxis);
            UpdateCameraPositioDueToAngulation();
        }
        /*if (yaw == 0)
        {
            UndoAxisAngulation(ref leftRightAngulationValue, yawAxis);
        }*/
    }

    private void UndoAxisAngulation(ref float targetAngulationValue, Vector3 targetAxis)
    {
        var sign = Mathf.Sign(targetAngulationValue);
        var deangulation = Time.deltaTime * deangulationMultiplier;
        var unsignedBottomTopAngulationValue = Mathf.Abs(targetAngulationValue);
        var newValue = unsignedBottomTopAngulationValue - deangulation;
        if (newValue < 0)
        {//recalculate new value and angulation if going further than zero
            deangulation += newValue;
            //newValue = bottomTopAngulationValue - (sign * deangulation);
            newValue = unsignedBottomTopAngulationValue - deangulation;
        }
        var signedDeangulation = -sign * deangulation;
        targetAngulationValue = sign * newValue;
        cameraTransform.RotateAround(cameraTransform.position, targetAxis, signedDeangulation);
    }

    public void Reset()
    {
        cameraRate = 0.01f;
        totalRoll = 0f;
        bottomTopAngulationValue = 0f;
        leftRightAngulationValue = 0f;
        pitch = 0f;
        yaw = 0f;
        cameraRollInput = 0f;
        cameraRotationInput = Vector2.zero;
        cameraMovementInput = Vector2.zero;
        UpdateGuidedCameraPosition();
        UpdateGuidedCameraRotation();
    }

    private void ComputeNavigationEndingValue()
    {
        TargetSpline.RefreshCurves();
        if (navitationExcludedNodesCount == 0)
        {
            navigationEndingValue = TargetSpline.Length;
        }
        else
        {
            navigationEndingValue = TargetSpline.Length - TargetSpline.curves[navitationExcludedNodesCount].Length;
        }
        Debug.LogWarning($"Navigation ending value: {navigationEndingValue} of {TargetSpline.Length}");
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        var direction = TargetSpline.GetSampleAtDistance(cameraRate).location - TargetSpline.GetSampleAtDistance(cameraRate + 0.25f).location;
        Debug.DrawRay((transform.rotation * TargetSpline.GetSampleAtDistance(cameraRate).location) + transform.position, -direction, Color.yellow);
        var pitchAxis = topAngulationRef.position - bottomAngulationRef.position;
        Debug.DrawRay(topAngulationRef.position, pitchAxis, Color.blue);
#endif
    }

    public abstract void ToggleMovementInputActions(bool status);
}