using UnityEngine;
using System;
using System.Collections.Generic;
using GeometryUtils;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Vertex = GeometryUtils.Vertex;
using Unity.VisualScripting;

namespace SplineMesh
{
    /// <summary>
    /// A component that creates a deformed mesh from a given one along the given spline segment.
    /// The source mesh will always be bended along the X axis.
    /// It can work on a cubic bezier curve or on any interval of a given spline.
    /// On the given interval, the mesh can be place with original scale, stretched, or repeated.
    /// The resulting mesh is stored in a MeshFilter component and automaticaly updated on the next update if the spline segment change.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class CustomMeshBender : MonoBehaviour
    {
        public bool useSkinnedMeshRenderer = true;
        private bool isDirty = false;
        public Mesh bendedMesh;
        private CubicBezierCurve curve;
        private Dictionary<float, CurveSample> sampleCache = new Dictionary<float, CurveSample>();
        private List<CurveSample> samples;
        private Vertex[] bentVertices;
        private float intervalStart, intervalEnd;
        private int vertexCount;
        private int blendshapeCount;
        private Renderer modelRenderer;
        private MeshFilter modelMeshFilter;
        private Mesh originalMesh;
        private MeshData meshData;
        private bool useSpline = false;
        private Spline spline;
        private bool _initialized = false;
        
        // Node-centered interval mode
        private bool useNodeCentered = false;
        private int centerNodeIndex = -1;

        /// <summary>
        /// When true, blendshape delta normals and tangents are forced to zero and never
        /// transformed. The NativeArrays are zero-initialised at allocation and left
        /// untouched each frame (no CopyFrom from source frame data). Rotating a zero
        /// vector stays zero, so no extra clearing is required. Set before Configure().
        /// </summary>
        public bool SkipBlendshapeNormalsAndTangents = false;

        /// <summary>
        /// The source mesh to bend.
        /// </summary>
        public MeshData MeshData
        {
            get { return meshData; }
            set
            {
                if (value == meshData) return;
                if (!_initialized) Initialize();
                SetDirty();
                meshData = value;
                InitBendingVariables();
            }
        }

        /// <summary>
        /// Sets a curve along which the mesh will be bent.
        /// The mesh will be updated if the curve changes.
        /// </summary>
        /// <param name="curve">The <see cref="CubicBezierCurve"/> to bend the source mesh along.</param>
        public void SetInterval(CubicBezierCurve curve)
        {
            if (this.curve == curve) return;
            if (curve == null) throw new ArgumentNullException("curve");
            if (this.curve != null)
            {
                this.curve.Changed.RemoveListener(SetDirty);
            }
            this.curve = curve;
            curve.Changed.AddListener(SetDirty);
            SetDirty();
        }

        /// <summary>
        /// Sets a spline's interval along which the mesh will be bent.
        /// If interval end is absent or set to 0, the interval goes from start to spline length.
        /// The mesh will be update if any of the curve changes on the spline, including curves
        /// outside the given interval.
        /// </summary>
        /// <param name="spline">The <see cref="SplineMesh"/> to bend the source mesh along.</param>
        /// <param name="intervalStart">Distance from the spline start to place the mesh minimum X.<param>
        /// <param name="intervalEnd">Distance from the spline start to stop deforming the source mesh.</param>
        public void SetInterval(Spline spline, float intervalStart, float intervalEnd = 0)
        {
            if (this.spline == spline && this.intervalStart == intervalStart && this.intervalEnd == intervalEnd) return;
            if (spline == null) throw new ArgumentNullException("spline");
            if (intervalStart < 0 || intervalStart >= spline.Length)
            {
                throw new ArgumentOutOfRangeException("interval start must be 0 or greater and lesser than spline length (was " + intervalStart + ")");
            }
            if (intervalEnd != 0f && intervalEnd <= intervalStart || intervalEnd > spline.Length)
            {
                throw new ArgumentOutOfRangeException("interval end must be 0 or greater than interval start, and lesser than spline length (was " + intervalEnd + ")");
            }
            if (this.spline != null)
            {
                // unlistening previous spline
                this.spline.CurveChanged.RemoveListener(SetDirty);
            }
            this.spline = spline;
            // listening new spline
            spline.CurveChanged.AddListener(SetDirty);

            curve = null;
            this.intervalStart = intervalStart;
            this.intervalEnd = intervalEnd;
            useSpline = true;
            useNodeCentered = false;
            centerNodeIndex = -1;
            SetDirty();
        }
        
        /// <summary>
        /// Sets a spline interval centered on a specific node.
        /// The interval spans from midpoint to previous node to midpoint to next node.
        /// Intervals are recalculated dynamically when the spline changes.
        /// </summary>
        /// <param name="spline">The spline to bend the mesh along.</param>
        /// <param name="nodeIndex">The index of the node to center the mesh on.</param>
        public void SetIntervalByNodeCenter(Spline spline, int nodeIndex)
        {
            if (spline == null) throw new ArgumentNullException("spline");
            if (nodeIndex < 0 || nodeIndex >= spline.nodes.Count)
            {
                throw new ArgumentOutOfRangeException($"nodeIndex must be between 0 and {spline.nodes.Count - 1} (was {nodeIndex})");
            }
            
            if (this.spline != null)
            {
                this.spline.CurveChanged.RemoveListener(SetDirty);
            }
            this.spline = spline;
            spline.CurveChanged.AddListener(SetDirty);
            
            curve = null;
            useSpline = true;
            useNodeCentered = true;
            centerNodeIndex = nodeIndex;
            
            // Initial calculation (will be recalculated each update)
            RecalculateNodeCenteredInterval();
            SetDirty();
        }
        
        /// <summary>
        /// Recalculates interval distances based on current node positions.
        /// </summary>
        private void RecalculateNodeCenteredInterval()
        {
            if (!useNodeCentered || spline == null || centerNodeIndex < 0) return;
            
            int nodeCount = spline.nodes.Count;
            
            // Calculate cumulative distances to each node
            var nodeDistances = new List<float> { 0f };
            float totalLength = 0f;
            for (int i = 0; i < spline.curves.Count; i++)
            {
                totalLength += spline.curves[i].Length;
                nodeDistances.Add(totalLength);
            }
            
            float nodeDistance = nodeDistances[centerNodeIndex];
            float prevNodeDistance = centerNodeIndex > 0 ? nodeDistances[centerNodeIndex - 1] : 0f;
            float nextNodeDistance = centerNodeIndex < nodeDistances.Count - 1 ? nodeDistances[centerNodeIndex + 1] : totalLength;
            
            // Interval spans from midpoint to previous node to midpoint to next node
            intervalStart = (prevNodeDistance + nodeDistance) / 2f;
            intervalEnd = (nodeDistance + nextNodeDistance) / 2f;
            
            // Handle first node: start from 0
            if (centerNodeIndex == 0)
            {
                intervalStart = 0f;
            }
            
            // Handle last node: end at spline length
            if (centerNodeIndex == nodeCount - 1)
            {
                intervalEnd = totalLength;
            }
        }

        private void OnEnable()
        {

        }

        private void OnValidate()
        {
            ComputeIfNeeded();
        }

        private void LateUpdate()
        {
            ComputeIfNeeded();
        }

        private void Initialize()
        {
            if (_initialized) return;
            if (useSkinnedMeshRenderer)
            {
                modelRenderer = this.GetComponent<SkinnedMeshRenderer>();
                if (modelRenderer == null) modelRenderer = this.AddComponent<SkinnedMeshRenderer>();
                var mesh = ((SkinnedMeshRenderer)modelRenderer).sharedMesh;
                if (mesh != null)
                {
                    bendedMesh = mesh;
                }
                else
                {
                    bendedMesh = ((SkinnedMeshRenderer)modelRenderer).sharedMesh = new Mesh();
                    bendedMesh.name = "Generated by " + GetType().Name;
                }
            }
            else
            {
                modelRenderer = this.AddComponent<MeshRenderer>();
                modelMeshFilter = this.AddComponent<MeshFilter>();
                var mesh = modelMeshFilter.sharedMesh;
                if (mesh != null)
                {
                    bendedMesh = mesh;
                }
                else
                {
                    bendedMesh = modelMeshFilter.sharedMesh = new Mesh();
                    bendedMesh.name = "Generated by " + GetType().Name;
                }
            }
            _initialized = true;
        }

        public void Configure()
        {
            Compute();
            ComputeBendedMeshRecalculations();
        }

        public void ComputeIfNeeded()
        {
            if (isDirty)
            {
                Compute();
            }
        }

        public void ForceCompute()
        {
            //Debug.Log("CustomMeshBender ForceCompute called");
            SetDirty();
            ComputeIfNeeded();
        }

        private void SetDirty()
        {
            //Debug.Log("CustomMeshBender SetDirty called");
            isDirty = true;
        }

        private void InitBendingVariables()
        {
            originalMesh = meshData.OriginalMesh;
            vertexCount = meshData.VertexCount;
            blendshapeCount = meshData.BlendshapeCount;
            samples = new List<CurveSample>();
            bentVertices = new Vertex[vertexCount];
            MeshUtils.CopyMesh(bendedMesh, meshData.GetTransformedMesh());
            modelRenderer.localBounds = bendedMesh.bounds;
        }

        /// <summary>
        /// Bend the mesh. This method may take time and should not be called more than necessary.
        /// Consider using <see cref="ComputeIfNeeded"/> for faster result.
        /// </summary>
        private void Compute()
        {
            //Debug.Log("CustomMeshBender Compute called");
            isDirty = false;
            FillStretchParallel();
            /*if (_initialized == false)
            {
                _initialized = true;
            }*/
        }

        private void OnDestroy()
        {
            if (curve != null)
            {
                curve.Changed.RemoveListener(Compute);
            }
        }

        private void FillStretchParallel()
        {
            UpdateSamples();
            NativeArray<Vertex> verticesNA = new NativeArray<Vertex>(vertexCount, Allocator.TempJob);
            NativeArray<Vector3> locations = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
            NativeArray<Quaternion> rotations = new NativeArray<Quaternion>(vertexCount, Allocator.TempJob);
            NativeArray<Vector2> scales = new NativeArray<Vector2>(vertexCount, Allocator.TempJob);
            NativeArray<float> rolls = new NativeArray<float>(vertexCount, Allocator.TempJob);

            verticesNA.CopyFrom(meshData.Vertices);
            locations.CopyFrom(CurveSamplesUtils.GetLocationsArray(samples));
            rotations.CopyFrom(CurveSamplesUtils.GetRotationsArray(samples));
            scales.CopyFrom(CurveSamplesUtils.GetScalesArray(samples));
            rolls.CopyFrom(CurveSamplesUtils.GetRollsArray(samples));

            UpdateBentVertices(verticesNA, locations, rotations, scales, rolls);

            UpdateBlendshapes(locations, rotations, scales, rolls);

            MeshUtils.SimpleMeshCopy(bendedMesh,
                originalMesh,
                false,
                null,
                meshData.Triangles,
                VertexUtils.GetPositionsArray(bentVertices),
                VertexUtils.GetNormalsArray(bentVertices));

            bendedMesh.RecalculateBounds();
            NormalSolver.RecalculateTangents(bendedMesh);

            if (useSkinnedMeshRenderer)
            {
                var smr = (SkinnedMeshRenderer)modelRenderer;
                smr.sharedMesh = bendedMesh;
                smr.localBounds = smr.sharedMesh.bounds;
            }
            else
            {
                var mr = (MeshRenderer)modelRenderer;
                modelMeshFilter.sharedMesh = bendedMesh;
                mr.localBounds = modelMeshFilter.sharedMesh.bounds;
            }

            verticesNA.Dispose();
            locations.Dispose();
            rotations.Dispose();
            scales.Dispose();
            rolls.Dispose();
        }

        private void UpdateSamples()
        {
            // Recalculate interval if using node-centered mode
            if (useNodeCentered)
            {
                RecalculateNodeCenteredInterval();
            }
            
            samples.Clear();
            sampleCache.Clear();
            var vertices = meshData.Vertices;
            var length = meshData.Length;
            var minX = meshData.MinX;
            for (int i = 0; i < vertexCount; i++)
            {
                var vertex = vertices[i];
                float distanceRate = length == 0 ? 0 : Mathf.Abs(vertex.position.x - minX) / length;
                CurveSample sample;
                if (!sampleCache.TryGetValue(distanceRate, out sample))
                {
                    if (!useSpline)
                    {
                        sample = curve.GetSampleAtDistance(curve.Length * distanceRate);

                    }
                    else
                    {
                        // Clamp intervals to current spline length to handle dynamic spline changes
                        float currentSplineLength = spline.Length;
                        float clampedStart = Mathf.Min(intervalStart, currentSplineLength - 0.0001f);
                        float clampedEnd = intervalEnd == 0 ? currentSplineLength : Mathf.Min(intervalEnd, currentSplineLength);
                        
                        // Ensure valid interval
                        if (clampedEnd <= clampedStart)
                        {
                            clampedEnd = clampedStart + 0.0001f;
                        }
                        
                        float intervalLength = clampedEnd - clampedStart;
                        float distOnSpline = clampedStart + intervalLength * distanceRate;
                        
                        // Final safety clamp
                        distOnSpline = Mathf.Clamp(distOnSpline, 0f, currentSplineLength);
                        sample = spline.GetSampleAtDistance(distOnSpline);
                    }
                    sampleCache[distanceRate] = sample;
                }
                samples.Add(sample);
            }
        }


        private void UpdateBentVertices(NativeArray<Vertex> verticesNA, NativeArray<Vector3> locations, NativeArray<Quaternion> rotations, NativeArray<Vector2> scales, NativeArray<float> rolls)
        {
            GetBentJob getBentJob = new GetBentJob
            {
                vertices = verticesNA,
                locations = locations,
                rotations = rotations,
                scales = scales,
                rolls = rolls,
            };

            JobHandle getBentJobHandle = getBentJob.Schedule(vertexCount, 1);
            getBentJobHandle.Complete();

            verticesNA.CopyTo(bentVertices);
        }

        private void UpdateBlendshapes(NativeArray<Vector3> locations, NativeArray<Quaternion> rotations, NativeArray<Vector2> scales, NativeArray<float> rolls)
        {
            Vector3[] deltaVertices = new Vector3[vertexCount];
            Vector3[] deltaNormals = new Vector3[vertexCount];
            Vector3[] deltaTangents = new Vector3[vertexCount];
            NativeArray<Vector3> deltaPositionsNA = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
            NativeArray<Vector3> deltaNormalsNA = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
            NativeArray<Vector3> deltaTangentsNA = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
            bendedMesh.ClearBlendShapes();

            var sourceBlendshapes = meshData.Blendshapes;
            for (int i = 0; i < blendshapeCount; i++)
            {
                var blendshape = sourceBlendshapes[i];
                string blendShapeName = blendshape.name;
                int frameCount = blendshape.frames.Length;
                for (int j = 0; j < frameCount; j++)
                {
                    var frame = blendshape.frames[j];
                    float frameWeight = frame.frameWeight;
                    deltaPositionsNA.CopyFrom(frame.deltaVertices);
                    if (!SkipBlendshapeNormalsAndTangents)
                    {
                        deltaNormalsNA.CopyFrom(frame.deltaNormals);
                        deltaTangentsNA.CopyFrom(frame.deltaTangents);
                    }
                    // else: arrays remain zero-initialised; rotating zero vectors stays zero,
                    // so the job output for normals/tangents will also be zero.

                    GetBentShapesJob getBentShapesJob = new GetBentShapesJob
                    {
                        deltaPositions = deltaPositionsNA,
                        deltaNormals = deltaNormalsNA,
                        deltaTangents = deltaTangentsNA,
                        locations = locations,
                        rotations = rotations,
                        scales = scales,
                        rolls = rolls,
                    };

                    JobHandle getBentShapesJobHandle = getBentShapesJob.Schedule(vertexCount, 1);
                    getBentShapesJobHandle.Complete();

                    deltaPositionsNA.CopyTo(deltaVertices);
                    deltaNormalsNA.CopyTo(deltaNormals);
                    deltaTangentsNA.CopyTo(deltaTangents);

                    bendedMesh.AddBlendShapeFrame(blendShapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
                }
            }

            deltaPositionsNA.Dispose();
            deltaNormalsNA.Dispose();
            deltaTangentsNA.Dispose();
        }


        public void ComputeBendedMeshRecalculations()
        {
            NormalSolver.RecalculateTangents(bendedMesh);
            bendedMesh.RecalculateBounds();
            if (useSkinnedMeshRenderer)
            {
                var smr = (SkinnedMeshRenderer)modelRenderer;
                smr.sharedMesh = bendedMesh;
                smr.localBounds = smr.sharedMesh.bounds;
            }
            else
            {
                var mr = (MeshRenderer)modelRenderer;
                modelMeshFilter.sharedMesh = bendedMesh;
                mr.localBounds = bendedMesh.bounds;
            }
        }


        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        public struct GetBentJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vertex> vertices;
            [ReadOnly] public NativeArray<Vector3> locations;
            [ReadOnly] public NativeArray<Quaternion> rotations;
            [ReadOnly] public NativeArray<Vector2> scales;
            [ReadOnly] public NativeArray<float> rolls;


            public void Execute(int index)
            {
                var v = vertices[index];
                var vp = v.position;
                var vn = v.normal;
                var scale = scales[index];
                var roll = rolls[index];

                var vs = new float4(0, scale.y, scale.x, 0);
                // application of scale
                vp = new Vector3(vp.x * vs.x, vp.y * vs.y, vp.z * vs.z);

                // application of roll
                vp = Quaternion.AngleAxis(roll, Vector3.right) * vp;
                vn = Quaternion.AngleAxis(roll, Vector3.right) * vn;

                // reset X value
                //vp.x = 0;

                // application of the rotation + locatio
                Quaternion q = rotations[index] * Quaternion.Euler(0, -90, 0);
                vp = q * vp + locations[index];
                vn = q * vn;

                vertices[index] = new Vertex(vp, vn, vertices[index].uv);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        public struct GetBentShapesJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> deltaPositions;
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> deltaNormals;
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> deltaTangents;
            [ReadOnly] public NativeArray<Vector3> locations;
            [ReadOnly] public NativeArray<Quaternion> rotations;
            [ReadOnly] public NativeArray<Vector2> scales;
            [ReadOnly] public NativeArray<float> rolls;
            public void Execute(int index)
            {
                var scale = scales[index];
                var roll = rolls[index];

                // For blendshape deltas, keep X component (tangent direction deformation)
                // Use 1.0 for X scale instead of 0 to preserve delta magnitude
                var vs = new Vector4(1, scale.y, scale.x, 0);

                var dp = deltaPositions[index];
                var dn = deltaNormals[index];
                var dt = deltaTangents[index];

                // application of scale
                dp = new Vector3(dp.x * vs.x, dp.y * vs.y, dp.z * vs.z);

                // application of roll
                dp = Quaternion.AngleAxis(roll, Vector3.right) * dp;
                dn = Quaternion.AngleAxis(roll, Vector3.right) * dn;
                dt = Quaternion.AngleAxis(roll, Vector3.right) * dt;

                // NOTE: Do NOT zero dp.x for blendshapes - it represents intentional
                // deformation along the mesh length axis (spline tangent direction)

                // application of the rotation
                Quaternion q = rotations[index] * Quaternion.Euler(0, -90, 0);
                dp = q * dp;
                dn = q * dn;
                dt = q * dt;

                deltaPositions[index] = dp;
                deltaNormals[index] = dn;
                deltaTangents[index] = dt;
            }
        }
    }
}