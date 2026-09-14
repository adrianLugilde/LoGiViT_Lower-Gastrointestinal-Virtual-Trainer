// ============================================================================
// ModelEditorManager.cs
//
// Central manager for the Model Editor scene in LoGIViT-HDRP.
// Coordinates all subsystems for editing large intestine models including:
//   - Tract editing (spline node manipulation)
//   - Preset management (save/load spline configurations)
//   - Details editing (blendshape adjustments)
//   - Disease placement and editing
//   - Internal preview navigation
//
// Architecture:
//   - Implements ISecondaryRoomManager for room transition support
//   - Implements IModelEditorStateProvider for state queries (InputController)
//   - Uses Dependency Injection pattern with interface-based dependencies
//   - Uses event-driven communication with child controllers
//   - Manages 5 edition modes through a state machine pattern
//   - Acts as Mediator between edition mode controllers and feature controllers
//
// Dependency Flow (No Circular Dependencies):
//   Manager → Edition Mode Controllers → Feature Controllers (via interfaces)
//                                     → Model Generator (via ISplineModelGenerator)
//
// Core Controllers (MonoBehaviour):
//   - UIController: UI facade for all user interface operations
//   - InputController: XR input handling and guided camera control
//   - SplinePresetsController (ISplinePresetsController): Preset management with undo/redo
//   - SplineNodeInteractablesController (ISplineNodesInteractablesController): Node XR interactables
//   - BlendshapesController (IBlendshapesController): Blendshape manipulation
//   - LargeIntestineSectionsInteractablesController: Section selection interactables
//   - DiseasePlacementController: Disease mesh projection and placement
//   - SelectionController: Multi-selection management (from CustomUI)
//
// Edition Mode Controllers (Pure C# classes, created via DI):
//   - TractEditionModeController: Spline node editing mode
//   - PresetEditionModeController: Preset management mode
//   - DetailsEditionModeController: Blendshape editing mode
//   - DiseaseEditionModeController: Disease placement mode (also IDiseaseEditionStateProvider)
//   - PreviewEditionModeController: Internal camera navigation mode
//
// State Providers:
//   - IModelEditorStateProvider: Implemented by Manager for state queries
//   - IDiseaseEditionStateProvider: Implemented by DiseaseEditionModeController
//
// Execution Order: -200 (must execute before child controllers)
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using CustomUI;
using ImgSpc.Exporters;
using UnityEngine;
using MainRoom;
using TMPro;
using Messages;
using ModelEditor.EditionModes;

namespace ModelEditor
{
    /// <summary>
    /// Central manager for the Model Editor scene.
    /// Coordinates tract editing, preset management, details editing,
    /// disease placement, and internal preview functionality.
    /// </summary>
    /// <remarks>
    /// This class orchestrates multiple subsystems through event-based communication.
    /// It manages 5 edition modes: Tract, Presets, Details, Diseases, and Preview.
    /// Edition mode controllers are being extracted for better separation of concerns.
    /// </remarks>
    [DefaultExecutionOrder(-200)]//MUST BE EXECUTED BEFORE THE OTHER CONTROLLERS
    public class ModelEditorManager : MonoBehaviour, ISecondaryRoomManager, IModelEditorStateProvider
    {
        #region Serialized Fields

        [Header("Resources")]
        [SerializeField] private XRTeleportDestinationProvider _teleportDestinationProvider;
        [SerializeField] private GameObject modelGeneratorObject;
        [SerializeField] private Camera _modelPreviewCamera;
        [SerializeField] private DiseasePlacementController _diseasePlacementController;

        #endregion

        #region Private Fields - Dependencies

        private ISplineModelGenerator _modelGenerator;

        #endregion

        #region Public Properties

        [HideInInspector] public UIController UIController { get; private set; }
        [HideInInspector] public InputController InputController { get; private set; }

        #endregion

        #region Private Fields - Controllers

        private ApplicationManager _applicationManager;
        private IAppSettingsProvider _appSettings;
        private SplinePresetsController _splinePresetsController;
        private SplineNodesInteractablesController _splineNodesInteractableController;
        private LargeIntestineSectionsInteractablesController _liSectionsInteractablesController;
        private BlendshapesController _blendshapesController;
        private SelectionController _selectionController;
        private ModelRotationHandleController _modelRotationHandleController;
        private ModelNoiseSettingsController _modelNoiseSettingsController;

        #endregion

        #region Private Fields - Edition Mode Controllers

        private TractEditionModeController _tractModeController;
        private PresetEditionModeController _presetModeController;
        private DetailsEditionModeController _detailsModeController;
        private DiseaseEditionModeController _diseaseModeController;
        private PreviewEditionModeController _previewModeController;

        /// <summary>Dictionary mapping edition modes to their controllers.</summary>
        private Dictionary<EditionMode, IEditionModeController> _modeControllers;

        #endregion

        #region Public State Properties

        /// <summary>Whether spline node interactables are ready for interaction.</summary>
        public bool EditionNodesReady { get; private set; } = false;

        /// <summary>
        /// Available edition modes in the Model Editor.
        /// </summary>
        public enum EditionMode
        {
            /// <summary>Spline node editing for tract shaping.</summary>
            Tract = 0,
            /// <summary>Spline preset management and creation.</summary>
            Presets = 1,
            /// <summary>Section blendshape editing for shape refinement.</summary>
            Details = 2,
            /// <summary>Disease mesh placement and editing.</summary>
            Diseases = 3,
            /// <summary>Internal camera preview navigation.</summary>
            Preview = 4
        }

        /// <summary>Gets the current active edition mode.</summary>
        public EditionMode CurrentEditionMode { get; private set; }

        /// <summary>Whether the model has unsaved changes.</summary>
        [HideInInspector] public bool ModelChangesNotSaved = false;

        #endregion

        #region Private Fields - Runtime

        private List<Material[]> _tempMaterialsList;
        private bool _segmentedModelSelectionEnabled = false;
        private bool _isNBIEnabled = false;
        private ImgSpcExporter _meshExporter;
        private Model _currentModel => _applicationManager.GetCurrentModel();

        #endregion

        #region Initialization State

        /// <summary>Whether the Model Editor has completed initialization.</summary>
        public bool IsInitialized { get; private set; } = false;

        /// <summary>Fired when the user confirms exit from the Model Editor room.</summary>
        public event Action OnExitRoomRequested;

        #endregion

        #region NOT REVISED
        private bool weldedSelectionActive = false;
        [HideInInspector] public bool inPreviewCombinedModel = false;
        [HideInInspector] public bool inInsidePreview = false;
        [HideInInspector] public int currentSelectedSectionIdx = 0;
        #endregion

        #region IModelEditorStateProvider Implementation

        /// <inheritdoc />
        bool IModelEditorStateProvider.IsInitialized => IsInitialized;

        /// <inheritdoc />
        public bool IsInPreviewMode => CurrentEditionMode == EditionMode.Preview;

        /// <inheritdoc />
        public bool IsInDiseasesMode => CurrentEditionMode == EditionMode.Diseases;

        /// <inheritdoc />
        public bool IsInPreviewOrDiseasesMode => IsInPreviewMode || IsInDiseasesMode;

        /// <inheritdoc />
        public bool IsPresetPreviewActive => _presetModeController?.IsPreviewActive ?? false;

        /// <inheritdoc />
        public SplinePreset GetSelectedPreset() => UIController?.GetSelectedSplinePreset();

        /// <inheritdoc />
        int IModelEditorStateProvider.SplineNodeCount => _modelGenerator.SplineNodeCount;

        /// <inheritdoc />
        Quaternion IModelEditorStateProvider.GetSampleRotationAtRate(float rate)
        {
            return _modelGenerator.Spline.GetSample(rate).Rotation;
        }

        /// <inheritdoc />
        Vector3 IModelEditorStateProvider.GetSampleLocationAtRate(float rate)
        {
            return _modelGenerator.transform.TransformPoint(_modelGenerator.Spline.GetSample(rate).location);
        }

        #endregion


        /// <summary>
        /// Unity Awake lifecycle method.
        /// Establishes singleton instance, retrieves application manager,
        /// locates model generator, and sets up controller references.
        /// </summary>
        /// <remarks>
        /// Execution order: -200 (before child controllers initialize).
        /// Sets up the foundation for dependency injection into mode controllers.
        /// </remarks>
        private void Awake()
        {
            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null)
                throw new Exception("ApplicationManager not found.");

            if (modelGeneratorObject == null)
                throw new Exception("Model Generator GameObject not assigned.");

            _modelGenerator = modelGeneratorObject.GetComponent<ISplineModelGenerator>();
            if (_modelGenerator == null)
                throw new Exception("Model Generator must implement ISplineModelGenerator interface.");

            SetControllers();
            SubscribeEvents();
        }

