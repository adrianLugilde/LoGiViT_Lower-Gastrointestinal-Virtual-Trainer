// ============================================================================
// XROverlayView.cs
//
// Displays overlay messages with fade in/out effects for XR applications.
// Supports localized messages, plain text, and countdown timers.
//
// Usage:
//   - ShowMessage(LocalizedString) - Display a localized message
//   - ShowMessage(string) - Display a plain text message
//   - ShowCountdownMessage(seconds) - Display countdown, fires OnCountdownEnded
//   - CloseOverlay() - Manually close the overlay
//
// Configuration (Inspector):
//   - _transitionSpeed: Fade in/out speed (default: 8)
//   - _displayDuration: How long temporary messages stay visible (default: 2s)
//
// Events:
//   - OnCountdownEnded: Fired when countdown reaches zero
// ============================================================================

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

/// <summary>
/// Displays overlay messages with fade effects for XR applications.
/// Supports localized strings, plain text, and countdown timers.
/// </summary>
public class XROverlayView : MonoBehaviour
{
    #region Serialized Fields

    /// <summary>
    /// Localized string event for displaying localized messages.
    /// </summary>
    [SerializeField] 
    private LocalizeStringEvent _localizedStringEvent;

    /// <summary>
    /// TextMeshPro component for displaying the message text.
    /// </summary>
    [SerializeField] 
    private TextMeshProUGUI _messageTMP;

    /// <summary>
    /// Canvas group controlling visibility and fade.
    /// </summary>
    [SerializeField] 
    private CanvasGroup _canvasGroup;

    /// <summary>
    /// Speed of fade in/out transitions.
    /// </summary>
    [SerializeField] 
    private float _transitionSpeed = 8f;

    /// <summary>
    /// Default duration for temporary messages before auto-close.
    /// </summary>
    [SerializeField] 
    private float _displayDuration = 2f;

    #endregion

    #region Events

    /// <summary>
    /// Fired when a countdown reaches zero.
    /// </summary>
    public event Action OnCountdownEnded;

    #endregion

    #region Private Fields

    /// <summary>
    /// Reference to active fade coroutine for proper cancellation.
    /// </summary>
    private Coroutine _fadeCoroutine;

    /// <summary>
    /// Reference to active auto-close coroutine for proper cancellation.
    /// </summary>
    private Coroutine _autoCloseCoroutine;

    /// <summary>
    /// Reference to active countdown coroutine for proper cancellation.
    /// </summary>
    private Coroutine _countdownCoroutine;

    #endregion

    #region Properties

    /// <summary>
    /// Returns true if the overlay is currently visible (alpha > 0).
    /// </summary>
    public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0;

    /// <summary>
    /// Returns true if a countdown is currently active.
    /// </summary>
    public bool IsCountdownActive => _countdownCoroutine != null;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Validates required components on startup.
    /// </summary>
    private void Awake()
    {
        if (_canvasGroup == null)
        {
            Debug.LogError("[XROverlayView] Canvas group not assigned.");
            enabled = false;
            return;
        }

        if (_messageTMP == null)
        {
            Debug.LogError("[XROverlayView] Message TMP not assigned.");
            enabled = false;
            return;
        }

        if (_localizedStringEvent == null)
        {
            Debug.LogError("[XROverlayView] Localized string event not assigned.");
            enabled = false;
            return;
        }
    }

    #endregion

    #region Public Methods - Message Display

    /// <summary>
    /// Shows a localized message on the overlay.
    /// </summary>
    /// <param name="localizedString">The localized string to display.</param>
    /// <param name="isTemporary">If true, auto-closes after display duration.</param>
    /// <param name="customDisplayDuration">Custom duration (0 = use default).</param>
    public void ShowMessage(LocalizedString localizedString, bool isTemporary = true, float customDisplayDuration = 0f)
    {
        _localizedStringEvent.StringReference = localizedString;
        
        ShowOverlayInternal(isTemporary, customDisplayDuration);
    }

