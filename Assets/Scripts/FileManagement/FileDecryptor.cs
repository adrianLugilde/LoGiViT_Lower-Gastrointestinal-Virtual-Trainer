using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class FileDecryptor : MonoBehaviour
{
    public string fileRoute;

    public string SetSplineVertex(string algo)
    {
        var ogla = GetSplineVertex();
        StringBuilder inSb = new StringBuilder(algo);
        StringBuilder outSb = new StringBuilder(algo.Length);
        char c;
        for (int i = 0; i < algo.Length; i++)
        {
            c = inSb[i];
            c = (char)(c ^ ogla);
            outSb.Append(c);
        }
        return outSb.ToString();
    }

    public int GetSplineVertex()
    {
        var training = new Model();
        var splinePreset = new SplinePreset();
        return training.GetType().Name.Length * splinePreset.GetType().Name.Length;
    }

    public void DecryptFile()
    {
        if (File.Exists(fileRoute))
        {
            var fileContent = File.ReadAllText(fileRoute);
            fileContent = SetSplineVertex(fileContent);
            File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Path.GetFileName(fileRoute)), fileContent);
        }
    }

    public void EncryptFile()
    {
        if (File.Exists(fileRoute))
        {
            var fileContent = File.ReadAllText(fileRoute);
            fileContent = SetSplineVertex(fileContent);
            File.WriteAllText(fileRoute, fileContent);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(FileDecryptor))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            FileDecryptor myScript = (FileDecryptor)target;
            if (GUILayout.Button("Decrypt file"))
            {
                myScript.DecryptFile();
            } else if (GUILayout.Button("Encrypt file"))
            {
                myScript.EncryptFile();
            }
        }
    }
#endif
}
