using UnityEngine;
using UnityEngine.Localization.Settings;
using System.Collections;

public class ForceLocale : MonoBehaviour
{
    [SerializeField] private string localeCode = "es";

    IEnumerator Start()
    {
        // Wait for the localization system to initialize
        yield return LocalizationSettings.InitializationOperation;

        // Find the locale by its code (e.g., "es", "en", "fr")
        var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);

        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
            Debug.Log($"Locale successfully set to: {localeCode}");
        }
        else
        {
            Debug.LogError($"Locale code '{localeCode}' not found in Localization Settings!");
        }
    }
}