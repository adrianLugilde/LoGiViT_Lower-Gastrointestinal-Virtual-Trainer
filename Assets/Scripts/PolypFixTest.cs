using UnityEditor;
using UnityEngine;

public class PolypFixTest : MonoBehaviour
{
    public SkinnedMeshRenderer targetSmr;

    void TestFix()
    {
        if (targetSmr != null)
        {
            Debug.Log("Baking mesh for target SkinnedMeshRenderer.");
            var mesh = new Mesh();
            targetSmr.BakeMesh(mesh);
            targetSmr.sharedMesh = mesh;
        }
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(PolypFixTest))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PolypFixTest myScript = (PolypFixTest)target;
            if (GUILayout.Button("TestFix"))
            {
                myScript.TestFix();
            }

        }
    }
#endif
}