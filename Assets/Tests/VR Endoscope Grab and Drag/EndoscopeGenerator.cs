using GeometryUtils;
using SplineMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

public class EndoscopeGenerator : MonoBehaviour
{
    public Spline spline;

    [Header("Required variables for the generator")]
    public GameObject splinePrefab;
    public Mesh baseSegmentMesh;
    [Tooltip("Translation to apply on the mesh before bending it.")]
    public Vector3 translation;
    [Tooltip("Rotation to apply on the mesh before bending it.")]
    public Vector3 rotation;
    [Tooltip("Scale to apply on the mesh before bending it.")]
    public Vector3 scale = Vector3.one;
    public Material baseSegmentMaterial;
    public GameObject segmentDynamicsPrefab;
    public float segmentSpacing = 0.3f;
    public int segmentCount = 10;
    public Transform splineOrigin;
    public List<GameObject> segmentsWaypoints;
    private bool _generated = false;
    private ConfigurableJoint joint = null;
    private List<Rigidbody> segmentsRBs;
    public bool toGenerate = true;
    public GameObject endoscopeTipGo;
    public Rigidbody endoscopeControlsRB;
    public ColonoscopyHoleController colonoscopyHoleController;

    private GameObject modelVisualsHolder;
    public GameObject ModelVisualsHolder
    {
        get
        {
            if (modelVisualsHolder == null)
            {
                modelVisualsHolder = CommonUtils.Create("Model Visuals Holder", transform);
            }
            return modelVisualsHolder;
        }
        private set
        {
            modelVisualsHolder = value;
        }
    }

    private GameObject modelDynamicsHolder;
    public GameObject ModelDynamicsHolder
    {
        get
        {
            if (modelDynamicsHolder == null)
            {
                modelDynamicsHolder = CommonUtils.Create("Model Dynamics Holder", transform);
            }
            return modelDynamicsHolder;
        }
        private set
        {
            modelDynamicsHolder = value;
        }
    }

    void OnEnable()
    {
        segmentsWaypoints = new List<GameObject>();
        segmentsRBs = new List<Rigidbody>();
    }

    private void Awake()
    {
        segmentsWaypoints = new List<GameObject>();
        segmentsRBs = new List<Rigidbody>();
    }

    private void Update()
    {
        if (toGenerate)
        {
            toGenerate = false;
            GenerateModel();
        }

        // Use jobs for updating spline nodes
        UpdateSplineNodesJob();
    }

    [BurstCompile]
    private struct SplineNodeUpdateJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> targetPositions;
        [ReadOnly] public NativeArray<Vector3> targetUps;
        [WriteOnly] public NativeArray<Vector3> newNodePositions;
        [WriteOnly] public NativeArray<Vector3> newNodeUps;

