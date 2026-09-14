using UnityEngine;
using UnityEditor;

public class MeshDimensions : MonoBehaviour
{
    public Vector3 meshScale = Vector3.one; // Public variable for scale, default is no scaling
    public Vector3 dimensionsInMillimeters;

    public void CalculateDimensions()
    {
        // Get the mesh filter component attached to the GameObject
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter != null)
        {
            // Get the bounds of the mesh
            Bounds meshBounds = meshFilter.sharedMesh.bounds;

            // Apply the provided scale to the bounds size
            Vector3 scaledSize = Vector3.Scale(meshBounds.size, meshScale);

            // Conversion factor from Unity units to meters (1 unit = 0.01 meters)
            float conversionFactor = 0.01f;

            // Convert dimensions to millimeters
            dimensionsInMillimeters = scaledSize * (conversionFactor * 1000f);

            Debug.Log("Mesh dimensions in millimeters: " +
                      "Width = " + dimensionsInMillimeters.x +
                      ", Height = " + dimensionsInMillimeters.y +
                      ", Depth = " + dimensionsInMillimeters.z);
        }
        else
        {
            Debug.LogError("MeshFilter component not found on the GameObject.");
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MeshDimensions))]
public class MeshDimensionsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MeshDimensions meshDimensions = (MeshDimensions)target;

        if (GUILayout.Button("Calculate Dimensions"))
        {
            meshDimensions.CalculateDimensions();
        }

        GUILayout.Label("Dimensions in millimeters:");
        GUILayout.Label("Width: " + meshDimensions.dimensionsInMillimeters.x);
        GUILayout.Label("Height: " + meshDimensions.dimensionsInMillimeters.y);
        GUILayout.Label("Depth: " + meshDimensions.dimensionsInMillimeters.z);
    }
}
#endif
