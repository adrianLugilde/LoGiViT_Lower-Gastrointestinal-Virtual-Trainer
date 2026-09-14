// ============================================================================
// XRKeybindDisplayProfileView.cs
//
// Displays XR controller keybind information in the UI based on active profiles.
// Updates text labels and icons for left/right controller bindings.
//
// Usage:
//   - Assign KeybindUIBinding arrays in Inspector for left/right controllers
//   - Call UpdateKeybindDisplay(profile) when the active XR device changes
//   - Each binding maps a LogicalBinding enum to UI elements (label + icon)
//
// Notes:
//   - Bindings without matching profile data are automatically hidden
//   - Supports both text labels (LocalizedString) and icons (Sprite)
// ============================================================================

using System;
using CustomUI;
using Unity.VRTemplate;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Displays XR controller keybind labels and icons based on the active keybind profile.
/// </summary>
public class XRKeybindDisplayProfileView : MonoBehaviour
{
    #region Nested Types

    /// <summary>
    /// Binding between a logical XR input and its UI representation.
    /// </summary>
    /// <remarks>
    /// This is a struct for Inspector serialization efficiency with Unity.
    /// The struct contains references but is only used for configuration.
    /// </remarks>
    [Serializable]
    public struct KeybindUIBinding
    {
        /// <summary>
        /// Localized string event for the keybind label text.
        /// </summary>
        public LocalizeStringEvent LocalizeStringEvent;

        /// <summary>
        /// Callout component that controls visibility and animation.
        /// </summary>
        public Callout Callout;

        /// <summary>
        /// Image component for displaying keybind icon.
        /// </summary>
        public Image IconImage;

        /// <summary>
        /// The logical XR input this binding represents.
        /// </summary>
        public XRControllerLogicalBinding LogicalBinding;
    }

    #endregion

    #region Serialized Fields

    /// <summary>
    /// UI bindings for left controller keybinds.
    /// </summary>
    [SerializeField] 
    private KeybindUIBinding[] _leftControllerKeybindUIBindings;

    /// <summary>
    /// UI bindings for right controller keybinds.
    /// </summary>
    [SerializeField] 
    private KeybindUIBinding[] _rightControllerKeybindUIBindings;

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the keybind display for both controllers based on the active profile.
    /// Call this when the XR device is detected or when switching profiles.
    /// </summary>
    /// <param name="keybindDisplayProfile">The profile to display, or null to hide all bindings.</param>
    public void UpdateKeybindDisplay(XRKeybindDisplayProfile keybindDisplayProfile)
    {
        // Update left controller bindings
        UpdateBindingsArray(
            _leftControllerKeybindUIBindings,
            keybindDisplayProfile?.leftControllerProfile);

        // Update right controller bindings
        UpdateBindingsArray(
            _rightControllerKeybindUIBindings,
            keybindDisplayProfile?.rightControllerProfile);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Updates an array of keybind UI bindings based on a controller profile.
    /// </summary>
    /// <param name="bindings">The UI bindings to update.</param>
    /// <param name="controllerProfile">The controller profile providing labels/icons, or null to hide all.</param>
    private void UpdateBindingsArray(KeybindUIBinding[] bindings, XRControllerKeybindProfile controllerProfile)
    {
        if (bindings == null) return;

        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];

            // No profile - hide all bindings
            if (controllerProfile == null)
            {
                HideBinding(ref binding);
                bindings[i] = binding;
                continue;
            }

            // Try to get label and icon from profile
            bool hasLabel = controllerProfile.TryGetLabel(binding.LogicalBinding, out LocalizedString label);
            bool hasIcon = controllerProfile.TryGetIcon(binding.LogicalBinding, out Sprite icon);

            // No label or icon for this binding - hide it
            if (!hasLabel && !hasIcon)
            {
                HideBinding(ref binding);
                bindings[i] = binding;
                continue;
            }

            // Validate required components
            if (binding.Callout == null || (binding.LocalizeStringEvent == null && binding.IconImage == null))
            {
                Debug.LogWarning($"[XRKeybindDisplayProfileView] Binding for {binding.LogicalBinding} is missing required components.");
                HideBinding(ref binding);
                bindings[i] = binding;
                continue;
            }

            // Update label if present
            UpdateBindingLabel(ref binding, hasLabel, label);

            // Update icon if present
            UpdateBindingIcon(ref binding, hasIcon, icon);

            bindings[i] = binding;
        }
    }

    /// <summary>
    /// Updates the label for a keybind binding.
    /// </summary>
    private void UpdateBindingLabel(ref KeybindUIBinding binding, bool hasLabel, LocalizedString label)
    {
        if (binding.LocalizeStringEvent == null) return;

        if (hasLabel)
        {
            binding.LocalizeStringEvent.StringReference = label;
            binding.LocalizeStringEvent.RefreshString();
            binding.Callout.TurnOnStuff();
            binding.Callout.gameObject.SetActive(true);
        }
        else
        {
            binding.LocalizeStringEvent.StringReference = null;
            binding.Callout.TurnOffStuff();
            binding.LocalizeStringEvent.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Updates the icon for a keybind binding.
    /// </summary>
    private void UpdateBindingIcon(ref KeybindUIBinding binding, bool hasIcon, Sprite icon)
    {
        if (binding.IconImage == null) return;

        if (hasIcon)
        {
            binding.IconImage.gameObject.SetActive(true);
            binding.IconImage.sprite = icon;
        }
        else
        {
            binding.IconImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Hides a keybind binding completely.
    /// </summary>
    /// <param name="binding">The binding to hide.</param>
    private void HideBinding(ref KeybindUIBinding binding)
    {
        if (binding.Callout != null)
        {
            binding.Callout.gameObject.SetActive(false);
            binding.Callout.TurnOffStuff();
        }

        if (binding.LocalizeStringEvent != null)
        {
            binding.LocalizeStringEvent.StringReference = null;
        }

        if (binding.IconImage != null)
        {
            binding.IconImage.gameObject.SetActive(false);
        }
    }

    #endregion
}
