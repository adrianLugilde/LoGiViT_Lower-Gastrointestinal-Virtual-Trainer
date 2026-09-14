using UnityEngine;
using SplineMesh;
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
    public class AlphaLoop : BaseLoop
    {
        [Header("Alpha Loop Parameters")]
        [SerializeField, Range(0f, 0.5f)] 
        public float height = 0f;
        
        [SerializeField, Range(-0.5f, 0.5f)] 
        public float lateralOffset = 0f; // Offset along red axis (loop coordinate system)
        
        [SerializeField, Range(0.01f, 0.5f)] 
        public float radius = 0.25f;
        
        [Header("Node Configuration")]
        [SerializeField, Range(3, 5)]
        public new int resolution = 4;
        
        [Header("Helix Parameters")]
        [SerializeField, Range(0.2f, 0.6f)]
        public float helixStartDistance = 0.2f; // Distance from center to start along red axis (negative direction)
        
        [SerializeField, Range(0.2f, 0.6f)]
        public float helixEndDistance = 0.2f; // Distance from center to end along red axis (positive direction)
        
        [Header("Helix Tilt Parameters")]
        [SerializeField, Range(-45f, 45f)]
        public float tiltAroundUp = 0f; // Rotation around up axis (blue/Z axis) (degrees)
        
        [SerializeField, Range(-50f, 10f)]
        public float tiltAroundFlow = 0f; // Rotation around flow axis (green/Y axis) (degrees)
        
        // Note: positionInSegment inherited from BaseLoop uses extended range [0, 1.35] for this loop type
        // This is handled in the custom editor to allow positioning beyond normal segment boundaries

        public override string GetLoopTypeName()
        {
            return "Alpha Loop";
        }

        public AlphaLoop()
        {
            // Alpha loops specifically target the sigmoid segment
            targetSegment = LI_Segment.Sigmoid;
            loopName = "Alpha Loop";
            
            // Override position in segment range for alpha loop
            positionInSegment = 0.5f; 
        }

        public override void GenerateLoop(Spline spline)
        {
            if (!enabled || spline == null || spline.nodes.Count == 0)
                return;

            GetSegmentIndices(out int startNodeIndex, out int endNodeIndex);

            if (startNodeIndex >= spline.nodes.Count || endNodeIndex >= spline.nodes.Count)
                return;

            int segmentNodeCount = endNodeIndex - startNodeIndex + 1;

            // Calculate loop-specific coordinate system
            var loopCoordinateSystem = CalculateLoopCoordinateSystem(spline, startNodeIndex, endNodeIndex);

            // Generate the actual loop with real node modifications
            GenerateCircleLoop(spline, startNodeIndex, endNodeIndex, segmentNodeCount, loopCoordinateSystem);
        }

        private void GenerateCircleLoop(Spline spline, int startNodeIndex, int endNodeIndex, int segmentNodeCount, SegmentCoordinateSystem loopCoordinateSystem)
        {
            int loopNodeCount = Mathf.Min(resolution, segmentNodeCount);
            
            // Calculate loop start position - center the loop around positionInSegment
            int loopCenterNodeIndex = startNodeIndex + Mathf.RoundToInt((segmentNodeCount - 1) * positionInSegment);
            int loopStartNodeIndex = Mathf.Max(startNodeIndex, loopCenterNodeIndex - loopNodeCount / 2);
            int loopEndNodeIndex = Mathf.Min(endNodeIndex, loopStartNodeIndex + loopNodeCount - 1);
            
            // Ensure we have enough nodes
            if (loopEndNodeIndex <= loopStartNodeIndex || loopNodeCount < 3)
                return;

            // Calculate the displaced center (same as the magenta sphere in gizmos)
            Vector3 loopCenter = loopCoordinateSystem.segmentCenter;
            Vector3 displacedCenter = loopCenter + (loopCoordinateSystem.segmentUp * height) + (loopCoordinateSystem.segmentRight * lateralOffset);
            
            // Define the helix axes for sigmoid segment
            Vector3 redAxis = loopCoordinateSystem.segmentRight;        // X - helix progression axis (unchanged)
            Vector3 blueAxis = loopCoordinateSystem.segmentUp;          // Z - up axis (was green)
            Vector3 greenAxis = loopCoordinateSystem.segmentDirection;  // Y - flow direction axis (was blue)
            
            // Apply tilt rotations to the coordinate system
            // Rotation around up axis (blue axis) (affects red and green axes)
            if (Mathf.Abs(tiltAroundUp) > 0.01f)
            {
                float blueRotationRad = tiltAroundUp * Mathf.Deg2Rad;
                Quaternion blueRotation = Quaternion.AngleAxis(tiltAroundUp, blueAxis);
                redAxis = blueRotation * redAxis;
                greenAxis = blueRotation * greenAxis;
            }
            
            // Rotation around flow axis (green axis) (affects red and blue axes)
            if (Mathf.Abs(tiltAroundFlow) > 0.01f)
            {
                float greenRotationRad = tiltAroundFlow * Mathf.Deg2Rad;
                Quaternion greenRotation = Quaternion.AngleAxis(tiltAroundFlow, greenAxis);
                redAxis = greenRotation * redAxis;
                blueAxis = greenRotation * blueAxis;
            }
            
                // Generate helix positions and modify the actual spline nodes
            for (int i = 0; i < loopNodeCount; i++)
            {
                int nodeIndex = loopStartNodeIndex + i;
                if (nodeIndex >= spline.nodes.Count) break;
                
                // Calculate parameter t for this node (0 to 1)
                float t = (float)i / (loopNodeCount - 1);
                
                // Calculate angle for circular component (exactly one full revolution)
                float angle = 2f * Mathf.PI * t; // One complete ring regardless of resolution
                
                // Calculate position in helix (blue-green circular plane) - inverted to start below and spiral upward
                Vector3 circularComponent = blueAxis * (-radius * Mathf.Cos(angle)) + 
                                          greenAxis * (radius * Mathf.Sin(angle));
                
                // Progressive deformation: start distance affects nodes near start (t=0), end distance affects nodes near end (t=1)
                // Use gentler falloff curves for more extended influence across more nodes
                float startInfluence = Mathf.Pow(1f - t, 1.2f); // Gentler falloff - affects more nodes on start side
                float endInfluence = Mathf.Pow(t, 1.2f);        // Gentler falloff - affects more nodes on end side                // Base position would be at center (no displacement)
                float basePosition = 0f;
                
                // Apply start distance with falloff
                float startDisplacement = -helixStartDistance * startInfluence;
                
                // Apply end distance with falloff  
                float endDisplacement = helixEndDistance * endInfluence;
                
                // Combine displacements
                float totalDisplacement = basePosition + startDisplacement + endDisplacement;
                
                Vector3 linearComponent = redAxis * totalDisplacement;
                
                Vector3 helixPosition = displacedCenter + circularComponent + linearComponent;
                
                // Calculate tangent direction for helix
                // Tangent combines circular tangent and linear progression - updated for inverted helix
                Vector3 circularTangent = blueAxis * (radius * Mathf.Sin(angle)) + 
                                        greenAxis * (radius * Mathf.Cos(angle));
                
                // Linear tangent should reflect the progressive deformation
                float tangentInfluence = (helixStartDistance + helixEndDistance) * 0.5f;
                Vector3 linearTangent = redAxis * tangentInfluence;
                
                Vector3 helixTangent = (circularTangent + linearTangent).normalized;
                
                // Set the new node position and direction
                spline.nodes[nodeIndex].Position = helixPosition;
                spline.nodes[nodeIndex].Direction = helixTangent;
                
                // Keep the original up vector or use the loop coordinate system's up (blue axis for sigmoid)
                spline.nodes[nodeIndex].Up = blueAxis;
            }
            
            // Apply enhanced transition smoothing to blend with adjacent segments
            SmoothHelixTransitions(spline, loopStartNodeIndex, loopEndNodeIndex, loopCoordinateSystem);
            
            // Refresh the spline curves
            spline.RefreshCurves();
        }

        private void SmoothHelixTransitions(Spline spline, int loopStartIndex, int loopEndIndex, SegmentCoordinateSystem loopCoordinateSystem)
        {
            if (spline == null || spline.nodes.Count == 0) return;

            int transitionLength = Mathf.Min(3, loopStartIndex); // Number of nodes to smooth before the loop
            int endTransitionLength = Mathf.Min(3, spline.nodes.Count - loopEndIndex - 1); // Number of nodes to smooth after the loop

            // Smooth the transition at the start of the loop
            SmoothStartTransition(spline, loopStartIndex, transitionLength, loopCoordinateSystem);
            
            // Smooth the transition at the end of the loop
            SmoothEndTransition(spline, loopEndIndex, endTransitionLength, loopCoordinateSystem);
        }

        private void SmoothStartTransition(Spline spline, int loopStartIndex, int transitionLength, SegmentCoordinateSystem loopCoordinateSystem)
        {
            if (transitionLength <= 0 || loopStartIndex <= 0) return;

            // Get the original segment direction before the loop
            Vector3 preLoopDirection = spline.nodes[Mathf.Max(0, loopStartIndex - transitionLength - 1)].Direction;
            Vector3 loopStartPosition = spline.nodes[loopStartIndex].Position;
            Vector3 loopStartDirection = spline.nodes[loopStartIndex].Direction;

            // Smooth the positions and directions of nodes leading into the loop
            for (int i = 0; i < transitionLength; i++)
            {
                int nodeIndex = loopStartIndex - transitionLength + i;
                if (nodeIndex < 0 || nodeIndex >= spline.nodes.Count) continue;

                float t = (float)i / (transitionLength - 1); // 0 at start of transition, 1 at loop start
                t = Mathf.SmoothStep(0, 1, t); // Apply smooth interpolation curve

                // Get the original position for this node
                Vector3 originalPos = spline.nodes[nodeIndex].Position;
                
                // Calculate a transition position that gradually approaches the loop
                Vector3 targetDirection = Vector3.Slerp(preLoopDirection, loopStartDirection, t);
                
                // Adjust position to create smooth approach
                Vector3 adjustedPos = Vector3.Lerp(originalPos, loopStartPosition - targetDirection * (1f - t) * 0.2f, t * 0.5f);
                
                // Apply the smoothed values
                spline.nodes[nodeIndex].Position = adjustedPos;
                spline.nodes[nodeIndex].Direction = targetDirection.normalized;
            }
        }

        private void SmoothEndTransition(Spline spline, int loopEndIndex, int transitionLength, SegmentCoordinateSystem loopCoordinateSystem)
        {
            if (transitionLength <= 0 || loopEndIndex >= spline.nodes.Count - 1) return;

            // Get the original segment direction after the loop
            Vector3 postLoopDirection = loopCoordinateSystem.segmentDirection;
            if (loopEndIndex + transitionLength + 1 < spline.nodes.Count)
            {
                postLoopDirection = spline.nodes[loopEndIndex + transitionLength + 1].Direction;
            }
            
            Vector3 loopEndPosition = spline.nodes[loopEndIndex].Position;
            Vector3 loopEndDirection = spline.nodes[loopEndIndex].Direction;

            // Smooth the positions and directions of nodes exiting the loop
            for (int i = 1; i <= transitionLength; i++)
            {
                int nodeIndex = loopEndIndex + i;
                if (nodeIndex >= spline.nodes.Count) break;

                float t = (float)i / (transitionLength + 1); // 0 at loop end, 1 at end of transition
                t = Mathf.SmoothStep(0, 1, t); // Apply smooth interpolation curve

                // Get the original position for this node
                Vector3 originalPos = spline.nodes[nodeIndex].Position;
                
                // Calculate a transition direction that gradually moves away from the loop influence
                Vector3 transitionDirection = Vector3.Slerp(loopEndDirection, postLoopDirection, t);
                
                // Calculate expected position based on smooth progression
                Vector3 expectedPos = loopEndPosition + transitionDirection * t * 0.3f;
                
                // Blend between original and expected position for smooth transition
                Vector3 smoothedPos = Vector3.Lerp(originalPos, expectedPos, (1f - t) * 0.7f);
                
                // Apply the smoothed values with gentle influence
                spline.nodes[nodeIndex].Position = Vector3.Lerp(originalPos, smoothedPos, 0.6f);
                spline.nodes[nodeIndex].Direction = transitionDirection.normalized;
            }
        }

        public override void DrawGizmos(Spline spline)
        {
            if (!enabled || spline == null || spline.nodes.Count == 0)
                return;

            GetSegmentIndices(out int startNodeIndex, out int endNodeIndex);

            if (startNodeIndex >= spline.nodes.Count || endNodeIndex >= spline.nodes.Count)
                return;

            // Get current segment coordinate system
            var segmentCoordinateSystem = GetCurrentSegmentCoordinateSystem(spline);

            // Calculate loop-specific coordinate system
            var loopCoordinateSystem = CalculateLoopCoordinateSystem(spline, startNodeIndex, endNodeIndex);

            DrawAlphaLoopPreview(segmentCoordinateSystem, loopCoordinateSystem);
        }

        private void DrawAlphaLoopPreview(SegmentCoordinateSystem segmentCoordinateSystem, SegmentCoordinateSystem loopCoordinateSystem)
        {
            // Calculate the position based on positionInSegment using segment coordinate system
            Vector3 segmentStart = segmentCoordinateSystem.segmentCenter - (segmentCoordinateSystem.segmentDirection * segmentCoordinateSystem.segmentSpan * 0.5f);
            Vector3 segmentEnd = segmentCoordinateSystem.segmentCenter + (segmentCoordinateSystem.segmentDirection * segmentCoordinateSystem.segmentSpan * 0.5f);
            
            // Get position along segment based on positionInSegment parameter
            Vector3 basePosition = Vector3.Lerp(segmentStart, segmentEnd, positionInSegment);
            
            // Displace upward in the segment coordinate system's "up" axis (controlled by height parameter)
            Vector3 alphaPosition = basePosition + (segmentCoordinateSystem.segmentUp * height);
            
            // Create a copy of the loop center displaced up along the blue axis (up axis) by the height value
            // and offset laterally along the red axis
            Vector3 loopCenter = loopCoordinateSystem.segmentCenter;
            Vector3 loopCenterCopy = loopCenter + (loopCoordinateSystem.segmentUp * height) + (loopCoordinateSystem.segmentRight * lateralOffset);
            
            // Draw the alpha loop gizmo
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(alphaPosition, 0.1f);
            
            // Draw a line from base position to displaced position to show the displacement
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(basePosition, alphaPosition);
            
            // Draw the loop center copy
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(loopCenterCopy, 0.08f);
            
            // Draw a line from original loop center to the copy to show the displacement along blue axis
            Gizmos.color = Color.green;
            Gizmos.DrawLine(loopCenter, loopCenterCopy);
            
            // Draw circle of nodes around the displaced center in the blue-green plane
            DrawCircleOfNodes(loopCenterCopy, loopCoordinateSystem);
            
            // Draw loop coordinate system (smaller, more opaque) 
            DrawCoordinateSystem(loopCoordinateSystem, "Loop", Color.magenta, 0.25f, 1.0f);
        }

        private void DrawCircleOfNodes(Vector3 center, SegmentCoordinateSystem loopCoordinateSystem)
        {
            if (resolution < 3) return; // Need at least 3 nodes for a helix
            
            // Define the helix axes for sigmoid segment
            Vector3 redAxis = loopCoordinateSystem.segmentRight;       // X - helix progression axis (unchanged)
            Vector3 blueAxis = loopCoordinateSystem.segmentUp;         // Z - up axis (was green)
            Vector3 greenAxis = loopCoordinateSystem.segmentDirection; // Y - flow direction axis (was blue)
            
            // Apply tilt rotations to the coordinate system
            // Rotation around up axis (blue axis) (affects red and green axes)
            if (Mathf.Abs(tiltAroundUp) > 0.01f)
            {
                Quaternion blueRotation = Quaternion.AngleAxis(tiltAroundUp, blueAxis);
                redAxis = blueRotation * redAxis;
                greenAxis = blueRotation * greenAxis;
            }
            
            // Rotation around flow axis (green axis) (affects red and blue axes)
            if (Mathf.Abs(tiltAroundFlow) > 0.01f)
            {
                Quaternion greenRotation = Quaternion.AngleAxis(tiltAroundFlow, greenAxis);
                redAxis = greenRotation * redAxis;
                blueAxis = greenRotation * blueAxis;
            }
            
            // Create helix nodes
            Vector3[] helixNodes = new Vector3[resolution];
            
            for (int i = 0; i < resolution; i++)
            {
                // Calculate parameter t for this node (0 to 1)
                float t = (float)i / (resolution - 1);
                
                // Calculate angle for circular component (exactly one full revolution)
                float angle = 2f * Mathf.PI * t; // One complete ring regardless of resolution
                
                // Calculate position in helix (blue-green circular plane) - inverted to start below and spiral upward
                Vector3 circularComponent = blueAxis * (-radius * Mathf.Cos(angle)) + 
                                          greenAxis * (radius * Mathf.Sin(angle));
                
                // Progressive deformation: start distance affects nodes near start (t=0), end distance affects nodes near end (t=1)
                // Use more balanced influence curves - reduce tip dominance while maintaining gradual falloff
                float startInfluence = 0.7f + 0.3f * Mathf.Pow(1f - t, 0.8f); // Tips get 1.0, nearby nodes get 0.85-0.95
                float endInfluence = 0.7f + 0.3f * Mathf.Pow(t, 0.8f);        // Tips get 1.0, nearby nodes get 0.85-0.95
                
                // Base position would be at center (no displacement)
                float basePosition = 0f;
                
                // Apply start distance with falloff
                float startDisplacement = -helixStartDistance * startInfluence;
                
                // Apply end distance with falloff  
                float endDisplacement = helixEndDistance * endInfluence;
                
                // Combine displacements
                float totalDisplacement = basePosition + startDisplacement + endDisplacement;
                
                Vector3 linearComponent = redAxis * totalDisplacement;
                
                helixNodes[i] = center + circularComponent + linearComponent;
            }
            
            // Draw the helix nodes
            Gizmos.color = Color.red;
            for (int i = 0; i < helixNodes.Length; i++)
            {
                Gizmos.DrawWireSphere(helixNodes[i], 0.05f);
            }
            
            // Draw lines connecting the nodes to form the helix curve
            Gizmos.color = Color.cyan;
            for (int i = 0; i < helixNodes.Length - 1; i++)
            {
                Gizmos.DrawLine(helixNodes[i], helixNodes[i + 1]);
            }
            
            // Draw start and end markers
            if (helixNodes.Length > 0)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(helixNodes[0], 0.08f);
                
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(helixNodes[helixNodes.Length - 1], 0.08f);
            }
            
            // Draw helix progression axis (red axis line)
            Vector3 helixStart = center - redAxis * helixStartDistance;
            Vector3 helixEnd = center + redAxis * helixEndDistance;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(helixStart, helixEnd);
            
            // Draw tilted coordinate system axes if there's any tilt
            if (Mathf.Abs(tiltAroundUp) > 0.01f || Mathf.Abs(tiltAroundFlow) > 0.01f)
            {
                float axisLength = 0.3f;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(center, center + redAxis * axisLength);   // X - helix progression
                Gizmos.color = Color.green;
                Gizmos.DrawLine(center, center + greenAxis * axisLength); // Y - flow direction
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(center, center + blueAxis * axisLength);  // Z - up axis
            }
            
            // Draw circular plane outline at center for reference - inverted to match helix direction
            Gizmos.color = Color.gray;
            Vector3[] circlePoints = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                float angle = (2f * Mathf.PI * i) / 12;
                Vector3 localPosition = blueAxis * (-radius * Mathf.Cos(angle)) + 
                                       greenAxis * (radius * Mathf.Sin(angle));
                circlePoints[i] = center + localPosition;
            }
            
            for (int i = 0; i < circlePoints.Length; i++)
            {
                int nextIndex = (i + 1) % circlePoints.Length;
                Gizmos.DrawLine(circlePoints[i], circlePoints[nextIndex]);
            }
        }

        private void DrawCoordinateSystem(SegmentCoordinateSystem coordinateSystem, string label, Color baseColor, float axisLength, float alpha)
        {
            Vector3 center = coordinateSystem.segmentCenter;
            
            // Draw coordinate system axes with different colors and transparency
            Color rightColor = Color.red;
            rightColor.a = alpha;
            Gizmos.color = rightColor;
            Gizmos.DrawLine(center, center + coordinateSystem.segmentRight * axisLength); // X-axis (red)
            
            Color directionColor = Color.green;
            directionColor.a = alpha;
            Gizmos.color = directionColor;
            Gizmos.DrawLine(center, center + coordinateSystem.segmentDirection * axisLength); // Y-axis (green)
            
            Color upColor = Color.blue;
            upColor.a = alpha;
            Gizmos.color = upColor;
            Gizmos.DrawLine(center, center + coordinateSystem.segmentUp * axisLength); // Z-axis (blue)
            
            // Draw a small sphere at the center
            Color centerColor = baseColor;
            centerColor.a = alpha;
            Gizmos.color = centerColor;
            Gizmos.DrawWireSphere(center, axisLength * 0.1f);

#if UNITY_EDITOR
            // Draw text label
            UnityEditor.Handles.color = centerColor;
            UnityEditor.Handles.Label(center + Vector3.up * (axisLength + 0.1f), label);
#endif
        }
    }
}
