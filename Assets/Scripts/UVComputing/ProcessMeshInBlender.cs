using UnityEngine;
using System.Diagnostics;

public class ProcessMeshInBlender : MonoBehaviour
{
    public string blenderPath = "C:\\Program Files\\Blender Foundation\\Blender 4.0\\blender.exe";
    public string pythonScriptPath = "";
    public string inputFBXPath = "";
    public string outputFBXPath = "";

    public void ProcessMesh()
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = blenderPath,
            Arguments = $"-b -P \"{pythonScriptPath}\" -- \"{inputFBXPath}\" \"{outputFBXPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(error))
            {
                UnityEngine.Debug.LogError(error);
            }
            else
            {
                UnityEngine.Debug.Log(output);
                UnityEngine.Debug.Log("Mesh processed in Blender");
            }
        }
    }
}
