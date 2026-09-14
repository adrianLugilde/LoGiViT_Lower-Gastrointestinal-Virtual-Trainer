using Michsky.MUIP;
using UnityEngine;

public class CoverageTrainingScreenHudView : ScreenHudViewBase<CoverageTrainingScreenHudDataSnapshot>   
{
    [SerializeField] private ProgressBar _coverageProgressBar;

    public void UpdateCoverageProgressBar(float value)
    {
        if (_coverageProgressBar != null && value > 0f)
        {
            _coverageProgressBar.currentPercent = value;
            _coverageProgressBar.UpdateUI();
        }
    }

    protected override void UpdateData(CoverageTrainingScreenHudDataSnapshot snapshot)
    {
        UpdateCoverageProgressBar(snapshot.CoveragePercentage);
        UpdateTimerText(snapshot.TimerText);
    }
}