        /// <summary>
        /// Locates all child controllers in the scene hierarchy using GetComponentInChildren.
        /// Must be called before ConfigureControllers().
        /// </summary>
        /// <remarks>
        /// Controllers are expected to be in the following hierarchy:
        /// - UIController: Direct or nested child
        /// - InputController: Direct or nested child
        /// - Feature controllers: Nested under model or UI objects
        /// - SelectionController: From CustomUI namespace
        /// </remarks>
        private void SetControllers()
        {
            UIController = GetComponentInChildren<UIController>();
            if (UIController == null)
                throw new Exception("UIController not found in children.");

            InputController = GetComponentInChildren<InputController>();
            if (InputController == null)
                throw new Exception("InputController not found in children.");

            _splinePresetsController = GetComponentInChildren<SplinePresetsController>();
            if (_splinePresetsController == null)
                throw new Exception("SplinePresetsController not found in children.");
            _splinePresetsController.Initialize(this, _modelGenerator);

            _blendshapesController = GetComponentInChildren<BlendshapesController>();
            if (_blendshapesController == null)
                throw new Exception("BlendshapesController not found in children.");
            // Note: Initialize() will be called in the Initialize() coroutine after configuration is loaded

            _splineNodesInteractableController = GetComponentInChildren<SplineNodesInteractablesController>();
            if (_splineNodesInteractableController == null)
                throw new Exception("SplineNodeInteractablesController not found in children.");

            _liSectionsInteractablesController = GetComponentInChildren<LargeIntestineSectionsInteractablesController>();
            if (_liSectionsInteractablesController == null)
                throw new Exception("LargeIntestineSectionInteractablesController not found in children.");


            _selectionController = GetComponentInChildren<CustomUI.SelectionController>();
            if (_selectionController == null)
                throw new Exception("SelectionController not found in children.");

            _modelRotationHandleController = GetComponentInChildren<ModelRotationHandleController>();
            if (_modelRotationHandleController == null)
                throw new Exception("ModelRotationHandleController not found in children.");

            _modelNoiseSettingsController = GetComponentInChildren<ModelNoiseSettingsController>(true);
            if (_modelNoiseSettingsController == null)
                throw new Exception("ModelNoiseSettingsController not found in children.");

            if (_diseasePlacementController == null)
                throw new Exception("DiseasePlacer not assigned in editor.");

            if (_modelPreviewCamera == null)
                throw new Exception("ModelPreviewCamera not assigned in editor.");
        }

        /// <summary>
        /// Initializes all feature controllers with their required dependencies.
        /// Uses Dependency Injection to provide interfaces to each controller.
        /// </summary>
        /// <remarks>
        /// Dependency Injection Pattern:
        /// - SplinePresetsController.Initialize(IModelEditorStateProvider, ISplineModelGenerator)
        /// - BlendshapesController.Initialize(ISplineModelGenerator, config, change callback)
        /// - InputController receives IModelEditorStateProvider reference
        /// 
        /// Note: LargeIntestineModelGenerator cast is safe as it's the only implementation.
        /// </remarks>
        private void ConfigureControllers()
        {
            _splineNodesInteractableController.Initialize(_modelGenerator, _selectionController);
            _modelRotationHandleController.Initialize(_splineNodesInteractableController);
            _modelNoiseSettingsController.Initialize(_modelGenerator, () => ModelChangesNotSaved = true);

            _liSectionsInteractablesController.Initialize(_modelGenerator, _selectionController);

            // Initialize BlendshapesController requires model generator configuration, so it must be done after LoadGene
            _blendshapesController.Initialize(
                _modelGenerator,
                (hasChanges) => ModelChangesNotSaved = hasChanges);

            // Initialize edition mode controllers
            InitializeEditionModeControllers();

            // Inject state provider into InputController (replaces legacy Func<> events)
            InputController.SetStateProvider(this);
        }

        /// <summary>
        /// Creates all edition mode controllers using constructor-based dependency injection.
        /// Mode controllers are pure C# classes (not MonoBehaviours) for better testability.
        /// </summary>
        /// <remarks>
        /// Each mode controller receives only the interfaces it needs:
        /// - TractEditionModeController: Node editing dependencies
        /// - PresetEditionModeController: Preset management dependencies
        /// - DetailsEditionModeController: Blendshape editing + disease state dependencies
        /// - DiseaseEditionModeController: Disease placement dependencies
        /// - PreviewEditionModeController: Camera navigation + disease state dependencies
        /// 
        /// The dictionary allows O(1) mode switching based on EditionMode enum.
        /// </remarks>
        private void InitializeEditionModeControllers()
        {
            // Create Tract mode controller
            _tractModeController = new TractEditionModeController(
                UIController,
                InputController,
                _splineNodesInteractableController,
                _modelGenerator,
                _selectionController,
                _modelNoiseSettingsController);

            // Create Preset mode controller
            _presetModeController = new PresetEditionModeController(
                UIController,
                InputController,
                _splinePresetsController,
                _splineNodesInteractableController,
                _modelGenerator,
                _selectionController,
                (code) => ManageMessageCode(code));

            // Create Disease mode controller FIRST - it's a state provider for others
            _diseaseModeController = new DiseaseEditionModeController(
                UIController,
                InputController,
                _diseasePlacementController,
                _modelGenerator,
                _liSectionsInteractablesController);

            // Create Details mode controller - receives disease state provider
            _detailsModeController = new DetailsEditionModeController(
                UIController,
                InputController,
                _blendshapesController,
                _liSectionsInteractablesController,
                _modelGenerator,
                _selectionController,
                _diseaseModeController);  // As IDiseaseEditionStateProvider

            // Create Preview mode controller - receives disease state provider
            _previewModeController = new PreviewEditionModeController(
                UIController,
                InputController,
                _diseaseModeController,  // As IDiseaseEditionStateProvider
                _modelGenerator,
                () => SetWeldedModelNBI(0f));

            // Build mode controller dictionary for easy lookup
            _modeControllers = new Dictionary<EditionMode, IEditionModeController>
            {
                { EditionMode.Tract, _tractModeController },
                { EditionMode.Presets, _presetModeController },
                { EditionMode.Details, _detailsModeController },
                { EditionMode.Diseases, _diseaseModeController },
                { EditionMode.Preview, _previewModeController }
            };
        }

        /// <summary>
        /// Subscribes to all events from UI, Input, Selection, and Feature controllers.
        /// Establishes event-driven communication between manager and subsystems.
        /// </summary>
        /// <remarks>
        /// Event Categories:
        /// - UIController: Mode toggles, common actions, preset actions, disease actions
        /// - InputController: State queries (legacy Func), locomotion, trackpad input
        /// - SelectionController: Add/remove/clear selection events
        /// - SplineNodeInteractablesController: Node transform changed
        /// - SplinePresetsController: Record load event
        /// - BlendshapesController: Section selection changed/cleared
        /// - DiseasePlacementController: Disease projected/unprojected
        /// 
        /// Legacy Func events: Deprecated state queries, replaced by IModelEditorStateProvider.
        /// Kept for backward compatibility during transition.
        /// </remarks>
        private void SubscribeEvents()
        {
            // UIController - Mode toggle events
            UIController.OnToggleGroupUpdate += BeforeEditionModeChangeChecks;
            UIController.OnSaveModelModalShowed += OnSaveModel;
            UIController.OnTractModeSelected += HandleTractModeSelected;
            UIController.OnTractModeDeselected += HandleTractModeDeselected;
            UIController.OnDiseasesModeSelected += HandleDiseasesModeSelected;
            UIController.OnDiseasesModeDeselected += HandleDiseasesModeDeselected;
            UIController.OnPreviewModeDeselected += HandlePreviewModeDeselected;

            // UIController - Common actions
            UIController.OnUndoAction += UndoAction;
            UIController.OnRedoAction += RedoAction;
            UIController.OnSaveModelAction += ConfirmSaveModel;
            UIController.OnSaveModelAsAction += ConfirmSaveModelAs;
            UIController.OnExitRoomAction += ValidateExitRoom;
            UIController.OnConfirmExitRoomAction += ExitTrainingRoom;
            UIController.OnHelpAction += Help;

            // UIController - Tract mode controls
            UIController.OnJoinedNodesSelected += EnableJoinedNodes;
            UIController.OnJoinedNodesDeselected += DisableJoinedNodes;
            UIController.OnMultipleSelectionSelected += EnableMultipleSelection;
            UIController.OnMultipleSelectionDeselected += DisableMultipleSelection;

            // UIController - Preset actions
            UIController.OnApplyPresetAction += ApplySplinePreset;
            UIController.OnCreatePresetAction += ConfigureEditorForNewPresetCreation;
            UIController.OnEditPresetAction += ConfigureEditorForExistingPresetEdition;
            UIController.OnDeletePresetAction += ConfirmDeleteSplinePreset;
            UIController.OnImportPresetAction += SplinePresetImport;
            UIController.OnExportPresetAction += SplinePresetExport;

            UIController.OnPresetEditionSaveAction += SaveSplinePreset;
            UIController.OnPresetEditionSaveAsAction += SaveSplinePresetAs;
            UIController.OnPresetEditionExitAction += ExitSplinePresetEditionOrCreation;

            // UIController - Disease actions
            UIController.OnPlaceDiseaseAction += PlaceDisease;
            UIController.OnEnlargeDiseaseAction += EnlargeDiseaseMesh;
            UIController.OnShrinkDiseaseAction += ShrinkDiseaseMesh;

            // UIController - Selection actions
            UIController.OnClearSelectionAction += ClearMultipleSelection;

            // UIController - Scroll area events
            UIController.OnSplinePresetRead += HandleSplinePresetRead;
            UIController.OnSplineFilterNoResults += HandleSplineFilterNoResults;
            UIController.OnSplinePresetScrollAreaElementSelected += OnSplinePresetSrollAreaElementSelect;

            UIController.OnDiseaseRead += HandleDiseaseRead;
            UIController.OnDiseaseFilterNoResults += HandleDiseaseFilterNoResults;
            UIController.OnDiseaseScrollAreaElementSelected += OnDiseaseScrollAreaElementSelect;

            InputController.LocomotionChanged += ManageLocomotionChange;
            InputController.OnRightHandTrackpadInput += UpdateSelectedBlenshapeSliders;
            InputController.RightHandTrackpadTouch += StoreSelectedBlendshapeSlidersPreviousValues;
            InputController.RightHandTrackpadTouchCanceled += ManageSlidersValueChange;
            InputController.OnToggleNBIAction += HandleNBIToggle;

            InputController.OnDpadPressed += HandleDpadPressed;
            InputController.OnDpadReleased += HandleDpadReleased;

            // SelectionController events
            _selectionController.OnAddToSelection += HandleElementSelection;
            _selectionController.OnRemoveFromSelection += HandleElementDeselection;
            _selectionController.OnClearSelection += HandleSelectionCleared;

            // SplineNodeInteractablesController events
            _splineNodesInteractableController.OnNodeTransformChanged += ManageSplineNodeChanged;

            // SplinePresetsController events
            _splinePresetsController.OnRecordLoad += _splineNodesInteractableController.AddSplineNodesInteractables;

            // BlendshapesController events
            _blendshapesController.OnSectionSelectionChanged += HandleSectionSelectionChanged;
            _blendshapesController.OnSectionSelectionCleared += HandleSectionSelectionCleared;

            // DiseasePlacementController events
            _diseasePlacementController.OnDiseaseProjected += _selectionController.AddRendererToSelection;
            _diseasePlacementController.OnDiseaseUnprojected += _selectionController.RemoveRendererFromSelection;
        }

