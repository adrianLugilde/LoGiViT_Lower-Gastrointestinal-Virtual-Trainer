// ============================================================================
// ModelNoiseSettingsController.cs
//
// Panel controller for per-model anatomical noise settings in Tract edition mode.
// Exposes 3 sliders + 2 toggles + 1 seed randomize button.
//
// Exposed controls:
//   Surface bumps toggle  → NoiseSettings.MeshNoiseEnabled
//   Bump depth slider     → NoiseSettings.RadialAmplitude  (slider maxValue = 0.5)
//   Bump density slider   → NoiseSettings.AxialFrequency   (slider maxValue = 50)
//   Tract torsion toggle  → NoiseSettings.UpVectorNoiseEnabled
//   Torsion amount slider → NoiseSettings.UpVectorNoiseMaxAngle (slider maxValue = 90)
//   Randomize seed button → NoiseSettings.Seed (random int)
//
// Hidden advanced params (AngularFrequency, CenterFade, UpVectorNoiseFrequency,
// UpVectorNoiseMaxStepAngle) keep their default values and are not exposed in UI.
//
// Regeneration strategy:
//   Sliders: update NoiseSettings in memory on every drag tick (cheap);
//            trigger full GenerateSegmentedModel() only on pointer-up (expensive).
//   Toggles: regenerate immediately on toggle (boolean — no drag).
//   Seed:    regenerate immediately on button press.
// ============================================================================

using System;
using CustomUI;
using ModelEditor;
using UnityEngine;

/// <summary>
/// MonoBehaviour panel controller for per-model noise configuration.
/// Lives in the ModelEditor scene hierarchy; found via GetComponentInChildren.
/// Implements ITrackpadSliderTarget so VR trackpad input can drive the selected
/// noise slider while in Tract edition mode.
/// </summary>
public class ModelNoiseSettingsController : MonoBehaviour, ITrackpadSliderTarget
{
    #region Serialized Fields

    [Header("Surface Bumps")]
    [Tooltip("Toggle: enables/disables mesh surface bump noise.")]
    [SerializeField] private SwitchController _meshNoiseSwitch;
    [Tooltip("RadialAmplitude. Configure slider: maxValue=0.05, decimals=3.")]
    [SerializeField] private SelectableRadialSlider _bumpDepthSlider;
    [Tooltip("AxialFrequency. Configure slider: maxValue=50, decimals=1.")]
    [SerializeField] private SelectableRadialSlider _bumpDensitySlider;

    [Header("Tract Torsion")]
    [Tooltip("Toggle: enables/disables up-vector roll noise.")]
    [SerializeField] private SwitchController _torsionNoiseSwitch;
    [Tooltip("UpVectorNoiseMaxAngle. Configure slider: maxValue=90, decimals=0.")]
    [SerializeField] private SelectableRadialSlider _torsionNoiseAmountSlider;

    [Header("Seed")]
    [SerializeField] private ButtonController _randomizeSeedButton;

    private ToggleGroupController _sliderSelectionToggleGroupController;

    #endregion

    #region Private Fields

    private ISplineModelGenerator _generator;
    private Action _onSettingsChanged;
    // Suppresses callbacks during programmatic UI sync to avoid spurious regeneration.
    private bool _isInitializing;

    // VR trackpad slider selection — at most one noise slider selected at a time.
    // Cleared automatically via OnDisableNotified when the owning panel is hidden.
    private SelectableRadialSlider _selectedSlider;

    #endregion

    #region Initialization

    private void Awake()
    {
        _sliderSelectionToggleGroupController = gameObject.GetComponentInChildren<ToggleGroupController>();
        if (_sliderSelectionToggleGroupController != null)
        {
            _sliderSelectionToggleGroupController.RegisterToggleController(_bumpDepthSlider.ToggleController);
            _sliderSelectionToggleGroupController.RegisterToggleController(_bumpDensitySlider.ToggleController);
            _sliderSelectionToggleGroupController.RegisterToggleController(_torsionNoiseAmountSlider.ToggleController);
        }
    }


