// ============================================================================
// DiseasePlacementController.cs
//
// Handles mesh projection and placement of disease assets (polyps, etc.)
// onto the welded large organ model surface.
//
// Responsibilities:
//   - Project a disease mesh onto the organ surface via batch raycasting
//   - Scale (enlarge/shrink) the projected disease mesh interactively
//   - Detect organ location from the projected mesh vertex positions
//   - Delegate Delaunay-based mesh merge to DiseaseMeshMerger
//   - Manage per-session undo/redo history via ModelRecord snapshots
//
// Architecture:
//   - Receives ISplineModelGenerator and IModelEditorUIController via Initialize(),
//     called once per editor session from DiseaseEditionModeController's constructor.
//   - Init(renderer, collider) must be called before each disease editing session to
//     bind the current welded mesh and rebuild the Job-system NativeArrays.
//   - SetUp(disease) configures the controller for a specific disease asset.
//   - Projection runs in FixedUpdate whenever the projection camera transform changes.
//   - Uses Unity Jobs + Burst for parallel raycasting and vertex projection.
//   - Merging is delegated to DiseaseMeshMerger (one instance per Init() session).
//
// Dependency Flow:
//   DiseaseEditionModeController → Initialize(ISplineModelGenerator, IModelEditorUIController)
//                                → Init(SkinnedMeshRenderer, MeshCollider)
//                                → SetUp(Disease)
// ============================================================================

using GeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Localization;
using static Disease;

namespace ModelEditor
{
    //TODO refactor this to action driven
    /// <summary>
    /// Projects and places disease meshes (polyps, etc.) onto the welded intestine model.
    /// Receives its core dependencies via <see cref="Initialize"/> rather than grabbing singletons.
    /// Mesh merging is delegated to <see cref="DiseaseMeshMerger"/>.
    /// </summary>
    public class DiseasePlacementController : MonoBehaviour, IEditorActionController
    {
        #region Serialized Fields — Projection Configuration

        [SerializeField] private GameObject _projectedDiseaseObject;  // Must be at Vector3.zero; transforms are applied to the mesh directly.

        [Header("Projection Settings")]
        [SerializeField] private bool   _useWelding                         = false;
        [SerializeField] private bool   _useSubmeshes                       = true;
        [SerializeField] private float  _heightOffset                       = 0.00001f;
        [SerializeField] private int    _batchPool                          = 1024;
        [SerializeField] private int    _raycastBatch                       = 1;
        [SerializeField] private Vector3 _translation;
        [SerializeField] private Vector3 _rotation;
        [SerializeField] private Vector3 _scale                             = Vector3.one;
        [SerializeField] private float  _projectionDistance                 = 100f;
        [SerializeField] private Material _delaunayMeshMaterial;

        /// <summary>
        /// Maximum allowed ratio of projected-bounds-magnitude to source-bounds-magnitude.
        /// Exceeding this means the disease mesh is wrapping badly (e.g. rays are hitting
        /// a far wall), and the projection is treated as invalid.
        /// A value of 3 allows up to 3× expansion from surface curvature before flagging distortion.
        /// </summary>
        [SerializeField] private float _maxProjectionDistortionMagnitudeRatio = 3.0f;

        #endregion

        #region Public Fields — Disease Scale (accessed by DiseaseEditionModeController)

        /// <summary>Per-disease scale multiplier applied on top of the base scale.</summary>
        public Vector3 diseaseScaleFactor = Vector3.one;

        /// <summary>Step size added/subtracted to <see cref="diseaseScaleFactor"/> per enlarge/shrink action.</summary>
        public float diseaseScaleFactorMultiplier = 0.1f;

        #endregion

        #region Public Events

        /// <summary>Fired when a valid disease projection is established.</summary>
        public Action<Renderer> OnDiseaseProjected;

        /// <summary>Fired when a raycast miss or distortion check invalidates the projection.</summary>
        public Action<Renderer> OnDiseaseUnprojected;

        #endregion

        #region Debug Fields

        [Header("Debug Options")]
        public bool drawProjectionLines    = false;
        public bool drawProjectionVertices = false;
        public bool drawCollisionPoints    = false;
        public bool drawBounds             = false;
        public float gizmosScale           = 0.001f;

        #endregion

        #region Private — Injected Dependencies

        // Provided via Initialize(); replaces the former ModelEditorManager singleton grab.
        private ISplineModelGenerator    _modelGenerator;
        private IModelEditorUIController _uiController;

        #endregion

        #region Private — Welded Model References (provided via Init())

