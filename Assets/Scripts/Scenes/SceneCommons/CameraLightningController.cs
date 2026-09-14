using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraLightningController : MonoBehaviour
{
    public List<Light> cameraLights = new List<Light>();

    private void OnPreCull()
    {
        var lightCount = cameraLights.Count;
        for (int i = 0; i < lightCount; i++)
        {
            cameraLights[i].enabled = false;
        } 
    }

    private void OnPostRender()
    {
        var lightCount = cameraLights.Count;
        for (int i = 0; i < lightCount; i++)
        {
            cameraLights[i].enabled = true;
        }
    }
}
