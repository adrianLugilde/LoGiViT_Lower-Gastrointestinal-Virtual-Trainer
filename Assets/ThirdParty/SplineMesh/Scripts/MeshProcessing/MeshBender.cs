using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using GeometryUtils;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace SplineMesh {
    /// <summary>
    /// A component that creates a deformed mesh from a given one along the given spline segment.
    /// The source mesh will always be bended along the X axis.
    /// It can work on a cubic bezier curve or on any interval of a given spline.
    /// On the given interval, the mesh can be place with original scale, stretched, or repeated.
    /// The resulting mesh is stored in a MeshFilter component and automaticaly updated on the next update if the spline segment change.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [ExecuteInEditMode]
    public class MeshBender : MonoBehaviour {
        public bool useParallelBender = false;
        private bool isDirty = false;
        public Mesh result;
        private bool useSpline;
        private Spline spline;
        private float intervalStart, intervalEnd;
        private CubicBezierCurve curve;
        private Dictionary<float, CurveSample> sampleCache = new Dictionary<float, CurveSample>();
        private List<CurveSample> samples;
        private List<Vertex> bentVertices;
        private List<OldBlendshape> blendshapes;
        private int vertexCount;
        private int blendshapesCount;
        private Mesh sourceMesh;



        private SourceMesh source;
        /// <summary>
        /// The source mesh to bend.
        /// </summary>
        public SourceMesh Source {
            get { return source; }
            set {
                if (value == source) return;
                SetDirty();
                source = value;
                InitBendingVariables();
            }
        }

        private FillingMode mode = FillingMode.StretchToInterval;
        /// <summary>
        /// The scaling mode along the spline
        /// </summary>
        public FillingMode Mode {
            get { return mode; }
            set {
                if (value == mode) return;
                SetDirty();
                mode = value;
            }
        }

        /// <summary>
        /// Sets a curve along which the mesh will be bent.
        /// The mesh will be updated if the curve changes.
        /// </summary>
        /// <param name="curve">The <see cref="CubicBezierCurve"/> to bend the source mesh along.</param>
        public void SetInterval(CubicBezierCurve curve) {
            if (this.curve == curve) return;
            if (curve == null) throw new ArgumentNullException("curve");
            if (this.curve != null) {
                this.curve.Changed.RemoveListener(SetDirty);
            }
            this.curve = curve;
            spline = null;
            curve.Changed.AddListener(SetDirty);
            useSpline = false;
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
        public void SetInterval(Spline spline, float intervalStart, float intervalEnd = 0) {
            if (this.spline == spline && this.intervalStart == intervalStart && this.intervalEnd == intervalEnd) return;
            if (spline == null) throw new ArgumentNullException("spline");
            if (intervalStart < 0 || intervalStart >= spline.Length) {
                throw new ArgumentOutOfRangeException("interval start must be 0 or greater and lesser than spline length (was " + intervalStart + ")");
            }
            if (intervalEnd != 0 && intervalEnd <= intervalStart || intervalEnd > spline.Length) {
                throw new ArgumentOutOfRangeException("interval end must be 0 or greater than interval start, and lesser than spline length (was " + intervalEnd + ")");
            }
            if (this.spline != null) {
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
            SetDirty();
        }

        private void OnEnable() {
            if (GetComponent<SkinnedMeshRenderer>().sharedMesh != null) {
                result = GetComponent<SkinnedMeshRenderer>().sharedMesh;
            } else {
                GetComponent<SkinnedMeshRenderer>().sharedMesh = result = new Mesh();
                result.name = "Generated by " + GetType().Name;
            }
        }

        private void OnValidate()
        {
            ComputeIfNeeded();
        }

        private void LateUpdate() {
            ComputeIfNeeded();
        }

        public void ComputeIfNeeded() {
            if (isDirty) {
                Compute();
            }
        }

        private void SetDirty() {
            isDirty = true;
        }

        private void InitBendingVariables()
        {
            sourceMesh = source.Mesh;
            vertexCount = sourceMesh.vertexCount;
            blendshapesCount = sourceMesh.blendShapeCount;
            samples = new List<CurveSample>();
            bentVertices = new List<Vertex>(vertexCount);
            blendshapes = new List<OldBlendshape>(blendshapesCount);
        }

        /// <summary>
        /// Bend the mesh. This method may take time and should not be called more than necessary.
        /// Consider using <see cref="ComputeIfNeeded"/> for faster result.
        /// </summary>
        private void Compute() {
            isDirty = false;
            switch (Mode) {
                case FillingMode.Once:
                    FillOnce();
                    break;
                case FillingMode.Repeat:
                    FillRepeat();
                    break;
                case FillingMode.StretchToInterval:
                    if(useParallelBender)
                    {
                        FillStretchParallel();
                    } else
                    {
                        FillStretchParallel();
                    }
                    break;
            }
        }

        private void OnDestroy() {
            if (curve != null) {
                curve.Changed.RemoveListener(Compute);
            }
        }

        /// <summary>
        /// The mode used by <see cref="MeshBender"/> to bend meshes on the interval.
        /// </summary>
        public enum FillingMode {
            /// <summary>
            /// In this mode, source mesh will be placed on the interval by preserving mesh scale.
            /// Vertices that are beyond interval end will be placed on the interval end.
            /// </summary>
            Once,
            /// <summary>
            /// In this mode, the mesh will be repeated to fill the interval, preserving
            /// mesh scale.
            /// This filling process will stop when the remaining space is not enough to
            /// place a whole mesh, leading to an empty interval.
            /// </summary>
            Repeat,
            /// <summary>
            /// In this mode, the mesh is deformed along the X axis to fill exactly the interval.
            /// </summary>
            StretchToInterval
        }
        #region MODES NOT USED
        private void FillOnce() {
            sampleCache.Clear();
            var bentVertices = new List<Vertex>(source.Vertices.Count);
            // for each mesh vertex, we found its projection on the curve
            foreach (var vert in source.Vertices) {
                float distance = vert.position.x - source.MinX;
                CurveSample sample;
                if (!sampleCache.TryGetValue(distance, out sample)) {
                    if (!useSpline) {
                        if (distance > curve.Length) distance = curve.Length;
                        sample = curve.GetSampleAtDistance(distance);
                    } else {
                        float distOnSpline = intervalStart + distance;
                        if (distOnSpline > spline.Length) {
                            if (spline.IsLoop) {
                                while (distOnSpline > spline.Length) {
                                    distOnSpline -= spline.Length;
                                }
                            } else {
                                distOnSpline = spline.Length;
                            }
                        }
                        sample = spline.GetSampleAtDistance(distOnSpline);
                    }
                    sampleCache[distance] = sample;
                }

                bentVertices.Add(sample.GetBent(vert));
            }

            MeshUtils.CopyMesh(result,
                source.Mesh, true,
                source.Triangles,
                bentVertices.Select(b => b.position),
                bentVertices.Select(b => b.normal));
            //LUGILDE
            if (TryGetComponent(out MeshCollider collider)) {
                collider.sharedMesh = result;
            }
            if (TryGetComponent(out SkinnedMeshRenderer skinnedMeshRenderer))
            {
                skinnedMeshRenderer.sharedMesh = result;
            }
        }

        private void FillRepeat() {
            float intervalLength = useSpline ?
                (intervalEnd == 0 ? spline.Length : intervalEnd) - intervalStart :
                curve.Length;
            int repetitionCount = Mathf.FloorToInt(intervalLength / source.Length);


            // building triangles and UVs for the repeated mesh
            var triangles = new List<int>();
            var uv = new List<Vector2>();
            var uv2 = new List<Vector2>();
            var uv3 = new List<Vector2>();
            var uv4 = new List<Vector2>();
            var uv5 = new List<Vector2>();
            var uv6 = new List<Vector2>();
            var uv7 = new List<Vector2>();
            var uv8 = new List<Vector2>();
            for (int i = 0; i < repetitionCount; i++) {
                foreach (var index in source.Triangles) {
                    triangles.Add(index + source.Vertices.Count * i);
                }
                uv.AddRange(source.Mesh.uv);
                uv2.AddRange(source.Mesh.uv2);
                uv3.AddRange(source.Mesh.uv3);
                uv4.AddRange(source.Mesh.uv4);
#if UNITY_2018_2_OR_NEWER
                uv5.AddRange(source.Mesh.uv5);
                uv6.AddRange(source.Mesh.uv6);
                uv7.AddRange(source.Mesh.uv7);
                uv8.AddRange(source.Mesh.uv8);
#endif
            }

            // computing vertices and normals
            var bentVertices = new List<Vertex>(source.Vertices.Count);
            float offset = 0;
            for (int i = 0; i < repetitionCount; i++) {

                sampleCache.Clear();
                // for each mesh vertex, we found its projection on the curve
                foreach (var vert in source.Vertices) {
                    float distance = vert.position.x - source.MinX + offset;
                    CurveSample sample;
                    if (!sampleCache.TryGetValue(distance, out sample)) {
                        if (!useSpline) {
                            if (distance > curve.Length) continue;
                            sample = curve.GetSampleAtDistance(distance);
                        } else {
                            float distOnSpline = intervalStart + distance;
                            //if (true) { //spline.isLoop) {
                            while (distOnSpline > spline.Length) {
                                distOnSpline -= spline.Length;
                            }
                            //} else if (distOnSpline > spline.Length) {
                            //    continue;
                            //}
                            sample = spline.GetSampleAtDistance(distOnSpline);
                        }
                        sampleCache[distance] = sample;
                    }
                    bentVertices.Add(sample.GetBent(vert));
                }
                offset += source.Length;
            }

            MeshUtils.CopyMesh(result,
                source.Mesh,
                true,
                triangles,
                bentVertices.Select(b => b.position),
                bentVertices.Select(b => b.normal),
                uv,
                uv2,
                uv3,
                uv4,
                uv5,
                uv6,
                uv7,
                uv8);
        }

        //Custom adaptation by LUGILDE
        /*private void FillStretch() {
            var bentVertices = new List<Vertex>(source.Vertices.Count);
            sampleCache.Clear();
            var blendshapes = new List<Blendshape>();
            for (int i = 0; i < source.Mesh.blendShapeCount; i++)
            {
                blendshapes.Add(new Blendshape(source.Mesh, i));
            }

            for (int i = 0; i < source.Vertices.Count; i++)
            {
                var vert = source.Vertices[i];
                float distanceRate = source.Length == 0 ? 0 : Math.Abs(vert.position.x - source.MinX) / source.Length;
                CurveSample sample;
                if (!sampleCache.TryGetValue(distanceRate, out sample))
                {
                    sample = curve.GetSampleAtDistance(curve.Length * distanceRate);
                    sampleCache[distanceRate] = sample;
                }
                bentVertices.Add(sample.GetBent(vert));
                foreach (var blendshape in blendshapes)
                {
                    foreach (var frame in blendshape.frames)
                    {
                        var bentBlendVertex = sample.GetBent(frame.deltaVertices[i], frame.deltaNormals[i], frame.deltaTangents[i]);
                        frame.deltaVertices[i] = bentBlendVertex.position;
                        frame.deltaNormals[i] = bentBlendVertex.normal;
                        frame.deltaTangents[i] = bentBlendVertex.tangent;
                    }
                }
            }

            SplineMeshUtils.BuildMesh(curve, result,
                source.Mesh,
                source.Triangles,
                bentVertices.Select(b => b.position),
                bentVertices.Select(b => b.normal),
                blendshapes);

            if (TryGetComponent(out SkinnedMeshRenderer skinnedMeshRenderer))
            {
                skinnedMeshRenderer.sharedMesh = result;
                skinnedMeshRenderer.localBounds = skinnedMeshRenderer.sharedMesh.bounds;
            }
        }*/
        #endregion

        private void FillStretchParallel()
        {
            samples.Clear();
            bentVertices.Clear();
            sampleCache.Clear();
            blendshapes.Clear();
            for (int i = 0; i < blendshapesCount; i++)
            {
                blendshapes.Add(new OldBlendshape(sourceMesh, i));
            }
            var sourceLenght = source.Length;
            for (int i = 0; i < vertexCount; i++)
            {
                var vert = source.Vertices[i];
                float distanceRate = sourceLenght == 0 ? 0 : Math.Abs(vert.position.x - source.MinX) / sourceLenght;
                CurveSample sample;
                if (!sampleCache.TryGetValue(distanceRate, out sample))
                {
                    sample = curve.GetSampleAtDistance(curve.Length * distanceRate);
                    sampleCache[distanceRate] = sample;

                }
                samples.Add(sample);
            }

            NativeArray<Vertex> vertices = new NativeArray<Vertex>(vertexCount, Allocator.TempJob);
            NativeArray<Vector3> locations = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
            NativeArray<Quaternion> rotations = new NativeArray<Quaternion>(vertexCount, Allocator.TempJob);
            NativeArray<Vector2> scales = new NativeArray<Vector2>(vertexCount, Allocator.TempJob);
            NativeArray<float> rolls = new NativeArray<float>(vertexCount, Allocator.TempJob);

            vertices.CopyFrom(source.Vertices.ToArray());
            locations.CopyFrom(samples.Select(s => s.location).ToArray());
            rotations.CopyFrom(samples.Select(s => s.Rotation).ToArray());
            scales.CopyFrom(samples.Select(s => s.scale).ToArray());
            rolls.CopyFrom(samples.Select(s => s.roll).ToArray());

            GetBentJob getBentJob = new GetBentJob
            {
                vertices = vertices,
                locations = locations,
                rotations = rotations,
                scales = scales,
                rolls = rolls,
            };

            JobHandle getBentJobHandle = getBentJob.Schedule(vertexCount, 16);
            getBentJobHandle.Complete();


            NativeArray<Vector3> deltaVertices = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
            NativeArray<Vector3> deltaNormals = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
            NativeArray<Vector3> deltaTangents = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
            for (int i = 0; i < blendshapesCount; i++)
            {
                var blendshape = blendshapes[i];
                for (int j = 0; j < blendshape.frames.Length; j++)
                {
                    var frame = blendshape.frames[j];
                    
                    deltaVertices.CopyFrom(frame.deltaVertices);
                    deltaNormals.CopyFrom(frame.deltaNormals);
                    deltaTangents.CopyFrom(frame.deltaTangents);

                    GetBentShapesJob getBentShapesJob = new GetBentShapesJob
                    {
                        deltaVertices = deltaVertices,
                        deltaNormals = deltaNormals,
                        deltaTangents = deltaTangents,
                        locations = locations,
                        rotations = rotations,
                        scales = scales,
                        rolls = rolls,
                    };

                    JobHandle getBentShapesJobHandle = getBentShapesJob.Schedule(source.Vertices.Count, 100);
                    getBentShapesJobHandle.Complete();

                    frame.deltaVertices = deltaVertices.ToArray();
                    frame.deltaNormals = deltaNormals.ToArray();
                    frame.deltaTangents = deltaTangents.ToArray();
                    blendshape.frames[j] = frame;
                }
                blendshapes[i] = blendshape;
            }

            MeshUtils.BuildMesh2(result,
                source.Mesh,
                true,
                blendshapes.ToArray(),
                source.Triangles,
                vertices.Select(b => b.position),
                vertices.Select(b => b.normal));

            if (TryGetComponent(out SkinnedMeshRenderer skinnedMeshRenderer))
            {
                skinnedMeshRenderer.sharedMesh = result;
                skinnedMeshRenderer.localBounds = skinnedMeshRenderer.sharedMesh.bounds;
            }

            vertices.Dispose();
            locations.Dispose();
            rotations.Dispose();
            scales.Dispose();
            rolls.Dispose();
            /*deltaVertices.Dispose();
            deltaNormals.Dispose();
            deltaTangents.Dispose();*/
        }

        

        [BurstCompile]
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
                vp.x = 0;

                // application of the rotation + locatio
                Quaternion q = rotations[index] * Quaternion.Euler(0, -90, 0);
                vp = q * vp + locations[index];
                vn = q * vn;

                vertices[index] = new Vertex(vp, vn, vertices[index].uv);
            }
        }

        [BurstCompile]
        public struct GetBentShapesJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> deltaVertices;
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

                var vs = new float4(0, scale.y, scale.x, 0);
               
                var dv = deltaVertices[index];
                var dn = deltaNormals[index];
                var dt = deltaTangents[index];

                // application of scale
                dv = new Vector3(dv.x * vs.x, dv.y * vs.y, dv.z * vs.z);

                // application of roll
                dv = Quaternion.AngleAxis(roll, Vector3.right) * dv;
                dn = Quaternion.AngleAxis(roll, Vector3.right) * dn;
                dt = Quaternion.AngleAxis(roll, Vector3.right) * dt;

                // reset X value
                dv.x = 0;

                // application of the rotation
                Quaternion q = rotations[index] * Quaternion.Euler(0, -90, 0);
                dv = q * dv;
                dn = q * dn;
                dt = q * dt;

                deltaVertices[index] = dv;
                deltaNormals[index] = dn;
                deltaTangents[index] = dt;
            }
        }
    }
}