

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;
using System.Collections;
using System;
using TMPro;
using static TMPro.TMP_Dropdown;

namespace CustomUI
{

    public class ModalView : MonoBehaviour
    {
        [Header("Modal configurations")]
        [SerializeField] private ModalConfig[] _modalConfigs;
        [Header("UI References")]
        [SerializeField] private Image _icon;
        [SerializeField] private LocalizeStringEvent _localizedTitleEvent;
        [SerializeField] private LocalizeStringEvent _localizedMessage;
        [SerializeField] private GameObject _buttonsContainer;
        [SerializeField] private ButtonController _buttonPrefab;
        [SerializeField] private GameObject _inputFieldsContainer;
        [SerializeField] private GameObject _inputFieldPrefab;
        [SerializeField] private GameObject _dropdownContainer;
        [SerializeField] private TMP_Dropdown _dropdown;
        [SerializeField] private Animator _animator;

        public bool IsModalActive = false;
        public bool IsInAnimation = false;

        public List<GameObject> _spawnedElements = new List<GameObject>();
        private Dictionary<ModalType, ModalConfig> _modalConfigDict;
        private float _cachedStateLength;
        private TMP_InputField[] _tmpInputFields;

        private void Awake()
        {
            _cachedStateLength = GetAnimatorClipLength(_animator, "Fade-out");
        }

        private ModalConfig GetModalConfig(ModalType modalType)
        {
            if (_modalConfigDict.TryGetValue(modalType, out ModalConfig config))
            {
                return config;
            }
            else
            {
                return null;
            }
        }

        public IEnumerator ShowModalCoroutine(ModalType modalType,
            LocalizedString messageStringOverwrite = null,
            Action onConfirmOverwrite = null,
            Action onCancelOverwrite = null,
            LocalizedString[] dropdownOptionsOverwrite = null,
            int dropdownSelectedOptionOverwrite = 0,
            LocalizedString[] inputFieldsOverwrite = null)
        {
            if (_modalConfigDict == null)
            {
                _modalConfigDict = new Dictionary<ModalType, ModalConfig>();
                foreach (var config in _modalConfigs)
                {
                    _modalConfigDict[config.ModalType] = config;
                }
            }

            if (IsModalActive)
            {
                Debug.LogWarning("A modal is already active. Please close it before opening a new one.");
                yield return null;
            }

            var modalConfig = GetModalConfig(modalType);
            if (modalConfig == null)
            {
                Debug.LogError($"No modal configuration found for type: {modalType}");
                yield return null;
            }

            _icon.sprite = modalConfig.Icon;
            _localizedTitleEvent.StringReference = modalConfig.LocalizedTitle;
            _localizedMessage.StringReference = messageStringOverwrite ?? modalConfig.LocalizedMessage;

            ClearSpawned();

            _buttonsContainer.SetActive(modalConfig.UseButtons);
            if (modalConfig.UseButtons)
            {
                foreach (var btnCfg in modalConfig.ButtonConfigs)
                {
                    var btn = Instantiate(_buttonPrefab, _buttonsContainer.transform);
                    btn.GetComponentInChildren<LocalizeStringEvent>().StringReference = btnCfg.LocalizedLabel;
                    btn.OnSelected = null;
                    btn.SetIcon(btnCfg.Icon);
                    btn.SetLabel(btnCfg.LocalizedLabel);

                    Action actionToExecute;
                    switch (btnCfg.ButtonType)
                    {
                        case ModalButtonType.Comfirm:
                            actionToExecute = onConfirmOverwrite ?? (() => btnCfg.OnClick?.Invoke());
                            break;
                        case ModalButtonType.Cancel:
                            actionToExecute = onCancelOverwrite ?? (() => btnCfg.OnClick?.Invoke());
                            break;
                        default:
                            actionToExecute = () => btnCfg.OnClick?.Invoke();
                            break;
                    }
                    btn.OnSelected += Close;
                    btn.OnSelected += actionToExecute;
                    _spawnedElements.Add(btn.gameObject);
                }
            }

            _inputFieldsContainer.SetActive(modalConfig.UseInputFields);
            if (modalConfig.UseInputFields)
            {
                if (modalType == ModalType.NameDescriptionInput || modalType == ModalType.NameInput)
                {
                    for (int i = 0; i < modalConfig.InputFieldsConfigs.Length; i++)
                    {
                        var inputFieldConfig = modalConfig.InputFieldsConfigs[i];
                        var inputFieldObject = _inputFieldsContainer.transform.GetChild(i).gameObject;
                        var localizedPlaceholder = inputFieldObject.GetComponentInChildren<LocalizeStringEvent>();
                        localizedPlaceholder.StringReference = (inputFieldsOverwrite?.Length > i)
                            ? inputFieldsOverwrite[i]
                            : inputFieldConfig.LocalizedPlaceholder;
                        var tmpInputField = inputFieldObject.GetComponent<TMP_InputField>();
                        //tmpInputField.onValueChanged.RemoveAllListeners();
                        //tmpInputField.onValueChanged.AddListener((value) => inputFieldConfig.OnValueChanged(value));
                        //_spawnedElements.Add(inputFieldObject);
                    }

                    for (int i = 0; i < _inputFieldsContainer.transform.childCount; i++)
                    {
                        if(i > modalConfig.InputFieldsConfigs.Length - 1)
                            _inputFieldsContainer.transform.GetChild(i).gameObject.SetActive(false);
                        else
                            _inputFieldsContainer.transform.GetChild(i).gameObject.SetActive(true);
                    }
                }
                else
                {
                    Debug.LogWarning($"Modal type {modalType} for input fields is not managed. Check the configuration.");
                    yield return null;
                }
                /*for (int i = 0; i < modalConfig.InputFieldsConfigs.Length; i++)
                {
                    var inputFieldConfig = modalConfig.InputFieldsConfigs[i];
                    var inputFieldObject = Instantiate(_inputFieldsContainer, _inputFieldsContainer.transform);
                    var localizedPlaceholder = inputFieldObject.GetComponentInChildren<LocalizeStringEvent>();
                    localizedPlaceholder.StringReference = (inputFieldsOverwrite?.Length > i)
                        ? inputFieldsOverwrite[i]
                        : inputFieldConfig.LocalizedPlaceholder;
                    var tmpInputField = inputFieldObject.GetComponent<TMP_InputField>();
                    tmpInputField.onValueChanged.RemoveAllListeners();
                    tmpInputField.onValueChanged.AddListener(inputFieldConfig.OnValueChanged.Invoke);
                    _spawnedElements.Add(inputFieldObject);
                }*/
            }

            _dropdownContainer.SetActive(modalConfig.UseDropdown);
            if (modalConfig.UseDropdown)
            {
                var optionsSource = dropdownOptionsOverwrite?.Length > 0 ? dropdownOptionsOverwrite : modalConfig.DropdownConfig.LocalizedOptions;
                _dropdown.ClearOptions();
                if (optionsSource?.Length > 0)
                {
                    var options = new List<OptionData>(optionsSource.Length);
                    foreach (var localizedOption in optionsSource)
                    {
                        options.Add(new OptionData(localizedOption.GetLocalizedString()));
                    }

                    _dropdown.AddOptions(options);
                }
                else
                {
                    _dropdown.ClearOptions();
                    Debug.LogWarning("No dropdown options provided or available in the configuration.");
                    _dropdownContainer.SetActive(false);
                }
                _dropdown.value = dropdownSelectedOptionOverwrite > 0 ? dropdownSelectedOptionOverwrite : modalConfig.DropdownConfig.DefaultIndex;
                _dropdown.onValueChanged.RemoveAllListeners();
                if (modalConfig.DropdownConfig.OnValueChanged != null) _dropdown.onValueChanged.AddListener(value => modalConfig.DropdownConfig.OnValueChanged(value));
                //_spawnedElements.Add(_dropdownContainer);
            }
            yield return new WaitUntil(() => !IsInAnimation);
            Open();
        }

