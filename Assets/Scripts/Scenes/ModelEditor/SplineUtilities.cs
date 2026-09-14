using SplineMesh;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LargeIntestine
{
    /// <summary>
    /// Static utility class for spline management and manipulation operations.
    /// </summary>
    public static class SplineUtilities
    {
        #region Node Replacement Methods

        /// <summary>
        /// Replaces all nodes in a spline with a new set of nodes.
        /// </summary>
        /// <param name="spline">The spline to modify.</param>
        /// <param name="newSplineNodes">The new nodes to add.</param>
        /// <param name="splineSmoother">Optional spline smoother to disable during operation.</param>
        public static void ReplaceSplineNodes(Spline spline, List<SplineNode> newSplineNodes, SplineSmoother splineSmoother = null)
        {
            if (splineSmoother != null)
                splineSmoother.enabled = false;

            spline.nodes.Clear();
            spline.curves.Clear();

            foreach (var sourceSplineNode in newSplineNodes)
            {
                spline.AddNode(sourceSplineNode);
            }

            spline.RefreshCurves();

            if (splineSmoother != null)
                splineSmoother.enabled = true;
        }

        /// <summary>
        /// Replaces data of existing spline nodes with data from preset nodes.
        /// </summary>
        /// <param name="spline">The spline to modify.</param>
        /// <param name="presetSplineNodes">The preset nodes containing the new data.</param>
        public static void ReplaceSplineNodesData(Spline spline, List<SplineNode> presetSplineNodes)
        {
            for (int i = 0; i < presetSplineNodes.Count && i < spline.nodes.Count; i++)
            {
                spline.nodes[i].ReplaceData(presetSplineNodes[i]);
            }
        }

        #endregion

        #region Transform Methods

        /// <summary>
        /// Transforms all spline nodes using a given transform, applying position, rotation, and optional scale.
        /// </summary>
        /// <param name="spline">The spline to transform.</param>
        /// <param name="holderTransform">The transform to apply.</param>
        /// <param name="scaleFactor">The scale factor to apply.</param>
        /// <param name="useScale">Whether to apply scaling.</param>
        /// <param name="splineSmoother">Optional spline smoother to disable during operation.</param>
        public static void TransformSpline(Spline spline, Transform holderTransform, Vector3 scaleFactor, bool useScale = true, SplineSmoother splineSmoother = null)
        {
            SplineNode[] transformedNodes = new SplineNode[spline.nodes.Count];

            for (int i = 0; i < spline.nodes.Count; i++)
            {
                var originalNode = spline.nodes[i];

                // Transform position considering scale, rotation, and position
                Vector3 scaledPosition = useScale ? Vector3.Scale(originalNode.Position, scaleFactor) : originalNode.Position;
                Vector3 transformedPosition = holderTransform.TransformPoint(scaledPosition);

                // Transform direction considering scale and rotation (but not position)
                Vector3 scaledDirection = useScale ? Vector3.Scale(originalNode.Direction, scaleFactor) : originalNode.Direction;
                Vector3 transformedDirection = holderTransform.TransformDirection(scaledDirection);

                // Transform up vector considering rotation only (up vectors shouldn't be scaled)
                Vector3 transformedUp = holderTransform.TransformDirection(originalNode.Up);

                // Create new transformed node
                transformedNodes[i] = new SplineNode(transformedPosition, transformedDirection)
                {
                    Up = transformedUp.normalized, // Normalize to maintain proper orientation
                    Scale = originalNode.Scale // Keep original scale, as it is not affected by transformation
                };
            }

            ReplaceSplineNodes(spline, transformedNodes.ToList(), splineSmoother);
        }

        /// <summary>
        /// Gets the world-space transformed position of a spline node.
        /// </summary>
        /// <param name="spline">The spline containing the node.</param>
        /// <param name="nodeIdx">The index of the node.</param>
        /// <param name="localScale">The local scale to apply.</param>
        /// <param name="localPosition">The local position offset.</param>
        /// <returns>The transformed world position, or Vector3.zero if index is invalid.</returns>
        public static Vector3 GetSplineNodeTransformedPosition(Spline spline, int nodeIdx, Vector3 localScale, Vector3 localPosition)
        {
            if (nodeIdx < 0 || nodeIdx >= spline.nodes.Count)
                return Vector3.zero;

            return spline.transform.TransformPoint(spline.nodes[nodeIdx].Position * localScale.x + localPosition);
        }

        #endregion

        #region Scale Methods

        /// <summary>
        /// Scales all spline nodes by a given factor.
        /// </summary>
        /// <param name="spline">The spline to scale.</param>
        /// <param name="scaleFactor">The scale factor to apply.</param>
        public static void ScaleSpline(Spline spline, float scaleFactor)
        {
            var splineNodes = new List<SplineNode>();

            foreach (var sourceSplineNode in spline.nodes)
            {
                var splineNode = new SplineNode(
                    sourceSplineNode.Position * scaleFactor,
                    sourceSplineNode.Direction * scaleFactor
                )
                {
                    Up = sourceSplineNode.Up,
                    Scale = sourceSplineNode.Scale
                };
                splineNodes.Add(splineNode);
            }

            spline.nodes.Clear();
            foreach (var sn in splineNodes)
            {
                spline.AddNode(sn);
            }

            spline.curves.Clear();
            spline.RefreshCurves();
        }

        /// <summary>
        /// Creates a new list of scaled spline nodes without modifying the original spline.
        /// </summary>
        /// <param name="spline">The source spline.</param>
        /// <param name="scaleFactor">The scale factor to apply.</param>
        /// <returns>A new list of scaled spline nodes.</returns>
        public static List<SplineNode> GetScaledSplineNodes(Spline spline, float scaleFactor)
        {
            var splineNodes = new List<SplineNode>();

            foreach (var sourceSplineNode in spline.nodes)
            {
                var splineNode = new SplineNode(
                    sourceSplineNode.Position * scaleFactor,
                    sourceSplineNode.Direction * scaleFactor
                )
                {
                    Up = sourceSplineNode.Up,
                    Scale = sourceSplineNode.Scale
                };
                splineNodes.Add(splineNode);
            }

            return splineNodes;
        }

        #endregion

        #region Rotation Methods

        /// <summary>
        /// Rotates all spline nodes around the Y axis by a given angle.
        /// </summary>
        /// <param name="spline">The spline to rotate.</param>
        /// <param name="rotationAngle">The rotation angle in degrees.</param>
        public static void RotateSpline(Spline spline, float rotationAngle)
        {
            RotateSpline(spline, rotationAngle, Vector3.up);
        }

        /// <summary>
        /// Rotates all spline nodes around a specified axis by a given angle.
        /// </summary>
        /// <param name="spline">The spline to rotate.</param>
        /// <param name="rotationAngle">The rotation angle in degrees.</param>
        /// <param name="axis">The axis to rotate around.</param>
        public static void RotateSpline(Spline spline, float rotationAngle, Vector3 axis)
        {
            var newSplineNodes = new List<SplineNode>();
            var rotation = Quaternion.AngleAxis(rotationAngle, axis);

            foreach (var sourceSplineNode in spline.nodes)
            {
                var newSplineNode = new SplineNode(
                    rotation * sourceSplineNode.Position,
                    rotation * sourceSplineNode.Direction
                )
                {
                    Up = rotation * sourceSplineNode.Up,
                    Scale = sourceSplineNode.Scale
                };
                newSplineNodes.Add(newSplineNode);
            }

            spline.nodes.Clear();
            foreach (var sn in newSplineNodes)
            {
                spline.AddNode(sn);
            }

            spline.curves.Clear();
            spline.RefreshCurves();
        }

        /// <summary>
        /// Creates a new list of rotated spline nodes without modifying the original spline.
        /// </summary>
        /// <param name="spline">The source spline.</param>
        /// <param name="rotationAngle">The rotation angle in degrees.</param>
        /// <param name="axis">The axis to rotate around.</param>
        /// <returns>A new list of rotated spline nodes.</returns>
        public static List<SplineNode> GetRotatedSplineNodes(Spline spline, float rotationAngle, Vector3 axis)
        {
            var newSplineNodes = new List<SplineNode>();
            var rotation = Quaternion.AngleAxis(rotationAngle, axis);

            foreach (var sourceSplineNode in spline.nodes)
            {
                var newSplineNode = new SplineNode(
                    rotation * sourceSplineNode.Position,
                    rotation * sourceSplineNode.Direction
                )
                {
                    Up = rotation * sourceSplineNode.Up,
                    Scale = sourceSplineNode.Scale
                };
                newSplineNodes.Add(newSplineNode);
            }

            return newSplineNodes;
        }

        #endregion

        #region Up Vector Methods

        /// <summary>
        /// Updates the Up vectors of all spline nodes to point toward the centroid of all node positions.
        /// </summary>
        /// <param name="spline">The spline to modify.</param>
        public static void UpdateUpVectorsToCentroid(Spline spline)
        {
            var centroid = GetSplineNodesCentroid(spline);

            foreach (var node in spline.nodes)
            {
                node.Up = centroid - node.Position;
            }
        }

        /// <summary>
        /// Updates the Up vectors of all spline nodes using the first node up vector as reference.
        /// </summary>
        /// <param name="spline">The spline to modify.</param>
        public static void UpdateUpVectors(Spline spline)
        {
            if (spline == null || spline.nodes.Count < 2) return;

            var firstNode = spline.nodes[0];
            Vector3 firstTangent = (firstNode.Direction - firstNode.Position).normalized;
            // Ensure we have a valid tangent
            if (firstTangent == Vector3.zero) firstTangent = Vector3.forward;

            Vector3 currentUp = firstNode.Up == Vector3.zero ? Vector3.up : firstNode.Up;
            Vector3.OrthoNormalize(ref firstTangent, ref currentUp);
            firstNode.Up = currentUp;

            for (int i = 1; i < spline.nodes.Count; i++)
            {
                if (spline.IsLoop && i == spline.nodes.Count - 1) break;

                var prevNode = spline.nodes[i - 1];
                var currentNode = spline.nodes[i];

                Vector3 prevTangent = (prevNode.Direction - prevNode.Position).normalized;
                Vector3 curTangent = (currentNode.Direction - currentNode.Position).normalized;

                Quaternion rot = Quaternion.FromToRotation(prevTangent, curTangent);
                currentNode.Up = rot * prevNode.Up;
            }
        }

        /// <summary>
        /// Rotates each spline node's up vector around its tangent axis by a smooth noise-based
        /// angle, introducing gradual roll variation along the path. Call after
        /// <see cref="UpdateUpVectors"/> to layer roll noise on top of parallel transport.
        /// No-op when <paramref name="config"/> is disabled or <c>upVectorNoiseMaxAngle</c> is zero.
        ///
        /// <b>How:</b> While mesh noise pushes the skin of the tube in/out,
        /// this method twists the whole tube — like slowly wringing a towel. Each node's "up"
        /// direction (which controls roll, i.e. how the cross-section is oriented around the tube
        /// axis) is rotated by a smooth noise-derived angle. This breaks the visual uniformity of
        /// surface deformations when the model is viewed from inside.
        ///
        /// A three-pass approach is used:
        /// 1. Compute raw noise angles for every node.
        /// 2. Cap the angle delta between adjacent nodes to <c>upVectorNoiseMaxStepAngle</c> —
        ///    this acts as a speed-limit that prevents sudden kinks regardless of frequency.
        /// 3. Apply the clamped angles by rotating each node's up vector around its tangent.
        /// </summary>
        public static void ApplyUpVectorNoise(Spline spline, NoiseSettings config)
        {
            if (spline == null || spline.nodes.Count < 2) return;
            if (!config.UpVectorNoiseEnabled || config.UpVectorNoiseMaxAngle <= 0f) return;

            int nodeCount = spline.nodes.Count;
            // Mask to 14 bits: 16383 * 73.13 = ~1.2 M, ULP ≈ 0.125 — safe float precision.
            // Matches SplineMeshNoiseApplicator limit (16384 distinct patterns).
            float seedOffset = (config.Seed & 0x3FFF) * 73.13f;
            float f = config.UpVectorNoiseFrequency;

            // Pass 1: compute raw angles from noise
            float[] angles = new float[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                float t = (float)i / Mathf.Max(1, nodeCount - 1);
                float coarse = (Mathf.PerlinNoise(t * f       + seedOffset,        seedOffset * 0.31f + 5.7f) * 2f - 1f) * 2.5f;
                float fine   = (Mathf.PerlinNoise(t * f * 2f  + seedOffset * 1.7f, seedOffset * 0.57f + 13f)  * 2f - 1f) * 2.5f;
                float noiseVal = Mathf.Clamp((coarse + fine * 0.5f) / 1.5f, -1f, 1f);
                angles[i] = noiseVal * config.UpVectorNoiseMaxAngle;
            }

            // Pass 2: cap the delta between adjacent nodes to prevent sudden flips
            float maxStep = config.UpVectorNoiseMaxStepAngle;
            for (int i = 1; i < nodeCount; i++)
            {
                float delta = angles[i] - angles[i - 1];
                if (Mathf.Abs(delta) > maxStep)
                    angles[i] = angles[i - 1] + Mathf.Sign(delta) * maxStep;
            }

            // Pass 3: apply angles to nodes
            for (int i = 0; i < nodeCount; i++)
            {
                var node = spline.nodes[i];
                Vector3 tangent = (node.Direction - node.Position).normalized;
                if (tangent.sqrMagnitude < 0.001f) continue;
                node.Up = (Quaternion.AngleAxis(angles[i], tangent) * node.Up).normalized;
                //Debug.Log($"[UpVectorNoise] Node {i}: angle = {angles[i]:F2}°");
            }

            spline.RefreshCurves();
        }

        /// <summary>
        /// Calculates the centroid (center point) of all spline node positions.
        /// </summary>
        /// <param name="spline">The spline to analyze.</param>
        /// <returns>The centroid position.</returns>
        public static Vector3 GetSplineNodesCentroid(Spline spline)
        {
            if (spline.nodes.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            foreach (var node in spline.nodes)
            {
                sum += node.Position;
            }
            return sum / spline.nodes.Count;
        }

        #endregion

        #region Preset Methods

        /// <summary>
        /// Applies a spline preset to a spline, replacing node data starting from the preset's start index.
        /// </summary>
        /// <param name="spline">The spline to modify.</param>
        /// <param name="splinePreset">The preset to apply.</param>
        public static void ApplySplinePreset(Spline spline, SplinePreset splinePreset)
        {
            for (int i = 0; i < splinePreset.modifiedNodes.Count; i++)
            {
                int targetIndex = splinePreset.startNodeIdx + i;
                if (targetIndex < spline.nodes.Count)
                {
                    spline.nodes[targetIndex].ReplaceData(splinePreset.modifiedNodes[i]);
                }
            }
        }

        /// <summary>
        /// Loads spline preset data into a spline, copying position, direction, up, and scale from preset nodes.
        /// </summary>
        /// <param name="spline">The spline to modify.</param>
        /// <param name="splinePreset">The preset containing the node data.</param>
        public static void LoadSplinePreset(Spline spline, SplinePreset splinePreset)
        {
            for (int i = 0; i < splinePreset.modifiedNodes.Count; i++)
            {
                int targetIndex = splinePreset.startNodeIdx + i;
                if (targetIndex < spline.nodes.Count)
                {
                    var oldNode = spline.nodes[targetIndex];
                    var newNode = splinePreset.modifiedNodes[i];
                    oldNode.Position = newNode.Position;
                    oldNode.Direction = newNode.Direction;
                    oldNode.Up = newNode.Up;
                    oldNode.Scale = newNode.Scale;
                }
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Creates a deep copy of a spline node.
        /// </summary>
        /// <param name="source">The source node to copy.</param>
        /// <returns>A new SplineNode with the same data.</returns>
        public static SplineNode CloneSplineNode(SplineNode source)
        {
            return new SplineNode(source.Position, source.Direction)
            {
                Up = source.Up,
                Scale = source.Scale
            };
        }

        /// <summary>
        /// Creates a deep copy of a list of spline nodes.
        /// </summary>
        /// <param name="sourceNodes">The source nodes to copy.</param>
        /// <returns>A new list of SplineNodes with the same data.</returns>
        public static List<SplineNode> CloneSplineNodes(List<SplineNode> sourceNodes)
        {
            var clonedNodes = new List<SplineNode>(sourceNodes.Count);
            foreach (var node in sourceNodes)
            {
                clonedNodes.Add(CloneSplineNode(node));
            }
            return clonedNodes;
        }

        /// <summary>
        /// Gets the total arc length of a spline.
        /// </summary>
        /// <param name="spline">The spline to measure.</param>
        /// <returns>The total length of the spline.</returns>
        public static float GetSplineLength(Spline spline)
        {
            return spline.Length;
        }

        /// <summary>
        /// Gets the number of nodes in a spline.
        /// </summary>
        /// <param name="spline">The spline to count nodes from.</param>
        /// <returns>The number of nodes.</returns>
        public static int GetSplineNodeCount(Spline spline)
        {
            return spline.nodes.Count;
        }

        #endregion
    }
}