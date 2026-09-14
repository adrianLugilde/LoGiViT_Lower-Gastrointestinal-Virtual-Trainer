using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Put this on ARM 5 (the screen link). No Rigidbody anywhere in the articulation.
/// Each ARM link must have an ArticulationBody. BASE should be immovable=true.
/// The script auto-builds the chain: ARM1..ARM5 (BASE excluded).
[RequireComponent(typeof(ArticulationBody))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class VRArticulationGrabFollow : MonoBehaviour
{
    [Header("Chain (auto-filled)")]
    public bool autoBuildChain = true;
    [Tooltip("Bodies from first movable (ARM 1) to tip (ARM 5).")]
    public ArticulationBody[] chain;

    [Header("CCD")]
    [Min(1)] public int iterations = 28;
    [Tooltip("Max degrees a joint can rotate per solver step.")]
    public float stepDegrees = 2.0f;
    [Tooltip("Stop when grabbed point is within this distance of the hand (meters).")]
    public float reachTolerance = 0.004f;

    [Header("Drives (applied on Awake)")]
    public float stiffness = 5000f;
    public float damping   = 500f;
    public float forceLimit = 1e6f;

    [Header("Tip twist alignment")]
    [Tooltip("Preserve tip’s relative rotation from the moment of grab (about tip’s hinge axis).")]
    public bool preserveGrabbedRotation = true;
    public float twistStepDegrees = 2.0f;

    // runtime
    private XRSimpleInteractable interactable;
    private IXRSelectInteractor interactor;
    private Transform handAttach;

    // remember where/what we grabbed
    private Vector3 grabbedPointLocal;          // local point on ARM 5 where we grabbed
    private Quaternion tipRotationAtGrab;
    private Quaternion handRotationAtGrab;

    void Reset()       { TryAutoBuildChain(); }
    void OnValidate()  { if (autoBuildChain) TryAutoBuildChain(); }

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);

        if (autoBuildChain || chain == null || chain.Length < 2)
            TryAutoBuildChain();

        // Strengthen drives and ensure only BASE is immovable
        if (chain != null)
        {
            for (int i = 0; i < chain.Length; i++)
            {
                var j = chain[i];
                if (i == 0) j.immovable = false; // ARM 1 must be movable
                var dx = j.xDrive; dx.stiffness = stiffness; dx.damping = damping; dx.forceLimit = forceLimit; j.xDrive = dx;
                var dy = j.yDrive; dy.stiffness = stiffness; dy.damping = damping; dy.forceLimit = forceLimit; j.yDrive = dy;
                var dz = j.zDrive; dz.stiffness = stiffness; dz.damping = damping; dz.forceLimit = forceLimit; j.zDrive = dz;
            }
        }
    }

    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
        }
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        interactor = args.interactorObject as IXRSelectInteractor;
        handAttach = interactor != null
            ? interactor.GetAttachTransform(interactable)
            : args.interactorObject.transform;

        grabbedPointLocal = transform.InverseTransformPoint(handAttach.position);
        tipRotationAtGrab  = transform.rotation;
        handRotationAtGrab = handAttach.rotation;
    }

    void OnRelease(SelectExitEventArgs _)
    {
        interactor = null;
        handAttach = null;
    }

    void FixedUpdate()
    {
        if (handAttach == null || chain == null || chain.Length < 2) return;

        int last = chain.Length - 1;
        Vector3 targetPos = handAttach.position;

        // --- CCD POSITION SOLVE: rotates ARM 1..ARM 4 to bring ARM 5 to target ---
        for (int it = 0; it < iterations; it++)
        {
            Vector3 eePos = chain[last].transform.TransformPoint(grabbedPointLocal);
            if ((targetPos - eePos).sqrMagnitude < reachTolerance * reachTolerance) break;

            for (int i = last - 1; i >= 0; i--)
            {
                var joint = chain[i];

                Vector3 pivot  = joint.transform.position;
                Vector3 axisWS = GetRevoluteAxisWS(joint); // +X of joint space

                Vector3 toEE     = (eePos     - pivot).normalized;
                Vector3 toTarget = (targetPos - pivot).normalized;

                float angle = Vector3.SignedAngle(toEE, toTarget, axisWS);
                angle = Mathf.Clamp(angle, -stepDegrees, stepDegrees);

                var d = joint.xDrive;
                d.target = ClampToLimits(d, d.target + angle);
                joint.xDrive = d;

                eePos = chain[last].transform.TransformPoint(grabbedPointLocal);
            }
        }

        // --- TIP TWIST PRESERVATION (only about ARM 5 hinge axis) ---
        if (preserveGrabbedRotation)
        {
            var tip = chain[last];
            Vector3 axisWS = GetRevoluteAxisWS(tip);

            // desired tip rotation = hand * (tip@grab relative to hand@grab)
            Quaternion relative      = Quaternion.Inverse(handRotationAtGrab) * tipRotationAtGrab;
            Quaternion desiredTipRot = handAttach.rotation * relative;

            float delta = SignedAngleAroundAxis(tip.transform.rotation, desiredTipRot, axisWS);
            delta = Mathf.Clamp(delta, -twistStepDegrees, twistStepDegrees);

            var dTip = tip.xDrive;
            dTip.target = ClampToLimits(dTip, dTip.target + delta);
            tip.xDrive = dTip;
        }
    }

    // ---------- Helpers ----------

    /// Build chain from this link up to (but excluding) the first immovable AB (your BASE).
    void TryAutoBuildChain()
    {
        var tip = GetComponent<ArticulationBody>();
        if (tip == null) return;

        var list = new List<ArticulationBody>();
        // collect upward including this
        for (Transform t = transform; t != null; t = t.parent)
        {
            var ab = t.GetComponent<ArticulationBody>();
            if (ab == null) break;
            list.Add(ab);
            if (ab.immovable) break; // hit BASE
        }

        // We want from first movable to tip; remove BASE if it’s at index last
        if (list.Count > 0 && list[list.Count - 1].immovable)
            list.RemoveAt(list.Count - 1);

        list.Reverse(); // now ARM1..ARM5
        chain = list.ToArray();
    }

    /// World-space hinge axis for a REVOLUTE articulation body (+X in joint space).
    static Vector3 GetRevoluteAxisWS(ArticulationBody j)
    {
        return j.transform.TransformDirection(j.anchorRotation * Vector3.right);
    }

    /// Clamp a drive target to its limits (degrees).
    static float ClampToLimits(ArticulationDrive d, float value)
    {
        return Mathf.Clamp(value, d.lowerLimit, d.upperLimit);
    }

    /// Signed angle from 'from' to 'to' measured around 'axisWS'.
    static float SignedAngleAroundAxis(Quaternion from, Quaternion to, Vector3 axisWS)
    {
        Vector3 fwdA = from * Vector3.forward;
        Vector3 fwdB = to   * Vector3.forward;
        return Vector3.SignedAngle(fwdA, fwdB, axisWS);
    }
}
