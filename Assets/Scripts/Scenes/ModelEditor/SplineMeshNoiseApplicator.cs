using GeometryUtils;
using UnityEngine;

namespace LargeIntestine
{
    /// <summary>
    /// Applies smooth, spline-parameterized anatomical noise to mesh section vertices
    /// before the spline bending step.
    ///
    /// <b>How:</b> Each vertex of the straight tube mesh is pushed
    /// slightly in or out (in the radial direction) to create an organic, bumpy surface.
    /// The amount of push is determined by layered Perlin noise (three octaves of fBm)
    /// sampled at the vertex's global position along the intestine and its angular position
    /// around the tube's cross-section.
    ///
    /// <b>Key implementation details:</b>
    /// <list type="bullet">
    ///   <item>Noise is applied in <em>source-mesh space</em> (straight tube, before spline
    ///   bending) so radial directions are trivial to compute (just the Y/Z components).</item>
    ///   <item>A global arc-length coordinate (tNorm) is used so all sections sample the same
    ///   continuous noise field — the bump pattern flows seamlessly across section boundaries
    ///   without any special blending.</item>
    ///   <item>Three fBm octaves (60 % large + 25 % medium + 15 % fine) give both large-scale
    ///   shape and fine surface rugosity. Weights sum to 1.0 so radialAmplitude is always
    ///   the true maximum displacement.</item>
    ///   <item>A golden-ratio phase shift spirals the angular lobe pattern along the tube axis,
    ///   preventing the straight-ridge artifact of static angular sampling.</item>
    ///   <item>Frequency multipliers 2.3 × and 5.1 × are chosen to be irrational with respect
    ///   to each other, preventing repeating harmonic grid patterns.</item>
    /// </list>
    ///
    /// An optional center-fade envelope suppresses noise near the haustral fold (tSection = 0.5)
    /// to model the reduced wall deformability near the teniae coli.
    /// An optional tail-fade envelope linearly reduces amplitude to zero over the last section(s),
    /// ensuring zero displacement at the distal end.
    /// </summary>
    public static class SplineMeshNoiseApplicator
    {
        /// <summary>
        /// Displaces vertices of <paramref name="meshData"/> radially using Perlin noise
        /// sampled at global spline-space coordinates (t, θ). Adjacent sections share the
        /// same t at their boundary → same displacement → weld continuity without seam fading.
        /// Call this after <see cref="MeshData.BuildData"/> and before assigning the
        /// MeshData to a CustomMeshBender.
        /// </summary>
        /// <param name="meshData">The mesh data to modify in place.</param>
        /// <param name="intervalStart">Arc-length start of this section along the whole spline.</param>
        /// <param name="curveArcLength">Arc-length span of this section's spline interval.</param>
        /// <param name="totalSplineLength">Total arc-length of the spline (used to normalize t).</param>
        /// <param name="config">Noise parameters.</param>
        /// <param name="tailFadeStartArcLength">
        /// Arc-length where the tail amplitude fade begins (linear ramp 1 → TailJunctionAmplitude
        /// ending at tailSectionStartArcLength). Pass -1 to disable the two-step tail envelope.
        /// </param>
        /// <param name="tailSectionStartArcLength">
        /// Arc-length at the start of the last section (section n). Two roles:
        /// (1) fade endpoint for section n-1 — amplitude reaches TailJunctionAmplitude here;
        /// (2) centerFade is skipped for vertices at or beyond this point so the last section
        ///     uses the bump envelope instead of the haustral-fold suppression.
        /// Pass -1 to fall back to a simple linear fade to zero at totalSplineLength.
        /// </param>
        public static void Apply(
            MeshData meshData,
            float intervalStart,
            float curveArcLength,
            float totalSplineLength,
            NoiseSettings config,
            float tailFadeStartArcLength = -1f,
            float tailSectionStartArcLength = -1f)
        {
            if (!config.MeshNoiseEnabled || totalSplineLength <= 0f || curveArcLength <= 0f) return;

            var vertices = meshData.Vertices;
            float minX = meshData.MinX;
            float meshLength = meshData.Length;

            // Mask seed to 14 bits (0..16383) before multiplying.
            // At full int range, seed * 311.7f exceeds float precision and wipes out
            // all per-vertex variation — every vertex samples the same Perlin value.
            // Limit: offset must stay below 2^23 = 8.4 M so ULP < 1 and angular variation
            // (±2) remains resolvable. 16383 * 311.7 = ~5.1 M → ULP = 0.5. ✓
            // 16384 distinct noise patterns; any seed value wraps into this range.
            int safeSeed = config.Seed & 0x3FFF;
            float seedOffsetX = safeSeed * 127.1f;
            float seedOffsetY = safeSeed * 311.7f;

            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];

