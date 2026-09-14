using System;
using System.Collections.Generic;
using System.Linq;
using Messages;
using CustomUI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;


namespace CoverageTraining
{
    public class UIController : MonoBehaviour, IXRKeybindDisplayProfileProvider, ITrainingScreenHudDataProvider<CoverageTrainingScreenHudDataSnapshot>
    {
        [Header("UI resources")]
        [SerializeField] private XRKeybindDisplayProfile _baseXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _baseXRKeybindDisplayProfileGrabVariant;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeCompleteXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeCompleteXRKeybindDisplayProfileGrabVariant;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeNoOrganViewXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeNoOrganViewXRKeybindDisplayProfileGrabVariant;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeNoOrganViewNoAIXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeNoOrganViewNoAIXRKeybindDisplayProfileGrabVariant;  
        [SerializeField] private XRKeybindDisplayProfile _endoscopeNoAIXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeNoAIXRKeybindDisplayProfileGrabVariant;
        [SerializeField] private ScrollAreaAutoScroller _aiChatAutoScroller;
        [SerializeField] private ModalView _modalView;
        [SerializeField] private TrainingRankingViewBase _trainingRankingView;
        [SerializeField] private GameObject _desktopModeViewHolder;
        [Header("Interactive elements")]
        [Header("Training configuration elements")]
        [SerializeField] private GameObject _coverageTrainingInterfaceObject;
        [SerializeField] private SwitchController _timeLimitSwitch;
        [SerializeField] private TextMeshProUGUI _timeLimitConfigurationText;
        [SerializeField] private GameObject _timeLimitTextFieldObject;
        [SerializeField] private SwitchController _organExplorationSwitch;
        [SerializeField] private SwitchController _exploredOrganViewSwitch;
        [SerializeField] private SwitchController _enableAIAssistanceSwitch;

        [Header("Training control buttons")]
        [SerializeField] private ButtonController _startTrainingButton;
        [SerializeField] private ButtonController _pauseTrainingButton;
        [SerializeField] private ButtonController _resumeTrainingButton;
        [SerializeField] private ButtonController _backFromTrainingButton;
        [SerializeField] private ButtonController _endTrainingButton;
        [SerializeField] private ButtonController _saveTrainingResultsButton;
        [SerializeField] private ButtonController _showRankingButton;
        [SerializeField] private ButtonController _returnFromRankingButton;
        [SerializeField] private ButtonController _exitRoomButton;
        [Header("Training Data Holders")]
        [SerializeField] private LocalizeStringEvent _trainingStatusLocalizedStringEvent;
        [SerializeField] private TextMeshProUGUI _trainingTimerValueText;
        [SerializeField] private TextMeshProUGUI _finalScoreValueText;
        [SerializeField] private HorizontalLayoutGroup _progressPanelsHorizontalLayoutGroup;
        [SerializeField] private RectOffset _progressPanelsDefaultPadding;
        [SerializeField] private RectOffset _progressPanelsAIDisabledPadding;
        [SerializeField] private GameObject _finalScorePanel;
        [SerializeField] private GameObject _aiPanel;
        [SerializeField] private List<TrainingStatUIBinding> _trainingStatBindings;
        [Header("Localization resources")]
        [SerializeField] private LocalizedString _trainingInProgressLocalizedString;
        [SerializeField] private LocalizedString _trainingPausedLocalizedString;
        [SerializeField] private LocalizedString _trainingFinishedResultsLocalizedString;
        [Header("Configuration parameters")]
        [SerializeField] private float _aiMessageOverlayDisplayTime = 5f;
        public event Action<bool> OnTimeLimitSwitchPressedAction;
        public event Action<bool> OnExplorationDataSwitchPressedAction;
        public event Action<bool> OnExploredOrganViewSwitchPressedAction;
        public event Action<bool> OnEnableAIAssistanceSwitchPressedAction;
        public event Action OnStartTrainingButtonPressedAction;
        public event Action OnEndTrainingButtonPressedAction;
        public event Action OnPauseTrainingButtonPressedAction;
        public event Action OnResumeTrainingButtonPressedAction;
        public event Action OnRestartTrainingButtonPressedAction;
        public event Action OnExitRoomButtonPressedAction;
        public event Action OnConfirmExitRoomButtonPressedAction;
        public event Action OnSaveTrainingResultsButtonPressedAction;
        public event Action OnShowRankingButtonPressedAction;
        public event Action OnReturnFromRankingButtonPressedAction;
        public event Action<string> OnConfirmSaveTrainingResultsButtonPressedAction;
        public event Action<XRKeybindDisplayProfile> OnKeybindDisplayProfileReady;
        public event Action<CoverageTrainingScreenHudDataSnapshot> TrainingScreenHudDataUpdate;
        public event Action<bool> ToggleTrainingSpecificDataActive;

