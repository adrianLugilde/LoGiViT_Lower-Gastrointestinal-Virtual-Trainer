using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public class CameraLightsMasking : MonoBehaviour
{
    public List<Light> Lights;
    private Camera currentCam;

    private void OnEnable()
    {
        currentCam = transform.GetComponent<Camera>();
        RenderPipelineManager.beginCameraRendering += RenderPipelineManager_beginCameraRendering;
        RenderPipelineManager.endCameraRendering += RenderPipelineManager_endCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= RenderPipelineManager_beginCameraRendering;
        RenderPipelineManager.endCameraRendering -= RenderPipelineManager_endCameraRendering;
    }

    private void RenderPipelineManager_beginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        foreach (Light light in Lights)
        {
            if (camera == currentCam) light.enabled = false;
        }
    }

    private void RenderPipelineManager_endCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        foreach (Light light in Lights)
        {
            if (camera == currentCam) light.enabled = true;
        }
    }

    public void addLight(Light light)
    {
        Lights.Add(light);
    }

    public void removeLight(Light light)
    {
        Lights.Remove(light);
    }

}