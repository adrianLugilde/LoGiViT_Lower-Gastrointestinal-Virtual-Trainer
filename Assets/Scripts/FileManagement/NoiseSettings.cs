using System;

/// <summary>
/// Per-model noise configuration for anatomical surface bumps and tract-twist noise.
/// Serialized into .tc model files via Newtonsoft JSON.
/// Defaults: both noise systems off, values ready to enable without further tuning.
/// </summary>
[Serializable]
public class NoiseSettings
{
    // --- Surface bump noise ---

    /// <summary>Master switch for radial vertex displacement noise on the mesh surface.</summary>
    public bool MeshNoiseEnabled { get; set; } = true;

    /// <summary>
    /// Maximum radial displacement of a vertex (in local mesh units).
    /// Because fBm weights sum to 1.0, this is the true worst-case displacement.
    /// Typical range: 0.001 – 0.02. UI slider max: 0.05.
    /// </summary>
    public float RadialAmplitude { get; set; } = 0.03f;

    /// <summary>
    /// How many full noise cycles fit along the entire spline length.
    /// Higher values = tighter, more frequent bumps along the tube axis.
    /// Typical range: 1 – 20. UI slider max: 50.
    /// </summary>
    public float AxialFrequency { get; set; } = 8f;

    /// <summary>
    /// Scales the angular (circumferential) frequency of the noise lobes.
    /// Higher values = more lobes around the tube cross-section.
    /// Advanced — not exposed in UI. Default: 2.
    /// </summary>
    public float AngularFrequency { get; set; } = 2f;

    /// <summary>
    /// Width of the flat-zero suppression zone centred on the haustral fold (tSection = 0.5).
    /// 0 = no suppression; 1 = entire section zeroed.
    /// Models reduced wall deformability near the teniae coli.
    /// Advanced — not exposed in UI. Default: 0.05.
    /// </summary>
    public float CenterFade { get; set; } = 0.1f;

    // --- Up-vector twist noise ---

    /// <summary>Master switch for roll-variation noise applied to spline up-vectors.</summary>
    public bool UpVectorNoiseEnabled { get; set; } = true;

    /// <summary>
    /// Maximum rotation angle (degrees) that the up-vector may be rotated around the spline tangent.
    /// 1 = minimal twist; 90 = full quarter-turn possible.
    /// UI slider max: 90.
    /// </summary>
    public float UpVectorNoiseMaxAngle { get; set; } = 45f;

    /// <summary>
    /// Perlin noise frequency for up-vector angle sampling along the spline.
    /// Higher values = more rapid twist oscillation between nodes.
    /// Advanced — not exposed in UI. Default: 25.
    /// </summary>
    public float UpVectorNoiseFrequency { get; set; } = 25f;

    /// <summary>
    /// Maximum allowed angular delta (degrees) between adjacent spline nodes.
    /// Caps sudden twist jumps that would cause visible mesh flipping.
    /// Advanced — not exposed in UI. Default: 45.
    /// </summary>
    public float UpVectorNoiseMaxStepAngle { get; set; } = 45f;

    // --- Shared ---

    /// <summary>
    /// Integer seed for all noise functions in this model.
    /// Same seed + same configuration → identical noise pattern every generation.
    /// Change via the Randomize button in the Noise panel.
    /// </summary>
    public int Seed { get; set; } = 1996;

    public NoiseSettings() { }

    public NoiseSettings(NoiseSettings other)
    {
        MeshNoiseEnabled = other.MeshNoiseEnabled;
        RadialAmplitude = other.RadialAmplitude;
        AxialFrequency = other.AxialFrequency;
        AngularFrequency = other.AngularFrequency;
        CenterFade = other.CenterFade;
        UpVectorNoiseEnabled = other.UpVectorNoiseEnabled;
        UpVectorNoiseMaxAngle = other.UpVectorNoiseMaxAngle;
        UpVectorNoiseFrequency = other.UpVectorNoiseFrequency;
        UpVectorNoiseMaxStepAngle = other.UpVectorNoiseMaxStepAngle;
        Seed = other.Seed;
    }
}