        /// <summary>
        /// Unsubscribes from all events to prevent memory leaks on scene unload.
        /// Mirror of SubscribeEvents() - every subscription has a corresponding unsubscription.
        /// </summary>
        /// <remarks>
        /// Called in OnDestroy() lifecycle method.
        /// Includes null checks for all controllers to safely handle partial initialization failures.
        /// </remarks>
        private void UnsubscribeEvents()
        {
            if (UIController == null) return;

            // UIController - Mode toggle events
            UIController.OnToggleGroupUpdate -= BeforeEditionModeChangeChecks;
            UIController.OnSaveModelModalShowed -= OnSaveModel;
            UIController.OnTractModeSelected -= HandleTractModeSelected;
            UIController.OnTractModeDeselected -= HandleTractModeDeselected;
            UIController.OnDiseasesModeSelected -= HandleDiseasesModeSelected;
            UIController.OnDiseasesModeDeselected -= HandleDiseasesModeDeselected;
            UIController.OnPreviewModeDeselected -= HandlePreviewModeDeselected;

            // UIController - Common actions
            UIController.OnUndoAction -= UndoAction;
            UIController.OnRedoAction -= RedoAction;
            UIController.OnSaveModelAction -= ConfirmSaveModel;
            UIController.OnSaveModelAsAction -= ConfirmSaveModelAs;
            UIController.OnExitRoomAction -= ValidateExitRoom;
            UIController.OnConfirmExitRoomAction -= ExitTrainingRoom;
            UIController.OnHelpAction -= Help;

            // UIController - Tract mode controls
            UIController.OnJoinedNodesSelected -= EnableJoinedNodes;
            UIController.OnJoinedNodesDeselected -= DisableJoinedNodes;
            UIController.OnMultipleSelectionSelected -= EnableMultipleSelection;
            UIController.OnMultipleSelectionDeselected -= DisableMultipleSelection;

            // UIController - Preset actions
            UIController.OnApplyPresetAction -= ApplySplinePreset;
            UIController.OnCreatePresetAction -= ConfigureEditorForNewPresetCreation;
            UIController.OnEditPresetAction -= ConfigureEditorForExistingPresetEdition;
            UIController.OnDeletePresetAction -= ConfirmDeleteSplinePreset;
            UIController.OnImportPresetAction -= SplinePresetImport;
            UIController.OnExportPresetAction -= SplinePresetExport;

            UIController.OnPresetEditionSaveAction -= SaveSplinePreset;
            UIController.OnPresetEditionSaveAsAction -= SaveSplinePresetAs;
            UIController.OnPresetEditionExitAction -= ExitSplinePresetEditionOrCreation;

            // UIController - Disease actions
            UIController.OnPlaceDiseaseAction -= PlaceDisease;
            UIController.OnEnlargeDiseaseAction -= EnlargeDiseaseMesh;
            UIController.OnShrinkDiseaseAction -= ShrinkDiseaseMesh;

            // UIController - Selection actions
            UIController.OnClearSelectionAction -= ClearMultipleSelection;

            // UIController - Scroll area events
            UIController.OnSplinePresetRead -= HandleSplinePresetRead;
            UIController.OnSplineFilterNoResults -= HandleSplineFilterNoResults;
            UIController.OnSplinePresetScrollAreaElementSelected -= OnSplinePresetSrollAreaElementSelect;

            UIController.OnDiseaseRead -= HandleDiseaseRead;
            UIController.OnDiseaseFilterNoResults -= HandleDiseaseFilterNoResults;
            UIController.OnDiseaseScrollAreaElementSelected -= OnDiseaseScrollAreaElementSelect;

            if (InputController != null)
            {
                InputController.LocomotionChanged -= ManageLocomotionChange;
                InputController.OnRightHandTrackpadInput -= UpdateSelectedBlenshapeSliders;
                InputController.RightHandTrackpadTouch -= StoreSelectedBlendshapeSlidersPreviousValues;
                InputController.RightHandTrackpadTouchCanceled -= ManageSlidersValueChange;
                InputController.OnDpadPressed -= HandleDpadPressed;
                InputController.OnDpadReleased -= HandleDpadReleased;
            }

            // SelectionController events
            if (_selectionController != null)
            {
                _selectionController.OnAddToSelection -= HandleElementSelection;
                _selectionController.OnRemoveFromSelection -= HandleElementDeselection;
                _selectionController.OnClearSelection -= HandleSelectionCleared;
            }

            // SplineNodeInteractablesController events
            if (_splineNodesInteractableController != null)
            {
                _splineNodesInteractableController.OnNodeTransformChanged -= ManageSplineNodeChanged;
            }

            // SplinePresetsController events
            if (_splinePresetsController != null)
            {
                _splinePresetsController.OnRecordLoad -= _splineNodesInteractableController.AddSplineNodesInteractables;
            }

            // BlendshapesController events
            if (_blendshapesController != null)
            {
                _blendshapesController.OnSectionSelectionChanged -= HandleSectionSelectionChanged;
                _blendshapesController.OnSectionSelectionCleared -= HandleSectionSelectionCleared;
            }

            // DiseasePlacementController events
            if (_diseasePlacementController != null)
            {
                _diseasePlacementController.OnDiseaseProjected -= _selectionController.AddRendererToSelection;
                _diseasePlacementController.OnDiseaseUnprojected -= _selectionController.RemoveRendererFromSelection;
            }
        }

        #region Event Handler Methods (for proper subscribe/unsubscribe)

        private void HandleTractModeSelected() => UIController.ToggleTractModeToolsSlideEffect(true);
        private void HandleTractModeDeselected() => UIController.ToggleTractModeToolsSlideEffect(false);
        private void HandleDiseasesModeSelected() => UIController.ToggleDiseasesModeToolsSlideEffect(true);
        private void HandleDiseasesModeDeselected() => UIController.ToggleDiseasesModeToolsSlideEffect(false);
        private void HandlePreviewModeDeselected() => UIController.TogglePreviewModeToolsSlideEffect(false);

        private bool HandleSplinePresetRead(int resultCode) => ManageMessageCode(resultCode);
        private void HandleSplineFilterNoResults(int resultCode) => ManageMessageCode(resultCode);
        private bool HandleDiseaseRead(int resultCode) => ManageMessageCode(resultCode);
        private void HandleDiseaseFilterNoResults(int resultCode) => ManageMessageCode(resultCode);

        private void HandleDpadPressed() => MainRoomManager.Instance?.InputController?.SetCharacterLocomotionEnabled(false);
        private void HandleDpadReleased() => MainRoomManager.Instance?.InputController?.SetCharacterLocomotionEnabled(true);

        private void HandleSectionSelectionChanged() => UIController.SetDetailsPanelInteractive(true);
        private void HandleSectionSelectionCleared() => UIController.SetDetailsPanelInteractive(false);

        #endregion

        /// <summary>
        /// Unity Start lifecycle method.
        /// Initializes temporary data structures and begins async initialization.
        /// </summary>
        /// <remarks>
        /// Starts coroutine Initialize() which:
        /// 1. Loads or creates model data
        /// 2. Generates segmented model
        /// 3. Adds interactables
        /// 4. Determines starting mode (Diseases if HasMesh, else Tract)
        /// </remarks>
        private void Start()
        {
            _tempMaterialsList = new List<Material[]>();
            _meshExporter = _modelGenerator.GetComponentInChildren<ImgSpcExporter>(true);
            ApplyAppSettings();
            StartCoroutine(Initialize());
        }

