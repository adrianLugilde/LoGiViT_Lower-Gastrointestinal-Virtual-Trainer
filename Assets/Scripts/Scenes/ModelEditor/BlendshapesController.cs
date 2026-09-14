using CustomUI;
using LargeIntestine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModelEditor
{
    /// <summary>
    /// Refactored controller for managing blendshapes in the model editor.
    /// Uses data-driven approach with BlendshapeUpdateService instead of hardcoded enums.
    /// </summary>
    [Serializable]
    public class BlendshapesController : MonoBehaviour, IBlendshapesController, ITrackpadSliderTarget
    {
        #region Dependencies
        
        [Header("Resources")]
        [SerializeField] private DynamicBlendshapeSliderController _dynamicSliderController;
        
        private Action<bool> _onModelChangedCallback;
        private BlendshapeUpdateService _blendshapeUpdateService;
        
        #endregion
        
        #region State
        
        private List<IModelSectionInteractable> _selectedSections = new List<IModelSectionInteractable>();
        private List<BlendshapeRecord> _blendshapeRecords = new List<BlendshapeRecord>();
        private int _blendshapeRecordIdx = -1;
        private bool _changesPending = false;
        
        #endregion
        
        #region Events
        
        public event Action OnSectionSelectionChanged;
        public event Action OnSectionSelectionCleared;
        
        #endregion
        
        #region Properties
        
        public bool HasSelection => _selectedSections.Count > 0;
        public IReadOnlyList<int> SelectedSectionIndices => _selectedSections.Select(s => s.SectionIndex).ToList();
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initializes the blendshapes controller with required dependencies.
        /// </summary>
        /// <param name="modelGenerator">The model generator providing section data and configuration</param>
        /// <param name="onModelChangedCallback">Callback to notify when model changes occur</param>
        public void Initialize(
            ISplineModelGenerator modelGenerator,
            Action<bool> onModelChangedCallback)
        {
            _onModelChangedCallback = onModelChangedCallback;
            
            // Initialize the blendshape update service with interfaces
            _blendshapeUpdateService = new BlendshapeUpdateService(
                modelGenerator,
                modelGenerator.GenerationConfiguration
            );
            
            // Initialize the dynamic slider controller
            if (_dynamicSliderController != null)
            {
                _dynamicSliderController.Initialize(_blendshapeUpdateService);
                _dynamicSliderController.OnSliderValueChanged += HandleSliderValueChanged;
                //_dynamicSliderController.OnSliderPointerUp += HandleSliderPointerUp;
            }
        }
        
        #region Selection Management
        
        /// <summary>
        /// Adds a section to the selection
        /// </summary>
        public void AddSectionToSelection(IModelSectionInteractable section)
        {
            // Check if already selected
            if (_selectedSections.Contains(section))
                return;
            
            _selectedSections.Add(section);
            
            Debug.Log($"Added section {section.SectionIndex} to selection. Total: {_selectedSections.Count}");
            
            // Update colliders for sections that need it
            foreach (var s in _selectedSections.Where(s => s.ColliderNeedsUpdate))
            {
                s.UpdateCollider();
            }
            
            RefreshSliders();
            OnSectionSelectionChanged?.Invoke();
        }
        
        /// <summary>
        /// Removes a section from the selection
        /// </summary>
        public void RemoveSectionFromSelection(IModelSectionInteractable section)
        {
            _selectedSections.Remove(section);
            
            Debug.Log($"Removed section {section.SectionIndex} from selection. Total: {_selectedSections.Count}");
            
            if (_selectedSections.Count == 0)
            {
                ClearSliders();
                OnSectionSelectionCleared?.Invoke();
            }
            else
            {
                RefreshSliders();
                OnSectionSelectionChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Clears all selected sections
        /// </summary>
        public void ClearSelection()
        {
            _selectedSections.Clear();
            ClearSliders();
            OnSectionSelectionCleared?.Invoke();
        }
        
        #endregion
        
        #endregion
        
        #region Slider Management
        
        /// <summary>
        /// Refreshes the sliders based on current selection
        /// </summary>
        private void RefreshSliders()
        {
            if (_dynamicSliderController == null || _selectedSections.Count == 0)
            {
                ClearSliders();
                return;
            }
            
            _dynamicSliderController.GenerateSlidersForSections(SelectedSectionIndices);
        }
        
        /// <summary>
        /// Clears all sliders
        /// </summary>
        private void ClearSliders()
        {
            if (_dynamicSliderController != null)
            {
                _dynamicSliderController.ClearSliders();
            }
        }
        
        /// <summary>
        /// Updates selected sliders by a delta value (for VR controller input)
        /// </summary>
        public void UpdateSelectedSlidersByDelta(float delta)
        {
            _dynamicSliderController?.UpdateSelectedSlidersByDelta(delta);
        }

        // ITrackpadSliderTarget — delegates to the existing named methods above
        void ITrackpadSliderTarget.UpdateSelectedSliderByDelta(float delta) => UpdateSelectedSlidersByDelta(delta);
        void ITrackpadSliderTarget.StoreSelectedSliderPreviousValue() => StoreSelectedBlendshapeSlidersPreviousValues();
        void ITrackpadSliderTarget.OnTrackpadReleased() => ManageSlidersValueChange();

        #endregion
        
        #region Event Handlers
        
        private void HandleSliderValueChanged(string blendshapeName, float value)
        {
            _changesPending = true;
            
            // Mark colliders as needing update for width-related blendshapes
            // (This could be made data-driven too via BlendshapeInfo metadata)
            foreach (var section in _selectedSections)
            {
                section.ColliderNeedsUpdate = true;
            }
            
            // Notify that model has unsaved changes
            _onModelChangedCallback?.Invoke(true);
        }

        public void ManageSlidersValueChange()
        {
            if (_changesPending)
            {
                CreateBlendshapeRecord();
                _changesPending = false;
            }
        }
        
        #endregion
        
        #region Undo/Redo
        
        private void CreateBlendshapeRecord()
        {
            Debug.Log("Creating blendshape record for undo/redo");
            if (_blendshapeRecords.Count > _blendshapeRecordIdx)
            {
                _blendshapeRecords = _blendshapeRecords.Take(_blendshapeRecordIdx).ToList();
            }
            
            // Capture current state
            var snapshots = new List<BlendshapeSnapshot>();
            foreach (var slider in _dynamicSliderController.ActiveSliders.Values)
            {
                snapshots.Add(new BlendshapeSnapshot
                {
                    BlendshapeName = slider.BlendshapeName,
                    Value = slider.GetPreviousValue()
                });
            }
            
            var record = new BlendshapeRecord
            {
                SectionIndices = SelectedSectionIndices.ToList(),
                Snapshots = snapshots
            };
            
            _blendshapeRecords.Add(record);
            _blendshapeRecordIdx = _blendshapeRecords.Count;
        }
        
        private void LoadRecord(int recordIndex)
        {
            if (recordIndex < 0 || recordIndex >= _blendshapeRecords.Count)
                return;
            
            var record = _blendshapeRecords[recordIndex];
            
            // Apply the snapshot values
            foreach (var snapshot in record.Snapshots)
            {
                foreach (var sectionIndex in record.SectionIndices)
                {
                    _blendshapeUpdateService.UpdateBlendshape(sectionIndex, snapshot.BlendshapeName, snapshot.Value);
                }
                
                _dynamicSliderController.SetSliderValueSilent(snapshot.BlendshapeName, snapshot.Value);
            }
        }
        
        public void Undo()
        {
            if (_blendshapeRecordIdx - 1 >= 0)
            {
                _blendshapeRecordIdx--;
                LoadRecord(_blendshapeRecordIdx);
            }
        }
        
        public void Redo()
        {
            if (_blendshapeRecordIdx < _blendshapeRecords.Count)
            {
                LoadRecord(_blendshapeRecordIdx);
                _blendshapeRecordIdx++;
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Gets the mesh profile for a selected section
        /// </summary>
        public SplineMeshProfile GetProfileForSection(int sectionIndex)
        {
            return _blendshapeUpdateService?.GetProfileForSection(sectionIndex);
        }
        
        /// <summary>
        /// Gets all blendshape values for a section
        /// </summary>
        public Dictionary<string, float> GetBlendshapeValuesForSection(int sectionIndex)
        {
            return _blendshapeUpdateService?.GetAllBlendshapeValues(sectionIndex) 
                   ?? new Dictionary<string, float>();
        }
        
        /// <summary>
        /// Stores previous values for selected sliders (for undo support with VR controller)
        /// </summary>
        public void StoreSelectedBlendshapeSlidersPreviousValues()
        {
            _dynamicSliderController?.StoreSelectedSlidersPreviousValues();
        }
        
        #endregion
        
        private void OnDestroy()
        {
            if (_dynamicSliderController != null)
            {
                _dynamicSliderController.OnSliderValueChanged -= HandleSliderValueChanged;
                //_dynamicSliderController.OnSliderPointerUp -= HandleSliderPointerUp;
            }
        }
    }
    
    /// <summary>
    /// Snapshot of a single blendshape value
    /// </summary>
    [Serializable]
    public struct BlendshapeSnapshot
    {
        public string BlendshapeName;
        public float Value;
    }
    
    /// <summary>
    /// Record of blendshape changes for undo/redo
    /// </summary>
    [Serializable]
    public class BlendshapeRecord
    {
        public List<int> SectionIndices;
        public List<BlendshapeSnapshot> Snapshots;
    }
}
