using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LargeIntestine;

namespace ModelEditor
{
    /// <summary>
    /// Dynamically generates and manages blendshape sliders based on mesh profile groups.
    /// Uses 3 generic prefabs (1, 2, or 3 sliders) that are instantiated based on blendshape group size.
    /// </summary>
    public class DynamicBlendshapeSliderController : MonoBehaviour
    {
        [Header("Slider Prefabs")]
        [Tooltip("Prefab with 1 SplineMeshBlendshapeSlider for single-blendshape groups")]
        [SerializeField] private GameObject singleSliderPrefab;
        
        [Tooltip("Prefab with 2 SplineMeshBlendshapeSliders for 2-blendshape groups")]
        [SerializeField] private GameObject doubleSliderPrefab;
        
        [Tooltip("Prefab with 3 SplineMeshBlendshapeSliders for 3-blendshape groups")]
        [SerializeField] private GameObject tripleSliderPrefab;
        
        [Header("Container")]
        [SerializeField] private Transform sliderContainer;
        
        /// <summary>
        /// Invoked when any slider value changes. Parameters: blendshapeName, newValue
        /// </summary>
        public event Action<string, float> OnSliderValueChanged;
        
        /// <summary>
        /// Invoked when slider interaction ends. Parameters: blendshapeName, newValue, previousValue
        /// </summary>
        public event Action<string, float, float> OnSliderPointerUp;
        
        private Dictionary<string, SplineMeshBlendshapeSlider> _activeSliders = new Dictionary<string, SplineMeshBlendshapeSlider>();
        private List<GameObject> _instantiatedPrefabs = new List<GameObject>();
        private List<SplineMeshBlendshapeSlider> _selectedSliders = new List<SplineMeshBlendshapeSlider>();
        private List<int> _currentSectionIndices = new List<int>();
        private BlendshapeUpdateService _updateService;
        // Store delegates for proper event cleanup
        private Dictionary<SplineMeshBlendshapeSlider, Action> _sliderSelectedDelegates = new Dictionary<SplineMeshBlendshapeSlider, Action>();
        private Dictionary<SplineMeshBlendshapeSlider, Action> _sliderDeselectedDelegates = new Dictionary<SplineMeshBlendshapeSlider, Action>();
        private Dictionary<SplineMeshBlendshapeSlider, Action> _sliderDisableDelegates = new Dictionary<SplineMeshBlendshapeSlider, Action>();
        
        public IReadOnlyDictionary<string, SplineMeshBlendshapeSlider> ActiveSliders => _activeSliders;
        public IReadOnlyList<SplineMeshBlendshapeSlider> SelectedSliders => _selectedSliders;
        public IReadOnlyList<int> CurrentSectionIndices => _currentSectionIndices;
        
        /// <summary>
        /// Initializes the controller with a blendshape update service
        /// </summary>
        public void Initialize(BlendshapeUpdateService updateService)
        {
            if (_updateService != null)
                _updateService.OnBlendshapeRevalidated -= HandleBlendshapeRevalidated;

            _updateService = updateService;

            if (_updateService != null)
                _updateService.OnBlendshapeRevalidated += HandleBlendshapeRevalidated;
        }

        /// <summary>
        /// Gets the appropriate prefab based on the number of sliders needed
        /// </summary>
        private GameObject GetPrefabForSliderCount(int count)
        {
            return count switch
            {
                1 => singleSliderPrefab,
                2 => doubleSliderPrefab,
                3 => tripleSliderPrefab,
                _ => null
            };
        }
        
