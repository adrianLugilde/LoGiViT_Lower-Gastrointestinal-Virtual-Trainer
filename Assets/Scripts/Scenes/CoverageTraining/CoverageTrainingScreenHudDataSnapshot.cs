public readonly struct CoverageTrainingScreenHudDataSnapshot : ITrainingScreenHudDataSnapshotBase
{
    public string TimerText { get; }
    public float CoveragePercentage { get; }

    public CoverageTrainingScreenHudDataSnapshot(string timerText, float coveragePercentage)
    {
        TimerText = timerText;
        CoveragePercentage = coveragePercentage;
    }
}
