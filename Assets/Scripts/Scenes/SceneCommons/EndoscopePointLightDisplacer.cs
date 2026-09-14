using UnityEngine;

public class EndoscopePointLightDisplacer : MonoBehaviour
{
    public Camera sourceCamera;           // The camera to cast the ray from
    public Transform targetObject;        // The object to move
    public GameObject targetMesh;         // The mesh to detect (can use tag instead)
    [Range(0f, 1f)]
    public float percent = 0.2f;          // X% away from hit point (1 = camera, 0 = hit point)
    public string targetTag = "TargetMesh"; // Tag to identify the target mesh


    private void OnEnable()
    {
        //Debug.Log("EndoscopePointLightDisplacer enabled. Finding target mesh with tag: " + targetTag);
        targetMesh = GameObject.FindGameObjectWithTag(targetTag);
    }

    void Update()
    {
        if (sourceCamera == null || targetObject == null || targetMesh == null)
            return;
        if(sourceCamera.transform.hasChanged == false) return;
        Ray ray = new Ray(sourceCamera.transform.position, sourceCamera.transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject == targetMesh)
            {
                Vector3 cameraPos = sourceCamera.transform.position;
                Vector3 hitPoint = hit.point;
                Vector3 direction = (cameraPos - hitPoint).normalized;
                float distance = Vector3.Distance(cameraPos, hitPoint);

                // Move targetObject to X% away from the hit point toward the camera
                targetObject.position = hitPoint + direction * (distance * percent);
            }
        }
    }
}