        /// <summary>
        /// Generates sliders for the specified section indices.
        /// Shows only blendshapes that are common to all selected sections.
        /// Collects groups from all unique mesh profiles.
        /// </summary>
        public void GenerateSlidersForSections(IEnumerable<int> sectionIndices)
        {
            _currentSectionIndices = sectionIndices.ToList();

            if (_currentSectionIndices.Count == 0)
            {
                ClearSliders();
                return;
            }

            // A blendshape should only get a slider if it is referenced by a panel
            // in EVERY selected section's profile. Being present in the blendshapes list
            // is not enough — it may be there to define a static shape, not for editing.
            var profiles = _updateService.GetUniqueProfilesForSections(_currentSectionIndices);

            // Per-section set of blendshape names that appear in at least one panel
            HashSet<string> panelCommonNames = null;
            foreach (var idx in _currentSectionIndices)
            {
                var profile = _updateService.GetProfileForSection(idx);
                if (profile == null || profile.blendshapePanels == null) continue;

                var namesInPanels = new HashSet<string>(
                    profile.blendshapePanels.SelectMany(p => p.blendshapeNames ?? Enumerable.Empty<string>()));

                if (panelCommonNames == null)
                    panelCommonNames = namesInPanels;
                else
                    panelCommonNames.IntersectWith(namesInPanels);
            }

            if (panelCommonNames == null || panelCommonNames.Count == 0)
            {
                ClearSliders();
                return;
            }

            // Build blendshape info lookup from all profiles (first occurrence wins per name)
            var blendshapeInfoLookup = new Dictionary<string, SplineMeshProfile.BlendshapeInfo>();
            foreach (var profile in profiles)
            {
                if (profile == null || profile.blendshapes == null) continue;
                foreach (var bs in profile.blendshapes)
                {
                    if (!blendshapeInfoLookup.ContainsKey(bs.name))
                        blendshapeInfoLookup[bs.name] = bs;
                }
            }

            var commonBlendshapes = blendshapeInfoLookup.Values
                .Where(b => panelCommonNames.Contains(b.name))
                .ToList();

            // Collect panels from all profiles, then filter each panel's blendshapes
            // to only those in panelCommonNames
            var allPanels = _updateService.GetPanelsForSections(_currentSectionIndices);

            GenerateSlidersFromPanels(commonBlendshapes, allPanels, panelCommonNames);
            LoadCurrentValues();
        }
        
        /// <summary>
        /// Generates sliders for a single mesh profile
        /// </summary>
        public void GenerateSlidersForProfile(SplineMeshProfile profile)
        {
            if (profile == null || profile.blendshapes == null)
            {
                ClearSliders();
                return;
            }
            
            var panels = profile.blendshapePanels ?? new List<SplineMeshProfile.BlendshapePanel>();
            GenerateSlidersFromPanels(profile.blendshapes, panels);
        }
        
        /// <summary>
        /// Clears all generated sliders
        /// </summary>
        public void ClearSliders()
        {
            _selectedSliders.Clear();
            
            // Unsubscribe from all slider events before clearing
            foreach (var slider in _activeSliders.Values)
            {
                if (slider == null) continue;
                
                slider.OnSliderValueChangedNotify -= HandleSliderValueChanged;
                slider.OnSliderPointerUpNotify -= HandleSliderPointerUp;
                
                // Unsubscribe toggle events
                if (_sliderSelectedDelegates.TryGetValue(slider, out var selectedDelegate))
                {
                    if (slider.ToggleController != null)
                        slider.ToggleController.OnSelected -= selectedDelegate;
                }
                if (_sliderDeselectedDelegates.TryGetValue(slider, out var deselectedDelegate))
                {
                    if (slider.ToggleController != null)
                        slider.ToggleController.OnDeselected -= deselectedDelegate;
                }
                if (_sliderDisableDelegates.TryGetValue(slider, out var disableDelegate))
                {
                    slider.OnDisableNotified -= disableDelegate;
                }
            }
            
            _sliderSelectedDelegates.Clear();
            _sliderDeselectedDelegates.Clear();
            _sliderDisableDelegates.Clear();
            _activeSliders.Clear();
            
            // Destroy instantiated prefab instances
            foreach (var prefabInstance in _instantiatedPrefabs)
            {
                if (prefabInstance != null)
                {
                    Destroy(prefabInstance);
                }
            }
            _instantiatedPrefabs.Clear();
        }
        
        /// <summary>
        /// Sets a slider value without triggering events
        /// </summary>
        public void SetSliderValueSilent(string blendshapeName, float value)
        {
            if (_activeSliders.TryGetValue(blendshapeName, out var slider))
            {
                slider.SetSliderValueSilent(value);
            }
        }
        
        /// <summary>
        /// Sets a slider value (triggers events)
        /// </summary>
        public void SetSliderValue(string blendshapeName, float value)
        {
            if (_activeSliders.TryGetValue(blendshapeName, out var slider))
            {
                slider.SetSliderValue(value);
            }
        }
        