        private void ApplyAppSettings()
        {
            _appSettings = _applicationManager.AppSettings;
            if (_appSettings != null)
            {
                UIController.SetUseGrabKeybindProfiles(_appSettings.UseGrabKeybindProfiles);
                _appSettings.OnUseGrabKeybindProfilesChanged += UIController.SetUseGrabKeybindProfiles;
                InputController.SetUseGrabKeybindProfiles(_appSettings.UseGrabKeybindProfiles);
                _appSettings.OnUseGrabKeybindProfilesChanged += InputController.SetUseGrabKeybindProfiles;
            }
            else
            {
                Debug.LogError("[ModelEditorManager] IAppSettingsProvider not available from ApplicationManager.");
            }
        }

        /// <summary>
        /// Handles the toggle of NBI (Narrow Band Imaging) mode from input. Updates the welded model's material to reflect the NBI state.
        /// </summary>
        private void HandleNBIToggle()
        {
            SetWeldedModelNBI(_isNBIEnabled ? 0f : 1f);
        }

        /// <summary>
        /// Sets the NBI (Narrow Band Imaging) state on the welded model's material by updating a shader property.
        /// This method assumes the material used by the welded model has a float property named "_NBI" that controls the NBI effect.
        /// The value is typically 0 for normal view and 1 for NBI view, but this depends on the shader implementation.
        /// The method retrieves the MeshRenderer from the ISplineModelGenerator, gets the current MaterialPropertyBlock, sets the "_NBI" property, and applies the block back to the renderer.
        /// This allows dynamic toggling of the NBI effect without needing to switch materials or reload the mesh, enabling smooth transitions between views during training.
        /// </summary>
        /// <param name="value"></param>
        private void SetWeldedModelNBI(float value)
        {
            var renderer = _modelGenerator?.WeldedModelRenderer;
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat("_NBI", value);
            renderer.SetPropertyBlock(block);
            _isNBIEnabled = value > 0 ? true : false;
        }

        /// <summary>
        /// Async initialization coroutine.
        /// Loads model data, generates segmented model, and sets starting mode.
        /// </summary>
        /// <returns>Coroutine enumerator.</returns>
        /// <remarks>
        /// Initialization sequence:
        /// 1. Load/create current model from ApplicationManager
        /// 2. Initialize spline from model data (if existing model)
        /// 3. Load generation configuration
        /// 4. Configure controllers with dependencies
        /// 5. Generate segmented model (async, waits for completion)
        /// 6. Add section and node interactables
        /// 7. Start in Diseases mode if model has mesh, else Tract mode
        /// 8. Set teleport destination
        /// 9. Mark as initialized
        /// 
        /// Waits for SegmentedModelGenerated flag before proceeding to interactables.
        /// </remarks>
        private IEnumerator Initialize()
        {
            if (_currentModel == null)
            {
                _applicationManager.SetCurrentModel(new Model());
            }
            else
            {
                //_currentModel.LoadFromDefaultPath();//model should be loaded already in this version
                _modelGenerator.InitializeSplineFromModelData(_currentModel);
            }

            _modelGenerator.LoadGenerationConfiguration(_currentModel);
            ConfigureControllers();
            _modelGenerator.GenerateSegmentedModel();
            ModelChangesNotSaved = false;

            yield return new WaitUntil(() => _modelGenerator.IsSegmentedModelGenerated);

            _liSectionsInteractablesController.AddSectionsInteractables();
            _liSectionsInteractablesController.DisableSectionInteractables();
            _splineNodesInteractableController.AddSplineNodesInteractables();
            _splineNodesInteractableController.HideNodes();
            _splineNodesInteractableController.DisableNodeInteraction();
            EditionNodesReady = true;
            if (_currentModel.HasMesh)
            {
                StartInDiseaseEditionMode();
            }
            else
            {
                StartInTractEditionMode();
            }
            SetRoomTeleportDestination();
            IsInitialized = true;
            //EnableVRControllersGO();
            yield return null;
        }

        /// <summary>
        /// Sets the teleport destination for VR locomotion in the Model Editor room.
        /// Notifies the teleport destination provider that the room is ready for player entry.
        /// </summary>
        private void SetRoomTeleportDestination()
        {
            Debug.Log("[ModelEditorManager] Setting room teleport destination.");
            _teleportDestinationProvider?.NotifyReady();
        }

        /// <summary>
        /// Validates whether the user can exit the Model Editor room.
        /// Checks for unsaved changes in preset edition or model before allowing exit.
        /// Shows confirmation modals if changes exist.
        /// </summary>
        private void ValidateExitRoom()
        {
            if (_presetModeController.IsPresetEditionEnabledWithUnsavedChanges())
            {
                UIController.ShowConfirmModal(
                    ModelEditorMessages.PresetChangesNotSaved,
                    ValidateExitRoom
                    );
            }
            else if (ModelChangesNotSaved)
            {
                UIController.ShowConfirmModal(
                   ModelEditorMessages.ModelChangesNotSaved,
                   ConfirmExitTrainingRoom
                   );
            }
            else
            {
                ConfirmExitTrainingRoom();
            }
        }

        /// <summary>
        /// Shows the final confirmation modal before exiting the Model Editor room.
        /// Called after validation checks pass (no unsaved changes or user confirmed discard).
        /// </summary>
        private void ConfirmExitTrainingRoom()
        {
            UIController.ShowConfirmExitRoomModal();
        }

        /// <summary>
        /// Executes the exit from Model Editor room.
        /// Fires OnExitRoomRequested event to notify the application manager to perform scene transition.
        /// </summary>
        private void ExitTrainingRoom()
        {
            OnExitRoomRequested?.Invoke();
        }

        /// <summary>
        /// Clears all currently selected elements (nodes or sections).
        /// Delegates to SelectionController which notifies all listeners.
        /// </summary>
        private void ClearMultipleSelection()
        {
            _selectionController.ClearSelectionAndNotify();
        }

        /// <summary>
        /// Handles locomotion mode changes between character and endoscope movement.
        /// Updates the UI keybind display to show appropriate controls for each mode.
        /// </summary>
        /// <param name="isEndoscopeLocomotion">True if endoscope locomotion, false for character locomotion.</param>
        private void ManageLocomotionChange(bool isEndoscopeLocomotion)
        {
            if (isEndoscopeLocomotion)
            {
                UIController.SetEndoscopeLocomotionKeybindDisplayProfile();
            }
            else
            {
                UIController.SetCharacterLocomotionKeybindDisplayProfile();
            }
        }

        /// <summary>
        /// Updates the active slider target based on VR trackpad input.
        /// Details mode: adjusts selected blendshape sliders. Tract mode: adjusts selected noise slider.
        /// </summary>
        /// <param name="input">Delta value from trackpad input.</param>
        private void UpdateSelectedBlenshapeSliders(float input)
        {
            GetTrackpadTarget()?.UpdateSelectedSliderByDelta(input);
        }

        /// <summary>
        /// Stores current slider value(s) before trackpad adjustment begins.
        /// Details mode: for undo record. Tract mode: for regeneration diff on release.
        /// </summary>
        private void StoreSelectedBlendshapeSlidersPreviousValues()
        {
            GetTrackpadTarget()?.StoreSelectedSliderPreviousValue();
        }

        /// <summary>
        /// Finalizes slider changes after trackpad touch ends.
        /// Details mode: records undo entry. Tract mode: triggers mesh regeneration.
        /// </summary>
        private void ManageSlidersValueChange()
        {
            GetTrackpadTarget()?.OnTrackpadReleased();
        }

        /// <summary>
        /// Returns the ITrackpadSliderTarget for the current edition mode,
        /// or null if the current mode does not use trackpad slider input.
        /// </summary>
        private ITrackpadSliderTarget GetTrackpadTarget() => CurrentEditionMode switch
        {
            EditionMode.Details => _blendshapesController,
            EditionMode.Tract => _modelNoiseSettingsController,
            _ => null
        };

        /// <summary>
        /// Clears all selection visual indicators (outlines) without notifying listeners.
        /// Used internally for cleanup during mode transitions.
        /// </summary>
        private void ClearSelection()
        {
            _selectionController.ClearAllOutlines();
        }

        /// <summary>
        /// Starts the Model Editor in Tract edition mode.
        /// Called during initialization when creating a new model (no existing mesh).
        /// </summary>
        private void StartInTractEditionMode()
        {
            EnterEditorMode(EditionMode.Tract);
        }

        /// <summary>
        /// Starts the Model Editor in Disease edition mode.
        /// Called during initialization when loading an existing model with mesh.
        /// Loads the saved mesh and configures disease placement system.
        /// </summary>
        private void StartInDiseaseEditionMode()
        {
            _modelGenerator.InitializeWeldedModelGO();
            //var fallbackMats = _modelGenerator.Sections.Count > 0
            //    ? _modelGenerator.Sections[0].Renderer.sharedMaterials
            //    : null;
            _currentModel.LoadIntoSMR(_modelGenerator.WeldedModelRenderer, _modelGenerator.WeldedModelCollider, null);
            _diseaseModeController.WeldedModelNeedsUpdate = false;
            //_modelGenerator.WeldedModelCollider.sharedMesh = _modelGenerator.WeldedModelRenderer.sharedMesh;
            EnableDiseasesEditionMode();
            InputController.ChangeLocomotion(); //Entering disease mode defaults to endoscope locomotion, so we need to change it after entering the mode to move in the room.
        }

