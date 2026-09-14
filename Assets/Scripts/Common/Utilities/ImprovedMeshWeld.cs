using g4;
using GeometryUtils;
using gs;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ImprovedMeshWeld : MonoBehaviour
{
    public SkinnedMeshRenderer myWeldedMeshRenderer;
    public int[] rectumEdgeVertices = new int[]
    {
        362,361,363,365,369,372,376,382,385,388,391,394,396,
        399,402,405,408,412,413,415,417,1226,1225,1227,1229,
        1234,1239,1244,1250,1257,1264,1268,1270,1273,1277,
        1279,1282,1285,1289,1290,1292,1294,558,557,559,561,
        566,571,576,580,583,586,589,592,595,598,600,604,608,
        610,611,613,615
    };
    public GameObject meshHolder;
    public Mesh combinedMesh;
    public int takeInt = 100;
    private void CombineTest()
    {
        var mfs = meshHolder.GetComponentsInChildren<MeshFilter>();
        var meshes = mfs.Select(mf => mf.sharedMesh).ToList();
        combinedMesh = MeshUtils.CombineMeshes(meshes, false);
        //NormalSolver.AdvancedRecalculateNormals(combinedMesh, 35, true);
        MeshUtils.CreateGOFromMesh(combinedMesh, "CombinedMesh");
        //MeshVerticesUtils.DrawVerticesAsSpheres(combinedMesh.vertices.Skip(takeInt), 0.01f);
    }

    private void WeldTest()
    {
        //var dMesh = G4Utils.UnityMeshToDMesh(combinedMesh);
        var vertices = combinedMesh.vertices;
        var normals = combinedMesh.normals;
        var tangents = combinedMesh.tangents;
        var uvs = combinedMesh.uv;
        var vertexCount = vertices.Length;
        var triangles = combinedMesh.triangles;
        var triangleCount = triangles.Length;
        DMesh3 dMesh = new DMesh3(MeshComponents.VertexNormals | MeshComponents.VertexUVs);
        for (int i = 0; i < vertexCount; i++)
        {
            dMesh.AppendVertex(new NewVertexInfo(vertices[i], normals[i], uvs[i]));
        }
        for (int i = 0; i < triangleCount; i+=3) 
        {
            dMesh.AppendTriangle(new Index3i(triangles[i], triangles[i + 1], triangles[i + 2]));
        }
        MergeCoincidentEdges mce = new MergeCoincidentEdges(dMesh);
        mce.Apply();
        var cdMesh = new DMesh3(mce.Mesh, true);
        var ndMesh = G4Utils.DMeshToUnityMesh(cdMesh, false);
        ndMesh.RecalculateUVDistributionMetrics(); ;
        MeshUtils.CreateGOFromMesh(ndMesh, "WeldedMesh");
    }
    private void MyWeldTest()
    {
        var weldedMesh = myWeldedMeshRenderer.sharedMesh;
        var dMesh = G4Utils.UnityMeshToDMesh(weldedMesh);
        MeshExtrudeMesh meshExtrudeMesh = new MeshExtrudeMesh(dMesh);
        meshExtrudeMesh.Extrude();
        MeshUtils.CreateGOFromMesh(G4Utils.DMeshToUnityMesh(meshExtrudeMesh.Mesh, false), "ExtrudedMesh");
    }

    private void RepairTest()
    {
        var dMesh = G4Utils.UnityMeshToDMesh(combinedMesh);
        MeshAutoRepair mar = new MeshAutoRepair(dMesh);
        mar.Apply();
        MeshUtils.CreateGOFromMesh(G4Utils.DMeshToUnityMesh(mar.Mesh, false), "RepairedMesh");
    }

    private void FillHoleTest()
    {
        var dMesh = G4Utils.UnityMeshToDMesh(combinedMesh);
        SimpleHoleFiller shf = new SimpleHoleFiller(dMesh, EdgeLoop.FromVertices(dMesh, rectumEdgeVertices));
        shf.Fill();
        MeshUtils.CreateGOFromMesh(G4Utils.DMeshToUnityMesh(shf.Mesh, false), "ClosedMesh");
    }

    private void SimplifyMesh()
    {
        var weldedMesh = myWeldedMeshRenderer.sharedMesh;
        var dMesh = G4Utils.UnityMeshToDMesh(weldedMesh);
        Debug.LogWarning(dMesh.MaxVertexID);
        MeshUtils.CreateGOFromMesh(G4Utils.DMeshToUnityMesh(dMesh, false), "dMesh");
        Reducer reducer = new Reducer(dMesh);
        reducer.ReduceToVertexCount(50000);
        //reducer.ReduceToTriangleCount(100000);
        //reducer.DoReduce();
        MeshUtils.CreateGOFromMesh(G4Utils.DMeshToUnityMesh(reducer.Mesh, false), "SimplifiedMesh");
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ImprovedMeshWeld))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ImprovedMeshWeld myScript = (ImprovedMeshWeld)target;
            if (GUILayout.Button("MyWeldTest"))
            {
                myScript.WeldTest();
            }
            if (GUILayout.Button("WeldTest"))
            {
                myScript.WeldTest();
            }
            else if (GUILayout.Button("CombineTest"))
            {
                myScript.CombineTest();
            }
            else if (GUILayout.Button("RepairTest"))
            {
                myScript.RepairTest();
            }
            else if (GUILayout.Button("FillHoleTest"))
            {
                myScript.FillHoleTest();
            }
            else if (GUILayout.Button("SimplifyMesh"))
            {
                myScript.SimplifyMesh();
            }
        }
    }
#endif
}
