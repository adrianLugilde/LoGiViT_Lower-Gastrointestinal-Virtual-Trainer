#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class BlendshapeNormalChecker
{
    [MenuItem("Debug/Check Blendshape Normal Consistency")]
    static void Check()
    {
        var smr = Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>();
        var mesh = smr.sharedMesh;
        var baseNormals = mesh.normals;
        int vc = mesh.vertexCount;
        var dv = new Vector3[vc];
        var dn = new Vector3[vc];
        var dt = new Vector3[vc];

        for (int s = 0; s < mesh.blendShapeCount; s++)
        {
            for (int f = 0; f < mesh.GetBlendShapeFrameCount(s); f++)
            {
                mesh.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                int flips = 0;
                for (int i = 0; i < vc; i++)
                {
                    if (Vector3.Dot(baseNormals[i] + dn[i], baseNormals[i]) < 0)
                        flips++;
                }
                Debug.Log($"{mesh.GetBlendShapeName(s)} frame {f}: {flips}/{vc} vertices flip");
            }
        }
    }
}
#endif
