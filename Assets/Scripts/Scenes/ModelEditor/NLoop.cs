using SplineMesh;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using LargeIntestine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ModelEditor
{
    [System.Serializable]
    public class NLoop : BaseLoop
    {
        [Header("N Loop Specific")]
        [Range(0.1f, 1.5f)]
        public float leftLegWidth = 0.8f; // Width of the left leg from center
        
        [Range(0.1f, 1.5f)]
        public float rightLegWidth = 1.0f; // Width of the right leg from center
        
        [Range(0.5f, 1.5f)]
        public float loopHeight = 1.5f; // Height of the N shape
        
        [Range(0.1f, 0.9f)]
        public float upperBendPosition = 0.7f; // Position of upper bend (0=start, 1=end)
        
        [Range(0.1f, 0.9f)]
        public float lowerBendPosition = 0.3f; // Position of lower bend (0=start, 1=end)
        
        [Range(0.1f, 0.8f)]
        public float bendSharpness = 0.3f; // How sharp the bends are (0=smooth, 1=sharp)
        
        [Range(0.0f, 1.0f)]
        public float leftLegHeight = 0.8f; // Relative height of left leg
        
        [Range(0.0f, 1.0f)]
        public float rightLegHeight = 0.9f; // Relative height of right leg
        
        [Range(-45f, 45f)]
        public float diagonalTiltDirection = 0f; // Tilt angle along flow direction (forward/backward)
        
        [Range(-45f, 45f)]
        public float diagonalTiltRight = 0f; // Tilt angle along right direction (left/right)
        
        [Range(0.0f, 1.0f)]
        public float asymmetryFactor = 0.1f; // Asymmetry in the N shape
        
        public NLoop()
        {
            // N-loops specifically target the sigmoid segment
            targetSegment = LI_Segment.Sigmoid;
            loopName = "N Loop";
        }
        
        public override string GetLoopTypeName()
        {
            return "N Loop";
        }
        
        public override void GenerateLoop(Spline spline)
        {
            Debug.Log($"Generating {GetLoopTypeName()} for spline with {positionInSegment} position in segment");
            if (!enabled || spline == null || spline.nodes.Count == 0)
                return;
                
            GetSegmentIndices(out int startNodeIndex, out int endNodeIndex);
            
            if (startNodeIndex >= spline.nodes.Count || endNodeIndex >= spline.nodes.Count)
                return;
                
            int segmentNodeCount = endNodeIndex - startNodeIndex + 1;
            
            // Use original segment coordinate system for loop generation
            var originalCoordinateSystem = GetOriginalSegmentCoordinateSystem();
            
            // Use Burst jobs for optimal performance
            GenerateLoopWithJobs(spline, startNodeIndex, endNodeIndex, segmentNodeCount, originalCoordinateSystem);
        }
        
        private void GenerateLoopWithJobs(Spline spline, int startNodeIndex, int endNodeIndex, int segmentNodeCount, SegmentCoordinateSystem coordinateSystem)
        {
            // Use ALL available nodes in the segment for the N-loop
            // The N-shape should span the entire segment to ensure proper distribution
            int loopNodeCount = segmentNodeCount;
            int loopStartNodeIndex = startNodeIndex;
            int loopEndNodeIndex = endNodeIndex;
            
            Debug.Log($"N-Loop using ALL segment nodes: {loopNodeCount} nodes from {loopStartNodeIndex} to {loopEndNodeIndex}");
            
            // Prepare data for Burst jobs
            var originalNodes = new NativeArray<SplineNodeData>(spline.nodes.Count, Allocator.TempJob);
            var loopResults = new NativeArray<SplineNodeData>(loopNodeCount, Allocator.TempJob);
            var nodeIndices = new NativeArray<int>(loopNodeCount, Allocator.TempJob);
            
            try
            {
                // Convert spline data to job-compatible format
                for (int i = 0; i < spline.nodes.Count; i++)
                {
                    var node = spline.nodes[i];
                    originalNodes[i] = new SplineNodeData
                    {
                        position = new float3(node.Position.x, node.Position.y, node.Position.z),
                        direction = new float3(node.Direction.x, node.Direction.y, node.Direction.z),
                        up = new float3(node.Up.x, node.Up.y, node.Up.z),
                        scale = node.Scale.x
                    };
                }
                
                // Set up node indices for the loop
                for (int i = 0; i < loopNodeCount; i++)
                {
                    nodeIndices[i] = loopStartNodeIndex + i;
                }
                
                // Convert coordinate system data and apply N-loop orientation
                Vector3 loopDirection = coordinateSystem.segmentDirection;  // X-axis: along segment flow (direction of the N progression)
                Vector3 loopUp = coordinateSystem.segmentUp;             // Y-axis: upward direction (for vertical legs)
                Vector3 loopRight = coordinateSystem.segmentRight;      // Z-axis: across segment width (for N width)
                
                var coordinateSystemData = new SegmentCoordinateSystemData
                {
                    segmentCenter = new float3(coordinateSystem.segmentCenter.x, coordinateSystem.segmentCenter.y, coordinateSystem.segmentCenter.z),
                    segmentDirection = new float3(coordinateSystem.segmentDirection.x, coordinateSystem.segmentDirection.y, coordinateSystem.segmentDirection.z),
                    segmentUp = new float3(coordinateSystem.segmentUp.x, coordinateSystem.segmentUp.y, coordinateSystem.segmentUp.z),
                    segmentRight = new float3(coordinateSystem.segmentRight.x, coordinateSystem.segmentRight.y, coordinateSystem.segmentRight.z),
                    segmentSpan = coordinateSystem.segmentSpan,
                    // For N-loop: loopDirection = flow direction, loopUp = vertical direction, spiralAxis = width direction
                    loopDirection = new float3(loopDirection.x, loopDirection.y, loopDirection.z),     // Flow direction (N progression along segment)
                    loopUp = new float3(loopUp.x, loopUp.y, loopUp.z),                // Up direction (vertical legs)
                    spiralAxis = new float3(loopRight.x, loopRight.y, loopRight.z) // Width direction (N width across segment)
                };
                
                // Set up N-loop parameters
                var parameters = new NLoopParameters
                {
                    leftLegWidth = leftLegWidth,
                    rightLegWidth = rightLegWidth,
                    loopHeight = loopHeight,
                    upperBendPosition = upperBendPosition,
                    lowerBendPosition = lowerBendPosition,
                    bendSharpness = bendSharpness,
                    leftLegHeight = leftLegHeight,
                    rightLegHeight = rightLegHeight,
                    diagonalTiltDirection = diagonalTiltDirection,
                    diagonalTiltRight = diagonalTiltRight,
                    asymmetryFactor = asymmetryFactor,
                    resolution = resolution,
                    positionInSegment = positionInSegment
                };
                
                // Create and schedule the Burst job
                var job = new NLoopGenerationJob
                {
                    originalNodes = originalNodes,
                    parameters = parameters,
                    coordinateSystem = coordinateSystemData,
                    nodeIndices = nodeIndices,
                    loopStartIndex = 0,
                    loopNodeCount = loopNodeCount,
                    loopResults = loopResults
                };
                
                // Execute job
                var jobHandle = job.Schedule(loopNodeCount, 1);
                jobHandle.Complete();
                
                // Copy results back to spline
                for (int i = 0; i < loopNodeCount; i++)
                {
                    int nodeIndex = loopStartNodeIndex + i;
                    var modifiedNode = loopResults[i];
                    
                    spline.nodes[nodeIndex].Position = new Vector3(modifiedNode.position.x, modifiedNode.position.y, modifiedNode.position.z);
                    spline.nodes[nodeIndex].Direction = new Vector3(modifiedNode.direction.x, modifiedNode.direction.y, modifiedNode.direction.z);
                    spline.nodes[nodeIndex].Up = new Vector3(modifiedNode.up.x, modifiedNode.up.y, modifiedNode.up.z);
                }
            }
            finally
            {
                // Always dispose NativeArrays
                if (originalNodes.IsCreated) originalNodes.Dispose();
                if (loopResults.IsCreated) loopResults.Dispose();
                if (nodeIndices.IsCreated) nodeIndices.Dispose();
            }
            
            // Apply transition smoothing
            SmoothSegmentTransitions(spline, loopStartNodeIndex, loopEndNodeIndex);
            
            spline.RefreshCurves();
        }
        
        public override void DrawGizmos(Spline spline)
        {
            if (!enabled || spline == null || spline.nodes.Count == 0)
                return;
                
            GetSegmentIndices(out int startNodeIndex, out int endNodeIndex);
            
            if (startNodeIndex >= spline.nodes.Count || endNodeIndex >= spline.nodes.Count)
                return;
            
            // Get original coordinate system
            SegmentCoordinateSystem originalCoordinateSystem;
            try
            {
                originalCoordinateSystem = GetOriginalSegmentCoordinateSystem();
            }
            catch
            {
                return; // No original system available
            }
            
            // Draw N-loop shape preview
            DrawNLoopPreview(originalCoordinateSystem);
            
            // Draw coordinate system
            DrawCoordinateSystem(originalCoordinateSystem);
        }
        
        private void DrawNLoopPreview(SegmentCoordinateSystem coordinateSystem)
        {
            Vector3 center = coordinateSystem.segmentCenter;
            Vector3 loopDirection = coordinateSystem.segmentDirection;  // Flow direction (N progression)
            Vector3 loopUp = coordinateSystem.segmentUp;             // Vertical direction (legs)
            Vector3 spiralAxis = coordinateSystem.segmentRight;      // Width direction (N width)
            
            // Scale factor for visualization
            float loopScale = coordinateSystem.segmentSpan * 0.4f;
            
            // Calculate N-shape points using arc-length-based distribution (like the actual implementation)
            int previewSteps = 20;
            Vector3[] nPoints = new Vector3[previewSteps + 1];
            
            for (int i = 0; i <= previewSteps; i++)
            {
                // Use the same arc-length-based parameter calculation as the job
                float t = CalculateArcLengthBasedParameterForGizmo(i, previewSteps + 1);
                Vector3 nPos = CalculateNShapeInCoordinateSystemForGizmo(t);
                
                // Apply leg width adjustments ONLY to first and last points (like the job)
                if (i == 0)
                {
                    // First point (left leg start): apply left leg width offset
                    nPos.x += -leftLegWidth; // Move backward in flow direction
                }
                else if (i == previewSteps)
                {
                    // Last point (right leg end): apply right leg width offset  
                    nPos.x += rightLegWidth; // Move forward in flow direction
                }
                // All other points (including diagonal) remain unchanged
                
                // Transform to world space using coordinate system
                nPoints[i] = center + (nPos.x * loopDirection + nPos.y * loopUp + nPos.z * spiralAxis) * loopScale;
            }
            
            // Draw the N shape
            Gizmos.color = new Color(0.9f, 0.2f, 0.9f, 0.8f); // Magenta for N-loop
            
            // Draw N-shape curve
            for (int i = 0; i < previewSteps; i++)
            {
                Gizmos.DrawLine(nPoints[i], nPoints[i + 1]);
            }
            
            // Draw key points based on actual bend positions
            Gizmos.color = Color.yellow;
            int lowerBendIndex = -1;
            int upperBendIndex = -1;
            
            // Find the closest preview points to the bend positions
            for (int i = 0; i <= previewSteps; i++)
            {
                float t = CalculateArcLengthBasedParameterForGizmo(i, previewSteps + 1);
                if (lowerBendIndex == -1 && t >= lowerBendPosition)
                    lowerBendIndex = i;
                if (upperBendIndex == -1 && t >= upperBendPosition)
                    upperBendIndex = i;
            }
            
            if (lowerBendIndex >= 0) Gizmos.DrawWireSphere(nPoints[lowerBendIndex], 0.05f); // Lower bend
            if (upperBendIndex >= 0) Gizmos.DrawWireSphere(nPoints[upperBendIndex], 0.05f); // Upper bend
            
            // Highlight the width-affected nodes
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(nPoints[0], 0.08f);                    // Left leg width control (first node)
            Gizmos.DrawWireSphere(nPoints[previewSteps], 0.08f);         // Right leg width control (last node)
            
            // Draw connection points to main spline
            Gizmos.color = new Color(0.9f, 0.2f, 0.9f, 0.4f);
            Gizmos.DrawLine(center, nPoints[0]);                    // Start connection
            Gizmos.DrawLine(center, nPoints[previewSteps]);         // End connection
            
            // Draw coordinate system axes for reference
            Gizmos.color = new Color(0.9f, 0.2f, 0.9f, 0.6f);
            float axisLength = loopScale * 0.8f;
            
            // Flow direction (loopDirection) - where N progresses
            Gizmos.DrawLine(center, center + loopDirection * axisLength);
            Gizmos.DrawSphere(center + loopDirection * axisLength, 0.03f);
            
            // Up direction (loopUp) - where legs extend
            Gizmos.DrawLine(center, center + loopUp * axisLength);
            Gizmos.DrawSphere(center + loopUp * axisLength, 0.03f);
            
            // Width direction (spiralAxis) - N width
            Gizmos.DrawLine(center, center + spiralAxis * axisLength);
            Gizmos.DrawSphere(center + spiralAxis * axisLength, 0.03f);
            
#if UNITY_EDITOR
            // Add parameter labels
            var originalColor = Handles.color;
            
            Handles.color = new Color(0.9f, 0.2f, 0.9f, 1f);
            Handles.Label(center + loopUp * (loopScale + 0.3f), 
                $"N Loop (Simple Width Control)\nLeft Width: {leftLegWidth:F2} (First Node)\nRight Width: {rightLegWidth:F2} (Last Node)\nHeight: {loopHeight:F2}\n" +
                $"Upper Bend: {upperBendPosition:F2}\nLower Bend: {lowerBendPosition:F2}\n" +
                $"Left Leg: {leftLegHeight:F2}\nRight Leg: {rightLegHeight:F2}\n" +
                $"Tilt Direction: {diagonalTiltDirection:F1}°\nTilt Right: {diagonalTiltRight:F1}°\n" +
                $"Flow Direction: Right Axis\nVertical: Up Axis\nWidth: Spiral Axis");
            
            Handles.color = originalColor;
#endif
        }
        
        private Vector3 CalculateNShapeInCoordinateSystemForGizmo(float t)
        {
            // Same logic as in the job but for gizmo visualization
            Vector3 position = Vector3.zero;
            
            if (t <= lowerBendPosition)
            {
                // Left leg: going UP from main spline
                float localT = t / lowerBendPosition;
                float bendT = Mathf.SmoothStep(0f, 1f, localT * (2f - bendSharpness));
                
                // Apply asymmetry to left leg - slightly offset X position and modify height
                float leftAsymmetry = asymmetryFactor * -0.3f; // Negative for left side
                
                position.x = localT * lowerBendPosition + leftAsymmetry * localT; // Asymmetric progression
                position.y = Mathf.Lerp(0f, loopHeight * leftLegHeight, bendT); // Go UP
                position.z = leftAsymmetry * 0.5f * localT; // Slight perpendicular offset for asymmetry
            }
            else if (t <= upperBendPosition)
            {
                // Diagonal section: going DOWN diagonally from left leg to right leg
                float localT = (t - lowerBendPosition) / (upperBendPosition - lowerBendPosition);
                float progressT = lowerBendPosition + localT * (upperBendPosition - lowerBendPosition);
                
                // Apply diagonal tilts in both axes separately with opposing directions at ends
                // Create a tilt that goes from -effect at start to +effect at end
                float directionTiltFactor = Mathf.Sin(diagonalTiltDirection * Mathf.Deg2Rad);
                float rightTiltFactor = Mathf.Sin(diagonalTiltRight * Mathf.Deg2Rad);
                
                // Use (localT - 0.5f) to create opposing movements: negative at start, positive at end
                float tiltMultiplier = (localT - 0.5f) * 2f; // Range from -1 to +1 across diagonal
                
                // Apply asymmetry to diagonal - creates curved diagonal instead of straight
                float diagonalAsymmetry = asymmetryFactor * Mathf.Sin(localT * Mathf.PI); // Sine curve for smooth asymmetry
                
                position.x = progressT + directionTiltFactor * 0.5f * tiltMultiplier + diagonalAsymmetry * 0.2f; // Asymmetric diagonal
                position.y = Mathf.Lerp(loopHeight * leftLegHeight, 
                                      -loopHeight * rightLegHeight, localT); // Go DOWN diagonally
                position.z = rightTiltFactor * 0.5f * tiltMultiplier + diagonalAsymmetry * 0.3f; // Asymmetric perpendicular displacement
            }
            else
            {
                // Right leg: going UP from diagonal end back to main spline
                float localT = (t - upperBendPosition) / (1f - upperBendPosition);
                float bendT = Mathf.SmoothStep(0f, 1f, localT * (2f - bendSharpness));
                float progressT = upperBendPosition + localT * (1f - upperBendPosition);
                
                // Apply diagonal tilt to the connection point (start of right leg)
                // This ensures the diagonal end point moves when tilt changes
                float directionTiltFactor = Mathf.Sin(diagonalTiltDirection * Mathf.Deg2Rad);
                float rightTiltFactor = Mathf.Sin(diagonalTiltRight * Mathf.Deg2Rad);
                
                // Gradually reduce tilt effect from full at connection point to none at spline
                // Use positive tilt (matching end of diagonal) that blends to zero
                float tiltBlend = 1f - bendT; // Full tilt at start, no tilt at end
                float endTiltMultiplier = 1f; // Positive tilt matching diagonal end
                
                // Apply asymmetry to right leg - opposite to left leg
                float rightAsymmetry = asymmetryFactor * 0.3f; // Positive for right side
                
                position.x = progressT + directionTiltFactor * 0.5f * endTiltMultiplier * tiltBlend + rightAsymmetry * localT; // Asymmetric progression
                position.y = Mathf.Lerp(-loopHeight * rightLegHeight, 0f, bendT); // Go UP to main spline
                position.z = rightTiltFactor * 0.5f * endTiltMultiplier * tiltBlend + rightAsymmetry * 0.5f * localT; // Asymmetric perpendicular offset
            }
            
            return position;
        }
        
        private float CalculateArcLengthBasedParameterForGizmo(int nodeIndex, int totalNodes)
        {
            // Same arc-length calculation as in the job, but for gizmo visualization
            if (totalNodes <= 1) return 0f;
            
            // Pre-calculate the approximate arc lengths of each section
            // Base N-shape without leg width modifications
            float leftLegLength = Mathf.Sqrt(
                Mathf.Pow(lowerBendPosition, 2) +  // X progression
                Mathf.Pow(loopHeight * leftLegHeight, 2) // Y displacement
            );
            
            float diagonalLength = Mathf.Sqrt(
                Mathf.Pow(upperBendPosition - lowerBendPosition, 2) + // X progression
                Mathf.Pow(loopHeight * (leftLegHeight + rightLegHeight), 2) // Y displacement
            );
            
            float rightLegLength = Mathf.Sqrt(
                Mathf.Pow(1f - upperBendPosition, 2) + // X progression
                Mathf.Pow(loopHeight * rightLegHeight, 2) // Y displacement
            );
            
            float totalArcLength = leftLegLength + diagonalLength + rightLegLength;
            
            // Calculate cumulative arc length for this node index
            float nodeProgress = (float)nodeIndex / (totalNodes - 1);
            float targetArcLength = nodeProgress * totalArcLength;
            
            // Map arc length back to parameter t
            if (targetArcLength <= leftLegLength)
            {
                // In the left leg section
                float sectionProgress = targetArcLength / leftLegLength;
                return sectionProgress * lowerBendPosition;
            }
            else if (targetArcLength <= leftLegLength + diagonalLength)
            {
                // In the diagonal section
                float sectionProgress = (targetArcLength - leftLegLength) / diagonalLength;
                return lowerBendPosition + sectionProgress * (upperBendPosition - lowerBendPosition);
            }
            else
            {
                // In the right leg section
                float sectionProgress = (targetArcLength - leftLegLength - diagonalLength) / rightLegLength;
                return upperBendPosition + sectionProgress * (1f - upperBendPosition);
            }
        }
        
        private void DrawCoordinateSystem(SegmentCoordinateSystem coordinateSystem)
        {
            float axisLength = 0.8f;
            
            // Draw coordinate system axes
            Gizmos.color = Color.red;
            Gizmos.DrawLine(coordinateSystem.segmentCenter, coordinateSystem.segmentCenter + coordinateSystem.segmentRight * axisLength);
            
            Gizmos.color = Color.green;
            Gizmos.DrawLine(coordinateSystem.segmentCenter, coordinateSystem.segmentCenter + coordinateSystem.segmentUp * axisLength);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(coordinateSystem.segmentCenter, coordinateSystem.segmentCenter + coordinateSystem.segmentDirection * axisLength);
            
            // Draw center point
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(coordinateSystem.segmentCenter, 0.1f);
        }
    }
}
