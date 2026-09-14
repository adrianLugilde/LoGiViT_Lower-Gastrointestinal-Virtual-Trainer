using UnityEngine.Localization;
using UnityEngine.Localization.Components;

public class CustomLocalizeStringEvent : LocalizeStringEvent
{
    public void SetText(LocalizedString localizedString)
    {
        StringReference = localizedString;
        StringReference.RefreshString();
    }
}