        private SkinnedMeshRenderer _liWeldedModelRenderer;
        private MeshCollider        _liWeldedModelCollider;

        #endregion

        #region Private — Projection Runtime State

        private MeshData            _projectionSourceMeshData;
        private SkinnedMeshRenderer _projectedDiseaseSmr;
        private Camera              _projectionCamera;
        private Mesh                _projectedDiseaseMesh;
        private Bounds              _baseProjectionMeshBounds;
        private Disease             _currentDisease;
        private bool                _projectionEnabled = false;

        // Projection working arrays — sized once per SetUp() call.
        private Vector3[]       _hitPoints;
        private Vector3[]       _projectedMeshPositions;
        private RaycastHit[]    _raycastHits;
        private RaycastCommand[] _raycastCommands;

        // Intestine hit-triangle tracking — cleared before each projection pass.
        private HashSet<Vector3> _hitTrianglesVertices;
        private HashSet<int>     _hitTrianglesIdxs;

        // Blendshape scale factor read once per Init() from the model generator.
        private float        _blendshapeGlobalWidthFactor = 0.5f;

        // Cached section bounds for location detection.
        private List<Bounds> _intestineSectionsBounds;

        #endregion

        #region Private — Mesh Merger

        // One merger instance per Init() session; its working buffers are reused across Merge() calls.
        private DiseaseMeshMerger _meshMerger;

        #endregion

        #region Private — Job System NativeArrays

        private NativeArray<Vector3>    _projectedMeshPositionsNA;
        private NativeArray<RaycastHit> _raycastHitsNA;
        private NativeArray<RaycastCommand> _raycastCommandsNA;
        private NativeArray<Vector3>    _liVerticesPosNA;
        private NativeArray<int>        _liTrianglesNA;

        #endregion

        #region Private — Undo/Redo History

        [SerializeField] private List<ModelRecord> _modelRecords    = new List<ModelRecord>();
        [SerializeField] private int               _modelRecordIdx  = 0;

        #endregion

        #region Private — Location Detection

        // Maps cumulative section-index upper bounds to anatomical locations.
        // Evaluated in order; first range whose MaxSectionIndex >= location wins.
        private static readonly (int MaxSectionIndex, IntestineLocation Location)[] s_locationRanges =
        {
            (4,           IntestineLocation.Rectum),
            (9,           IntestineLocation.RectumSigmoid),
            (18,          IntestineLocation.Descending),
            (20,          IntestineLocation.SplenicFlexure),
            (35,          IntestineLocation.Transverse),
            (37,          IntestineLocation.HepaticFlexure),
            (40,          IntestineLocation.Ascending),
            (int.MaxValue, IntestineLocation.Cecum),
        };

        #endregion

        #region Unity Lifecycle

        private void OnEnable()  => _projectedDiseaseObject.SetActive(true);
        private void OnDisable() => _projectedDiseaseObject.SetActive(false);

        private void Start()
        {
            _projectionCamera    = GetComponentInChildren<Camera>(true);
            _projectedDiseaseSmr = _projectedDiseaseObject.GetComponent<SkinnedMeshRenderer>();
        }

        private void Update()
        {
            if (drawProjectionLines) DrawProjectionLines();
        }

        private void FixedUpdate()
        {
            if (_projectionEnabled && _projectionCamera.transform.hasChanged)
                UpdateProjectedMesh();
        }

