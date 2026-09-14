/// <summary>
/// Represents logical inputs that can exist on any XR controller,
/// independent of hardware (Vive, Index, Quest, etc.).
/// Used by ControllerKeybindProfile and UI to map labels/icons.
/// </summary>
public enum XRControllerLogicalBinding
{
    // Primary interactions
    Trigger,
    Grab,

    // Analog surfaces
    Trackpad,
    Joystick,


    // D-Pad directions (virtualized from touchpad or joystick)
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,

    // Face buttons (A/B, X/Y, etc.)
    ButtonA,
    ButtonB,
}