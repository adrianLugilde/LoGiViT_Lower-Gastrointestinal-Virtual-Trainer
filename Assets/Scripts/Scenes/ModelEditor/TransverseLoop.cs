using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using SplineMesh;
using LargeIntestine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ModelEditor
{
    [System.Serializable]
    public class TransverseLoop : BaseLoop
    {
        [Header("Transverse Loop Parameters")]
        [SerializeField, Range(0.2f, 0.35f)] 
        public float depth = 0.2f; // Displacement of U center along green axis
        
        [SerializeField, Range(-0.5f, 0.5f)] 
        public float lateralOffset = 0f; // Offset along red axis (loop coordinate system)
        
        [SerializeField, Range(0.01f, 0.5f)] 
        public float width = 0.25f; // Opening of U shape along blue axis (flow direction)
        
        [Header("Node Configuration")]
        [SerializeField, Range(3, 7)]
        public new int resolution = 4;
        
        [Header("U-Shape Tilt Parameters")]
        [SerializeField, Range(-35f, 35f)]
        public float tiltAroundUp = 0f; // Rotation around up axis (green/Y axis) (degrees)
        
        [SerializeField, Range(-50f, 10f)]
        public float tiltAroundFlow = -25f; // Rotation around flow axis (blue/Z axis) (degrees)

        [Header("Transition Smoothing")]
        [SerializeField, Range(0f, 1.5f)]
        public float blendingStrength = 1.0f; // How much transition smoothing affects adjacent nodes

        public TransverseLoop()
        {
            // N-loops specifically target the sigmoid segment
            targetSegment = LI_Segment.Transverse;
            loopName = "Transverse Loop";
        }

        public override string GetLoopTypeName() => "Transverse Loop";

        public override void GenerateLoop(Spline spline)
        {
            if (!enabled || spline == null || spline.nodes.Count == 0)
                return;

            GetSegmentIndices(out int startNodeIndex, out int endNodeIndex);

            if (startNodeIndex >= spline.nodes.Count || endNodeIndex >= spline.nodes.Count)
                return;

            int segmentNodeCount = endNodeIndex - startNodeIndex + 1;

            // Calculate loop-specific coordinate system (same as GammaLoop)
            var loopCoordinateSystem = CalculateLoopCoordinateSystem(spline, startNodeIndex, endNodeIndex);

            // Generate the U-shaped loop with real node modifications
            GenerateUShapeLoop(spline, startNodeIndex, endNodeIndex, segmentNodeCount, loopCoordinateSystem);
        }

        private void GenerateUShapeLoop(Spline spline, int startNodeIndex, int endNodeIndex, int segmentNodeCount, SegmentCoordinateSystem loopCoordinateSystem)
        {
            int loopNodeCount = Mathf.Min(resolution, segmentNodeCount);
            
            // Calculate loop start position - center the loop around positionInSegment
            int loopCenterNodeIndex = startNodeIndex + Mathf.RoundToInt((segmentNodeCount - 1) * positionInSegment);
            int loopStartNodeIndex = Mathf.Max(startNodeIndex, loopCenterNodeIndex - loopNodeCount / 2);
            int loopEndNodeIndex = Mathf.Min(endNodeIndex, loopStartNodeIndex + loopNodeCount - 1);
            
            // Ensure we have enough nodes
            if (loopEndNodeIndex <= loopStartNodeIndex || loopNodeCount < 3)
                return;

            // Calculate the displaced center (displaced down in the green axis by depth)
            Vector3 loopCenter = loopCoordinateSystem.segmentCenter;
            Vector3 displacedCenter = loopCenter - (loopCoordinateSystem.segmentDirection * depth) + (loopCoordinateSystem.segmentRight * lateralOffset);
            
            // Define the U-shape axes (same as GammaLoop coordinate system)
            Vector3 greenAxis = loopCoordinateSystem.segmentDirection;  // Y - vertical axis
            Vector3 blueAxis = loopCoordinateSystem.segmentUp;          // Z - flow direction (U opening)
            Vector3 redAxis = loopCoordinateSystem.segmentRight;        // X - lateral axis
            
            // Apply tilt rotations to the coordinate system (same as GammaLoop)
            // Rotation around up axis (green axis) (affects red and blue axes)
            if (Mathf.Abs(tiltAroundUp) > 0.01f)
            {
                Quaternion greenRotation = Quaternion.AngleAxis(tiltAroundUp, greenAxis);
                redAxis = greenRotation * redAxis;
                blueAxis = greenRotation * blueAxis;
            }
            
            // Rotation around flow axis (blue axis) (affects red and green axes)
            if (Mathf.Abs(tiltAroundFlow) > 0.01f)
            {
                Quaternion blueRotation = Quaternion.AngleAxis(tiltAroundFlow, blueAxis);
                redAxis = blueRotation * redAxis;
                greenAxis = blueRotation * greenAxis;
            }
            
            // Generate U-shape positions and modify the actual spline nodes
            for (int i = 0; i < loopNodeCount; i++)
            {
                int nodeIndex = loopStartNodeIndex + i;
                if (nodeIndex >= spline.nodes.Count) break;
                
                // Calculate parameter t for this node (0 to 1)
                float t = (float)i / (loopNodeCount - 1);
                
                // Generate U shape: bottom of U passes through the displaced center
                // U shape is created along blue axis (flow direction), with depth in green axis (downward)
                Vector3 uPosition = CalculateUShapePosition(t, displacedCenter, blueAxis, greenAxis, redAxis);
                
                // Calculate tangent direction for U shape
                Vector3 uTangent = CalculateUShapeTangent(t, blueAxis, greenAxis);
                
                // Set the new node position and direction
                spline.nodes[nodeIndex].Position = uPosition;
                spline.nodes[nodeIndex].Direction = uTangent.normalized;
                
                // Keep up vector aligned with the blue axis (flow direction)
                spline.nodes[nodeIndex].Up = blueAxis;
            }
            
            // Apply transition smoothing to blend with adjacent segments
            SmoothUTransitions(spline, loopStartNodeIndex, loopEndNodeIndex, loopCoordinateSystem);
            
            // Refresh the spline curves
            spline.RefreshCurves();
        }

        private Vector3 CalculateUShapePosition(float t, Vector3 displacedCenter, Vector3 blueAxis, Vector3 greenAxis, Vector3 redAxis)
        {
            // Redistribute t to achieve better node distribution along the U curve
            // Use a mapping that spreads nodes more evenly along arc length
            float redistributedT = RedistributeParameterForUShape(t);
            
            // Convert redistributed t from [0,1] to [-1,1] for symmetric U shape
            float u = (redistributedT - 0.5f) * 2f;
            
            // U shape components:
            // - Along blue axis (flow): linear progression from -width/2 to +width/2
            // - Along green axis (vertical): U shape where center goes DOWN and edges stay at displaced center level
            // The depth parameter controls how much the center goes below the displaced center
            
            float blueComponent = u * width; // Linear progression along flow direction
            
            // FIXED U-shape: center goes DOWN by depth amount, edges stay at displaced center level
            // This creates a proper U where increasing depth makes bottom deeper but keeps edges level
            float greenComponent = -depth * (1f - u * u); // Inverted parabola: 0 at edges, -depth at center
            
            Vector3 uPosition = displacedCenter + 
                              blueAxis * blueComponent + 
                              greenAxis * greenComponent;
            
            return uPosition;
        }

        private float RedistributeParameterForUShape(float t)
        {
            // Use arc-length parameterization approximation to distribute nodes more evenly
            // This prevents clustering at the bottom of the U
            
            // For a parabola y = depth * x^2 where x goes from -width to +width,
            // we want to redistribute t to follow the actual curve length more closely
            
            // Simple redistribution using a power function that spreads nodes away from center
            // This creates more uniform spacing along the U curve
            float redistribution = 0.6f; // Redistribution strength (0.5 = no change, higher = more spreading)
            
            if (t < 0.5f)
            {
                // Left side of U
                float leftT = t * 2f; // Convert to [0,1] for left side
                float redistributedLeft = Mathf.Pow(leftT, redistribution);
                return redistributedLeft * 0.5f; // Convert back to [0,0.5]
            }
            else
            {
                // Right side of U
                float rightT = (t - 0.5f) * 2f; // Convert to [0,1] for right side
                float redistributedRight = Mathf.Pow(rightT, redistribution);
                return 0.5f + redistributedRight * 0.5f; // Convert back to [0.5,1]
            }
        }

        private Vector3 CalculateUShapeTangent(float t, Vector3 blueAxis, Vector3 greenAxis)
        {
            // Calculate tangent based on the redistributed parameter and CORRECTED U shape derivative
            float redistributedT = RedistributeParameterForUShape(t);
            float u = (redistributedT - 0.5f) * 2f;
            
            // Derivative components for the CORRECTED U shape: greenComponent = -depth * (1 - u^2)
            float blueDerivative = width; // Constant along flow direction
            float greenDerivative = -depth * (-2f * u); // Derivative of -depth * (1 - u^2) = -depth * (-2u) = 2*depth*u
            
            Vector3 tangent = blueAxis * blueDerivative + greenAxis * greenDerivative;
            
            return tangent.normalized;
        }

        private void SmoothUTransitions(Spline spline, int loopStartIndex, int loopEndIndex, SegmentCoordinateSystem loopCoordinateSystem)
        {
            if (spline == null || spline.nodes.Count == 0) return;

            // Reduce transition length - only affect nodes very close to the loop
            int transitionLength = Mathf.Min(2, loopStartIndex); // Reduced from 5 to 2
            int endTransitionLength = Mathf.Min(2, spline.nodes.Count - loopEndIndex - 1); // Reduced from 5 to 2

            // Gentle smoothing with minimal adjustments
            SmoothStartTransitionGentle(spline, loopStartIndex, transitionLength, loopCoordinateSystem);
            SmoothEndTransitionGentle(spline, loopEndIndex, endTransitionLength, loopCoordinateSystem);
        }

        private void SmoothStartTransitionGentle(Spline spline, int loopStartIndex, int transitionLength, SegmentCoordinateSystem loopCoordinateSystem)
        {
            if (transitionLength <= 0 || loopStartIndex <= 0) return;

            // Get reference points
            Vector3 preLoopPosition = spline.nodes[Mathf.Max(0, loopStartIndex - transitionLength - 1)].Position;
            Vector3 preLoopDirection = spline.nodes[Mathf.Max(0, loopStartIndex - transitionLength - 1)].Direction;
            Vector3 loopStartPosition = spline.nodes[loopStartIndex].Position;
            Vector3 loopStartDirection = spline.nodes[loopStartIndex].Direction;

            // Smooth both positions and directions
            for (int i = 0; i < transitionLength; i++)
            {
                int nodeIndex = loopStartIndex - transitionLength + i;
                if (nodeIndex < 0 || nodeIndex >= spline.nodes.Count) continue;

                // Use gentle linear interpolation
                float t = (float)i / transitionLength;
                
                // Calculate distance-based blending: nodes closer to loop get stronger blending
                float distanceFromLoop = (float)(transitionLength - i) / transitionLength; // 1.0 = farthest, 0.0 = closest
                // Use much more aggressive blending - even distant nodes should be significantly affected
                float gradientFactor = 1.0f - 0.3f * distanceFromLoop; // Minimum 70% effect even for farthest nodes
                float gradientBlending = blendingStrength * gradientFactor;
                
                // Instead of interpolating between pre-loop and loop start, pull nodes toward the loop's displaced center
                Vector3 currentPosition = spline.nodes[nodeIndex].Position;
                Vector3 displacedCenter = loopCoordinateSystem.segmentCenter;
                
                // Calculate pull direction toward loop center (but not all the way)
                Vector3 pullDirection = (displacedCenter - currentPosition).normalized;
                float pullStrength = gradientBlending * 0.1f; // Gentle pull toward loop center
                Vector3 targetPosition = currentPosition + pullDirection * pullStrength;
                
                // Apply the position adjustment
                spline.nodes[nodeIndex].Position = targetPosition;
                
                // Interpolate directions
                Vector3 currentDirection = spline.nodes[nodeIndex].Direction;
                Vector3 targetDirection = Vector3.Slerp(preLoopDirection, loopStartDirection, t);
                
                // Apply gradient blending for direction adjustment
                spline.nodes[nodeIndex].Direction = Vector3.Slerp(currentDirection, targetDirection, gradientBlending).normalized;
            }
        }

        private void SmoothEndTransitionGentle(Spline spline, int loopEndIndex, int transitionLength, SegmentCoordinateSystem loopCoordinateSystem)
        {
            if (transitionLength <= 0 || loopEndIndex >= spline.nodes.Count - 1) return;

            // Get reference points
            Vector3 loopEndPosition = spline.nodes[loopEndIndex].Position;
            Vector3 loopEndDirection = spline.nodes[loopEndIndex].Direction;
            Vector3 postLoopPosition = spline.nodes[Mathf.Min(spline.nodes.Count - 1, loopEndIndex + transitionLength + 1)].Position;
            Vector3 postLoopDirection = spline.nodes[Mathf.Min(spline.nodes.Count - 1, loopEndIndex + transitionLength + 1)].Direction;

            // Smooth both positions and directions
            for (int i = 1; i <= transitionLength; i++)
            {
                int nodeIndex = loopEndIndex + i;
                if (nodeIndex >= spline.nodes.Count) break;

                // Use gentle linear interpolation
                float t = (float)i / transitionLength;
                
                // Calculate distance-based blending: nodes closer to loop get stronger blending
                float distanceFromLoop = (float)i / transitionLength; // 0.0 = closest to loop, 1.0 = farthest
                // Use much more aggressive blending - even distant nodes should be significantly affected
                float gradientFactor = 1.0f - 0.3f * distanceFromLoop; // Minimum 70% effect even for farthest nodes
                float gradientBlending = blendingStrength * gradientFactor;
                
                // Instead of interpolating between loop end and post-loop, pull nodes toward the loop's displaced center
                Vector3 currentPosition = spline.nodes[nodeIndex].Position;
                Vector3 displacedCenter = loopCoordinateSystem.segmentCenter;
                
                // Calculate pull direction toward loop center (but not all the way)
                Vector3 pullDirection = (displacedCenter - currentPosition).normalized;
                float pullStrength = gradientBlending * 0.1f; // Gentle pull toward loop center
                Vector3 targetPosition = currentPosition + pullDirection * pullStrength;
                
                // Apply the position adjustment
                spline.nodes[nodeIndex].Position = targetPosition;
                
                // Interpolate directions
                Vector3 currentDirection = spline.nodes[nodeIndex].Direction;
                Vector3 targetDirection = Vector3.Slerp(loopEndDirection, postLoopDirection, t);
                
                // Apply gradient blending for direction adjustment
                spline.nodes[nodeIndex].Direction = Vector3.Slerp(currentDirection, targetDirection, gradientBlending).normalized;
            }
        }

        public override void DrawGizmos(Spline spline)
        {
            if (!enabled || spline == null || spline.nodes.Count == 0)
                return;

            GetSegmentIndices(out int startNodeIndex, out int endNodeIndex);

            if (startNodeIndex >= spline.nodes.Count || endNodeIndex >= spline.nodes.Count)
                return;

#if UNITY_EDITOR
            // Get loop-specific coordinate system (same as GammaLoop)
            SegmentCoordinateSystem loopCoordinateSystem;
            try
            {
                loopCoordinateSystem = CalculateLoopCoordinateSystem(spline, startNodeIndex, endNodeIndex);
            }
            catch
            {
                return; // No loop system available
            }

            // Draw U-loop shape preview
            DrawTransverseLoopPreview(loopCoordinateSystem);

            // Draw coordinate system
            DrawCoordinateSystemForTransverse(loopCoordinateSystem);
#endif
        }

#if UNITY_EDITOR
        private void DrawTransverseLoopPreview(SegmentCoordinateSystem coordinateSystem)
        {
            Color originalColor = Handles.color;
            Handles.color = new Color(0.7f, 1f, 0.7f, 0.8f); // Light green for transverse loops

            // Calculate the displaced center (same as the logic in GenerateUShapeLoop)
            Vector3 loopCenter = coordinateSystem.segmentCenter;
            Vector3 displacedCenter = loopCenter - (coordinateSystem.segmentDirection * depth) + (coordinateSystem.segmentRight * lateralOffset);

            // Draw displaced center as magenta sphere (same as GammaLoop)
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(displacedCenter, 0.05f);

            // Define the U-shape axes (same as GenerateUShapeLoop)
            Vector3 greenAxis = coordinateSystem.segmentDirection;  // Y - vertical axis
            Vector3 blueAxis = coordinateSystem.segmentUp;          // Z - flow direction (U opening)
            Vector3 redAxis = coordinateSystem.segmentRight;        // X - lateral axis

            // Apply tilt rotations (same as GenerateUShapeLoop)
            if (Mathf.Abs(tiltAroundUp) > 0.01f)
            {
                Quaternion greenRotation = Quaternion.AngleAxis(tiltAroundUp, greenAxis);
                redAxis = greenRotation * redAxis;
                blueAxis = greenRotation * blueAxis;
            }

            if (Mathf.Abs(tiltAroundFlow) > 0.01f)
            {
                Quaternion blueRotation = Quaternion.AngleAxis(tiltAroundFlow, blueAxis);
                redAxis = blueRotation * redAxis;
                greenAxis = blueRotation * greenAxis;
            }

            int previewResolution = Mathf.Max(resolution, 8);
            Vector3[] points = new Vector3[previewResolution];

            // Generate U-shape points for preview
            for (int i = 0; i < previewResolution; i++)
            {
                float t = (float)i / (previewResolution - 1);
                points[i] = CalculateUShapePosition(t, displacedCenter, blueAxis, greenAxis, redAxis);
            }

            // Draw the U-curve
            Handles.color = new Color(0.7f, 1f, 0.7f, 0.8f);
            for (int i = 0; i < points.Length - 1; i++)
            {
                Handles.DrawLine(points[i], points[i + 1]);
            }

            // Draw node positions
            for (int i = 0; i < points.Length; i++)
            {
                Handles.DrawWireCube(points[i], Vector3.one * 0.02f * coordinateSystem.segmentSpan);
            }

            // Draw info label  
            Vector3 labelPos = coordinateSystem.segmentCenter + coordinateSystem.segmentUp * coordinateSystem.segmentSpan * 0.3f;
            Handles.Label(labelPos,
                $"Transverse Loop (U-Shape)\n" +
                $"Depth: {depth:F2}, Width: {width:F2}\n" +
                $"Lateral Offset: {lateralOffset:F2}\n" +
                $"Tilt Around Up: {tiltAroundUp:F1}°, Tilt Around Flow: {tiltAroundFlow:F1}°");

            Handles.color = originalColor;
        }

        private void DrawCoordinateSystemForTransverse(SegmentCoordinateSystem coordinateSystem)
        {
            float axisLength = 0.8f;

            // Draw coordinate system axes
            Gizmos.color = Color.red;
            Gizmos.DrawLine(coordinateSystem.segmentCenter, coordinateSystem.segmentCenter + coordinateSystem.segmentRight * axisLength);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(coordinateSystem.segmentCenter, coordinateSystem.segmentCenter + coordinateSystem.segmentDirection * axisLength);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(coordinateSystem.segmentCenter, coordinateSystem.segmentCenter + coordinateSystem.segmentUp * axisLength);

            // Draw center point
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(coordinateSystem.segmentCenter, 0.1f);
        }
#endif
    }
}