        private void OnDestroy()
        {
            DisposeNativeContainers();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Injects the dependencies this controller requires.
        /// Must be called once before any other method, typically from
        /// <see cref="EditionModes.DiseaseEditionModeController"/>'s constructor.
        /// </summary>
        /// <param name="modelGenerator">Used to read global blendshape factor and section bounds.</param>
        /// <param name="uiController">Used to toggle disease-mode tool interactability.</param>
        public void Initialize(ISplineModelGenerator modelGenerator, IModelEditorUIController uiController)
        {
            _modelGenerator = modelGenerator;
            _uiController   = uiController;
        }

        /// <summary>
        /// Prepares the controller for a new disease editing session on the given welded model.
        /// Rebuilds hit-tracking sets, NativeArrays for the mesh job system, and
        /// takes the first undo/redo snapshot of the pristine welded model.
        /// </summary>
        /// <param name="weldedRenderer">SkinnedMeshRenderer of the current welded intestine model.</param>
        /// <param name="weldedCollider">MeshCollider used for raycast targets on the welded model.</param>
        public void Init(SkinnedMeshRenderer weldedRenderer, MeshCollider weldedCollider)
        {
            _liWeldedModelRenderer = weldedRenderer;
            _liWeldedModelCollider = weldedCollider;

            _hitTrianglesVertices = new HashSet<Vector3>();
            _hitTrianglesIdxs     = new HashSet<int>();

            RebuildCollidedMeshNativeArrays();

            _meshMerger = new DiseaseMeshMerger();

            _modelRecords    = new List<ModelRecord>();
            _modelRecordIdx  = 0;
            CreateModelRecord();

            _blendshapeGlobalWidthFactor = _modelGenerator.GetBlendshapeGlobalWidthFactor();
            _intestineSectionsBounds     = _modelGenerator.GetIntestineSectionsBounds();
        }

        /// <summary>
        /// Configures the controller to project a specific disease mesh.
        /// Allocates per-vertex Job arrays sized to the source mesh vertex count.
        /// Must be called after <see cref="Init"/>.
        /// </summary>
        /// <param name="disease">Disease asset whose mesh and materials will be projected.</param>
        public void SetUp(Disease disease)
        {
            var sourceMesh = disease.Mesh;
#if UNITY_EDITOR
            Debug.Log(sourceMesh == null
                ? "Disease mesh is null"
                : $"Disease mesh: {sourceMesh.name}, raw bounds size={sourceMesh.bounds.size}");
#endif

            // Compute initial transform for the source mesh data in world space.
            var tr  = Vector3.Cross(_translation, Vector3.forward) + transform.position;
            var rot = Quaternion.Euler(_rotation + _projectionCamera.transform.rotation.eulerAngles);
            var sc  = _scale * _blendshapeGlobalWidthFactor;
#if UNITY_EDITOR
            Debug.Log($"Translation: {tr}, Rotation: {rot.eulerAngles}, Scale: {sc}");
#endif

            _projectionSourceMeshData = new MeshData(
                sourceMesh, tr, rot, sc,
                disease.Materials.ToList(),
                false);

            _projectedDiseaseMesh = new Mesh();
            MeshUtils.CopyMesh(_projectedDiseaseMesh, _projectionSourceMeshData.OriginalMesh);
            _currentDisease = disease;

            _projectionSourceMeshData.BuildData();
            UpdateDiseaseProjectionBaseBounds();

            // (Re-)allocate per-vertex Job arrays for the new mesh vertex count.
            int vertexCount = _projectionSourceMeshData.VertexCount;
            _hitPoints              = new Vector3[vertexCount];
            _projectedMeshPositions = new Vector3[vertexCount];
            _raycastHits            = new RaycastHit[vertexCount];
            _raycastCommands        = new RaycastCommand[vertexCount];

            if (_projectedMeshPositionsNA.IsCreated) _projectedMeshPositionsNA.Dispose();
            _projectedMeshPositionsNA = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);

            if (_raycastHitsNA.IsCreated) _raycastHitsNA.Dispose();
            _raycastHitsNA = new NativeArray<RaycastHit>(vertexCount, Allocator.Persistent);

            if (_raycastCommandsNA.IsCreated) _raycastCommandsNA.Dispose();
            _raycastCommandsNA = new NativeArray<RaycastCommand>(vertexCount, Allocator.Persistent);

            _projectionCamera.transform.hasChanged = true; // force re-projection on next FixedUpdate
            _projectedDiseaseSmr.sharedMaterials   = disease.Materials;
            _projectionEnabled                     = true;
        }

        /// <summary>
        /// Resets projection state between disease selections.
        /// Does not release NativeArrays — those are managed by Init()/OnDestroy().
        /// </summary>
        public void Reset()
        {
            diseaseScaleFactor          = Vector3.one;
            _projectionEnabled          = false;
            _projectedDiseaseSmr.sharedMesh = null;
        }

        #endregion

        #region Projection

        /// <summary>
        /// Forces a re-projection of the current disease mesh.
        /// Called by FixedUpdate on camera movement and manually after scale changes.
        /// </summary>
        /// <param name="upscaleOperation">
        /// True when called after an enlarge action; enables the Shrink button even when
        /// projection is distorted so the user can undo the scale-up.
        /// </param>
        public void UpdateProjectedMesh(bool upscaleOperation = false)
        {
            // Lock camera roll to zero so projection direction stays predictable.
            var rotEuler = _projectionCamera.transform.rotation.eulerAngles;
            _projectionCamera.transform.rotation = Quaternion.Euler(new Vector3(rotEuler.x, rotEuler.y, 0));

            _projectionSourceMeshData.UpdateBasicData(
                Vector3.Cross(_translation, Vector3.forward) + transform.position,
                Quaternion.Euler(_rotation + _projectionCamera.transform.rotation.eulerAngles),
                Vector3.Scale(_scale * _blendshapeGlobalWidthFactor, diseaseScaleFactor));

            _projectionCamera.transform.hasChanged = false;

#if UNITY_EDITOR
            var controllerPos = transform.position;
            var cameraPos     = _projectionCamera.transform.position;
            if (controllerPos != cameraPos)
                Debug.Log($"[DiseasePlacement] Position mismatch — controller: {controllerPos}, camera: {cameraPos}, delta: {cameraPos - controllerPos}");
#endif

            ProjectMesh(upscaleOperation);
        }

