// ============================================================================
// LargeIntestineModelGenerator.cs
// 
// Generates and manages the large intestine 3D model based on a spline path.
// Creates segmented and welded mesh representations with blendshape support.
// 
// Key Features:
//   - Spline-based mesh generation for anatomically accurate shapes
//   - Segmented model: Individual sections with SkinnedMeshRenderers
//   - Welded model: Combined mesh for efficient rendering and collision
//   - Configuration-driven: Reads from LI_GenerationConfiguration files
// 
// Implements ISplineModelGenerator for integration with the Model Editor system.
// ============================================================================

using System.Collections.Generic;
using SplineMesh;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using GeometryUtils;
using LI.MeshTools;
using ModelEditor;
using Unity.VisualScripting;
using System.Runtime.InteropServices;
using MathNet.Numerics.Distributions;

namespace LargeIntestine
{
    /// <summary>
    /// Defines how meshes are placed along the spline.
    /// </summary>
    public enum MeshPlacementMode
    {
        /// <summary>
        /// Each mesh spans from one node to the next (traditional mode).
        /// Node0 ----[Mesh0]---- Node1 ----[Mesh1]---- Node2
        /// </summary>
        CurveBased,

        /// <summary>
        /// Each mesh is centered on a node, spanning half-way to adjacent nodes.
        /// --[Mesh0]-- Node1 --[Mesh1]-- Node2 --[Mesh2]--
        /// </summary>
        NodeCentered
    }

    /// <summary>
    /// Generates and manages the large intestine 3D model.
    /// Implements <see cref="ISplineModelGenerator"/> for editor integration.
    /// </summary>
    /// <remarks>
    /// <para>This generator creates two model representations:</para>
    /// <list type="bullet">
    ///   <item><b>Segmented Model:</b> Individual sections with separate renderers for editing</item>
    ///   <item><b>Welded Model:</b> Combined mesh for viewing and collision detection</item>
    /// </list>
    /// </remarks>
    public class LargeIntestineModelGenerator : MonoBehaviour, ISplineModelGenerator
    {
        #region Serialized Fields

        [Tooltip("Default spline prefab to use if no spline exists")]
        [SerializeField] private GameObject defaultSpline;

        [Tooltip("Configuration file name (in StreamingAssets)")]
        [SerializeField] private string LIConfigurationFile = "108217697723506.ligc";

        [Header("Generated GameObject Names")]
        [SerializeField] private string _segmentedModelGoName = "SegmentedModel";
        [SerializeField] private string _weldedModelGOName = "WeldedModel";
        [SerializeField] private string _visualModelGOName = "VisualModel";
        [SerializeField] private string _modelLayerMaskName = "ColonModel";
        [SerializeField] private string _modelTagName = "ColonModel";

        [Header("Welding Configuration")]
        [Tooltip("Use AI-based mesh welding algorithm")]
        [SerializeField] private bool _useAIWelder = false;
        [Tooltip("Use simple mesh combining instead of welding")]
        [SerializeField] private bool _generateCombinedVersion = false;
        [Tooltip("Show debug visualization of welded vertices")]
        [SerializeField] private bool _showWeldedVertices = false;
        [Tooltip("Recalculate normals after welding")]
        [SerializeField] private bool _recalculateNormals = false;
        [Tooltip("Position threshold for vertex welding")]
        [SerializeField] private float _weldingPosThreshold = 0.0005f;
        [Tooltip("Angle threshold for normal smoothing")]
        [SerializeField] private float _weldingAngleThreshold = 35f;
        [Tooltip("Whether to fix blendshape delta normals to prevent flipping artifacts")]
        [SerializeField] private bool _fixBlendshapeNormals = false;
        [Tooltip("When true, blendshape delta normals and tangents are zeroed out and never computed during mesh bending. Useful when blendshape normals/tangents cause artifacts.")]
        [SerializeField] private bool _skipBlendshapeNormalsTangents = false;

        [Header("Mesh Placement")]
        [Tooltip("How meshes are placed along the spline")]
        [SerializeField] private MeshPlacementMode _meshPlacementMode = MeshPlacementMode.CurveBased;

        [Header("Anatomical Distribution")]
        [Tooltip("Minimum teniae coli presence (Tenias_Sup/Med/Inf)")]
        [SerializeField, Range(0f, 100f)] private float _teniaeMin = 0f;
        [Tooltip("Maximum teniae coli presence (Tenias_Sup/Med/Inf)")]
        [SerializeField, Range(0f, 100f)] private float _teniaeMax = 100f;

        [Tooltip("Minimum haustra presence (Saculacion_sup/Inf)")]
        [SerializeField, Range(0f, 100f)] private float _haustraMin = 0f;
        [Tooltip("Maximum haustra presence (Saculacion_sup/Inf)")]
        [SerializeField, Range(0f, 100f)] private float _haustraMax = 100f;

        [Tooltip("Minimum plicae semilunares / fold closure (Cierre_A)")]
        [SerializeField, Range(0f, 100f)] private float _plicaeMin = 0f;
        [Tooltip("Maximum plicae semilunares / fold closure (Cierre_A)")]
        [SerializeField, Range(0f, 100f)] private float _plicaeMax = 100f;

        [Tooltip("Minimum width for Ancho X/Y values")]
        [SerializeField, Range(0f, 100f)] private float _anchoMin = 0f;
        [Tooltip("Maximum width for Ancho X/Y values")]
        [SerializeField, Range(0f, 100f)] private float _anchoMax = 100f;

        [Tooltip("Minimum triangular cross-section shape (Deltas) - peaks at Transverse")]
        [SerializeField, Range(0f, 100f)] private float _deltasMin = 0f;
        [Tooltip("Maximum triangular cross-section shape (Deltas) - peaks at Transverse")]
        [SerializeField, Range(0f, 100f)] private float _deltasMax = 100f;

        [Header("Segment Characteristic Curves")]
        [Tooltip("Teniae Coli distribution curve (X=segment 0-5, Y=factor 0-1)")]
        [SerializeField] private AnimationCurve _teniaeCurve = AnimationCurve.Linear(0, 0, 5, 0.9f);
        [Tooltip("Haustra distribution curve (X=segment 0-5, Y=factor 0-1)")]
        [SerializeField] private AnimationCurve _haustraCurve = AnimationCurve.Linear(0, 0, 5, 0.9f);
        [Tooltip("Plicae Semilunares distribution curve (X=segment 0-5, Y=factor 0-1)")]
        [SerializeField] private AnimationCurve _plicaeCurve = AnimationCurve.Linear(0, 0, 5, 0.9f);
        [Tooltip("Width X-axis distribution curve (X=segment 0-5, Y=factor 0-1)")]
        [SerializeField] private AnimationCurve _anchoXCurve = AnimationCurve.Linear(0, 0.5f, 5, 0.95f);
        [Tooltip("Width Y-axis distribution curve (X=segment 0-5, Y=factor 0-1)")]
        [SerializeField] private AnimationCurve _anchoYCurve = AnimationCurve.Linear(0, 0.5f, 5, 0.95f);
        [Tooltip("Triangular geometry distribution curve (X=segment 0-5, Y=factor 0-1)")]
        [SerializeField] private AnimationCurve _deltasCurve = AnimationCurve.Linear(0, 0, 5, 0.25f);

        [Header("Distribution Variation")]
        [Tooltip("Noise strength for anatomical variation (0=smooth, 1=high variation)")]
        [SerializeField, Range(0f, 1f)] private float _distributionNoiseStrength = 0f;
        [Tooltip("Noise scale - higher values = faster variation along colon")]
        [SerializeField, Range(0.1f, 10f)] private float _distributionNoiseScale = 2f;
        [Tooltip("Seed for reproducible noise (change for different variations)")]
        [SerializeField] private int _distributionNoiseSeed = 42;

        // Noise settings — loaded from Model.NoiseSettings on generation; overwritten by user input during model edition.
        // Displayed read/write via LargeIntestineModelGeneratorEditor (CustomEditor).
        private NoiseSettings _noiseSettings = new NoiseSettings();

        [Header("Debug")]
        [SerializeField] private string _modelSavePath = "Assets/Models/SavedModel.tc";

        #endregion

        #region Public Properties

        /// <summary>
        /// The generation configuration loaded from file.
        /// </summary>
        [HideInInspector] public LI_GenerationConfiguration generationConfiguration;

        /// <summary>
        /// The spline defining the intestine's path.
        /// </summary>
        public Spline Spline { get; private set; }

        /// <summary>
        /// Number of nodes in the spline.
        /// </summary>
        public int SplineNodeCount { get; private set; }

        /// <summary>
        /// Whether the segmented model has been generated.
        /// </summary>
        public bool IsSegmentedModelGenerated { get; private set; }