        public void Execute(int index)
        {
            newNodePositions[index] = targetPositions[index];
            newNodeUps[index] = targetUps[index];
        }
    }

    private void UpdateSplineNodesJob()
    {
        int nodeCount = spline.nodes.Count;

        NativeArray<Vector3> targetPositions = new NativeArray<Vector3>(segmentsWaypoints.Count, Allocator.TempJob);
        NativeArray<Vector3> targetUps = new NativeArray<Vector3>(segmentsWaypoints.Count, Allocator.TempJob);
        NativeArray<Vector3> newNodePositions = new NativeArray<Vector3>(nodeCount, Allocator.TempJob);
        NativeArray<Vector3> newNodeUps = new NativeArray<Vector3>(nodeCount, Allocator.TempJob);

        for (int i = 0; i < segmentsWaypoints.Count; i++)
        {
            var target = segmentsWaypoints[i].transform;
            targetPositions[i] = transform.InverseTransformPoint(target.position);
            targetUps[i] = target.up;
        }

        SplineNodeUpdateJob job = new SplineNodeUpdateJob
        {
            targetPositions = targetPositions,
            targetUps = targetUps,
            newNodePositions = newNodePositions,
            newNodeUps = newNodeUps
        };

        JobHandle jobHandle = job.Schedule(nodeCount, 1024); // 64 is a reasonable batch size for parallel execution
        jobHandle.Complete();

        // Apply the results back to the spline nodes
        for (int i = 0; i < nodeCount; i++)
        {
            spline.nodes[i].Position = newNodePositions[i];
            spline.nodes[i].Up = newNodeUps[i];
        }

        // Dispose of NativeArrays
        targetPositions.Dispose();
        targetUps.Dispose();
        newNodePositions.Dispose();
        newNodeUps.Dispose();
    }

    public void GenerateModel()
    {
        segmentsWaypoints = new List<GameObject>();
        segmentsRBs = new List<Rigidbody>();
        segmentsWaypoints.Clear();
        segmentsRBs.Clear();
        GenerateModelSpline();
        GenerateModelSegments();
        colonoscopyHoleController.endoscopeGo = endoscopeTipGo = segmentsRBs.Last().gameObject;
    }

    private void GenerateModelSpline()
    {
        if (spline != null)
        {
            Destroy(spline);
        }
        else
        {
            spline = CommonUtils.Instantiate(splinePrefab, transform, "Endoscope spline").GetComponent<Spline>();
        }
        for (int i = 2; i < segmentCount; i++) //Base spline always have two nodes, so we start at idx 2 
        {
            spline.AddNode(new SplineNode(Vector3.zero, Vector3.zero));
        }
        spline.RefreshCurves();
    }

    private void GenerateModelSegments()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            if (i < segmentCount - 1) GenerateSegmentVisuals(i);
            GenerateSegmentDynamics(i);
        }
    }

    private void GenerateSegmentVisuals(int segmentIdx)
    {
        var segmentVisuals = CommonUtils.Create("Segment " + segmentIdx + " visuals",
            ModelVisualsHolder.transform,
            new Type[] { typeof(CustomMeshBender) });
        ConfigureSegmentMeshBender(segmentVisuals.GetComponent<CustomMeshBender>(), baseSegmentMesh, segmentIdx);
        segmentVisuals.GetComponent<Renderer>().sharedMaterials = new Material[] { baseSegmentMaterial };
    }


    private void GenerateSegmentDynamics(int segmentIdx)
    {
        var worldPos = transform.InverseTransformPoint(splineOrigin.position);
        var segmentTranslation = new Vector3(0 + worldPos.x, 0 + worldPos.y, (-segmentSpacing * segmentIdx) + worldPos.z);
        var segmentDynamics = CommonUtils.Instantiate(segmentDynamicsPrefab, ModelDynamicsHolder.transform, "Segment " + segmentIdx + " dynamics");
        segmentDynamics.transform.Translate(segmentTranslation);
        var collider = segmentDynamics.GetComponent<CapsuleCollider>();
        var segmentRB = segmentDynamics.GetComponent<Rigidbody>();
        joint = segmentDynamics.GetComponent<ConfigurableJoint>();

        if (segmentIdx == 0)
        {
            Destroy(segmentDynamics.GetComponent<ConfigurableJoint>());
            var fixedJoint = segmentDynamics.AddComponent<FixedJoint>();
            fixedJoint.connectedBody = endoscopeControlsRB;
            fixedJoint.enablePreprocessing = false;
            collider.center = new Vector3(0, 0, -segmentSpacing / 2);
        }
        else
        {
            joint.connectedBody = segmentsRBs[segmentIdx - 1];
        }
        if (segmentIdx == segmentCount - 1)
        {   
            //segmentRB.mass = 100f; 
            collider.center = new Vector3(0, 0, +segmentSpacing / 2);
        }
        collider.height = segmentSpacing;

        segmentsWaypoints.Add(segmentDynamics);
        segmentsRBs.Add(segmentRB);
    }


    private void ConfigureSegmentMeshBender(CustomMeshBender meshBender, Mesh segmentMesh, int segmentIdx)
    {
        meshBender.useSkinnedMeshRenderer = true;
        meshBender.SetInterval(spline.GetCurve(segmentIdx));
        var meshData = new MeshData(segmentMesh,
            translation,
            Quaternion.Euler(rotation),
            scale,
            false);
        meshData.BuildData();
        meshBender.MeshData = meshData;
        meshBender.Configure();
    }

    private void UpdateSplineNodes()
    {
        for (int i = 0; i < segmentsWaypoints.Count; i++)
        {
            var node = spline.nodes[i];
            var target = segmentsWaypoints[i].transform;
            var newPosition = transform.InverseTransformPoint(target.position);

            if (Vector3.Distance(node.Position, newPosition) > 0.001f || node.Up != target.up)
            {
                node.Position = newPosition;
                node.Up = target.up;
            }
        }
    }

    private void ResetSpline()
    {
        spline.nodes.Clear();
        spline.curves.Clear();
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(EndoscopeGenerator))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EndoscopeGenerator myScript = (EndoscopeGenerator)target;
            if (GUILayout.Button("Generate Model"))
            {
                myScript.GenerateModel();
                myScript.UpdateSplineNodes();
            }
        }
    }
#endif
}
