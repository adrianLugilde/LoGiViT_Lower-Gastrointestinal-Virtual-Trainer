using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LoGiViT.ModelEditor
{
    /// <summary>
    /// Mirrors the world-space rotation delta of a grabbed source transform onto a target transform,
    /// with optional per-axis inversion. Assign an XRGrabInteractable to auto-wire grab events.
    /// Call <see cref="RestoreRotation"/> to reset both source and target to their original rotations.
    /// </summary>
    public class XRGrabRotationFollower : MonoBehaviour
    {
        [Tooltip("The object whose rotation is read (e.g. the manipulation handle).")]
        [SerializeField] private Transform source;

        [Tooltip("The object whose rotation is driven (e.g. the generated intestine model root).")]
        [SerializeField] private Transform target;

        [Tooltip("Optional. When assigned, BeginFollow/EndFollow are wired to selectEntered/selectExited automatically.")]
        [SerializeField] private XRGrabInteractable grabInteractable;

        [Header("Axis Inversion")]
        [Tooltip("Invert the X rotation axis before applying to target.")]
        [SerializeField] private bool invertX = true;

        [Tooltip("Invert the Y rotation axis before applying to target.")]
        [SerializeField] private bool invertY = true;

        [Tooltip("Invert the Z rotation axis before applying to target.")]
        [SerializeField] private bool invertZ = true;

        private Quaternion _originalTargetRotation;
        private Quaternion _originalSourceRotation;
        private Quaternion _sourceRefRotation;
        private Quaternion _targetRefRotation;
        private bool _isFollowing;

        private void Awake()
        {
            if (target != null)
                _originalTargetRotation = target.localRotation;
            if (source != null)
                _originalSourceRotation = source.localRotation;
        }

        private void Start()
        {
            if (source == null || target == null) return;
            _sourceRefRotation = source.rotation;
            _targetRefRotation = target.rotation;
            _isFollowing = true;
        }

        private void OnEnable()
        {
            if (grabInteractable == null) return;
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            if (grabInteractable == null) return;
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        private void OnSelectEntered(SelectEnterEventArgs _) => BeginFollow();
        private void OnSelectExited(SelectExitEventArgs _) => EndFollow();

        // LateUpdate runs after XRGrabInteractable sets source rotation in Update.
        private void LateUpdate()
        {
            if (!_isFollowing || source == null || target == null) return;
            ApplyDelta();
        }

        /// <summary>
        /// Captures world-space reference rotations so the delta starts at identity —
        /// avoids snap from parent offsets or XR attach-point alignment.
        /// Called automatically via grabInteractable if assigned.
        /// </summary>
        public void BeginFollow()
        {
            if (source == null || target == null) return;
            _sourceRefRotation = source.rotation;
            _targetRefRotation = target.rotation;
            _isFollowing = true;
        }

        /// <summary>Target stays at current rotation. Called automatically via grabInteractable if assigned.</summary>
        public void EndFollow()
        {
            _isFollowing = false;
        }

        /// <summary>Matches target rotation to source rotation immediately (requires BeginFollow called first).</summary>
        public void ApplyRotation()
        {
            if (source == null || target == null) return;
            ApplyDelta();
        }

        private void ApplyDelta()
        {
            Quaternion delta = source.rotation * Quaternion.Inverse(_sourceRefRotation);
            delta = ApplyInversion(delta);
            target.rotation = delta * _targetRefRotation;
        }

        private Quaternion ApplyInversion(Quaternion q)
        {
            if (invertX) q = InvertAxis(q, Vector3.right);
            if (invertY) q = InvertAxis(q, Vector3.up);
            if (invertZ) q = InvertAxis(q, Vector3.forward);
            return q;
        }

        // Swing-twist decomposition around the given world axis.
        // Splits q into swing (no component around axis) * twist (rotation around axis only),
        // then returns swing * Inverse(twist) — inverting only that axis.
        private static Quaternion InvertAxis(Quaternion q, Vector3 axis)
        {
            Vector3 proj = Vector3.Project(new Vector3(q.x, q.y, q.z), axis);
            Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, q.w).normalized;
            Quaternion swing = q * Quaternion.Inverse(twist);
            return swing * Quaternion.Inverse(twist);
        }

        /// <summary>Restores both source and target to their original rotations, then resets follow references.</summary>
        public void RestoreRotation()
        {
            if (source != null) source.localRotation = _originalSourceRotation;
            if (target != null) target.localRotation = _originalTargetRotation;
            if (source != null) _sourceRefRotation = source.rotation;
            if (target != null) _targetRefRotation = target.rotation;
        }

        /// <summary>Replaces the stored original rotations with the current source and target rotations.</summary>
        public void BakeCurrentAsOriginal()
        {
            if (source != null) _originalSourceRotation = source.localRotation;
            if (target != null) _originalTargetRotation = target.localRotation;
        }
    }
}
