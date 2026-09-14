using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using static AIUtility.AIPromptsHelper;
using static CommonUtils;

public class TrainingProgressController : MonoBehaviour
{
    [SerializeField] protected SimpleSplineNavigator simpleSplineNavigator; //TODO this should be in a manager
    [SerializeField] protected SystemPrompt helperSystemPrompt = SystemPrompt.Default;
    [SerializeField] protected SystemPrompt supervisorSystemPrompt = SystemPrompt.Default;
    public Action<string, string> SendLLMRequest;

    public int TrainingMemoryBufferCapacity = 40;
    public float LoggingInterval  = 0.2f;
    public float LimitSpeed  = 0.6f;
    public int SpeedCheckTimestamps  = 10;
    public float MediumSpeedMultiplier  = 1.2f;
    public float HighSpeedMultiplier  = 1.5f;
    public float SpeedCheckInterval  = 15f;
    public float AngulationCheckPeriod  = 5f;
    public float MovementThreshold  = 0.1f;
    public float AngulationThreshold  = 30f;
    public float AngulationCheckInterval  = 10f;
    public float CecumReachedCheckInterval  = 3f;
    public bool CecumReached = false;

    protected CircularBuffer<TrainingTimeStamp> trainingTimeStampsBuffer;
    protected List<float> segmentsStartValueInSpline;
    protected string helperSystemPromptString = "";
    protected string supervisorSystemPromptString = "";
    protected string excesiveSpeedLvlOneString = "";
    protected string excesiveSpeedLvlTwoString = "";
    protected string excesiveSpeedLvlThreeString = "";
    protected string excesiveForwardAngulationString = "";
    protected string cecumReachedString = "";

    void Awake()
    {
        SetSystemPromptStrings();
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    protected virtual void Start()
    {
        segmentsStartValueInSpline = new List<float> { 0, 2.844923f, 4.50978f, 7.392925f, 11.29504f, 12.54126f };
        trainingTimeStampsBuffer = new CircularBuffer<TrainingTimeStamp>(TrainingMemoryBufferCapacity);
        CaptureTrainingTimeStamp();
        UpdatePromptStrings();
    }

    private void OnLocaleChanged(Locale locale)
    {
        UpdatePromptStrings();
    }

    protected virtual void UpdatePromptStrings()
    {
        SetSystemPromptStrings();
        SetApplicationPromptStrings();
    }

    private void SetSystemPromptStrings()
    {
        helperSystemPromptString = GetPromptString(helperSystemPrompt);
        supervisorSystemPromptString = GetPromptString(supervisorSystemPrompt);
    }

    private void SetApplicationPromptStrings()
    {
        excesiveSpeedLvlOneString = GetPromptString(StandardPrompt.ExcesiveSpeedLvlOne);
        excesiveSpeedLvlTwoString = GetPromptString(StandardPrompt.ExcesiveSpeedLvlTwo);
        excesiveSpeedLvlThreeString = GetPromptString(StandardPrompt.ExcesiveSpeedLvlThree);
        excesiveForwardAngulationString = GetPromptString(StandardPrompt.ExcesiveForwardAngulation);
        cecumReachedString = GetPromptString(StandardPrompt.CecumReached);
    }

    public void Init()
    {
        Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "INIT TRAINING PROGRESS CONTROLLER");
        InvokeRepeating(nameof(CaptureTrainingTimeStamp), 0, LoggingInterval);
        InvokeRepeating(nameof(CheckNavigationSpeed), SpeedCheckInterval, SpeedCheckInterval);
        InvokeRepeating(nameof(CheckAngulationDuringMovement), AngulationCheckInterval, AngulationCheckInterval);
        InvokeRepeating(nameof(CheckCecumReached), CecumReachedCheckInterval, CecumReachedCheckInterval);
    }

