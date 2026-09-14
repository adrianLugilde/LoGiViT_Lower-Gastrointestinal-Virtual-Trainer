using GeometryUtils;
using LI.MeshTools;
using Newtonsoft.Json;
using SplineMesh;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;


namespace LargeIntestine
{
    public class LI_ModelGenerator : MonoBehaviour
    {
        public bool useScale = true;
        public Vector3 scaleFactor = Vector3.one;
        [Header("Variables required to generate the model")]
        public GameObject defaultSpline;
        public List<BlendshapesWeights> renderersBlendshapesWeights;
        public List<SubmodelType> submodelsTypes;
        public string modelLayerMaskName = "Default";
        [Header("Variables required for the mesh welder")]
        public float maxPosDelta = 1e-6f;
        public float maxAngleDelta = 5f;
        public bool showWeldedVertices = false;
        public bool recalculateNormals = true;
        [Header("Variables of the generated model")]
        public Spline Spline;
        [HideInInspector] public int SplineNodeCount { get; private set; }
        public List<float> segmentsStartValueInSpline;
        public List<SkinnedMeshRenderer> sectionRenderers;
        public List<MeshCollider> sectionsColliders;
        public LI_GenerationConfiguration liGenerationConfiguration;
        public WelderTest welderTest;
        public List<CustomMeshBender> customMeshBenderList;
        public bool useWelderTest = false;
        public bool combineMeshes = false;
        public string saveAIModelfilePath = "Assets/GeneratedModel.json";

        [HideInInspector] public static LI_ModelGenerator sharedInstance;

        private MeshCollider weldedModelCollider;
        private bool parallel = true;

        public MeshCollider WeldedModelCollider
        {
            get
            {
                if (weldedModelCollider == null)
                {
                    weldedModelCollider = WeldedModelGO.GetComponent<MeshCollider>();
                }
                return weldedModelCollider;
            }
            private set
            {
                weldedModelCollider = value;
            }
        }

        private SkinnedMeshRenderer weldedModelRenderer;
        public SkinnedMeshRenderer WeldedModelRenderer
        {
            get
            {
                if (weldedModelRenderer == null)
                {
                    weldedModelRenderer = WeldedModelGO.GetComponent<SkinnedMeshRenderer>();
                }
                return weldedModelRenderer;
            }
            private set
            {
                weldedModelRenderer = value;
            }
        }

        private SkinnedMeshRenderer visualModelRenderer;
        public SkinnedMeshRenderer VisualModelRenderer
        {
            get
            {
                if (visualModelRenderer == null)
                {
                    visualModelRenderer = VisualModelGO.GetComponent<SkinnedMeshRenderer>();
                }
                return visualModelRenderer;
            }
            private set
            {
                visualModelRenderer = value;
            }
        }

        private GameObject weldedModelGO;
        public GameObject WeldedModelGO
        {
            get
            {
                if (weldedModelGO == null)
                {
                    weldedModelGO = CommonUtils.Create("WeldedModelGO", modelLayerMaskName, gameObject, new Type[] { typeof(SkinnedMeshRenderer), typeof(MeshCollider) });
                }
                return weldedModelGO;
            }
            private set
            {
                weldedModelGO = value;
            }
        }

        private GameObject visualModelGO;
        public GameObject VisualModelGO
        {
            get
            {
                if (visualModelGO == null)
                {
                    visualModelGO = CommonUtils.Create("VisualModelGO", modelLayerMaskName, gameObject, new Type[] { typeof(SkinnedMeshRenderer) });
                }
                return visualModelGO;
            }
            private set
            {
                visualModelGO = value;
            }
        }

        private GameObject segmentedModelGO;
        public GameObject SegmentedModelGO
        {
            get
            {
                if (segmentedModelGO == null)
                {
                    segmentedModelGO = CommonUtils.Create("SegmentedModelGO", gameObject);
                }
                return segmentedModelGO;
            }
            private set
            {
                segmentedModelGO = value;
            }
        }

        [Header("Spline preset variables")]
        public int startNodeIdx;
        public int endNodeIdx;
        public int presetFileID;

        [Header("Debug variables")]
        public bool IsInitialized {get; private set;} = false;
        public bool ModelGenerated {get; private set;} = false;
        public float splineScaleFactor = 1f;
        public bool useParallelBender = false;
        public bool useParallelWeld = false;
        public float splineRotation = 0f;


        public SplineSmoother splineSmoother;
        private LI_MeshWelder liMeshWelder;
        private Dictionary<string, Mesh> sectionMeshesDict;
        private Dictionary<string, Material> sectionMaterialsDict;
        private int splineNodeIndex = 0;
        private float currentSegmentLenght = 0f;

        public string llmFilePath = "";
        public string datasetFilePath = "";
        public int datasetEntryRecord = 0;