        private ChatBubbleSpawner _chatBubbleSpawner;
        private WindowNavigator _windowNavigator;
        private MessageStrings _messageStringsHelper;
        private XROverlayView _xrOverlayView;
        private Dictionary<TrainingStatUI, int> _trainingStatsDict;
        private int _lastTimerSec = int.MinValue;
        private bool _isTimeLimitEnabled = false;
        private int _timeLimitInSeconds = int.MinValue;
        private const float trainingStatEpsilon = 0.0005f;
        private bool _useGrabProfiles;

        public enum TrainingStatUI
        {
            OrganExplored,
            RectumExplored,
            SigmoidExplored,
            DescendingExplored,
            TransverseExplored,
            AscendingExplored,
            CecumExplored,
        }

        public enum TrainingStateUITitle
        {
            InProgress,
            Paused,
            FinishedResults
        }

        public enum CoverageTrainingWindows
        {
            TrainingConfiguration = 0,
            TrainingData = 1,
            Ranking = 2
        }

        [Serializable]
        public struct TrainingStatUIBinding
        {
            public TrainingStatUI trainingStat;
            public TextMeshProUGUI textElement;
            [NonSerialized] public float lastValue;
            [NonSerialized] public bool hasLastValue;
        }

        private void Awake()
        {
            _trainingStatsDict = new Dictionary<TrainingStatUI, int>();
            for (int i = 0; i < _trainingStatBindings.Count; i++)
            {
                _trainingStatsDict[_trainingStatBindings[i].trainingStat] = i;
            }
            _chatBubbleSpawner = GetComponentInChildren<ChatBubbleSpawner>();
            if (_chatBubbleSpawner == null)
                throw new Exception("ChatBubbleSpawner not found in children.");

            _windowNavigator = GetComponentInChildren<WindowNavigator>();
            if (_windowNavigator == null)
                throw new Exception("WindowController not found in children.");

            if (_modalView == null)
                throw new Exception("ModalView not assigned in editor.");

            if (_aiChatAutoScroller == null)
                throw new Exception("ScrollAreaAutoScroller not assigned in editor.");

            var myObject = GameObject.FindGameObjectWithTag("XRUIHolder");
            if (myObject != null)
            {
                _coverageTrainingInterfaceObject.transform.SetParent(myObject.transform, false);

            }
            else
            {
                Debug.LogError("No GameObject with tag 'XRUIHolder' found in the scene.");
            }

            myObject = GameObject.FindGameObjectWithTag("XROverlayView");
            if (myObject != null)
            {
                _xrOverlayView = myObject.GetComponent<XROverlayView>();
                if (_xrOverlayView == null)
                    throw new Exception("XROverlayView not found in XROverlayView GameObject.");
            }

            _messageStringsHelper = new MessageStrings();

            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            _timeLimitSwitch.OnSelected += () => OnTimeLimitSwitchPressedAction?.Invoke(true);
            _timeLimitSwitch.OnDeselected += () => OnTimeLimitSwitchPressedAction?.Invoke(false);
            _organExplorationSwitch.OnSelected += () => OnExplorationDataSwitchPressedAction?.Invoke(true);
            _organExplorationSwitch.OnDeselected += () => OnExplorationDataSwitchPressedAction?.Invoke(false);
            _exploredOrganViewSwitch.OnSelected += () => OnExploredOrganViewSwitchPressedAction?.Invoke(true);
            _exploredOrganViewSwitch.OnDeselected += () => OnExploredOrganViewSwitchPressedAction?.Invoke(false);
            _enableAIAssistanceSwitch.OnSelected += () => OnEnableAIAssistanceSwitchPressedAction?.Invoke(true);
            _enableAIAssistanceSwitch.OnDeselected += () => OnEnableAIAssistanceSwitchPressedAction?.Invoke(false);


            _startTrainingButton.OnSelected += () => OnStartTrainingButtonPressedAction?.Invoke();
            _pauseTrainingButton.OnSelected += () => OnPauseTrainingButtonPressedAction?.Invoke();
            _resumeTrainingButton.OnSelected += () => OnResumeTrainingButtonPressedAction?.Invoke();
            _backFromTrainingButton.OnSelected += () => OnRestartTrainingButtonPressedAction?.Invoke();
            _endTrainingButton.OnSelected += () => OnEndTrainingButtonPressedAction?.Invoke();
            _saveTrainingResultsButton.OnSelected += () => OnSaveTrainingResultsButtonPressedAction?.Invoke();
            _showRankingButton.OnSelected += () => OnShowRankingButtonPressedAction?.Invoke();
            _returnFromRankingButton.OnSelected += () => OnReturnFromRankingButtonPressedAction?.Invoke();
            _exitRoomButton.OnSelected += () => OnExitRoomButtonPressedAction?.Invoke();

            _chatBubbleSpawner.OnMessageReceived += AutoScrollAIChat;
        }

