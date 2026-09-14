using System;
using System.Collections.Generic;
using System.Linq;
using Messages;
using CustomUI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;


namespace PolypTraining
{
    public class UIController : MonoBehaviour, IXRKeybindDisplayProfileProvider, ITrainingScreenHudDataProvider<PolypTrainingScreenHudDataSnapshot>
    {
        [Header("UI resources")]
        [SerializeField] private XRKeybindDisplayProfile _baseXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _baseXRKeybindDisplayProfileGrabVariant;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeCompleteXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeCompleteXRKeybindDisplayProfileGrabVariant;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeNoAIXRKeybindDisplayProfile;
        [SerializeField] private XRKeybindDisplayProfile _endoscopeNoAIXRKeybindDisplayProfileGrabVariant;
        [SerializeField] private ScrollAreaAutoScroller _aiChatAutoScroller;
        [SerializeField] private ModalView _modalView;
        [SerializeField] private TrainingRankingViewBase _trainingRankingView;
        [SerializeField] private GameObject _desktopModeViewHolder;
        [Header("Interactive elements")]
        [Header("Training configuration elements")]
        [SerializeField] private GameObject _polypTrainingInterfaceObject;
        [SerializeField] private SwitchController _timeLimitSwitch;
        [SerializeField] private TextMeshProUGUI _timeLimitConfigurationInputText;
        [SerializeField] private GameObject _timeLimitTextFieldObject;
        [SerializeField] private SwitchController _showTrainingStatsSwitch;
        [SerializeField] private SwitchController _showPolypCountSwitch;
        [SerializeField] private GameObject _showPolypCountSwitchObject;
        [SerializeField] private SwitchController _showAnswersDuringTrainingSwitch;
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
        [Header("Polyp identification elements")]
        [SerializeField] private GameObject _polypIdentificationPanel;
        [SerializeField] private LocalizeStringEvent _polypIdentificationTitleLocalizedStringEvent;
        [SerializeField] private GameObject _polypIdentificationControlsSubpanel;
        [SerializeField] private GameObject _polypIdentificationAnswerScoreSubpanel;
        [SerializeField] private ButtonController _identifyPolypButton;
        [SerializeField] private ButtonController _continueIdentificationButton;
        [SerializeField] private RawImage _polypImage;
        [SerializeField] private TMP_Dropdown sizeDropdown;
        [SerializeField] private TMP_Dropdown diseaseLocationDropdown;
        [SerializeField] private TMP_Dropdown parisClassDropdown;
        [SerializeField] private TMP_Dropdown jnetClassDropdown;
        [SerializeField] private TextMeshProUGUI sizeCorrectAnswerCaption;
        [SerializeField] private TextMeshProUGUI diseaseLocationCorrectAnswerCaption;
        [SerializeField] private TextMeshProUGUI parisCorrectAnswerCaption;
        [SerializeField] private TextMeshProUGUI jnetCorrectAnswerCaption;
        [SerializeField] private ButtonController _previousIdentificationAnswerButton;
        [SerializeField] private ButtonController _nextIdentificationAnswerButton;
        [SerializeField] private TextMeshProUGUI _answerScoreValueText;
        [SerializeField] private LocalizeStringEvent _trainingStateLocalizedStringEvent;
        [SerializeField] private TextMeshProUGUI _trainingTimerValueText;
        [SerializeField] private TextMeshProUGUI _finalScoreValueText;
        [SerializeField] private GameObject _trainingStatsPanel;
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
        [SerializeField] private LocalizedString _trainingPausedForIdentificationLocalizedString;
        [SerializeField] private LocalizedString _fieldNotAnsweredLocalizedString;
        [SerializeField] private LocalizedString _polypIdentificationLocalizedString;
        [SerializeField] private LocalizedString _polypIdentificationNotDetectedLocalizedString;
        [SerializeField] private LocalizedString _polypIdentificationNotIdentifiedLocalizedString;
        [Header("Configuration parameters")]
        [SerializeField] private float _aiMessageOverlayDisplayTime = 5f;

