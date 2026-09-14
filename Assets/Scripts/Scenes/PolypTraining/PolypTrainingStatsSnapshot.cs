namespace PolypTraining
{
    public readonly struct PolypTrainingStatsSnapshot
    {
        public readonly float TimerSeconds;
        public readonly float PolypsDetected;
        public readonly float PolypsIdentified;
        public readonly float FinalScore;


        public PolypTrainingStatsSnapshot(
            float timerSeconds,
            float polypsDetected,
            float polypsIdentified,
            float globalScore
            )
        {
            TimerSeconds = timerSeconds;
            PolypsDetected = polypsDetected;
            PolypsIdentified = polypsIdentified;
            FinalScore = globalScore;
        }
    }
}