        private void Awake()
        {
            if (sharedInstance == null)
            {
                sharedInstance = this;
            }
            else
            {
                Destroy(gameObject);
            }
            if (Spline == null) Spline = CommonUtils.Instantiate(defaultSpline, transform, null, true).GetComponent<Spline>();
            splineSmoother = Spline.GetComponent<SplineSmoother>();

            liMeshWelder = new LI_MeshWelder();
            sectionMeshesDict = new Dictionary<string, Mesh>();
            sectionMaterialsDict = new Dictionary<string, Material>();
            submodelsTypes = new List<SubmodelType>();
            segmentsStartValueInSpline = new List<float>();
            IsInitialized = true;
        }

        private void Start()
        {

        }

        public void Reset()
        {
            UOUtility.Destroy(segmentedModelGO);
            UOUtility.Destroy(Spline.gameObject);
        }

        public void InitializeSplineFromModelData(Model training)
        {
            if (Spline == null) Spline = CommonUtils.Instantiate(defaultSpline, transform, null, true).GetComponent<Spline>();//redundant, fix this
            ReplaceSplineNodes(training.SplineNodes);
            //renderersBlendshapesWeights = training.renderersBlendshapesDicts;
        }

        public void ReplaceSplineNodes(List<SplineNode> newSplineNodes)
        {
            if (!splineSmoother) splineSmoother = Spline.GetComponent<SplineSmoother>();
            splineSmoother.enabled = false;
            Spline.nodes.Clear();
            Spline.curves.Clear();
            foreach (var sourceSplineNode in newSplineNodes)
            {
                Spline.AddNode(sourceSplineNode);
            }
            Spline.RefreshCurves();
            splineSmoother.enabled = true;
        }

        private void ResetGenerationVariables()
        {
            ModelGenerated = false;
            splineNodeIndex = 0;
            currentSegmentLenght = 0f;
            sectionRenderers.Clear();
            sectionsColliders.Clear();
            customMeshBenderList.Clear();
            submodelsTypes.Clear();
            segmentsStartValueInSpline.Clear();
        }


        public void GenerateSegmentedModel()
        {
            if (Spline == null) Spline = CommonUtils.Instantiate(defaultSpline, transform, null, true).GetComponent<Spline>();
            SplineNodeCount = Spline.nodes.Count;
            ResetGenerationVariables();
            foreach (var intestineSegment in liGenerationConfiguration.intestineSegments)
            {
                segmentsStartValueInSpline.Add(currentSegmentLenght);
                GenerateIntestineSegment(intestineSegment);
            }
            ModelGenerated = true;
            SegmentedModelGO.transform.localScale = scaleFactor;
            /*TransformSpline();
            for(int i = 0; i < customMeshBenderList.Count; i++)
            {
                customMeshBenderList[i].SetInterval(Spline.GetCurve(i));
            }*/
        }