        /// <summary>
        /// Gets current value of a slider
        /// </summary>
        public float GetSliderValue(string blendshapeName)
        {
            if (_activeSliders.TryGetValue(blendshapeName, out var slider))
            {
                return slider.SliderValueRaw;
            }
            return 0f;
        }
        
        /// <summary>
        /// Loads current blendshape values from the first selected section
        /// </summary>
        public void LoadCurrentValues()
        {
            if (_currentSectionIndices.Count == 0 || _updateService == null)
                return;
            
            // Load values from first selected section
            var values = _updateService.GetAllBlendshapeValues(_currentSectionIndices[0]);
            
            foreach (var kvp in values)
            {
                SetSliderValueSilent(kvp.Key, kvp.Value);
            }
        }
        
        /// <summary>
        /// Stores the previous values for selected sliders (for undo support with VR controller)
        /// </summary>
        public void StoreSelectedSlidersPreviousValues()
        {
            foreach (var slider in _selectedSliders)
            {
                if (slider == null) continue;
                slider.StorePreviousValue();
            }
        }
        
        #region Slider Selection (for VR controller input)
        
        /// <summary>
        /// Adds a slider to the selection for group adjustment
        /// </summary>
        public void AddSliderToSelection(SplineMeshBlendshapeSlider slider)
        {
            if (!_selectedSliders.Contains(slider))
            {
                _selectedSliders.Add(slider);
            }
        }
        
        /// <summary>
        /// Removes a slider from the selection
        /// </summary>
        public void RemoveSliderFromSelection(SplineMeshBlendshapeSlider slider)
        {
            _selectedSliders.Remove(slider);
        }
        
        /// <summary>
        /// Clears all selected sliders
        /// </summary>
        public void ClearSliderSelection()
        {
            _selectedSliders.Clear();
        }
        
        /// <summary>
        /// Updates all selected sliders by a delta value (for VR controller input)
        /// </summary>
        public void UpdateSelectedSlidersByDelta(float delta)
        {
            foreach (var slider in _selectedSliders)
            {
                if (slider == null) continue;
                var newValue = slider.SliderValueRaw + delta;
                slider.SetSliderValue(newValue);
            }
        }
        
        #endregion
        
        #region Slider Generation
        
        /// <summary>
        /// Generates sliders based on blendshape panels.
        /// Uses the appropriate prefab (1, 2, or 3 sliders) based on panel size.
        /// Only creates sliders for blendshapes explicitly defined in panels.
        /// </summary>
        private void GenerateSlidersFromPanels(List<SplineMeshProfile.BlendshapeInfo> blendshapes, List<SplineMeshProfile.BlendshapePanel> panels, HashSet<string> allowedNames = null)
        {
            ClearSliders();

            if (sliderContainer == null)
            {
                Debug.LogError("DynamicBlendshapeSliderController: Missing slider container reference");
                return;
            }

            if (singleSliderPrefab == null && doubleSliderPrefab == null && tripleSliderPrefab == null)
            {
                Debug.LogError("DynamicBlendshapeSliderController: No slider prefabs assigned");
                return;
            }

            // Build lookup for blendshape info by name
            var blendshapeInfoLookup = blendshapes.ToDictionary(b => b.name, b => b);
            var availableBlendshapeNames = allowedNames ?? new HashSet<string>(blendshapes.Select(b => b.name));

            // Process panels
            foreach (var panel in panels)
            {
                // Filter to only include blendshapes that are allowed and available
                var validBlendshapeNames = panel.blendshapeNames
                    .Where(name => availableBlendshapeNames.Contains(name))
                    .ToList();
                
                if (validBlendshapeNames.Count == 0)
                    continue;
                
                // Get appropriate prefab for this panel size
                var prefab = GetPrefabForSliderCount(validBlendshapeNames.Count);
                if (prefab == null)
                {
                    Debug.LogWarning($"DynamicBlendshapeSliderController: No prefab for {validBlendshapeNames.Count}-slider panel '{panel.panelTitle?.GetLocalizedString() ?? "UnnamedPanel"}'" );
                    continue;
                }
                
                // Instantiate the prefab
                var instance = Instantiate(prefab, sliderContainer);
                instance.name = panel.panelTitle?.GetLocalizedString() ?? "UnnamedPanel";
                _instantiatedPrefabs.Add(instance);
                
                // Set title on the panel component
                var panelComponent = instance.GetComponent<BlendshapeSliderPanel>();
                if (panelComponent != null)
                {
                    // If panel has a localized title, use it
                    if (panel.panelTitle != null && !panel.panelTitle.IsEmpty)
                    {
                        panelComponent.SetLocalizedTitle(panel.panelTitle);
                    }
                    // Otherwise, use the blendshape name(s) as plain text title
                    else if (validBlendshapeNames.Count > 0)
                    {
                        // For multiple blendshapes, use the first one as the title
                        panelComponent.SetPlainTitle(validBlendshapeNames[0]);
                    }
                }
                
                // Configure sliders in the prefab
                var sliders = instance.GetComponentsInChildren<SplineMeshBlendshapeSlider>(true);
                
                for (int i = 0; i < Mathf.Min(sliders.Length, validBlendshapeNames.Count); i++)
                {
                    var slider = sliders[i];
                    var blendshapeName = validBlendshapeNames[i];
                    
                    ConfigureSlider(slider, blendshapeName, blendshapeInfoLookup);
                }
            }
            
            // Note: Only blendshapes explicitly defined in panels are shown.
            // Ungrouped blendshapes are not automatically displayed.
        }
        
