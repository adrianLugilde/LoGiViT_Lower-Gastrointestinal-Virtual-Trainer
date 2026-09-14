// ============================================================================
// ITrackpadSliderTarget.cs
//
// Implemented by any controller that can receive VR trackpad input to drive
// slider adjustments. ModelEditorManager routes the three trackpad events
// (touch-start, delta, touch-end) to whichever controller owns the active
// edition mode via GetTrackpadTarget().
//
// Implementations:
//   BlendshapesController  — Details mode (multi-slider, undo record on release)
//   ModelNoiseSettingsController — Tract mode (single-slider, regenerate on release)
// ============================================================================

namespace ModelEditor
{
    /// <summary>
    /// Receives VR right-hand trackpad events for slider adjustment.
    /// </summary>
    public interface ITrackpadSliderTarget
    {
        /// <summary>Applies <paramref name="delta"/> to the currently selected slider(s).</summary>
        void UpdateSelectedSliderByDelta(float delta);

        /// <summary>
        /// Called when the trackpad is first touched.
        /// Implementations should snapshot current slider value(s) here for later diffing.
        /// </summary>
        void StoreSelectedSliderPreviousValue();

        /// <summary>
        /// Called when the trackpad touch ends.
        /// Implementations should commit the change (undo record, regeneration, etc.).
        /// </summary>
        void OnTrackpadReleased();
    }
}
