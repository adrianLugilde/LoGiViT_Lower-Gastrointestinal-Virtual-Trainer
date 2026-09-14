using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
#if UNITY_XR_MANAGEMENT
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif

namespace CustomUI
{
    /// <summary>
    /// This class is used to create a hover sway effect on UI elements.
    /// </summary>
    /// <remarks>
    /// The hover sway effect is applied to the specified RectTransform when the mouse hovers over it.
    /// The effect can be customized with various settings such as smoothness and world space displacement factor.
    /// </remarks>  
    public class HoverSwayEffect : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private RectTransform swayObject;
        [SerializeField] private Camera sourceCamera = null;
        [SerializeField] private HoverGroupController swayGroupController;
        [Header("Settings")]
        [SerializeField] private float smoothness = 10;
        [SerializeField] private float worldSpaceDisplacementFactor = 0.15f;
        [SerializeField] private bool defaultSubscribeToInteractiveElement = true;

        private bool allowSway = false;
        private bool xrEnabled = false;
        private Vector3 cursorPos;
        private Vector2 defaultPos;
        private Camera activeCamera;

#if UNITY_XR_MANAGEMENT
        public bool isHovered => isXRHovering;
        public XRInteractionManager interactionManager { get; set; }
        public System.Collections.Generic.List<IXRHoverInteractor> interactorsHovering { get; } = new System.Collections.Generic.List<IXRHoverInteractor>();
        
        public bool IsHoverableBy(IXRHoverInteractor interactor) => true;
        public void OnHoverEntered(HoverEnterEventArgs args) {
            isXRHovering = true;
            allowSway = true;
            swayParent.DissolveAll(this);
        }
        public void OnHoverExited(HoverExitEventArgs args) {
            isXRHovering = false;
            allowSway = false;
            swayParent.HighlightAll();
        }
        public void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase) { }
        public Transform GetAttachTransform(IXRInteractor interactor) => transform;
#endif

        void OnEnable()
        {
#if UNITY_XR_MANAGEMENT
        InputDevices.deviceConnected += OnDeviceConnected;
        InputDevices.deviceDisconnected += OnDeviceDisconnected;

        CheckXRDevices();
#endif
        }

        void OnDisable()
        {
#if UNITY_XR_MANAGEMENT
        InputDevices.deviceConnected -= OnDeviceConnected;
        InputDevices.deviceDisconnected -= OnDeviceDisconnected;
#endif
        }
#if UNITY_XR_MANAGEMENT
    private void OnDeviceConnected(InputDevice device)
    {
        if (device.isValid)
            xrEnabled = true;
    }

    private void OnDeviceDisconnected(InputDevice device)
    {
        CheckXRDevices();
    }

    private void CheckXRDevices()
    {
        if (UnityEngine.XR.Management.XRGeneralSettings.Instance != null &&
            UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null &&
            UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.isInitializationComplete)
        {
            var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevices(devices);
            xrEnabled = devices.Exists(device => device.isValid);
        }
        else
        {
            xrEnabled = false;
        }
    }
#endif

        void Awake()
        {
            defaultPos = swayObject.anchoredPosition;
            if (defaultSubscribeToInteractiveElement)
            {
                var interactiveElement = GetComponent<ResponsiveInteractiveElement>();
                if (interactiveElement != null)
                {
                    interactiveElement.OnHoverEnter += EnableSway;
                    interactiveElement.OnHoverExit += DisableSway;
                    interactiveElement.OnSelected += DisableSway;
                }
                else
                {
                    Debug.LogWarning("HoverSwayEffect requires an InteractiveElement component to subscribe by default hover events");
                }
            }
            //TODO maybe add auto register to hover events if interactive element is present in the game object
        }

        void Start()
        {
            SetupCamera();
        }

        void SetupCamera()
        {
            if (sourceCamera != null)
            {
                activeCamera = sourceCamera;
            }
            else
            {
                activeCamera = Camera.main;
            }
        }

        void Update()
        {
            if (!xrEnabled)
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                if (allowSway) { cursorPos = Input.mousePosition; }
#elif ENABLE_INPUT_SYSTEM
                if (allowSway) { cursorPos = Mouse.current.position.ReadValue(); }
#endif
            }
#if UNITY_XR_MANAGEMENT
        else if (allowSway && isXRHovering && interactorsHovering.Count > 0)
        {
            if (interactorsHovering[0] is XRRayInteractor rayInteractor)
            {
                if (rayInteractor.TryGetHitInfo(out Vector3 position, out Vector3 normal, out int positionInLine, out bool isValidTarget))
                {
                    cursorPos = position;
                }
            }
        }
#endif      

            if (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay) { ProcessOverlay(); }
            else if (mainCanvas.renderMode == RenderMode.ScreenSpaceCamera) { ProcessSSC(); }
            else if (mainCanvas.renderMode == RenderMode.WorldSpace) { ProcessWorldSpace(); }
        }

        void ProcessOverlay()
        {
            if (allowSway) { swayObject.position = Vector2.Lerp(swayObject.position, cursorPos, Time.deltaTime * smoothness); }
            else { swayObject.localPosition = Vector2.Lerp(swayObject.localPosition, defaultPos, Time.deltaTime * smoothness); }
        }

        void ProcessSSC()
        {
            if (allowSway) { swayObject.position = Vector2.Lerp(swayObject.position, activeCamera.ScreenToWorldPoint(cursorPos), Time.deltaTime * smoothness); }
            else { swayObject.localPosition = Vector2.Lerp(swayObject.localPosition, defaultPos, Time.deltaTime * smoothness); }
        }


        //Only for World Space Canvas is XR considered, as is the only one that can be used with a ray interactor
        void ProcessWorldSpace()
        {
            if (allowSway)
            {
                Vector3 clampedPos = new Vector3(cursorPos.x, cursorPos.y, (mainCanvas.transform.position.z / 6f));
                Vector3 targetWorldPos = activeCamera.ScreenToWorldPoint(clampedPos);

                // Calculate direction and limit displacement
                Vector3 originalPos = transform.TransformPoint(defaultPos);
                Vector3 direction = targetWorldPos - originalPos;
                float distance = direction.magnitude;

                // Limit the distance to maxWorldSpaceDisplacement
                if (distance > worldSpaceDisplacementFactor)
                {
                    direction = direction.normalized * worldSpaceDisplacementFactor;
                    targetWorldPos = originalPos + direction;
                }

                swayObject.position = Vector3.Lerp(swayObject.position, targetWorldPos, Time.deltaTime * smoothness);
            }
            else
            {
                swayObject.localPosition = Vector3.Lerp(swayObject.localPosition, defaultPos, Time.deltaTime * smoothness);
            }
        }
        public void EnableSway()
        {
            allowSway = true;
        }

        public void DisableSway()
        {
            allowSway = false;
        }
    }
}