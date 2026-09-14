#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro; // Import the TextMeshPro namespace

public class TwoDimRuler : MonoBehaviour
{
    public Vector2 xAxisRange = new Vector2(0f, 5f); // X-axis range in centimeters
    public Vector2 yAxisRange = new Vector2(0f, 5f); // Y-axis range in centimeters
    public float subdivisionInterval = 0.5f; // Interval for subdivisions in centimeters
    public float lineWidth = 0.05f; // Width of the ruler lines
    public Material lineMaterial; // Material for the LineRenderer (set this in the Inspector)
    public TMP_FontAsset textFont; // Font asset for the TextMeshPro labels
    public Color textColor = Color.black; // Color for the text labels
    public float textSize = 0.1f; // Size of the text labels
    public Vector3 distanceFromCamera = new Vector3(0, 0, 5f); // Distance from the camera to the ruler

    private void Start()
    {
        if (lineMaterial == null || textFont == null)
        {
            Debug.LogError("Line material or text font is not assigned.");
            return;
        }

        GenerateRuler();
    }

    public void GenerateRuler()
    {
        ClearRuler(); // Clear any existing ruler lines

        if (xAxisRange != Vector2.zero)
        {
            // Create X-Axis
            CreateAxis("X-Axis", xAxisRange, Vector3.right, Vector3.back); // Labels below
        }

        if (yAxisRange != Vector2.zero)
        {
            // Create Y-Axis
            CreateAxis("Y-Axis", yAxisRange, Vector3.up, Vector3.left); // Labels to the left
        }

        // Position the ruler at the correct distance from the camera
        PositionRuler();
    }

    private void PositionRuler()
    {
        // Set the ruler's position and rotation
        transform.localPosition = distanceFromCamera;
        transform.localRotation = Quaternion.identity; // Ensure zero rotation
    }

    private void CreateAxis(string axisName, Vector2 range, Vector3 mainDirection, Vector3 labelOffsetDirection)
    {
        GameObject axis = new GameObject(axisName);
        axis.transform.parent = transform;
        axis.transform.localPosition = Vector3.zero;
        axis.transform.localScale = Vector3.one;
        axis.layer = gameObject.layer;

        LineRenderer axisLine = axis.AddComponent<LineRenderer>();
        axisLine.useWorldSpace = false;
        axisLine.positionCount = 2;

        axisLine.SetPosition(0, range.x * mainDirection);
        axisLine.SetPosition(1, range.y * mainDirection);
        axisLine.startWidth = lineWidth * transform.localScale.x; // Scale width
        axisLine.endWidth = lineWidth * transform.localScale.x;
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

        float tickSizeMultiplier = 20f; // This controls the relative size of ticks

        Vector3 scaledDirection = perpendicularDirection * transform.localScale.y; // Apply scale

        if (parentAxis.name == "X-Axis")
        {
            Vector3 tickStart = new Vector3(position, -lineWidth * tickSizeMultiplier, 0);
            Vector3 tickEnd = new Vector3(position, lineWidth * tickSizeMultiplier, 0);
            tickLine.positionCount = 2;
            tickLine.SetPosition(0, tickStart);
            tickLine.SetPosition(1, tickEnd);
        }
        else if (parentAxis.name == "Y-Axis")
        {
            Vector3 tickStart = new Vector3(-lineWidth * tickSizeMultiplier, position, 0);
            Vector3 tickEnd = new Vector3(lineWidth * tickSizeMultiplier, position, 0);
            tickLine.positionCount = 2;
            tickLine.SetPosition(0, tickStart);
            tickLine.SetPosition(1, tickEnd);
        }

        tickLine.startWidth = lineWidth * transform.localScale.x; // Scale width
        tickLine.endWidth = lineWidth * transform.localScale.x;
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
        textMeshPro.text = position.ToString("0.0") + " cm";
        textMeshPro.fontSize = textSize * 100; // Set font size
        textMeshPro.color = textColor;
        textMeshPro.font = textFont;
        textMeshPro.alignment = TextAlignmentOptions.Center;

        Vector3 scaledOffset = labelOffsetDirection * transform.localScale.x; // Apply scale to offset

        if (parentTick.transform.parent.name == "X-Axis")
        {
            textLabel.transform.localPosition = (position * mainDirection) + scaledOffset;
        }
        else if (parentTick.transform.parent.name == "Y-Axis")
        {
            textLabel.transform.localPosition = (position * mainDirection) + scaledOffset;
        }

        textLabel.transform.localRotation = Quaternion.identity;
        textLabel.transform.localScale = Vector3.one * textSize; // Scale text size
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
[CustomEditor(typeof(TwoDimRuler))]
public class TwoDimRulerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TwoDimRuler rulerGenerator = (TwoDimRuler)target;

        if (GUILayout.Button("Generate Ruler"))
        {
            rulerGenerator.GenerateRuler();
        }

        if (GUILayout.Button("Clear Ruler"))
        {
            rulerGenerator.ClearRuler();
        }
    }
}
#endif