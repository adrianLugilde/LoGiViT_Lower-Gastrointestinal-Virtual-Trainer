        public readonly struct CoverageTrainingStatsSnapshot
        {
            public readonly float TimerSeconds;
            public readonly float OrganExplored;
            public readonly float RectumExplored;
            public readonly float SigmoidExplored;
            public readonly float DescendingExplored;
            public readonly float TransverseExplored;
            public readonly float AscendingExplored;
            public readonly float CecumExplored;
            public readonly float FinalScore;

            public CoverageTrainingStatsSnapshot(
                float timerSeconds,
                float organExplored,
                float rectumExplored,
                float sigmoidExplored,
                float descendingExplored,
                float transverseExplored,
                float ascendingExplored,
                float cecumExplored,
                float finalScore)
            {
                TimerSeconds = timerSeconds;
                OrganExplored = organExplored;
                RectumExplored = rectumExplored;
                SigmoidExplored = sigmoidExplored;
                DescendingExplored = descendingExplored;
                TransverseExplored = transverseExplored;
                AscendingExplored = ascendingExplored;
                CecumExplored = cecumExplored;
                FinalScore = finalScore;
            }
        }
