#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System;
using System.Linq;
using System.Reflection;


public class MeshProcessor : MonoBehaviour
{
    public string fileName = "LargeIntestine.fbx";
    public string blenderPath = "C:\\Program Files\\Blender Foundation\\Blender 4.0\\blender.exe";
    public string pythonScriptPath = "";

    [ContextMenu("Export Mesh to FBX")]
    public void Export()
    {
#if UNITY_EDITOR
        string path = Path.Combine(Application.dataPath, fileName);
        GameObject obj = this.gameObject;

        //ModelExporter.ExportObject(path, obj);
        ExportBinaryFBX(path, obj);

        UnityEngine.Debug.Log("Exported mesh to " + path);
#else
        Debug.LogError("FBX export is only available in the Unity Editor.");
#endif
    }

    private static void ExportBinaryFBX(string filePath, UnityEngine.Object singleObject)
    {
        // Find relevant internal types in Unity.Formats.Fbx.Editor assembly
        Type[] types = AppDomain.CurrentDomain.GetAssemblies().First(x => x.FullName == "Unity.Formats.Fbx.Editor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null").GetTypes();
        Type optionsInterfaceType = types.First(x => x.Name == "IExportOptions");
        Type optionsType = types.First(x => x.Name == "ExportOptionsSettingsSerializeBase");

        // Instantiate a settings object instance
        MethodInfo optionsProperty = typeof(ModelExporter).GetProperty("DefaultOptions", BindingFlags.Static | BindingFlags.NonPublic).GetGetMethod(true);
        object optionsInstance = optionsProperty.Invoke(null, null);

        // Change the export setting from ASCII to binary
        FieldInfo exportFormatField = optionsType.GetField("exportFormat", BindingFlags.Instance | BindingFlags.NonPublic);
        exportFormatField.SetValue(optionsInstance, 1);

        // Invoke the ExportObject method with the settings param
        MethodInfo exportObjectMethod = typeof(ModelExporter).GetMethod("ExportObject", BindingFlags.Static | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(string), typeof(UnityEngine.Object), optionsInterfaceType }, null);
        exportObjectMethod.Invoke(null, new object[] { filePath, singleObject, optionsInstance });
    }

    [ContextMenu("Process Mesh in Blender")]
    public void ProcessMesh()
    {
        string inputFBXPath = Path.Combine(Application.dataPath, fileName);
        string outputFBXPath = Path.Combine(Application.dataPath, "Processed_" + fileName);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = blenderPath,
            Arguments = $"-b -P \"{pythonScriptPath}\" -- \"{inputFBXPath}\" \"{outputFBXPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        UnityEngine.Debug.Log(startInfo.Arguments);

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(error))
            {
                UnityEngine.Debug.LogError("Blender error: " + error);
            }
            else
            {
                UnityEngine.Debug.Log("Blender output: " + output);
                UnityEngine.Debug.Log("Mesh processed in Blender");
            }
        }
    }

    [ContextMenu("Import Processed Mesh")]
    public void Import()
    {
        string processedFBXPath = Path.Combine(Application.dataPath, "Processed_" + fileName);

        if (File.Exists(processedFBXPath))
        {
            string assetPath = "Assets/Processed_" + fileName;
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
                    UnityEngine.Debug.Log("Imported processed mesh with new UVs");
                }
                else
                {
                    UnityEngine.Debug.LogError("MeshFilter component missing!");
                }
            }
            else
            {
                UnityEngine.Debug.LogError("Failed to load the processed FBX file.");
            }
        }
        else
        {
            UnityEngine.Debug.LogError("Processed FBX file not found!");
        }
    }
}

[CustomEditor(typeof(MeshProcessor))]
public class MeshProcessorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MeshProcessor script = (MeshProcessor)target;
        if (GUILayout.Button("Export Mesh to FBX"))
        {
            script.Export();
        }

        if (GUILayout.Button("Process Mesh in Blender"))
        {
            script.ProcessMesh();
        }

        if (GUILayout.Button("Import Processed Mesh"))
        {
            script.Import();
        }
    }
}
#endif