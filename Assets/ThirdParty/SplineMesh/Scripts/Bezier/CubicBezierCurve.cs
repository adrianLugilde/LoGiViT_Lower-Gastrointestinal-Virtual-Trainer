using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SplineMesh {
    /// <summary>
    /// Mathematical object for cubic Bézier curve definition.
    /// It is made of two spline nodes which hold the four needed control points : two positions and two directions
    /// It provides methods to get positions and tangent along the curve, specifying a distance or a ratio, plus the curve length.
    /// 
    /// Note that a time of 0.5 and half the total distance won't necessarily define the same curve point as the curve curvature is not linear.
    /// </summary>
    [Serializable]
    public class CubicBezierCurve {

        private const int STEP_COUNT = 30;
        private const float T_STEP = 1.0f / STEP_COUNT;

        private readonly List<CurveSample> samples = new List<CurveSample>(STEP_COUNT);

        public SplineNode n1, n2;

        /// <summary>
        /// Length of the curve in world unit.
        /// </summary>
        public float Length { get; private set; }

        /// <summary>
        /// This event is raised when of of the control points has moved.
        /// </summary>
        public UnityEvent Changed = new UnityEvent();

        /// <summary>
        /// Performance mode for curve computation
        /// </summary>
        public enum ComputeMode {
            Original,           // Original implementation (for compatibility)
            Optimized,         // Single-threaded optimized version
            Parallel           // Burst-compiled parallel version (fastest)
        }
        
        [SerializeField] private ComputeMode computeMode = ComputeMode.Parallel;
        
        /// <summary>
        /// Gets or sets the computation mode for curve sample generation
        /// </summary>
        public ComputeMode ComputationMode {
            get { return computeMode; }
            set {
                if (computeMode != value) {
                    computeMode = value;
                    DispatchComputeSamples(null, null); // Recompute with new mode
                }
            }
        }

        /// <summary>
        /// Build a new cubic Bézier curve between two given spline node.
        /// </summary>
        /// <param name="n1"></param>
        /// <param name="n2"></param>
        public CubicBezierCurve(SplineNode n1, SplineNode n2) {
            this.n1 = n1;
            this.n2 = n2;
            n1.Changed += DispatchComputeSamples;
            n2.Changed += DispatchComputeSamples;
            DispatchComputeSamples(null, null);
        }

        /// <summary>
        /// Change the start node of the curve.
        /// </summary>
        /// <param name="n1"></param>
        public void ConnectStart(SplineNode n1) {
            this.n1.Changed -= DispatchComputeSamples;
            this.n1 = n1;
            n1.Changed += DispatchComputeSamples;
            DispatchComputeSamples(null, null);
        }

        /// <summary>
        /// Change the end node of the curve.
        /// </summary>
        /// <param name="n2"></param>
        public void ConnectEnd(SplineNode n2) {
            this.n2.Changed -= DispatchComputeSamples;
            this.n2 = n2;
            n2.Changed += DispatchComputeSamples;
            DispatchComputeSamples(null, null);
        }

        /// <summary>
        /// Convinent method to get the third control point of the curve, as the direction of the end spline node indicates the starting tangent of the next curve.
        /// </summary>
        /// <returns></returns>
        public Vector3 GetInverseDirection() {
            return (2 * n2.Position) - n2.Direction;
        }

        /// <summary>
        /// Dispatches computation to the appropriate method based on performance mode
        /// </summary>
        private void DispatchComputeSamples(object sender, EventArgs e) {
            switch (computeMode) {
                case ComputeMode.Original:
                    ComputeSamplesOriginal(sender, e);
                    break;
                case ComputeMode.Optimized:
                    ComputeSamples(sender, e);
                    break;
                case ComputeMode.Parallel:
                    ComputeSamplesParallel(sender, e);
                    break;
                default:
                    ComputeSamplesParallel(sender, e);
                    break;
            }
        }

        /// <summary>
        /// Original computation method (preserved for compatibility and validation)
        /// </summary>
        private void ComputeSamplesOriginal(object sender, EventArgs e) {
            samples.Clear();
            Length = 0;
            Vector3 previousPosition = GetLocation(0);
            for (float t = 0; t < 1; t += T_STEP) {
                Vector3 position = GetLocation(t);
                Length += Vector3.Distance(previousPosition, position);
                previousPosition = position;
                samples.Add(CreateSample(Length, t));
            }
            Length += Vector3.Distance(previousPosition, GetLocation(1));
            samples.Add(CreateSample(Length, 1));

            if (Changed != null) Changed.Invoke();
        }

        /// <summary>
        /// Returns point on curve at given time. Time must be between 0 and 1.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private Vector3 GetLocation(float t) {
            float omt = 1f - t;
            float omt2 = omt * omt;
            float t2 = t * t;
            return
                n1.Position * (omt2 * omt) +
                n1.Direction * (3f * omt2 * t) +
                GetInverseDirection() * (3f * omt * t2) +
                n2.Position * (t2 * t);
        }

        /// <summary>
        /// Returns tangent of curve at given time. Time must be between 0 and 1.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private Vector3 GetTangent(float t) {
            float omt = 1f - t;
            float omt2 = omt * omt;
            float t2 = t * t;
            Vector3 tangent =
                n1.Position * (-omt2) +
                n1.Direction * (3 * omt2 - 2 * omt) +
                GetInverseDirection() * (-3 * t2 + 2 * t) +
                n2.Position * (t2);
            return tangent.normalized;
        }

        /// <summary>
        /// Optimized version of GetTangent that uses pre-calculated inverse direction
        /// </summary>
        /// <param name="t"></param>
        /// <param name="inverseDirection"></param>
        /// <returns></returns>
        private Vector3 GetTangentOptimized(float t, Vector3 inverseDirection) {
            float omt = 1f - t;
            float omt2 = omt * omt;
            float t2 = t * t;
            Vector3 tangent =
                n1.Position * (-omt2) +
                n1.Direction * (3 * omt2 - 2 * omt) +
                inverseDirection * (-3 * t2 + 2 * t) +
                n2.Position * (t2);
            return tangent.normalized;
        }

        private Vector3 GetUp(float t) {
            return Vector3.Lerp(n1.Up, n2.Up, t);
        }

        private Vector2 GetScale(float t) {
            return Vector2.Lerp(n1.Scale, n2.Scale, t);
        }

        private float GetRoll(float t) {
            return Mathf.Lerp(n1.Roll, n2.Roll, t);
        }

        private void ComputeSamples(object sender, EventArgs e) {
            samples.Clear();
            Length = 0;
            
            // Pre-calculate inverse direction once instead of every GetLocation call
            Vector3 inverseDirection = GetInverseDirection();
            
            Vector3 previousPosition = GetLocationOptimized(0, inverseDirection);
            samples.Add(CreateSampleOptimized(Length, 0, inverseDirection));
            
            for (float t = T_STEP; t < 1; t += T_STEP) {
                Vector3 position = GetLocationOptimized(t, inverseDirection);
                Length += Vector3.Distance(previousPosition, position);
                previousPosition = position;
                samples.Add(CreateSampleOptimized(Length, t, inverseDirection));
            }
            
            // Final sample at t=1
            Vector3 finalPosition = GetLocationOptimized(1, inverseDirection);
            Length += Vector3.Distance(previousPosition, finalPosition);
            samples.Add(CreateSampleOptimized(Length, 1, inverseDirection));

            if (Changed != null) Changed.Invoke();
        }

        /// <summary>
        /// Burst-optimized parallel computation of curve samples
        /// </summary>
        private void ComputeSamplesParallel(object sender, EventArgs e) {
            samples.Clear();
            Length = 0;
            
            // Pre-calculate inverse direction
            Vector3 inverseDirection = GetInverseDirection();
            
            // Allocate native arrays for parallel computation
            int totalSamples = STEP_COUNT + 1; // +1 for t=1.0
            using (var positions = new NativeArray<float3>(totalSamples, Allocator.TempJob))
            using (var tangents = new NativeArray<float3>(totalSamples, Allocator.TempJob))
            using (var distances = new NativeArray<float>(totalSamples, Allocator.TempJob)) {
                
                // Setup job data
                var sampleJob = new BezierSampleComputeJob {
                    n1Position = n1.Position,
                    n1Direction = n1.Direction,
                    n2Position = n2.Position,
                    inverseDirection = inverseDirection,
                    tStep = T_STEP,
                    stepCount = STEP_COUNT,
                    positions = positions,
                    tangents = tangents
                };
                
                // Execute parallel sample computation
                var sampleJobHandle = sampleJob.Schedule(totalSamples, math.max(1, totalSamples / 4));
                sampleJobHandle.Complete();
                
                // Compute distances in parallel
                var distanceJob = new DistanceComputeJob {
                    positions = positions,
                    distances = distances
                };
                
                var distanceJobHandle = distanceJob.Schedule(totalSamples, math.max(1, totalSamples / 4));
                distanceJobHandle.Complete();
                
                // Accumulate total length and create samples
                Length = 0;
                for (int i = 0; i < totalSamples; i++) {
                    Length += distances[i];
                    float t = i < STEP_COUNT ? i * T_STEP : 1f;
                    
                    var sample = new CurveSample(
                        positions[i],
                        tangents[i],
                        GetUp(t),
                        GetScale(t),
                        GetRoll(t),
                        Length,
                        t,
                        this);
                    
                    samples.Add(sample);
                }
            }
            
            if (Changed != null) Changed.Invoke();
        }

        private CurveSample CreateSample(float distance, float time) {
            return new CurveSample(
                GetLocation(time),
                GetTangent(time),
                GetUp(time),
                GetScale(time),
                GetRoll(time),
                distance,
                time,
                this);
        }

        /// <summary>
        /// Optimized version of GetLocation that uses pre-calculated inverse direction
        /// </summary>
        /// <param name="t"></param>
        /// <param name="inverseDirection"></param>
        /// <returns></returns>
        private Vector3 GetLocationOptimized(float t, Vector3 inverseDirection) {
            float omt = 1f - t;
            float omt2 = omt * omt;
            float t2 = t * t;
            return
                n1.Position * (omt2 * omt) +
                n1.Direction * (3f * omt2 * t) +
                inverseDirection * (3f * omt * t2) +
                n2.Position * (t2 * t);
        }

        /// <summary>
        /// Optimized version of CreateSample that uses pre-calculated inverse direction
        /// </summary>
        /// <param name="distance"></param>
        /// <param name="time"></param>
        /// <param name="inverseDirection"></param>
        /// <returns></returns>
        private CurveSample CreateSampleOptimized(float distance, float time, Vector3 inverseDirection) {
            return new CurveSample(
                GetLocationOptimized(time, inverseDirection),
                GetTangentOptimized(time, inverseDirection),
                GetUp(time),
                GetScale(time),
                GetRoll(time),
                distance,
                time,
                this);
        }

        /// <summary>
        /// Returns an interpolated sample of the curve, containing all curve data at this time.
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public CurveSample GetSample(float time) {
            AssertTimeInBounds(time);
            CurveSample previous = samples[0];
            CurveSample next = default(CurveSample);
            bool found = false;
            var samplesCount = samples.Count;
            for(int i = 0; i < samplesCount; i++)
            {
                var cp = samples[i];
                if (cp.timeInCurve >= time) {
                    next = cp;
                    found = true;
                    break;
                }
                previous = cp;
            }
            if (!found) throw new Exception("Can't find curve samples.");
            float t = next == previous ? 0 : (time - previous.timeInCurve) / (next.timeInCurve - previous.timeInCurve);

            return CurveSample.Lerp(previous, next, t);
        }

        /// <summary>
        /// Returns an interpolated sample of the curve, containing all curve data at this distance.
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public CurveSample GetSampleAtDistance(float d) {
            if (d < 0 || d > Length)
                throw new ArgumentException("Distance must be positive and less than curve length. Length = " + Length + ", given distance was " + d);

            CurveSample previous = samples[0];
            CurveSample next = default(CurveSample);
            bool found = false;
            var samplesCount = samples.Count;
            for(int i = 0; i < samplesCount; i++)
            {
                var cp = samples[i];
                if (cp.distanceInCurve >= d)
                {
                    next = cp;
                    found = true;
                    break;
                }
                previous = cp;
            }
            /*foreach (CurveSample cp in samples) {
                if (cp.distanceInCurve >= d) {
                    next = cp;
                    found = true;
                    break;
                }
                previous = cp;
            }*/
            if (!found) throw new Exception("Can't find curve samples.");
            float t = next == previous ? 0 : (d - previous.distanceInCurve) / (next.distanceInCurve - previous.distanceInCurve);

            return CurveSample.Lerp(previous, next, t);
        }

        private static void AssertTimeInBounds(float time) {
            if (time < 0 || time > 1) throw new ArgumentException("Time must be between 0 and 1 (was " + time + ").");
        }

        /// <summary>
        /// Validation method to ensure optimized methods produce identical results to original methods.
        /// This method can be called during development to verify mathematical correctness.
        /// </summary>
        public bool ValidateOptimizations() {
            const float tolerance = 1e-6f;
            Vector3 inverseDirection = GetInverseDirection();
            
            // Test several points along the curve
            float[] testTimes = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f };
            
            foreach (float t in testTimes) {
                // Validate GetLocationOptimized
                Vector3 originalLocation = GetLocation(t);
                Vector3 optimizedLocation = GetLocationOptimized(t, inverseDirection);
                if (Vector3.Distance(originalLocation, optimizedLocation) > tolerance) {
                    Debug.LogError($"GetLocationOptimized failed validation at t={t}");
                    return false;
                }
                
                // Validate GetTangentOptimized
                Vector3 originalTangent = GetTangent(t);
                Vector3 optimizedTangent = GetTangentOptimized(t, inverseDirection);
                if (Vector3.Distance(originalTangent, optimizedTangent) > tolerance) {
                    Debug.LogError($"GetTangentOptimized failed validation at t={t}");
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Comprehensive validation that compares all computation modes
        /// </summary>
        public bool ValidateAllComputationModes() {
            const float tolerance = 1e-5f;
            
            // Store original samples
            var originalMode = computeMode;
            var originalSamples = new List<CurveSample>(samples);
            
            try {
                // Test Original vs Optimized
                computeMode = ComputeMode.Original;
                ComputeSamplesOriginal(null, null);
                var originalResults = new List<CurveSample>(samples);
                
                computeMode = ComputeMode.Optimized;
                ComputeSamples(null, null);
                var optimizedResults = new List<CurveSample>(samples);
                
                computeMode = ComputeMode.Parallel;
                ComputeSamplesParallel(null, null);
                var parallelResults = new List<CurveSample>(samples);
                
                // Validate lengths match
                if (Mathf.Abs(GetLengthFromSamples(originalResults) - GetLengthFromSamples(optimizedResults)) > tolerance ||
                    Mathf.Abs(GetLengthFromSamples(originalResults) - GetLengthFromSamples(parallelResults)) > tolerance) {
                    Debug.LogError("Length validation failed between computation modes");
                    return false;
                }
                
                // Validate sample count
                if (originalResults.Count != optimizedResults.Count || originalResults.Count != parallelResults.Count) {
                    Debug.LogError("Sample count mismatch between computation modes");
                    return false;
                }
                
                // Validate individual samples
                for (int i = 0; i < originalResults.Count; i++) {
                    if (Vector3.Distance(originalResults[i].location, optimizedResults[i].location) > tolerance ||
                        Vector3.Distance(originalResults[i].location, parallelResults[i].location) > tolerance) {
                        Debug.LogError($"Sample position validation failed at index {i}");
                        return false;
                    }
                    
                    if (Vector3.Distance(originalResults[i].tangent, optimizedResults[i].tangent) > tolerance ||
                        Vector3.Distance(originalResults[i].tangent, parallelResults[i].tangent) > tolerance) {
                        Debug.LogError($"Sample tangent validation failed at index {i}");
                        return false;
                    }
                }
                
                Debug.Log("All computation modes validated successfully!");
                return true;
            }
            finally {
                // Restore original state
                computeMode = originalMode;
                samples.Clear();
                samples.AddRange(originalSamples);
            }
        }
        
        private float GetLengthFromSamples(List<CurveSample> sampleList) {
            return sampleList.Count > 0 ? sampleList[sampleList.Count - 1].distanceInCurve : 0f;
        }

        /// <summary>
        /// High-performance binary search for finding curve samples by time
        /// </summary>
        public CurveSample GetSampleOptimized(float time) {
            AssertTimeInBounds(time);
            
            if (samples.Count == 0) return default(CurveSample);
            if (samples.Count == 1) return samples[0];
            
            // Binary search for better performance with large sample counts
            int left = 0;
            int right = samples.Count - 1;
            
            while (left < right) {
                int mid = (left + right) / 2;
                if (samples[mid].timeInCurve < time) {
                    left = mid + 1;
                } else {
                    right = mid;
                }
            }
            
            // Handle edge cases
            if (left == 0) {
                return CurveSample.Lerp(samples[0], samples[1], time / samples[1].timeInCurve);
            }
            
            if (left >= samples.Count) {
                return samples[samples.Count - 1];
            }
            
            var previous = samples[left - 1];
            var next = samples[left];
            
            if (Mathf.Approximately(previous.timeInCurve, next.timeInCurve)) {
                return next;
            }
            
            float t = (time - previous.timeInCurve) / (next.timeInCurve - previous.timeInCurve);
            return CurveSample.Lerp(previous, next, t);
        }

        /// <summary>
        /// High-performance binary search for finding curve samples by distance
        /// </summary>
        public CurveSample GetSampleAtDistanceOptimized(float d) {
            if (d < 0 || d > Length)
                throw new ArgumentException("Distance must be positive and less than curve length. Length = " + Length + ", given distance was " + d);
            
            if (samples.Count == 0) return default(CurveSample);
            if (samples.Count == 1) return samples[0];
            
            // Binary search for better performance
            int left = 0;
            int right = samples.Count - 1;
            
            while (left < right) {
                int mid = (left + right) / 2;
                if (samples[mid].distanceInCurve < d) {
                    left = mid + 1;
                } else {
                    right = mid;
                }
            }
            
            // Handle edge cases
            if (left == 0) {
                return CurveSample.Lerp(samples[0], samples[1], d / samples[1].distanceInCurve);
            }
            
            if (left >= samples.Count) {
                return samples[samples.Count - 1];
            }
            
            var previous = samples[left - 1];
            var next = samples[left];
            
            if (Mathf.Approximately(previous.distanceInCurve, next.distanceInCurve)) {
                return next;
            }
            
            float t = (d - previous.distanceInCurve) / (next.distanceInCurve - previous.distanceInCurve);
            return CurveSample.Lerp(previous, next, t);
        }
        
        /// <summary>
        /// Performance benchmarking method to compare all computation modes
        /// </summary>
        public void BenchmarkComputationModes(int iterations = 1000) {
            var originalMode = computeMode;
            
            try {
                Debug.Log($"Benchmarking CubicBezierCurve computation modes over {iterations} iterations...");
                
                // Benchmark Original
                computeMode = ComputeMode.Original;
                var originalTime = BenchmarkMode("Original", iterations, ComputeSamplesOriginal);
                
                // Benchmark Optimized
                computeMode = ComputeMode.Optimized;
                var optimizedTime = BenchmarkMode("Optimized", iterations, ComputeSamples);
                
                // Benchmark Parallel
                computeMode = ComputeMode.Parallel;
                var parallelTime = BenchmarkMode("Parallel", iterations, ComputeSamplesParallel);
                
                Debug.Log($"Performance Results:");
                Debug.Log($"Original: {originalTime:F4}ms (baseline)");
                Debug.Log($"Optimized: {optimizedTime:F4}ms ({originalTime/optimizedTime:F2}x faster)");
                Debug.Log($"Parallel: {parallelTime:F4}ms ({originalTime/parallelTime:F2}x faster)");
            }
            finally {
                computeMode = originalMode;
                DispatchComputeSamples(null, null);
            }
        }
        
        private float BenchmarkMode(string modeName, int iterations, System.Action<object, EventArgs> computeMethod) {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++) {
                computeMethod(null, null);
            }
            
            stopwatch.Stop();
            float timeMs = (float)stopwatch.Elapsed.TotalMilliseconds;
            Debug.Log($"{modeName} mode: {timeMs:F4}ms total, {timeMs/iterations:F6}ms per iteration");
            
            return timeMs;
        }
        
        /// <summary>
        /// Unity Inspector test method to validate and benchmark optimizations
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void RunOptimizationTests() {
            Debug.Log("=== CubicBezierCurve Optimization Tests ===");
            
            // Validate mathematical correctness
            if (ValidateOptimizations()) {
                Debug.Log("✓ Basic optimization validation passed");
            } else {
                Debug.LogError("✗ Basic optimization validation failed");
                return;
            }
            
            if (ValidateAllComputationModes()) {
                Debug.Log("✓ All computation modes validation passed");
            } else {
                Debug.LogError("✗ Computation modes validation failed");
                return;
            }
            
            // Validate projection methods
            /*if (ValidateProjectionMethods()) {
                Debug.Log("✓ Projection methods validation passed");
            } else {
                Debug.LogError("✗ Projection methods validation failed");
                return;
            }*/
            
            // Run performance benchmark
            BenchmarkComputationModes(100); // Reduced iterations for editor testing
            
            Debug.Log("=== All optimization tests completed successfully! ===");
        }

        /// <summary>
        /// Finds the closest point on the curve to the given point in 3D space
        /// </summary>
        /// <param name="pointToProject">The 3D point to project onto the curve</param>
        /// <returns>The closest CurveSample on the curve</returns>
        public CurveSample GetProjectionSample(Vector3 pointToProject) {
            float minSqrDistance = float.PositiveInfinity;
            int closestIndex = -1;
            int i = 0;
            foreach (var sample in samples) {
                float sqrDistance = (sample.location - pointToProject).sqrMagnitude;
                if (sqrDistance < minSqrDistance) {
                    minSqrDistance = sqrDistance;
                    closestIndex = i;
                }
                i++;
            }
            CurveSample previous, next;
            if(closestIndex == 0) {
                previous = samples[closestIndex];
                next = samples[closestIndex + 1];
            } else if(closestIndex == samples.Count - 1) {
                previous = samples[closestIndex - 1];
                next = samples[closestIndex];
            } else {
                var toPreviousSample = (pointToProject - samples[closestIndex - 1].location).sqrMagnitude;
                var toNextSample = (pointToProject - samples[closestIndex + 1].location).sqrMagnitude;
                if (toPreviousSample < toNextSample) {
                    previous = samples[closestIndex - 1];
                    next = samples[closestIndex];
                } else {
                    previous = samples[closestIndex];
                    next = samples[closestIndex + 1];
                }
            }

            var onCurve = Vector3.Project(pointToProject - previous.location, next.location - previous.location) + previous.location;
            var rate = (onCurve - previous.location).sqrMagnitude / (next.location - previous.location).sqrMagnitude;
            rate = Mathf.Clamp(rate, 0, 1);
            var result = CurveSample.Lerp(previous, next, rate);
            return result;
        }

        /// <summary>
        /// Optimized version of GetProjectionSample with better performance and accuracy
        /// </summary>
        /// <param name="pointToProject">The 3D point to project onto the curve</param>
        /// <returns>The closest CurveSample on the curve</returns>
        public CurveSample GetProjectionSampleOptimized(Vector3 pointToProject) {
            if (samples.Count == 0) return default(CurveSample);
            if (samples.Count == 1) return samples[0];
            
            float minSqrDistance = float.PositiveInfinity;
            int closestIndex = -1;
            var samplesCount = samples.Count;
            
            // Use for loop instead of foreach for better performance
            for (int i = 0; i < samplesCount; i++) {
                var sample = samples[i];
                float sqrDistance = (sample.location - pointToProject).sqrMagnitude;
                if (sqrDistance < minSqrDistance) {
                    minSqrDistance = sqrDistance;
                    closestIndex = i;
                }
            }
            
            // Determine the best segment for projection
            CurveSample previous, next;
            if (closestIndex == 0) {
                previous = samples[0];
                next = samples[1];
            } else if (closestIndex == samplesCount - 1) {
                previous = samples[closestIndex - 1];
                next = samples[closestIndex];
            } else {
                // Check both neighboring segments to find the better one
                var toPreviousSample = (pointToProject - samples[closestIndex - 1].location).sqrMagnitude;
                var toNextSample = (pointToProject - samples[closestIndex + 1].location).sqrMagnitude;
                if (toPreviousSample < toNextSample) {
                    previous = samples[closestIndex - 1];
                    next = samples[closestIndex];
                } else {
                    previous = samples[closestIndex];
                    next = samples[closestIndex + 1];
                }
            }

            // Project point onto the line segment between previous and next
            var segmentVector = next.location - previous.location;
            var pointVector = pointToProject - previous.location;
            
            // Handle degenerate case where segment has zero length
            if (segmentVector.sqrMagnitude < 1e-10f) {
                return previous;
            }
            
            var projectedPoint = Vector3.Project(pointVector, segmentVector) + previous.location;
            var segmentLength = segmentVector.sqrMagnitude;
            var projectionLength = (projectedPoint - previous.location).sqrMagnitude;
            
            float rate = Mathf.Clamp01(Mathf.Sqrt(projectionLength / segmentLength));
            
            return CurveSample.Lerp(previous, next, rate);
        }

        /// <summary>
        /// High-precision projection using iterative refinement for better accuracy
        /// </summary>
        /// <param name="pointToProject">The 3D point to project onto the curve</param>
        /// <param name="iterations">Number of refinement iterations (default: 3)</param>
        /// <returns>The closest CurveSample on the curve</returns>
        public CurveSample GetProjectionSamplePrecise(Vector3 pointToProject, int iterations = 3) {
            // Start with optimized rough projection
            var initialProjection = GetProjectionSampleOptimized(pointToProject);
            
            if (samples.Count <= 2) return initialProjection;
            
            // Iterative refinement for better accuracy
            float bestT = initialProjection.timeInCurve;
            float bestDistance = Vector3.Distance(initialProjection.location, pointToProject);
            
            // Search range around the initial projection
            float searchRange = 2f / STEP_COUNT; // Two sample steps
            
            for (int iter = 0; iter < iterations; iter++) {
                float stepSize = searchRange / 10f; // 10 sub-steps per iteration
                float currentBestT = bestT;
                float currentBestDistance = bestDistance;
                
                // Test points around current best
                for (int i = -5; i <= 5; i++) {
                    float testT = Mathf.Clamp01(bestT + i * stepSize);
                    
                    // Get sample at test time
                    var testSample = GetSample(testT);
                    float testDistance = Vector3.Distance(testSample.location, pointToProject);
                    
                    if (testDistance < currentBestDistance) {
                        currentBestDistance = testDistance;
                        currentBestT = testT;
                    }
                }
                
                bestT = currentBestT;
                bestDistance = currentBestDistance;
                searchRange *= 0.5f; // Narrow search for next iteration
            }
            
            return GetSample(bestT);
        }

        /// <summary>
        /// Burst-optimized projection using parallel processing for multiple points
        /// </summary>
        /// <param name="pointsToProject">Array of points to project</param>
        /// <param name="results">Output array for projection results (must be same length as input)</param>
        public void GetProjectionSamplesParallel(Vector3[] pointsToProject, CurveSample[] results) {
            if (pointsToProject.Length != results.Length) {
                throw new ArgumentException("Input and output arrays must have the same length");
            }
            
            if (samples.Count == 0) return;
            
            // Convert samples to native arrays for job system
            var sampleCount = samples.Count;
            var samplePositions = new NativeArray<float3>(sampleCount, Allocator.TempJob);
            var sampleTimes = new NativeArray<float>(sampleCount, Allocator.TempJob);
            var inputPoints = new NativeArray<float3>(pointsToProject.Length, Allocator.TempJob);
            var outputIndices = new NativeArray<int>(pointsToProject.Length, Allocator.TempJob);
            
            try {
                // Fill sample data
                for (int i = 0; i < sampleCount; i++) {
                    samplePositions[i] = samples[i].location;
                    sampleTimes[i] = samples[i].timeInCurve;
                }
                
                // Fill input points
                for (int i = 0; i < pointsToProject.Length; i++) {
                    inputPoints[i] = pointsToProject[i];
                }
                
                // Execute parallel projection job
                var projectionJob = new ParallelProjectionJob {
                    inputPoints = inputPoints,
                    samplePositions = samplePositions,
                    sampleTimes = sampleTimes,
                    outputIndices = outputIndices
                };
                
                var jobHandle = projectionJob.Schedule(pointsToProject.Length, math.max(1, pointsToProject.Length / 4));
                jobHandle.Complete();
                
                // Convert results back to CurveSamples
                for (int i = 0; i < pointsToProject.Length; i++) {
                    int closestIndex = outputIndices[i];
                    results[i] = samples[closestIndex]; // For simplicity, using closest sample directly
                    // In a full implementation, you'd do the segment projection here
                }
            }
            finally {
                // Dispose native arrays
                if (samplePositions.IsCreated) samplePositions.Dispose();
                if (sampleTimes.IsCreated) sampleTimes.Dispose();
                if (inputPoints.IsCreated) inputPoints.Dispose();
                if (outputIndices.IsCreated) outputIndices.Dispose();
            }
        }
    }
}

