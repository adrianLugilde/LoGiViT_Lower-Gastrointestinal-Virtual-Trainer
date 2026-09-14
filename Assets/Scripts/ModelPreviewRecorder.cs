using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Utils.Math;
using Newtonsoft.Json;
using SplineMesh;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class ModelPreviewRecorder : MonoBehaviour
{
    [Header("Sphere")]
    [Min(0.001f)] public float radius = 0.5f;

    [Tooltip("Keep waypoints this many degrees away from exact poles to avoid singularities.")]
    [Range(0.1f, 10f)] public float poleMarginDeg = 1.5f;

    [Header("Spacing (meters along the surface)")]
    [Tooltip("Distance along the spiral between consecutive waypoints.")]
    [Min(0.005f)] public float pointSpacing = 0.05f;

    [Tooltip("Distance between adjacent windings (measured along a meridian). " +
             "Smaller = more turns around the sphere.")]
    [Min(0.01f)] public float loopSpacing = 0.15f;

    [Header("Speeds (m/s along spline)")]
    public float surroundingCameraRateMultiplier = 10f;
    public float modelCameraRateMultiplier = 5f;
    public float reverseCameraRateMultiplier = 5f;

    [Header("Reverse Look (circular orbit)")]
    [Tooltip("Meters ahead (along the reverse tangent) where the center of the look orbit sits.")]
    public float lookOrbitDistance = 0.4f;
    [Tooltip("Radius in meters of the circular look around the reverse tangent.")]
    public float lookOrbitRadius = 0.12f;
    [Tooltip("Revolutions per second of the look orbit while reversing.")]
    public float lookOrbitRevsPerSecond = 0.25f;

    [Header("Landmarks (reverse phase)")]
    [Tooltip("All landmarks share this same hold time (seconds).")]
    public float landmarkHoldSeconds = 2.0f;
    [Tooltip("Within this distance (meters along spline) of a landmark, start blending look toward it.")]
    public float approachBlendDistance = 1.0f;
    [Tooltip("Within this distance (meters along spline) of a landmark, stop moving and hold gaze.")]
    public float stopDistance = 0.1f;

    [Serializable]
    public class Landmark
    {
        public Vector3 position;
        [HideInInspector] public float projectedDistance; // distance along model spline (meters)
    }

    [SerializeField] public List<Landmark> landmarks = new List<Landmark>();

    [Header("Gizmos")]
    public bool drawConnections = true;
    public Color gizmoColor = new Color(1f, 0.5f, 0f, 1f);
    [Min(0.001f)] public float cubeSize = 0.015f;

    [Header("IO & Refs")]
    [SerializeField] private string modelDirectoryPath = "";
    [SerializeField] private string fileName = "";
    [SerializeField] private Spline _baseSurroundingSpline;
    [SerializeField] private Spline _baseModelSpline;
    [SerializeField] private Spline _surroundingSpline;
    [SerializeField] private Spline _modelSpline;
    [SerializeField] private GameObject _cameraHolder;

    private SplineSmoother _splineSmoother;
    public bool Navigating = false;
    public float CameraRate = 0f; // meters along *current* spline
    private float surroundingSplineLength = 0f;
    private float modelSplineLength = 0f;
    private Vector3 splineModelCentroid = Vector3.zero;

    public List<Vector3> Waypoints = new List<Vector3>();

    // --- Navigation State Machine ---
    private enum NavPhase
    {
        SurroundingForward,
        ModelForward,
        ModelReverseOrbit,
        LandmarkHold,
        Done
    }
    [SerializeField] private NavPhase _phase = NavPhase.SurroundingForward;
    [SerializeField, HideInInspector] private int _reverseLandmarkIndex = -1; // index into _landmarksDesc for reverse traversal
    private List<Landmark> _landmarksDesc = new List<Landmark>(); // sorted by projectedDistance descending for reverse
    private float _holdTimer = 0f;
    private float _orbitPhase = 0f; // radians
    private const float TwoPI = Mathf.PI * 2f;

    private void OnValidate() => Rebuild();
    private void Reset() => Rebuild();
    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) Rebuild();