        public void TransformSpline()
        {
            // Get the transform of the holder object (this gameObject)
            var holderTransform = transform;
            // Create array to store new transformed spline nodes
            SplineNode[] transformedNodes = new SplineNode[Spline.nodes.Count];

            for (int i = 0; i < Spline.nodes.Count; i++)
            {
                var originalNode = Spline.nodes[i];

                // Transform position considering scale, rotation, and position
                Vector3 scaledPosition = useScale ? Vector3.Scale(originalNode.Position, scaleFactor) : originalNode.Position;
                Vector3 transformedPosition = holderTransform.TransformPoint(scaledPosition);

                // Transform direction considering scale and rotation (but not position)
                Vector3 scaledDirection = useScale ? Vector3.Scale(originalNode.Direction, scaleFactor) : originalNode.Direction;
                Vector3 transformedDirection = holderTransform.TransformDirection(scaledDirection);

                // Transform up vector considering rotation only (up vectors shouldn't be scaled)
                Vector3 transformedUp = holderTransform.TransformDirection(originalNode.Up);

                // Apply scale to the node's scale property
                Vector3 transformedScale = useScale ? Vector3.Scale(originalNode.Scale, scaleFactor) : originalNode.Scale;

                // Create new transformed node
                transformedNodes[i] = new SplineNode(transformedPosition, transformedDirection)
                {
                    Up = transformedUp.normalized, // Normalize to maintain proper orientation
                    Scale = originalNode.Scale // Keep original scale, as it is not affected by transformation
                };
            }

            ReplaceSplineNodes(transformedNodes.ToList());
        }
        public void GenerateWeldedModel(bool withoutSegmentedModel = false, bool withVisualModel = false)
        {
            if (segmentedModelGO == null) GenerateSegmentedModel();
            if (liMeshWelder == null) liMeshWelder = new LI_MeshWelder();
            if (useWelderTest)
            {
                CommonUtils.TestMethod(() => liMeshWelder.WelderTest(WeldedModelRenderer, sectionRenderers.ToArray(), maxPosDelta, welderTest, true, showWeldedVertices));

            }
            else if (combineMeshes)
            {
                var combinedMesh = new Mesh();
                var meshes = sectionRenderers.Select(m =>
                {
                    var mesh = new Mesh();
                    m.BakeMesh(mesh);
                    return mesh;
                }
                ).ToList();
                CommonUtils.TestMethod(() => combinedMesh = MeshUtils.CombineMeshes(meshes, false));
                if (recalculateNormals)
                {
                    NormalSolver.AdvancedRecalculateNormals(combinedMesh, maxAngleDelta, true);
                    //combinedMesh = MeshUtils.SimplifyMeshVertexCount(combinedMesh, 75000);
                }
                WeldedModelRenderer.sharedMesh = combinedMesh;
                weldedModelRenderer.sharedMaterials = sectionRenderers[0].sharedMaterials;
            }
            else
            {
                if (useParallelWeld)
                {
                    CommonUtils.TestMethod(() => liMeshWelder.Weld(WeldedModelRenderer, sectionRenderers.ToArray(), maxPosDelta, maxAngleDelta, true, showWeldedVertices, recalculateNormals));

                    //CommonUtils.TestMethod(() => liMeshWelder.ParallelWeld(WeldedModelRenderer, sectionRenderers.ToArray(), maxPosDelta, maxAngleDelta, true, showWeldedVertices));
                }
                else
                {
                    CommonUtils.TestMethod(() => NewWelder.Weld(WeldedModelRenderer, sectionRenderers.ToArray(), true, maxPosDelta, showWeldedVertices, true, true));
                }
            }
            WeldedModelCollider.sharedMesh = WeldedModelRenderer.sharedMesh;
            if (withVisualModel)
            {
                GenerateVisualMesh();
                WeldedModelRenderer.sharedMesh = VisualModelRenderer.sharedMesh;
                WeldedModelCollider.sharedMesh = weldedModelRenderer.sharedMesh;
                //weldedModelRenderer.enabled = false;
                Destroy(visualModelGO);
            }
            if (withoutSegmentedModel) Destroy(SegmentedModelGO);
            /*var c = WeldedModelGO.AddComponent<Cloth>();
            c.useGravity = false;
            c.bendingStiffness = 1;
            c.damping = 0;*/
            ModelGenerated = true;
        }

        private void GenerateVisualMesh()
        {
            var tempMesh = new Mesh();
            var bakedMeshes = new List<Mesh>();
            foreach (var renderer in sectionRenderers)
            {
                tempMesh = new Mesh();
                renderer.BakeMesh(tempMesh);
                bakedMeshes.Add(tempMesh);
            }
            VisualModelRenderer.sharedMesh = MeshUtils.CombineMeshes(bakedMeshes, false);
            VisualModelRenderer.sharedMaterials = weldedModelRenderer.sharedMaterials;
        }

        public List<Bounds> GetIntestineSectionsBounds()
        {
            var intestineSectionsBounds = new List<Bounds>(sectionRenderers.Select(r =>
            {
                r.sharedMesh.RecalculateBounds();

                // Get the mesh bounds
                var meshBounds = r.sharedMesh.bounds;
                
                // Apply the renderer's transform scale to the bounds
                var scale = WeldedModelRenderer.transform.lossyScale;
                //meshBounds.size = Vector3.Scale(meshBounds.size, scale);
                //meshBounds.center = Vector3.Scale(meshBounds.center, scale);

                meshBounds.center = WeldedModelRenderer.transform.TransformVector(meshBounds.center) + WeldedModelRenderer.transform.position;
                meshBounds.size = WeldedModelRenderer.transform.TransformVector(meshBounds.size);


                return meshBounds;
            }));
            return intestineSectionsBounds;
        }

        public Vector3 GetSplineNodeTransformedPosition(int nodeIdx)
        {
            if (nodeIdx < 0 || nodeIdx >= Spline.nodes.Count) return Vector3.zero;
            return Spline.transform.TransformPoint(Spline.nodes[nodeIdx].Position * transform.localScale.x + transform.position);
        }

        /*public void CreateLGIEModel(Model training)
        {
            var lgieRotation = Quaternion.Euler(new Vector3(90f, 0, 0));
            EndoscopeSpline.sharedInstance.ControlledStart();
            InitializeSplineFromModelData(training);
            Spline.transform.rotation = lgieRotation;
            EndoscopeSpline.sharedInstance.ControlledStart();
            if (training.mesh != null && training.mesh.vertices.Length > 0)
            {
                training.mesh.AssignToRenderer(WeldedModelRenderer);
            }
            else
            {
                GenerateWeldedModel();
                Destroy(SegmentedModelGO);
            }
            WeldedModelGO.transform.rotation = lgieRotation;
        }*/

