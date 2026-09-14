using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshRotator : MonoBehaviour
{
    public float rotationSpeed = 50f;
    public RotationAxis rotationAxis = RotationAxis.y;
    public Space space = Space.World;
    private Transform targetTransform;

    private void Start()
    {
        if (GetComponent<MeshRenderer>() == null && GetComponent<SkinnedMeshRenderer>() == null)
        {
            targetTransform = transform.GetChild(1).transform;
        } else
        {
            targetTransform = this.transform;
        }
    }

    void Update()
    {
        switch(rotationAxis)
        {
            case RotationAxis.x:
                targetTransform.Rotate(new Vector3(1f * rotationSpeed, 0, 0) * Time.deltaTime, space);
                break;
            case RotationAxis.y:
                targetTransform.Rotate(new Vector3(0, 1f * rotationSpeed, 0) * Time.deltaTime, space);
                break;
            case RotationAxis.z:
                targetTransform.Rotate(new Vector3(0, 0, 1f * rotationSpeed) * Time.deltaTime, space);
                break;
        }
    }

    public enum RotationAxis
    {
        x = 0,
        y = 1,
        z = 2,
    }
}