        /// <summary>
        /// Validates and handles edition mode changes before switching.
        /// Implements state machine logic for mode transitions with confirmation modals.
        /// </summary>
        /// <param name="targetEditionModeIdx">Index of target EditionMode enum value.</param>
        /// <remarks>
        /// Transition Logic:
        /// - Switching TO Diseases mode: Confirms welded model generation
        /// - Switching FROM Diseases mode: Confirms disease edition disabling
        /// - Switching FROM Presets mode: Checks for unsaved preset changes
        /// - Default: Direct switch via EnterEditorMode()
        /// 
        /// Pattern matching switch expression ensures all transition cases are handled.
        /// User confirmations required for destructive actions (losing unsaved work).
        /// </remarks>
        //CALLED ALWAYS WHEN A TOGGLE MODE IS PRESSED UNLESS IT IS THE CURRENTLY ACTIVE ONE
        private void BeforeEditionModeChangeChecks(int targetEditionModeIdx)
        {
            var targetEditionMode = (EditionMode)targetEditionModeIdx;
            if (targetEditionMode == CurrentEditionMode)
            {
                return;
            }

            var isDiseaseEditionEnabled = _diseaseModeController?.IsDiseaseModeEnabled ?? false;

            Action action = (CurrentEditionMode, targetEditionMode, isDiseaseEditionEnabled) switch
            {
                // Switching TO Diseases mode
                (not EditionMode.Preview, EditionMode.Diseases, _) =>
                    () => ConfirmSwitchToDiseasesMode(),
                (EditionMode.Preview, EditionMode.Diseases, false) =>
                    () => ConfirmSwitchToDiseasesMode(),
                (EditionMode.Preview, EditionMode.Diseases, true) =>
                    () => EnterEditorMode(targetEditionMode),

                // Switching FROM Diseases mode
                (EditionMode.Diseases, not EditionMode.Preview, _) =>
                    () => ConfirmExitOfDiseasesMode(targetEditionMode),

                // Switching FROM Preview mode with diseases enabled
                (EditionMode.Preview, not EditionMode.Diseases, true) =>
                    () => ConfirmExitOfDiseasesMode(targetEditionMode),

                // Switching FROM Presets mode
                (EditionMode.Presets, _, _) =>
                    () => HandlePresetEditionModeExit(targetEditionMode),

                // Default case - direct switch
                _ =>
                    () => EnterEditorMode(targetEditionMode)
            };

            action();
        }

        /// <summary>
        /// Handles exiting Presets edition mode with proper cleanup.
        /// Checks for unsaved changes or active preview state before proceeding.
        /// </summary>
        /// <param name="targetEditionMode">Mode to switch to after exiting presets mode.</param>
        /// <remarks>
        /// Three possible states:
        /// 1. Preset creation with unsaved changes → Confirm exit (lose changes)
        /// 2. Preview active → Confirm apply or cancel preview
        /// 3. Normal presets view → Direct switch
        /// </remarks>
        private void HandlePresetEditionModeExit(EditionMode targetEditionMode)
        {
            if (_presetModeController.IsPresetEditionEnabledWithUnsavedChanges())
                ConfirmExitPresetModeCreation(targetEditionMode); //In presets mode creation view, want to leave preset mode
            else if (_presetModeController.IsPreviewActive)
                ConfirmExitPresetMode(targetEditionMode); //in presets mode management view, want to leave preset mode
            else
                EnterEditorMode(targetEditionMode);
        }

        /// <summary>
        /// Switches to the target edition mode.
        /// Exits current mode, clears selection, updates UI, and enters new mode.
        /// </summary>
        /// <param name="targetEditionMode">Mode to enter.</param>
        /// <remarks>
        /// Mode switch sequence:
        /// 1. ExitCurrentEditorMode() - Cleanup current mode via controller
        /// 2. ClearSelection() - Reset all selection state
        /// 3. Update CurrentEditionMode property
        /// 4. UIController.UpdateToggleGroup() - Sync UI state
        /// 5. Enter new mode via its IEditionModeController.Enter()
        /// 
        /// Mode controllers handle their own Enter/Exit logic (enable/disable interactables, etc.).
        /// </remarks>

        private void EnterEditorMode(EditionMode targetEditionMode)
        {
            ExitCurrentEditorMode();
            ClearSelection();
            CurrentEditionMode = targetEditionMode;
            UIController.UpdateToggleGroup((int)CurrentEditionMode);

            // Enter the target mode using its controller
            _modeControllers[targetEditionMode].Enter();
        }

        /// <summary>
        /// Exits the currently active edition mode using its controller's Exit() method.
        /// Delegates cleanup to the mode controller for proper encapsulation.
        /// </summary>
        /// <remarks>
        /// Each IEditionModeController.Exit() handles:
        /// - Disabling mode-specific interactables
        /// - Cleaning up visual state
        /// - Resetting input profiles
        /// - Any mode-specific teardown logic
        /// </remarks>
        private void ExitCurrentEditorMode()
        {
            // Exit the current mode using its controller
            if (_modeControllers.ContainsKey(CurrentEditionMode))
            {
                _modeControllers[CurrentEditionMode].Exit();
            }
        }

        /// <summary>
        /// Opens the settings modal dialog.
        /// Currently displays settings UI but functionality may be limited.
        /// </summary>
        private void Settings()
        {
            UIController.ShowSettingsModal();
        }

        /// <summary>
        /// Opens the help guide video in default browser.
        /// Provides video tutorial for Model Editor usage.
        /// </summary>
        private void Help()
        {
            Application.OpenURL(ModelEditorConstants.HelpGuideVideoUrl);
        }

        /// <summary>
        /// Enables solidary (joined) node movement.
        /// Moving one spline node will affect neighboring nodes.
        /// </summary>
        /// <remarks>
        /// Delegates to SplineNodesInteractablesController.EnableSolidaryNodeMovement().
        /// Used in Tract mode when "Joined Nodes" toggle is activated.
        /// Provides smooth, natural curve editing.
        /// </remarks>
        private void EnableJoinedNodes()
        {
            _splineNodesInteractableController.EnableSolidaryNodeMovement();
        }
        /// <summary>
        /// Disables solidary (joined) node movement.
        /// Each spline node can be moved independently.
        /// </summary>
        /// <remarks>
        /// Delegates to SplineNodesInteractablesController.DisableSolidaryNodeMovement().
        /// Used in Tract mode when "Joined Nodes" toggle is deactivated.
        /// Allows precise individual node positioning.
        /// </remarks>
        private void DisableJoinedNodes()
        {
            _splineNodesInteractableController.DisableSolidaryNodeMovement();
        }

        /// <summary>
        /// Enables multiple selection mode.
        /// User can select multiple sections/nodes simultaneously.
        /// </summary>
        /// <remarks>
        /// Delegates to SelectionController.EnableMultipleSelection().
        /// Enables "Clear Selection" button in UI.
        /// Used primarily in Details mode for batch blendshape editing.
        /// </remarks>
        private void EnableMultipleSelection()
        {
            _selectionController.EnableMultipleSelection();
            UIController.SetClearSelectionButtonInteractable(true);
        }

        /// <summary>
        /// Disables multiple selection mode.
        /// User can only select one section/node at a time.
        /// </summary>
        /// <remarks>
        /// Delegates to SelectionController.DisableMultipleSelection().
        /// Disables "Clear Selection" button in UI.
        /// Single selection mode is default for most operations.
        /// </remarks>
        private void DisableMultipleSelection()
        {
            _selectionController.DisableMultipleSelection();
            UIController.SetClearSelectionButtonInteractable(false);
        }

        /// <summary>
        /// Handles spline node transform changes.
        /// Marks either preset changes or model changes as unsaved depending on current mode.
        /// </summary>
        private void ManageSplineNodeChanged()
        {
            if (_presetModeController.IsEditionEnabled) _presetModeController.HasUnsavedChanges = true;
            else ModelChangesNotSaved = true;
        }

        #region MODEL MANAGEMENT
        /// <summary>
        /// Shows save model modal for overwriting the current model file.
        /// </summary>
        /// <remarks>
        /// User provides file name and description via modal.
        /// On confirmation, calls SaveModel() to overwrite existing file.
        /// </remarks>
        private void ConfirmSaveModel()
        {
            UIController.ShowSaveModelModal(SaveModel, FileMessages.InputNameAndDescription);
        }

        /// <summary>
        /// Shows save model modal for creating a new model file (Save As).
        /// </summary>
        /// <remarks>
        /// User provides new file name and description via modal.
        /// On confirmation, calls SaveModelAs() to create new file.
        /// Original model remains unchanged.
        /// </remarks>
        private void ConfirmSaveModelAs()
        {
            UIController.ShowSaveModelModal(SaveModelAs, FileMessages.InputNameAndDescription);
        }

        /// <summary>
        /// Callback for save model modal input field changes.
        /// Updates the current model's preview data as user types.
        /// </summary>
        /// <param name="inputFields">Modal input fields (file name, description).</param>
        public void OnSaveModel(params TMP_InputField[] inputFields)
        {
            _applicationManager.GetCurrentModel().PreviewDataIn(inputFields);
            //UIController.SetXRKeyboardActive(true);
        }

        /// <summary>
        /// Saves the model by overwriting the existing file.
        /// Delegates to SaveEdition() for actual save logic.
        /// </summary>
        public void SaveModel()
        {
            SaveEdition();
        }