        private void Start()
        {
            SetBaseKeybindDisplayProfile();
        }

        public void PopulateTrainingRanking(List<TrainingResult> results)
        {
            _trainingRankingView.PopulateRankingTable(results);
        }

        public void SetUseGrabKeybindProfiles(bool useGrab)
        {
            _useGrabProfiles = useGrab;
            SetBaseKeybindDisplayProfile();
        }

        public void SetBaseKeybindDisplayProfile()
        {
            OnKeybindDisplayProfileReady?.Invoke(_useGrabProfiles ? _baseXRKeybindDisplayProfileGrabVariant : _baseXRKeybindDisplayProfile);
        }

        public void SetEndoscopeCompleteKeybindDisplayProfile()
        {
            OnKeybindDisplayProfileReady?.Invoke(_useGrabProfiles ? _endoscopeCompleteXRKeybindDisplayProfileGrabVariant : _endoscopeCompleteXRKeybindDisplayProfile);
        }

        public void SetEndoscopeNoOrganViewKeybindDisplayProfile()
        {
            OnKeybindDisplayProfileReady?.Invoke(_useGrabProfiles ? _endoscopeNoOrganViewXRKeybindDisplayProfileGrabVariant : _endoscopeNoOrganViewXRKeybindDisplayProfile);
        }

        public void SetEndoscopeNoOrganViewNoAIKeybindDisplayProfile()
        {
            OnKeybindDisplayProfileReady?.Invoke(_useGrabProfiles ? _endoscopeNoOrganViewNoAIXRKeybindDisplayProfileGrabVariant : _endoscopeNoOrganViewNoAIXRKeybindDisplayProfile);
        }

        public void SetEndoscopeNoAIKeybindDisplayProfile()
        {
            OnKeybindDisplayProfileReady?.Invoke(_useGrabProfiles ? _endoscopeNoAIXRKeybindDisplayProfileGrabVariant : _endoscopeNoAIXRKeybindDisplayProfile);
        }

        public void AutoScrollAIChat()
        {
            _aiChatAutoScroller.DelayScrollDown();
        }

        public void ShowAIStartRecordingMessage()
        {
            _xrOverlayView.ShowMessage(_messageStringsHelper.GetMessageLocalizedString(AIMessages.RecordingAISTTInput), false);
        }

        public void ShowAIStopRecordingMessage()
        {
            _xrOverlayView.CloseOverlay();
        }

        public void ShowAIToggleMessage(bool enabled)
        {
            _xrOverlayView.ShowMessage(_messageStringsHelper.GetMessageLocalizedString(enabled ? AIMessages.AITTSEnabled : AIMessages.AITTSDisabled));
        }

        public void ShowTrainingStartedMessage()
        {
            _xrOverlayView.ShowMessage(_messageStringsHelper.GetMessageLocalizedString(TrainingMessages.TrainingStarted));
        }

        public void ShowTrainingPausedMessage()
        {
            _xrOverlayView.ShowMessage(_messageStringsHelper.GetMessageLocalizedString(TrainingMessages.TrainingPaused));
        }

        public void ShowInvalidTrainingConfigurationModal()
        {
            _modalView.ShowModal(
                ModalType.Warning,
                _messageStringsHelper.GetMessageLocalizedString(TrainingMessages.InvalidTimeLimit));
        }

        public void ShowConfirmExitRoomModal()
        {
            _modalView.ShowModal(
                ModalType.Confirm,
                _messageStringsHelper.GetMessageLocalizedString(TrainingMessages.ConfirmExitRoom),
                () => OnConfirmExitRoomButtonPressedAction?.Invoke()
            );
        }

