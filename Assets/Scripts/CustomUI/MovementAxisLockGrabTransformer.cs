using System.Data;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets
{
    /// <summary>
    /// An XR grab transformer that allows for the locking of specific movement axes. When an object is grabbed and manipulated,
    /// this class ensures that movement are only applied to the specified axes, preserving the initial rotation for the others.
    /// </summary>
    public class MovementAxisLockGrabTransformer : XRBaseGrabTransformer
    {
        [SerializeField]
        [Tooltip("Defines which movement axes are allowed when an object is grabbed. Axes not selected will maintain their initial rotation.")]
        XRGeneralGrabTransformer.ManipulationAxes m_PermittedMovementAxis = XRGeneralGrabTransformer.ManipulationAxes.All;
        [SerializeField]
        [Tooltip("Max offset from initial position (upper bound per axis). Use 0 to lock at initial.")]
        Vector3 m_PermittedMovementOffset = Vector3.zero;
        [SerializeField]
        [Tooltip("Min offset from initial position (lower bound per axis). Use negative values to allow movement below initial (e.g. -0.5 allows 0.5 units down).")]
        Vector3 m_PermittedMovementMinOffset = Vector3.zero;
        [SerializeField]
        [Tooltip("Defines if the initial position of the object should be used as the rest position. If true, the initial position will be used even if grabbing the object instead of the grabbing position.")]
        bool m_StaticPosition = false;
        [SerializeField]
        [Tooltip("Defines if the initial position of the object should be used as the rest position. If true, the initial position will be used even if grabbing the object instead of the grabbing position.")]
        CustomTransformSync m_TransformSync;

        void Awake()
        {
            if (!m_StaticPosition)
                return;

            m_TransformSync ??= GetComponent<CustomTransformSync>();
            if (m_TransformSync != null)
            {
                m_InitialPosition = m_TransformSync.TargetTransform.position;

            }
            else
            {
                Debug.LogWarning("CustomTransformSync component is not assigned. The initial position will not be used as the rest position.");
            }
        }

        /// <inheritdoc />
        protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

        Vector3 m_InitialPosition;


        /// <inheritdoc />
        public override void OnLink(XRGrabInteractable grabInteractable)
        {
            base.OnLink(grabInteractable);
            if (m_StaticPosition == false) m_InitialPosition = grabInteractable.transform.position;
        }

        /// <inheritdoc />
        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            Vector3 newPosition = targetPose.position;

            float ProcessAxis(float value, float initial, float minOffset, float maxOffset, XRGeneralGrabTransformer.ManipulationAxes axis, XRGeneralGrabTransformer.ManipulationAxes permitted)
            {
                if ((permitted & axis) == 0)
                    return initial;
                return Mathf.Clamp(value, initial + minOffset, initial + maxOffset);
            }

            newPosition.x = ProcessAxis(newPosition.x, m_InitialPosition.x, m_PermittedMovementMinOffset.x, m_PermittedMovementOffset.x, XRGeneralGrabTransformer.ManipulationAxes.X, m_PermittedMovementAxis);
            newPosition.y = ProcessAxis(newPosition.y, m_InitialPosition.y, m_PermittedMovementMinOffset.y, m_PermittedMovementOffset.y, XRGeneralGrabTransformer.ManipulationAxes.Y, m_PermittedMovementAxis);
            newPosition.z = ProcessAxis(newPosition.z, m_InitialPosition.z, m_PermittedMovementMinOffset.z, m_PermittedMovementOffset.z, XRGeneralGrabTransformer.ManipulationAxes.Z, m_PermittedMovementAxis);

            targetPose.position = newPosition;
        }
    }
}