    /// <summary>
    /// Shows a plain text message on the overlay.
    /// </summary>
    /// <param name="message">The text message to display.</param>
    /// <param name="isTemporary">If true, auto-closes after display duration.</param>
    /// <param name="customDisplayDuration">Custom duration (0 = use default).</param>
    public void ShowMessage(string message, bool isTemporary = true, float customDisplayDuration = 0f)
    {
        // Clear localized string and set plain text
        _localizedStringEvent.StringReference = null;
        _messageTMP.text = message;
        
        ShowOverlayInternal(isTemporary, customDisplayDuration);
    }

    /// <summary>
    /// Shows a countdown message that counts down from the specified number.
    /// Fires <see cref="OnCountdownEnded"/> when countdown reaches zero.
    /// </summary>
    /// <param name="countdownSeconds">Number of seconds to count down from.</param>
    public void ShowCountdownMessage(int countdownSeconds)
    {
        if (countdownSeconds <= 0)
        {
            Debug.LogWarning("[XROverlayView] Countdown must be greater than 0.");
            return;
        }

        // Clear any existing display state
        StopAllActiveCoroutines();

        // Clear localized string for plain number display
        _localizedStringEvent.StringReference = null;

        // Start fade in
        _fadeCoroutine = StartCoroutine(FadeInCoroutine());
        
        // Start countdown (handles its own closure and event firing)
        _countdownCoroutine = StartCoroutine(CountdownCoroutine(countdownSeconds));
    }

    /// <summary>
    /// Manually closes the overlay with a fade out effect.
    /// </summary>
    public void CloseOverlay()
    {
        StopAllActiveCoroutines();
        _fadeCoroutine = StartCoroutine(FadeOutCoroutine());
    }

    #endregion

    #region Private Methods - Display Logic

    /// <summary>
    /// Internal method to show overlay with optional auto-close.
    /// </summary>
    private void ShowOverlayInternal(bool isTemporary, float customDisplayDuration)
    {
        StopAllActiveCoroutines();

        // Start fade in
        _fadeCoroutine = StartCoroutine(FadeInCoroutine());

        // Schedule auto-close if temporary
        if (isTemporary)
        {
            float duration = customDisplayDuration > 0 ? customDisplayDuration : _displayDuration;
            _autoCloseCoroutine = StartCoroutine(AutoCloseCoroutine(duration));
        }
    }

    /// <summary>
    /// Stops all active coroutines safely.
    /// </summary>
    private void StopAllActiveCoroutines()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (_autoCloseCoroutine != null)
        {
            StopCoroutine(_autoCloseCoroutine);
            _autoCloseCoroutine = null;
        }

        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// Fades the overlay in by increasing alpha.
    /// </summary>
    private IEnumerator FadeInCoroutine()
    {
        while (_canvasGroup.alpha < 1f)
        {
            _canvasGroup.alpha += Time.unscaledDeltaTime * _transitionSpeed;
            yield return null;
        }

        _canvasGroup.alpha = 1f;
        _fadeCoroutine = null;
    }

    /// <summary>
    /// Fades the overlay out by decreasing alpha.
    /// </summary>
    private IEnumerator FadeOutCoroutine()
    {
        while (_canvasGroup.alpha > 0f)
        {
            _canvasGroup.alpha -= Time.unscaledDeltaTime * _transitionSpeed;
            yield return null;
        }

        _canvasGroup.alpha = 0f;
        _fadeCoroutine = null;
    }

    /// <summary>
    /// Waits for the specified duration then closes the overlay.
    /// </summary>
    private IEnumerator AutoCloseCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        
        _autoCloseCoroutine = null;
        _fadeCoroutine = StartCoroutine(FadeOutCoroutine());
    }

    /// <summary>
    /// Counts down from the specified number, updating text each second.
    /// Fires OnCountdownEnded and closes overlay when complete.
    /// </summary>
    private IEnumerator CountdownCoroutine(int countdownSeconds)
    {
        int remaining = countdownSeconds;

        while (remaining > 0)
        {
            _messageTMP.text = remaining.ToString();
            yield return new WaitForSeconds(1f);
            remaining--;
        }

        _countdownCoroutine = null;

        // Fire event before closing
        OnCountdownEnded?.Invoke();

        // Close the overlay
        _fadeCoroutine = StartCoroutine(FadeOutCoroutine());
    }

    #endregion
}
