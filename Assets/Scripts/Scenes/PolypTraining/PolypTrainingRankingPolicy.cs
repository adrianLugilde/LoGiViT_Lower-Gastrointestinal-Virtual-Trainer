public sealed class PolypTrainingRankingPolicy : ITrainingRankingPolicy<PolypTrainingResult>
{
    public int Compare(PolypTrainingResult a, PolypTrainingResult b)
    {
        // Primary: higher Score, Secondary: lower Time, Tertiary: higher PolypsIdentified
        int byScore = b.Score.CompareTo(a.Score);
        if (byScore != 0) return byScore;
        int byTime = a.TimeInSeconds.CompareTo(b.TimeInSeconds);
        if (byTime != 0) return byTime;
        int byCoverage = b.PolypsIdentified.CompareTo(a.PolypsIdentified);
        if (byCoverage != 0) return byCoverage;
        return 0; // consider equal if all criteria are the same
    }
}