        /// <summary>
        /// Whether the welded model has been generated.
        /// </summary>
        public bool IsWeldedModelGenerated { get; private set; }

        /// <summary>
        /// Starting t-values for each segment along the spline (0-1).
        /// </summary>
        public List<float> SegmentsStartValueInSpline { get; private set; } = new List<float>();

        /// <summary>
        /// Root GameObject for the segmented model.
        /// </summary>
        public GameObject SegmentedModelGO { get; private set; }

        /// <summary>
        /// Root GameObject for the welded model.
        /// </summary>
        public GameObject WeldedModelGO { get; private set; }

        /// <summary>
        /// Renderer for the welded model.
        /// </summary>
        public SkinnedMeshRenderer WeldedModelRenderer { get; private set; }

        /// <summary>
        /// Collider for the welded model.
        /// </summary>
        public MeshCollider WeldedModelCollider { get; private set; }

        /// <summary>
        /// Root GameObject for the visual (display) model.
        /// </summary>
        public GameObject VisualModelGO { get; private set; }

        /// <summary>
        /// Renderer for the visual model.
        /// </summary>
        public SkinnedMeshRenderer VisualModelRenderer { get; private set; }

        /// <summary>
        /// List of all intestine sections with their renderers and colliders.
        /// </summary>
        public List<LargeIntestineSection> LargeIntestineSections { get; private set; } = new List<LargeIntestineSection>();

        /// <summary>
        /// List of section renderers for quick access.
        /// </summary>
        public List<SkinnedMeshRenderer> SectionRenderers { get; private set; } = new List<SkinnedMeshRenderer>();

        public bool useOldCombineMeshesMethod = false;

        #endregion

        #region Private Fields

        private SplineSmoother _splineSmoother;
        private List<CustomMeshBender> _customMeshBenders = new List<CustomMeshBender>();
        private GameObjectFactory _goFactory = new GameObjectFactory();
        private LI_MeshWelder _liMeshWelder = new LI_MeshWelder();
        private Dictionary<string, Material> _sectionMaterialsDict = new Dictionary<string, Material>();
        private Dictionary<SplineMeshProfile, Mesh> _filteredMeshCache = new Dictionary<SplineMeshProfile, Mesh>();
        private IEnumerable<MeshCollider> _sectionColliders => LargeIntestineSections?.Select(s => s.Collider);

        #endregion

        #region ISplineModelGenerator Implementation

        /// <inheritdoc/>
        GameObject ISplineModelGenerator.RootGameObject => gameObject;

        /// <inheritdoc/>
        GameObject ISplineModelGenerator.SegmentedModelGO => SegmentedModelGO;

        /// <inheritdoc/>
        MeshCollider ISplineModelGenerator.WeldedModelCollider => WeldedModelCollider;

        /// <inheritdoc/>
        SkinnedMeshRenderer ISplineModelGenerator.WeldedModelRenderer => WeldedModelRenderer;

        /// <inheritdoc/>
        T ISplineModelGenerator.GetComponentInChildren<T>(bool includeInactive) => GetComponentInChildren<T>(includeInactive);

        /// <inheritdoc/>
        IReadOnlyList<IModelSection> ISplineModelGenerator.Sections => LargeIntestineSections.Cast<IModelSection>().ToList();

        /// <inheritdoc/>
        void ISplineModelGenerator.ComputeMeshRecalculations() => ComputeBendedMeshesRecalculations();

        /// <inheritdoc/>
        ModelEditor.IGenerationConfiguration ISplineModelGenerator.GenerationConfiguration => generationConfiguration;

        #endregion

        #region Configuration

        /// <summary>
        /// Loads the generation configuration from the streaming assets folder.
        /// </summary>
        /// <param name="model">The model containing the configuration filename (without extension).</param>
        /// <remarks>
        /// The model stores only the filename. The extension is appended automatically
        /// using <see cref="FileManager.LIGenerationConfigurationFileExtension"/>.
        /// </remarks>
        public void LoadGenerationConfiguration(Model model)
        {
            generationConfiguration = new LI_GenerationConfiguration();
            generationConfiguration.ReadFromFile(Path.Combine(Application.streamingAssetsPath, model.GenerationConfigurationFile + FileManager.LIGenerationConfigurationFileExtension));
            _noiseSettings = model.NoiseSettings != null ? new NoiseSettings(model.NoiseSettings) : new NoiseSettings();
        }

        /// <summary>Returns a copy of the current noise settings.</summary>
        public NoiseSettings GetNoiseSettings() => new NoiseSettings(_noiseSettings);

        /// <summary>Replaces the current noise settings with a copy of <paramref name="settings"/>.</summary>
        public void ApplyNoiseSettings(NoiseSettings settings) => _noiseSettings = new NoiseSettings(settings);

        /// <summary>
        /// Loads the generation configuration from the streaming assets folder given the LI configuration file name.
        /// </summary>
        public void LoadCustomGenerationConfiguration()
        {
            generationConfiguration = new LI_GenerationConfiguration();
            generationConfiguration.ReadFromFile(Path.Combine(Application.streamingAssetsPath, LIConfigurationFile));
        }

        #endregion

        #region Reset and Cleanup

        /// <summary>
        /// Resets all variables related to segmented model generation.
        /// </summary>
        /// <remarks>
        /// Clears section lists, mesh benders, and cached materials.
        /// Does not destroy GameObjects - use <see cref="Reset"/> for full cleanup.
        /// </remarks>
        private void ResetSegmentedGenerationVariables()
        {
            IsSegmentedModelGenerated = false;
            SegmentsStartValueInSpline.Clear();
            LargeIntestineSections.Clear();
            SectionRenderers.Clear();
            _customMeshBenders.Clear();
            _sectionMaterialsDict.Clear();

            // Destroy filtered mesh copies (only those that differ from the original)
            foreach (var kvp in _filteredMeshCache)
            {
                if (kvp.Value != null && kvp.Value != kvp.Key.mesh)
                    Object.DestroyImmediate(kvp.Value);
            }
            _filteredMeshCache.Clear();
        }

        /// <summary>
        /// Fully resets the generator, destroying all created GameObjects.
        /// </summary>
        /// <remarks>
        /// Destroys the spline, segmented model, welded model, and visual model.
        /// Call this before regenerating from scratch or when cleaning up.
        /// </remarks>
        public void Reset()
        {
            // Destroy all factory-created GameObjects
            _goFactory.DestroyAll();
            _goFactory.Clear();

            // Destroy spline if it exists
            if (Spline != null)
            {
                CommonUtils.Destroy(Spline.gameObject);
                Spline = null;
            }

            // Clear model references
            SegmentedModelGO = null;
            WeldedModelGO = null;
            VisualModelGO = null;
            ResetSegmentedGenerationVariables();
        }

        #endregion

        #region Spline Management

        /// <summary>
        /// Ensures a spline exists, creating one from the default prefab if needed.
        /// </summary>
        /// <remarks>
        /// Also initializes the <see cref="_splineSmoother"/> reference and updates <see cref="SplineNodeCount"/>.
        /// </remarks>
        public void EnsureSplineExists()
        {
            if (Spline == null)
            {
                // Instantiate from default prefab
                Spline = CommonUtils.Instantiate(defaultSpline, transform, null, true).GetComponent<Spline>();
            }
            _splineSmoother = Spline.GetComponent<SplineSmoother>();
            SplineNodeCount = Spline.nodes.Count;
        }

        #endregion

        #region Model Generation

        /// <summary>
        /// Generates the segmented model from the spline and configuration.
        /// </summary>
        /// <remarks>
        /// <para>Creates individual mesh sections based on the selected placement mode.</para>
        /// <para>Each section gets its own <see cref="SkinnedMeshRenderer"/> for independent blendshape control.</para>
        /// </remarks>
        public void GenerateSegmentedModel(bool destroyExisting = true)
        {
            Debug.LogWarning("Generating segmented model. This can be expensive, especially with many sections or high-poly meshes. Use with caution.");
            if (destroyExisting)
            {
                _goFactory.DestroyAll();
                _goFactory.Clear();
                SegmentedModelGO = null;
            }
            EnsureSplineExists();
            ResetSegmentedGenerationVariables();

            if (SegmentedModelGO == null)
            {
                SegmentedModelGO = GetOrCreateGo(_segmentedModelGoName, this.transform);
            }

            if (_meshPlacementMode == MeshPlacementMode.NodeCentered)
            {
                GenerateSegmentedModelNodeCentered();
            }
            else
            {
                GenerateSegmentedModelCurveBased();
            }

            // Cache renderer references for quick access
            SectionRenderers = LargeIntestineSections.Select(s => s.Renderer).ToList();
            UpdateUpVectors();
            IsSegmentedModelGenerated = true;

        }