        public event Action<bool> OnTimeLimitSwitchPressedAction;
        public event Action<bool> OnShowStatsDuringTrainingSwitchPressedAction;
        public event Action<bool> OnShowPolypCountSwitchPressedAction;
        public event Action<bool> OnShowAnswersDuringTrainingSwitchPressedAction;
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
        public event Action<PolypIdentificationSnapshot> OnIdentifyPolypButtonPressedAction;
        public event Action OnContinueIdentificationButtonPressedAction;
        public event Action OnPreviousPolypIdentificationAnswerButtonPressedAction;
        public event Action OnNextPolypIdentificationAnswerButtonPressedAction;
        public event Action OnReturnFromIdentificationCountdownEnd;
        public event Action LocaleChangedAction;
        public event Action<XRKeybindDisplayProfile> OnKeybindDisplayProfileReady;
        public event Action<PolypTrainingScreenHudDataSnapshot> TrainingScreenHudDataUpdate;
        public event Action<bool> ToggleTrainingSpecificDataActive;

        private ChatBubbleSpawner _chatBubbleSpawner;
        private WindowNavigator _windowNavigator;
        private MessageStrings _messageStringsHelper;
        private XROverlayView _xrOverlayView;
        private Dictionary<TrainingStatUI, int> _trainingStatsDict;
        private CanvasGroup _nextIdentificationAnswerButtonCanvasGroup;
        private CanvasGroup _previousIdentificationAnswerButtonCanvasGroup;
        private int _lastTimerSec = int.MinValue;
        private bool _isTimeLimitEnabled = false;
        private int _timeLimitInSeconds = int.MinValue;
        private const float trainingStatEpsilon = 0.0005f;
        private bool _useGrabProfiles;

        public enum TrainingStatUI
        {
            PolypsDetected,
            PolypsIdentified,
        }

        public enum TrainingStateUITitle
        {
            InProgress,
            Paused,
            PausedForIdentification,
            FinishedResults
        }

        public enum PolypTrainingWindows
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

