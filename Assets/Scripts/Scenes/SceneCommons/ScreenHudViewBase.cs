using TMPro;
using UnityEngine;

public abstract class ScreenHudViewBase<TScreenHudDataSnapshot> : MonoBehaviour
{
    [SerializeField] public MonoBehaviour _dataProvider;
    [SerializeField] protected TextMeshProUGUI _timerText;
    [SerializeField] protected GameObject _trainingSpecificDataHolder;

    protected ITrainingScreenHudDataProvider<TScreenHudDataSnapshot> feed;

    protected void Awake()
    {
        feed = _dataProvider as ITrainingScreenHudDataProvider<TScreenHudDataSnapshot>;
        feed.TrainingScreenHudDataUpdate += UpdateData;
        feed.ToggleTrainingSpecificDataActive += ToggleTrainingSpecificDataActive;
    }

    protected virtual void ToggleTrainingSpecificDataActive(bool active)
    {
        _trainingSpecificDataHolder.SetActive(active);
    }

    public virtual void UpdateTimerText(string time)
    {
        if (_timerText != null && !string.IsNullOrEmpty(time))
        {
            _timerText.text = time;
        }
    }

    protected abstract void UpdateData(TScreenHudDataSnapshot s);
}