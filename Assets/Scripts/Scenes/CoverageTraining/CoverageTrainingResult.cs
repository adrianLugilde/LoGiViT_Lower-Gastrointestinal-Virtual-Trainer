using System;

[Serializable]
public class CoverageTrainingResult : TrainingResult
{
    public float CoveragePercent;

    public override string GetExclusiveTrainingParameterString()
    {
        return string.Format("{0:0.00}%", CoveragePercent);
    }
}