        public void ShowSaveTrainingResultsModal()
        {
            _modalView.ShowModal(
                ModalType.NameInput,
                _messageStringsHelper.GetMessageLocalizedString(TrainingMessages.ConfirmSaveTrainingResults),
                () => OnConfirmSaveTrainingResultsButtonPressedAction?.Invoke(_modalView.GetTMPInputFields()[0].text)
            );
        }

        public void ShowTrainingResultsSavedMessage()
        {
            _xrOverlayView.ShowMessage(_messageStringsHelper.GetMessageLocalizedString(TrainingMessages.TrainingResultsSaved));
        }

        public void ShowTrainingResultsNotSavedMessage()
        {
            _xrOverlayView.ShowMessage(_messageStringsHelper.GetMessageLocalizedString(TrainingMessages.TrainingResultsNotSaved));
        }

        public void CreateAIChatMessage(string message, bool isUser)
        {
            _chatBubbleSpawner.SpawnChatBubble(message, isUser);
        }

        public void ShowLLMResponseMessage(string response)
        {
            _xrOverlayView.ShowMessage(response, true, _aiMessageOverlayDisplayTime);
        }

        public void ShowTrainingDataWindow()
        {
            _windowNavigator.OpenWindowByIndex((int)CoverageTrainingWindows.TrainingData);
        }

        public void ShowRankingWindow()
        {
            _windowNavigator.OpenWindowByIndex((int)CoverageTrainingWindows.Ranking);
        }

        public void ShowTrainingConfigurationWindow()
        {
            _windowNavigator.OpenWindowByIndex((int)CoverageTrainingWindows.TrainingConfiguration);
        }

        public void ConfigureTimeLimitUI(bool enabled)
        {
            int timeLimitInseconds = GetTimeLimitConfigurationTextInSeconds();
            _isTimeLimitEnabled = enabled;
            _timeLimitInSeconds = Mathf.Max(0, timeLimitInseconds);
            _lastTimerSec = int.MinValue;
        }

        public int GetTimeLimitConfigurationTextInSeconds()
        {
            int timeLimitInSeconds = -1;
            string cleanText = new string(_timeLimitConfigurationText.text
                .Where(c => char.IsDigit(c) || c == '-' || c == '+')
                .ToArray());
            if (int.TryParse(cleanText, out int parsedValue))
            {
                timeLimitInSeconds = parsedValue * 60;
            }
            return timeLimitInSeconds;
        }

        public void UpdateTrainingStatsUI(bool isCoverageStatsActive, bool isFinalScoreActive, in CoverageTrainingStatsSnapshot trainingStatsSnapshot)
        {
            var timerText = UpdateTimer(trainingStatsSnapshot.TimerSeconds);
            var coveragePercentage = -1f;
            if (isCoverageStatsActive)
            {
                coveragePercentage = UpdateCoverageStats(in trainingStatsSnapshot);
            }
            if (isFinalScoreActive) UpdateFinalScore(trainingStatsSnapshot.FinalScore);
            TrainingScreenHudDataUpdate?.Invoke(new CoverageTrainingScreenHudDataSnapshot(timerText, coveragePercentage));
        }

        private string UpdateTimer(float timerSeconds)
        {
            int t = Mathf.Max(0, (int)Mathf.Floor(timerSeconds));
            if (_lastTimerSec == t) return string.Empty;
            _lastTimerSec = t;

            (int h, int m, int s) = CommonUtils.SecToHMS(t);

            string timerText;
            if (!_isTimeLimitEnabled)
            {
                timerText = string.Format("{0:00}:{1:00}:{2:00}", h, m, s);
            }
            else
            {
                var (H, M, S) = CommonUtils.SecToHMS(_timeLimitInSeconds);
                timerText = string.Format("{0:00}:{1:00}:{2:00}/{3:00}:{4:00}:{5:00}", h, m, s, H, M, S);
            }
            _trainingTimerValueText.SetText(timerText);
            return timerText;
        }

        public void ResetTrainingStatsBindings()
        {
            for (int i = 0; i < _trainingStatBindings.Count; i++)
            {
                var binding = _trainingStatBindings[i];
                binding.hasLastValue = false;
                binding.lastValue = 0f;
                binding.textElement.SetText(string.Empty);
                _trainingStatBindings[i] = binding;
            }
        }