        private void GenerateIntestineSegment(LI_SegmentConfiguration liSegmentConfiguration)
        {
            var segmentGo = FindOrCreateSegmentGo(liSegmentConfiguration.liSegment.ToString() + " segment");
            for (int i = 0; i < liSegmentConfiguration.liSections.Count; i++)
            {
                if (Spline.nodes.Count - 1 < splineNodeIndex) return;
                GenerateSegmentSection(segmentGo, liSegmentConfiguration, i);
                splineNodeIndex++;
                currentSegmentLenght += Spline.curves[splineNodeIndex - 1].Length;
            }
        }

        //TODO REPLACE RESOURCE LOAD WITH ASSIGNMENT IN THE EDITOR
        private void GenerateSegmentSection(GameObject parentSegmentGo, LI_SegmentConfiguration liSegmentConfiguration, int sectionIdxInSegment)
        {
            /*var liSectionConfiguration = liSegmentConfiguration.liSections[sectionIdxInSegment];
            if (!sectionMeshesDict.TryGetValue(liSectionConfiguration.meshName, out var sectionMesh))
            {
                sectionMesh = Resources.Load<Mesh>(Path.Combine(FileManager.meshesResourcesPath, liSectionConfiguration.meshName));
                sectionMeshesDict[liSectionConfiguration.meshName] = sectionMesh;
            }

            switch (sectionMesh.name)
            {
                case "rectum":
                    submodelsTypes.Add(SubmodelType.Rectum);
                    break;
                case "cecum":
                    submodelsTypes.Add(SubmodelType.Cecum);
                    break;
                default:
                    submodelsTypes.Add(SubmodelType.Default);
                    break;
            }

            var sectionGo = FindOrCreateSectionGo("section_" + splineNodeIndex, parentSegmentGo);
            ConfigureSectionMeshBender(sectionGo, sectionMesh);
            var renderer = sectionGo.GetComponent<SkinnedMeshRenderer>();
            var collider = sectionGo.GetComponent<MeshCollider>();

            var materials = new Material[liSectionConfiguration.materialsNames.Length];
            for (int i = 0; i < liSectionConfiguration.materialsNames.Length; i++)
            {
                {
                    var materialName = liSectionConfiguration.materialsNames[i];
                    if (!sectionMaterialsDict.TryGetValue(materialName, out var sectionMaterial))
                    {
                        sectionMaterial = Resources.Load<Material>(Path.Combine(FileManager.materialResourcesPath, materialName));
                        sectionMaterialsDict[materialName] = sectionMaterial;
                    }
                    materials[i] = sectionMaterial;
                }
            }
            renderer.sharedMaterials = materials;

            sectionRenderers.Add(renderer);
            sectionsColliders.Add(collider);


            var rendererBlendshapeWeights = renderersBlendshapesWeights[splineNodeIndex].weightsPerBlendshape;
            for (int j = 0; j < rendererBlendshapeWeights.Count; j++)
            {
                renderer.SetBlendShapeWeight(j, rendererBlendshapeWeights[j]);
            }*/
        }

        private void ConfigureSectionMeshBender(GameObject sectionGo, Mesh sectionMesh)
        {
            if (parallel)
            {
                var meshBender = sectionGo.GetComponent<CustomMeshBender>();
                meshBender.SetInterval(Spline.GetCurve(splineNodeIndex));
                var meshData = new MeshData(sectionMesh,
                    Vector3.zero,
                    Quaternion.Euler(Vector3.zero),
                    Vector3.one,
                    true);
                meshData.BuildData();
                meshBender.MeshData = meshData;
                meshBender.Configure();
                customMeshBenderList.Add(meshBender);
            }
            else
            {
                var meshBender = sectionGo.GetComponent<MeshBender>();
                meshBender.useParallelBender = useParallelBender;
                meshBender.SetInterval(Spline.GetCurve(splineNodeIndex));
                meshBender.Source = SourceMesh.Build(sectionMesh)
                    .Translate(Vector3.zero)
                    .Rotate(Quaternion.Euler(Vector3.zero))
                    .Scale(Vector3.one);
                meshBender.Mode = MeshBender.FillingMode.StretchToInterval;
                meshBender.ComputeIfNeeded();
            }
        }

        public void ForceBendingCompute()
        {
            foreach (var bender in customMeshBenderList)
            {
                bender.ForceCompute();
            }
        }

        public void ComputeBendedMeshesRecalculations()
        {
            var bendersCount = customMeshBenderList.Count;
            for (int i = 0; i < bendersCount; i++)
            {
                customMeshBenderList[i].ComputeBendedMeshRecalculations();
            }
        }

        public void ComputeBendedMeshesRecalculations(SplinePreset splinePreset)
        {
            var bendersCount = customMeshBenderList.Count;
            for (int i = 0; i < splinePreset.modifiedNodes.Count; i++)
            {
                customMeshBenderList[splinePreset.startNodeIdx + i].ComputeBendedMeshRecalculations();
            }
        }

