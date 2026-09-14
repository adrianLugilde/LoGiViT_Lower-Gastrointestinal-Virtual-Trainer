using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public abstract class XRControllerKeybindProfile : ScriptableObject
{
    [Serializable]
    public struct KeybindEntry
    {
        public XRControllerLogicalBinding Binding;
        public LocalizedString Label;
        public Sprite Icon;
    }

    [SerializeField] private List<KeybindEntry> _keybindEntries = new();

    public bool HasBinding(XRControllerLogicalBinding binding) => _keybindEntries.Exists(e => e.Binding == binding);

    public bool TryGetLabel(XRControllerLogicalBinding binding, out LocalizedString label)
    {
        int i = _keybindEntries.FindIndex(e => e.Binding == binding);
        if (i >= 0) { label = _keybindEntries[i].Label; return true; }
        label = default;
        return false;
    }

    public bool TryGetIcon(XRControllerLogicalBinding binding, out Sprite icon)
    {
        int i = _keybindEntries.FindIndex(e => e.Binding == binding);
        if (i >= 0) { icon = _keybindEntries[i].Icon; return true; }
        icon = null;
        return false;
    }
}