        /// <summary>
        /// Generates segmented model with curve-based placement (traditional mode).
        /// Each mesh spans from one spline node to the next.
        /// </summary>
        private void GenerateSegmentedModelCurveBased()
        {
            // Flatten configuration to match spline 1:1
            var sectionConfigs = generationConfiguration.intestineSegments
                .SelectMany(segment => segment.liSections.Select(section => new { segment, section }))
                .ToList();

            int splineCurvesCount = Spline.curves.Count;
            if (sectionConfigs.Count != splineCurvesCount)
            {
                Debug.LogError($"Configuration sections count ({sectionConfigs.Count}) does not match spline curves count ({Spline.curves.Count}).");
                return;
            }

            string currentSegmentName = null;
            GameObject currentSegmentGo = null;
            float currentSegmentLength = 0f;

            for (int i = 0; i < splineCurvesCount; i++)
            {
                var sectionConfig = sectionConfigs[i];
                string segmentName = sectionConfig.segment.liSegment.ToString();
                if (segmentName != currentSegmentName)
                {
                    SegmentsStartValueInSpline.Add(currentSegmentLength);
                    currentSegmentGo = GetOrCreateSegmentGo(segmentName);
                    currentSegmentName = segmentName;
                }
                float sectionIntervalStart = currentSegmentLength;
                GenerateSectionCurveBased(currentSegmentGo, sectionConfig.section, i, sectionIntervalStart);
                currentSegmentLength += Spline.curves[i].Length;
            }
        }

        /// <summary>
        /// Generates segmented model with node-centered placement.
        /// Each mesh is centered on a spline node, spanning half-way to adjacent nodes.
        /// </summary>
        private void GenerateSegmentedModelNodeCentered()
        {
            // Flatten configuration
            var sectionConfigs = generationConfiguration.intestineSegments
                .SelectMany(segment => segment.liSections.Select(section => new { segment, section }))
                .ToList();

            // For node-centered mode, we need one mesh per node
            int nodeCount = Spline.nodes.Count;
            int meshCount = nodeCount;

            if (sectionConfigs.Count != meshCount)
            {
                Debug.LogError($"Configuration sections count ({sectionConfigs.Count}) does not match node count ({meshCount}) for node-centered mode.");
                return;
            }

            // Calculate cumulative distances for SegmentsStartValueInSpline tracking
            var nodeDistances = new List<float> { 0f };
            for (int i = 0; i < Spline.curves.Count; i++)
            {
                nodeDistances.Add(nodeDistances[i] + Spline.curves[i].Length);
            }

            string currentSegmentName = null;
            GameObject currentSegmentGo = null;

            for (int i = 0; i < meshCount; i++)
            {
                var sectionConfig = sectionConfigs[i];
                string segmentName = sectionConfig.segment.liSegment.ToString();
                if (segmentName != currentSegmentName)
                {
                    SegmentsStartValueInSpline.Add(nodeDistances[i]);
                    currentSegmentGo = GetOrCreateSegmentGo(segmentName);
                    currentSegmentName = segmentName;
                }

                // Replicate SetIntervalByNodeCenter interval logic to know t for noise
                float nd = nodeDistances[i];
                float prev = i > 0 ? nodeDistances[i - 1] : 0f;
                float next = i < nodeDistances.Count - 1 ? nodeDistances[i + 1] : Spline.Length;
                float nodeIntervalStart = i == 0 ? 0f : (prev + nd) / 2f;

                GenerateSectionNodeCentered(currentSegmentGo, sectionConfig.section, i, nodeIntervalStart);
            }
        }

        /// <summary>
        /// Generates a single section using curve-based placement (mesh spans one curve).
        /// </summary>
        /// <param name="segmentGo">Parent GameObject for this anatomical segment.</param>
        /// <param name="liSectionConfiguration">Configuration defining mesh, materials, and blendshapes.</param>
        /// <param name="splineCurveIndex">Index of the spline curve this section follows.</param>
        private void GenerateSectionCurveBased(GameObject segmentGo, LI_SectionConfiguration liSectionConfiguration, int splineCurveIndex, float intervalStart)
        {
            if (liSectionConfiguration.meshProfile == null) Debug.LogError($"LI_SectionConfiguration.meshProfile is null for curve index {splineCurveIndex}");
            var sectionName = liSectionConfiguration.meshProfile.name + "_" + splineCurveIndex;
            var sectionGo = GetOrCreateSectionGo(sectionName, segmentGo.transform);
            var meshBender = sectionGo.GetComponent<CustomMeshBender>();
            meshBender.SetInterval(Spline.GetCurve(splineCurveIndex));

            int nc = Spline.curves.Count;
            // tailSectionStart = arc-length start of the last section (section n)
            float tailSectionStart = nc >= 1 ? Spline.Length - Spline.curves[nc - 1].Length : -1f;
            // tailFadeStart = where amplitude begins dropping (fade always ends at tailSectionStart):
            //   centerFade active → center of section n-1 (half a section to fade from 1 → 0)
            //   no centerFade    → start of section n-1 (full section to fade from 1 → 0)
            float tailFadeStart = nc >= 2
                ? (_noiseSettings.CenterFade > 0f
                    ? tailSectionStart - Spline.curves[nc - 2].Length * 0.5f
                    : tailSectionStart - Spline.curves[nc - 2].Length)
                : -1f;
            SetupSectionMeshAndMaterials(sectionGo, meshBender, liSectionConfiguration, intervalStart, Spline.curves[splineCurveIndex].Length, Spline.Length, tailFadeStart, tailSectionStart);
        }

        /// <summary>
        /// Generates a single section using node-centered placement (mesh centered on a node).
        /// </summary>
        /// <param name="segmentGo">Parent GameObject for this anatomical segment.</param>
        /// <param name="liSectionConfiguration">Configuration defining mesh, materials, and blendshapes.</param>
        /// <param name="nodeIndex">Index of the node this section is centered on.</param>
        private void GenerateSectionNodeCentered(GameObject segmentGo, LI_SectionConfiguration liSectionConfiguration, int nodeIndex, float intervalStart)
        {
            var sectionName = liSectionConfiguration.meshProfile.meshType.ToString() + "_Node" + nodeIndex;
            var sectionGo = GetOrCreateSectionGo(sectionName, segmentGo.transform);
            var meshBender = sectionGo.GetComponent<CustomMeshBender>();
            meshBender.SetIntervalByNodeCenter(Spline, nodeIndex);

            // Compute the arc-length span of this node-centered interval (mirrors SetIntervalByNodeCenter logic)
            var nodeDistances = new List<float> { 0f };
            for (int c = 0; c < Spline.curves.Count; c++)
                nodeDistances.Add(nodeDistances[c] + Spline.curves[c].Length);
            float nd = nodeDistances[nodeIndex];
            float prev = nodeIndex > 0 ? nodeDistances[nodeIndex - 1] : 0f;
            float next = nodeIndex < nodeDistances.Count - 1 ? nodeDistances[nodeIndex + 1] : Spline.Length;
            float nodeIntervalEnd = nodeIndex == nodeDistances.Count - 2 ? Spline.Length : (nd + next) / 2f;
            float curveArcLength = nodeIntervalEnd - intervalStart;

            // tailSectionStart = intervalStart of the last node (section n)
            int nn = nodeDistances.Count; // equals nodeCount
            float tailSectionStart = nn >= 2
                ? (nodeDistances[nn - 2] + nodeDistances[nn - 1]) / 2f
                : -1f;
            // intervalStart of section n-1
            float intervalStartN1 = nn >= 3
                ? (nodeDistances[nn - 3] + nodeDistances[nn - 2]) / 2f
                : 0f;
            // tailFadeStart = where amplitude begins dropping (fade always ends at tailSectionStart):
            //   centerFade active → center of section n-1, midpoint between its start and tailSectionStart
            //   no centerFade    → start of section n-1 (full section to fade from 1 → 0)
            float tailFadeStart = tailSectionStart >= 0f
                ? (_noiseSettings.CenterFade > 0f
                    ? (intervalStartN1 + tailSectionStart) * 0.5f
                    : intervalStartN1)
                : -1f;
            SetupSectionMeshAndMaterials(sectionGo, meshBender, liSectionConfiguration, intervalStart, curveArcLength, Spline.Length, tailFadeStart, tailSectionStart);
        }