        /// <summary>
        /// Refreshes the base bounds used for distortion ratio checking.
        /// Call after a scale change before the next distortion check.
        /// </summary>
        public void UpdateDiseaseProjectionBaseBounds()
        {
            _baseProjectionMeshBounds = _projectionSourceMeshData.GetTransformedMesh().bounds;
        }

        /// <summary>
        /// Performs batch raycasting from each source-mesh vertex toward the intestine surface,
        /// projects those vertices onto the hit points, and updates the displayed projected mesh.
        /// Also collects hit triangle vertices for subsequent <see cref="Merge"/> calls.
        /// </summary>
        private void ProjectMesh(bool upscaleOperation = false)
        {
            _uiController.SetDiseaseModeToolsControls(UIController.DiseaseTool.All, false);
            _hitTrianglesVertices.Clear();
            _hitTrianglesIdxs.Clear();

            var positions = VertexUtils.GetPositionsArray(_projectionSourceMeshData.Vertices);
            _projectedMeshPositionsNA.CopyFrom(positions);

            var projectionCameraForward = _projectionCamera.transform.forward;
            var rayDirection            = projectionCameraForward * _projectionDistance;

            // Build one RaycastCommand per source vertex.
            for (int i = 0; i < _projectionSourceMeshData.VertexCount; i++)
            {
                _raycastCommands[i] = new RaycastCommand(
                    positions[i], rayDirection.normalized,
                    new QueryParameters(Physics.DefaultRaycastLayers, hitBackfaces: true),
                    _projectionDistance);
            }
            _raycastCommandsNA.CopyFrom(_raycastCommands);

            // Schedule batch raycasts on the Job system.
            JobHandle raycastHandle = RaycastCommand.ScheduleBatch(
                _raycastCommandsNA, _raycastHitsNA, _raycastBatch);
            raycastHandle.Complete();
            _raycastHitsNA.CopyTo(_raycastHits);

            // Validate hits: store hit points and triangle indices only for rays that connected.
            // Missed rays (collider == null) leave triangleIndex as -1; exclude those from the
            // HashSet so the downstream job never receives an invalid triangle index.
            bool missedRay = false;
            int  hitCount  = _raycastHits.Length;
            for (int i = 0; i < hitCount; i++)
            {
                _hitPoints[i] = _raycastHits[i].point;

                if (_raycastHits[i].collider == null)
                {
                    if (!missedRay)
                    {
                        Debug.LogWarning($"[DiseasePlacement] Raycast from vertex {i} missed the organ surface; treating as unprojected.");
                        missedRay = true;
                    }
                }
                else
                {
                    _hitTrianglesIdxs.Add(_raycastHits[i].triangleIndex);
                }
            }

            if (missedRay)
            {
                OnDiseaseUnprojected?.Invoke(_projectedDiseaseSmr);
                return;
            }

            // Project each source vertex onto its hit surface point.
            var projectJob = new ProjectVerticesJob
            {
                raycastHits              = _raycastHitsNA,
                projectedMeshVerticesPos = _projectedMeshPositionsNA,
                projectionCameraForward  = -projectionCameraForward,
                projectionSourcePosition = transform.position,
                heightOffset             = _heightOffset
            };
            projectJob.Schedule(positions.Length, _batchPool).Complete();
            _projectedMeshPositionsNA.CopyTo(_projectedMeshPositions);

            _projectedDiseaseMesh.vertices = _projectedMeshPositions;
            _projectedDiseaseMesh.RecalculateBounds();

            // Distortion check: compare the magnitude of the projected bounds against the base.
            // If the projected mesh ballooned (e.g. rays wrapped around a convex surface),
            // treat the result as unprojected rather than merging a deformed disease.
            float distortionRatio = _projectedDiseaseMesh.bounds.size.magnitude /
                                    _baseProjectionMeshBounds.size.magnitude;
            bool distorted = distortionRatio > _maxProjectionDistortionMagnitudeRatio;

#if UNITY_EDITOR
            Debug.Log($"[DiseasePlacement] Distortion ratio: {distortionRatio:F3} (threshold: {_maxProjectionDistortionMagnitudeRatio}) → {(distorted ? "DISTORTED" : "OK")}");
#endif

            if (distorted)
            {
                OnDiseaseUnprojected?.Invoke(_projectedDiseaseSmr);

                // Still allow shrinking if distortion was triggered immediately after an enlarge,
                // so the user can scale back down without being completely locked out.
                if (upscaleOperation)
                    _uiController.SetDiseaseModeToolsControls(UIController.DiseaseTool.Shrink, true);
            }
            else
            {
                OnDiseaseProjected?.Invoke(_projectedDiseaseSmr);
                _uiController.SetDiseaseModeToolsControls(UIController.DiseaseTool.All, true);
            }

            _projectedDiseaseMesh.RecalculateNormals();
            _projectedDiseaseSmr.sharedMesh  = _projectedDiseaseMesh;
            _projectedDiseaseSmr.localBounds = _projectedDiseaseSmr.sharedMesh.bounds;

            // Collect the world-space vertices of every hit triangle so Merge() can crop the
            // intestine to exactly the region covered by the disease projection.
            // hitIdxs is clean at this point (no -1 entries; missed rays were filtered above).
            NativeArray<int>     triHitIdxsNA = new(_hitTrianglesIdxs.Count, Allocator.TempJob);
            triHitIdxsNA.CopyFrom(_hitTrianglesIdxs.ToArray());

            // Pre-size the output: each hit triangle contributes exactly 3 vertices, and each
            // parallel worker writes to a disjoint slice [index*3 … index*3+2].
            NativeArray<Vector3> hitVertsNA = new(
                _hitTrianglesIdxs.Count * 3, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);

            var getHitVertsJob = new GetHitTriangleVertsJob
            {
                vertices  = _liVerticesPosNA,
                triangles = _liTrianglesNA,
                hitIdxs   = triHitIdxsNA,
                hitVerts  = hitVertsNA
            };
            getHitVertsJob.Schedule(_hitTrianglesIdxs.Count, _batchPool).Complete();

            int vertsCount = hitVertsNA.Length;
            for (int i = 0; i < vertsCount; i++)
                _hitTrianglesVertices.Add(hitVertsNA[i]);

            triHitIdxsNA.Dispose();
            hitVertsNA.Dispose();
        }