        public void UpdateBlenshapeWeightsFromRenderers()
        {
            for (int i = 0; i < sectionRenderers.Count; i++)
            {
                renderersBlendshapesWeights[i] = renderersBlendshapesWeights[i].SetValues(sectionRenderers[i]);
            }
        }


        #region FIND OR CREATE METHODS

        private void FindOrCreateHolder()
        {
            string holderName = "SegmentedModelGO";
            var holderTransform = transform.Find(holderName);
            segmentedModelGO = holderTransform != null ? holderTransform.gameObject : UOUtility.Create(holderName, Spline.gameObject);
        }

        private GameObject FindOrCreateSegmentGo(string segmentName)
        {
            var childTransform = SegmentedModelGO.transform.Find(segmentName);
            GameObject res;
            if (childTransform == null)
            {
                res = UOUtility.Create(segmentName, segmentedModelGO);
            }
            else
            {
                res = childTransform.gameObject;
            }
            return res;
        }

        private GameObject FindOrCreateSectionGo(string sectionName, GameObject parentSegmentGo)
        {
            GameObject res;
            List<Type> components = null;
            if (parallel)
            {
                components = new List<Type> { typeof(MeshCollider), typeof(CustomMeshBender), };
            }
            else
            {
                components = new List<Type> { typeof(MeshCollider), typeof(MeshBender) };
            }
            //List<Type> components = new List<Type> {typeof(MeshBender), /*typeof(SkinnedMeshRenderer)*/};
            var childTransform = parentSegmentGo.transform.Find(sectionName);
            if (childTransform == null)
            {
                res = UOUtility.Create(sectionName, parentSegmentGo, components.ToArray());
            }
            else
            {
                res = childTransform.gameObject;
            }
            return res;
        }
        #endregion

        public void SaveGenerationConfiguration()
        {
            var tempLigc = new LI_GenerationConfiguration();
            int sectionCounter = 0;
            foreach (var liSegment in liGenerationConfiguration.intestineSegments)
            {
                var tempSegment = new LI_SegmentConfiguration(liSegment);
                for (int i = 0; i < liSegment.liSections.Count; i++)
                {
                    var tempSection = tempSegment.liSections[i];
                    //tempSection.blendshapesWeights = renderersBlendshapesWeights[sectionCounter++].weightsPerBlendshape;
                    tempSegment.liSections[i] = tempSection;
                }
                tempLigc.intestineSegments.Add(tempSegment);
            }
            liGenerationConfiguration = tempLigc;
            liGenerationConfiguration.Save();
        }

        public void LoadGenerationConfiguration(string path = null)
        {
            liGenerationConfiguration = new LI_GenerationConfiguration();
            liGenerationConfiguration.ReadFromFile(path);
            sectionMeshesDict = new Dictionary<string, Mesh>();
            sectionMaterialsDict = new Dictionary<string, Material>();
            renderersBlendshapesWeights = new List<BlendshapesWeights>();
            foreach (var liSegment in liGenerationConfiguration.intestineSegments)
            {
                foreach (var liSection in liSegment.liSections)
                {
                    //renderersBlendshapesWeights.Add(new BlendshapesWeights().SetValues(liSection.blendshapesWeights));
                }
            }
        }

        public void ChangeRendererBlendshapes()
        {
            for (int i = 1; i < sectionRenderers.Count - 1; i++)
            {
                var renderer = sectionRenderers[i];
                renderer.SetBlendShapeWeight(0, renderer.GetBlendShapeWeight(0) + renderer.GetBlendShapeWeight(6));
                renderer.SetBlendShapeWeight(1, renderer.GetBlendShapeWeight(1) + renderer.GetBlendShapeWeight(6));
                renderer.SetBlendShapeWeight(2, renderer.GetBlendShapeWeight(2) + renderer.GetBlendShapeWeight(7));
                renderer.SetBlendShapeWeight(3, renderer.GetBlendShapeWeight(3) + renderer.GetBlendShapeWeight(7));
                renderer.SetBlendShapeWeight(4, renderer.GetBlendShapeWeight(2) + renderer.GetBlendShapeWeight(8));
                renderer.SetBlendShapeWeight(5, renderer.GetBlendShapeWeight(3) + renderer.GetBlendShapeWeight(8));

                renderer.SetBlendShapeWeight(6, 0);
                renderer.SetBlendShapeWeight(7, 0);
                renderer.SetBlendShapeWeight(8, 0);

            }
        }

