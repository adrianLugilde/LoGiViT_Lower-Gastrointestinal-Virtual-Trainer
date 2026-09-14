using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.Formats.Fbx.Exporter;
#endif

public class ExportMeshToFBX : MonoBehaviour
{
    public string fileName = "LargeIntestine.fbx";

    public void Export()
    {
#if UNITY_EDITOR
        string path = Application.dataPath + "/" + fileName;
        GameObject obj = this.gameObject;

        ModelExporter.ExportObject(path, obj);

        Debug.Log("Exported mesh to " + path);
#else
        Debug.LogError("FBX export is only available in the Unity Editor.");
#endif
    }
}