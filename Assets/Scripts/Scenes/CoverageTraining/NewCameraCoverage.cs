// ============================================================================
// NewCameraCoverage.cs
//
// Tracks which triangles of a large intestine mesh have been seen by the
// endoscope camera during a Coverage Training session. Produces a live
// "explored mesh" overlay coloured by how long each vertex has been
// under observation, and exposes per-section coverage percentages to the HUD.
//
// Key Features:
//   - Circular cone detection model: a vertex is a candidate only when it lies
//     within a configurable world-space cone (focusConeAngle) and distance range
//     (maxViewDistance) from the camera. This models human perceptual focus —
//     the endoscope light falls off toward the periphery, so only the central
//     zone is considered reliably seen. The cone is circular, matching the
//     endoscope's round FOV, rather than the rectangular frustum of a camera.
//   - Normal facing check (visionAngle): surface must face the camera within a
//     threshold angle — grazing or back-facing surfaces are excluded.
//   - Occlusion confirmation via batch RaycastCommand (rejects triangles
//     blocked by other geometry before marking them as explored).
//   - Time-based observation accumulation: each confirmed vertex accumulates
//     view time (seconds) continuously while in sight. Colour saturates at
//     maxObservationTime. A sqrt bias and a full 256-entry gradient LUT keep
//     early observation visually distinct from zero across the full range.
//   - Source mesh data refreshed only on rotation-handle release, not
//     every frame, via NotifyTransformChanged() / meshDataHasChanged flag.
//   - All NativeArrays / NativeLists allocated once at Start and reused
//     for the lifetime of the component — zero per-frame managed alloc.
//   - Explored mesh update is rate-limited by exploredMeshUpdateDelay to
//     avoid submitting mesh data to the GPU every fixed-update tick.
//
// Dependencies:
//   - g4 (geometry4Sharp): Index3i triangle index struct.
//   - GeometryUtils.MeshUtils / MeshData: mesh copy and world-space transform.
//   - CommonUtils: Color ↔ float4 conversion helpers.
//   - CoverageTrainingManager: calls Initialize (via scene wiring) and
//     subscribes NotifyTransformChanged to OnRotationReleased.
//
// Usage:
//   1. Attach to any GameObject in the CoverageTraining scene.
//   2. Assign all serialized fields in the Inspector.
//   3. Call SetInsideCameraCoverageViewActive(true) to begin tracking.
//   4. Call Reset() when restarting the training session.
// ============================================================================

using g4;
using GeometryUtils;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.Mesh;

public class NewCameraCoverage : MonoBehaviour
{
    #region Public Fields

    [Tooltip("Skinned mesh renderer of the source intestine model.")]
    public SkinnedMeshRenderer SourceMeshRenderer;

    [Tooltip("Skinned mesh renderer that displays the explored overlay mesh.")]
    public SkinnedMeshRenderer exploredMeshRenderer;

    [Tooltip("Camera representing the endoscope eye view.")]
    public Camera eyeViewCamera;

    [Tooltip("Material applied to the explored overlay mesh.")]
    public Material transparentMaterial;

    [Tooltip("Half-angle (degrees) of the circular focus cone. Only vertices within this cone " +
             "of the camera forward axis are considered seen. Models the endoscope light falloff " +
             "and human perceptual focus toward the centre of the view.")]
    public float focusConeAngle = 30f;

    [Tooltip("Half-angle (degrees) of the cone within which surface normals must point toward the camera. " +
             "Excludes grazing and back-facing surfaces.")]
    public float visionAngle = 65f;

    [Tooltip("Maximum world-space distance (metres) from the camera at which a vertex can be detected.")]
    public float maxViewDistance = 0.15f;

    [Tooltip("Seconds of continuous observation required for a vertex to reach full gradient colour.")]
    public float maxObservationTime = 2f;

    [Tooltip("Minimum seconds between explored mesh uploads to the GPU.")]
    [Header("Explored mesh update delay in seconds")]
    public float exploredMeshUpdateDelay = 1f;

    /// <summary>Total percentage of mesh triangles marked as explored.</summary>
    public float coveragePercentage { get; private set; }

    /// <summary>Explored percentage for the rectum section.</summary>
    public float rectumCoveragePercentage { get; private set; }

    /// <summary>Explored percentage for the sigmoid colon section.</summary>
    public float sigmoidCoveragePercentage { get; private set; }

    /// <summary>Explored percentage for the descending colon section.</summary>
    public float descendingColonCoveragePercentage { get; private set; }

    /// <summary>Explored percentage for the transverse colon section.</summary>
    public float transverseColonCoveragePercentage { get; private set; }

    /// <summary>Explored percentage for the ascending colon section.</summary>
    public float ascendingColonCoveragePercentage { get; private set; }