        /// <summary>
        /// Configures a slider with the given blendshape name and subscribes to events
        /// </summary>
        private void ConfigureSlider(SplineMeshBlendshapeSlider slider, string blendshapeName, Dictionary<string, SplineMeshProfile.BlendshapeInfo> blendshapeInfoLookup)
        {
            // Get default value from blendshape info
            float defaultValue = 0f;
            if (blendshapeInfoLookup.TryGetValue(blendshapeName, out var blendshapeInfo))
            {
                defaultValue = blendshapeInfo.defaultValue;
            }
            
            // Initialize slider
            slider.Initialize(blendshapeName, defaultValue);
            
            // Subscribe to value change events
            slider.OnSliderValueChangedNotify += HandleSliderValueChanged;
            slider.OnSliderPointerUpNotify += HandleSliderPointerUp;
            
            // Subscribe to toggle events for slider selection (VR controller support)
            // Store delegates for proper cleanup later
            var toggleController = slider.ToggleController;
            if (toggleController != null)
            {
                Action selectedDelegate = () => AddSliderToSelection(slider);
                Action deselectedDelegate = () => RemoveSliderFromSelection(slider);
                
                toggleController.OnSelected += selectedDelegate;
                toggleController.OnDeselected += deselectedDelegate;
                
                _sliderSelectedDelegates[slider] = selectedDelegate;
                _sliderDeselectedDelegates[slider] = deselectedDelegate;
            }
            
            Action disableDelegate = () => RemoveSliderFromSelection(slider);
            slider.OnDisableNotified += disableDelegate;
            _sliderDisableDelegates[slider] = disableDelegate;
            
            _activeSliders[blendshapeName] = slider;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleSliderValueChanged(string blendshapeName, float value)
        {
            if (_updateService != null && _currentSectionIndices.Count > 0)
            {
                float previousValue = _updateService.GetBlendshapeValue(_currentSectionIndices[0], blendshapeName);
                _updateService.UpdateBlendshapeMultiple(_currentSectionIndices, blendshapeName, value);
                float actualValue = _updateService.GetBlendshapeValue(_currentSectionIndices[0], blendshapeName);

                if (!Mathf.Approximately(actualValue, value))
                    SetSliderValueSilent(blendshapeName, actualValue);

                if (!Mathf.Approximately(actualValue, previousValue))
                    OnSliderValueChanged?.Invoke(blendshapeName, actualValue);
            }
        }
        
        private void HandleSliderPointerUp(string blendshapeName, float value, float previousValue)
        {
            OnSliderPointerUp?.Invoke(blendshapeName, value, previousValue);
        }

        private void HandleBlendshapeRevalidated(int sectionIndex, string blendshapeName, float correctedValue)
        {
            if (_currentSectionIndices.Contains(sectionIndex))
                SetSliderValueSilent(blendshapeName, correctedValue);
        }
        
        #endregion
    }
}