        #endregion

        #region Disease Location Detection

        /// <summary>
        /// Determines which intestinal segment the projected disease overlaps by testing
        /// projected mesh vertices against each section's bounding box.
        /// Falls back to <see cref="IntestineLocation.Rectum"/> if no section matches.
        /// </summary>
        public IntestineLocation GetDiseasePlacementLocation()
        {
            var vertices      = _projectedDiseaseSmr.sharedMesh.vertices;
            var tempLocations = new HashSet<int>();

            // Refresh bounds in case the model was updated since last Init().
            _intestineSectionsBounds = _modelGenerator.GetIntestineSectionsBounds();

            for (int j = 0; j < _intestineSectionsBounds.Count; j++)
            {
                foreach (var vertex in vertices)
                {
                    if (_intestineSectionsBounds[j].Contains(vertex))
                    {
                        tempLocations.Add(j);
                        break; // One vertex match per section is sufficient.
                    }
                }
            }

            foreach (var sectionIdx in tempLocations)
            {
                foreach (var (maxIdx, location) in s_locationRanges)
                    if (sectionIdx <= maxIdx) return location;
            }

            return IntestineLocation.Rectum;
        }

        /// <summary>
        /// Returns localized string options for all <see cref="IntestineLocation"/> values,
        /// looked up in the "IntestineStructureTable" localization table.
        /// </summary>
        public LocalizedString[] GetDiseasePlacementLocationOptions()
        {
            var values  = Enum.GetValues(typeof(IntestineLocation));
            var options = new LocalizedString[values.Length];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = new LocalizedString
                {
                    TableReference      = "IntestineStructureTable",
                    TableEntryReference = Enum.GetName(typeof(IntestineLocation), i)
                };
            }
            return options;
        }

        #endregion

        #region Mesh Merge

