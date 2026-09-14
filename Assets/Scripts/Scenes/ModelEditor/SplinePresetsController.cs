using LargeIntestine;
using SplineMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModelEditor
{
    /// <summary>
    /// Manages spline preset records for undo/redo operations and preset previewing.
    /// </summary>
    /// <remarks>
    /// This controller handles:
    /// - Storing and restoring spline node states for preview operations
    /// - Managing undo/redo history for spline preset changes
    /// - Applying spline presets to the model
    /// </remarks>
    public class SplinePresetsController : MonoBehaviour, ISplinePresetsController
    {
        #region Private Fields

        private List<SplineNode> _tractEditionSplineNodesBackup;
        private List<SplinePresetRecord> _splinePresetRecords;
        private int _splinePresetRecordIdx;

        private ISplineModelGenerator _modelGenerator;
        private IModelEditorStateProvider _stateProvider;
        private bool _isInitialized;

        #endregion

        #region Events

        /// <summary>Fired when a record is loaded (undo/redo operation).</summary>
        public event Action OnRecordLoad;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the controller with required dependencies.
        /// </summary>
        /// <param name="stateProvider">Provider for editor state (manager acts as mediator).</param>
        /// <param name="modelGenerator">The spline model generator.</param>
        public void Initialize(IModelEditorStateProvider stateProvider, ISplineModelGenerator modelGenerator)
        {
            _stateProvider = stateProvider;
            _modelGenerator = modelGenerator;
            _tractEditionSplineNodesBackup = new List<SplineNode>();
            _splinePresetRecords = new List<SplinePresetRecord>();
            _splinePresetRecordIdx = 0;
            _isInitialized = true;
        }

        #endregion

        #region Preview Operations

        /// <summary>
        /// Previews a spline preset by applying it to the model.
        /// </summary>
        /// <param name="splinePreset">The preset to preview.</param>
        public void PreviewSplinePreset(SplinePreset splinePreset)
        {
            _modelGenerator.ApplySplinePreset(splinePreset);
            _modelGenerator.UpdateUpVectors();
        }

        /// <summary>
        /// Restores spline nodes to the backed-up state.
        /// </summary>
        public void RestoreSplineNodes()
        {
            _modelGenerator.ReplaceSplineNodesData(_tractEditionSplineNodesBackup);
            _modelGenerator.UpdateUpVectors();
        }

        /// <summary>
        /// Applies a preset to the model, updates up vectors, stores nodes, and creates a record.
        /// </summary>
        public void ApplySplinePreset(SplinePreset splinePreset)
        {
            EnsureBaseRecordExists();
            _modelGenerator.ApplySplinePreset(splinePreset);
            _modelGenerator.UpdateUpVectors();
            StoreCurrentSplineNodes();
            CreateSplinePresetRecord();
        }

        /// <summary>
        /// Stores the current spline nodes for later restoration.
        /// </summary>
        public void StoreCurrentSplineNodes()
        {
            _tractEditionSplineNodesBackup.Clear();
            foreach (var splineNode in _modelGenerator.Spline.nodes)
            {
                _tractEditionSplineNodesBackup.Add(new SplineNode(splineNode));
            }
        }

        #endregion

        #region Record Management

        /// <summary>
        /// Represents a snapshot of spline nodes for undo/redo operations.
        /// </summary>
        [Serializable]
        public class SplinePresetRecord
        {
            public List<SplineNode> splineNodes;

            public SplinePresetRecord(List<SplineNode> sourceSplineNodes)
            {
                this.splineNodes = new List<SplineNode>();
                foreach (var splineNode in sourceSplineNodes)
                {
                    splineNodes.Add(new SplineNode(splineNode));
                }
            }
        }

        /// <summary>
        /// Creates a new record of the current spline state.
        /// Truncates any redo history if in the middle of the record stack.
        /// </summary>
        public void CreateSplinePresetRecord()
        {
            if (_splinePresetRecords.Count > _splinePresetRecordIdx + 1)
            {
                _splinePresetRecords = _splinePresetRecords.Take(_splinePresetRecordIdx + 1).ToList();
            }
            _splinePresetRecords.Add(new SplinePresetRecord(_modelGenerator.Spline.nodes));
            _splinePresetRecordIdx = _splinePresetRecords.Count - 1;
        }

        /// <summary>
        /// Creates the initial base record from the backed-up spline nodes.
        /// </summary>
        private void CreateBaseRecord()
        {
            _splinePresetRecords.Add(new SplinePresetRecord(_tractEditionSplineNodesBackup));
            _splinePresetRecordIdx = _splinePresetRecords.Count - 1;
        }

        /// <summary>
        /// Loads a record at the specified index, applying it to the model.
        /// </summary>
        /// <param name="targetRecordIdx">The index of the record to load.</param>
        public void LoadRecord(int targetRecordIdx)
        {
            var targetRecord = _splinePresetRecords[targetRecordIdx];
            _modelGenerator.ReplaceSplineNodesData(targetRecord.splineNodes);
            StoreCurrentSplineNodes();
            OnRecordLoad?.Invoke();
            
            // Re-apply preview if one is active (query via manager as mediator)
            if (_stateProvider.IsPresetPreviewActive)
            {
                var currentPreset = _stateProvider.GetSelectedPreset();
                if (currentPreset != null)
                {
                    PreviewSplinePreset(currentPreset);
                    return; // PreviewSplinePreset already called UpdateUpVectors
                }
            }

            _modelGenerator.UpdateUpVectors();
        }

        /// <summary>
        /// Ensures a base record exists before making changes.
        /// </summary>
        public void EnsureBaseRecordExists()
        {
            if (_splinePresetRecords.Count == 0) CreateBaseRecord();
        }

        #endregion

        #region Undo/Redo

        /// <summary>
        /// Undoes the last spline preset change.
        /// </summary>
        public void Undo()
        {
            if (_splinePresetRecordIdx - 1 >= 0) LoadRecord(--_splinePresetRecordIdx);
        }

        /// <summary>
        /// Redoes the last undone spline preset change.
        /// </summary>
        public void Redo()
        {
            if (_splinePresetRecordIdx + 1 <= _splinePresetRecords.Count - 1) LoadRecord(++_splinePresetRecordIdx);
        }

        #endregion
    }
}