        public void ScaleSpline()
        {
            if (Spline != null) DestroyImmediate(Spline.gameObject);
            Spline = CommonUtils.Instantiate(defaultSpline, transform.parent, "ScaledSpline", true).GetComponent<Spline>();
            var splineNodes = new List<SplineNode>();
            foreach (var sourceSplineNode in Spline.nodes)
            {
                var splineNode = new SplineNode(sourceSplineNode.Position * splineScaleFactor, sourceSplineNode.Direction * splineScaleFactor);
                //splineNode.Up *= splineScaleFactor;
                splineNodes.Add(splineNode);
            }
            Spline.nodes.Clear();
            foreach (var sn in splineNodes)
            {
                Spline.AddNode(sn);
            }

            Spline.curves.Clear();
            Spline.RefreshCurves();
#if UNITY_EDITOR
            string path = EditorUtility.SaveFilePanelInProject("Save Spline Asset", "ScaledSpline", "asset", "Please enter a file name to save the spline asset to");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(Spline, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Spline asset saved at: {path}");
            }
#endif
        }

        public void RotateSpline()
        {
            if (Spline != null) DestroyImmediate(Spline.gameObject);
            Spline = CommonUtils.Instantiate(defaultSpline, transform.parent, "FlippedSpline", true).GetComponent<Spline>();
            var newSplineNodes = new List<SplineNode>();
            foreach (var sourceSplineNode in Spline.nodes)
            {
                var newSplineNode = new SplineNode(Quaternion.AngleAxis(splineRotation, Vector3.up) * sourceSplineNode.Position, Quaternion.AngleAxis(splineRotation, Vector3.up) * sourceSplineNode.Direction);
                newSplineNodes.Add(newSplineNode);
            }
            Spline.nodes.Clear();
            foreach (var sn in newSplineNodes)
            {
                Spline.AddNode(sn);
            }

            Spline.curves.Clear();
            Spline.RefreshCurves();
        }

        public void UpdateUpSplineNodesVectors()
        {
            var centroid = Utilities.MathUtils.GetCentroid(Spline.nodes.Select(n => n.Position));
            //MeshVerticesUtils.DrawVerticesAsSpheres(new List<Vector3>() { centroid }, 0.1f);
            foreach (var node in Spline.nodes)
            {
                node.Up = centroid - node.Position;
            }

            /*for(int i = 0; i < spline.nodes.Count; i++)
            {
                var node = spline.nodes[i];
                if(i == 0)
                {
                    node.Up = new Vector3(-0.01604187f, 0.02258003f, 0.007251084f) * 10;
                } else
                {
                    if(spline.nodes[i-1].Position.y > node.Position.y) node.Up = new Vector3(-0.01604187f, 0.02258003f, 0.007251084f) * -10;
                    else node.Up = new Vector3(-0.01604187f, 0.02258003f, 0.007251084f) * 10;
                }
            }*/

            /*foreach (var node in spline.nodes)
            {
                node.Up = new Vector3(-0.01604187f, 0.02258003f, 0.007251084f);
            }
            MeshVerticesUtils.DrawVerticesAsSpheres(new List<Vector3>() { new Vector3(-0.01604187f, 0.02258003f, 0.007251084f) }, 0.1f);
            */

        }

        public void SaveSplinePreset()
        {
            /*var splinePreset = new SplinePreset();
            splinePreset.Build(spline.nodes.GetRange(startNodeIdx, endNodeIdx), startNodeIdx);
            splinePreset.Save();*/
        }

        public void ApplySplinePreset(SplinePreset splinePreset)
        {
            for (int i = 0; i < splinePreset.modifiedNodes.Count; i++)
            {
                Spline.nodes[splinePreset.startNodeIdx + i].ReplaceData(splinePreset.modifiedNodes[i]);
            }
            ComputeBendedMeshesRecalculations(splinePreset);
        }

        public void ReplaceSplineNodesData(List<SplineNode> presetSplineNodes)
        {
            for (int i = 0; i < presetSplineNodes.Count; i++)
            {
                Spline.nodes[i].ReplaceData(presetSplineNodes[i]);
            }
            ComputeBendedMeshesRecalculations();
        }

        public void LoadSplinePreset()
        {
            var splinePreset = new SplinePreset();
            //splinePreset.ReadFrom();
            //Debug.Log(splinePreset.modifiedNodes.Count);
            for (int i = 0; i < splinePreset.modifiedNodes.Count; i++)
            {
                var oldNode = Spline.nodes[splinePreset.startNodeIdx + i];
                var newNode = splinePreset.modifiedNodes[i];
                oldNode.Position = newNode.Position;
                oldNode.Direction = newNode.Direction;
                oldNode.Up = newNode.Up;
                oldNode.Scale = newNode.Scale;
                //modelEditorMainController.splineNodeInteractables[(i - 1) + splinePreset.startNodeIdx].transform.position = newNode.Position;
            }
        }

