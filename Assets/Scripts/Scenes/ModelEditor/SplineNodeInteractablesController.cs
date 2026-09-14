// ============================================================================
// SplineNodeInteractablesController.cs
// 
// Manages spline node interactables for the model editor.
// Handles XR-based node manipulation with undo/redo support via records.
// 
// Key Features:
//   - Creates and manages XRSplineNodeInteractable instances
//   - Maintains a record history for undo/redo functionality
//   - Supports solidary node movement (linked adjacent nodes)
//   - Integrates with preset edition mode
// 
// Usage:
//   1. Call Initialize() with the model generator and selection controller
//   2. Call AddSplineNodesInteractables() to create node handles
//   3. Use Undo()/Redo() for history navigation
// ============================================================================

using CustomUI;
using SplineMesh;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace ModelEditor
{
    /// <summary>
    /// Controller for managing spline node interactables during model editing.
    /// Provides undo/redo functionality and coordinates node manipulation.
    /// </summary>
    /// <remarks>
    /// <para>Implements <see cref="IEditorActionController"/> for integration with the editor system.</para>
    /// <para>Key responsibilities:</para>
    /// <list type="bullet">
    ///   <item>Creating and positioning node handles</item>
    ///   <item>Managing record history for undo/redo</item>
    ///   <item>Handling preset edition mode transitions</item>
    ///   <item>Coordinating solidary node movement</item>
    /// </list>
    /// </remarks>
    [DefaultExecutionOrder(5)]
    public class SplineNodesInteractablesController : MonoBehaviour, ISplineNodesInteractablesController
    {
        #region Serialized Fields

        [Header("Resources")]
        [Tooltip("Prefab for XR spline node interactables")]
        [SerializeField] private GameObject _splineNodeInteractablePrefab;

        [Tooltip("Maximum percentage of distance variation between adjacent nodes")]
        [SerializeField] private float _splineNodesDistVarPercent = 0.1f;

        [Tooltip("Displacement factor for solidary node movement")]
        [SerializeField] private float _splineNodesSolidaryDisplacement = 0.5f;

        #endregion

        #region Record Management

        [Header("Records")]
        [Tooltip("Current record history for undo/redo")]
        public List<SGENRecord> SgenRecords;

        [Tooltip("Backup of records during preset edition")]
        public List<SGENRecord> SgenRecordsBackUp;

        [Tooltip("Current index in the record history")]
        public int SgenRecordIdx;

        #endregion

        #region Private Fields

        private ISplineModelGenerator _modelGenerator;
        private SelectionController _selectionController;
        private int _sgenRecordIdxBackUp;
        private List<XRSplineNodeInteractable> _splineNodeInteractables;
        private GameObject _editionNodesHolder;
        private int _activeNodeGrabCount;

        #endregion

        #region Events

        /// <summary>
        /// Fired when any node's transform changes (position or rotation).
        /// </summary>
        public event Action OnNodeTransformChanged;

        /// <summary>
        /// Fired when a spline node interactable is released after interaction.
        /// </summary>
        public event Action OnSplineNodeInteractableSelectExitNotify;

        /// <summary>
        /// Fired when the first node grab begins (transitions from zero to one active grab).
        /// </summary>
        public event Action OnAnyNodeGrabStart;

        /// <summary>
        /// Fired when the last active node grab ends (transitions from one to zero active grabs).
        /// </summary>
        public event Action OnAnyNodeGrabEnd;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Initialize record lists
            SgenRecords = new List<SGENRecord>();
            SgenRecordsBackUp = new List<SGENRecord>();
            SgenRecordIdx = _sgenRecordIdxBackUp = 0;
            _splineNodeInteractables = new List<XRSplineNodeInteractable>();
        }
        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the controller with required dependencies.
        /// </summary>
        /// <param name="modelGenerator">The model generator providing spline data</param>
        /// <param name="selectionController">The selection controller for handling user selection</param>
        public void Initialize(ISplineModelGenerator modelGenerator, SelectionController selectionController)
        {
            _modelGenerator = modelGenerator;
            _selectionController = selectionController;
            // Create container for edition node GameObjects
            _editionNodesHolder = CommonUtils.Create("EditionNodesHolder", _modelGenerator.RootGameObject);
            StartCoroutine(SetBaseRecord());
        }

        /// <summary>
        /// Waits for the model to be generated before creating the base record.
        /// </summary>
        private IEnumerator SetBaseRecord()
        {
            yield return new WaitUntil(() => _modelGenerator.IsSegmentedModelGenerated);
            CreateBaseSGENRecord();
        }

        #endregion

        #region Node Interactable Management

        /// <summary>
        /// Creates XR interactables for all spline nodes except the rectum (first node).
        /// </summary>
        public void AddSplineNodesInteractables()
        {
            // Rectum node (index 0) is unmovable, start at index 1
            _splineNodeInteractables.Clear();
            var splineNodes = _modelGenerator.Spline.nodes;

            for (int i = 1; i < splineNodes.Count; i++)
            {
                FindOrCreateSplineNodeInteractable("XRSplineNodeInteractable " + i, splineNodes[i], i - 1);
            }

            // Set up links between adjacent nodes for solidary movement
            for (int i = 0; i < _splineNodeInteractables.Count; i++)
            {
                var splineNodeInteractable = _splineNodeInteractables[i];
                var previousNode = i != 0 ? _splineNodeInteractables[i - 1] : null;
                var nextNode = i == _splineNodeInteractables.Count - 1 ? null : _splineNodeInteractables[i + 1];

                splineNodeInteractable.SetNodeLinks(previousNode, nextNode);
                splineNodeInteractable.idxInSpline = i + 1;
            }
        }

        /// <summary>
        /// Finds an existing node interactable or creates a new one.
        /// </summary>
        /// <param name="name">Name for the interactable GameObject</param>
        /// <param name="splineNode">The spline node to associate with</param>
        /// <param name="indexInSpline">Index of the node in the spline</param>
        private void FindOrCreateSplineNodeInteractable(string name, SplineNode splineNode, int indexInSpline)
        {
            var childTransform = _editionNodesHolder.transform.Find(name);
            GameObject res;

            if (childTransform == null)
            {
                // Create new interactable from prefab
                res = CommonUtils.Instantiate(_splineNodeInteractablePrefab, _editionNodesHolder.transform, name, true);
            }
            else
            {
                // Reuse existing interactable
                res = childTransform.gameObject;
            }

            // Position at the spline node's world position
            res.transform.position = _modelGenerator.Spline.transform.TransformPoint(splineNode.Position);

            // Initialize the XR interactable component
            var xrSplineNodeInteractable = res.GetComponent<XRSplineNodeInteractable>();
            xrSplineNodeInteractable.Initialize(_selectionController, splineNode, indexInSpline, _modelGenerator.Spline.transform);

            // Only register events for newly created interactables
            if (childTransform == null)
            {
                xrSplineNodeInteractable.OnNodeSelectExitNotify += OnSplineNodeInteractableSelectExited;
                xrSplineNodeInteractable.OnNodeTransformChanged += OnNodeTransformChanged;
                xrSplineNodeInteractable.xrGrabInteractable.selectEntered.AddListener(HandleNodeGrabEntered);
                xrSplineNodeInteractable.xrGrabInteractable.selectExited.AddListener(HandleNodeGrabExited);
            }

            // Configure movement parameters
            xrSplineNodeInteractable.SplineNodesDistVarPercent = _splineNodesDistVarPercent;
            xrSplineNodeInteractable.SplineNodesSolidaryDisplacement = _splineNodesSolidaryDisplacement;

            _splineNodeInteractables.Add(xrSplineNodeInteractable);
        }

        #region Interaction Control

        /// <summary>
        /// Enables interactability on all spline nodes (colliders / XR component).
        /// Does not affect node visibility.
        /// </summary>
        public void EnableNodeInteraction()
        {
            foreach (var interactable in _splineNodeInteractables)
                interactable.EnableInteractable();
        }

        /// <summary>
        /// Disables interactability on all spline nodes (colliders / XR component).
        /// Does not affect node visibility.
        /// </summary>
        public void DisableNodeInteraction()
        {
            foreach (var interactable in _splineNodeInteractables)
                interactable.DisableInteractable();
        }

        /// <summary>
        /// Makes the node holder GameObject active, showing all nodes.
        /// </summary>
        public void ShowNodes()
        {
            SetEditionNodesHolderActive(true);
        }

        /// <summary>
        /// Makes the node holder GameObject inactive, hiding all nodes.
        /// </summary>
        public void HideNodes()
        {
            SetEditionNodesHolderActive(false);
        }

        #endregion

        /// <summary>
        /// Shows or hides all node interactables, enabling or preventing user interaction.
        /// </summary>
        /// <param name="isActive">Whether to show the nodes</param>
        private void SetEditionNodesHolderActive(bool isActive)
        {
            _editionNodesHolder.SetActive(isActive);
        }

        /// <summary>
        /// Called when a node interactable is released after being grabbed.
        /// Creates a new record and updates the spline.
        /// </summary>
        private void OnSplineNodeInteractableSelectExited(XRSplineNodeInteractable obj)
        {
            Debug.Log(obj.gameObject.name + " select exited");
            CreateSGENRecord();
            _modelGenerator.UpdateUpVectors();
            OnSplineNodeInteractableSelectExitNotify?.Invoke();
        }

        private void HandleNodeGrabEntered(SelectEnterEventArgs args)
        {
            _activeNodeGrabCount++;
            if (_activeNodeGrabCount == 1)
                OnAnyNodeGrabStart?.Invoke();
        }

        private void HandleNodeGrabExited(SelectExitEventArgs args)
        {
            _activeNodeGrabCount = Mathf.Max(0, _activeNodeGrabCount - 1);
            if (_activeNodeGrabCount == 0)
                OnAnyNodeGrabEnd?.Invoke();
        }

        #endregion

        #region Solidary Movement

        /// <summary>
        /// Enables solidary movement mode where adjacent nodes move together.
        /// </summary>
        public void EnableSolidaryNodeMovement()
        {
            for (int i = 0; i < _splineNodeInteractables.Count; i++)
            {
                _splineNodeInteractables[i].SolidaryHandleTargets = true;
            }
        }

        /// <summary>
        /// Disables solidary movement mode.
        /// </summary>
        public void DisableSolidaryNodeMovement()
        {
            for (int i = 0; i < _splineNodeInteractables.Count; i++)
            {
                _splineNodeInteractables[i].SolidaryHandleTargets = false;
            }
        }

        #endregion

        #region Record Management (Undo/Redo)

        /// <summary>
        /// Creates a new record of the current spline state.
        /// Truncates any redo history beyond the current position.
        /// </summary>
        public void CreateSGENRecord()
        {
            // Remove any records after current position (truncate redo history)
            if (SgenRecords.Count > SgenRecordIdx + 1)
            {
                SgenRecords = SgenRecords.Take(SgenRecordIdx + 1).ToList();
            }
            SgenRecords.Add(new SGENRecord(_modelGenerator.Spline.nodes));
            SgenRecordIdx = SgenRecords.Count - 1;
        }

        /// <summary>
        /// Creates the initial base record when the controller is initialized.
        /// </summary>
        public void CreateBaseSGENRecord()
        {
            SgenRecords.Add(new SGENRecord(_modelGenerator.Spline.nodes));
            SgenRecordIdx = SgenRecords.Count - 1;
        }

        /// <summary>
        /// Loads a specific record by index, restoring the spline to that state.
        /// </summary>
        /// <param name="targetRecordIdx">Index of the record to load</param>
        public void LoadRecord(int targetRecordIdx)
        {
            var targetRecord = SgenRecords[targetRecordIdx];

            // Restore spline node data
            _modelGenerator.ReplaceSplineNodesData(targetRecord.splineNodes);

            // Recreate node interactables at new positions
            AddSplineNodesInteractables();

            // Recalculate the mesh to match new spline
            _modelGenerator.ComputeMeshRecalculations();
        }

        /// <summary>
        /// Undoes the last change by loading the previous record.
        /// </summary>
        public void Undo()
        {
            if (SgenRecordIdx - 1 >= 0) LoadRecord(--SgenRecordIdx);
        }

        /// <summary>
        /// Redoes the last undone change by loading the next record.
        /// </summary>
        public void Redo()
        {
            if (SgenRecordIdx + 1 <= SgenRecords.Count - 1) LoadRecord(++SgenRecordIdx);
        }

        #endregion

        #region Preset Edition Mode

        /// <summary>
        /// Enters preset edition mode, backing up current records.
        /// Creates a fresh record history for preset editing.
        /// </summary>
        public void EnableRecordsForPresetEdition()
        {
            // Backup current records
            SgenRecordsBackUp.Clear();
            SgenRecordsBackUp.AddRange(SgenRecords);
            _sgenRecordIdxBackUp = SgenRecordIdx;

            // Start fresh for preset edition
            SgenRecords.Clear();
            SgenRecordIdx = 0;
            CreateBaseSGENRecord();
        }

        /// <summary>
        /// Exits preset edition mode, restoring original records.
        /// </summary>
        public void DisableRecordsForPresetEdition()
        {
            // Restore backed up records
            SgenRecords.Clear();
            SgenRecords.AddRange(SgenRecordsBackUp);
            SgenRecordIdx = _sgenRecordIdxBackUp;
            _sgenRecordIdxBackUp = 0;
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Record of a spline state for undo/redo functionality.
        /// </summary>
        [Serializable]
        public class SGENRecord
        {
            /// <summary>
            /// Deep copy of spline nodes at the time of recording.
            /// </summary>
            public List<SplineNode> splineNodes;

            /// <summary>
            /// Creates a new record with a deep copy of the given spline nodes.
            /// </summary>
            /// <param name="sourceSplineNodes">Source nodes to copy</param>
            public SGENRecord(List<SplineNode> sourceSplineNodes)
            {
                splineNodes = new List<SplineNode>();
                foreach (var splineNode in sourceSplineNodes)
                {
                    splineNodes.Add(new SplineNode(splineNode));
                }
            }
        }

        #endregion
    }
}