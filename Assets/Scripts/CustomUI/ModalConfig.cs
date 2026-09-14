using UnityEngine;
using System;
using UnityEngine.Localization;

namespace CustomUI
{
    [Serializable]
    public struct ModalButtonConfig
    {
        public LocalizedString LocalizedLabel;
        public Sprite Icon;
        public Action OnClick;
        public ModalButtonType ButtonType;
    }

    [Serializable]
    public enum ModalButtonType
    {
        Comfirm,
        Cancel,
    }

    [Serializable]
    public enum ModalType
    {
        Warning,
        Error,
        Confirm,
        DropdownSelection,
        NameDescriptionInput,
        NameInput,
        Loading,
        Settings,
    }

    [Serializable]
    public struct DropdownConfig
    {
        public LocalizedString[] LocalizedOptions;
        public int DefaultIndex;
        public Action<int> OnValueChanged;
    }

    [Serializable]
    public struct InputFieldConfig
    {
        public LocalizedString LocalizedPlaceholder;
        public Action<string> OnValueChanged;
    }

    [CreateAssetMenu(menuName = "LoGiViT/UI/Modal Config")]
    public class ModalConfig : ScriptableObject
    {
        [Header("Basic Settings")]
        public ModalType ModalType;
        public Sprite Icon;
        public LocalizedString LocalizedTitle;
        public LocalizedString LocalizedMessage;
        public bool UseButtons;
        [Header("Buttons")]
        public ModalButtonConfig[] ButtonConfigs;

        public bool UseDropdown;
        [Header("Dropdown")]
        public DropdownConfig DropdownConfig;

        public bool UseInputFields;
        [Header("Dropdown")]
        public InputFieldConfig[] InputFieldsConfigs;
    }
}