using System;
using UnityEngine.Localization;

public interface IAppSettingsProvider
{
    Locale Locale { get; set; }
    bool UseGrabKeybindProfiles { get; set; }

    event Action<bool> OnUseGrabKeybindProfilesChanged;
}
