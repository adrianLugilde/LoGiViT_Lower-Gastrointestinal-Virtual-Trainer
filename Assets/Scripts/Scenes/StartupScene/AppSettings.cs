using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine;

public static class AppSettings
{
    const string KEY_LOCALE = "selected-locale";
    const string KEY_USE_GRAB_KEYBIND_PROFILES = "use_grab_keybind_profiles";

    public static Locale Locale
    {
        get
        {
            string id = PlayerPrefs.GetString(KEY_LOCALE, string.Empty);
            if (string.IsNullOrEmpty(id)) return LocalizationSettings.SelectedLocale;
            return LocalizationSettings.AvailableLocales.GetLocale(id);
        }
        set
        {
            LocalizationSettings.SelectedLocale = value;
            PlayerPrefs.SetString(KEY_LOCALE, value.Identifier.Code);
            PlayerPrefs.Save();
        }
    }

    public static bool UseGrabKeybindProfiles
    {
        get => PlayerPrefs.GetInt(KEY_USE_GRAB_KEYBIND_PROFILES, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(KEY_USE_GRAB_KEYBIND_PROFILES, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void ApplySavedLocale()
    {
        string id = PlayerPrefs.GetString(KEY_LOCALE, string.Empty);
        if (string.IsNullOrEmpty(id)) return;
        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(id);
        if (locale != null) LocalizationSettings.SelectedLocale = locale;
    }
}