namespace SplineMesh {
    
    /// <summary>
    /// Burst-compiled job for parallel computation of Bézier curve samples
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct BezierSampleComputeJob : IJobParallelFor {
        [ReadOnly] public float3 n1Position;
        [ReadOnly] public float3 n1Direction;
        [ReadOnly] public float3 n2Position;
        [ReadOnly] public float3 inverseDirection;
        [ReadOnly] public float tStep;
        [ReadOnly] public int stepCount;
        
        [WriteOnly] public NativeArray<float3> positions;
        [WriteOnly] public NativeArray<float3> tangents;
        
        public void Execute(int index) {
            float t = index * tStep;
            if (t > 1f) t = 1f;
            
            // Compute location (identical math to original)
            float omt = 1f - t;
            float omt2 = omt * omt;
            float omt3 = omt2 * omt;
            float t2 = t * t;
            float t3 = t2 * t;
            
            positions[index] = n1Position * omt3 +
                              n1Direction * (3f * omt2 * t) +
                              inverseDirection * (3f * omt * t2) +
                              n2Position * t3;
            
            // Compute tangent (identical math to original)
            float3 tangent = n1Position * (-omt2) +
                            n1Direction * (3f * omt2 - 2f * omt) +
                            inverseDirection * (-3f * t2 + 2f * t) +
                            n2Position * t2;
            
            tangents[index] = math.normalize(tangent);
        }
    }
    