        /// <summary>
        /// Common setup for a section's filtered mesh, anatomical noise, materials, and blendshapes.
        /// Uses a cached filtered mesh copy that only includes enabled blendshapes from the profile.
        /// Applies <see cref="SplineMeshNoiseApplicator"/> to the raw <see cref="MeshData"/> before
        /// handing it to <see cref="CustomMeshBender"/>, so noise is baked in object space prior to
        /// spline bending — this preserves seam continuity because adjacent sections share the same
        /// arc-length coordinate at their shared boundary.
        /// </summary>
        /// <param name="sectionGo">The section GameObject (must have CustomMeshBender, SkinnedMeshRenderer, MeshCollider).</param>
        /// <param name="meshBender">The bender component that will deform this section along the spline.</param>
        /// <param name="liSectionConfiguration">Section config: mesh profile, material overrides, blendshape values.</param>
        /// <param name="intervalStart">Arc-length position where this section begins along the whole spline.</param>
        /// <param name="curveArcLength">Arc-length span of this section (end − start in spline space).</param>
        /// <param name="totalSplineLength">Total arc-length of the spline; used to normalize t for noise sampling.</param>
        /// <param name="tailFadeStartArcLength">
        /// Arc-length where the tail amplitude fade begins (linear ramp from 1 → 0 toward totalSplineLength).
        /// When centerFade &gt; 0: set to the midpoint of section n-1, so the right side of the haustral fold
        /// rises only to a partial amplitude before continuing smoothly downward through section n.
        /// When centerFade == 0: set to the end of section n-1 (= start of section n), so n-1 keeps full
        /// amplitude and the decay happens entirely in section n.
        /// Pass -1 to disable tail fade.
        /// </param>
        /// <param name="tailSectionStartArcLength">
        /// Arc-length at the start of the last section (section n).
        /// Vertices at or beyond this point skip centerFade entirely, allowing the tail to decay smoothly
        /// without a haustral-fold dip interrupting it.
        /// Pass -1 to always apply centerFade (disables the tail-section exception).
        /// </param>
        private void SetupSectionMeshAndMaterials(GameObject sectionGo, CustomMeshBender meshBender, LI_SectionConfiguration liSectionConfiguration, float intervalStart, float curveArcLength, float totalSplineLength, float tailFadeStartArcLength = -1f, float tailSectionStartArcLength = -1f)
        {
            // Use a filtered mesh that contains only enabled blendshapes
            var sourceMesh = GetOrCreateFilteredMesh(liSectionConfiguration.meshProfile);
            var meshData = new MeshData(sourceMesh, Vector3.zero, Quaternion.Euler(Vector3.zero), Vector3.one, true);
            meshData.BuildData();
            // Apply anatomical noise before bending: displaces vertices radially in (t,θ) spline space,
            // ensuring continuity at section seams since adjacent sections share the same t at boundaries.
            SplineMeshNoiseApplicator.Apply(meshData, intervalStart, curveArcLength, totalSplineLength, _noiseSettings, tailFadeStartArcLength, tailSectionStartArcLength);
            meshBender.SkipBlendshapeNormalsAndTangents = _skipBlendshapeNormalsTangents;
            meshBender.MeshData = meshData;
            meshBender.Configure();
            _customMeshBenders.Add(meshBender);

            var renderer = sectionGo.GetComponent<SkinnedMeshRenderer>();
            var collider = sectionGo.GetComponent<MeshCollider>();
            if (liSectionConfiguration.meshProfile.defaultMaterials != null && liSectionConfiguration.meshProfile.defaultMaterials.Length > 0)
            {
                renderer.sharedMaterials = liSectionConfiguration.meshProfile.defaultMaterials;
            }
            else
            {
                if (liSectionConfiguration.materialOverrideNames != null && liSectionConfiguration.materialOverrideNames.Length > 0)
                {
                    var materials = new Material[liSectionConfiguration.materialOverrideNames.Length];
                    for (int i = 0; i < liSectionConfiguration.materialOverrideNames.Length; i++)
                    {
                        var materialName = liSectionConfiguration.materialOverrideNames[i];
                        if (!_sectionMaterialsDict.TryGetValue(materialName, out var sectionMaterial))
                        {
                            sectionMaterial = Resources.Load<Material>(Path.Combine(FileManager.materialResourcesPath, materialName));
                            _sectionMaterialsDict[materialName] = sectionMaterial;
                        }
                        materials[i] = sectionMaterial;
                    }
                    renderer.sharedMaterials = materials;
                }
            }

            var largeIntestineSection = new LargeIntestineSection(renderer, collider);

            // Apply blendshape weights by name so reindexing from filtering is transparent
            foreach (var kvp in liSectionConfiguration.blendshapeValues)
            {
                int bsIdx = renderer.sharedMesh.GetBlendShapeIndex(kvp.Key);
                if (bsIdx >= 0)
                    renderer.SetBlendShapeWeight(bsIdx, kvp.Value);
            }

            LargeIntestineSections.Add(largeIntestineSection);
        }

        /// <summary>
        /// Returns a cached filtered mesh for the given profile, creating it on first call.
        /// Multiple sections sharing the same profile reuse the same copy.
        /// </summary>
        private Mesh GetOrCreateFilteredMesh(SplineMeshProfile profile)
        {
            if (!_filteredMeshCache.TryGetValue(profile, out var filtered))
            {
                //ValidateUVChannels(profile.mesh, profile, "ORIGINAL");
                filtered = profile.CreateFilteredMesh();

                // CreateFilteredMesh may return the original asset mesh directly when no
                // blendshapes are filtered out. We need a writable copy to fix the normals.
                if (filtered == profile.mesh)
                {
                    filtered = Instantiate(profile.mesh);
                    filtered.name = profile.mesh.name + "_filtered";
                }

                if(_fixBlendshapeNormals) GeometryUtils.BlendshapeNormalFixer.FixBlendshapeNormals(filtered);

                // Bake the profile's target UV channel into UV0 so the shader always reads UV0,
                // and the channel selection survives mesh welding without any MaterialPropertyBlock.
                if (profile.uvChannelIndex != 0)
                {
                    var uvs = new List<Vector2>();
                    filtered.GetUVs(profile.uvChannelIndex, uvs);
                    if (uvs.Count > 0)
                        filtered.SetUVs(0, uvs);
                }

                _filteredMeshCache[profile] = filtered;
                //ValidateUVChannels(filtered, profile, "FILTERED");
                //DebugUVChannelBake(profile.mesh, filtered, profile);
            }
            return filtered;
        }

        /// <summary>
        /// Logs the UV channel state of a mesh: which channels have data, which are empty, and which are duplicates.
        /// </summary>
        private void ValidateUVChannels(Mesh mesh, SplineMeshProfile profile, string stage = "")
        {
            var channels = new List<List<Vector2>>();
            for (int i = 0; i < 5; i++)
            {
                var list = new List<Vector2>();
                mesh.GetUVs(i, list);
                channels.Add(list);
            }

            string stageLabel = string.IsNullOrEmpty(stage) ? "" : $"[{stage}] ";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[UV Validation] {stageLabel}Mesh: '{mesh.name}' | Profile: '{profile.name}' | Target channel: {profile.uvChannelIndex}");
            for (int i = 0; i < 5; i++)
            {
                string result = UVDescribe(i, channels);
                string warn = (i == profile.uvChannelIndex && (result == "EMPTY" || result.StartsWith("DUPLICATE"))) ? " ← TARGET CHANNEL WARNING" : "";
                sb.AppendLine($"  UV{i}: {result}{warn}");
            }

            Debug.Log(sb.ToString());
        }

        private string UVDescribe(int channel, List<List<Vector2>> all)
        {
            var verts = all[channel];
            if (verts.Count == 0) return "EMPTY";

            for (int j = 0; j < channel; j++)
                if (UVIsDuplicate(verts, all[j]))
                    return $"DUPLICATE of UV{j}  ({verts.Count} verts)";

            bool allZero = true;
            for (int v = 0; v < Mathf.Min(verts.Count, 20); v++)
                if (verts[v] != Vector2.zero) { allZero = false; break; }

            Vector2 min = verts[0], max = verts[0];
            foreach (var uv in verts) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }

            string zeroWarn = allZero ? "  !! ALL ZERO" : "";
            return $"OK  {verts.Count} verts  range X:[{min.x:F2}..{max.x:F2}] Y:[{min.y:F2}..{max.y:F2}]{zeroWarn}";
        }

