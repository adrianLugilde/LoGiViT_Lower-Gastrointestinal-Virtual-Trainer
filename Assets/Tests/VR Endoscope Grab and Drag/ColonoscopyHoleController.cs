using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColonoscopyHoleController : MonoBehaviour
{
    public GameObject endoscopeGo;
    private Bounds holeBounds;
    private bool isEndoscopeInside;
    public float depth = 0;
    void Start()
    {
        holeBounds = transform.parent.GetComponent<MeshRenderer>().bounds;
    }

    // Update is called once per frame
    void Update()
    {
        if (isEndoscopeInside)
        {
            // Measure how far the object has moved inside
            depth = CalculateDepth();
            Debug.Log("Object depth inside the cylinder: " + depth);
        }
    }

    private float CalculateDepth()
    {
        // Assume the cylinder's open end is at the entrancePosition (Z-axis is forward)
        // Adjust the axis calculation depending on your cylinder's orientation
        float entranceZ = transform.position.z + (transform.localScale.z / 2); // Adjust as needed for your setup
        float objectZ = endoscopeGo.transform.position.z;
        return entranceZ - objectZ;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("OnTriggerEnter");
        if (other.gameObject == endoscopeGo)
        {
            isEndoscopeInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("OnTriggerExit");
        if (other.gameObject == endoscopeGo)
        {
            if (!holeBounds.Contains(endoscopeGo.transform.position))
            {
                isEndoscopeInside = false;
                depth = 0;
            }
        }
    }
}
