using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace ModelEditor
{
    /// <summary>
    /// Component for blendshape slider prefabs (1, 2, or 3 sliders).
    /// Manages the panel's title display using localized strings.
    /// </summary>
    public class BlendshapeSliderPanel : MonoBehaviour
    {
        [Header("Title Display")]
        [Tooltip("The LocalizeStringEvent component that automatically updates the title text based on locale")]
        [SerializeField] private LocalizeStringEvent localizedTitleEvent;
        
        /// <summary>
        /// Sets the localized title for this panel.
        /// If null or empty, uses the name class as title.
        /// </summary>
        public void SetLocalizedTitle(LocalizedString localizedTitle)
        {
            if (localizedTitleEvent == null)
            {
                Debug.LogWarning($"BlendshapeSliderPanel on {gameObject.name}: No LocalizeStringEvent component assigned", this);
                return;
            }
            
            // Use provided title if valid, otherwise use default
            if (localizedTitle != null && !localizedTitle.IsEmpty)
            {
                localizedTitleEvent.StringReference = localizedTitle;
                localizedTitleEvent.RefreshString();
            }
            else
            {
                Debug.LogWarning($"BlendshapeSliderPanel on {gameObject.name}: No valid localized string provided and no default set", this);
                SetPlainTitle(this.GetType().Name);
            }
        }
        
        /// <summary>
        /// Sets a plain text title (non-localized) - creates a temporary LocalizedString from the text
        /// </summary>
        public void SetPlainTitle(string title)
        {
            if (localizedTitleEvent == null)
            {
                Debug.LogWarning($"BlendshapeSliderPanel on {gameObject.name}: No LocalizeStringEvent component assigned", this);
                return;
            }
            
            // Create a new LocalizedString with the plain text as a smart string
            var plainString = new LocalizedString { TableReference = "", LocaleOverride = null };
            plainString.SetReference("", title);
            localizedTitleEvent.StringReference = plainString;
            
            // Force refresh to apply the change
            localizedTitleEvent.RefreshString();
        }
    }
}