    /// <summary>
    /// Called by ModelEditorManager.ConfigureControllers() after the generator is ready.
    /// </summary>
    public void Initialize(ISplineModelGenerator generator, Action onSettingsChanged)
    {
        _generator = generator;
        _onSettingsChanged = onSettingsChanged;
        SubscribeEvents();
    }

    private void SubscribeEvents()
    {
        // Toggles regenerate immediately (no drag — just flip the bool)
        _meshNoiseSwitch.OnSelected += OnMeshNoiseEnabled;
        _meshNoiseSwitch.OnDeselected += OnMeshNoiseDisabled;
        _torsionNoiseSwitch.OnSelected += OnTorsionNoiseEnabled;
        _torsionNoiseSwitch.OnDeselected += OnTorsionNoiseDisabled;

        // Sliders: flush settings to memory and regenerate only on pointer-up
        _bumpDepthSlider.OnSliderPointerUp += (_, _) => FlushSliderValuesToSettingsAndRegenerate();
        _bumpDensitySlider.OnSliderPointerUp += (_, _) => FlushSliderValuesToSettingsAndRegenerate();
        _torsionNoiseAmountSlider.OnSliderPointerUp += (_, _) => FlushSliderValuesToSettingsAndRegenerate();

        _randomizeSeedButton.OnSelected += OnRandomizeSeed;

        // VR trackpad selection — toggle group ensures at most one selected at a time.
        // OnDisableNotified auto-clears selection when panel is hidden.
        SubscribeSliderSelection(_bumpDepthSlider);
        SubscribeSliderSelection(_bumpDensitySlider);
        SubscribeSliderSelection(_torsionNoiseAmountSlider);
    }

    #endregion

    #region UI Sync

    /// <summary>Pushes current generator noise settings into the UI controls without triggering callbacks.</summary>
    public void SyncUIFromModelNoiseSettings()
    {
        _isInitializing = true;

        var s = _generator.GetNoiseSettings();

        Debug.Log($"Syncing noise settings to UI: MeshNoiseEnabled={s.MeshNoiseEnabled}, RadialAmplitude={s.RadialAmplitude}, AxialFrequency={s.AxialFrequency}, UpVectorNoiseEnabled={s.UpVectorNoiseEnabled}, UpVectorNoiseMaxAngle={s.UpVectorNoiseMaxAngle}, Seed={s.Seed}");
        if (s.MeshNoiseEnabled) _meshNoiseSwitch.Select();
        else _meshNoiseSwitch.Deselect();

        _bumpDepthSlider.SetSliderValueSilent(s.RadialAmplitude);
        _bumpDensitySlider.SetSliderValueSilent(s.AxialFrequency);
        SetBumpSlidersInteractable(s.MeshNoiseEnabled);
        if (s.UpVectorNoiseEnabled) _torsionNoiseSwitch.Select();
        else _torsionNoiseSwitch.Deselect();

        _torsionNoiseAmountSlider.SetSliderValueSilent(s.UpVectorNoiseMaxAngle);
        _torsionNoiseAmountSlider.ToggleController.IsInteractable = s.UpVectorNoiseEnabled;

        _isInitializing = false;
    }

    /// <summary>Reads slider values, writes them into the generator's noise settings, then triggers full regeneration.</summary>
    private void FlushSliderValuesToSettingsAndRegenerate()
    {
        if (_isInitializing) return;
        var s = _generator.GetNoiseSettings();
        s.RadialAmplitude = _bumpDepthSlider.SliderValue;
        s.AxialFrequency = _bumpDensitySlider.SliderValue;
        s.UpVectorNoiseMaxAngle = _torsionNoiseAmountSlider.SliderValue;
        _generator.ApplyNoiseSettings(s);
        var appliedSettings = _generator.GetNoiseSettings();
        Debug.Log($"Applied noise settings from sliders: RadialAmplitude={appliedSettings.RadialAmplitude}, AxialFrequency={appliedSettings.AxialFrequency}, UpVectorNoiseMaxAngle={appliedSettings.UpVectorNoiseMaxAngle}");
        RegenerateAndNotify();
    }

