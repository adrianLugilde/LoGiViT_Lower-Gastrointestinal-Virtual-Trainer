public sealed class CoverageTrainingRankingPolicy : ITrainingRankingPolicy<CoverageTrainingResult>
{
    public int Compare(CoverageTrainingResult a, CoverageTrainingResult b)
    {
        // Primary: higher Score, Secondary: lower Time, Tertiary: higher Coverage
        int byScore = b.Score.CompareTo(a.Score);
        if (byScore != 0) return byScore;
        int byTime = a.TimeInSeconds.CompareTo(b.TimeInSeconds);
        if (byTime != 0) return byTime;
        int byCoverage = b.CoveragePercent.CompareTo(a.CoveragePercent);
        if (byCoverage != 0) return byCoverage;
        return 0; // consider equal if all criteria are the same
    }
}