        /// <summary>
        /// Merges the projected disease mesh into the welded intestine model at the given location.
        /// Updates the welded renderer, collider, and records the result for undo/redo.
        /// </summary>
        /// <param name="selectedDiseaseLocation">Anatomical location to assign to the disease.</param>
        public void Merge(IntestineLocation selectedDiseaseLocation)
        {
            GetDiseasePlacementLocation();
            _currentDiseaseSize = Vector3.zero;

            if (_useWelding)
            {
                // Delaunay-based welding: crop intestine around hit area and bridge with a new triangle fan.
                _liWeldedModelRenderer.sharedMesh = _meshMerger.Merge(
                    _liWeldedModelRenderer.sharedMesh,
                    _hitTrianglesVertices,
                    _projectedDiseaseSmr.sharedMesh,
                    _projectionSourceMeshData.GetVerticesAtY0(),
                    _projectionCamera.transform.forward);

                // Material slots must align with submesh order from DiseaseMeshMerger:
                // [intestine submeshes] [disease submeshes] [Delaunay bridge].
                var sharedMaterials = new List<Material>();
                sharedMaterials.AddRange(_liWeldedModelRenderer.sharedMaterials);
                sharedMaterials.AddRange(_projectedDiseaseSmr.sharedMaterials);
                sharedMaterials.Add(_delaunayMeshMaterial);
                _liWeldedModelRenderer.sharedMaterials = sharedMaterials.ToArray();
                _liWeldedModelCollider.sharedMesh      = _liWeldedModelRenderer.sharedMesh;
            }
            else
            {
                // Simple combine: transform intestine into a normalized local space, combine,
                // then invert the transform to restore world-space proportions.
#if UNITY_EDITOR
                Debug.Log($"Colon model size: {Vector3.Scale(CalculateMeshWorldSize(_liWeldedModelRenderer), transform.lossyScale)}");
                Debug.Log($"Projected disease size: {Vector3.Scale(CalculateMeshWorldSize(_projectedDiseaseSmr), transform.lossyScale)}");
#endif
                _currentDiseaseSize = Vector3.Scale(CalculateMeshWorldSize(_projectedDiseaseSmr), transform.lossyScale);

                // Round size components to the nearest 0.5 cm.
                _currentDiseaseSize.x = RoundToNearestHalf(_currentDiseaseSize.x);
                _currentDiseaseSize.y = RoundToNearestHalf(_currentDiseaseSize.y);
                _currentDiseaseSize.z = RoundToNearestHalf(_currentDiseaseSize.z);

                var scaledIntestine = new MeshData(
                    _liWeldedModelRenderer.sharedMesh,
                    _liWeldedModelRenderer.transform.position,
                    Quaternion.identity,
                    transform.lossyScale,
                    false);
                scaledIntestine.BuildData();

                var combined = MeshUtils.CombineMeshes(
                    new List<Mesh> { scaledIntestine.GetTransformedMesh(), _projectedDiseaseSmr.sharedMesh },
                    _useSubmeshes);

                var restored = new MeshData(
                    combined,
                    _liWeldedModelRenderer.transform.position,
                    Quaternion.identity,
                    transform.lossyScale,
                    false);
                restored.InversedBuildData();

                _liWeldedModelRenderer.sharedMesh = restored.GetTransformedMesh();

                var sharedMaterials = new List<Material>();
                sharedMaterials.AddRange(_liWeldedModelRenderer.sharedMaterials);
                sharedMaterials.AddRange(_projectedDiseaseSmr.sharedMaterials);
                _liWeldedModelRenderer.sharedMaterials = sharedMaterials.ToArray();
                _liWeldedModelCollider.sharedMesh      = _liWeldedModelRenderer.sharedMesh;
            }

            _currentDisease.Location     = selectedDiseaseLocation;
            _currentDisease.SubmeshIndex = _liWeldedModelRenderer.sharedMesh.subMeshCount - 1;

            CreateModelRecord();
            RebuildCollidedMeshNativeArrays();
        }

        // Tracks the size of the most recently merged disease for recording in CreateModelRecord().
        private Vector3 _currentDiseaseSize;

        #endregion

        #region Undo / Redo

        /// <inheritdoc/>
        public void Undo()
        {
            if (_modelRecordIdx - 1 >= 0) LoadModelRecord(--_modelRecordIdx);
        }

        /// <inheritdoc/>
        public void Redo()
        {
            if (_modelRecordIdx + 1 <= _modelRecords.Count - 1) LoadModelRecord(++_modelRecordIdx);
        }

        /// <summary>
        /// Returns the disease list for the current undo/redo snapshot.
        /// Used by <see cref="ModelEditorManager"/> when saving the model.
        /// </summary>
        public List<Disease> GetCurrentDiseases() => _modelRecords[_modelRecordIdx].diseases;