        private bool UVIsDuplicate(List<Vector2> a, List<Vector2> b)
        {
            if (a.Count != b.Count || a.Count == 0) return false;
            int step = Mathf.Max(1, a.Count / 20);
            for (int i = 0; i < a.Count; i += step)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// Logs whether UV0 of the filtered mesh was changed by the UV channel bake,
        /// and if so, which channel of the original mesh it now matches.
        /// </summary>
        private void DebugUVChannelBake(Mesh originalMesh, Mesh filteredMesh, SplineMeshProfile profile)
        {
            var filteredUV0 = new List<Vector2>();
            filteredMesh.GetUVs(0, filteredUV0);

            var originalUV0 = new List<Vector2>();
            originalMesh.GetUVs(0, originalUV0);

            if (UVIsDuplicate(filteredUV0, originalUV0))
            {
                Debug.Log($"[UV Bake] Profile '{profile.name}' (uvChannelIndex={profile.uvChannelIndex}): filtered UV0 is UNCHANGED from original UV0.");
                return;
            }

            // UV0 changed — find which original channel it now matches
            for (int i = 1; i < 8; i++)
            {
                var origChannel = new List<Vector2>();
                originalMesh.GetUVs(i, origChannel);
                if (origChannel.Count == 0) continue;
                if (UVIsDuplicate(filteredUV0, origChannel))
                {
                    Debug.Log($"[UV Bake] Profile '{profile.name}' (uvChannelIndex={profile.uvChannelIndex}): filtered UV0 now matches original UV{i}. Bake successful.");
                    return;
                }
            }

            Debug.LogWarning($"[UV Bake] Profile '{profile.name}' (uvChannelIndex={profile.uvChannelIndex}): filtered UV0 does not match any original UV channel. Bake may have failed or source channel was empty.");
        }

        /// <summary>
        /// Generates a welded (unified) model from the segmented sections.
        /// </summary>
        /// <param name="keepSegmentedModel">If true, keeps the segmented model after welding.</param>
        /// <param name="generateCombinedVersion">If true, uses simple mesh combining instead of welding.</param>
        /// <remarks>
        /// <para>Welding merges vertices at section boundaries for seamless rendering.</para>
        /// <para>Can use either AI-based welding or traditional threshold-based welding.</para>
        /// </remarks>
        public void GenerateWeldedModel(bool keepSegmentedModel = false, bool generateCombinedVersion = false)
        {
            Debug.LogWarning("Generating welded model. Welding can be very expensive, especially with high-poly meshes and low thresholds. Use with caution.");
            IsWeldedModelGenerated = false;
            if (SegmentedModelGO == null || !IsSegmentedModelGenerated)
            {
                GenerateSegmentedModel();
            }
            
            if (WeldedModelGO == null)
            {
                WeldedModelGO = GetOrCreateWeldedModelGo(_weldedModelGOName);
            }

            WeldedModelRenderer = WeldedModelGO.GetComponent<SkinnedMeshRenderer>();
            WeldedModelCollider = WeldedModelGO.GetComponent<MeshCollider>();

            if (generateCombinedVersion || _generateCombinedVersion)
            {
                WeldedModelRenderer.sharedMesh = GenerateCombinedMesh();
                WeldedModelRenderer.sharedMaterials = LargeIntestineSections[0].Renderer.sharedMaterials;
                NormalSolver2.RecalculateNormals(WeldedModelRenderer.sharedMesh, 180f);
                NormalSolver2.RecalculateTangents(WeldedModelRenderer.sharedMesh);

            }
            else
            {
                if (_useAIWelder)
                {
                    NewWelder.Weld(WeldedModelRenderer, SectionRenderers.ToArray(), true, _weldingPosThreshold, _showWeldedVertices, true, true);
                }
                else
                {
                    _liMeshWelder.Weld(WeldedModelRenderer, SectionRenderers.ToArray(), _weldingPosThreshold, _weldingAngleThreshold, true, _showWeldedVertices, _recalculateNormals);
                }
            }

            WeldedModelCollider.sharedMesh = WeldedModelRenderer.sharedMesh;

            if (!keepSegmentedModel)
            {
                _goFactory.Destroy(SegmentedModelGO.GetInstanceID());
            }
            IsWeldedModelGenerated = true;
        }

        /// <summary>
        /// Generates a combined mesh by baking and merging all section meshes.
        /// </summary>
        /// <returns>A single combined mesh from all sections.</returns>
        /// <remarks>
        /// Bakes each SkinnedMeshRenderer to capture current blendshape state,
        /// then combines into a single mesh without welding vertices.
        /// </remarks>
        private Mesh GenerateCombinedMesh()
        {
            var tempMesh = new Mesh();
            var bakedMeshes = new List<Mesh>();

            // Bake each skinned mesh to capture blendshape deformations
            foreach (var renderer in SectionRenderers)
            {
                tempMesh = new Mesh();
                renderer.BakeMesh(tempMesh);
                bakedMeshes.Add(tempMesh);
            }

            if (useOldCombineMeshesMethod)
            {
                return MeshUtils.CombineMeshesOld(bakedMeshes, false);
            }
            else
            {
                return MeshUtils.CombineMeshes(bakedMeshes, false);
            }
        }

        /// <summary>
        /// Generates a visual-only model (combined mesh without welding).
        /// </summary>
        /// <param name="keepSegmentedModel">If true, keeps the segmented model after generation.</param>
        /// <remarks>
        /// Creates a simpler combined mesh suitable for display purposes.
        /// Faster than welded model but may have visible seams.
        /// </remarks>
        public void GenerateVisualModel(bool keepSegmentedModel = false)
        {
            IsWeldedModelGenerated = false;
            if (SegmentedModelGO == null || !IsSegmentedModelGenerated)
            {
                GenerateSegmentedModel();
            }

            if (WeldedModelGO == null)
            {
                WeldedModelGO = GetOrCreateWeldedModelGo(_weldedModelGOName);
            }

            WeldedModelRenderer = WeldedModelGO.GetComponent<SkinnedMeshRenderer>();
            WeldedModelCollider = WeldedModelGO.GetComponent<MeshCollider>();

            WeldedModelRenderer.sharedMesh = GenerateCombinedMesh();
            WeldedModelRenderer.sharedMaterials = LargeIntestineSections[0].Renderer.sharedMaterials;
            NormalSolver2.RecalculateNormals(WeldedModelRenderer.sharedMesh, 180f);
            NormalSolver2.RecalculateTangents(WeldedModelRenderer.sharedMesh);

            WeldedModelCollider.sharedMesh = WeldedModelRenderer.sharedMesh;

            if (!keepSegmentedModel)
            {
                _goFactory.Destroy(SegmentedModelGO.GetInstanceID());
                IsSegmentedModelGenerated = false;
            }
            IsWeldedModelGenerated = true;
        }

        /// <summary>
        /// Initializes the spline from saved model data.
        /// </summary>
        /// <param name="model">The model containing spline node data.</param>
        public void InitializeSplineFromModelData(Model model)
        {
            if (model == null || model.SplineNodes == null)
            {
                Debug.LogError("Model or Spline data is null. Cannot initialize spline.");
                return;
            }

            EnsureSplineExists();
            SplineUtilities.ReplaceSplineNodes(Spline, model.SplineNodes, _splineSmoother);
        }

        #endregion

        #region Blendshape Queries

        /// <summary>
        /// Sets all blendshape weights to 0 on every section renderer. Useful for diagnostics.
        /// </summary>
        [ContextMenu("Zero All Blendshapes")]
        public void ZeroAllBlendshapes()
        {
            foreach (var renderer in SectionRenderers)
            {
                for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    renderer.SetBlendShapeWeight(i, 0f);
            }
        }

        /// <summary>
        /// Gets all blendshape dictionaries from all section renderers.
        /// </summary>
        /// <returns>List of dictionaries mapping blendshape names to weights.</returns>
        public List<SerializableDictionary<string, float>> GetRenderersBlendshapesDicts()
        {
            return LargeIntestineSections.Select(s => s.GetRendererBlendshapeDict()).ToList();
        }

        /// <summary>
        /// Calculates a global width factor based on the first section's width blendshape.
        /// </summary>
        /// <returns>A scale factor between 0.5 and 2.0 based on blendshape value.</returns>
        /// <remarks>
        /// TODO: UPDATE THIS FOR NEW MODELS - may need different calculation logic.
        /// </remarks>
        public float GetBlendshapeGlobalWidthFactor()
        {
            return 1f; // Placeholder until we have a new width blendshape to base this on
            var blendValue = LargeIntestineSections[0].GetBlendShapeWeight(generationConfiguration.globalWidthBlendshapeName);
            if (blendValue >= 50f)
            {
                return Mathf.Lerp(0.75f, 2f, blendValue / 100);
            }
            else
            {
                return Mathf.Lerp(0.75f, 0.5f, blendValue / 100);
            }
        }

        #endregion

        #region GameObject Factory Helpers

        /// <summary>
        /// Gets or creates a segment container GameObject.
        /// </summary>
        /// <param name="segmentName">Name of the anatomical segment (e.g., "Cecum", "Ascending").</param>
        /// <returns>The segment GameObject.</returns>
        private GameObject GetOrCreateSegmentGo(string segmentName)
        {
            return _goFactory.GetOrCreate(segmentName, SegmentedModelGO?.transform);
        }

        /// <summary>
        /// Gets or creates a section GameObject with required mesh components.
        /// </summary>
        /// <param name="sectionName">Name for the section.</param>
        /// <param name="parentTransform">Parent transform for hierarchy.</param>
        /// <returns>GameObject with MeshCollider, SkinnedMeshRenderer, and CustomMeshBender.</returns>
        private GameObject GetOrCreateSectionGo(string sectionName, Transform parentTransform = null)
        {
            var components = new[] { typeof(MeshCollider), typeof(SkinnedMeshRenderer), typeof(CustomMeshBender) };
            return _goFactory.GetOrCreate(sectionName, parentTransform, components);
        }

        /// <summary>
        /// Creates the welded model GameObject and initializes WeldedModelRenderer/WeldedModelCollider
        /// without generating the mesh. Used when loading a saved mesh into the renderer directly.
        /// </summary>
        public void InitializeWeldedModelGO()
        {
            if (WeldedModelGO == null)
                WeldedModelGO = GetOrCreateWeldedModelGo(_weldedModelGOName);
            WeldedModelRenderer = WeldedModelGO.GetComponent<SkinnedMeshRenderer>();
            WeldedModelCollider = WeldedModelGO.GetComponent<MeshCollider>();
        }

        /// <summary>
        /// Destroys the welded model GameObject and nulls the internal reference immediately,
        /// so GenerateWeldedModel will recreate it on next call.
        /// </summary>
        public void DestroyWeldedModelGO()
        {
            if (WeldedModelGO != null)
            {
                _goFactory.Destroy(WeldedModelGO);
                WeldedModelGO = null;
                WeldedModelRenderer = null;
                WeldedModelCollider = null;
            }
        }

        /// <summary>
        /// Gets or creates a welded model GameObject with rendering and collision components.
        /// </summary>
        /// <param name="weldedModelName">Name for the welded model.</param>
        /// <returns>GameObject with MeshCollider and SkinnedMeshRenderer.</returns>
        private GameObject GetOrCreateWeldedModelGo(string weldedModelName)
        {
            var components = new[] { typeof(MeshCollider), typeof(SkinnedMeshRenderer) };
            var go = _goFactory.GetOrCreate(weldedModelName, transform, components);
            go.layer = LayerMask.NameToLayer(_modelLayerMaskName);
            go.tag = _modelTagName;
            return go;
        }

        /// <summary>
        /// Gets or creates a basic GameObject without additional components.
        /// </summary>
        /// <param name="objectName">Name for the GameObject.</param>
        /// <param name="parentTransform">Optional parent transform.</param>
        /// <returns>A basic GameObject.</returns>
        private GameObject GetOrCreateGo(string objectName, Transform parentTransform = null)
        {
            return _goFactory.GetOrCreate(objectName, parentTransform);
        }

        #endregion

        #region Spline Operations

        /// <summary>
        /// Applies a preset configuration to the spline.
        /// </summary>
        /// <param name="splinePreset">The preset containing node positions and configurations.</param>
        public void ApplySplinePreset(SplinePreset splinePreset)
        {
            SplineUtilities.ApplySplinePreset(Spline, splinePreset);
        }

        /// <summary>
        /// Replaces all spline nodes with data from another source.
        /// </summary>
        /// <param name="sourceSplineNodes">List of spline nodes to copy from.</param>
        public void ReplaceSplineNodesData(List<SplineNode> sourceSplineNodes)
        {
            SplineUtilities.ReplaceSplineNodesData(Spline, sourceSplineNodes);
        }

        /// <summary>
        /// Updates the up vectors for all spline nodes.
        /// </summary>
        /// <remarks>
        /// Up vectors control the orientation of bent meshes along the spline.
        /// </remarks>
        public void UpdateUpVectors()
        {
            // Reset node 0 to a canonical up before parallel transport so that noise
            // applied in a previous call doesn't pollute the base for this call.
            if (Spline != null && Spline.nodes.Count > 0)
                Spline.nodes[0].Up = Vector3.up;
            SplineUtilities.UpdateUpVectors(Spline);
            SplineUtilities.ApplyUpVectorNoise(Spline, _noiseSettings);
        }

        #endregion

        #region Mesh Calculations

        /// <summary>
        /// Gets the world-space bounds for all intestine sections. 
        /// </summary>
        /// <returns>List of bounds, one per section, transformed to world space.</returns>
        public List<Bounds> GetIntestineSectionsBounds()
        {
            var renderers = LargeIntestineSections.Select(section => section.Renderer).ToList();
            var intestineSectionsBounds = new List<Bounds>(renderers.Select(r =>
            {
                r.sharedMesh.RecalculateBounds();

                // Get the mesh bounds in local space
                var meshBounds = r.sharedMesh.bounds;

                // Transform bounds to world space using welded model's transform
                var scale = WeldedModelRenderer.transform.lossyScale;
                meshBounds.center = WeldedModelRenderer.transform.TransformVector(meshBounds.center) + WeldedModelRenderer.transform.position;
                meshBounds.size = WeldedModelRenderer.transform.TransformVector(meshBounds.size);

                return meshBounds;
            }));
            return intestineSectionsBounds;
        }

        /// <summary>
        /// Triggers recalculation of all bent meshes.
        /// </summary>
        public void ComputeBendedMeshesRecalculations()
        {
            var bendersCount = _customMeshBenders.Count;
            for (int i = 0; i < bendersCount; i++)
            {
                _customMeshBenders[i].ComputeBendedMeshRecalculations();
            }
        }

        #endregion

        #region Persistence (Save/Load)

        /// <summary>
        /// Saves the current model state to a file.
        /// </summary>
        /// <remarks>
        /// Saves spline nodes, segment positions, and blendshape values.
        /// Requires either segmented or welded model to be generated first.
        /// </remarks>
        public void SaveModel()
        {
            if (IsSegmentedModelGenerated || IsWeldedModelGenerated)
            {
                // Create model with current state
                // Strip extension from LIConfigurationFile - Model stores filename only
                var configFileWithoutExtension = Path.GetFileNameWithoutExtension(LIConfigurationFile);
                var model = new Model(null,
                        null,
                        Spline.nodes,
                        SegmentsStartValueInSpline,
                        GetRenderersBlendshapesDicts(),
                        null,
                        configFileWithoutExtension);
                model.Save(null, null, _modelSavePath);
            }
            else
            {
                Debug.LogError("No generated model to save. Please generate a segmented or welded model first.");
            }
        }

        /// <summary>
        /// Loads a model from file and regenerates the mesh.
        /// </summary>
        /// <remarks>
        /// Restores spline configuration, loads generation settings, and regenerates the segmented model.
        /// </remarks>
        public void LoadModel()
        {
            // Load model data from file
            var model = new Model();
            model.ReadFromFile(_modelSavePath);

            // Restore configuration file reference
            LIConfigurationFile = model.GenerationConfigurationFile + FileManager.LIGenerationConfigurationFileExtension;
            generationConfiguration = new LI_GenerationConfiguration();
            generationConfiguration.ReadFromFile(Path.Combine(Application.streamingAssetsPath, LIConfigurationFile));

            // Initialize spline and regenerate model
            InitializeSplineFromModelData(model);
            GenerateSegmentedModel();
        }

        /// <summary>
        /// Updates the generation configuration with current blendshape values from the renderers
        /// and saves the configuration file.
        /// </summary>
        /// <remarks>
        /// <para>Reads the current blendshape weights from all section renderers and updates
        /// the corresponding sections in the generation configuration.</para>
        /// <para>The configuration file is then saved to StreamingAssets.</para>
        /// </remarks>
        public void UpdateConfigurationFromRenderers()
        {
            if (!IsSegmentedModelGenerated || LargeIntestineSections.Count == 0)
            {
                Debug.LogError("No generated model. Please generate a segmented model first.");
                return;
            }

            if (generationConfiguration == null)
            {
                Debug.LogError("Generation configuration is null. Please load a configuration first.");
                return;
            }

            // Flatten configuration sections to match renderer order
            var sectionConfigs = generationConfiguration.intestineSegments
                .SelectMany(segment => segment.liSections)
                .ToList();

            if (sectionConfigs.Count != LargeIntestineSections.Count)
            {
                Debug.LogError($"Section count mismatch: Configuration has {sectionConfigs.Count} sections, but there are {LargeIntestineSections.Count} renderers.");
                return;
            }

            // Update each section's blendshape values from the renderer
            for (int i = 0; i < LargeIntestineSections.Count; i++)
            {
                var rendererBlendshapes = LargeIntestineSections[i].GetRendererBlendshapeDict();
                sectionConfigs[i].blendshapeValues = rendererBlendshapes;
            }

            // Save the updated configuration
            var configPath = Path.Combine(Application.streamingAssetsPath, LIConfigurationFile);
            generationConfiguration.Save(destPath: configPath);

            Debug.Log($"[LargeIntestineModelGenerator] Updated and saved configuration to: {configPath}");
        }

        /// <summary>
        /// Applies anatomically-accurate blendshape distributions across all sections.
        /// Ensures continuity between adjacent meshes (_Inf of mesh i matches _Sup of mesh i+1).
        /// </summary>
        /// <remarks>
        /// <para>Distribution curves based on anatomical features:</para>
        /// <list type="bullet">
        ///   <item><b>Teniae Coli:</b> Absent in Rectum, low in Sigmoid, peaks in Transverse</item>
        ///   <item><b>Haustra:</b> Absent in Rectum, low in Sigmoid, peaks in Transverse/Ascending</item>
        ///   <item><b>Plicae Semilunares:</b> Absent in Rectum/Sigmoid, present Descending through Cecum</item>
        ///   <item><b>Ancho (Width):</b> Variable in Rectum, small in Sigmoid, asymmetric in Transverse, large in Cecum</item>
        /// </list>
        /// </remarks>
        public void ApplyAnatomicalDistribution()
        {
            if (!IsSegmentedModelGenerated || LargeIntestineSections.Count == 0)
            {
                Debug.LogError("No generated model. Please generate a segmented model first.");
                return;
            }

            // Flatten configuration to get segment info per section
            var sectionInfos = generationConfiguration.intestineSegments
                .SelectMany(segment => segment.liSections.Select(section => new { segment.liSegment, section }))
                .ToList();

            if (sectionInfos.Count != LargeIntestineSections.Count)
            {
                Debug.LogError($"Section count mismatch: {sectionInfos.Count} config vs {LargeIntestineSections.Count} renderers.");
                return;
            }

            int totalSections = LargeIntestineSections.Count;

            // Calculate boundary values - there are totalSections+1 boundaries (edges between/around sections)
            // Boundary i is at the "Sup" end of section i (and "Inf" end of section i-1)
            float[] teniaeValues = new float[totalSections + 1];
            float[] haustraValues = new float[totalSections + 1];
            float[] plicaeValues = new float[totalSections + 1];
            float[] anchoXValues = new float[totalSections + 1];
            float[] anchoYValues = new float[totalSections + 1];
            float[] deltasValues = new float[totalSections + 1];

            // Calculate boundary values with smooth interpolation
            for (int i = 0; i <= totalSections; i++)
            {
                float t = (float)i / totalSections; // 0 at Rectum end, 1 at Cecum end

                // Get interpolated anatomical factors based on position along colon
                GetInterpolatedAnatomicalFactors(t, out float teniaeFactor, out float haustraFactor,
                                                 out float plicaeFactor, out float anchoXFactor, out float anchoYFactor,
                                                 out float deltasFactor);

                teniaeValues[i] = Mathf.Lerp(_teniaeMin, _teniaeMax, teniaeFactor);
                haustraValues[i] = Mathf.Lerp(_haustraMin, _haustraMax, haustraFactor);
                plicaeValues[i] = Mathf.Lerp(_plicaeMin, _plicaeMax, plicaeFactor);
                anchoXValues[i] = Mathf.Lerp(_anchoMin, _anchoMax, anchoXFactor);
                anchoYValues[i] = Mathf.Lerp(_anchoMin, _anchoMax, anchoYFactor);
                deltasValues[i] = Mathf.Lerp(_deltasMin, _deltasMax, deltasFactor);
            }

            // Apply noise to create jumps in progression while maintaining boundary continuity
            if (_distributionNoiseStrength > 0.001f)
            {
                ApplyNoiseToDistribution(teniaeValues, totalSections, 0);
                ApplyNoiseToDistribution(haustraValues, totalSections, 1000);
                ApplyNoiseToDistribution(plicaeValues, totalSections, 2000);
                ApplyNoiseToDistribution(anchoXValues, totalSections, 3000);
                ApplyNoiseToDistribution(anchoYValues, totalSections, 4000);
                ApplyNoiseToDistribution(deltasValues, totalSections, 5000);
            }

            // Apply values to each section
            for (int i = 0; i < totalSections; i++)
            {
                var renderer = LargeIntestineSections[i].Renderer;
                var mesh = renderer.sharedMesh;

                // Boundary i is at the "Inf" end (toward Rectum, lower index)
                // Boundary i+1 is at the "Sup" end (toward Cecum, higher index)
                // Connection: section[i].Sup connects to section[i+1].Inf
                float supValue, medValue, infValue;

                // Inf connects to previous mesh's Sup (boundary i)
                infValue = teniaeValues[i];
                // Sup connects to next mesh's Inf (boundary i+1)
                supValue = teniaeValues[i + 1];
                // Med interpolates between
                medValue = (supValue + infValue) / 2f;

                SetBlendshapeIfExists(renderer, mesh, "Tenias_Sup", supValue);
                SetBlendshapeIfExists(renderer, mesh, "Tenias_Med", medValue);
                SetBlendshapeIfExists(renderer, mesh, "Tenias_Inf", infValue);

                // Haustra
                infValue = haustraValues[i];
                supValue = haustraValues[i + 1];
                SetBlendshapeIfExists(renderer, mesh, "Saculacion_sup", supValue);
                SetBlendshapeIfExists(renderer, mesh, "Saculación_Inf", infValue);

                // Plicae (Cierre_A) - single value per section, use average
                float plicaeValue = (plicaeValues[i] + plicaeValues[i + 1]) / 2f;
                SetBlendshapeIfExists(renderer, mesh, "Cierre_A", plicaeValue);

                // Ancho - width values
                float anchoXInf = anchoXValues[i];
                float anchoXSup = anchoXValues[i + 1];
                float anchoXMed = (anchoXSup + anchoXInf) / 2f;

                float anchoYInf = anchoYValues[i];
                float anchoYSup = anchoYValues[i + 1];
                float anchoYMed = (anchoYSup + anchoYInf) / 2f;

                SetBlendshapeIfExists(renderer, mesh, "Ancho_Sup_X", anchoXSup);
                SetBlendshapeIfExists(renderer, mesh, "Ancho_Sup_Y", anchoYSup);
                SetBlendshapeIfExists(renderer, mesh, "Ancho_Med_X", anchoXMed);
                SetBlendshapeIfExists(renderer, mesh, "Ancho_Med_Y", anchoYMed);
                SetBlendshapeIfExists(renderer, mesh, "Ancho_inf_X", anchoXInf);
                SetBlendshapeIfExists(renderer, mesh, "Ancho_inf_Y", anchoYInf);

                // Deltas (triangular cross-section) - single value per section, use average
                float deltasValue = (deltasValues[i] + deltasValues[i + 1]) / 2f;
                SetBlendshapeIfExists(renderer, mesh, "Deltas", deltasValue);
            }

            Debug.Log($"[LargeIntestineModelGenerator] Applied anatomical distribution to {totalSections} sections.");
        }

        /// <summary>
        /// Gets smoothly interpolated anatomical factors based on position along the entire colon (0-1).
        /// Calculates segment centers dynamically from the configuration.
        /// </summary>
        private void GetInterpolatedAnatomicalFactors(float t,
            out float teniaeFactor, out float haustraFactor, out float plicaeFactor,
            out float anchoXFactor, out float anchoYFactor, out float deltasFactor)
        {
            // Calculate segment centers from actual configuration
            var segmentRanges = new Dictionary<LI_Segment, (int start, int end)>();
            int currentIndex = 0;

            foreach (var segment in generationConfiguration.intestineSegments)
            {
                int sectionCount = segment.liSections.Count;
                segmentRanges[segment.liSegment] = (currentIndex, currentIndex + sectionCount - 1);
                currentIndex += sectionCount;
            }

            int totalSections = currentIndex;

            // Build arrays ordered by segment enum (Rectum=0 through Cecum=5)
            var segmentCentersList = new List<(float center, LI_Segment segment)>();

            for (int seg = 0; seg <= 5; seg++) // LI_Segment values 0-5
            {
                var segment = (LI_Segment)seg;
                if (segmentRanges.ContainsKey(segment))
                {
                    var range = segmentRanges[segment];
                    float center = ((range.start + range.end) / 2f) / totalSections;
                    segmentCentersList.Add((center, segment));
                }
            }

            float[] segmentCenters = segmentCentersList.Select(x => x.center).ToArray();

            // Characteristic values at each segment center (Rectum through Cecum)
            // Built based on the segments we actually have in the configuration
            var teniaeFactorsList = new List<float>();
            var haustraFactorsList = new List<float>();
            var plicaeFactorsList = new List<float>();
            var anchoXFactorsList = new List<float>();
            var anchoYFactorsList = new List<float>();
            var deltasFactorsList = new List<float>();

            foreach (var (center, segment) in segmentCentersList)
            {
                GetSegmentCharacteristicFactors(segment,
                    out float tFactor, out float hFactor, out float pFactor,
                    out float axFactor, out float ayFactor, out float dFactor);

                teniaeFactorsList.Add(tFactor);
                haustraFactorsList.Add(hFactor);
                plicaeFactorsList.Add(pFactor);
                anchoXFactorsList.Add(axFactor);
                anchoYFactorsList.Add(ayFactor);
                deltasFactorsList.Add(dFactor);
            }

            float[] teniaeFactors = teniaeFactorsList.ToArray();
            float[] haustraFactors = haustraFactorsList.ToArray();
            float[] plicaeFactors = plicaeFactorsList.ToArray();
            float[] anchoXFactors = anchoXFactorsList.ToArray();
            float[] anchoYFactors = anchoYFactorsList.ToArray();
            float[] deltasFactors = deltasFactorsList.ToArray();

            // Find which segment centers we're between and interpolate
            teniaeFactor = InterpolateAlongCurve(t, segmentCenters, teniaeFactors);
            haustraFactor = InterpolateAlongCurve(t, segmentCenters, haustraFactors);
            plicaeFactor = InterpolateAlongCurve(t, segmentCenters, plicaeFactors);
            anchoXFactor = InterpolateAlongCurve(t, segmentCenters, anchoXFactors);
            anchoYFactor = InterpolateAlongCurve(t, segmentCenters, anchoYFactors);
            deltasFactor = InterpolateAlongCurve(t, segmentCenters, deltasFactors);
        }

        /// <summary>
        /// Gets characteristic anatomical factors for a specific segment type.
        /// Evaluates from the AnimationCurves defined in the inspector.
        /// </summary>
        private void GetSegmentCharacteristicFactors(LI_Segment segment,
            out float teniaeFactor, out float haustraFactor, out float plicaeFactor,
            out float anchoXFactor, out float anchoYFactor, out float deltasFactor)
        {
            float segmentValue = (float)segment; // 0=Rectum through 5=Cecum

            teniaeFactor = _teniaeCurve.Evaluate(segmentValue);
            haustraFactor = _haustraCurve.Evaluate(segmentValue);
            plicaeFactor = _plicaeCurve.Evaluate(segmentValue);
            anchoXFactor = _anchoXCurve.Evaluate(segmentValue);
            anchoYFactor = _anchoYCurve.Evaluate(segmentValue);
            deltasFactor = _deltasCurve.Evaluate(segmentValue);
        }

        /// <summary>
        /// Initializes the characteristic curves with anatomically-accurate default values.
        /// Call this to reset curves to their default distribution.
        /// </summary>
        [ContextMenu("Reset Characteristic Curves to Defaults")]
        public void ResetCharacteristicCurvesToDefaults()
        {
            // Teniae: Absent in Rectum, low in Sigmoid, peaks at Transverse
            _teniaeCurve = new AnimationCurve(
                new Keyframe(0, 0f),      // Rectum
                new Keyframe(1, 0.25f),   // Sigmoid
                new Keyframe(2, 0.55f),   // Descending
                new Keyframe(3, 1f),      // Transverse (MAX)
                new Keyframe(4, 0.85f),   // Ascending
                new Keyframe(5, 0.9f)     // Cecum
            );

            // Haustra: Same pattern as teniae
            _haustraCurve = new AnimationCurve(
                new Keyframe(0, 0f),      // Rectum
                new Keyframe(1, 0.25f),   // Sigmoid
                new Keyframe(2, 0.55f),   // Descending
                new Keyframe(3, 1f),      // Transverse (MAX)
                new Keyframe(4, 0.85f),   // Ascending
                new Keyframe(5, 0.9f)     // Cecum
            );

            // Plicae: Absent in Rectum/Sigmoid, present Descending onward
            _plicaeCurve = new AnimationCurve(
                new Keyframe(0, 0f),      // Rectum
                new Keyframe(1, 0f),      // Sigmoid
                new Keyframe(2, 0.5f),    // Descending
                new Keyframe(3, 1f),      // Transverse (MAX)
                new Keyframe(4, 0.85f),   // Ascending
                new Keyframe(5, 0.9f)     // Cecum
            );

            // Ancho X: Variable in Rectum, small in Sigmoid, triangular in Transverse, wide in Cecum
            _anchoXCurve = new AnimationCurve(
                new Keyframe(0, 0.5f),    // Rectum (variable)
                new Keyframe(1, 0.15f),   // Sigmoid (narrow)
                new Keyframe(2, 0.4f),    // Descending
                new Keyframe(3, 0.6f),    // Transverse (triangular, wider X)
                new Keyframe(4, 0.7f),    // Ascending
                new Keyframe(5, 0.95f)    // Cecum (balloon, wide)
            );

            // Ancho Y: Similar but asymmetric in Transverse
            _anchoYCurve = new AnimationCurve(
                new Keyframe(0, 0.5f),    // Rectum (variable)
                new Keyframe(1, 0.15f),   // Sigmoid (narrow)
                new Keyframe(2, 0.4f),    // Descending
                new Keyframe(3, 0.35f),   // Transverse (triangular, narrower Y for asymmetry)
                new Keyframe(4, 0.65f),   // Ascending
                new Keyframe(5, 0.95f)    // Cecum (balloon, wide)
            );

            // Deltas: Absent in Rectum/Sigmoid, maximum in Transverse, low in Cecum
            _deltasCurve = new AnimationCurve(
                new Keyframe(0, 0f),      // Rectum (no triangle)
                new Keyframe(1, 0f),      // Sigmoid (circular)
                new Keyframe(2, 0.2f),    // Descending (fading)
                new Keyframe(3, 1f),      // Transverse (MAXIMUM triangle)
                new Keyframe(4, 0.5f),    // Ascending (moderate)
                new Keyframe(5, 0.25f)    // Cecum (low, balloon shape)
            );

            Debug.Log("[LargeIntestineModelGenerator] Reset characteristic curves to anatomical defaults.");
        }

        /// <summary>
        /// Interpolates a value along a curve defined by control points.
        /// </summary>
        private float InterpolateAlongCurve(float t, float[] positions, float[] values)
        {
            if (t <= positions[0])
                return values[0];
            if (t >= positions[positions.Length - 1])
                return values[values.Length - 1];

            // Find the two control points we're between
            for (int i = 0; i < positions.Length - 1; i++)
            {
                if (t >= positions[i] && t <= positions[i + 1])
                {
                    float localT = (t - positions[i]) / (positions[i + 1] - positions[i]);
                    // Use smoothstep for smoother transitions
                    localT = localT * localT * (3f - 2f * localT);
                    return Mathf.Lerp(values[i], values[i + 1], localT);
                }
            }

            return values[values.Length - 1];
        }

        /// <summary>
        /// Applies noise to boundary values to create non-continuous jumps in the progression.
        /// Example: Instead of smooth [0.1, 0.15, 0.2, 0.25, 0.3] you get [0.1, 0.15, 0.25, 0.20, 0.30]
        /// Maintains geometric continuity because section[i].Sup == section[i+1].Inf (same boundary).
        /// </summary>
        /// <param name="values">Array of boundary values to modify.</param>
        /// <param name="totalSections">Total number of sections in the model.</param>
        /// <param name="seedOffset">Offset added to base seed for different noise patterns per feature.</param>
        private void ApplyNoiseToDistribution(float[] values, int totalSections, int seedOffset)
        {
            if (_distributionNoiseStrength < 0.001f)
                return;

            float baseSeed = _distributionNoiseSeed + seedOffset;

            // Calculate the overall range for this feature to determine safe variation amount
            float minValue = float.MaxValue;
            float maxValue = float.MinValue;
            for (int i = 0; i <= totalSections; i++)
            {
                minValue = Mathf.Min(minValue, values[i]);
                maxValue = Mathf.Max(maxValue, values[i]);
            }
            float range = maxValue - minValue;

            // Apply noise to each boundary value
            for (int i = 0; i <= totalSections; i++)
            {
                // Sample Perlin noise using boundary index
                float noiseInput = i * _distributionNoiseScale * 0.2f + baseSeed;
                float noise = Mathf.PerlinNoise(noiseInput, baseSeed * 0.1f);

                // Remap noise from [0,1] to [-1,1] for bidirectional variation
                noise = (noise - 0.5f) * 2f;

                // Calculate variation amount scaled by the feature's range
                // This creates jumps proportional to the overall growth
                float variationAmount = noise * _distributionNoiseStrength * range * 0.5f;

                // Apply noise and clamp to valid range [0, 100]
                values[i] = Mathf.Clamp(values[i] + variationAmount, 0f, 100f);
            }
        }

        /// <summary>
        /// Sets a blendshape weight if it exists on the mesh.
        /// </summary>
        private void SetBlendshapeIfExists(SkinnedMeshRenderer renderer, Mesh mesh, string blendshapeName, float weight)
        {
            int index = mesh.GetBlendShapeIndex(blendshapeName);
            if (index >= 0)
            {
                renderer.SetBlendShapeWeight(index, weight);
            }
        }

        #endregion
    }
}