    /// <summary>Explored percentage for the cecum section.</summary>
    public float cecumCoveragePercentage { get; private set; }

    [Tooltip("Gradient applied to explored vertices: left = first glimpse, right = fully observed.")]
    public Gradient exploredVerticesColorGradient;

    #endregion

    #region Private Fields

    // World-space transformed version of the source mesh, rebuilt on rotation release.
    private Mesh sourceMesh;

    // Overlay mesh written by UpdateExploredMeshRenderer; lives on exploredMeshRenderer.
    private Mesh exploredMesh;

    // Plain triangle index list written directly to exploredMesh.SetTriangles.
    private List<int> _exploredTriangleIndices;

    private Transform eyeViewCameraTransform;

    private int vertexCount;
    private int triangleCount;

    // Guards the rate-limited GPU upload in UpdateExploredMeshRenderer.
    private bool exploredMeshRendererNeedsUpdate = false;
    private float lastExploredMeshRendererUpdateTime = 0f;

    // ── Persistent NativeArrays — allocated once in InitializePersistentNativeContainers ──

    private NativeArray<Vector3> verticesNA;
    private NativeArray<int>     exploredVertexIndexesNA;  // 1 = visited, 0 = not visited
    private NativeArray<Vector3> normalsNA;
    private NativeArray<Index3i> trianglesNA;
    private NativeArray<float>   vertexObservationNA;       // accumulated observation seconds per vertex
    private NativeArray<int>     _vertexSeenThisTickNA;     // 1 = vertex confirmed visible this tick; self-reset by AccumulateObservationTimeJob
    private NativeArray<int>     areTrianglesExploredNA;    // parallel to trianglesNA; 1 = added to mesh
    private NativeArray<float4>  float4ColorsNA;

    // 256-entry gradient LUT baked at Start — Burst cannot access managed Gradient objects.
    private NativeArray<float4>  _gradientLutNA;

    // Working lists reused each coverage tick — cleared, not reallocated.
    private NativeList<Index3i> _newSeenTrianglesList;
    private NativeList<int>     _newSeenTriangleIndexesList;
    private NativeList<Index3i> _newValidDetectedTrianglesList;

    // Persistent raycast buffers — sliced via GetSubArray to match seen-triangle count.
    private NativeArray<RaycastCommand> _raycastCommandsNA;
    private NativeArray<Vector3>        _triangleCentersNA;
    private NativeArray<RaycastHit>     _raycastHitsNA;

    // Pre-computed vertex count per intestinal section (Rectum…Cecum).
    private NativeArray<int>   _coverageRanges;
    private NativeArray<float> _coveragePercentagesNA;

    // Cached managed arrays — avoid per ComputeVertexColors call GC pressure.
    private float4[] _cachedFloat4Colors;
    private Color[]  _cachedColors;

    // Managed zero-fill arrays used during Reset to avoid re-allocation.
    private int[]   isTriangledExplored;
    private float[] _zeroObservationArray;
    private int[]   _zeroVertexArray;

    /// <summary>
    /// Set by NotifyTransformChanged() when the model is rotated.
    /// Triggers a vertex/normal refresh at the start of the next coverage tick.
    /// </summary>
    public bool meshDataHasChanged = false;

    /// <summary>True while coverage tracking is active.</summary>
    public bool coverageEnabled = false;

    /// <summary>True while the explored overlay mesh is rendered.</summary>
    public bool isExploredMeshEnabled = false;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        eyeViewCameraTransform = eyeViewCamera.transform;

        // Bake the full gradient into a 256-entry LUT — Burst jobs cannot access managed Gradient objects.
        _gradientLutNA = new NativeArray<float4>(256, Allocator.Persistent);
        for (int i = 0; i < 256; i++)
            _gradientLutNA[i] = CommonUtils.ColorToFloat4(exploredVerticesColorGradient.Evaluate(i / 255f));

        exploredMesh = exploredMeshRenderer.sharedMesh = new Mesh();

        SetSourceMeshData();

        isTriangledExplored   = new int[triangleCount];
        _zeroObservationArray = new float[vertexCount];
        _zeroVertexArray      = new int[vertexCount];

        InitializePersistentNativeContainers();

        _cachedFloat4Colors = new float4[vertexCount];
        _cachedColors       = new Color[vertexCount];