        /// <summary>
        /// Saves the model as a new file (Save As operation).
        /// Creates a new Model instance with new file ID if name changed.
        /// </summary>
        /// <remarks>
        /// If new name equals current name, creates copy with new file ID.
        /// Otherwise, creates new model with same file ID but different name.
        /// Delegates to SaveEdition() for actual save logic.
        /// </remarks>
        public void SaveModelAs()
        {
            var newName = UIController.GetModalFileNameInputFieldText();
            _applicationManager.SetCurrentModel(new Model(_currentModel, copyFileID: newName.Equals(_currentModel.FileName) ? true : false));
            SaveEdition();
        }

        /// <summary>
        /// Callback invoked when model file save operation completes successfully.
        /// Shows confirmation modal and clears unsaved changes flag.
        /// </summary>
        public void OnModelFileEditionSaved()
        {
            UIController.ShowWarningModal(FileMessages.FileSaved);
            ModelChangesNotSaved = false;
        }

        /// <summary>
        /// Saves the current model edition to disk.
        /// Collects all model data (spline, blendshapes, diseases, mesh) and writes to file.
        /// Shows confirmation modal on success.
        /// </summary>
        public void SaveEdition()
        {
            //TODO SEEM UNUSED NOW
            /*if (BlendshapeConfigurationsNeedUpdate)
                UpdateBlenshapeConfigurations();*/

            var isDiseaseEditionEnabled = _diseaseModeController?.IsDiseaseModeEnabled ?? false;
            var diseases = isDiseaseEditionEnabled ? _diseasePlacementController.GetCurrentDiseases() : null;
            var weldedRenderer = isDiseaseEditionEnabled ? _modelGenerator.WeldedModelRenderer : null;
            var inputFields = UIController.GetModalInputFields();
            var fileName = inputFields[0].text;
            var fileDescription = inputFields[1].text;
            var buildResult = _currentModel.Update(
                fileName,
                fileDescription,
                _modelGenerator.Spline.nodes,
                _modelGenerator.SegmentsStartValueInSpline,
                _modelGenerator.GetRenderersBlendshapesDicts(),
                diseases,
                weldedRenderer,
                noiseSettings: _modelGenerator.GetNoiseSettings());

            if (ManageMessageCode(buildResult))
            {
                _meshExporter.ObjectsToExport = new UnityEngine.Object[] { _modelGenerator.WeldedModelGO };
                _currentModel.Save(_modelGenerator.WeldedModelRenderer, _modelPreviewCamera, OnModelFileEditionSaved, _currentModel.Overwrite);
            }
            //UIController.SetXRKeyboardActive(false);
        }
        #endregion


        #region PRESET EDITION MODE
        #region SPLINE PRESETS
        /// <summary>
        /// Coroutine that applies the previewed preset and exits Presets mode.
        /// Waits for preset application to complete before switching modes.
        /// Used when user confirms applying preset preview on mode exit.
        /// </summary>
        /// <param name="targetEditionMode">Mode to switch to after preset applied.</param>
        IEnumerator ApplyPresetAndExitPresetsEditionMode(EditionMode targetEditionMode)
        {
            Debug.Log("Applying preset and exiting presets edition mode");
            ApplySplinePreset();
            yield return new WaitUntil(() => _presetModeController.IsPreviewActive == false);
            OnConfirmExitPresetsEditionModeResult(targetEditionMode);
        }

        /// <summary>
        /// Applies the currently selected spline preset to the model.
        /// Delegates to PresetEditionModeController for preset application logic.
        /// </summary>
        /// <remarks>
        /// Replaces current spline nodes with preset's saved configuration.
        /// Triggers model regeneration and updates interactables.
        /// </remarks>
        public void ApplySplinePreset()
        {
            _presetModeController.ApplySelectedPreset();
        }

        /// <summary>
        /// Configures the editor for creating a new spline preset.
        /// Enables preset creation mode with node editing.
        /// </summary>
        /// <remarks>
        /// User can edit spline nodes to create new preset configuration.
        /// Changes are tracked for save prompt on exit.
        /// Delegates to PresetEditionModeController.
        /// </remarks>
        public void ConfigureEditorForNewPresetCreation()
        {
            _presetModeController.ConfigureForNewPresetCreation();
        }

        /// <summary>
        /// Configures the editor for editing an existing spline preset.
        /// Loads selected preset and enables node editing.
        /// </summary>
        /// <remarks>
        /// User can modify existing preset configuration.
        /// Changes are tracked for save prompt on exit.
        /// Delegates to PresetEditionModeController.
        /// </remarks>
        public void ConfigureEditorForExistingPresetEdition()
        {
            _presetModeController.ConfigureForExistingPresetEdition();
        }

        /// <summary>
        /// Exits preset creation/edition mode and returns to preset management view.
        /// Validates unsaved changes before exit.
        /// </summary>
        public void ExitSplinePresetEditionOrCreation()
        {
            _presetModeController.ExitEditionOrCreation();
        }

        /// <summary>
        /// Disables preset creation/edition mode without validation.
        /// Used for force cleanup when switching modes.
        /// </summary>
        public void DisableSplinePresetEditionOrCreation()
        {
            _presetModeController.DisableEditionOrCreation();
        }

        /// <summary>
        /// Shows confirmation modal when exiting preset creation with unsaved changes.
        /// Warns user that unsaved preset changes will be lost.
        /// </summary>
        /// <param name="targetEditionMode">Mode to switch to if user confirms.</param>
        private void ConfirmExitPresetModeCreation(EditionMode targetEditionMode)
        {
            UIController.ShowConfirmModal(
                ModelEditorMessages.PresetChangesNotSaved,
                () => EnterEditorMode(targetEditionMode));
        }

        /// <summary>
        /// Shows confirmation modal when exiting presets mode with active preview.
        /// Asks user whether to apply or cancel the previewed preset.
        /// </summary>
        /// <param name="targetEditionMode">Mode to switch to after handling preview.</param>
        private void ConfirmExitPresetMode(EditionMode targetEditionMode)
        {
            UIController.ShowConfirmModal(
                ModelEditorMessages.ConfirmPresetPreviewApplyOnExit,
                () => StartCoroutine(ApplyPresetAndExitPresetsEditionMode(targetEditionMode)),
                () => OnConfirmExitPresetsEditionModeCancel(targetEditionMode)
            );
        }

        /// <summary>
        /// Handles cancellation of preset preview on mode exit.
        /// Restores original spline nodes and clears preview state before switching modes.
        /// </summary>
        /// <param name="targetEditionMode">Mode to switch to after cancellation.</param>
        private void OnConfirmExitPresetsEditionModeCancel(EditionMode targetEditionMode)
        {
            UIController.ResetPresetsManagementMenuSelection();
            _presetModeController.ClearPreviewState();
            _splinePresetsController.RestoreSplineNodes();
            _splineNodesInteractableController.AddSplineNodesInteractables();
            OnConfirmExitPresetsEditionModeResult(targetEditionMode);
        }

        /// <summary>
        /// Completes preset mode exit and switches to target mode.
        /// Handles special case for Diseases mode which requires additional validation.
        /// </summary>
        /// <param name="targetEditionMode">Mode to switch to.</param>
        private void OnConfirmExitPresetsEditionModeResult(EditionMode targetEditionMode)
        {
            if (targetEditionMode == EditionMode.Diseases)
            {
                BeforeEditionModeChangeChecks((int)targetEditionMode);
            }
            else
            {
                EnterEditorMode(targetEditionMode);
            }
        }

        /// <summary>
        /// Saves the current preset by overwriting the existing preset file.
        /// Delegates to PresetEditionModeController for save logic.
        /// </summary>
        //TODO review this to be more like the save model methods
        public void SaveSplinePreset()
        {
            _presetModeController.SavePreset();
        }

        /// <summary>
        /// Saves the current preset as a new file (Save As operation).
        /// Delegates to PresetEditionModeController for save logic.
        /// </summary>
        public void SaveSplinePresetAs()
        {
            _presetModeController.SavePresetAs();
        }

        /// <summary>
        /// Validates whether the current preset can be saved.
        /// Checks for required data and valid selection state.
        /// </summary>
        /// <returns>True if preset save is valid.</returns>
        public bool IsValidSplinePresetSaveSelection()
        {
            return _presetModeController.IsValidPresetSaveSelection();
        }

        /// <summary>
        /// Shows confirmation modal before deleting the selected preset.
        /// Delegates to PresetEditionModeController for deletion logic.
        /// </summary>
        private void ConfirmDeleteSplinePreset()
        {
            _presetModeController.ConfirmDeletePreset();
        }

        /// <summary>
        /// Imports a spline preset from an external file.
        /// Currently not implemented - placeholder for future functionality.
        /// </summary>
        private void SplinePresetImport()
        {
            throw new NotImplementedException("SplinePresetImport method is not implemented yet.");
        }

        #region SPLINE PRESET IMPORT/EXPORT
        /// <summary>
        /// Callback when user selects a preset file for import via file browser.
        /// Delegates import to PresetEditionModeController and closes file browser.
        /// </summary>
        /// <param name="paths">Selected file paths from file browser.</param>
        private void OnPresetFileSelectedForImport(string[] paths)
        {
            _presetModeController.OnFileSelectedForImport(paths);
            OnFileBrowserCancel();
        }

