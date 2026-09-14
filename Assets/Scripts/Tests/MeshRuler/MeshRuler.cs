#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;

public class MeshRuler : MonoBehaviour
{
    public SkinnedMeshRenderer targetMesh; // The target mesh to measure
    public float rulerMargin = 0.05f; // Margin around the mesh in meters
    public float subdivisionInterval = 0.005f; // Interval for subdivisions in meters, default 0.5 cm
    public float lineWidth = 0.002f; // Width of the ruler lines in meters
    public Material lineMaterial; // Material for the LineRenderer
    public TMP_FontAsset textFont; // Font asset for the TextMeshPro labels
    public Color textColor = Color.black; // Color for the text labels
    public float textSize = 0.02f; // Size of the text labels in meters

    private void Start()
    {
        if (targetMesh == null || lineMaterial == null || textFont == null)
        {
            Debug.LogError("Target mesh, line material, or text font is not assigned.");
            return;
        }

        GenerateRulerForMesh();
    }

    public void GenerateRulerForMesh()
    {
        Vector3 meshSize = CalculateMeshWorldSize(targetMesh);
        Vector3 meshPosition = CalculateMeshWorldPosition(targetMesh);

        // Set ruler range relative to the mesh size and add a small margin
        float margin = 0.02f; // 2 cm margin
        Vector2 xAxisRange = new Vector2(0f, meshSize.x + margin);
        Vector2 yAxisRange = new Vector2(0f, meshSize.y + margin);

        // Scale the line width based on the mesh size
        float baseLineWidth = 0.01f; // Base line width for a 1-meter object
        lineWidth = baseLineWidth * Mathf.Min(meshSize.x, meshSize.y);

        // Create X-Axis Ruler
        CreateAxis("X-Axis", xAxisRange, Vector3.right, Vector3.down,
                   new Vector3(meshPosition.x - meshSize.x / 2f, meshPosition.y - meshSize.y / 2f, 0f));

        // Create Y-Axis Ruler
        CreateAxis("Y-Axis", yAxisRange, Vector3.up, Vector3.left,
                   new Vector3(meshPosition.x - meshSize.x / 2f, meshPosition.y - meshSize.y / 2f, 0f));
    }

    private void CreateAxis(string axisName, Vector2 range, Vector3 mainDirection, Vector3 labelOffsetDirection, Vector3 axisPosition)
    {
        GameObject axis = new GameObject(axisName);
        axis.transform.parent = transform;
        axis.transform.localPosition = axisPosition; // Position at the bounds
        axis.transform.localScale = Vector3.one; // No scaling needed
        axis.layer = gameObject.layer;

        LineRenderer axisLine = axis.AddComponent<LineRenderer>();
        axisLine.useWorldSpace = false;
        axisLine.positionCount = 2;
        axisLine.SetPosition(0, range.x * mainDirection); // Position in meters
        axisLine.SetPosition(1, range.y * mainDirection); // Position in meters
        axisLine.startWidth = lineWidth; // Width in meters
        axisLine.endWidth = lineWidth; // Width in meters
        axisLine.material = lineMaterial;

        axis.transform.localRotation = Quaternion.identity;

        for (float i = range.x; i <= range.y; i += subdivisionInterval)
        {
            if (axisName == "X-Axis" && i == 0)
            {
                continue;
            }
            CreateTick(axis, i, mainDirection, labelOffsetDirection);
        }
    }

    private void CreateTick(GameObject parentAxis, float position, Vector3 mainDirection, Vector3 perpendicularDirection)
    {
        GameObject tick = new GameObject(parentAxis.name + "_Tick_" + position);
        tick.transform.parent = parentAxis.transform;
        tick.transform.localPosition = Vector3.zero;
        tick.transform.localScale = Vector3.one;
        tick.transform.localRotation = Quaternion.identity;
        tick.layer = gameObject.layer;

        LineRenderer tickLine = tick.AddComponent<LineRenderer>();
        tickLine.useWorldSpace = false;

        float tickSizeMultiplier = 1f; // Adjust the relative size of ticks

        if (parentAxis.name == "X-Axis")
        {
            Vector3 tickStart = new Vector3(position, -lineWidth * tickSizeMultiplier, 0); // In meters
            Vector3 tickEnd = new Vector3(position, lineWidth * tickSizeMultiplier, 0); // In meters
            tickLine.positionCount = 2;
            tickLine.SetPosition(0, tickStart);
            tickLine.SetPosition(1, tickEnd);
        }
        else if (parentAxis.name == "Y-Axis")
        {
            Vector3 tickStart = new Vector3(-lineWidth * tickSizeMultiplier, position, 0); // In meters
            Vector3 tickEnd = new Vector3(lineWidth * tickSizeMultiplier, position, 0); // In meters
            tickLine.positionCount = 2;
            tickLine.SetPosition(0, tickStart);
            tickLine.SetPosition(1, tickEnd);
        }

        tickLine.startWidth = lineWidth; // Width in meters
        tickLine.endWidth = lineWidth; // Width in meters
        tickLine.material = lineMaterial;

        CreateTextLabel(tick, position, mainDirection, perpendicularDirection);
    }