        BuildTriangles();
        SetExploredMeshData();
        SetInsideCameraCoverageViewActive(false);
    }

    private void FixedUpdate()
    {
        if (coverageEnabled)
        {
            UpdateCoverage();
        }

        // Rate-limit mesh uploads — submitting every tick is wasteful.
        if (coverageEnabled && Time.time - lastExploredMeshRendererUpdateTime >= exploredMeshUpdateDelay)
        {
            UpdateExploredMeshRenderer();
            lastExploredMeshRendererUpdateTime = Time.time;
        }
    }

    private void OnDestroy()
    {
        DisposeNativeContainers();
    }

    private void OnDrawGizmos()
    {
        if (eyeViewCameraTransform == null) return;

        var    origin  = eyeViewCameraTransform.position;
        var    forward = eyeViewCameraTransform.forward;
        var    right   = eyeViewCameraTransform.right;
        var    up      = eyeViewCameraTransform.up;
        float  radius  = Mathf.Tan(focusConeAngle * Mathf.Deg2Rad) * maxViewDistance;
        Vector3 tip    = origin + forward * maxViewDistance;

        // Forward axis
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, tip);

        // Four cone edge rays and a circle at the base
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, tip + right * radius);
        Gizmos.DrawLine(origin, tip - right * radius);
        Gizmos.DrawLine(origin, tip + up    * radius);
        Gizmos.DrawLine(origin, tip - up    * radius);
        Gizmos.DrawWireSphere(tip, radius);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Flags that the source mesh's world-space transform has changed (model was rotated).
    /// Vertices and normals are re-read from the renderer at the start of the next coverage tick.
    /// Called by CoverageTrainingManager when the rotation handle is released.
    /// </summary>
    public void NotifyTransformChanged()
    {
        meshDataHasChanged = true;
    }

    /// <summary>
    /// Enables or disables rendering of the explored overlay mesh.
    /// </summary>
    public void SetInsideCameraCoverageViewActive(bool status)
    {
        isExploredMeshEnabled = status;
        exploredMeshRenderer.enabled = status;
    }

    /// <summary>
    /// Resets all coverage state: clears the overlay mesh, zeroes all counters,
    /// and refreshes source mesh data. Safe to call mid-session for a full restart.
    /// </summary>
    public void Reset()
    {
        exploredMesh = exploredMeshRenderer.sharedMesh = new Mesh();

        coveragePercentage              = 0f;
        rectumCoveragePercentage        = 0f;
        sigmoidCoveragePercentage       = 0f;
        descendingColonCoveragePercentage = 0f;
        transverseColonCoveragePercentage = 0f;
        ascendingColonCoveragePercentage  = 0f;
        cecumCoveragePercentage           = 0f;

        SetSourceMeshData();
        PopulatePersistentNativeContainers();
        SetExploredMeshData();

        Array.Clear(isTriangledExplored, 0, triangleCount);
        exploredVertexIndexesNA.CopyFrom(_zeroVertexArray);
        exploredMeshRendererNeedsUpdate = false;
    }

    #endregion

    #region Mesh Data Initialisation

    /// <summary>
    /// Bakes the current world-space transform of the source renderer into <c>sourceMesh</c>
    /// so that vertex positions and normals are in world space for the coverage jobs.
    /// Optionally copies the untransformed mesh onto <c>exploredMesh</c> so both renderers
    /// share the same vertex layout.
    /// </summary>
    private void SetSourceMeshData(bool updateExploredMesh = true)
    {
        var nonTransformedSourceMesh = new Mesh();
        sourceMesh = SourceMeshRenderer.sharedMesh;
        MeshUtils.CopyMesh(nonTransformedSourceMesh, sourceMesh);

        // Apply the renderer's current TRS so all subsequent distance/angle math
        // can work in world space without per-vertex matrix multiplications at runtime.
        var transformableMeshData = new GeometryUtils.MeshData(
            sourceMesh,
            transform.position,
            transform.rotation,
            transform.lossyScale,
            false);
        transformableMeshData.BuildData();
        sourceMesh = transformableMeshData.GetTransformedMesh();

        if (updateExploredMesh)
            MeshUtils.CopyMesh(exploredMesh, nonTransformedSourceMesh);

        vertexCount   = sourceMesh.vertexCount;
        triangleCount = sourceMesh.triangles.Length / 3;
    }

    /// <summary>
    /// Clears the explored triangle list and resets the overlay mesh's triangle buffer.
    /// Called at Start and on Reset.
    /// </summary>
    private void SetExploredMeshData()
    {
        exploredMesh.triangles      = new int[0];
        _exploredTriangleIndices    = new List<int>(triangleCount * 3);
    }

    /// <summary>
    /// Builds the <c>trianglesNA</c> NativeArray from the overlay mesh's triangle buffer.
    /// Must be called after SetSourceMeshData has populated <c>exploredMesh</c>.
    /// </summary>
    private void BuildTriangles()
    {
        var triangleIndices       = exploredMesh.triangles;
        var triangleIndicesLength = triangleIndices.Length;
        for (int i = 0; i < triangleIndicesLength; i += 3)
        {
            trianglesNA[i / 3] = new Index3i(triangleIndices[i], triangleIndices[i + 1], triangleIndices[i + 2]);
        }
    }

    #endregion

    #region Native Container Management

    /// <summary>
    /// Allocates all NativeArrays and NativeLists with Persistent allocator.
    /// Called once in Start; containers survive for the component's entire lifetime.
    /// </summary>
    private void InitializePersistentNativeContainers()
    {
        verticesNA              = new NativeArray<Vector3>(vertexCount,   Allocator.Persistent);
        exploredVertexIndexesNA = new NativeArray<int>(vertexCount,       Allocator.Persistent);
        normalsNA               = new NativeArray<Vector3>(vertexCount,   Allocator.Persistent);
        trianglesNA             = new NativeArray<Index3i>(triangleCount, Allocator.Persistent);
        vertexObservationNA     = new NativeArray<float>(vertexCount,     Allocator.Persistent);
        _vertexSeenThisTickNA   = new NativeArray<int>(vertexCount,       Allocator.Persistent);
        areTrianglesExploredNA  = new NativeArray<int>(triangleCount,     Allocator.Persistent);
        float4ColorsNA          = new NativeArray<float4>(vertexCount,    Allocator.Persistent);

        // Working lists — capacity pre-set to worst case (all triangles seen).
        _newSeenTrianglesList           = new NativeList<Index3i>(triangleCount, Allocator.Persistent);
        _newSeenTriangleIndexesList     = new NativeList<int>(triangleCount,     Allocator.Persistent);
        _newValidDetectedTrianglesList  = new NativeList<Index3i>(triangleCount, Allocator.Persistent);

        // Raycast buffers — sized for worst case; sliced per tick via GetSubArray.
        _raycastCommandsNA = new NativeArray<RaycastCommand>(triangleCount, Allocator.Persistent);
        _triangleCentersNA = new NativeArray<Vector3>(triangleCount,        Allocator.Persistent);
        _raycastHitsNA     = new NativeArray<RaycastHit>(triangleCount,     Allocator.Persistent);

        // Section vertex ranges — fixed ratios of total vertex count.
        // Sections (Rectum, Sigmoid, Descending, Transverse, Ascending, Cecum)
        // correspond to anatomical proportions of the 46-mesh large intestine model.
        int   verticesPerMesh = vertexCount / 46;
        int[] sectionRatios   = { 5, 5, 11, 15, 5, 5 };
        _coverageRanges = new NativeArray<int>(6, Allocator.Persistent);
        for (int i = 0; i < 6; i++) _coverageRanges[i] = sectionRatios[i] * verticesPerMesh;
        _coveragePercentagesNA = new NativeArray<float>(6, Allocator.Persistent);

        PopulatePersistentNativeContainers();
    }

    /// <summary>
    /// Copies the current source mesh vertices and normals into the persistent NativeArrays,
    /// and resets the exploration state arrays. Called at init and after mesh data changes.
    /// </summary>
    private void PopulatePersistentNativeContainers()
    {
        var meshData = AcquireReadOnlyMeshData(sourceMesh)[0];
        meshData.GetVertices(verticesNA);
        meshData.GetNormals(normalsNA);

        areTrianglesExploredNA.CopyFrom(isTriangledExplored);
        vertexObservationNA.CopyFrom(_zeroObservationArray);
        _vertexSeenThisTickNA.CopyFrom(_zeroVertexArray);
        // exploredVertexIndexesNA is zero-initialised on creation; zeroed explicitly in Reset.

        meshDataHasChanged = false;
    }

    /// <summary>Disposes all persistent native containers. Called in OnDestroy.</summary>
    private void DisposeNativeContainers()
    {
        SafeDisposeNativeArray(verticesNA);
        SafeDisposeNativeArray(exploredVertexIndexesNA);
        SafeDisposeNativeArray(normalsNA);
        SafeDisposeNativeArray(trianglesNA);
        SafeDisposeNativeArray(vertexObservationNA);
        SafeDisposeNativeArray(_vertexSeenThisTickNA);
        SafeDisposeNativeArray(areTrianglesExploredNA);

        if (float4ColorsNA.IsCreated)                 float4ColorsNA.Dispose();
        if (_gradientLutNA.IsCreated)                 _gradientLutNA.Dispose();
        if (_newSeenTrianglesList.IsCreated)           _newSeenTrianglesList.Dispose();
        if (_newSeenTriangleIndexesList.IsCreated)     _newSeenTriangleIndexesList.Dispose();
        if (_newValidDetectedTrianglesList.IsCreated)  _newValidDetectedTrianglesList.Dispose();

        SafeDisposeNativeArray(_raycastCommandsNA);
        SafeDisposeNativeArray(_triangleCentersNA);
        SafeDisposeNativeArray(_raycastHitsNA);
        SafeDisposeNativeArray(_coverageRanges);
        SafeDisposeNativeArray(_coveragePercentagesNA);
    }

    private void SafeDisposeNativeArray<T>(NativeArray<T> na) where T : struct
    {
        if (na.IsCreated) na.Dispose();
    }

    #endregion

    #region Coverage Update

    /// <summary>
    /// Main per-tick entry point. Runs the full detection pipeline:
    /// cone cull → occlusion raycast → accumulate observation time → mark explored → update percentages.
    /// Called every FixedUpdate tick while <c>coverageEnabled</c>, regardless of camera movement.
    /// </summary>
    private void UpdateCoverage()
    {
        // Refresh world-space vertex/normal data if the model was rotated since last tick.
        if (meshDataHasChanged)
        {
            SetSourceMeshData(false);
            var meshData = AcquireReadOnlyMeshData(sourceMesh)[0];
            meshData.GetVertices(verticesNA);
            meshData.GetNormals(normalsNA);
            meshDataHasChanged = false;
        }

        _newSeenTrianglesList.Clear();
        _newSeenTriangleIndexesList.Clear();

        // Read public fields fresh each tick so Inspector tweaks take effect immediately.
        var    cameraForward     = eyeViewCameraTransform.forward;
        float3 cameraForwardNorm = new float3(cameraForward.x, cameraForward.y, cameraForward.z);

        // Precompute cos(threshold) — lets jobs use dot product comparisons instead of
        // acos, eliminating transcendental ops from the parallel loop.
        float cosVisionAngle    = math.cos(math.radians(visionAngle));
        float cosFocusConeAngle = math.cos(math.radians(focusConeAngle));

        // Step 1: parallel cone cull + normal check — ALL triangles, explored or not.
        // Explored triangles are still included so their vertices continue accumulating
        // observation time while they remain in the camera cone.
        new FindTrianglesInCameraView
        {
            vertices          = verticesNA,
            normals           = normalsNA,
            triangles         = trianglesNA,
            cameraPosition    = eyeViewCameraTransform.position,
            cameraForwardNorm = cameraForwardNorm,
            cosVisionAngle    = cosVisionAngle,
            cosFocusConeAngle = cosFocusConeAngle,
            maxViewDistance   = maxViewDistance,
            newSeenTriangles       = _newSeenTrianglesList.AsParallelWriter(),
            newSeenTriangleIndexes = _newSeenTriangleIndexesList.AsParallelWriter(),
        }.Schedule(triangleCount, 1024).Complete();

        int seenCount = _newSeenTrianglesList.Length;
        if (seenCount > 0)
        {
            // GetSubArray gives zero-allocation slices into the persistent buffers,
            // sized exactly to the number of candidate triangles this tick.
            var raycastCommandsSlice = _raycastCommandsNA.GetSubArray(0, seenCount);
            var triangleCentersSlice = _triangleCentersNA.GetSubArray(0, seenCount);
            var raycastHitsSlice     = _raycastHitsNA.GetSubArray(0, seenCount);

            // Step 2: build one raycast command per candidate triangle.
            new PopulateRaycastCommandsJob
            {
                newSeenTriangles       = _newSeenTrianglesList.AsArray(),
                vertices               = verticesNA,
                raycastCommands        = raycastCommandsSlice,
                triangleCenters        = triangleCentersSlice,
                eyeViewCameraPosition  = eyeViewCameraTransform.position
            }.Schedule(seenCount, 128).Complete();

            // Step 3: fire all raycasts in a single batched call (GPU-accelerated path).
            RaycastCommand.ScheduleBatch(raycastCommandsSlice, raycastHitsSlice, 128).Complete();

            // Step 4: validate hits — flag confirmed vertices as seen this tick.
            // For newly confirmed triangles only: add to the explored mesh list.
            _newValidDetectedTrianglesList.Clear();
            new ProcessRaycastHitsJob
            {
                raycastHitsNA               = raycastHitsSlice,
                triangleCentersNA           = triangleCentersSlice,
                newSeenTriangleIndexesArray = _newSeenTriangleIndexesList.AsArray(),
                newSeenTrianglesArray       = _newSeenTrianglesList.AsArray(),
                areTrianglesExploredNA      = areTrianglesExploredNA,
                vertexSeenThisTick          = _vertexSeenThisTickNA,
                newValidDetectedTriangles   = _newValidDetectedTrianglesList.AsParallelWriter()
            }.Schedule(seenCount, 128).Complete();

            // Step 5: accumulate dt once per flagged vertex — avoids N× over-accumulation
            // for vertices shared by multiple triangles. Self-resets the flag to 0.
            new AccumulateObservationTimeJob
            {
                vertexSeenThisTick = _vertexSeenThisTickNA,
                vertexObservation  = vertexObservationNA,
                fixedDeltaTime     = Time.fixedDeltaTime
            }.Schedule(vertexCount, 1024).Complete();

            if (_newValidDetectedTrianglesList.Length != 0)
            {
                // Step 6: mark vertices as explored (binary flag used for % calculation).
                new UpdateExploredVertexIndexesJob
                {
                    newValidDetectedTriangles = _newValidDetectedTrianglesList.AsArray(),
                    exploredVertexIndexes     = exploredVertexIndexesNA
                }.Schedule(_newValidDetectedTrianglesList.Length, 128).Complete();

                UpdateExploredMeshTriangles(_newValidDetectedTrianglesList);
                UpdateCoveragePercentage();
            }
        }

        eyeViewCameraTransform.hasChanged = false;
    }

    /// <summary>
    /// Appends newly confirmed triangles to the explored triangle list.
    /// Sets the dirty flag consumed by the rate-limited UpdateExploredMeshRenderer.
    /// </summary>
    private void UpdateExploredMeshTriangles(NativeList<Index3i> newExploredTriangles)
    {
        for (int i = 0; i < newExploredTriangles.Length; i++)
        {
            var t = newExploredTriangles[i];
            _exploredTriangleIndices.Add(t.a);
            _exploredTriangleIndices.Add(t.b);
            _exploredTriangleIndices.Add(t.c);
        }
        exploredMeshRendererNeedsUpdate = true;
    }

    /// <summary>
    /// Uploads the current triangle list and recomputed vertex colours to the GPU.
    /// Rate-limited by <c>exploredMeshUpdateDelay</c>.
    /// Triangle topology is only re-submitted when new triangles were added (dirty flag).
    /// Vertex colours are always recomputed when any triangles exist — observation time
    /// accumulates continuously, so colours must update even when no new triangles appear.
    /// </summary>
    private void UpdateExploredMeshRenderer()
    {
        if (exploredMeshRendererNeedsUpdate)
        {
            exploredMesh.SetTriangles(_exploredTriangleIndices, 0);
            exploredMesh.RecalculateBounds();
            exploredMeshRenderer.localBounds = exploredMesh.bounds;
            exploredMesh.RecalculateTangents();
            exploredMeshRendererNeedsUpdate = false;
        }

        // Always refresh colours when triangles exist — observation time keeps accumulating.
        if (_exploredTriangleIndices.Count > 0)
            ComputeVertexColors();
    }

    /// <summary>
    /// Runs the Burst colour job then copies results into the cached managed arrays
    /// before assigning to <c>exploredMesh.colors</c>.
    /// </summary>
    private void ComputeVertexColors()
    {
        new ComputeVertexColorsJob
        {
            vertexObservation  = vertexObservationNA,
            float4Colors       = float4ColorsNA,
            gradientLut        = _gradientLutNA,
            maxObservationTime = maxObservationTime
        }.Schedule(vertexCount, 1024).Complete();

        float4ColorsNA.CopyTo(_cachedFloat4Colors);
        for (int i = 0; i < vertexCount; i++)
            _cachedColors[i] = CommonUtils.Float4ToColor(_cachedFloat4Colors[i]);

        exploredMesh.colors = _cachedColors;
    }

    /// <summary>
    /// Computes per-section and overall coverage percentages from the explored vertex flags.
    /// Uses a single Burst IJob (sequential) rather than IJobParallelFor — the 6-range scan
    /// is too small to amortise parallel job scheduling overhead.
    /// </summary>
    private void UpdateCoveragePercentage()
    {
        new CalculateCoveragePercentagesJob
        {
            exploredVertices    = exploredVertexIndexesNA,
            ranges              = _coverageRanges,
            coveragePercentages = _coveragePercentagesNA
        }.Schedule().Complete();

        rectumCoveragePercentage          = _coveragePercentagesNA[0];
        sigmoidCoveragePercentage         = _coveragePercentagesNA[1];
        descendingColonCoveragePercentage = _coveragePercentagesNA[2];
        transverseColonCoveragePercentage = _coveragePercentagesNA[3];
        ascendingColonCoveragePercentage  = _coveragePercentagesNA[4];
        cecumCoveragePercentage           = _coveragePercentagesNA[5];

        // Overall: triangle-based to avoid double-counting shared boundary vertices.
        coveragePercentage = _exploredTriangleIndices.Count / 3f / triangleCount * 100f;
    }

    #endregion

    #region Burst Jobs

    /// <summary>
    /// Parallel job — culls each triangle against a circular focus cone and normal facing check.
    /// A triangle passes only when all three of its vertices satisfy:
    ///   1. Distance check: world-space distance from camera ≤ maxViewDistance.
    ///   2. Cone check: vertex lies within the circular focusConeAngle cone around cameraForward.
    ///      Evaluated as dot(forward, toVertex) ≥ cos(focusConeAngle) × dist — avoids normalize.
    ///   3. Normal check: surface faces the camera within visionAngle.
    ///      dot(cameraForward, -normal) ≥ cos(visionAngle).
    /// All triangles that pass are emitted — including already-explored ones — so observation
    /// time keeps accumulating while a surface stays in the camera cone.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct FindTrianglesInCameraView : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3>  vertices;
        [ReadOnly] public NativeArray<Vector3>  normals;
        [ReadOnly] public NativeArray<Index3i>  triangles;
        [ReadOnly] public Vector3               cameraPosition;
        [ReadOnly] public float3                cameraForwardNorm;
        [ReadOnly] public float                 cosVisionAngle;    // cos(visionAngle)    — precomputed in UpdateCoverage
        [ReadOnly] public float                 cosFocusConeAngle; // cos(focusConeAngle) — precomputed in UpdateCoverage
        [ReadOnly] public float                 maxViewDistance;
        [WriteOnly] public NativeList<Index3i>.ParallelWriter newSeenTriangles;
        [WriteOnly] public NativeList<int>.ParallelWriter     newSeenTriangleIndexes;

        public void Execute(int index)
        {
            var  triangle         = triangles[index];
            bool isTriangleInView = true;

            for (int vi = 0; vi < 3; vi++)
            {
                if (!isTriangleInView) break;

                int    vIdx   = vi == 0 ? triangle.a : (vi == 1 ? triangle.b : triangle.c);
                var    vertex = vertices[vIdx];
                var    normal = normals[vIdx];

                // Vector from camera to vertex (unnormalized — length reused for both checks).
                float3 toVertex = new float3(
                    vertex.x - cameraPosition.x,
                    vertex.y - cameraPosition.y,
                    vertex.z - cameraPosition.z);

                float dist = math.length(toVertex);

                // Distance check + circular cone check combined.
                // Cone: dot(forward, toVertex) >= cos(coneAngle) * dist
                // (equivalent to dot(forward, normalize(toVertex)) >= cos(coneAngle) but avoids division).
                bool isInsideView = dist > 0f
                    && dist < maxViewDistance
                    && math.dot(cameraForwardNorm, toVertex) >= cosFocusConeAngle * dist;

                if (isInsideView)
                {
                    // Normal facing check: surface must point toward the camera.
                    // normalDir = -normal (unit normal negated — algebraic simplification of the original expression).
                    float3 normalDir = new float3(-normal.x, -normal.y, -normal.z);

                    // dot < cos(threshold) ↔ angle > threshold (cosine monotone-decreasing on [0°,180°]).
                    if (math.dot(cameraForwardNorm, normalDir) < cosVisionAngle)
                        isTriangleInView = false;
                }
                else
                {
                    isTriangleInView = false;
                }
            }

            if (isTriangleInView)
            {
                newSeenTriangles.AddNoResize(triangle);
                newSeenTriangleIndexes.AddNoResize(index);
            }
        }
    }

    /// <summary>
    /// Parallel job — builds one RaycastCommand per candidate triangle,
    /// aimed from the camera position toward that triangle's centroid.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct PopulateRaycastCommandsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Index3i>         newSeenTriangles;
        [ReadOnly] public NativeArray<Vector3>         vertices;
        [WriteOnly] public NativeArray<RaycastCommand> raycastCommands;
        [WriteOnly] public NativeArray<Vector3>        triangleCenters;
        public Vector3                                 eyeViewCameraPosition;

        public void Execute(int index)
        {
            var triangleCenter = GetTriangleCenter(newSeenTriangles[index]);
            raycastCommands[index] = new RaycastCommand
            {
                from            = eyeViewCameraPosition,
                direction       = (triangleCenter - eyeViewCameraPosition).normalized,
                distance        = 10f,
                queryParameters = QueryParameters.Default
            };
            triangleCenters[index] = triangleCenter;
        }

        private Vector3 GetTriangleCenter(Index3i triangle)
        {
            return (vertices[triangle.a] + vertices[triangle.b] + vertices[triangle.c]) / 3;
        }
    }

    /// <summary>
    /// Parallel job — validates each raycast result against the expected triangle centre.
    /// For every confirmed (unoccluded) triangle: flags its vertices as seen this tick
    /// (write value 1). Races across parallel threads are harmless — all writes are
    /// identical, so the final value is always 1 regardless of order.
    /// Actual time accumulation is deferred to <see cref="AccumulateObservationTimeJob"/>
    /// so each vertex accumulates exactly one <c>fixedDeltaTime</c> per tick, regardless
    /// of how many triangles share it.
    /// Only newly confirmed triangles (not yet in the mesh list) are added to
    /// <c>newValidDetectedTriangles</c> and marked as explored.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct ProcessRaycastHitsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<RaycastHit>  raycastHitsNA;
        [ReadOnly] public NativeArray<Vector3>     triangleCentersNA;
        [ReadOnly] public NativeArray<int>         newSeenTriangleIndexesArray;
        [ReadOnly] public NativeArray<Index3i>     newSeenTrianglesArray;
        [NativeDisableParallelForRestriction]
        public NativeArray<int>                    areTrianglesExploredNA;
        // Races OK: all parallel threads write the same value (1), so outcome is always 1.
        [NativeDisableParallelForRestriction]
        public NativeArray<int>                    vertexSeenThisTick;
        [WriteOnly] public NativeList<Index3i>.ParallelWriter newValidDetectedTriangles;

        public void Execute(int index)
        {
            if (raycastHitsNA[index].point != triangleCentersNA[index]) return;

            var triangle = newSeenTrianglesArray[index];

            // Flag vertices seen this tick. AccumulateObservationTimeJob adds dt exactly
            // once per flagged vertex, avoiding N× accumulation for shared vertices.
            vertexSeenThisTick[triangle.a] = 1;
            vertexSeenThisTick[triangle.b] = 1;
            vertexSeenThisTick[triangle.c] = 1;

            // Only add to the mesh list on first confirmation.
            int triangleIndex = newSeenTriangleIndexesArray[index];
            if (areTrianglesExploredNA[triangleIndex] == 0)
            {
                newValidDetectedTriangles.AddNoResize(triangle);
                areTrianglesExploredNA[triangleIndex] = 1;
            }
        }
    }

    /// <summary>
    /// Parallel job — accumulates exactly one <c>fixedDeltaTime</c> per vertex that was
    /// flagged visible this tick, then self-resets the flag to 0.
    /// Must run after <see cref="ProcessRaycastHitsJob"/> completes.
    /// Self-resetting avoids a separate clear pass — only touched vertices are visited.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct AccumulateObservationTimeJob : IJobParallelFor
    {
        public NativeArray<int>   vertexSeenThisTick;
        public NativeArray<float> vertexObservation;
        [ReadOnly] public float   fixedDeltaTime;

        public void Execute(int index)
        {
            if (vertexSeenThisTick[index] == 0) return;
            vertexObservation[index] += fixedDeltaTime;
            vertexSeenThisTick[index] = 0;
        }
    }

    /// <summary>
    /// Parallel job — sets the binary "visited" flag for each vertex of each confirmed triangle.
    /// Used by CalculateCoveragePercentagesJob for section percentage calculations.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct UpdateExploredVertexIndexesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Index3i> newValidDetectedTriangles;
        [NativeDisableParallelForRestriction] public NativeArray<int> exploredVertexIndexes;

        public void Execute(int index)
        {
            var triangle = newValidDetectedTriangles[index];
            exploredVertexIndexes[triangle.a] = 1;
            exploredVertexIndexes[triangle.b] = 1;
            exploredVertexIndexes[triangle.c] = 1;
        }
    }

    /// <summary>
    /// Parallel job — maps accumulated observation time to a gradient colour for each vertex.
    /// t = sqrt(saturate(observation / maxObservationTime)) — sqrt bias makes the first
    /// fraction of a second visually distinct from zero, so newly seen areas pop immediately
    /// rather than blending into the gradient start colour.
    /// Colour is sampled from a 256-entry LUT baked from the full managed Gradient at Start,
    /// supporting multi-stop gradients (not just two endpoints).
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct ComputeVertexColorsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float>  vertexObservation;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4>            float4Colors;
        [ReadOnly] public NativeArray<float4> gradientLut;
        [ReadOnly] public float               maxObservationTime;

        public void Execute(int index)
        {
            // sqrt bias: early observation time is visually distinct from zero.
            float t      = math.sqrt(math.saturate(vertexObservation[index] / maxObservationTime));
            int   lutIdx = (int)(t * 255f);
            float4Colors[index] = gradientLut[lutIdx];
        }
    }

    /// <summary>
    /// Sequential Burst job — counts explored vertices in each of the six anatomical sections
    /// and writes a percentage per section into <c>coveragePercentages</c>.
    /// Implemented as IJob rather than IJobParallelFor because the six sequential range scans
    /// are too small to amortise the overhead of scheduling six parallel workers.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct CalculateCoveragePercentagesJob : IJob
    {
        [ReadOnly]  public NativeArray<int>   exploredVertices;
        [ReadOnly]  public NativeArray<int>   ranges;             // vertex count per section
        [WriteOnly] public NativeArray<float> coveragePercentages;

        public void Execute()
        {
            int start = 0;
            for (int i = 0; i < 6; i++)
            {
                int range = ranges[i];
                int count = 0;
                for (int j = start; j < start + range; j++)
                {
                    if (exploredVertices[j] != 0) count++;
                }
                coveragePercentages[i] = (count / (float)range) * 100f;
                start += range;
            }
        }
    }

    #endregion
}