                float ry = v.position.y;
                float rz = v.position.z;
                float r = Mathf.Sqrt(ry * ry + rz * rz);
                if (r < 1e-6f) continue; // skip vertices on the tube axis

                // tSection in [0,1]: local position within this section (0 = start, 1 = end).
                float tSection = meshLength > 0f
                    ? Mathf.Clamp01((v.position.x - minX) / meshLength)
                    : 0f;

                // Convert to a global position along the whole intestine.
                // tAbs  = absolute arc-length from the very start of the spline.
                // tNorm = fraction of the full intestine traversed (0 = start, 1 = end).
                // Using tNorm as the noise lookup key means all sections sample the same
                // continuous noise field: at the boundary between two sections both sides
                // evaluate to the same tAbs → same tNorm → same noise → same displacement.
                // The seam closes without any special fading or blending.
                float tAbs = intervalStart + tSection * curveArcLength;
                float tNorm = tAbs / totalSplineLength;

                // Tail section (section n): centerFade is skipped so the tail amplitude can
                // decay smoothly without the haustral-fold dip interrupting it.
                bool inTailSection = tailSectionStartArcLength >= 0f && tAbs >= tailSectionStartArcLength;

                // Center fade: suppress noise near the haustral fold (tSection = 0.5).
                // centerFade defines a flat-zero zone width (0 = no effect, 1 = full section zeroed).
                // Outside the flat zone a short SmoothStep transition rises to full amplitude.
                float centerEnvelope = 1f;
                if (!inTailSection && config.CenterFade > 0f)
                {
                    float distFromCenter = Mathf.Abs(tSection - 0.5f); // [0, 0.5]
                    float halfZone = config.CenterFade * 0.5f;
                    if (distFromCenter <= halfZone)
                    {
                        centerEnvelope = 0f;
                    }
                    else
                    {
                        float remaining = 0.5f - halfZone; // space between zone edge and mesh edge
                        float transitionWidth = Mathf.Min(0.1f, remaining);
                        float t = Mathf.Clamp01((distFromCenter - halfZone) / transitionWidth);
                        centerEnvelope = Mathf.SmoothStep(0f, 1f, t);
                    }
                }
                if (centerEnvelope < 1e-6f) continue;

                float theta = Mathf.Atan2(rz, ry);

                // The phase shift makes the angular lobe pattern spiral around the tube axis
                // instead of running as straight ridges down its length. It grows continuously
                // with tNorm using the golden ratio (≈ 1.618), an irrational number whose
                // non-repeating decimal prevents the spiral from settling into a repeating
                // pattern at any point along the tube.
                float phaseShift  = tNorm * config.AxialFrequency * 1.618f * Mathf.PI; // golden ratio

                float aFreq = config.AngularFrequency;

                // Octave 1 — base shape deformation
                float u1 = Mathf.Sin(theta + phaseShift) * aFreq + seedOffsetY;
                float v1 = Mathf.Cos(theta + phaseShift) * aFreq + seedOffsetX * 0.7f;
                float n1 = Mathf.PerlinNoise(tNorm * config.AxialFrequency + seedOffsetX + u1 * 0.5f, v1) * 2f - 1f;

