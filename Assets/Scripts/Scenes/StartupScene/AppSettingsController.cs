using System;
using UnityEngine;
using UnityEngine.Localization;

public class AppSettingsController : MonoBehaviour, IAppSettingsProvider
{
    public event Action<bool> OnUseGrabKeybindProfilesChanged;

    public Locale Locale
    {
        get => AppSettings.Locale;
        set => AppSettings.Locale = value;
    }

    public bool UseGrabKeybindProfiles
    {
        get => AppSettings.UseGrabKeybindProfiles;
        set
        {
            AppSettings.UseGrabKeybindProfiles = value;
            OnUseGrabKeybindProfilesChanged?.Invoke(value);
        }
    }

    private void Awake()
    {
        AppSettings.ApplySavedLocale();
    }
}