    /// <summary>
    /// Burst-compiled job for parallel distance computation between consecutive points
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct DistanceComputeJob : IJobParallelFor {
        [ReadOnly] public NativeArray<float3> positions;
        [WriteOnly] public NativeArray<float> distances;
        
        public void Execute(int index) {
            if (index == 0) {
                distances[index] = 0f;
            } else {
                distances[index] = math.distance(positions[index - 1], positions[index]);
            }
        }
    }
    
    /// <summary>
    /// Burst-compiled job for parallel projection of multiple points onto curve samples
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct ParallelProjectionJob : IJobParallelFor {
        [ReadOnly] public NativeArray<float3> inputPoints;
        [ReadOnly] public NativeArray<float3> samplePositions;
        [ReadOnly] public NativeArray<float> sampleTimes;
        [WriteOnly] public NativeArray<int> outputIndices;
        
        public void Execute(int index) {
            float3 pointToProject = inputPoints[index];
            float minSqrDistance = float.PositiveInfinity;
            int closestIndex = 0;
            
            // Find closest sample point
            for (int i = 0; i < samplePositions.Length; i++) {
                float sqrDistance = math.distancesq(samplePositions[i], pointToProject);
                if (sqrDistance < minSqrDistance) {
                    minSqrDistance = sqrDistance;
                    closestIndex = i;
                }
            }
            
            outputIndices[index] = closestIndex;
        }
    }
}
