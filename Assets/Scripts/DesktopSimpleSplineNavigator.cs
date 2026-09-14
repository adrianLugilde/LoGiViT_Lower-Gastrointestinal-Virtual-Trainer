using UnityEngine;
using UnityEngine.InputSystem;

public class DesktopSimpleSplineNavigator : SimpleSplineNavigator
{
    private DesktopUserActions userControls;
    private DesktopUserActions.DesktopGuidedCameraActions desktopGuidedCameraActions;
    private bool cameraRotating = false;

    protected override void Awake()
    {
        userControls = new DesktopUserActions();
        SetActions();
    }

    protected override void SetActions()
    {
        desktopGuidedCameraActions = userControls.DesktopGuidedCamera;

        desktopGuidedCameraActions.CameraMovement.performed += ctx => cameraMovementInput = ctx.ReadValue<Vector2>();
        desktopGuidedCameraActions.CameraMovement.canceled += ctx => cameraMovementInput = Vector2.zero;
        desktopGuidedCameraActions.CameraRotationMouse.performed += ctx => cameraRotating = true;
        desktopGuidedCameraActions.CameraRotationMouse.canceled += MouseCameraRotationCanceled;
        desktopGuidedCameraActions.CameraRotationGamepad.performed += ctx => cameraRotationInput = ctx.ReadValue<Vector2>();
        desktopGuidedCameraActions.CameraRotationGamepad.canceled += GamepadCameraRotationCanceled;
        desktopGuidedCameraActions.SpinCameraKeyboard.performed += ctx => cameraRollInput = ctx.ReadValue<float>();
        desktopGuidedCameraActions.SpinCameraKeyboard.canceled += ctx => cameraRollInput = 0f;
        desktopGuidedCameraActions.SpinCameraGamepad.performed += ctx => cameraRollInput = ctx.ReadValue<float>();
        desktopGuidedCameraActions.SpinCameraGamepad.canceled += ctx => cameraRollInput = 0f;
        desktopGuidedCameraActions.Enable();
    }


    protected override void FixedUpdate()
    {
        if(cameraRotating)
        {
            cameraRotationInput.x = Input.GetAxis("Mouse X");
            cameraRotationInput.y = Input.GetAxis("Mouse Y");
        }
        base.FixedUpdate();
    }

    private void MouseCameraRotationCanceled(InputAction.CallbackContext ctx)
    {
        cameraRotating = false;
        CameraRotationCanceled();
    }

    private void GamepadCameraRotationCanceled(InputAction.CallbackContext ctx)
    {
        CameraRotationCanceled();
    }

    public override void ToggleMovementInputActions(bool status)
    {
        if(status)
        {
            desktopGuidedCameraActions.CameraMovement.Enable();
            desktopGuidedCameraActions.CameraRotationMouse.Enable();
            desktopGuidedCameraActions.CameraRotationGamepad.Enable();
        } else
        {
            desktopGuidedCameraActions.CameraMovement.Disable();
            desktopGuidedCameraActions.CameraRotationMouse.Disable();
            desktopGuidedCameraActions.CameraRotationGamepad.Disable();
        }
        enabled = status;
    }

    private void OnDestroy()
    {
        userControls.Dispose();
    }
}