    public void Stop()
    {
        Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "STOP TRAINING PROGRESS CONTROLLER");
        CancelInvoke();
    }

    public void Reset()
    {
        Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "RESET TRAINING PROGRESS CONTROLLER");
        trainingTimeStampsBuffer.Clear();
        CaptureTrainingTimeStamp();
    }

    private void CaptureTrainingTimeStamp()
    {
        var trainingTimeStamp = new TrainingTimeStamp
        {
            time = Time.time,
            cameraPosInSpline = simpleSplineNavigator.cameraRate,
            angulation = simpleSplineNavigator.bottomTopAngulationValue,
        };
        trainingTimeStampsBuffer.Add(trainingTimeStamp);
    }

    private void CheckNavigationSpeed()
    {
        var timeStamps = trainingTimeStampsBuffer.GetLastTwoElements();
        if (timeStamps != null)
        {
            var currentSpeed = CalculateNavigationSpeed();

            if (currentSpeed >= LimitSpeed * HighSpeedMultiplier)
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"{excesiveSpeedLvlThreeString}: {currentSpeed}", Debugger.MessageType.Warn);
                SendLLMRequest?.Invoke(excesiveSpeedLvlThreeString, supervisorSystemPromptString);
            }
            else if (currentSpeed >= LimitSpeed * MediumSpeedMultiplier)
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"{excesiveSpeedLvlTwoString} Speed: {currentSpeed}", Debugger.MessageType.Warn);
                SendLLMRequest?.Invoke(excesiveSpeedLvlTwoString, supervisorSystemPromptString);
            }
            else if (currentSpeed >= LimitSpeed)
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"{excesiveSpeedLvlOneString} Speed: {currentSpeed}", Debugger.MessageType.Warn);
                SendLLMRequest?.Invoke(excesiveSpeedLvlOneString, supervisorSystemPromptString);
            }
            else
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "Speed is within the limit: " + currentSpeed);
            }
        }
    }

    private void CheckAngulationDuringMovement()
    {
        if (trainingTimeStampsBuffer.Count == 0) return;

        int numberOfTimestampsToConsider = Mathf.FloorToInt(AngulationCheckPeriod / LoggingInterval);
        float totalAngulation = 0f;
        int movementCount = 0;

        for (int i = trainingTimeStampsBuffer.Count - 1; i >= 0 && movementCount < numberOfTimestampsToConsider; i--)
        {
            var timeStamp = trainingTimeStampsBuffer.GetAt(i);
            float speed = CalculateNavigationSpeed(numberOfTimestampsToConsider);
            if (speed > MovementThreshold)
            {
                totalAngulation += timeStamp.angulation;
                movementCount++;
            }
        }

        if (movementCount > 0)
        {
            float averageAngulation = totalAngulation / movementCount;
            if (averageAngulation > AngulationThreshold)
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"{excesiveForwardAngulationString}: {averageAngulation}", Debugger.MessageType.Warn);
                SendLLMRequest?.Invoke(excesiveForwardAngulationString, supervisorSystemPromptString);
            }
            else
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"Angulation is within safe limits: {averageAngulation}");
            }
        }
        else
        {
            Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "No movement detected in the last set of timestamps.");
        }
    }

    private float CalculateNavigationSpeed(int timeStamps = 0)
    {
        var lastTimestamps = trainingTimeStampsBuffer.GetLastNElements(timeStamps == 0 ? SpeedCheckTimestamps : timeStamps);
        if (lastTimestamps.Count < 2) return 0f;

        float totalDistance = 0f;
        float totalTime = 0f;

        for (int i = 1; i < lastTimestamps.Count; i++)
        {
            float distance = Mathf.Abs(lastTimestamps[i].cameraPosInSpline - lastTimestamps[i - 1].cameraPosInSpline);
            float timeDifference = lastTimestamps[i].time - lastTimestamps[i - 1].time;

            if (timeDifference > 0)
            {
                totalDistance += distance;
                totalTime += timeDifference;
            }
        }

        return totalTime > 0 ? totalDistance / totalTime : 0f;
    }

    private void CheckCecumReached()
    {
        var splineLength = simpleSplineNavigator.TargetSpline.Length;
        var lastSegmentLength = splineLength - segmentsStartValueInSpline[^1];
        if (simpleSplineNavigator.cameraRate > segmentsStartValueInSpline[^1] + lastSegmentLength / 2)
        {
            CecumReached = true;
            Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, cecumReachedString, Debugger.MessageType.Warn);
            SendLLMRequest?.Invoke(cecumReachedString, supervisorSystemPromptString);
            CancelInvoke(nameof(CheckCecumReached));
        }
    }

    public void OnSTTResponseReceived(string audioTranscription)
    {
        SendLLMRequest?.Invoke(audioTranscription, helperSystemPromptString);
    }

    public struct TrainingTimeStamp
    {
        public float cameraPosInSpline;
        public float time;
        public float angulation;
    }
}