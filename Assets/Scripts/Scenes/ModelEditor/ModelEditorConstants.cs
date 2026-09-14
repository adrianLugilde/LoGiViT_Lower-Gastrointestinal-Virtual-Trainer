// ============================================================================
// ModelEditorConstants.cs
//
// Centralized constants for the Model Editor scene.
// Contains URLs, timing values, and other magic numbers/strings
// extracted from ModelEditorManager and related classes.
// ============================================================================

namespace ModelEditor
{
    /// <summary>
    /// Centralized constants for the Model Editor scene.
    /// </summary>
    public static class ModelEditorConstants
    {
        #region URLs

        /// <summary>
        /// URL to the user guide video for the Model Editor.
        /// </summary>
        public const string HelpGuideVideoUrl = "https://drive.google.com/file/d/16bG2ipkX5rMATqSU-jK6nj9yTnH99VSs/view?usp=sharing";

        #endregion

        #region Input Settings

        /// <summary>
        /// Default sensitivity for guided camera movement along the spline.
        /// </summary>
        public const float DefaultMovementSensitivity = 0.1f;

        /// <summary>
        /// Default multiplier for guided camera rate changes.
        /// </summary>
        public const float DefaultGuidedCameraRateMultiplier = 0.1f;

        /// <summary>
        /// Default sensitivity for guided camera rotation.
        /// </summary>
        public const float DefaultGuidedCameraRotationSensitivity = 0.1f;

        /// <summary>
        /// Default sensitivity for guided camera spin (roll).
        /// </summary>
        public const float DefaultGuidedCameraSpinSensitivity = 0.1f;

        #endregion

        #region Camera Limits

        /// <summary>
        /// Maximum pitch angle (in degrees) for guided camera rotation.
        /// </summary>
        public const float MaxPitchAngle = 80f;

        /// <summary>
        /// Minimum pitch angle (in degrees) for guided camera rotation.
        /// </summary>
        public const float MinPitchAngle = -80f;

        #endregion

        #region Timing

        /// <summary>
        /// Delay (in seconds) after D-pad touch before releasing locomotion.
        /// </summary>
        public const float DpadTouchReleaseDelay = 0.5f;

        /// <summary>
        /// Delay (in seconds) after D-pad release before fully releasing.
        /// </summary>
        public const float DpadReleaseDelay = 1f;

        #endregion

        #region Spline Navigation

        /// <summary>
        /// Offset added to diseases guided camera rate when starting at 0.
        /// Prevents camera from being placed at the exact spline start.
        /// </summary>
        public const float DiseasesGuidedCameraStartOffset = 0.1f;

        /// <summary>
        /// Number of nodes before the end to stop navigation.
        /// -2 because last node is the appendix.
        /// </summary>
        public const int SplineNavigationEndNodeOffset = 2;

        #endregion
    }
}