        public void ShapesModification()
        {
            foreach (var smr in sectionRenderers)
            {
                /*smr.SetBlendShapeWeight(0, Mathf.Clamp(smr.GetBlendShapeWeight(0) - 40f, 0f, 100f));
                smr.SetBlendShapeWeight(1, Mathf.Clamp(smr.GetBlendShapeWeight(1) - 40f, 0f, 100f));
                smr.SetBlendShapeWeight(2, Mathf.Clamp(smr.GetBlendShapeWeight(2) - 40f, 0f, 100f));
                smr.SetBlendShapeWeight(3, Mathf.Clamp(smr.GetBlendShapeWeight(3) - 40f, 0f, 100f));
                smr.SetBlendShapeWeight(4, Mathf.Clamp(smr.GetBlendShapeWeight(4) - 50f, 0f, 100f));
                smr.SetBlendShapeWeight(5, Mathf.Clamp(smr.GetBlendShapeWeight(5) - 50f, 0f, 100f));*/
                smr.SetBlendShapeWeight(0, smr.GetBlendShapeWeight(0) * 0.8f);
                smr.SetBlendShapeWeight(1, smr.GetBlendShapeWeight(1) * 0.8f);
                smr.SetBlendShapeWeight(2, smr.GetBlendShapeWeight(2) * 0.8f);
                smr.SetBlendShapeWeight(3, smr.GetBlendShapeWeight(3) * 0.8f);
                smr.SetBlendShapeWeight(4, smr.GetBlendShapeWeight(4) * 0.8f);
                smr.SetBlendShapeWeight(5, smr.GetBlendShapeWeight(5) * 0.8f);
            }
        }

