using UnityEngine;

[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]


public class XRPolypInteractable : MonoBehaviour
{
    public Polyp Polyp;
    public Mesh polypMesh;

    public void ToggleInteractable(bool status)
    {
        enabled = status;
    }
}