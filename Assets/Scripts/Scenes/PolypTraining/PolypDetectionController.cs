using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Threading;
using System;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using System.Linq;
using Messages;
using GeometryUtils;

namespace PolypTraining
{

    public class PolypDetectionController : MonoBehaviour
    {
        [SerializeField] private Transform _polypInteratablesHolderTransform;
        [SerializeField] private Camera _eyeViewCamera;
        [SerializeField] private Light _cameraLight;
        [SerializeField] private float _eyeVisionAngle = 70f;
        [SerializeField] private float _partialDetectionValueThreshold = 0.45f;
        [SerializeField] private float _correctDetectionValueThreshold = 0.75f;
        public int TotalPolypCount { get; private set; } = 0;
        public int DetectedPolypCount { get; private set; } = 0;
        public List<XRPolypInteractable> PolypInteractables { get; private set; }
        [SerializeField] private Material _discoveredPolypMaterial;
        [SerializeField] private Material _undiscoveredPolypMaterial;
        [SerializeField] public List<XRPolypInteractable> _detectedPolypInteractables;
        [SerializeField] private string _polypInteractablesLayerName = "ColonModel";
        private SkinnedMeshRenderer _modelRenderer;
        private Transform _eyeViewCameraTransform;
        public event Action<List<Polyp>> OnNewPolypsDetected;
        public event Action OnPolypInvalidDetection;

        private void Awake()
        {
            PolypInteractables = new List<XRPolypInteractable>();
            _detectedPolypInteractables = new List<XRPolypInteractable>();
        }

        private void Start()
        {
            _eyeViewCameraTransform = _eyeViewCamera.transform;
        }

        public virtual void Reset()
        {
            DetectedPolypCount = TotalPolypCount = 0;
            for (int i = 0; i < PolypInteractables.Count; i++)
            {
                var pi = PolypInteractables[i];
                Destroy(pi.gameObject);
            }
            PolypInteractables.Clear();
            _detectedPolypInteractables.Clear();
        }

        public void SetUpPolypMeshes(List<Disease> diseases, SkinnedMeshRenderer modelRenderer)
        {
            _modelRenderer = modelRenderer;
            var polypMeshes = MeshUtils.GetSubmeshesAsMeshes(modelRenderer.sharedMesh, new List<int>() { 0 });
            TotalPolypCount = polypMeshes.Count;
            for (int i = 0; i < TotalPolypCount; i++)
            {
                var go = CommonUtils.Create("Polyp mesh " + i, _polypInteractablesLayerName, _polypInteratablesHolderTransform.gameObject);
                var mesh = polypMeshes[i];
                AddPolypInteractable(go, mesh, diseases.First(d => d.SubmeshIndex == i + 1));
            }
        }

        private void AddPolypInteractable(GameObject targetGo, Mesh mesh, Disease disease)
        {
            var dpi = targetGo.AddComponent<XRPolypInteractable>();
            var transformableMeshData = new GeometryUtils.MeshData(mesh,
                               transform.position,
                               transform.rotation,
                               transform.lossyScale, //it has to inherit the globlal scale from the parent transform
                               false);
            transformableMeshData.BuildData();
            mesh = transformableMeshData.GetTransformedMesh();
            dpi.polypMesh = mesh;
            PolypInteractables.Add(dpi);
            dpi.ToggleInteractable(false);
            dpi.Polyp = (Polyp)disease;
        }

        public bool WasPolypDetected(Polyp polyp)
        {
            return _detectedPolypInteractables.Any(dpi => dpi.Polyp == polyp);
        }

        public void ShowUndetectedPolyps()
        {
            var undetectedPolyps = PolypInteractables.Except(_detectedPolypInteractables);
            foreach (var undetectedPolyp in undetectedPolyps)
            {
                var modelSubmeshIdx = PolypInteractables.IndexOf(undetectedPolyp) + 1;
                Debug.LogWarning($"Undetected polyp {undetectedPolyp.gameObject.name}");
                var sharedMaterials = _modelRenderer.sharedMaterials;
                sharedMaterials[modelSubmeshIdx] = _undiscoveredPolypMaterial;
                _modelRenderer.sharedMaterials = sharedMaterials;
            }
        }

        public void TogglePolypInteractables(bool status)
        {
            for (int i = 0; i < PolypInteractables.Count; i++)
            {
                var pi = PolypInteractables[i];
                pi.ToggleInteractable(status);
            }
        }

