using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ModelEditor
{
    // Data structures for Burst jobs (using Unity.Mathematics types)
    public struct SplineNodeData
    {
        public float3 position;
        public float3 direction;
        public float3 up;
        public float scale;
    }
    
    public struct AlphaLoopParameters
    {
        public float radius;
        public float tiltAngle;
        public float openingAngle;
        public float rotationAroundFlow;
        public float spiralStart;
        public float spiralEnd;
        public float spiralVariation;
        public int resolution;
        public float positionInSegment;
    }
    
    public struct ReversedAlphaLoopParameters
    {
        public float radius;
        public float tiltAngle;
        public float openingAngle;
        public float rotationAroundFlow;
        public float spiralStart;
        public float spiralEnd;
        public float spiralVariation;
        public int resolution;
        public float positionInSegment;
    }
    
    public struct NLoopParameters
    {
        public float leftLegWidth;
        public float rightLegWidth;
        public float loopHeight;
        public float upperBendPosition;
        public float lowerBendPosition;
        public float bendSharpness;
        public float leftLegHeight;
        public float rightLegHeight;
        public float diagonalTiltDirection; // Tilt along flow direction (X-axis)
        public float diagonalTiltRight;     // Tilt along right direction (Z-axis)
        public float asymmetryFactor;
        public int resolution;
        public float positionInSegment;
    }
    
    public struct TransverseLoopParameters
    {
        public float depth;
        public float width;
        public float asymmetry;
        public float curvatureSmoothing;
        public float sagPosition;
        public float lateralOffset;
        public float tiltDirection;  // Tilt around averaged flow direction of loop nodes
        public float tiltRight;      // Tilt around averaged right direction of loop nodes
        public int resolution;
        public float positionInSegment;
        public float3 averageFlowDirection; // Averaged direction of loop nodes for tilt reference
        public float3 averageRightDirection; // Averaged right direction of loop nodes for right tilt reference
    }
    
    
    public struct SegmentCoordinateSystemData
    {
        public float3 segmentCenter;
        public float3 segmentDirection;
        public float3 segmentUp;
        public float3 segmentRight;
        public float segmentSpan;
        public float3 loopDirection;
        public float3 loopUp;
        public float3 spiralAxis;
    }
    
    [BurstCompile]
    public struct AlphaLoopGenerationJob : IJobParallelFor
    {
        // Input data (read-only)
        [ReadOnly] public NativeArray<SplineNodeData> originalNodes;
        [ReadOnly] public AlphaLoopParameters parameters;
        [ReadOnly] public SegmentCoordinateSystemData coordinateSystem;
        [ReadOnly] public NativeArray<int> nodeIndices; // Which nodes to modify
        [ReadOnly] public int loopStartIndex;
        [ReadOnly] public int loopNodeCount;
        
        // Output data - results array that matches job execution indices
        [WriteOnly] public NativeArray<SplineNodeData> loopResults;
        
        public void Execute(int index)
        {
            if (index >= loopNodeCount) return;
            
            int nodeIndex = nodeIndices[loopStartIndex + index];
            float t = (float)index / (loopNodeCount - 1);
            
            // Generate loop - circular motion in segment's coordinate system
            float angle = t * (2f * math.PI - parameters.openingAngle * math.PI / 180f);
            float baseX = math.cos(angle);
            float baseY = math.sin(angle);
            
            // Spiral progression along segment direction
            float spiralProgress = math.lerp(parameters.spiralStart, parameters.spiralEnd, t);
            float variation = math.sin(t * math.PI * 2f) * parameters.spiralVariation;
            spiralProgress += variation;
            
            // Apply tilt (rotation around spiral axis)
            float tiltRad = parameters.tiltAngle * math.PI / 180f;
            float tiltedX = baseX * math.cos(tiltRad) - baseY * math.sin(tiltRad);
            float tiltedY = baseX * math.sin(tiltRad) + baseY * math.cos(tiltRad);
            
            // Calculate loop scale
            float loopScale = coordinateSystem.segmentSpan * parameters.radius;
            
            // Create position: circular motion in segment's flow-up plane, spiral across segment width
            float3 circularComponent = (tiltedX * coordinateSystem.loopDirection + tiltedY * coordinateSystem.loopUp) * loopScale;
            float3 spiralComponent = spiralProgress * coordinateSystem.spiralAxis * (coordinateSystem.segmentSpan * 0.3f);
            
            float3 worldPosition = coordinateSystem.segmentCenter + circularComponent + spiralComponent;
            
            // Calculate direction - tangent to the curve
            float angleDerivative = (2f * math.PI - parameters.openingAngle * math.PI / 180f) / (loopNodeCount - 1);
            float tangentX = -math.sin(angle) * angleDerivative;
            float tangentY = math.cos(angle) * angleDerivative;
            float spiralProgressDerivative = (parameters.spiralEnd - parameters.spiralStart) / (loopNodeCount - 1);
            float variationDerivative = math.cos(t * math.PI * 2f) * math.PI * 2f * parameters.spiralVariation / (loopNodeCount - 1);
            float spiralDerivative = spiralProgressDerivative + variationDerivative;
            
            // Apply tilt to circular tangent components
            float tiltedTangentX = tangentX * math.cos(tiltRad) - tangentY * math.sin(tiltRad);
            float tiltedTangentY = tangentX * math.sin(tiltRad) + tangentY * math.cos(tiltRad);
            
            // Calculate tangent: circular motion in segment's flow-up plane, spiral across segment width
            float3 circularTangent = (tiltedTangentX * coordinateSystem.loopDirection + tiltedTangentY * coordinateSystem.loopUp) * loopScale;
            float3 spiralTangent = spiralDerivative * coordinateSystem.spiralAxis * (coordinateSystem.segmentSpan * 0.3f);
            float3 localTangent = circularTangent + spiralTangent;
            
            // Combine radial and tangential components for smooth flow
            float3 radialDirection = math.normalize(circularComponent);
            float3 tangentDirection = math.normalize(localTangent);
            float3 localDirection = math.normalize(radialDirection + tangentDirection * 0.3f);
            
            // Scale direction appropriately
            float3 worldDirection = localDirection * (math.length(circularComponent) + math.length(spiralComponent));
            
            // Update the modified node data
            SplineNodeData modifiedNode = originalNodes[nodeIndex];
            modifiedNode.position = worldPosition;
            modifiedNode.direction = worldDirection;
            modifiedNode.up = coordinateSystem.segmentUp;
            
            // Store result at job execution index, not node index
            loopResults[index] = modifiedNode;
        }
    }
    
    [BurstCompile]
    public struct ReversedAlphaLoopGenerationJob : IJobParallelFor
    {
        // Input data (read-only)
        [ReadOnly] public NativeArray<SplineNodeData> originalNodes;
        [ReadOnly] public ReversedAlphaLoopParameters parameters;
        [ReadOnly] public SegmentCoordinateSystemData coordinateSystem;
        [ReadOnly] public NativeArray<int> nodeIndices; // Which nodes to modify
        [ReadOnly] public int loopStartIndex;
        [ReadOnly] public int loopNodeCount;
        
        // Output data - results array that matches job execution indices
        [WriteOnly] public NativeArray<SplineNodeData> loopResults;
        
        public void Execute(int index)
        {
            if (index >= loopNodeCount) return;
            
            int nodeIndex = nodeIndices[loopStartIndex + index];
            float t = (float)index / (loopNodeCount - 1);
            
            // Generate loop - circular motion in segment's coordinate system
            float angle = t * (2f * math.PI - parameters.openingAngle * math.PI / 180f);
            float baseX = math.cos(angle);
            float baseY = math.sin(angle);
            
            // Spiral progression along segment direction
            float spiralProgress = math.lerp(parameters.spiralStart, parameters.spiralEnd, t);
            float variation = math.sin(t * math.PI * 2f) * parameters.spiralVariation;
            spiralProgress += variation;
            
            // Apply tilt (rotation around spiral axis)
            float tiltRad = parameters.tiltAngle * math.PI / 180f;
            float tiltedX = baseX * math.cos(tiltRad) - baseY * math.sin(tiltRad);
            float tiltedY = baseX * math.sin(tiltRad) + baseY * math.cos(tiltRad);
            
            // Calculate loop scale
            float loopScale = coordinateSystem.segmentSpan * parameters.radius;
            
            // Create position: circular motion in segment's flow-up plane, spiral across segment width
            float3 circularComponent = (tiltedX * coordinateSystem.loopDirection + tiltedY * coordinateSystem.loopUp) * loopScale;
            float3 spiralComponent = spiralProgress * coordinateSystem.spiralAxis * (coordinateSystem.segmentSpan * 0.3f);
            
            float3 worldPosition = coordinateSystem.segmentCenter + circularComponent + spiralComponent;
            
            // Calculate direction - tangent to the curve
            float angleDerivative = (2f * math.PI - parameters.openingAngle * math.PI / 180f) / (loopNodeCount - 1);
            float tangentX = -math.sin(angle) * angleDerivative;
            float tangentY = math.cos(angle) * angleDerivative;
            float spiralProgressDerivative = (parameters.spiralEnd - parameters.spiralStart) / (loopNodeCount - 1);
            float variationDerivative = math.cos(t * math.PI * 2f) * math.PI * 2f * parameters.spiralVariation / (loopNodeCount - 1);
            float spiralDerivative = spiralProgressDerivative + variationDerivative;
            
            // Apply tilt to circular tangent components
            float tiltedTangentX = tangentX * math.cos(tiltRad) - tangentY * math.sin(tiltRad);
            float tiltedTangentY = tangentX * math.sin(tiltRad) + tangentY * math.cos(tiltRad);
            
            // Calculate tangent: circular motion in segment's flow-up plane, spiral across segment width
            float3 circularTangent = (tiltedTangentX * coordinateSystem.loopDirection + tiltedTangentY * coordinateSystem.loopUp) * loopScale;
            float3 spiralTangent = spiralDerivative * coordinateSystem.spiralAxis * (coordinateSystem.segmentSpan * 0.3f);
            float3 localTangent = circularTangent + spiralTangent;
            
            // Combine radial and tangential components for smooth flow
            float3 radialDirection = math.normalize(circularComponent);
            float3 tangentDirection = math.normalize(localTangent);
            float3 localDirection = math.normalize(radialDirection + tangentDirection * 0.3f);
            
            // Scale direction appropriately
            float3 worldDirection = localDirection * (math.length(circularComponent) + math.length(spiralComponent));
            
            // Update the modified node data
            SplineNodeData modifiedNode = originalNodes[nodeIndex];
            modifiedNode.position = worldPosition;
            modifiedNode.direction = worldDirection;
            modifiedNode.up = coordinateSystem.segmentUp;
            
            // Store result at job execution index, not node index
            loopResults[index] = modifiedNode;
        }
    }
    
    [BurstCompile]
    public struct NLoopGenerationJob : IJobParallelFor
    {
        // Input data (read-only)
        [ReadOnly] public NativeArray<SplineNodeData> originalNodes;
        [ReadOnly] public NLoopParameters parameters;
        [ReadOnly] public SegmentCoordinateSystemData coordinateSystem;
        [ReadOnly] public NativeArray<int> nodeIndices; // Which nodes to modify
        [ReadOnly] public int loopStartIndex;
        [ReadOnly] public int loopNodeCount;
        
        // Output data - results array that matches job execution indices
        [WriteOnly] public NativeArray<SplineNodeData> loopResults;
        
        public void Execute(int index)
        {
            if (index >= loopNodeCount) return;
            
            int nodeIndex = nodeIndices[loopStartIndex + index];
            
            // Calculate parameter t based on arc-length distribution, not linear
            float t = CalculateArcLengthBasedParameter(index, loopNodeCount);
            
            // Generate N-shape in the segment's coordinate system
            // N-shape progression: up leg -> diagonal down -> up leg
            float3 nPosition = CalculateNShapeInCoordinateSystem(t);
            
            // Apply leg width adjustments ONLY to first and last nodes
            if (index == 0)
            {
                // First node (left leg start): apply left leg width offset
                nPosition.x += -parameters.leftLegWidth; // Move backward in flow direction to increase left leg width
            }
            else if (index == loopNodeCount - 1)
            {
                // Last node (right leg end): apply right leg width offset  
                nPosition.x += parameters.rightLegWidth; // Move forward in flow direction to increase right leg width
            }
            // All other nodes (including diagonal) remain unchanged
            
            // Scale by segment span for appropriate size
            float loopScale = coordinateSystem.segmentSpan * 0.4f; // Scale factor for loop size
            
            // Transform position using the coordinate system (like Alpha loop)
            // loopRight = flow direction (N progression along segment)
            // loopUp = vertical direction (for legs going up/down)  
            // spiralAxis = width direction (N width across segment)
            float3 worldPosition = coordinateSystem.segmentCenter + 
                                 (nPosition.x * coordinateSystem.loopDirection + 
                                  nPosition.y * coordinateSystem.loopUp + 
                                  nPosition.z * coordinateSystem.spiralAxis) * loopScale;
            
            // Calculate direction - tangent to the N curve in coordinate system
            float3 nTangent = CalculateNShapeTangent(t);
            
            // Transform tangent using coordinate system and scale appropriately
            float3 worldTangent = (nTangent.x * coordinateSystem.loopDirection + 
                                 nTangent.y * coordinateSystem.loopUp + 
                                 nTangent.z * coordinateSystem.spiralAxis) * loopScale;
            
            // Ensure minimum tangent magnitude and normalize
            if (math.length(worldTangent) < 0.01f)
            {
                worldTangent = coordinateSystem.loopDirection * 0.1f; // Default flow direction
            }
            
            float3 worldDirection = math.normalize(worldTangent) * (coordinateSystem.segmentSpan * 0.2f);
            
            // Update the modified node data
            SplineNodeData modifiedNode = originalNodes[nodeIndex];
            modifiedNode.position = worldPosition;
            modifiedNode.direction = worldDirection;
            modifiedNode.up = coordinateSystem.loopUp; // Use loop up vector for consistency
            
            // Store result at job execution index, not node index
            loopResults[index] = modifiedNode;
        }
        
        private float3 CalculateNShapeInCoordinateSystem(float t)
        {
            // N-shape in local coordinate system:
            // X-axis (loopDirection): progression along the N (0 to 1)
            // Y-axis (loopUp): vertical displacement (up/down legs)
            // Z-axis (spiralAxis): width of the N (perpendicular to flow, should stay at 0 for basic N)
            
            float3 position = float3.zero;
            
            if (t <= parameters.lowerBendPosition)
            {
                // Left leg: going UP from main spline
                float localT = t / parameters.lowerBendPosition;
                float bendT = math.smoothstep(0f, 1f, localT * (2f - parameters.bendSharpness)); // Smoother bending
                
                // Apply asymmetry to left leg - slightly offset X position and modify height
                float leftAsymmetry = parameters.asymmetryFactor * -0.3f; // Negative for left side
                
                position.x = localT * parameters.lowerBendPosition + leftAsymmetry * localT; // Asymmetric progression
                position.y = math.lerp(0f, parameters.loopHeight * parameters.leftLegHeight, bendT); // Go UP
                position.z = leftAsymmetry * 0.5f * localT; // Slight perpendicular offset for asymmetry
            }
            else if (t <= parameters.upperBendPosition)
            {
                // Diagonal section: going DOWN diagonally from left leg to right leg
                float localT = (t - parameters.lowerBendPosition) / (parameters.upperBendPosition - parameters.lowerBendPosition);
                float progressT = parameters.lowerBendPosition + localT * (parameters.upperBendPosition - parameters.lowerBendPosition);
                
                // Apply diagonal tilts in both axes separately with opposing directions at ends
                // Create a tilt that goes from -effect at start to +effect at end
                float directionTiltFactor = math.sin(parameters.diagonalTiltDirection * math.PI / 180f);
                float rightTiltFactor = math.sin(parameters.diagonalTiltRight * math.PI / 180f);
                
                // Use (localT - 0.5f) to create opposing movements: negative at start, positive at end
                float tiltMultiplier = (localT - 0.5f) * 2f; // Range from -1 to +1 across diagonal
                
                // Apply asymmetry to diagonal - creates curved diagonal instead of straight
                float diagonalAsymmetry = parameters.asymmetryFactor * math.sin(localT * math.PI); // Sine curve for smooth asymmetry
                
                position.x = progressT + directionTiltFactor * 0.5f * tiltMultiplier + diagonalAsymmetry * 0.2f; // Asymmetric diagonal
                position.y = math.lerp(parameters.loopHeight * parameters.leftLegHeight, 
                                     -parameters.loopHeight * parameters.rightLegHeight, localT); // Go DOWN diagonally
                position.z = rightTiltFactor * 0.5f * tiltMultiplier + diagonalAsymmetry * 0.3f; // Asymmetric perpendicular displacement
            }
            else
            {
                // Right leg: going UP from diagonal end back to main spline
                float localT = (t - parameters.upperBendPosition) / (1f - parameters.upperBendPosition);
                float bendT = math.smoothstep(0f, 1f, localT * (2f - parameters.bendSharpness)); // Smoother bending
                float progressT = parameters.upperBendPosition + localT * (1f - parameters.upperBendPosition);
                
                // Apply diagonal tilt to the connection point (start of right leg)
                // This ensures the diagonal end point moves when tilt changes
                float directionTiltFactor = math.sin(parameters.diagonalTiltDirection * math.PI / 180f);
                float rightTiltFactor = math.sin(parameters.diagonalTiltRight * math.PI / 180f);
                
                // Gradually reduce tilt effect from full at connection point to none at spline
                // Use positive tilt (matching end of diagonal) that blends to zero
                float tiltBlend = 1f - bendT; // Full tilt at start, no tilt at end
                float endTiltMultiplier = 1f; // Positive tilt matching diagonal end
                
                // Apply asymmetry to right leg - opposite to left leg
                float rightAsymmetry = parameters.asymmetryFactor * 0.3f; // Positive for right side
                
                position.x = progressT + directionTiltFactor * 0.5f * endTiltMultiplier * tiltBlend + rightAsymmetry * localT; // Asymmetric progression
                position.y = math.lerp(-parameters.loopHeight * parameters.rightLegHeight, 0f, bendT); // Go UP to main spline
                position.z = rightTiltFactor * 0.5f * endTiltMultiplier * tiltBlend + rightAsymmetry * 0.5f * localT; // Asymmetric perpendicular offset
            }
            
            return position;
        }
        
        private float3 CalculateNShapeTangent(float t)
        {
            // Calculate tangent using finite differences for smooth direction
            float deltaT = 1f / (loopNodeCount - 1); // Use actual node spacing
            
            float3 pos1 = CalculateNShapeInCoordinateSystem(math.max(0f, t - deltaT * 0.5f));
            float3 pos2 = CalculateNShapeInCoordinateSystem(math.min(1f, t + deltaT * 0.5f));
            
            float3 tangent = (pos2 - pos1) / deltaT;
            
            // Ensure reasonable tangent magnitude
            if (math.length(tangent) < 0.1f)
            {
                tangent = new float3(1f, 0f, 0f); // Default to flow direction
            }
            
            return tangent;
        }
        
        private float CalculateArcLengthBasedParameter(int nodeIndex, int totalNodes)
        {
            // Calculate parameter based on arc length distribution for N-shape
            // The N-shape has three main sections with different geometric lengths:
            // 1. Left leg (vertical up)
            // 2. Diagonal (down and across) 
            // 3. Right leg (vertical up)
            // Note: Leg widths only affect the first and last nodes, not the shape calculation
            
            if (totalNodes <= 1) return 0f;
            
            // Pre-calculate the approximate arc lengths of each section
            // Base N-shape without leg width modifications
            float leftLegLength = math.sqrt(
                math.pow(parameters.lowerBendPosition, 2) +  // X progression
                math.pow(parameters.loopHeight * parameters.leftLegHeight, 2) // Y displacement
            );
            
            float diagonalLength = math.sqrt(
                math.pow(parameters.upperBendPosition - parameters.lowerBendPosition, 2) + // X progression
                math.pow(parameters.loopHeight * (parameters.leftLegHeight + parameters.rightLegHeight), 2) // Y displacement
            );
            
            float rightLegLength = math.sqrt(
                math.pow(1f - parameters.upperBendPosition, 2) + // X progression
                math.pow(parameters.loopHeight * parameters.rightLegHeight, 2) // Y displacement
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
                return sectionProgress * parameters.lowerBendPosition;
            }
            else if (targetArcLength <= leftLegLength + diagonalLength)
            {
                // In the diagonal section
                float sectionProgress = (targetArcLength - leftLegLength) / diagonalLength;
                return parameters.lowerBendPosition + sectionProgress * (parameters.upperBendPosition - parameters.lowerBendPosition);
            }
            else
            {
                // In the right leg section
                float sectionProgress = (targetArcLength - leftLegLength - diagonalLength) / rightLegLength;
                return parameters.upperBendPosition + sectionProgress * (1f - parameters.upperBendPosition);
            }
        }
    }
    
    [BurstCompile]
    public struct TransverseLoopGenerationJob : IJobParallelFor
    {
        // Input data (read-only)
        [ReadOnly] public NativeArray<SplineNodeData> originalNodes;
        [ReadOnly] public TransverseLoopParameters parameters;
        [ReadOnly] public SegmentCoordinateSystemData coordinateSystem;
        [ReadOnly] public NativeArray<int> nodeIndices; // Which nodes to modify
        [ReadOnly] public int loopStartIndex;
        [ReadOnly] public int loopNodeCount;
        
        // Output data - results array that matches job execution indices
        [WriteOnly] public NativeArray<SplineNodeData> loopResults;
        
        public void Execute(int index)
        {
            if (index >= loopNodeCount) return;
            
            int nodeIndex = nodeIndices[loopStartIndex + index];
            float t = (float)index / (loopNodeCount - 1);
            
            // Calculate U-shape position in coordinate system
            float3 position = CalculateUShapePosition(t);
            
            // Get original node for reference
            SplineNodeData originalNode = originalNodes[nodeIndex];
            
            // Transform U-shape offset to world space and ADD to original position
            // This way nodes are only displaced from their original positions to form the U
            float3 uShapeOffset = position.x * coordinateSystem.loopDirection +
                                 position.y * coordinateSystem.loopUp +
                                 position.z * coordinateSystem.spiralAxis;
            
            float3 worldPosition = originalNode.position + uShapeOffset;
            
            // Calculate tangent direction for U-curve
            float3 tangent = CalculateUShapeTangent(t);
            float3 worldTangent = math.normalize(
                tangent.x * coordinateSystem.loopDirection +
                tangent.y * coordinateSystem.loopUp +
                tangent.z * coordinateSystem.spiralAxis);
            
            // Create modified node
            SplineNodeData result = new SplineNodeData
            {
                position = worldPosition,
                direction = worldTangent,
                up = coordinateSystem.loopUp, // Keep segment's up direction
                scale = originalNode.scale
            };
            
            loopResults[index] = result;
        }
        
        private float3 CalculateUShapePosition(float t)
        {
            // CORRECTED U-shape calculation:
            // - Primary movement: DOWN in segmentDirection axis (≈ Y world) to create U sag
            // - Width: controls how much the U spreads in segmentRight axis (≈ X world) - INVERTED
            // - Lateral offset: displacement in segmentUp axis (≈ Z world)
            
            // X: Minimal horizontal spread for U width (segmentRight axis)
            // This creates the U aperture but should be subtle
            float tCentered = (t - 0.5f) * 2f; // Convert to -1 to +1 range, center at 0
            float xPos = tCentered * (1.0f - parameters.width) * 0.1f; // INVERTED: higher width = narrower U
            
            // Y: Primary U-shape motion - vertical sag (segmentDirection axis, DOWN)
            // This is the main U-shape movement - nodes go down and back up
            float ySag = -parameters.depth * (1f - tCentered * tCentered); // Parabola: 0 at ends, -depth at center
            
            // Apply asymmetry (left vs right height difference)
            if (math.abs(parameters.asymmetry) > 0.01f)
            {
                float asymmetryOffset = parameters.asymmetry * tCentered * parameters.depth * 0.3f;
                ySag += asymmetryOffset;
            }
            
            // Apply curvature smoothing if needed
            if (parameters.curvatureSmoothing > 0.01f)
            {
                float smoothFactor = math.pow(1f - math.abs(tCentered), 2f - parameters.curvatureSmoothing);
                ySag = math.lerp(ySag, ySag * smoothFactor, parameters.curvatureSmoothing);
            }
            
            // Z: Lateral offset (perpendicular displacement in segmentUp axis)
            float zPos = parameters.lateralOffset;
            
            // Scale by segment span
            float loopScale = coordinateSystem.segmentSpan * 0.4f;
            
            // Create base position in coordinate system space
            float3 basePosition = new float3(
                xPos * loopScale,       // X: minimal U width spread (segmentRight)
                ySag * loopScale,       // Y: PRIMARY U sag DOWN (segmentDirection)
                zPos * loopScale        // Z: lateral offset (segmentUp)
            );
            
            // Apply sag position - shifts only CENTER nodes along the U-curve's flow direction
            // Use a bell curve to affect only center nodes, not border nodes
            float centerWeight = math.exp(-8f * tCentered * tCentered); // Strong falloff from center
            float sagDisplacement = (parameters.sagPosition - 0.5f) * 2f; // Convert 0-1 to -1 to +1
            
            // Calculate the tangent direction at this point to get flow direction
            float epsilon = 0.001f;
            float tNext = math.min(1f, t + epsilon);
            float tPrev = math.max(0f, t - epsilon);
            
            // Calculate base positions for tangent
            float tNextCentered = (tNext - 0.5f) * 2f;
            float tPrevCentered = (tPrev - 0.5f) * 2f;
            
            float3 nextBasePos = new float3(
                tNextCentered * (1.0f - parameters.width) * 0.1f * loopScale,
                -parameters.depth * (1f - tNextCentered * tNextCentered) * loopScale,
                zPos * loopScale
            );
            
            float3 prevBasePos = new float3(
                tPrevCentered * (1.0f - parameters.width) * 0.1f * loopScale,
                -parameters.depth * (1f - tPrevCentered * tPrevCentered) * loopScale,
                zPos * loopScale
            );
            
            // Get flow direction (tangent) of the U-curve at this point
            float3 flowDirection = math.normalize(nextBasePos - prevBasePos);
            
            // Apply sag displacement along the flow direction, only for center nodes
            basePosition += sagDisplacement * centerWeight * flowDirection * 0.3f * loopScale;
            
            // Apply dual-axis tilts if specified
            if (math.abs(parameters.tiltDirection) > 0.01f || math.abs(parameters.tiltRight) > 0.01f)
            {
                float tiltDirRad = parameters.tiltDirection * math.PI / 180f;
                float tiltRightRad = parameters.tiltRight * math.PI / 180f;
                
                // For tiltDirection: Rotate around the averaged flow direction of loop nodes
                if (math.abs(parameters.tiltDirection) > 0.01f)
                {
                    // Use the averaged flow direction as rotation axis
                    float3 rotationAxis = math.normalize(parameters.averageFlowDirection);
                    
                    // Calculate perpendicular vectors to create rotation matrix
                    float3 perpVector1 = math.cross(rotationAxis, new float3(0, 1, 0));
                    if (math.length(perpVector1) < 0.1f)
                        perpVector1 = math.cross(rotationAxis, new float3(1, 0, 0));
                    perpVector1 = math.normalize(perpVector1);
                    
                    float3 perpVector2 = math.normalize(math.cross(rotationAxis, perpVector1));
                    
                    // Apply rotation around averaged flow direction axis
                    float3 alongAxisComponent = math.dot(basePosition, rotationAxis) * rotationAxis;
                    float3 inPlaneComponent = basePosition - alongAxisComponent;
                    
                    // Project in-plane component onto perpendicular vectors
                    float comp1 = math.dot(inPlaneComponent, perpVector1);
                    float comp2 = math.dot(inPlaneComponent, perpVector2);
                    
                    // Rotate the in-plane component
                    float cosTheta = math.cos(tiltDirRad);
                    float sinTheta = math.sin(tiltDirRad);
                    
                    float3 rotatedInPlane = (comp1 * cosTheta - comp2 * sinTheta) * perpVector1 +
                                          (comp1 * sinTheta + comp2 * cosTheta) * perpVector2;
                    
                    basePosition = rotatedInPlane + alongAxisComponent;
                }
                
                // For tiltRight: Apply rotation around the averaged right direction of loop nodes
                if (math.abs(parameters.tiltRight) > 0.01f)
                {
                    // Use the averaged right direction as rotation axis
                    float3 rightRotationAxis = math.normalize(parameters.averageRightDirection);
                    
                    // Calculate perpendicular vectors to create rotation matrix
                    float3 rightPerpVector1 = math.cross(rightRotationAxis, new float3(0, 1, 0));
                    if (math.length(rightPerpVector1) < 0.1f)
                        rightPerpVector1 = math.cross(rightRotationAxis, new float3(1, 0, 0));
                    rightPerpVector1 = math.normalize(rightPerpVector1);
                    
                    float3 rightPerpVector2 = math.normalize(math.cross(rightRotationAxis, rightPerpVector1));
                    
                    // Apply rotation around averaged right direction axis
                    float3 rightAlongAxisComponent = math.dot(basePosition, rightRotationAxis) * rightRotationAxis;
                    float3 rightInPlaneComponent = basePosition - rightAlongAxisComponent;
                    
                    // Project in-plane component onto perpendicular vectors
                    float rightComp1 = math.dot(rightInPlaneComponent, rightPerpVector1);
                    float rightComp2 = math.dot(rightInPlaneComponent, rightPerpVector2);
                    
                    // Rotate the in-plane component
                    float rightCosTheta = math.cos(tiltRightRad);
                    float rightSinTheta = math.sin(tiltRightRad);
                    
                    float3 rightRotatedInPlane = (rightComp1 * rightCosTheta - rightComp2 * rightSinTheta) * rightPerpVector1 +
                                               (rightComp1 * rightSinTheta + rightComp2 * rightCosTheta) * rightPerpVector2;
                    
                    basePosition = rightRotatedInPlane + rightAlongAxisComponent;
                }
            }
            
            return basePosition;
        }
        
        private float3 CalculateUShapeTangent(float t)
        {
            // Calculate tangent direction for smooth curve flow
            float epsilon = 0.001f;
            float tNext = math.min(1f, t + epsilon);
            float tPrev = math.max(0f, t - epsilon);
            
            float3 posNext = CalculateUShapePosition(tNext);
            float3 posPrev = CalculateUShapePosition(tPrev);
            
            return math.normalize(posNext - posPrev);
        }
    }    [BurstCompile]
    public struct CoordinateSystemCalculationJob : IJob
    {
        [ReadOnly] public NativeArray<SplineNodeData> splineNodes;
        [ReadOnly] public int startNodeIndex;
        [ReadOnly] public int endNodeIndex;
        [ReadOnly] public float positionInSegment;
        [ReadOnly] public float rotationAroundFlow;
        
        public SegmentCoordinateSystemData result;
        
        public void Execute()
        {
            // Calculate segment bounds and position based on positionInSegment
            float3 segmentStartPos = splineNodes[startNodeIndex].position;
            float3 segmentEndPos = splineNodes[endNodeIndex].position;
            float3 segmentCenter = math.lerp(segmentStartPos, segmentEndPos, positionInSegment);
            
            // Calculate average flow direction from node directions in the segment
            float3 averageDirection = float3.zero;
            for (int i = startNodeIndex; i <= endNodeIndex; i++)
            {
                averageDirection += math.normalize(splineNodes[i].direction);
            }
            averageDirection = math.normalize(averageDirection);
            
            // Calculate segment's local coordinate system
            float3 segmentDirection = averageDirection; // Primary flow direction
            float3 segmentUp = new float3(0, 1, 0); // Start with world up
            float3 segmentRight = math.normalize(math.cross(segmentDirection, segmentUp));
            segmentUp = math.normalize(math.cross(segmentRight, segmentDirection)); // Recompute for orthogonality
            
            // Calculate segment span
            float segmentSpan = 0f;
            for (int i = startNodeIndex; i < endNodeIndex; i++)
            {
                segmentSpan += math.distance(splineNodes[i].position, splineNodes[i + 1].position);
            }
            
            // Loop axes based on segment's anatomy
            float3 loopRight = segmentDirection;  // X-axis for circular motion (along segment flow)
            float3 loopUp = segmentUp;           // Y-axis for circular motion (upward - vertical component)
            float3 spiralAxis = segmentRight;    // Z-axis for spiral progression (across segment width)
            
            // Apply rotation around the segment flow direction
            if (math.abs(rotationAroundFlow) > 0.01f)
            {
                float rotationRad = rotationAroundFlow * math.PI / 180f;
                
                float3 originalUp = loopUp;
                float3 originalSpiral = spiralAxis;
                
                // Apply rotation around segmentDirection (loopRight)
                loopUp = originalUp * math.cos(rotationRad) + originalSpiral * math.sin(rotationRad);
                spiralAxis = -originalUp * math.sin(rotationRad) + originalSpiral * math.cos(rotationRad);
            }
            
            result = new SegmentCoordinateSystemData
            {
                segmentCenter = segmentCenter,
                segmentDirection = segmentDirection,
                segmentUp = segmentUp,
                segmentRight = segmentRight,
                segmentSpan = segmentSpan,
                loopDirection = loopRight,
                loopUp = loopUp,
                spiralAxis = spiralAxis
            };
        }
    }
    
    public struct SmoothingParameters
    {
        public float blendStrength;
        public int loopStartIndex;
        public int loopEndIndex;
        public int splineNodeCount;
    }
    
    [BurstCompile]
    public struct SegmentTransitionSmoothingJob : IJob
    {
        [ReadOnly] public NativeArray<SplineNodeData> originalNodes;
        [ReadOnly] public SmoothingParameters parameters;
        
        public NativeArray<SplineNodeData> modifiedNodes;
        
        public void Execute()
        {
            // Copy original data first
            for (int i = 0; i < originalNodes.Length; i++)
            {
                modifiedNodes[i] = originalNodes[i];
            }
            
            // Phase 1: Smooth nodes before loop start
            if (parameters.loopStartIndex > 0)
            {
                SmoothLoopStartTransition();
            }
            
            // Phase 2: Smooth nodes after loop end
            if (parameters.loopEndIndex < parameters.splineNodeCount - 1)
            {
                SmoothLoopEndTransition();
            }
        }
        
        private void SmoothLoopStartTransition()
        {
            int nodeIndex = parameters.loopStartIndex - 1;
            
            if (nodeIndex > 0)
            {
                float3 prevPos = originalNodes[nodeIndex - 1].position;
                float3 loopStartPos = originalNodes[parameters.loopStartIndex].position;
                
                float3 direction = math.normalize(loopStartPos - prevPos);
                float3 blendedPos = math.lerp(prevPos, loopStartPos, parameters.blendStrength * 0.7f);
                
                SplineNodeData modifiedNode = modifiedNodes[nodeIndex];
                modifiedNode.position = blendedPos;
                
                float transitionMagnitude = math.distance(blendedPos, loopStartPos) * 0.8f;
                modifiedNode.direction = direction * transitionMagnitude;
                modifiedNodes[nodeIndex] = modifiedNode;
                
                // Multi-node smoothing for gradual transition
                if (nodeIndex > 1)
                {
                    int prevNodeIndex = nodeIndex - 1;
                    float3 prevPrevPos = originalNodes[prevNodeIndex].position;
                    float3 smoothedPos = math.lerp(prevPrevPos, blendedPos, parameters.blendStrength * 0.4f);
                    
                    SplineNodeData prevModifiedNode = modifiedNodes[prevNodeIndex];
                    prevModifiedNode.position = smoothedPos;
                    
                    float3 smoothDir = math.normalize(blendedPos - smoothedPos);
                    float smoothMagnitude = math.distance(smoothedPos, blendedPos) * 0.6f;
                    prevModifiedNode.direction = smoothDir * smoothMagnitude;
                    modifiedNodes[prevNodeIndex] = prevModifiedNode;
                }
            }
        }
        
        private void SmoothLoopEndTransition()
        {
            int nodeIndex = parameters.loopEndIndex + 1;
            
            if (nodeIndex < parameters.splineNodeCount)
            {
                float3 loopEndPos = originalNodes[parameters.loopEndIndex].position;
                float3 nextPos = originalNodes[nodeIndex].position;
                
                float3 direction = math.normalize(nextPos - loopEndPos);
                float3 blendedPos = math.lerp(nextPos, loopEndPos, parameters.blendStrength * 0.7f);
                
                SplineNodeData modifiedNode = modifiedNodes[nodeIndex];
                modifiedNode.position = blendedPos;
                modifiedNodes[nodeIndex] = modifiedNode;
                
                // Update loop end node's direction
                SplineNodeData loopEndNode = modifiedNodes[parameters.loopEndIndex];
                float transitionMagnitude = math.distance(loopEndPos, blendedPos) * 0.8f;
                loopEndNode.direction = direction * transitionMagnitude;
                modifiedNodes[parameters.loopEndIndex] = loopEndNode;
                
                // Multi-node smoothing for gradual transition
                if (nodeIndex < parameters.splineNodeCount - 1)
                {
                    int nextNodeIndex = nodeIndex + 1;
                    float3 nextNextPos = originalNodes[nextNodeIndex].position;
                    float3 smoothedPos = math.lerp(nextNextPos, blendedPos, parameters.blendStrength * 0.4f);
                    
                    SplineNodeData nextModifiedNode = modifiedNodes[nextNodeIndex];
                    nextModifiedNode.position = smoothedPos;
                    
                    float3 smoothDir = math.normalize(smoothedPos - blendedPos);
                    float smoothMagnitude = math.distance(blendedPos, smoothedPos) * 0.6f;
                    modifiedNode.direction = smoothDir * smoothMagnitude;
                    modifiedNodes[nodeIndex] = modifiedNode; // Update the direction for the current node
                    modifiedNodes[nextNodeIndex] = nextModifiedNode;
                }
            }
        }
    }
}