        public Bounds GetSegmentedModelCombinedBounds()
        {
            var bounds = new Bounds();

            foreach (var renderer in sectionRenderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        public void LoadFileManager()
        {
            FileManager.init(true);
        }

        public void UpdateGenerationConfiguration()
        {
            int sectionIdx = 0;
            /*foreach (var liSegment in liGenerationConfiguration.intestineSegments)
            {
                for (int i = 0; i < liSegment.liSections.Count; i++)
                {
                    var liSection = liSegment.liSections[i];
                    if (liSection.meshName == "defaultSection")
                    {
                        var oldBlendWeights = new BlendshapesWeights().SetValues(liSection.blendshapesWeights);
                        var newBlendWeights = new BlendshapesWeights();
                        newBlendWeights.weightsPerBlendshape = new List<float>();
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[0]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[1]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[2]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[3]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[4]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[5]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[8]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[9]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[6]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[7]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[10]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[11]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[12]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[13]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[14]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[15]);
                        liSection.blendshapesWeights = newBlendWeights.weightsPerBlendshape;
                        liSegment.liSections[i] = liSection;
                    }
                    else if (liSection.meshName == "cecum")
                    {
                        var oldBlendWeights = new BlendshapesWeights().SetValues(liSection.blendshapesWeights);
                        var newBlendWeights = new BlendshapesWeights();
                        newBlendWeights.weightsPerBlendshape = new List<float>();
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[0]);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[1]);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[2]);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[3]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[4]);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[5]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[6]);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[9]);
                        newBlendWeights.weightsPerBlendshape.Add(0f);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[8]);
                        liSection.blendshapesWeights = newBlendWeights.weightsPerBlendshape;
                        liSegment.liSections[i] = liSection;
                    }
                    else if (liSection.meshName == "rectum")
                    {
                        var oldBlendWeights = new BlendshapesWeights().SetValues(liSection.blendshapesWeights);
                        var newBlendWeights = new BlendshapesWeights();
                        newBlendWeights.weightsPerBlendshape = new List<float>();
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[0]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[1]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[2]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[3]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[4]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[5]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[8]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[9]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[6]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[7]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[10]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[11]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[12]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[13]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[14]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[15]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[16]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[17]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[23]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[24]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[25]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[26]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[27]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[28]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[29]);
                        newBlendWeights.weightsPerBlendshape.Add(oldBlendWeights.weightsPerBlendshape[30]);
                        liSection.blendshapesWeights = newBlendWeights.weightsPerBlendshape;
                        liSegment.liSections[i] = liSection;
                    }
                    renderersBlendshapesWeights[sectionIdx++] = new BlendshapesWeights().SetValues(liSection.blendshapesWeights);
                }
            }*/
        }

        private void GetSegmentsVertexOffset()
        {

        }

        public void InstantiateLLMModel()
        {
            Debug.Log("Instantiating LLM model...");
            FileManager.DeserializeJsonFile(llmFilePath, out Model model, JsonSerializationSettings.AITrainingModelJsonSettings);
            InitializeSplineFromModelData(model);
            LoadGenerationConfiguration();
            GenerateSegmentedModel();
        }

        public void InstantiateDatasetModel()
        {
            Debug.Log("Instantiating dataset model...");
            var record = datasetEntryRecord;
            
            // Check if dataset file path is set
            if (string.IsNullOrEmpty(datasetFilePath))
            {
                Debug.LogError("Dataset file path is not set.");
                return;
            }
            
            // Check if dataset file exists
            if (!File.Exists(datasetFilePath))
            {
                Debug.LogError("Dataset file not found: " + datasetFilePath);
                return;
            }
            
            try
            {
                // Read all lines from the JSON Lines file
                string[] lines = File.ReadAllLines(datasetFilePath);
                
                // Validate record index
                if (record < 0 || record >= lines.Length)
                {
                    Debug.LogError($"Dataset entry record {record} is out of range. Total entries: {lines.Length}");
                    return;
                }
                
                // Get the specific line
                string targetLine = lines[record];
                
                // Parse the JSON line into a ModelEntry object
                var entry = JsonUtility.FromJson<ModelEntry>(targetLine);
                if (entry?.conversations == null)
                {
                    Debug.LogError("Failed to parse dataset entry or conversations missing.");
                    return;
                }
                
                // Find the conversation with role "gpt"
                foreach (var conversation in entry.conversations)
                {
                    if (conversation.role == "gpt")
                    {
                        // The value string contains escaped JSON - deserialize it into a Model object
                        var model = JsonConvert.DeserializeObject<Model>(conversation.value, JsonSerializationSettings.AITrainingModelJsonSettings);
                        if (model != null)
                        {
                            // Initialize the spline from the model data
                            InitializeSplineFromModelData(model);
                            LoadGenerationConfiguration();
                            GenerateSegmentedModel();
                            Debug.Log($"Successfully instantiated dataset model from entry {record}");
                            return;
                        }
                        else
                        {
                            Debug.LogError("Failed to deserialize GPT response into Model object.");
                            return;
                        }
                    } else if (conversation.role == "human")
                    {
                        // Display the human conversation text
                        Debug.LogWarning($"Generated model prompt is: {conversation.value}");
                    }
                }
                
                Debug.LogError("No GPT conversation found in dataset entry.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error instantiating dataset model: {ex.Message}");
            }
        }

        public void SaveAIModel()
        {
            if (saveAIModelfilePath == null || saveAIModelfilePath == "")
            {
                Debug.LogError("LLM file path is not set.");
                return;
            }
            var model = new Model();
            //model.BuildForAITraining(Spline, renderersBlendshapesWeights);
            try
            {
                File.WriteAllText(saveAIModelfilePath, model.ToJson(JsonSerializationSettings.AITrainingModelJsonSettings, false));
                Debug.Log($"File saved successfully at {saveAIModelfilePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save file at {saveAIModelfilePath}: {ex.Message}");
            }
            Debug.Log($"AI model saved to {saveAIModelfilePath}");
        }

    }



#if UNITY_EDITOR
    [CustomEditor(typeof(LI_ModelGenerator))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LI_ModelGenerator myScript = (LI_ModelGenerator)target;
            if (GUILayout.Button("Save generation configuration"))
            {
                myScript.SaveGenerationConfiguration();
            }
            else if (GUILayout.Button("Load generation configuration"))
            {
                myScript.LoadGenerationConfiguration();
            }
            else if (GUILayout.Button("Generate segmented model"))
            {
                myScript.GenerateSegmentedModel();
            }
            else if (GUILayout.Button("Generate welded model"))
            {
                myScript.GenerateWeldedModel();
            }
            else if (GUILayout.Button("Update blendhsapes weights from renderers"))
            {
                myScript.UpdateBlenshapeWeightsFromRenderers();
            }
            else if (GUILayout.Button("Scale spline"))
            {
                myScript.ScaleSpline();
            }
            else if (GUILayout.Button("Rotate spline"))
            {
                myScript.RotateSpline();
            }
            else if (GUILayout.Button("Change spline up vector"))
            {
                myScript.UpdateUpSplineNodesVectors();
            }
            else if (GUILayout.Button("Save spline preset"))
            {
                myScript.SaveSplinePreset();
            }
            else if (GUILayout.Button("Load spline preset"))
            {
                myScript.LoadSplinePreset();
            }
            else if (GUILayout.Button("Load file manager"))
            {
                myScript.LoadFileManager();
            }
            else if (GUILayout.Button("Update Generation Configuration"))
            {
                myScript.UpdateGenerationConfiguration();
            }
            else if (GUILayout.Button("ShapesModification"))
            {
                myScript.ShapesModification();
            }
            else if (GUILayout.Button("Instantiate model from LLM"))
            {
                myScript.InstantiateLLMModel();
            }
            else if (GUILayout.Button("Instantiate dataset model"))
            {
                myScript.InstantiateDatasetModel();
            }
            else if (GUILayout.Button("Save AI model"))
            {
                myScript.SaveAIModel();
            }
        }
    }
#endif

    // Helper classes for dataset JSON parsing
    [System.Serializable]
    public class Conversation
    {
        public string role;
        public string value;
    }

    [System.Serializable]
    public class ModelEntry
    {
        public Conversation[] conversations;
    }
}