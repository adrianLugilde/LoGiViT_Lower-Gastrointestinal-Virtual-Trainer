using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
[ExecuteAlways]
#endif
public class PerCameraLightExcluder : MonoBehaviour
{
    [Tooltip("Lights that should NOT BE active when rendering the target cameras.")]
    public List<Light> Lights;
    public LayerMask environmentLayerMaskExcluded;
    private Camera targetCamera;


    void OnEnable()
    {
        targetCamera = transform.GetComponent<Camera>();
        Lights.Clear();
        if (environmentLayerMaskExcluded.value != 0)
        {
            Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light light in allLights)
            {
                if ((environmentLayerMaskExcluded.value & (1 << light.gameObject.layer)) != 0)
                {
                    Lights.Add(light);
                }
            }
        }
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        foreach (Light light in Lights)
        {
            if (cam == targetCamera) light.enabled = false;
        }
    }

    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        foreach (Light light in Lights)
        {
            if (cam == targetCamera) light.enabled = true;
        }
    }


}
