using TMPro;
using UnityEngine;

public class PolypTrainingScreenHudView : ScreenHudViewBase<PolypTrainingScreenHudDataSnapshot>
{
    [SerializeField] private TextMeshProUGUI _identifiedPolypsCountText;
    [SerializeField] private GameObject _polypInIdentificationMsgGo;
    
    public void UpdateIdentifiedPolypsText(string text)
    {
        if (_identifiedPolypsCountText != null && !string.IsNullOrEmpty(text))
        {
            _identifiedPolypsCountText.text = text;
        }
    }

    private void SetPolypInIdentificationMsgActive(bool isActive)
    {
        if (_polypInIdentificationMsgGo != null)
        {
            _polypInIdentificationMsgGo.SetActive(isActive);
        }
    }

    protected override void UpdateData(PolypTrainingScreenHudDataSnapshot snapshot)
    {
        UpdateTimerText(snapshot.TimerText);
        UpdateIdentifiedPolypsText(snapshot.IdentifiedPolypCountText);
        if (snapshot.PolypInIdentification.HasValue)
            SetPolypInIdentificationMsgActive(snapshot.PolypInIdentification.Value);
    }
}