        public readonly struct TrainingProgressUIState
        {
            public readonly PolypTrainingStatsSnapshot Stats;
            public readonly bool ShowPolypCountView;
            public readonly int PolypCount;
            public readonly bool ForcePolypCountView;

            public TrainingProgressUIState(
                in PolypTrainingStatsSnapshot stats,
                bool showPolypCountView,
                int polypCount,
                bool forcePolypCountView)
            {
                Stats = stats;
                ShowPolypCountView = showPolypCountView;
                PolypCount = polypCount;
                ForcePolypCountView = forcePolypCountView;
            }
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
                _polypTrainingInterfaceObject.transform.SetParent(myObject.transform, false);
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

            _nextIdentificationAnswerButtonCanvasGroup = _nextIdentificationAnswerButton.GetComponent<CanvasGroup>();
            _previousIdentificationAnswerButtonCanvasGroup = _previousIdentificationAnswerButton.GetComponent<CanvasGroup>();

            _messageStringsHelper = new MessageStrings();

            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            _timeLimitSwitch.OnSelected += () => OnTimeLimitSwitchPressedAction?.Invoke(true);
            _timeLimitSwitch.OnDeselected += () => OnTimeLimitSwitchPressedAction?.Invoke(false);
            _showTrainingStatsSwitch.OnSelected += () => ToggleTrainingStatsSwitch(true);
            _showTrainingStatsSwitch.OnDeselected += () => ToggleTrainingStatsSwitch(false);
            _showPolypCountSwitch.OnSelected += () => OnShowPolypCountSwitchPressedAction?.Invoke(true);
            _showPolypCountSwitch.OnDeselected += () => OnShowPolypCountSwitchPressedAction?.Invoke(false);
            _showAnswersDuringTrainingSwitch.OnSelected += () => OnShowAnswersDuringTrainingSwitchPressedAction?.Invoke(true);
            _showAnswersDuringTrainingSwitch.OnDeselected += () => OnShowAnswersDuringTrainingSwitchPressedAction?.Invoke(false);
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

            _identifyPolypButton.OnSelected += () => OnIdentifyPolypButtonPressedAction?.Invoke(GetPolypIdentificationSnapshot());
            _continueIdentificationButton.OnSelected += () => OnContinueIdentificationButtonPressedAction?.Invoke();
            _previousIdentificationAnswerButton.OnSelected += () => OnPreviousPolypIdentificationAnswerButtonPressedAction?.Invoke();
            _nextIdentificationAnswerButton.OnSelected += () => OnNextPolypIdentificationAnswerButtonPressedAction?.Invoke();

            _chatBubbleSpawner.OnMessageReceived += AutoScrollAIChat;

            _xrOverlayView.OnCountdownEnded += OnCountdownEnded;

            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
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

        public void SetEndoscopeNoAIKeybindDisplayProfile()
        {
            OnKeybindDisplayProfileReady?.Invoke(_useGrabProfiles ? _endoscopeNoAIXRKeybindDisplayProfileGrabVariant : _endoscopeNoAIXRKeybindDisplayProfile);
        }

        private void OnLocaleChanged(Locale locale)
        {
            LocaleChangedAction?.Invoke();
        }

        public void PopulatePolypIdentificationDropdowns(List<string> diseaseLocationOptions,
            List<string> parisClassOptions, List<string> jnetClassOptions)
        {
            PopulateDropdown(diseaseLocationDropdown, diseaseLocationOptions);
            PopulateDropdown(parisClassDropdown, parisClassOptions);
            PopulateDropdown(jnetClassDropdown, jnetClassOptions);
        }

        public void PopulatePolypSizeDropdown(List<string> sizeOptions)
        {
            PopulateDropdown(sizeDropdown, sizeOptions);
        }

        private void PopulateDropdown(TMP_Dropdown dropdown, List<string> options)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
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

        public void ShowLLMResponseMessage(string response)
        {
            _xrOverlayView.ShowMessage(response, true, _aiMessageOverlayDisplayTime);
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

        public void ShowPolypDetectionMessage(int resultCode)
        {
            _xrOverlayView.ShowMessage(_messageStringsHelper.GetMessageLocalizedString(resultCode));
        }

        public void ShowReturnFromIdentificationMessage(int countDownNumber)
        {
            _xrOverlayView.ShowCountdownMessage(countDownNumber);
        }

        private void OnCountdownEnded()
        {
            OnReturnFromIdentificationCountdownEnd?.Invoke();
        }

        public void ResetPolypIdentificationPanel()
        {
            sizeDropdown.value = 0;
            diseaseLocationDropdown.value = 0;
            parisClassDropdown.value = 0;
            jnetClassDropdown.value = 0;
            SetPolypIdentificationDropdownsActive(true);
            SetPolypIdentificationCorrectAnswersCaptionEnable(false);
            SwapPolypIdentificationControlPanelActiveButtons(showIdentifyButton: true);
            SetPolypIdentificationAnswerScorePanelActive(false);
            SetPolypIdentificationControlsPanelActive(true);
            SetStandardPolypIdentificationPanelTitle();
            SetNextPolypIdentificationAnswerInteractable(true);
            SetPreviousPolypIdentificationAnswerInteractable(true);
        }

        public void SwapPolypIdentificationControlPanelActiveButtons(bool showIdentifyButton)
        {
            _identifyPolypButton.gameObject.SetActive(showIdentifyButton);
            _continueIdentificationButton.gameObject.SetActive(!showIdentifyButton);
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

        public void ShowPolypIdentificationAnswer(PolypIdentificationAnswer answer)
        {
            SetPolypIdentificationDropdownsActive(false);
            SetPolypIdentificationCorrectAnswersCaptionEnable(true);

            if (answer.WasNotIdentified)
            {
                ShowUnidentifiedPolyp(answer.Polyp);
                return;
            }

            string fieldNotAnswered = _fieldNotAnsweredLocalizedString.GetLocalizedString();
            sizeCorrectAnswerCaption.text = GetAnswerText(answer.IsSizeCorrect,
                string.IsNullOrEmpty(answer.Size) ? fieldNotAnswered : answer.Size,
                answer.Polyp.GetSizeString());
            diseaseLocationCorrectAnswerCaption.text = GetAnswerText(answer.IsLocationCorrect,
                answer.Location.HasValue ? Disease.GetLocationString(answer.Location.Value) : fieldNotAnswered,
                Disease.GetLocationString(answer.Polyp.Location ?? Disease.IntestineLocation.Rectum));
            parisCorrectAnswerCaption.text = GetAnswerText(answer.IsParisClassCorrect,
                answer.ParisClass.HasValue ? Polyp.GetParisClassString(answer.ParisClass.Value) : fieldNotAnswered,
                Polyp.GetParisClassString(answer.Polyp.ParisClass));
            jnetCorrectAnswerCaption.text = GetAnswerText(answer.IsJnetClassCorrect,
                answer.JnetClass.HasValue ? Polyp.GetJnetClassString(answer.JnetClass.Value) : fieldNotAnswered,
                Polyp.GetJnetClassString(answer.Polyp.JnetClass));
            _answerScoreValueText.SetText("{0:0.00}/10", answer.Score);
        }

        private void ShowUnidentifiedPolyp(Polyp polyp)
        {
            sizeCorrectAnswerCaption.text = GetEmptyAnswerText(polyp.GetSizeString());
            diseaseLocationCorrectAnswerCaption.text = GetEmptyAnswerText(Disease.GetLocationString(polyp.Location ?? Disease.IntestineLocation.Rectum));
            parisCorrectAnswerCaption.text = GetEmptyAnswerText(Polyp.GetParisClassString(polyp.ParisClass));
            jnetCorrectAnswerCaption.text = GetEmptyAnswerText(Polyp.GetJnetClassString(polyp.JnetClass));
        }

        private string GetEmptyAnswerText(string correctAnswer)
        {
            return $"<color=red>{correctAnswer}</color>";
        }

        private string GetAnswerText(bool isCorrect, string userAnswer, string correctAnswer)
        {
            return isCorrect
                ? $"<color=green>{userAnswer}</color>"
                : $"<color=red>{userAnswer} --> {correctAnswer}</color>";
        }

        private void SetPolypIdentificationDropdownsActive(bool active)
        {
            sizeDropdown.gameObject.SetActive(active);
            diseaseLocationDropdown.gameObject.SetActive(active);
            parisClassDropdown.gameObject.SetActive(active);
            jnetClassDropdown.gameObject.SetActive(active);
        }

        private void SetPolypIdentificationCorrectAnswersCaptionEnable(bool enabled)
        {
            sizeCorrectAnswerCaption.gameObject.SetActive(enabled);
            diseaseLocationCorrectAnswerCaption.gameObject.SetActive(enabled);
            parisCorrectAnswerCaption.gameObject.SetActive(enabled);
            jnetCorrectAnswerCaption.gameObject.SetActive(enabled);
        }

        public void CreateAIChatMessage(string message, bool isUser)
        {
            _chatBubbleSpawner.SpawnChatBubble(message, isUser);
        }

        public void ToggleTrainingStatsSwitch(bool enabled)
        {
            OnShowStatsDuringTrainingSwitchPressedAction?.Invoke(enabled);
            _showPolypCountSwitchObject.SetActive(enabled);
        }

        public void ShowTrainingDataWindow()
        {
            _windowNavigator.OpenWindowByIndex((int)PolypTrainingWindows.TrainingData);
        }

        public void ShowRankingWindow()
        {
            _windowNavigator.OpenWindowByIndex((int)PolypTrainingWindows.Ranking);
        }

        public void ShowTrainingConfigurationWindow()
        {
            _windowNavigator.OpenWindowByIndex((int)PolypTrainingWindows.TrainingConfiguration);
        }

        public void ConfigureTimeLimitUI(bool enabled)
        {
            float timeLimitInSeconds = GetTimeLimitConfigurationTextInSeconds();
            _isTimeLimitEnabled = enabled;
            _timeLimitInSeconds = Mathf.Max(0, (int)Mathf.Floor(timeLimitInSeconds));
            _lastTimerSec = int.MinValue;
        }

        public int GetTimeLimitConfigurationTextInSeconds()
        {
            int timeLimitInSeconds = -1;
            string cleanText = new string(_timeLimitConfigurationInputText.text
                .Where(c => char.IsDigit(c) || c == '-' || c == '+')
                .ToArray());
            if (int.TryParse(cleanText, out int parsedValue))
            {
                timeLimitInSeconds = parsedValue * 60;
            }
            return timeLimitInSeconds;
        }

        public void UpdateTrainingStatsUI(bool isTrainingStatsActive, bool isFinalScoreActive, in TrainingProgressUIState trainingProgressUIState)
        {
            var timerText = UpdateTimer(trainingProgressUIState.Stats.TimerSeconds);
            var polypIdentifiedText = string.Empty;
            if (isTrainingStatsActive)
            {
                polypIdentifiedText = UpdateTrainingProgressStats(in trainingProgressUIState);
            }
            if (isFinalScoreActive) UpdateFinalScore(trainingProgressUIState.Stats.FinalScore);
            TrainingScreenHudDataUpdate?.Invoke(new PolypTrainingScreenHudDataSnapshot(timerText, polypIdentifiedText));
        }

        private string UpdateTimer(float timerSeconds)
        {
            int t = Mathf.Max(0, (int)Mathf.Floor(timerSeconds));
            if (_lastTimerSec == t) return string.Empty;
            _lastTimerSec = t;

            (int h, int m, int s) = SecToHMS(t);

            string timerText;
            if (!_isTimeLimitEnabled)
            {
                timerText = string.Format("{0:00}:{1:00}:{2:00}", h, m, s);
            }
            else
            {
                var (H, M, S) = SecToHMS(_timeLimitInSeconds);
                timerText = string.Format("{0:00}:{1:00}:{2:00}/{3:00}:{4:00}:{5:00}", h, m, s, H, M, S);
            }
            _trainingTimerValueText.SetText(timerText);
            return timerText;
        }

        private static (int h, int m, int s) SecToHMS(int total)
        {
            int h = total / 3600;
            total -= h * 3600;
            int m = total / 60;
            int s = total - m * 60;
            return (h, m, s);
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

        private string UpdateTrainingProgressStats(in TrainingProgressUIState trainingProgressUIState)
        {
            string polypsIdentifiedText = string.Empty;
            Span<(TrainingStatUI, float)> batch = stackalloc (TrainingStatUI, float)[2];
            int i = 0;
            batch[i++] = (TrainingStatUI.PolypsDetected, trainingProgressUIState.Stats.PolypsDetected);
            batch[i++] = (TrainingStatUI.PolypsIdentified, trainingProgressUIState.Stats.PolypsIdentified);

            for (int u = 0; u < i; u++)
            {
                var (key, value) = batch[u];
                if (!_trainingStatsDict.TryGetValue(key, out int bindingIndex)) continue;

                var trainingStatBinding = _trainingStatBindings[bindingIndex];

                if (!trainingStatBinding.hasLastValue || Mathf.Abs(trainingStatBinding.lastValue - value) > trainingStatEpsilon)
                {
                    var tempText = string.Empty;
                    trainingStatBinding.lastValue = value;
                    trainingStatBinding.hasLastValue = true;
                    if (trainingProgressUIState.ShowPolypCountView || trainingProgressUIState.ForcePolypCountView)
                    {
                        tempText = string.Format("{0:0}/{1:0}", value, trainingProgressUIState.PolypCount);
                        trainingStatBinding.textElement.SetText(tempText);
                    }
                    else
                    {
                        tempText = string.Format("{0:0}", value);
                        trainingStatBinding.textElement.SetText(tempText);
                    }
                    _trainingStatBindings[bindingIndex] = trainingStatBinding;
                    if (key == TrainingStatUI.PolypsIdentified)
                    {
                        polypsIdentifiedText = tempText;
                    }
                }
            }
            return polypsIdentifiedText;
        }

        private void UpdateFinalScore(float finalScore)
        {
            _finalScoreValueText.SetText("{0:0.00}/10", finalScore);
        }

        public void SetPolypIdentificationPanelActive(bool isActive)
        {
            _polypIdentificationPanel.SetActive(isActive);
        }

        public void SetPolypIdentificationHUDMsgActive(bool isActive)
        {
            TrainingScreenHudDataUpdate?.Invoke(new PolypTrainingScreenHudDataSnapshot(string.Empty, string.Empty, isActive));
        }

        public void SetPolypIdentificationControlsPanelActive(bool isActive)
        {
            _polypIdentificationControlsSubpanel.SetActive(isActive);
        }

        public void SetPolypIdentificationAnswerScorePanelActive(bool isActive)
        {
            _polypIdentificationAnswerScoreSubpanel.SetActive(isActive);
        }

        public void SetPolypIdentificationImage(Texture image)
        {
            _polypImage.texture = image;
        }

        public void SetTrainingStatsPanelActive(bool isActive)
        {
            _trainingStatsPanel.SetActive(isActive);
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

        public void SetShowPolypCountSwitchActive(bool isActive)
        {
            _showPolypCountSwitchObject.SetActive(isActive);
        }

        private void SetPolypIdentificationPanelTitle(string title)
        {
            _polypIdentificationTitleLocalizedStringEvent.StringReference = _polypIdentificationLocalizedString;
        }

        public void SetNotDetectedPolypIdentificationPanelTitle() => _polypIdentificationTitleLocalizedStringEvent.StringReference = _polypIdentificationNotDetectedLocalizedString;

        public void SetNotIdentifiedPolypIdentificationPanelTitle() => _polypIdentificationTitleLocalizedStringEvent.StringReference = _polypIdentificationNotIdentifiedLocalizedString;

        public void SetStandardPolypIdentificationPanelTitle() => _polypIdentificationTitleLocalizedStringEvent.StringReference = _polypIdentificationLocalizedString;

        public void SetTrainingStateText(TrainingStateUITitle state)
        {
            _trainingStateLocalizedStringEvent.StringReference = state switch
            {
                TrainingStateUITitle.InProgress => _trainingInProgressLocalizedString,
                TrainingStateUITitle.Paused => _trainingPausedLocalizedString,
                TrainingStateUITitle.FinishedResults => _trainingFinishedResultsLocalizedString,
                TrainingStateUITitle.PausedForIdentification => _trainingPausedForIdentificationLocalizedString,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }

        public PolypIdentificationSnapshot GetPolypIdentificationSnapshot()
        {
            return new PolypIdentificationSnapshot(
                sizeDropdown.options[sizeDropdown.value].text,
                diseaseLocationDropdown.value,
                parisClassDropdown.options[parisClassDropdown.value].text,
                jnetClassDropdown.options[jnetClassDropdown.value].text);
        }

        public void SetPolypStatsElementsActive(bool isActive)
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

        private void SetTrainingUIControls(bool showPause, bool showResume, bool showReturn, bool showEnd, bool showSaveResult, bool showRanking)
        {
            _pauseTrainingButton.gameObject.SetActive(showPause);
            _resumeTrainingButton.gameObject.SetActive(showResume);
            _backFromTrainingButton.gameObject.SetActive(showReturn);
            _endTrainingButton.gameObject.SetActive(showEnd);
            _saveTrainingResultsButton.gameObject.SetActive(showSaveResult);
            _showRankingButton.gameObject.SetActive(showRanking);
        }

        public void ShowTrainingInProgressUIControls() => SetTrainingUIControls(true, false, false, true, false, false);
        public void ShowTrainingPausedUIControls() => SetTrainingUIControls(false, true, false, true, false, false);
        public void ShowTrainingFinishedUIControls() => SetTrainingUIControls(false, false, true, false, true, true);
        public void ShowTrainingInPolypIdentificationUIControls() => SetTrainingUIControls(false, false, false, true, false, false);

        #region UI Elements interaction control
        public void SetNextPolypIdentificationAnswerInteractable(bool interactable)
        {
            _nextIdentificationAnswerButtonCanvasGroup.alpha = interactable ? 1f : 0f;
            //_nextIdentificationAnswerButton.GetComponent<CanvasGroup>().interactable = interactable;
        }

        public void SetPreviousPolypIdentificationAnswerInteractable(bool interactable)
        {
            _previousIdentificationAnswerButtonCanvasGroup.alpha = interactable ? 1f : 0f;
            //_previousIdentificationAnswerButton.GetComponent<CanvasGroup>().interactable = interactable;
        }
        #endregion

        public void ConfigureTrainingScreenHud(bool allowPolypStatsView)
        {
            ToggleTrainingSpecificDataActive?.Invoke(allowPolypStatsView);
        }

        public void ToggleDesktopModeView(bool enabled)
        {
            _desktopModeViewHolder.SetActive(enabled);
        }

        private void OnDestroy()
        {
            Destroy(_polypTrainingInterfaceObject);
        }
    }
}