using System;
using System.Collections.Generic;
using SplineMesh;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using LargeIntestine;

namespace ModelEditor
{
    [System.Serializable]
    public abstract class BaseLoop
    {
        [Header("Base Loop Settings")]
        public string loopName = "New Loop";
        public bool enabled = true;
        public LI_Segment targetSegment = LI_Segment.Sigmoid;

        [Header("Position & Scale")]
        [Range(0f, 1f)]
        public float positionInSegment = 0.5f; // 0.0 = start, 0.5 = center, 0.8 = max

        [Header("Node Configuration")]
        [Range(3, 20)]
        public int resolution = 4;

        // Segment ranges for different intestinal segments
        protected readonly int[] ranges = { 4, 9, 20, 35, 40, 46 };

        // Cached coordinate system data
        [System.NonSerialized]
        private SegmentCoordinateSystem? cachedOriginalCoordinateSystem = null;
        [System.NonSerialized]
        private SegmentCoordinateSystem? cachedCurrentCoordinateSystem = null;
        [System.NonSerialized]
        private float cachedPositionInSegment = -1f;
        [System.NonSerialized]
        protected List<SplineNode> originalSplineNodes = null; // For coordinate system calculations

        [System.Serializable]
        public struct SegmentCoordinateSystem
        {
            public Vector3 segmentCenter;
            public Vector3 segmentDirection;
            public Vector3 segmentUp;
            public Vector3 segmentRight;
            public float segmentSpan;
        }

        /// <summary>
        /// Generate the loop on the specified spline
        /// </summary>
        public abstract void GenerateLoop(Spline spline);

        /// <summary>
        /// Draw gizmos for this loop in the scene view
        /// </summary>
        public abstract void DrawGizmos(Spline spline);

        /// <summary>
        /// Set the original spline nodes for this loop (call when loop is added or original spline changes)
        /// </summary>
        public virtual void SetOriginalSplineNodes(List<SplineNode> originalSplineNodes)
        {
            this.originalSplineNodes = originalSplineNodes;
            // Invalidate cached coordinate system since original spline changed
            InvalidateCoordinateSystemCache();
        }

        /// <summary>
        /// Clear the original spline nodes reference
        /// </summary>
        public virtual void ClearOriginalSplineNodes()
        {
            this.originalSplineNodes = null;
            InvalidateCoordinateSystemCache();
        }

        /// <summary>
        /// Get the loop type name for UI display
        /// </summary>
        public abstract string GetLoopTypeName();

        /// <summary>
        /// Helper method to get segment node indices
        /// </summary>
        protected void GetSegmentIndices(out int startNodeIndex, out int endNodeIndex)
        {
            int segmentIndex = (int)targetSegment;
            startNodeIndex = segmentIndex == 0 ? 0 : ranges[segmentIndex - 1] + 1;
            endNodeIndex = ranges[segmentIndex];
        }

        /// <summary>
        /// Get the original segment coordinate system (calculated from original spline nodes.
        /// If the positionInSegment has changed, it will recalculate the coordinate system.
        /// If the original spline nodes are not set, this will throw an exception.
        /// This should be used for loop generation to ensure loops are relative to the original segment.
        /// </summary>
        protected SegmentCoordinateSystem GetOriginalSegmentCoordinateSystem()
        {
            if (!cachedOriginalCoordinateSystem.HasValue || Math.Abs(cachedPositionInSegment - positionInSegment) > 0.001f)
            {
                if (originalSplineNodes == null)
                {
                    throw new InvalidOperationException("Original spline nodes not available. Call GenerateLoop with original spline nodes first.");
                }
                else
                {
                    cachedOriginalCoordinateSystem = CalculateSegmentCoordinateSystemFromNodes(originalSplineNodes);
                    cachedPositionInSegment = positionInSegment;
                }

            }
            return cachedOriginalCoordinateSystem.Value;
        }

        /// <summary>
        /// Get the current segment coordinate system based on the spline nodes.
        /// This will use the current spline nodes.
        /// If the positionInSegment has changed, it will recalculate the coordinate system.
        /// </summary>
        protected SegmentCoordinateSystem GetCurrentSegmentCoordinateSystem(Spline spline)
        {
            if (!cachedCurrentCoordinateSystem.HasValue || Math.Abs(cachedPositionInSegment - positionInSegment) > 0.001f)
            {
                cachedCurrentCoordinateSystem = CalculateSegmentCoordinateSystemFromNodes(spline.nodes);
                cachedPositionInSegment = positionInSegment;
            }
            return cachedCurrentCoordinateSystem.Value;
        }

