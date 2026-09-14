using GeometryUtils;
using SplineMesh;
using UnityEngine;

public class GrowingSplineEndoscope : MonoBehaviour
{
    public Spline targetSpline;
    public float rate = 0.01f;
    private CustomMeshBender meshBender;

    public Mesh segmentMesh;
    public Material material;
    public Vector3 rotation;
    public Vector3 scale;
    public Camera endoscopeCamera;

    public void SetRate(float newRate)
    {
        if(newRate == 0) newRate = 0.01f;
        rate = newRate;
        Contort();
    }

    private void Contort()
    {
        meshBender.SetInterval(targetSpline, 0, rate);
        meshBender.ComputeIfNeeded();
    }

    public void Init()
    {
        meshBender = gameObject.AddComponent<CustomMeshBender>();
        meshBender.useSkinnedMeshRenderer = false;
        meshBender.SetInterval(targetSpline, 0, 0.01f);
        var meshData = new MeshData(segmentMesh,
            Vector3.zero,
            Quaternion.Euler(rotation),
            scale,
            false);
        meshData.BuildData();
        meshBender.MeshData = meshData;
        meshBender.Configure();
        GetComponent<MeshRenderer>().sharedMaterials = new Material[] { material };
        SetRate(0.01f);
    }
}