        private float UpdateCoverageStats(in CoverageTrainingStatsSnapshot trainingStatsSnapshot)
        {
            float coveragePercentage = -1f;
            Span<(TrainingStatUI, float)> batch = stackalloc (TrainingStatUI, float)[7];
            int i = 0;
            batch[i++] = (TrainingStatUI.OrganExplored, trainingStatsSnapshot.OrganExplored);
            batch[i++] = (TrainingStatUI.RectumExplored, trainingStatsSnapshot.RectumExplored);
            batch[i++] = (TrainingStatUI.SigmoidExplored, trainingStatsSnapshot.SigmoidExplored);
            batch[i++] = (TrainingStatUI.DescendingExplored, trainingStatsSnapshot.DescendingExplored);
            batch[i++] = (TrainingStatUI.TransverseExplored, trainingStatsSnapshot.TransverseExplored);
            batch[i++] = (TrainingStatUI.AscendingExplored, trainingStatsSnapshot.AscendingExplored);
            batch[i++] = (TrainingStatUI.CecumExplored, trainingStatsSnapshot.CecumExplored);

            for (int u = 0; u < i; u++)
            {
                var (key, pct) = batch[u];
                if (!_trainingStatsDict.TryGetValue(key, out int bindingIndex)) continue;

                var trainingStatBinding = _trainingStatBindings[bindingIndex];

                if (!trainingStatBinding.hasLastValue || Mathf.Abs(trainingStatBinding.lastValue - pct) > trainingStatEpsilon)
                {
                    trainingStatBinding.lastValue = pct;
                    trainingStatBinding.hasLastValue = true;
                    trainingStatBinding.textElement.SetText("{0:0.00}%", pct);
                    _trainingStatBindings[bindingIndex] = trainingStatBinding;
                    if (key == TrainingStatUI.OrganExplored)
                    {
                        coveragePercentage = pct;
                    }
                }
            }
            return coveragePercentage;
        }

        private void UpdateFinalScore(float finalScore)
        {
            _finalScoreValueText.SetText("{0:0.00}/10", finalScore);
        }
        public void SetFinalScorePanelActive(bool isActive)
        {
            _finalScorePanel.SetActive(isActive);
        }

        public void SetAIDataPanelActive(bool isActive)
        {
            _aiPanel.SetActive(isActive);
            _progressPanelsHorizontalLayoutGroup.padding = isActive ? _progressPanelsDefaultPadding : _progressPanelsAIDisabledPadding;
        }

        public void SetTimeLimitFieldTextActive(bool isActive)
        {
            _timeLimitTextFieldObject.SetActive(isActive);
        }

        public void SetTrainingStateText(TrainingStateUITitle status)
        {
            _trainingStatusLocalizedStringEvent.StringReference = status switch
            {
                TrainingStateUITitle.InProgress => _trainingInProgressLocalizedString,
                TrainingStateUITitle.Paused => _trainingPausedLocalizedString,
                TrainingStateUITitle.FinishedResults => _trainingFinishedResultsLocalizedString,
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
            };
        }

        public void SetCoverageStatsElementsActive(bool isActive)
        {
            for (int i = 0; i < _trainingStatBindings.Count; i++)
            {
                _trainingStatBindings[i].textElement.transform.parent.gameObject.SetActive(isActive);
            }
        }

        public void SetSaveTrainingResultsButtonActive(bool isActive)
        {
            _saveTrainingResultsButton.gameObject.SetActive(isActive);
        }

        private void SetTrainingUIControls(bool showPause, bool showResume, bool showReturn, bool showEnd, bool showSaveResult, bool showRaking)
        {
            _pauseTrainingButton.gameObject.SetActive(showPause);
            _resumeTrainingButton.gameObject.SetActive(showResume);
            _backFromTrainingButton.gameObject.SetActive(showReturn);
            _endTrainingButton.gameObject.SetActive(showEnd);
            _saveTrainingResultsButton.gameObject.SetActive(showSaveResult);
            _showRankingButton.gameObject.SetActive(showRaking);
        }

        public void ShowTrainingInProgressUIControls() => SetTrainingUIControls(true, false, false, true, false, false);
        public void ShowTrainingPausedUIControls() => SetTrainingUIControls(false, true, false, true, false, false);
        public void ShowTrainingFinishedUIControls() => SetTrainingUIControls(false, false, true, false, true, true);

        #region UI Elements interaction control
        #endregion

        public void ConfigureTrainingScreenHud(bool allowCoverageStatsView)
        {
            ToggleTrainingSpecificDataActive?.Invoke(allowCoverageStatsView);
        }

        public void ToggleDesktopModeView(bool enabled)
        {
            _desktopModeViewHolder.SetActive(enabled);
        }

        private void OnDestroy()
        {
            Destroy(_coverageTrainingInterfaceObject);
        }
    }
}
