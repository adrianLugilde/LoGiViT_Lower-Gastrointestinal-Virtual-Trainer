using UnityEngine;
using g4;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MeshDifference : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] Mesh colonMesh;
    [SerializeField] Vector3 cubeCenter = Vector3.zero;
    [SerializeField] Vector3 cubeSize = new Vector3(0.3f, 0.3f, 0.3f);

    [Header("SDF / Meshing")]
    [Range(64, 512)] public int sdfResolution = 192;
    [Range(64, 512)] public int mcResolution = 192;

    [Header("Gizmos")]
    [SerializeField] bool drawGizmos = true;
    [SerializeField] bool drawSolid = true;
    [SerializeField] Color gizmoColor = new Color(0.2f, 0.6f, 1f, 0.15f);
    [SerializeField] Color wireColor = new Color(0.2f, 0.6f, 1f, 1f);
    [Tooltip("If enabled, cubeCenter/Size are treated as LOCAL to this transform.")]
    [SerializeField] bool cubeInLocalSpace = false;

    public void CreateMesh()
    {
        // Unity Mesh -> DMesh3
        var dmesh = G4Utils.UnityMeshToDMesh(colonMesh);

        // Colon SDF
        var bb = dmesh.CachedBounds;
        double cell = bb.MaxDim / sdfResolution;
        var grid = new MeshSignedDistanceGrid(dmesh, cell)
        {
            NarrowBandMaxDistance = cubeSize.magnitude
        };
        grid.Compute();

        var colonImplicit = new DenseGridTrilinearImplicit(grid.Grid, grid.GridOrigin, grid.CellSize);

        // Axis-aligned “cube” SDF
        var half = 0.5 * (Vector3d)cubeSize;
        Vector3d centerWS = cubeInLocalSpace ? (Vector3d)transform.TransformPoint(cubeCenter) : (Vector3d)cubeCenter;
        var aabb = new AxisAlignedBox3d(centerWS - half, centerWS + half);
        var cubeImplicit = new ImplicitAxisAlignedBox3d() { AABox = aabb };

        // Difference: Cube - Colon
        var diff = new ImplicitDifference3d() { A = cubeImplicit, B = colonImplicit };

        // Marching Cubes inside the cube bounds
        var mc = new MarchingCubes
        {
            Implicit = diff,
            Bounds = aabb,                      // directly use the AA bounds
            CubeSize = aabb.MaxDim / mcResolution
        };
        mc.Bounds.Expand(3 * mc.CubeSize);
        mc.Generate();
        var result = mc.Mesh;

        // Push to Unity
        var go = new GameObject("AABBMinusColon", typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = G4Utils.DMeshToUnityMesh(result);
    }

    // --------- Gizmos ----------
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Choose matrix based on space setting
        var prevMatrix = Gizmos.matrix;
        if (cubeInLocalSpace)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
        }
        else
        {
            Gizmos.matrix = Matrix4x4.identity;
        }

        // Draw solid (semi-transparent) and wire cube
        if (drawSolid)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(cubeCenter, cubeSize);
        }

        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(cubeCenter, cubeSize);

        // restore
        Gizmos.matrix = prevMatrix;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(MeshDifference))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var myScript = (MeshDifference)target;
            if (GUILayout.Button("Get Mesh difference"))
            {
                myScript.CreateMesh();
            }
        }
    }
#endif
}
