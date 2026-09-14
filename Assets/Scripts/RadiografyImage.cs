using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadiografyImage : MonoBehaviour
{
    // Start is called before the first frame update
    private RawImage image;
    public bool updateImage = false;

    void Start()
    {
        image = GetComponent<RawImage>();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!updateImage) return;
        Debug.Log("onrenderimage");
        image.texture = source;
        updateImage = !updateImage;
    }
}
