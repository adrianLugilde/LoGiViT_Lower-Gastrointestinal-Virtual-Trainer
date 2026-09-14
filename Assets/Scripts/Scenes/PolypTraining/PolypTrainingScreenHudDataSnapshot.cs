public readonly struct PolypTrainingScreenHudDataSnapshot : ITrainingScreenHudDataSnapshotBase
{
    public string TimerText { get; }
    public string IdentifiedPolypCountText { get; }
    public bool? PolypInIdentification { get; }

    public PolypTrainingScreenHudDataSnapshot(string timerText, string polypsIdentifiedText, bool? polypInIdentification = null)
    {
        TimerText = timerText;
        IdentifiedPolypCountText = polypsIdentifiedText;
        PolypInIdentification = polypInIdentification;
    }
}