#endif
    }

    // --- Waypoints generation (loxodrome on sphere) ---
    public void Rebuild()
    {
        Waypoints.Clear();

        float R = Mathf.Max(0.0001f, radius);

        // Loxodrome: phi = k * theta where k controls wrapping count via loopSpacing.
        float k = (2f * Mathf.PI * R) / Mathf.Max(0.001f, loopSpacing);

        float marginRad = Mathf.Deg2Rad * Mathf.Clamp(poleMarginDeg, 0.01f, 89.9f);
        float theta = marginRad;
        float thetaMax = Mathf.PI - marginRad;

        int safety = 0, safetyMax = 200000;
        while (theta <= thetaMax && safety++ < safetyMax)
        {
            float phi = k * theta;

            float sinT = Mathf.Sin(theta);
            float cosT = Mathf.Cos(theta);

            // Spherical -> Cartesian (y up)
            Vector3 local = new Vector3(
                R * sinT * Mathf.Cos(phi),
                R * cosT,
                R * sinT * Mathf.Sin(phi)
            );

            Waypoints.Add(transform.TransformPoint(local));

            // Arc length differential on sphere for phi=k*theta:
            // ds = R * sqrt(1 + k^2 * sin^2(theta)) dtheta  =>  dtheta = ds / (R * sqrt(...))
            float dTheta = pointSpacing / (R * Mathf.Sqrt(1f + (k * k * sinT * sinT)));
            if (!float.IsFinite(dTheta) || dTheta <= 1e-7f) break;
            theta += dTheta;
        }
    }

    public List<Vector3> GetWaypoints() => new List<Vector3>(Waypoints);

    // --- Surrounding spline creation ---
    public void GenerateSurroundingSpline()
    {
        if (_surroundingSpline == null)
            _surroundingSpline = Instantiate(_baseSurroundingSpline, transform);
        ReplaceSplineNodes();
    }

    private void ReplaceSplineNodes()
    {
        if (_surroundingSpline == null) return;
        if (Waypoints.Count < 2) return;

        if (!_splineSmoother) _splineSmoother = _surroundingSpline.GetComponent<SplineSmoother>();
        _splineSmoother.enabled = false;
        _surroundingSpline.nodes.Clear();

        for (int i = 0; i < Waypoints.Count; i++)
        {
            var node = new SplineNode(transform.InverseTransformPoint(Waypoints[i]), GetNodeDirection(i));
            _surroundingSpline.AddNode(node);
        }
        _splineSmoother.enabled = true;
        _surroundingSpline.RefreshCurves();
        _splineSmoother.SmoothAll();
    }

    private void ReplaceSplineNodes(List<SplineNode> nodes)
    {
        if (_modelSpline == null) return;
        if (nodes == null || nodes.Count < 2) return;

        var smoother = _modelSpline.GetComponent<SplineSmoother>();
        if (smoother) smoother.enabled = false;
        _modelSpline.nodes.Clear();

        foreach (var n in nodes)
        {
            var node = new SplineNode(n.Position, n.Direction, n.Roll, n.Up);
            _modelSpline.AddNode(node);
        }
        _modelSpline.RefreshCurves();
        if (smoother)
        {
            smoother.enabled = true;
            smoother.SmoothAll();
        }
    }

    private Vector3 GetNodeDirection(int index)
    {
        if (index < Waypoints.Count - 2)
            return transform.InverseTransformDirection((Waypoints[index + 1] - Waypoints[index]).normalized);
        else
            return transform.InverseTransformDirection((Waypoints[index] - Waypoints[index - 1]).normalized);
    }

    public void ClearWaypoints() { Waypoints.Clear(); }

    // --- Loading recorded (model) spline and wiring orbit up vectors ---
    public void InstantianteRecordingSplines()
    {
        if (string.IsNullOrEmpty(modelDirectoryPath) || string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("InstantianteRecordingSplines: modelDatasetPath or fileName is empty.");
            return;
        }

        if (_modelSpline == null)
        {
            _modelSpline = Instantiate(_baseSurroundingSpline, transform); // intentional: reuse template
            if (_modelSpline == null)
            {
                Debug.LogError("InstantianteRecordingSplines: _modelSpline is null and could not be created.");
                return;
            }
        }

        string modelPath = Path.Combine(modelDirectoryPath, fileName);

        if (!File.Exists(modelPath))
        {
            Debug.LogError($"InstantianteRecordingSplines: File not found at '{modelPath}'.");
            return;
        }

        var contents = File.ReadAllText(modelPath);
        var model = JsonConvert.DeserializeObject<Model>(contents, JsonSerializationSettings.ModelJsonSettings);
        if (model != null)
        {
            ReplaceSplineNodes(model.SplineNodes);
        }

        splineModelCentroid = Utilities.MathUtils.GetCentroid(_modelSpline.nodes.Select(n => n.Position));

        GenerateSurroundingSpline();
        foreach (var node in _surroundingSpline.nodes)
        {
            node.Up = splineModelCentroid - node.Position;
        }
        _surroundingSpline.RefreshCurves();

        // after model spline is ready compute lengths and project landmarks
        modelSplineLength = _modelSpline.Length - _modelSpline.curves[2].Length;
        ProjectLandmarksOntoModelSpline();
    }

    // --- Navigation control ---
    public void StartNavigation()
    {
#if UNITY_EDITOR
        EditorApplication.update -= FixedUpdate;
        EditorApplication.update += FixedUpdate;
#endif
        if (_surroundingSpline == null || _modelSpline == null || _cameraHolder == null)
        {
            Debug.LogError("StartNavigation: Missing spline(s) or camera holder.");
            return;
        }

        splineModelCentroid = Utilities.MathUtils.GetCentroid(_modelSpline.nodes.Select(n => n.Position));
        surroundingSplineLength = _surroundingSpline.Length;
        modelSplineLength = _modelSpline.Length - _modelSpline.curves[2].Length;

        CameraRate = 0f;
        _phase = NavPhase.SurroundingForward;
        _orbitPhase = 0f;
        _holdTimer = 0f;

        // prepare reverse landmark order (descending by projected distance)
        _landmarksDesc = landmarks
            .Where(l => l != null)
            .OrderByDescending(l => l.projectedDistance)
            .ToList();
        _reverseLandmarkIndex = _landmarksDesc.Count - 1; // start beyond end; we decrement as we pass

        Navigating = true;

        Debug.Log($"Start navigation. Surrounding length={surroundingSplineLength:F2}, Model length={modelSplineLength:F2}, Landmarks={_landmarksDesc.Count}");
    }

    private void FixedUpdate()
    {
        if (!Navigating || _cameraHolder == null) return;

        switch (_phase)
        {
            case NavPhase.SurroundingForward:
                DoSurroundingForward();
                break;
            case NavPhase.ModelForward:
                DoModelForward();
                break;
            case NavPhase.ModelReverseOrbit:
                DoModelReverseOrbit();
                break;
            case NavPhase.LandmarkHold:
                DoLandmarkHold();
                break;
            case NavPhase.Done:
            default:
                StopNavigation();
                break;
        }
    }

    private void DoSurroundingForward()
    {
        if (CameraRate >= surroundingSplineLength)
        {
            // switch to model forward
            CameraRate = 0f;
            _phase = NavPhase.ModelForward;
            return;
        }

        var sample = _surroundingSpline.GetSampleAtDistance(CameraRate);
        _cameraHolder.transform.localPosition = sample.location;
        // look toward model centroid
        _cameraHolder.transform.localRotation = Quaternion.LookRotation(splineModelCentroid - sample.location, Vector3.up);

        CameraRate += Time.deltaTime * surroundingCameraRateMultiplier;
    }

    private void DoModelForward()
    {
        if (CameraRate >= modelSplineLength)
        {
            // reached end -> prepare reverse
            CameraRate = modelSplineLength;
            _phase = NavPhase.ModelReverseOrbit;
            return;
        }

        var p0 = _modelSpline.GetSampleAtDistance(CameraRate).location;
        var p1 = _modelSpline.GetSampleAtDistance(CameraRate + 0.25f).location;
        var forwardLocal = (p1 - p0).normalized;
        _cameraHolder.transform.localPosition = p0;
        _cameraHolder.transform.localRotation = Quaternion.LookRotation(forwardLocal, Vector3.up);

        CameraRate += Time.deltaTime * modelCameraRateMultiplier;
    }

    private void DoModelReverseOrbit()
    {
        // move backward
        CameraRate -= Time.deltaTime * reverseCameraRateMultiplier;
        if (CameraRate <= 0f)
        {
            _phase = NavPhase.Done;
            return;
        }

        var sample = _modelSpline.GetSampleAtDistance(CameraRate);
        _cameraHolder.transform.localPosition = sample.location;

        // Build reverse tangent (look direction target baseline)
        float ahead = 0.25f;
        float sBack = Mathf.Max(0f, CameraRate - ahead);
        var posBack = _modelSpline.GetSampleAtDistance(sBack).location;
        var posNow = sample.location;
        var T = (posBack - posNow).normalized; // backwards along spline

        // Construct a stable local frame orthonormal to T
        var upRef = Vector3.up;
        if (Vector3.Dot(T, upRef) > 0.95f) upRef = Vector3.right; // avoid near-collinearity
        var N = Vector3.Normalize(Vector3.Cross(upRef, T)); // right
        var B = Vector3.Normalize(Vector3.Cross(T, N));     // up-ish in the T-orthogonal plane

        // Orbiting look target
        _orbitPhase += TwoPI * lookOrbitRevsPerSecond * Time.deltaTime;
        var center = posNow + T * lookOrbitDistance;
        var orbitOffset = (Mathf.Cos(_orbitPhase) * N + Mathf.Sin(_orbitPhase) * B) * lookOrbitRadius;
        var orbitTarget = center + orbitOffset;

        // Landmark approach/hold detection
        Landmark currentLm = GetCurrentReverseLandmark();
        Vector3 desiredTarget = orbitTarget;

        if (currentLm != null)
        {
            float distToLm = Mathf.Abs(CameraRate - currentLm.projectedDistance);

            // Blend toward landmark when within approach window
            if (distToLm <= approachBlendDistance)
            {
                Vector3 toOrbit = (orbitTarget - posNow).normalized;
                Vector3 toLm = (currentLm.position - transform.TransformPoint(posNow)).normalized; // currentLm is world; posNow is local
                // ensure both in world space
                Vector3 toLmWorld = (currentLm.position - _cameraHolder.transform.position).normalized;
                Vector3 toOrbitWorld = (transform.TransformPoint(orbitTarget) - _cameraHolder.transform.position).normalized;
                float t = Mathf.InverseLerp(approachBlendDistance, 0f, distToLm);
                var blendedDir = Vector3.Slerp(toOrbitWorld, toLmWorld, t);
                desiredTarget = _cameraHolder.transform.position + blendedDir * 2f; // any positive scalar
            }

            // Stop and hold if within stop distance
            if (distToLm <= stopDistance)
            {
                _phase = NavPhase.LandmarkHold;
                _holdTimer = Mathf.Max(0f, landmarkHoldSeconds);
                return;
            }
        }

        // Apply rotation to look at desired target
        var lookDirWorld = (desiredTarget - _cameraHolder.transform.position).normalized;
        if (lookDirWorld.sqrMagnitude > 1e-6f)
            _cameraHolder.transform.rotation = Quaternion.LookRotation(lookDirWorld, Vector3.up);
    }

    private void DoLandmarkHold()
    {
        // Keep looking at the current landmark
        Landmark lm = GetCurrentReverseLandmark();
        if (lm == null)
        {
            _phase = NavPhase.ModelReverseOrbit;
            return;
        }

        // Freeze position at current CameraRate (already set on entry)
        var posNow = _modelSpline.GetSampleAtDistance(CameraRate).location;
        _cameraHolder.transform.localPosition = posNow;

        // Look at landmark
        var lookDir = (lm.position - _cameraHolder.transform.position).normalized;
        if (lookDir.sqrMagnitude > 1e-6f)
            _cameraHolder.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

        _holdTimer -= Time.deltaTime;
        if (_holdTimer <= 0f)
        {
            // consume this landmark and continue
            _reverseLandmarkIndex--;
            _phase = NavPhase.ModelReverseOrbit;
        }
    }

    private Landmark GetCurrentReverseLandmark()
    {
        if (_landmarksDesc == null || _landmarksDesc.Count == 0) return null;

        // ensure index points to the next landmark behind current CameraRate
        // while CameraRate is less than the landmark distance + approach window, this is the current one
        for (int i = _reverseLandmarkIndex; i >= 0; --i)
        {
            var lm = _landmarksDesc[i];
            // Because we move from high s to low s, as soon as we pass below a landmark distance + approach window,
            // that landmark is the target.
            if (CameraRate <= lm.projectedDistance + approachBlendDistance + 0.0001f)
            {
                _reverseLandmarkIndex = i;
                return lm;
            }
        }
        return null;
    }

    public void StopNavigation()
    {
        Navigating = false;
#if UNITY_EDITOR
        EditorApplication.update -= FixedUpdate;
#endif
    }

    // --- Landmark projection ---
    public void ProjectLandmarksOntoModelSpline(int samples = 512)
    {
        if (_modelSpline == null || _modelSpline.nodes == null || _modelSpline.nodes.Count < 2)
            return;

        foreach (var lm in landmarks)
        {
            if (lm == null) continue;

            var localPoint = transform.InverseTransformPoint(lm.position);
            var projection = _modelSpline.GetProjectionSample(localPoint);

            // cumulative distance = sum(length of all previous curves) + distance on this curve
            float offset = 0f;
            var curves = _modelSpline.curves;
            for (int i = 0; i < curves.Count; i++)
            {
                var c = curves[i];
                if (c == projection.curve) break;
                offset += c.Length;
            }
            lm.projectedDistance = Mathf.Clamp(offset + projection.distanceInCurve, 0f, Mathf.Max(0.0001f, modelSplineLength));
        }
    }

    private void OnDrawGizmos()
    {
        if (Waypoints != null && Waypoints.Count > 0)
        {
            Gizmos.color = gizmoColor;
            Vector3 prev = Vector3.zero;
            bool havePrev = false;

            foreach (var p in Waypoints)
            {
                Gizmos.DrawCube(p, Vector3.one * cubeSize);

                if (drawConnections && havePrev)
                    Gizmos.DrawLine(prev, p);

                prev = p;
                havePrev = true;
            }
        }

        // Draw landmarks (editor aid)
        Gizmos.color = Color.cyan;
        if (landmarks != null)
        {
            foreach (var lm in landmarks)
            {
                Gizmos.DrawSphere(lm.position, cubeSize * 1.2f);
            }
        }
    }
}