        public int PolypDetection(out List<Polyp> newlyDetectedPolyps)
        {
            Debug.Log("PolypDetection");
            var newlyDetectedPolypInteractables = new List<XRPolypInteractable>();
            float lightAngle = _cameraLight.spotAngle / 2.5f;
            var detectionValues = new List<float>();
            float4x4 eyeViewCameraMatrix = _eyeViewCamera.projectionMatrix * _eyeViewCamera.worldToCameraMatrix;
            for (int i = 0; i < PolypInteractables.Count; i++)
            {
                var polypInteractable = PolypInteractables[i];
                if (polypInteractable.enabled == false) continue; //if the interactable is disable, it was already detected so we skip it
                var polypMesh = polypInteractable.polypMesh;
                var polypVertices = polypMesh.vertices;
                var polypNormals = polypMesh.normals;
                var vertexCount = polypVertices.Length;

                NativeCounter visibleVerticesNC = new NativeCounter(Allocator.TempJob);
                NativeArray<Vector3> polypVerticesNA = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
                NativeArray<Vector3> polypNormalsNA = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
                polypVerticesNA.CopyFrom(polypVertices);
                polypNormalsNA.CopyFrom(polypNormals);

                CheckPolypVisibilityJob checkPolypVisibilityJob = new CheckPolypVisibilityJob
                {
                    vertices = polypVerticesNA,
                    normals = polypNormalsNA,
                    eyeViewCameraMatrix = eyeViewCameraMatrix,
                    cameraPosition = _eyeViewCameraTransform.position,
                    cameraForwardReach = _eyeViewCameraTransform.forward * _eyeViewCamera.farClipPlane,
                    eyeVisionAngle = _eyeVisionAngle,
                    lightAngle = lightAngle,
                    farClipPlaneDistance = _eyeViewCamera.farClipPlane,
                    visibleVerticesCounter = visibleVerticesNC.Count,
                };

                var checkPolypVisibilityJobHandle = checkPolypVisibilityJob.Schedule(vertexCount, 1024);
                checkPolypVisibilityJobHandle.Complete();
                var detectionValue = (float)visibleVerticesNC.CountValue / vertexCount;
                Debug.Log($"Polyp interactable {polypInteractable.gameObject.name} visibility is {detectionValue}");
                detectionValues.Add(detectionValue);
                if ((float)visibleVerticesNC.CountValue / vertexCount >= _correctDetectionValueThreshold) //The polyp is over or equal to the required visibility value so its counted as detected
                {
                    newlyDetectedPolypInteractables.Add(polypInteractable);
                }
                polypVerticesNA.Dispose();
                polypNormalsNA.Dispose();
                visibleVerticesNC.Dispose();
            }
            //if any new polyp has been detected, we update the detected polyps and proceed with identification
            if (newlyDetectedPolypInteractables.Count != 0)
            {
                newlyDetectedPolyps = newlyDetectedPolypInteractables.Select(dpi => dpi.Polyp).ToList();
                UpdateDetectedPolyps(newlyDetectedPolypInteractables);
                return PolypTrainingMessages.PolypDetected;
            }
            else
            {
                newlyDetectedPolyps = null;
                if (detectionValues.Any(v => v >= _partialDetectionValueThreshold))
                {
                    OnPolypInvalidDetection?.Invoke();
                    return PolypTrainingMessages.InvalidPolypDetection;
                }
                else
                {
                    return PolypTrainingMessages.NoPolypsDetected;
                }
            }
        }

        private void UpdateDetectedPolyps(List<XRPolypInteractable> newlyDetectedPolypInteractables)
        {
            _detectedPolypInteractables.AddRange(newlyDetectedPolypInteractables);
            for (int i = 0; i < newlyDetectedPolypInteractables.Count; i++)
            {
                var dpi = newlyDetectedPolypInteractables[i];
                //MeshUtils.CreateGOFromMesh(dpi.polypMesh);
                dpi.enabled = false;
                var modelSubmeshIdx = PolypInteractables.IndexOf(dpi) + 1;
                DetectedPolypCount++;
                Debug.LogWarning($"Detected polyp {dpi.gameObject.name}");
                var sharedMaterials = _modelRenderer.sharedMaterials;
                sharedMaterials[modelSubmeshIdx] = _discoveredPolypMaterial;
                _modelRenderer.sharedMaterials = sharedMaterials;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct CheckPolypVisibilityJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> vertices;
            [ReadOnly] public NativeArray<Vector3> normals;
            [ReadOnly] public float4x4 eyeViewCameraMatrix;
            [ReadOnly] public Vector3 cameraPosition;
            [ReadOnly] public Vector3 cameraForwardReach;
            [ReadOnly] public float eyeVisionAngle;
            [ReadOnly] public float lightAngle;
            [ReadOnly] public float farClipPlaneDistance;
            public NativeCounter.Counter visibleVerticesCounter;
            public void Execute(int index)
            {
                var vertex = vertices[index];
                var normal = normals[index];

                var pos = math.mul(eyeViewCameraMatrix, new float4(vertex.x, vertex.y, vertex.z, 1));
                var screenPos = new float4(pos.x + 1f, pos.y + 1f, pos.z + 1f, 0) / 2f;
                bool isInsideCameraFustrum = pos.z > 0 && pos.z < farClipPlaneDistance && screenPos.x > 0 && screenPos.y > 0 && screenPos.x < 1 && screenPos.y < 1;
                if (isInsideCameraFustrum)
                {
                    //the vertex is inside of the camera view
                    var normalDir = (vertex + normal - (vertex + normal * 2f)).normalized;
                    var vertexDirToCamera = (vertex - cameraPosition).normalized;
                    var angle = math.acos(math.dot(cameraForwardReach, normalDir) / (cameraForwardReach.magnitude * normalDir.magnitude)) * (180 / math.PI);
                    var angleToLight = math.acos(math.dot(cameraForwardReach, vertexDirToCamera) / (cameraForwardReach.magnitude * vertexDirToCamera.magnitude)) * (180 / math.PI);
                    if (angle < eyeVisionAngle && angleToLight < lightAngle)
                    {
                        visibleVerticesCounter.Increment();
                    }
                }
            }
        }

        public struct NativeCounter : IDisposable
        {
            public struct Counter
            {
                [NativeDisableUnsafePtrRestriction] private unsafe int* m_CounterPtr;

                public unsafe Counter(int* counterPtr)
                {
                    m_CounterPtr = counterPtr;
                }

                public unsafe void Increment()
                {
                    Interlocked.Increment(ref *m_CounterPtr);
                }
            }

            private NativeArray<int> m_CounterArray;

            public unsafe NativeCounter(Allocator allocator)
            {
                m_CounterArray = new NativeArray<int>(1, allocator);
            }

            public unsafe Counter Count => new Counter((int*)m_CounterArray.GetUnsafePtr());

            public int CountValue => m_CounterArray[0];

            public void Dispose()
            {
                m_CounterArray.Dispose();
            }
        }
    }
}