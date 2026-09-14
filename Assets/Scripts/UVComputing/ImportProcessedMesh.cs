#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class ImportProcessedMesh : MonoBehaviour
{
    public string processedFBXPath = "LargeIntestine_with_UVs.fbx";

    public void Import()
    {
        if (File.Exists(processedFBXPath))
        {
            string assetPath = "Assets/" + Path.GetFileName(processedFBXPath);
            File.Copy(processedFBXPath, assetPath, true);
            AssetDatabase.Refresh();

            GameObject importedObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (importedObject != null)
            {
                MeshFilter meshFilter = GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    Mesh newMesh = importedObject.GetComponentInChildren<MeshFilter>().sharedMesh;
                    meshFilter.mesh = newMesh;
                    Debug.Log("Imported processed mesh with new UVs");
                }
                else
                {
                    Debug.LogError("MeshFilter component missing!");
                }
            }
            else
            {
                Debug.LogError("Failed to load the processed FBX file.");
            }
        }
        else
        {
            Debug.LogError("Processed FBX file not found!");
        }
    }
}
#endif