        public void ShowModal(ModalType modalType,
            LocalizedString messageStringOverwrite = null,
            Action onConfirmOverwrite = null,
            Action onCancelOverwrite = null,
            LocalizedString[] dropdownOptionsOverwrite = null,
            int dropdownSelectedOptionOverwrite = 0,
            LocalizedString[] inputFieldsOverwrite = null)
        {
            StartCoroutine(ShowModalCoroutine(modalType, messageStringOverwrite, onConfirmOverwrite, onCancelOverwrite, dropdownOptionsOverwrite, dropdownSelectedOptionOverwrite, inputFieldsOverwrite));
        }

        public TMP_InputField[] GetTMPInputFields()
        {
            if (_inputFieldsContainer != null && _inputFieldsContainer.activeSelf)
            {
                _tmpInputFields = _inputFieldsContainer.GetComponentsInChildren<TMP_InputField>(true);
                return _tmpInputFields;
            }
            else
            {
                return null;
            }
        }

        public int GetDropdownSelectedIndex()
        {
            if (_dropdownContainer != null && _dropdownContainer.activeSelf)
            {
                return _dropdown.value;
            }
            else
            {
                Debug.LogWarning("Dropdown is not active or not set.");
                return -1;
            }
        }

        private void Open()
        {
            IsModalActive = true;
            _animator.Play("Fade-in");
        }

        public void Close()
        {
            IsModalActive = false;
            IsInAnimation = true;
            _animator.Play("Fade-out");
            ClearSpawned();
            StartCoroutine(SetModalReady());
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawnedElements)
                Destroy(go);
            _spawnedElements.Clear();
        }

        IEnumerator SetModalReady()
        {
            yield return new WaitForSecondsRealtime(_cachedStateLength);
            IsInAnimation = false;
        }

        public float GetAnimatorClipLength(Animator _animator, string _clipName)
        {
            float _lengthValue = -1;
            RuntimeAnimatorController _rac = _animator.runtimeAnimatorController;

            for (int i = 0; i < _rac.animationClips.Length; i++)
            {
                if (_rac.animationClips[i].name == _clipName)
                {
                    _lengthValue = _rac.animationClips[i].length;
                    break;
                }
            }

            return _lengthValue;
        }

        public void ShowTempModal()
        {
            throw new NotImplementedException();
        }
    }
}