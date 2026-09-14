using SplineMesh;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Endoscope : MonoBehaviour
{
    [Header("Parameters for endoscope camera")]
    public float maxEndoscopeFOV;
    public float minEndoscopeFOV;

    public static Endoscope sharedInstance;

    public List<Rigidbody> cameraRBs;
    public List<Rigidbody> tubeRBs;
    public Camera endoscopeCamera;
    public Light endoscopeLight;

    public CameraState cameraState { get; set; }
    public CameraLimiter cameraLimiter { get; set; }
    public Vector3 originalRelativeCameraPosition;
    public Dictionary<CameraState, Vector3> cameraOffsetDict;
    public Dictionary<CameraLimit, float> cameraLimitDict;

    private void Awake()
    {
        if (sharedInstance == null)
        {
            sharedInstance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ControlledStart()
    {
        enabled = true;
        cameraState = CameraState.Center;
        cameraLimiter = CameraLimiter.None;
        InitializeCameraPositionDict();
        InitializeCameraLimitDict();
        //DESCOMENTAR
        //UserInputController.sharedInstance.ControlledStart();
    }


    private void InitializeCameraPositionDict()
    {
        cameraOffsetDict = new Dictionary<CameraState, Vector3>()
        {
            { CameraState.Center, -10 * originalRelativeCameraPosition},
            { CameraState.PositiveAngulation, new Vector3(0.35f, 0f, 0.35f)},
            { CameraState.NegativeAngulation, new Vector3(-0.35f, 0f, 0.35f)},
            { CameraState.PositiveDisplacement, new Vector3(0f, 0.35f, 0.35f)},
            { CameraState.NegativeDisplacement, new Vector3(0f, -0.35f, 0.35f)},
        };
    }

    private void InitializeCameraLimitDict()
    {
        //TODO que los valores para las key del diccionario se puedan configurar al principio en al conf del endoscopio
        cameraLimitDict = new Dictionary<CameraLimit, float>()
        {
            { CameraLimit.LaxAngularXLimit, 177f},
            { CameraLimit.StrictAngularXLimit, 1f},
            { CameraLimit.LaxAngularYLimit, 177f},
            { CameraLimit.StrictAngularYLimit, 1f},
        };
    }

    public enum CameraLimit
    {
        LaxAngularXLimit,
        StrictAngularXLimit,
        LaxAngularYLimit,
        StrictAngularYLimit

    }

    public void FreezeSegmentRotation(List<Rigidbody> segmentList, bool value)
    {
        foreach (Rigidbody rb in segmentList)
        {
            rb.freezeRotation = value;
        }
    }

    public void ChangeRigidBodyConstrains(List<Rigidbody> segmentList, RigidbodyConstraints constraints)
    {
        foreach (Rigidbody rb in segmentList)
        {
            rb.constraints = constraints;
        }
    }

    public void StopSegment(List<Rigidbody> segmentList)
    {
        foreach (Rigidbody rb in segmentList)
        {
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = Vector3.zero;
        }
    }
    
    public enum CameraLimiter
    {
        None, LimitedAngulation, LimitedDisplacement
    }

    public enum CameraState
    {
        Center = 0,
        PositiveAngulation = 1,
        NegativeAngulation = -1,
        PositiveDisplacement = 2,
        NegativeDisplacement = -2,
    }

    public enum EndoscopeSegment
    {
        Tube,
        Camera
    }
}