        /// <summary>
        /// Exports the selected spline preset to an external file.
        /// Currently not implemented - placeholder for future functionality.
        /// </summary>
        private void SplinePresetExport()
        {
            throw new NotImplementedException("SplinePresetExport method is not implemented yet.");
        }

        /// <summary>
        /// Callback when user selects export destination via file browser.
        /// Exports the selected preset to the chosen file path.
        /// </summary>
        /// <param name="paths">Selected export paths from file browser.</param>
        public void OnModelFileSelectedForExport(string[] paths)
        {
            var selectedSplinePreset = UIController.GetSelectedSplinePreset();
            selectedSplinePreset.Download(paths[0], OnFileExported, selectedSplinePreset.Overwrite);
        }

        /// <summary>
        /// Handles selection of a preset from the presets scroll area UI.
        /// Delegates to PresetEditionModeController for preview or edit actions.
        /// </summary>
        /// <param name="element">Selected preset scroll area element.</param>
        public void OnSplinePresetSrollAreaElementSelect(SplinePresetScrollAreaElement element)
        {
            _presetModeController.OnPresetScrollAreaElementSelect(element);
        }

        /// <summary>
        /// Callback invoked when file export completes successfully.
        /// Shows confirmation modal to user.
        /// </summary>
        private void OnFileExported()
        {
            UIController.ShowWarningModal(FileMessages.FileExported);
        }

        /// <summary>
        /// Callback when user cancels file browser operation.
        /// Currently stubbed out - file browser integration disabled.
        /// </summary>
        public void OnFileBrowserCancel()
        {
            //if (fileBrowserModal == null) fileBrowserModal = FindObjectOfType<FileBrowser>().gameObject.GetComponent<LeanWindow>();
            //fileBrowserModal.TurnOff();
        }

        /// <summary>
        /// Callback when file browser is opened.
        /// Currently stubbed out - file browser integration disabled.
        /// </summary>
        public void OnFileBrowserOpen()
        {
            //if (fileBrowserModal == null) fileBrowserModal = FindObjectOfType<FileBrowser>().gameObject.GetComponent<LeanWindow>();
            //fileBrowserModal.TurnOn();
        }
        #endregion
        #endregion
        #endregion

        #region DISEASE EDITION MODE
        /// <summary>
        /// Shows confirmation modal before switching to Diseases mode.
        /// Warns user that entering disease mode will generate welded model (computationally expensive).
        /// </summary>
        private void ConfirmSwitchToDiseasesMode()
        {
            UIController.ShowConfirmModal(
                ModelEditorMessages.ConfirmDiseaseEditionEnabling,
                EnableDiseasesEditionMode);
        }

        /// <summary>
        /// Shows confirmation modal before exiting Diseases mode.
        /// Warns user that disease placement state will be disabled.
        /// </summary>
        /// <param name="targetEditionMode">Mode to switch to if user confirms.</param>
        private void ConfirmExitOfDiseasesMode(EditionMode targetEditionMode)
        {
            UIController.ShowConfirmModal(
                ModelEditorMessages.ConfirmDiseaseEditionDisabling,
                () => DisableDiseasesEditionMode(targetEditionMode));
        }


        /// <summary>
        /// Enables Diseases edition mode.
        /// Switches to Diseases mode and initializes disease placement system.
        /// </summary>
        private void EnableDiseasesEditionMode()
        {
            EnterEditorMode(EditionMode.Diseases);
            _diseaseModeController.EnableDiseaseEdition();
        }

        /// <summary>
        /// Disables Diseases edition mode and switches to target mode.
        /// Cleans up disease placement state before mode transition.
        /// </summary>
        /// <param name="targetEditionMode">Mode to switch to after disabling diseases.</param>
        private void DisableDiseasesEditionMode(EditionMode targetEditionMode)
        {
            _diseaseModeController.DisableDiseaseEdition(CurrentEditionMode);
            EnterEditorMode(targetEditionMode);
        }

        public void SetDiseaseModeAllToolsInteractive(bool isInteractive)
        {
            UIController.SetDiseaseModeToolsControls(UIController.DiseaseTool.All, isInteractive);
        }

        /// <summary>
        /// Sets the disease mode tools controls to interactive or not for the specified tool.
        /// This is used to enable or disable the shrink tool in disease mode.
        /// </summary>
        /// <param name="isInteractive"></param>
        public void SetDiseaseModeShrinkInteractive(bool isInteractive)
        {
            UIController.SetDiseaseModeToolsControls(UIController.DiseaseTool.Shrink, isInteractive);
        }

        /// <summary>
        /// Handles selection of a disease from the diseases scroll area UI.
        /// Toggles selection - selects disease if not selected, deselects if already selected.
        /// Enables/disables disease placer accordingly.
        /// </summary>
        /// <param name="dsce">Selected disease scroll area element.</param>
        private void OnDiseaseScrollAreaElementSelect(DiseaseScrollAreaElement dsce)
        {
            if (UIController.GetSelectedDiseaseElement() != dsce)
            {
                if (UIController.GetSelectedDisease() != null)
                {
                    UIController.ClearDiseasesScrollAreaSelection();
                }
                dsce.SetAsSelected();
                SetDiseasePlacementControllerEnable(true);
                SetUpDiseasePlacer(dsce);
            }
            else
            {
                UIController.ClearDiseasesScrollAreaSelection();
                _diseasePlacementController.Reset();
                SetDiseasePlacementControllerEnable(false);
                SetDiseaseModeAllToolsInteractive(false);
            }
        }

        /// <summary>
        /// Configures the disease placement controller with selected disease data.
        /// Resets previous state and loads new disease mesh for projection.
        /// </summary>
        /// <param name="dsce">Disease scroll area element containing disease data.</param>
        private void SetUpDiseasePlacer(DiseaseScrollAreaElement dsce)
        {
            _diseasePlacementController.Reset();
            _diseasePlacementController.SetUp(dsce.Data);
        }

        /// <summary>
        /// Gets bounding boxes for all intestine sections.
        /// Used for spatial queries and disease placement validation.
        /// </summary>
        /// <returns>List of Bounds for each model section.</returns>
        /// <remarks>
        /// Delegates to LIModelGenerator.GetIntestineSectionsBounds().
        /// Used by disease placement to determine which section a disease overlaps.
        /// </remarks>
        public List<Bounds> GetIntestineSectionsBounds()
        {
            return _modelGenerator.GetIntestineSectionsBounds();
        }

        #endregion


        /// <summary>
        /// Opens the Model Editor help guide video in default browser.
        /// Provides video tutorial for using the Model Editor features.
        /// </summary>
        public void OpenGuideVideoURL()
        {
            Application.OpenURL(ModelEditorConstants.HelpGuideVideoUrl);
        }

        #region DISEASE PLACER

        /// <summary>
        /// Enables or disables the disease placement controller.
        /// Controls whether disease projection and placement is active.
        /// </summary>
        /// <param name="isEnabled">True to enable disease placement.</param>
        public void SetDiseasePlacementControllerEnable(bool isEnabled)
        {
            _diseasePlacementController.enabled = isEnabled;
        }

        /// <summary>
        /// Initiates disease placement at the projected location.
        /// Gets placement location from disease placer and shows confirmation modal.
        /// </summary>
        /// <remarks>
        /// User projects disease mesh onto model, then clicks Place button.
        /// Modal allows selecting specific intestine section (cecum, colon, rectum, etc.).
        /// On confirmation, disease is merged into welded model.
        /// </remarks>
        public void PlaceDisease()
        {
            var diseaseLocation = _diseasePlacementController.GetDiseasePlacementLocation();
            ConfirmDiseasePlacement(diseaseLocation);
        }

        /// <summary>
        /// Shows modal for confirming disease placement with location selection.
        /// </summary>
        /// <param name="intestineLocation">Suggested intestine location (auto-detected).</param>
        /// <remarks>
        /// Modal displays dropdown with intestine section options.
        /// User can override auto-detected location before confirming.
        /// On confirmation, calls OnDiseasePlacementLocationConfirmed().
        /// </remarks>
        public void ConfirmDiseasePlacement(Disease.IntestineLocation intestineLocation)
        {
            UIController.ShowConfirmDiseasePlacementModal(
                () => OnDiseasePlacementLocationConfirmed(),
                _diseasePlacementController.GetDiseasePlacementLocationOptions(),
                (int)intestineLocation
            );
        }

        /// <summary>
        /// Callback when user confirms disease placement location via modal.
        /// Finalizes disease placement and merges it into welded model at selected intestine location.
        /// </summary>
        public void OnDiseasePlacementLocationConfirmed()
        {
            var selectedDiseaseLocation = UIController.GetModalDropwdownSelectedIndex();
            _diseasePlacementController.Merge((Disease.IntestineLocation)selectedDiseaseLocation);
        }

        /// <summary>
        /// Enlarges the currently projected disease mesh.
        /// Scales up the disease mesh before final placement.
        /// </summary>
        private void EnlargeDiseaseMesh()
        {
            ScaleDiseasePlacerMesh(1f, true);
        }

        /// <summary>
        /// Shrinks the currently projected disease mesh.
        /// Scales down the disease mesh before final placement.
        /// </summary>
        private void ShrinkDiseaseMesh()
        {
            ScaleDiseasePlacerMesh(-1f, false);
        }

