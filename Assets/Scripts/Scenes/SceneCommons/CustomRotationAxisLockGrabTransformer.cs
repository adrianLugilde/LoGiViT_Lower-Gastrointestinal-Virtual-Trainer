using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets
{
    /// <summary>
    /// An XR grab transformer that allows for the locking of specific rotation axes. When an object is grabbed and manipulated,
    /// this class ensures that rotations are only applied to the specified axes, preserving the initial rotation for the others.
    /// Permitted axes can optionally be clamped to a range relative to initial rotation.
    /// </summary>
    public class CustomRotationAxisLockGrabTransformer : XRBaseGrabTransformer
    {
        [SerializeField]
        [Tooltip("Defines which rotation axes are allowed when an object is grabbed. Axes not selected will maintain their initial rotation.")]
        XRGeneralGrabTransformer.ManipulationAxes m_PermittedRotationAxis = XRGeneralGrabTransformer.ManipulationAxes.All;
        [SerializeField]
        [Tooltip("Max rotation offset in degrees from initial rotation per axis (counterclockwise). Use 180 for no limit.")]
        Vector3 m_PermittedRotationMaxOffset = new(180f, 180f, 180f);
        [SerializeField]
        [Tooltip("Min rotation offset in degrees from initial rotation per axis (clockwise, use negative values). Use -180 for no limit.")]
        Vector3 m_PermittedRotationMinOffset = new(-180f, -180f, -180f);

        /// <inheritdoc />
        protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

        Vector3 m_InitialEulerRotation;

        /// <inheritdoc />
        public override void OnLink(XRGrabInteractable grabInteractable)
        {
            base.OnLink(grabInteractable);
            m_InitialEulerRotation = grabInteractable.transform.rotation.eulerAngles;
        }

        /// <inheritdoc />
        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            Vector3 newRotationEuler = targetPose.rotation.eulerAngles;

            static float ProcessAxis(float angle, float initial, float minOffset, float maxOffset, XRGeneralGrabTransformer.ManipulationAxes axis, XRGeneralGrabTransformer.ManipulationAxes permitted)
            {
                if ((permitted & axis) == 0)
                    return initial;
                float delta = Mathf.DeltaAngle(initial, angle);
                delta = Mathf.Clamp(delta, minOffset, maxOffset);
                return initial + delta;
            }

            newRotationEuler.x = ProcessAxis(newRotationEuler.x, m_InitialEulerRotation.x, m_PermittedRotationMinOffset.x, m_PermittedRotationMaxOffset.x, XRGeneralGrabTransformer.ManipulationAxes.X, m_PermittedRotationAxis);
            newRotationEuler.y = ProcessAxis(newRotationEuler.y, m_InitialEulerRotation.y, m_PermittedRotationMinOffset.y, m_PermittedRotationMaxOffset.y, XRGeneralGrabTransformer.ManipulationAxes.Y, m_PermittedRotationAxis);
            newRotationEuler.z = ProcessAxis(newRotationEuler.z, m_InitialEulerRotation.z, m_PermittedRotationMinOffset.z, m_PermittedRotationMaxOffset.z, XRGeneralGrabTransformer.ManipulationAxes.Z, m_PermittedRotationAxis);

            targetPose.rotation = Quaternion.Euler(newRotationEuler);
        }
    }
}
