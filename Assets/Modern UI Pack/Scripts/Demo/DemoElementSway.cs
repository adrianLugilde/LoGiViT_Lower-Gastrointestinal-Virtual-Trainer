using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
#if UNITY_XR_MANAGEMENT
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif

namespace Michsky.MUIP
{
    public class DemoElementSway : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
#if UNITY_XR_MANAGEMENT
    , IXRHoverInteractable
#endif
    {
        [Header("Resources")]
        [SerializeField] private DemoElementSwayParent swayParent;
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private RectTransform swayObject;
        [SerializeField] private CanvasGroup normalCG;
        [SerializeField] private CanvasGroup highlightedCG;
        [SerializeField] private CanvasGroup selectedCG;

        [Header("Settings")]
        [SerializeField] private float smoothness = 10;
        [SerializeField] private float transitionSpeed = 8;
        [SerializeField][Range(0, 1)] private float dissolveAlpha = 0.5f;
        [SerializeField] private float worldSpaceDisplacementFactor = 0.15f;

        [Header("XR Settings")]
        [SerializeField] private bool enableXRSupport = false;
        [SerializeField] private Camera xrCamera = null;

        [Header("Events")]
        [SerializeField] private UnityEvent onClick;

        bool allowSway;
        [HideInInspector] public bool wmSelected;
        private bool isXRHovering = false;

        Vector3 cursorPos;
        Vector2 defaultPos;
        private Camera activeCamera;

#if UNITY_XR_MANAGEMENT
        // XR interface implementation
        public bool isHovered => isXRHovering;
        public XRInteractionManager interactionManager { get; set; }
        public System.Collections.Generic.List<IXRHoverInteractor> interactorsHovering { get; } = new System.Collections.Generic.List<IXRHoverInteractor>();
        
        // Required XR interface methods
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

        void Awake()
        {
            if (swayParent == null)
            {
                var tempSway = transform.parent.GetComponent<DemoElementSwayParent>();
                if (tempSway == null) { transform.parent.gameObject.AddComponent<DemoElementSwayParent>(); }
                swayParent = tempSway;
            }

            defaultPos = swayObject.anchoredPosition;
            normalCG.alpha = 1;
            highlightedCG.alpha = 0;

            // Setup appropriate camera
            SetupCamera();
        }

        void SetupCamera()
        {
            if (enableXRSupport && xrCamera != null)
            {
                activeCamera = xrCamera;
            }
            else
            {
                activeCamera = Camera.main;
            }
        }

        void Update()
        {
            // Update cursor position for non-XR input
            if (!enableXRSupport)
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                if (allowSway) { cursorPos = Input.mousePosition; }
#elif ENABLE_INPUT_SYSTEM
                if (allowSway) { cursorPos = Mouse.current.position.ReadValue(); }
#endif
            }
#if UNITY_XR_MANAGEMENT
            // Update cursor position for XR input
            else if (enableXRSupport && isXRHovering && interactorsHovering.Count > 0)
            {
                // Try to get ray interactor
                if (interactorsHovering[0] is XRRayInteractor rayInteractor)
                {
                    if (rayInteractor.TryGetHitInfo(out Vector3 position, out Vector3 normal, out int positionInLine, out bool isValidTarget))
                    {
                        cursorPos = position;
                    }
                }
            }
#endif

            // Check for camera reference updates
            if (activeCamera == null)
            {
                SetupCamera();
            }

            // Process based on canvas render mode
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

        public void Dissolve()
        {
            if (wmSelected == true)
                return;

            StopCoroutine("DissolveHelper");
            StopCoroutine("HighlightHelper");
            StopCoroutine("ActiveHelper");

            StartCoroutine("DissolveHelper");
        }

        public void Highlight()
        {
            if (wmSelected == true)
                return;

            StopCoroutine("DissolveHelper");
            StopCoroutine("HighlightHelper");
            StopCoroutine("ActiveHelper");

            StartCoroutine("HighlightHelper");
        }

        public void Active()
        {
            if (wmSelected == true)
                return;

            StopCoroutine("DissolveHelper");
            StopCoroutine("HighlightHelper");
            StopCoroutine("HighlightHelper");

            StartCoroutine("ActiveHelper");
        }

        public void WindowManagerSelect()
        {
            wmSelected = true;

            StopCoroutine("ActiveHelper");
            StopCoroutine("HighlightHelper");
            StartCoroutine("WMSelectHelper");
        }

        public void WindowManagerDeselect()
        {
            wmSelected = false;

            StartCoroutine("WMDeselectHelper");
            StartCoroutine("DissolveHelper");
        }

        public void OnPointerEnter(PointerEventData data)
        {
            allowSway = true;
            swayParent.DissolveAll(this);
        }

        public void OnPointerExit(PointerEventData data)
        {
            allowSway = false;
            swayParent.HighlightAll();
        }

        public void OnPointerClick(PointerEventData data)
        {
            onClick.Invoke();
        }

        IEnumerator DissolveHelper()
        {
            while (normalCG.alpha > dissolveAlpha)
            {
                normalCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
                highlightedCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
                yield return null;
            }

            highlightedCG.alpha = 0;
            normalCG.alpha = dissolveAlpha;
            highlightedCG.gameObject.SetActive(false);
        }

        IEnumerator HighlightHelper()
        {
            while (normalCG.alpha < 1)
            {
                normalCG.alpha += Time.unscaledDeltaTime * transitionSpeed;
                highlightedCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
                yield return null;
            }

            normalCG.alpha = 1;
            highlightedCG.alpha = 0;
            highlightedCG.gameObject.SetActive(false);
        }

        IEnumerator ActiveHelper()
        {
            highlightedCG.gameObject.SetActive(true);

            while (highlightedCG.alpha < 1)
            {
                normalCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
                highlightedCG.alpha += Time.unscaledDeltaTime * transitionSpeed;
                yield return null;
            }

            highlightedCG.alpha = 1;
            normalCG.alpha = 0;
        }

        IEnumerator WMSelectHelper()
        {
            selectedCG.gameObject.SetActive(true);

            while (selectedCG.alpha < 1)
            {
                normalCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
                highlightedCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
                selectedCG.alpha += Time.unscaledDeltaTime * transitionSpeed;
                yield return null;
            }

            highlightedCG.alpha = 0;
            normalCG.alpha = 0;
            selectedCG.alpha = 1;
        }

        IEnumerator WMDeselectHelper()
        {
            while (selectedCG.alpha > 0)
            {
                selectedCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
                yield return null;
            }

            selectedCG.alpha = 0;
            selectedCG.gameObject.SetActive(false);
        }
    }
}