        /// <summary>
        /// Scales the disease placer mesh by a specific factor.
        /// Used internally by EnlargeDiseaseMesh and ShrinkDiseaseMesh.
        /// Updates both the mesh and its projection bounds.
        /// </summary>
        /// <param name="scaleFactor">Additive scale factor (positive for enlarge, negative for shrink).</param>
        /// <param name="upscaleOperation">True if scaling up, false if scaling down.</param>
        private void ScaleDiseasePlacerMesh(float scaleFactor, bool upscaleOperation)
        {
            _diseasePlacementController.diseaseScaleFactor += Vector3.one * scaleFactor * _diseasePlacementController.diseaseScaleFactorMultiplier;
            _diseasePlacementController.UpdateProjectedMesh(upscaleOperation);
            _diseasePlacementController.UpdateDiseaseProjectionBaseBounds();
        }
        #endregion

        /// <summary>
        /// Central error/message handling.
        /// Routes message codes to appropriate modals (error, warning, confirm).
        /// </summary>
        /// <param name="resultCode">Message code from BasicMessages, FileMessages, or FileErrors.</param>
        /// <param name="onConfirm">Optional callback for confirmation modals.</param>
        /// <returns>True if operation should continue, false if error occurred.</returns>
        /// <remarks>
        /// Message code categories:
        /// - BasicMessages.None → Success, return true
        /// - BasicMessages.Cancel → User cancelled, return false
        /// - FileErrors.* → Show error modal, return false
        /// - BasicMessages.SearchWithoutResults → Show warning modal, return false
        /// - FileMessages.ChangesNotSaved → Show confirm modal with callback
        /// - Default → Unexpected error modal, return false
        /// 
        /// Used throughout for consistent error handling.
        /// </remarks>
        public bool ManageMessageCode(int resultCode, Action onConfirm = null)
        {
            //Debug.Log($"ManageMessageCode called with resultCode: {resultCode}");
            switch (resultCode)
            {
                case BasicMessages.None:
                    return true;
                case BasicMessages.Cancel:
                    break;
                case FileErrors.MissingName:
                case FileErrors.FileNotFound:
                case FileErrors.InvalidExtension:
                case FileErrors.FileParsing:
                    UIController.ShowErrorModal(resultCode);
                    break;
                case BasicMessages.SearchWithoutResults:
                    UIController.ShowWarningModal(resultCode);
                    break;
                case FileErrors.FileAlreadyExists:
                case FileMessages.ChangesNotSaved:
                    UIController.ShowErrorModal(resultCode, onConfirm);
                    break;
                default:
                    UIController.ShowErrorModal(BasicMessages.Unexpected);
                    break;
            }
            return false;
        }

        /// <summary>
        /// Gets the global width factor used for blendshape calculations.
        /// Delegates to model generator.
        /// </summary>
        /// <returns>Global width scaling factor.</returns>
        /// <remarks>
        /// Used by blendshape controllers to normalize width adjustments
        /// across different model scales and configurations.
        /// </remarks>
        public float GetBlendshapeGlobalWidthFactor()
        {
            return _modelGenerator.GetBlendshapeGlobalWidthFactor();
        }

        /// <summary>
        /// Handles selection events from SelectionController.
        /// Routes selection to appropriate feature controller based on item type.
        /// </summary>
        /// <param name="args">Selection event arguments with item type and reference.</param>
        /// <remarks>
        /// Supported selection types:
        /// - IModelSectionInteractable → BlendshapesController.AddSectionToSelection()
        /// - XRSplineNodeInteractable → No special handling (handled by node controller)
        /// 
        /// Logs warning for unhandled selection types for debugging.
        /// </remarks>
        private void HandleElementSelection(SelectionEventArgs args)
        {
            if (args.GetItem<IModelSectionInteractable>() is IModelSectionInteractable section)
            {
                _blendshapesController.AddSectionToSelection(section);
                return;
            }

            if (args.GetItem<XRSplineNodeInteractable>() is XRSplineNodeInteractable sni)
            {
                // Handle spline node selection if needed
                // For now, spline nodes don't need special handling
                return;
            }

            // Log unhandled types for debugging
            Debug.LogWarning($"Unhandled selection type: {args.ItemType?.Name}");
        }

        /// <summary>
        /// Handles deselection events from SelectionController.
        /// Routes deselection to appropriate feature controller based on item type.
        /// </summary>
        /// <param name="args">Deselection event arguments with item type and reference.</param>
        /// <remarks>
        /// Supported deselection types:
        /// - IModelSectionInteractable → BlendshapesController.RemoveSectionFromSelection()
        /// - XRSplineNodeInteractable → No special handling
        /// 
        /// Logs warning for unhandled deselection types for debugging.
        /// </remarks>
        private void HandleElementDeselection(SelectionEventArgs args)
        {
            if (args.GetItem<IModelSectionInteractable>() is IModelSectionInteractable section)
            {
                _blendshapesController.RemoveSectionFromSelection(section);
                return;
            }

            if (args.GetItem<XRSplineNodeInteractable>() is XRSplineNodeInteractable sni)
            {
                // Handle spline node deselection if needed
                return;
            }

            Debug.LogWarning($"Unhandled deselection type: {args.ItemType?.Name}");
        }

        /// <summary>
        /// Handles selection cleared event from SelectionController.
        /// Clears selections in all feature controllers.
        /// </summary>
        /// <remarks>
        /// Currently notifies BlendshapesController.ClearSelection().
        /// Add additional cleanup here if other controllers need notification.
        /// </remarks>
        private void HandleSelectionCleared()
        {
            _blendshapesController.ClearSelection();
            // Any other cleanup when selection is cleared
        }

        /// <summary>
        /// Performs undo action for the current edition mode.
        /// Delegates to the appropriate IEditorActionController based on current mode.
        /// </summary>
        /// <remarks>
        /// Mode-specific undo targets:
        /// - Tract/Presets (editing): SplineNodesInteractablesController
        /// - Presets (management): SplinePresetsController
        /// - Details: BlendshapesController
        /// - Diseases: DiseasePlacementController
        /// </remarks>
        private void UndoAction()
        {
            ExecuteUndoRedoAction(controller => controller.Undo());
        }

        /// <summary>
        /// Performs redo action for the current edition mode.
        /// Delegates to the appropriate IEditorActionController based on current mode.
        /// </summary>
        /// <remarks>
        /// Mode-specific redo targets:
        /// - Tract/Presets (editing): SplineNodesInteractablesController
        /// - Presets (management): SplinePresetsController
        /// - Details: BlendshapesController
        /// - Diseases: DiseasePlacementController
        /// </remarks>
        private void RedoAction()
        {
            ExecuteUndoRedoAction(controller => controller.Redo());
        }

        /// <summary>
        /// Executes an undo/redo action on the current mode's action controller.
        /// Strategy pattern for delegating undo/redo to mode-specific controllers.
        /// </summary>
        /// <param name="action">Action to execute (Undo or Redo).</param>
        /// <remarks>
        /// Uses GetCurrentModeController() to determine which controller
        /// should handle the action based on CurrentEditionMode.
        /// </remarks>
        private void ExecuteUndoRedoAction(Action<IEditorActionController> action)
        {
            var controller = GetCurrentModeController();
            action(controller);
        }

        /// <summary>
        /// Returns the appropriate IEditorActionController for the current edition mode.
        /// Used for undo/redo delegation.
        /// </summary>
        /// <returns>Controller that handles undo/redo for current mode.</returns>
        /// <remarks>
        /// Mode-to-Controller mapping:
        /// - Diseases → DiseasePlacementController
        /// - Details → BlendshapesController
        /// - Presets → Conditional (editing vs management)
        /// - Tract/Default → SplineNodesInteractablesController
        /// </remarks>
        private IEditorActionController GetCurrentModeController()
        {
            return CurrentEditionMode switch
            {
                EditionMode.Diseases => _diseasePlacementController,
                EditionMode.Details => _blendshapesController,
                EditionMode.Presets => GetPresetsController(),
                _ => _splineNodesInteractableController
            };
        }

        /// <summary>
        /// Returns the appropriate controller for preset mode undo/redo.
        /// Chooses between node editing or preset management based on mode state.
        /// </summary>
        /// <returns>
        /// SplineNodesInteractablesController if editing preset nodes,
        /// SplinePresetsController if managing preset list.
        /// </returns>
        /// <remarks>
        /// Preset mode has two sub-states:
        /// - IsEditionEnabled=true: User editing preset nodes (undo node changes)
        /// - IsEditionEnabled=false: User managing presets (undo preset selection)
        /// </remarks>
        private IEditorActionController GetPresetsController()
        {
            return _presetModeController.IsEditionEnabled
                ? _splineNodesInteractableController
                : _splinePresetsController;
        }

        /// <summary>
        /// Unity OnDestroy lifecycle method.
        /// Unsubscribes from all events to prevent memory leaks.
        /// </summary>
        void OnDestroy()
        {
            if (_appSettings != null)
            {
                _appSettings.OnUseGrabKeybindProfilesChanged -= UIController.SetUseGrabKeybindProfiles;
                _appSettings.OnUseGrabKeybindProfilesChanged -= InputController.SetUseGrabKeybindProfiles;
            }
            UnsubscribeEvents();
        }
    }
}