    private void SetBumpSlidersInteractable(bool interactable)
    {
        _bumpDepthSlider.ToggleController.IsInteractable = interactable;
        _bumpDensitySlider.ToggleController.IsInteractable = interactable;
    }

    #endregion

    #region Event Handlers

    private void OnMeshNoiseEnabled()
    {
        if (_isInitializing) return;
        var s = _generator.GetNoiseSettings();
        s.MeshNoiseEnabled = true;
        _generator.ApplyNoiseSettings(s);
        SetBumpSlidersInteractable(true);
        RegenerateAndNotify();
    }

    private void OnMeshNoiseDisabled()
    {
        if (_isInitializing) return;
        var s = _generator.GetNoiseSettings();
        s.MeshNoiseEnabled = false;
        _generator.ApplyNoiseSettings(s);
        SetBumpSlidersInteractable(false);
        RegenerateAndNotify();
    }

    private void OnTorsionNoiseEnabled()
    {
        if (_isInitializing) return;
        var s = _generator.GetNoiseSettings();
        s.UpVectorNoiseEnabled = true;
        _generator.ApplyNoiseSettings(s);
        _torsionNoiseAmountSlider.IsInteractable = true;
        RegenerateAndNotify();
    }

    private void OnTorsionNoiseDisabled()
    {
        if (_isInitializing) return;
        var s = _generator.GetNoiseSettings();
        s.UpVectorNoiseEnabled = false;
        _generator.ApplyNoiseSettings(s);
        _torsionNoiseAmountSlider.IsInteractable = false;
        RegenerateAndNotify();
    }

    private void OnRandomizeSeed()
    {
        if (_isInitializing) return;
        var s = _generator.GetNoiseSettings();
        s.Seed = UnityEngine.Random.Range(0, int.MaxValue);
        _generator.ApplyNoiseSettings(s);
        Debug.Log($"Applied noise settings from sliders: RadialAmplitude={s.RadialAmplitude}, AxialFrequency={s.AxialFrequency}, UpVectorNoiseMaxAngle={s.UpVectorNoiseMaxAngle}");
        RegenerateAndNotify();
    }

    #endregion

    #region Regeneration

    private void RegenerateAndNotify()
    {
        _generator.GenerateSegmentedModel(destroyExisting: false);
        _onSettingsChanged?.Invoke();
    }

    #endregion

    #region VR Trackpad Slider Selection

    /// <summary>
    /// Subscribes a slider's toggle to set/clear _selectedSlider.
    /// OnDisableNotified auto-clears selection when the slider's GameObject is disabled.
    /// </summary>
    private void SubscribeSliderSelection(SelectableRadialSlider slider)
    {
        if (slider.ToggleController != null)
        {
            slider.ToggleController.OnSelected += () =>
            {
                _selectedSlider = slider;
                Debug.Log("Selected slider: " + slider.transform.parent.name);
            };
            slider.ToggleController.OnDeselected += () => { if (_selectedSlider == slider) _selectedSlider = null; };
        }
        slider.OnDisableNotified += () => { if (_selectedSlider == slider) _selectedSlider = null; };
    }

    #endregion

    #region ITrackpadSliderTarget

    /// <inheritdoc/>
    public void UpdateSelectedSliderByDelta(float delta)
    {
        if (_selectedSlider == null) return;
        Debug.Log("UpdateSelectedSliderByDelta: delta=" + delta);
        _selectedSlider.SetSliderValue(_selectedSlider.SliderValueRaw + delta * (_selectedSlider.maxValue / 100f));
        // FlushSliderValuesToSettings is triggered via OnSliderValueChanged subscription above.
    }

    /// <inheritdoc/>
    public void StoreSelectedSliderPreviousValue()
    {
        if (_selectedSlider != null)
            _selectedSlider.StorePreviousValue();
    }

    /// <inheritdoc/>
    public void OnTrackpadReleased()
    {
        if (_selectedSlider == null) return;
        if (!Mathf.Approximately(_selectedSlider.SliderValueRaw, _selectedSlider.GetPreviousValue()))
            FlushSliderValuesToSettingsAndRegenerate();
    }

    #endregion
}
