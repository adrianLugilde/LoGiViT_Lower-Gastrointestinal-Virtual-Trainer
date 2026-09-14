using LargeIntestine;
using static AIUtility.AIPromptsHelper;

public class CoverageTrainingProgressController : TrainingProgressController
{
    public NewCameraCoverage cameraCoverage;
    private LI_Segment currentIntestineSegment;
    private bool[] segmentsPassed = new bool[6];

    public LI_Segment CurrentIntestineSegment
    {
        get => currentIntestineSegment;
        private set
        {
            if (value != currentIntestineSegment)
            {
                CheckCurrentSegmentChange(value);
                currentIntestineSegment = value;
            }
        }
    }

    protected override void Start()
    {
        base.Start();
    }

    /*protected void Update()
    {
        UpdateCameraLocation();
    }*/

    public void UpdateProgress()
    {
        UpdateCameraLocation();
    }

    private void CheckCurrentSegmentChange(LI_Segment newSegment)
    {
        if (segmentsPassed[(int)currentIntestineSegment]) return;

        float currentSegmentCoverage = currentIntestineSegment switch
        {
            LI_Segment.Rectum => cameraCoverage.rectumCoveragePercentage,
            LI_Segment.Sigmoid => cameraCoverage.sigmoidCoveragePercentage,
            LI_Segment.Descending => cameraCoverage.descendingColonCoveragePercentage,
            LI_Segment.Transverse => cameraCoverage.transverseColonCoveragePercentage,
            LI_Segment.Ascending => cameraCoverage.ascendingColonCoveragePercentage,
            _ => cameraCoverage.cecumCoveragePercentage,
        };

        segmentsPassed[(int)currentIntestineSegment] = true;

        if (currentIntestineSegment == LI_Segment.Cecum) ResetControlPoints();

        var msg = GetPromptStringWithArgs(StandardPrompt.ExitingSegment, currentIntestineSegment, currentSegmentCoverage, newSegment);
        Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, msg, Debugger.MessageType.Log);
        SendLLMRequest?.Invoke(msg, supervisorSystemPromptString);
    }

    private void ResetControlPoints()
    {
        for (int i = 0; i < segmentsPassed.Length; i++)
        {
            segmentsPassed[i] = false;
        }
    }

    private void UpdateCameraLocation()
    {
        var cameraRate = simpleSplineNavigator.cameraRate;
        var segmentsCount = segmentsStartValueInSpline.Count;

        for (int i = segmentsCount - 1; i >= 0; i--)
        {
            if (cameraRate > segmentsStartValueInSpline[i])
            {
                CurrentIntestineSegment = (LI_Segment)i;
                return;
            }
        }
        CurrentIntestineSegment = LI_Segment.Rectum;
    }
}
