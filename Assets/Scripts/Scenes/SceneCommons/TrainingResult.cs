using System;

[Serializable]
public abstract class TrainingResult
{
    public string PlayerName;
    public float Score;
    public float TimeInSeconds;
    public abstract string GetExclusiveTrainingParameterString();
}