        /// <summary>
        /// Calculate segment coordinate system from specific node collection
        /// </summary>
        private SegmentCoordinateSystem CalculateSegmentCoordinateSystemFromNodes(IList<SplineNode> nodes)
        {
            GetSegmentIndices(out int startNodeIndex, out int endNodeIndex);

            // Calculate segment bounds
            Vector3 segmentStartPos = nodes[startNodeIndex].Position;
            Vector3 segmentEndPos = nodes[endNodeIndex].Position;
            Vector3 segmentCenter = Vector3.Lerp(segmentStartPos, segmentEndPos, positionInSegment);

            // Calculate average flow direction from node directions in the segment
            Vector3 averageDirection = Vector3.zero;
            for (int i = startNodeIndex; i <= endNodeIndex; i++)
            {
                averageDirection += nodes[i].Direction.normalized;
            }
            averageDirection = averageDirection.normalized;

            // Calculate segment's local coordinate system
            Vector3 segmentDirection = averageDirection; // Primary flow direction
            Vector3 segmentUp = Vector3.up; // Start with world up
            Vector3 segmentRight = Vector3.Cross(segmentDirection, segmentUp).normalized;
            segmentUp = Vector3.Cross(segmentRight, segmentDirection).normalized; // Recompute for orthogonality

            // Calculate segment span
            float segmentSpan = 0f;
            for (int i = startNodeIndex; i < endNodeIndex; i++)
            {
                segmentSpan += Vector3.Distance(nodes[i].Position, nodes[i + 1].Position);
            }

            return new SegmentCoordinateSystem
            {
                segmentCenter = segmentCenter,
                segmentDirection = segmentDirection,
                segmentUp = segmentUp,
                segmentRight = segmentRight,
                segmentSpan = segmentSpan
            };
        }

        /// <summary>
        /// Force recalculation of coordinate system (call when original spline changes)
        /// </summary>
        public void InvalidateCoordinateSystemCache()
        {
            Debug.Log("Invalidating coordinate system cache");
            cachedOriginalCoordinateSystem = null;
            cachedCurrentCoordinateSystem = null;
            cachedPositionInSegment = -1f;
        }

        /// <summary>
        /// Calculate loop-specific coordinate system based on the actual loop nodes
        /// This provides better local coordinate system for loops compared to segment-wide system
        /// </summary>
        protected SegmentCoordinateSystem CalculateLoopCoordinateSystem(Spline spline, int startNodeIndex, int endNodeIndex)
        {
            int segmentNodeCount = endNodeIndex - startNodeIndex + 1;
            int loopNodeCount = Mathf.Min(resolution, segmentNodeCount);
            
            // Calculate loop start position - center the loop around positionInSegment
            int loopCenterNodeIndex = startNodeIndex + Mathf.RoundToInt((segmentNodeCount - 1) * positionInSegment);
            int loopStartNodeIndex = Mathf.Max(startNodeIndex, loopCenterNodeIndex - loopNodeCount / 2);
            int loopEndNodeIndex = Mathf.Min(endNodeIndex, loopStartNodeIndex + loopNodeCount - 1);
            
            // Ensure we have enough nodes
            if (loopEndNodeIndex <= loopStartNodeIndex)
            {
                // Fallback to segment coordinate system if we can't calculate loop system
                return GetCurrentSegmentCoordinateSystem(spline);
            }

            // Calculate center of loop nodes
            Vector3 loopCenter = Vector3.zero;
            for (int i = loopStartNodeIndex; i <= loopEndNodeIndex; i++)
            {
                if (i < spline.nodes.Count)
                    loopCenter += spline.nodes[i].Position;
            }
            loopCenter /= (loopEndNodeIndex - loopStartNodeIndex + 1);

            // Calculate direction as average direction of loop nodes
            Vector3 loopDirection = Vector3.zero;
            for (int i = loopStartNodeIndex; i <= loopEndNodeIndex; i++)
            {
                if (i < spline.nodes.Count)
                    loopDirection += spline.nodes[i].Direction;
            }
            loopDirection = (loopDirection / (loopEndNodeIndex - loopStartNodeIndex + 1)).normalized;

            // Calculate up as average up vector of loop nodes
            Vector3 loopUp = Vector3.zero;
            for (int i = loopStartNodeIndex; i <= loopEndNodeIndex; i++)
            {
                if (i < spline.nodes.Count)
                    loopUp += spline.nodes[i].Up;
            }
            loopUp = (loopUp / (loopEndNodeIndex - loopStartNodeIndex + 1)).normalized;

            // Calculate right vector
            Vector3 loopRight = Vector3.Cross(loopDirection, loopUp).normalized;

            // Recalculate up to ensure orthogonality
            loopUp = Vector3.Cross(loopRight, loopDirection).normalized;

            // Calculate span based on distance between first and last loop node
            float loopSpan = 0f;
            if (loopEndNodeIndex > loopStartNodeIndex && loopStartNodeIndex < spline.nodes.Count && loopEndNodeIndex < spline.nodes.Count)
            {
                loopSpan = Vector3.Distance(spline.nodes[loopStartNodeIndex].Position, spline.nodes[loopEndNodeIndex].Position);
            }

            return new SegmentCoordinateSystem
            {
                segmentCenter = loopCenter,
                segmentDirection = loopDirection,
                segmentUp = loopUp,
                segmentRight = loopRight,
                segmentSpan = loopSpan
            };
        }