    private void CreateTextLabel(GameObject parentTick, float position, Vector3 mainDirection, Vector3 labelOffsetDirection)
    {
        GameObject textLabel = new GameObject(parentTick.name + "_Label_" + position);
        textLabel.transform.parent = parentTick.transform;
        textLabel.transform.localPosition = Vector3.zero;
        textLabel.transform.localScale = Vector3.one;
        textLabel.transform.localRotation = Quaternion.identity;
        textLabel.layer = gameObject.layer;

        TextMeshPro textMeshPro = textLabel.AddComponent<TextMeshPro>();
        textMeshPro.fontSize = textSize * 100f; // Font size in meters
        textMeshPro.color = textColor;
        textMeshPro.font = textFont;
        textMeshPro.alignment = TextAlignmentOptions.Center;

        if (position == 0)
        {
            textMeshPro.text = "0 cm"; // Label at the origin with "cm"
        }
        else
        {
            textMeshPro.text = (position * 100).ToString("0.0"); // Measurement value in cm
        }

        // Adjust the position of the text label based on the axis
        if (parentTick.transform.parent.name == "X-Axis")
        {
            textLabel.transform.localPosition = new Vector3(position, -lineWidth * 2f, 0); // Position below the tick
        }
        else if (parentTick.transform.parent.name == "Y-Axis")
        {
            textLabel.transform.localPosition = new Vector3(-lineWidth * 2f, position, 0); // Position left of the tick
        }

        textLabel.transform.localScale = Vector3.one * textSize; // Text size in meters
    }

    private Vector3 CalculateMeshWorldSize(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        // Ensure that SkinnedMeshRenderer has a valid mesh
        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        if (mesh != null)
        {
            Bounds bounds = mesh.bounds;

            // Calculate the world bounds of the mesh
            Matrix4x4 worldMatrix = skinnedMeshRenderer.transform.localToWorldMatrix;
            Vector3[] corners = new Vector3[8];
            corners[0] = worldMatrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.min.y, bounds.min.z));
            corners[1] = worldMatrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));
            corners[2] = worldMatrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));
            corners[3] = worldMatrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.max.y, bounds.max.z));
            corners[4] = worldMatrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
            corners[5] = worldMatrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
            corners[6] = worldMatrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
            corners[7] = worldMatrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.max.y, bounds.max.z));

            // Calculate the world size by finding the extents of the corners
            Vector3 min = corners[0];
            Vector3 max = corners[0];
            foreach (Vector3 corner in corners)
            {
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }

            // World size in meters
            Vector3 sizeInMeters = max - min;
            Debug.Log(sizeInMeters);
            return sizeInMeters;
        }
        else
        {
            Debug.LogError("Selected SkinnedMeshRenderer has no mesh.");
            return Vector3.zero; // Return zero if no mesh is found
        }
    }

    private Vector3 CalculateMeshWorldPosition(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        // Ensure that SkinnedMeshRenderer has a valid mesh
        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        if (mesh != null)
        {
            Bounds bounds = mesh.bounds;

            // Calculate the world position of the mesh bounds' center
            Vector3 worldPosition = skinnedMeshRenderer.transform.TransformPoint(bounds.center);

            return worldPosition;
        }
        else
        {
            Debug.LogError("Selected SkinnedMeshRenderer has no mesh.");
            return Vector3.zero; // Return zero if no mesh is found
        }
    }

    public void ClearRuler()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }
}

// Custom Editor to add buttons in the Inspector
[CustomEditor(typeof(MeshRuler))]
public class MeshRulerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MeshRuler rulerGenerator = (MeshRuler)target;

        if (GUILayout.Button("Generate Ruler"))
        {
            rulerGenerator.GenerateRulerForMesh();
        }

        if (GUILayout.Button("Clear Ruler"))
        {
            rulerGenerator.ClearRuler();
        }
    }
}
#endif