        /// <summary>
        /// Captures a new undo snapshot.
        /// When recording a disease placement (recordIdx > 0), clones the disease and
        /// carries forward all diseases from the previous record.
        /// </summary>
        private void CreateModelRecord()
        {
            // Truncate any redo history beyond the current position.
            if (_modelRecords.Count > _modelRecordIdx + 1)
                _modelRecords = _modelRecords.Take(_modelRecordIdx + 1).ToList();

            _modelRecords.Add(new ModelRecord(_liWeldedModelRenderer));
            _modelRecordIdx = _modelRecords.Count - 1;

            // Index 0 is the pristine welded mesh snapshot (no diseases placed yet).
            if (_modelRecords.Count != 1)
            {
                Disease clone;
                switch (_currentDisease)
                {
                    case Polyp p:
                        p.Size = _currentDiseaseSize;
                        clone  = new Polyp(p);
                        break;
                    default:
                        clone = _currentDisease.Clone();
                        break;
                }

                var prevDiseases = new List<Disease>(_modelRecords[_modelRecordIdx - 1].diseases) { clone };
                _modelRecords[_modelRecordIdx].diseases = prevDiseases;
            }
            else
            {
                // Seed the first record with whatever diseases the model already has.
                _modelRecords[_modelRecordIdx].diseases = new List<Disease>();
            }
        }

        /// <summary>Restores the welded mesh and materials from a stored snapshot.</summary>
        private void LoadModelRecord(int targetIdx)
        {
            var record = _modelRecords[targetIdx];
            _liWeldedModelRenderer.sharedMesh      = record.mesh;
            _liWeldedModelRenderer.sharedMaterials = record.materials;
            _liWeldedModelCollider.sharedMesh      = record.mesh;
        }

        /// <summary>
        /// A snapshot of the welded model mesh + materials + disease list at a point in time.
        /// </summary>
        [Serializable]
        public class ModelRecord
        {
            public Mesh          mesh;
            public Material[]    materials;
            public List<Disease> diseases;

            public ModelRecord(SkinnedMeshRenderer renderer)
            {
                mesh      = new Mesh();
                MeshUtils.CopyMesh(mesh, renderer.sharedMesh);
                materials = renderer.sharedMaterials;
                diseases  = new List<Disease>();
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// (Re-)populates the persistent NativeArrays with the current welded mesh vertices and triangles.
        /// Must be called after Init() and after each Merge() because the mesh topology changes.
        /// </summary>
        private void RebuildCollidedMeshNativeArrays()
        {
            if (_liVerticesPosNA.IsCreated) _liVerticesPosNA.Dispose();
            if (_liTrianglesNA.IsCreated)   _liTrianglesNA.Dispose();

            var mesh = _liWeldedModelRenderer.sharedMesh;
            _liVerticesPosNA = new NativeArray<Vector3>(mesh.vertices.Length, Allocator.Persistent);
            _liVerticesPosNA.CopyFrom(mesh.vertices);
            _liTrianglesNA = new NativeArray<int>(mesh.triangles.Length, Allocator.Persistent);
            _liTrianglesNA.CopyFrom(mesh.triangles);
        }

        /// <summary>Disposes all persistent NativeArrays to avoid memory leaks.</summary>
        private void DisposeNativeContainers()
        {
            if (_liVerticesPosNA.IsCreated)         _liVerticesPosNA.Dispose();
            if (_liTrianglesNA.IsCreated)            _liTrianglesNA.Dispose();
            if (_projectedMeshPositionsNA.IsCreated) _projectedMeshPositionsNA.Dispose();
            if (_raycastHitsNA.IsCreated)            _raycastHitsNA.Dispose();
            if (_raycastCommandsNA.IsCreated)        _raycastCommandsNA.Dispose();
        }

        /// <summary>
        /// Computes the axis-aligned bounding box size of a SkinnedMeshRenderer in centimetres,
        /// accounting for the full world-space transform of all eight AABB corners.
        /// </summary>
        private Vector3 CalculateMeshWorldSize(SkinnedMeshRenderer smr)
        {
            Mesh mesh = smr.sharedMesh;
            if (mesh == null)
            {
                Debug.LogError("SkinnedMeshRenderer has no mesh.");
                return Vector3.zero;
            }

            Bounds    b = mesh.bounds;
            Matrix4x4 m = smr.transform.localToWorldMatrix;

            // Transform all eight AABB corners to world space.
            Vector3[] corners =
            {
                m.MultiplyPoint3x4(new Vector3(b.min.x, b.min.y, b.min.z)),
                m.MultiplyPoint3x4(new Vector3(b.min.x, b.min.y, b.max.z)),
                m.MultiplyPoint3x4(new Vector3(b.min.x, b.max.y, b.min.z)),
                m.MultiplyPoint3x4(new Vector3(b.min.x, b.max.y, b.max.z)),
                m.MultiplyPoint3x4(new Vector3(b.max.x, b.min.y, b.min.z)),
                m.MultiplyPoint3x4(new Vector3(b.max.x, b.min.y, b.max.z)),
                m.MultiplyPoint3x4(new Vector3(b.max.x, b.max.y, b.min.z)),
                m.MultiplyPoint3x4(new Vector3(b.max.x, b.max.y, b.max.z)),
            };

            Vector3 min = corners[0], max = corners[0];
            foreach (var c in corners)
            {
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, c);
            }

            return (max - min) * 100f; // metres → centimetres
        }