        /// <summary>
        /// Optimized helper method to smooth transitions with adjacent nodes using Burst jobs
        /// </summary>
        protected void SmoothSegmentTransitions(Spline spline, int loopStartIndex, int loopEndIndex)
        {
            if (spline.nodes.Count == 0)
                return;

            // Convert to native arrays for Burst job
            var originalNodes = new NativeArray<SplineNodeData>(spline.nodes.Count, Allocator.TempJob);
            var modifiedNodes = new NativeArray<SplineNodeData>(spline.nodes.Count, Allocator.TempJob);

            try
            {
                // Convert spline data to job-compatible format
                for (int i = 0; i < spline.nodes.Count; i++)
                {
                    var node = spline.nodes[i];
                    var nodeData = new SplineNodeData
                    {
                        position = new float3(node.Position.x, node.Position.y, node.Position.z),
                        direction = new float3(node.Direction.x, node.Direction.y, node.Direction.z),
                        up = new float3(node.Up.x, node.Up.y, node.Up.z),
                        scale = node.Scale.x
                    };
                    originalNodes[i] = nodeData;
                }

                // Set up smoothing parameters
                var parameters = new SmoothingParameters
                {
                    blendStrength = 0.8f,
                    loopStartIndex = loopStartIndex,
                    loopEndIndex = loopEndIndex,
                    splineNodeCount = spline.nodes.Count
                };

                // Create and execute the smoothing job
                var smoothingJob = new SegmentTransitionSmoothingJob
                {
                    originalNodes = originalNodes,
                    parameters = parameters,
                    modifiedNodes = modifiedNodes
                };

                var jobHandle = smoothingJob.Schedule();
                jobHandle.Complete();

                // Copy results back to spline only for nodes that may have been modified
                int startRange = Mathf.Max(0, loopStartIndex - 2);
                int endRange = Mathf.Min(spline.nodes.Count - 1, loopEndIndex + 2);

                for (int i = startRange; i <= endRange; i++)
                {
                    var modifiedNode = modifiedNodes[i];
                    spline.nodes[i].Position = new Vector3(modifiedNode.position.x, modifiedNode.position.y, modifiedNode.position.z);
                    spline.nodes[i].Direction = new Vector3(modifiedNode.direction.x, modifiedNode.direction.y, modifiedNode.direction.z);
                    spline.nodes[i].Up = new Vector3(modifiedNode.up.x, modifiedNode.up.y, modifiedNode.up.z);
                }
            }
            finally
            {
                // Always dispose native arrays
                if (originalNodes.IsCreated) originalNodes.Dispose();
                if (modifiedNodes.IsCreated) modifiedNodes.Dispose();
            }
        }
    }
}
