using System;

public interface ITrainingScreenHudDataProvider<out TScreenHudDataSnapshot>
{
    event Action<TScreenHudDataSnapshot> TrainingScreenHudDataUpdate;
    event Action<bool> ToggleTrainingSpecificDataActive;
}