        /// <summary>Rounds a value to the nearest 0.5.</summary>
        private static float RoundToNearestHalf(float value)
        {
            float rounded = Mathf.Round(value * 10f) / 10f; // truncate to one decimal
            return Mathf.Round(rounded * 2f) / 2f;          // snap to 0.5 grid
        }

        #endregion

        #region Burst Jobs

        /// <summary>
        /// Parallel job that projects each source-mesh vertex onto its corresponding
        /// raycast hit point, offsetting slightly along the surface normal to avoid z-fighting.
        /// </summary>
        [BurstCompile]
        private struct ProjectVerticesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<RaycastHit> raycastHits;
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> projectedMeshVerticesPos;
            [ReadOnly] public Vector3 projectionCameraForward;
            [ReadOnly] public Vector3 projectionSourcePosition;
            [ReadOnly] public float   heightOffset;

            public void Execute(int index)
            {
                // Compute how far this vertex sits above the hit surface along the camera axis,
                // then offset the hit point by that amount to preserve relative depth.
                float height = Vector3.Dot(
                    projectedMeshVerticesPos[index] - projectionSourcePosition,
                    projectionCameraForward) + heightOffset;

                projectedMeshVerticesPos[index] =
                    raycastHits[index].point + projectionCameraForward * height;
            }
        }

        /// <summary>
        /// Parallel job that writes the three world-space vertex positions for each hit triangle
        /// into a pre-sized output array. Each worker writes to a disjoint slice [index*3 … index*3+2],
        /// making concurrent writes safe without a mutex.
        /// </summary>
        /// <remarks>
        /// The caller must guarantee that <c>hitIdxs</c> contains only valid triangle indices
        /// (no −1 entries from missed rays) before scheduling this job.
        /// </remarks>
        [BurstCompile]
        public struct GetHitTriangleVertsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> vertices;
            [ReadOnly] public NativeArray<int>     triangles;
            [ReadOnly] public NativeArray<int>     hitIdxs;
            // Pre-sized to hitIdxs.Length * 3; each Execute() writes exactly 3 elements
            // at [index*3], [index*3+1], [index*3+2] — disjoint across workers.
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> hitVerts;

            public void Execute(int index)
            {
                int triBase = hitIdxs[index] * 3;
                int outBase = index * 3;
                hitVerts[outBase + 0] = vertices[triangles[triBase + 0]];
                hitVerts[outBase + 1] = vertices[triangles[triBase + 1]];
                hitVerts[outBase + 2] = vertices[triangles[triBase + 2]];
            }
        }

        #endregion

        #region Debug / Gizmos

        private void DrawProjectionLines()
        {
            var lineDir = _projectionCamera.transform.forward * _projectionDistance;
            var verts   = _projectionSourceMeshData.Vertices;
            for (int i = 0; i < verts.Length; i++)
                Debug.DrawRay(verts[i].position, lineDir);
        }

        private void OnDrawGizmos()
        {
            if (!_projectionEnabled) return;

            if (drawProjectionVertices)
            {
                Gizmos.color = Color.white;
                foreach (var v in _projectionSourceMeshData.Vertices)
                    Gizmos.DrawSphere(v.position, gizmosScale);
            }

            if (drawCollisionPoints)
            {
                Gizmos.color = Color.red;
                foreach (var hit in _hitPoints)
                    Gizmos.DrawSphere(hit, gizmosScale);
            }

            if (drawBounds && _intestineSectionsBounds != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var b in _intestineSectionsBounds)
                    Gizmos.DrawWireCube(b.center, b.size);
            }
        }

        #endregion
    }
}
