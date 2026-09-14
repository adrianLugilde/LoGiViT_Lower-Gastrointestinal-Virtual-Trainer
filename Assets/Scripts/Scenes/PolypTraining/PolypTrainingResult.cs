using System;

[Serializable]
public class PolypTrainingResult : TrainingResult
{
    public int PolypsIdentified;
    public int TotalPolypsCount;

    public override string GetExclusiveTrainingParameterString()
    {
        return string.Format("{0}/{1}", PolypsIdentified, TotalPolypsCount);
    }
}