                // Octave 2 — medium bumps (2.3× axial/angular frequency, 25% weight)
                float u2 = Mathf.Sin(theta + phaseShift * 1.7f) * aFreq * 2.3f + seedOffsetX * 0.5f;
                float v2 = Mathf.Cos(theta + phaseShift * 1.7f) * aFreq * 2.3f + seedOffsetY * 0.8f;
                float n2 = Mathf.PerlinNoise(tNorm * config.AxialFrequency * 2.3f + seedOffsetY * 1.1f + u2 * 0.5f, v2) * 2f - 1f;

                // Octave 3 — fine rugosity (5.1× frequency, 15% weight)
                float u3 = Mathf.Sin(theta * 2f + phaseShift * 0.9f) * aFreq * 5.1f + seedOffsetX * 1.3f;
                float v3 = Mathf.Cos(theta * 2f + phaseShift * 0.9f) * aFreq * 5.1f + seedOffsetY * 0.5f;
                float n3 = Mathf.PerlinNoise(tNorm * config.AxialFrequency * 5.1f + seedOffsetX * 1.7f + u3 * 0.5f, v3) * 2f - 1f;

                // Blend octaves (fractal Brownian Motion / fBm):
                //   60% large hills  +  25% medium bumps  +  15% fine rugosity
                // Weights sum to 1.0, so the combined output stays in roughly [-1, 1] and
                // radialAmplitude remains the true maximum possible displacement per vertex.
                // Frequency multipliers 2.3× and 5.1× are irrational relative to each other,
                // preventing repeating harmonic grid patterns.
                float noise = n1 * 0.60f + n2 * 0.25f + n3 * 0.15f;

                // Two-step tail envelope:
                //   Section n-1: linear 1 → TailJunctionAmplitude (fade from tailFadeStart to tailSectionStart)
                //   Section n:   bump  TailJunctionAmplitude → TailPeakAmplitude → 0
                //     shape: A·sin(π·tN) + TailJunctionAmplitude·(1−tN)
                //     satisfies f(0)=junction, f(0.5)=peak, f(1)=0
                //     where A = peak − junction/2
                // Falls back to a plain linear fade to 0 at totalSplineLength when tailSectionStartArcLength < 0.
                const float TailJunctionAmplitude = 0.2f;
                const float TailPeakAmplitude     = 0.5f;
                float tailEnvelope = 1f;
                if (tailFadeStartArcLength >= 0f)
                {
                    if (tailSectionStartArcLength >= 0f && tAbs >= tailSectionStartArcLength)
                    {
                        // Section n bump: rises from junction value, peaks at mid, returns to 0.
                        float lastLen = totalSplineLength - tailSectionStartArcLength;
                        float tN = lastLen > 0f ? Mathf.Clamp01((tAbs - tailSectionStartArcLength) / lastLen) : 1f;
                        float sineAmp = TailPeakAmplitude - TailJunctionAmplitude * 0.5f;
                        tailEnvelope = sineAmp * Mathf.Sin(Mathf.PI * tN) + TailJunctionAmplitude * (1f - tN);
                    }
                    else
                    {
                        // Section n-1 (and earlier): linear fade to junction value.
                        float fadeEnd = tailSectionStartArcLength >= 0f ? tailSectionStartArcLength : totalSplineLength;
                        float fadeRange = fadeEnd - tailFadeStartArcLength;
                        if (fadeRange > 0f)
                        {
                            float t = Mathf.Clamp01((tAbs - tailFadeStartArcLength) / fadeRange);
                            tailEnvelope = Mathf.Lerp(1f, TailJunctionAmplitude, t);
                        }
                    }
                }
                if (tailEnvelope < 1e-6f) continue;

                float displacement = noise * config.RadialAmplitude * centerEnvelope * tailEnvelope;

                v.position.y += displacement * (ry / r);
                v.position.z += displacement * (rz / r);

                vertices[i] = v;
            }
        }

    }
}
