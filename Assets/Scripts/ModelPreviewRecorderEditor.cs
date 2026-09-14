#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ModelPreviewRecorder))]
public class ModelPreviewRecorderEditor : Editor
{
    SerializedProperty navigationProp;
    SerializedProperty fileNameProp;
    SerializedProperty modelPathProp;
    SerializedProperty radiusProp;
    SerializedProperty poleMarginDegProp;
    SerializedProperty pointSpacingProp;
    SerializedProperty loopSpacingProp;
    SerializedProperty drawConnectionsProp;
    SerializedProperty gizmoColorProp;
    SerializedProperty cubeSizeProp;
    SerializedProperty baseSurroundingSplineProp;
    SerializedProperty baseModelSplineProp;
    SerializedProperty surroundingSplineProp;
    SerializedProperty modelSplineProp;
    SerializedProperty cameraHolderProp;
    SerializedProperty cameraRateProp;
    SerializedProperty surroundingCameraRateMultiplierProp;
    SerializedProperty modelCameraRateMultiplierProp;
    SerializedProperty reverseCameraRateMultiplierProp;

    // Reverse look & landmarks
    SerializedProperty lookOrbitDistanceProp;
    SerializedProperty lookOrbitRadiusProp;
    SerializedProperty lookOrbitRevsPerSecondProp;
    SerializedProperty landmarkHoldSecondsProp;
    SerializedProperty approachBlendDistanceProp;
    SerializedProperty stopDistanceProp;
    SerializedProperty landmarksProp;

    readonly string[] extensions = new[] { "*.tc" };

    void OnEnable()
    {
        navigationProp = serializedObject.FindProperty("Navigating");
        fileNameProp = serializedObject.FindProperty("fileName");
        modelPathProp = serializedObject.FindProperty("modelDirectoryPath");
        cameraRateProp = serializedObject.FindProperty("CameraRate");
        surroundingCameraRateMultiplierProp = serializedObject.FindProperty("surroundingCameraRateMultiplier");
        modelCameraRateMultiplierProp = serializedObject.FindProperty("modelCameraRateMultiplier");
        reverseCameraRateMultiplierProp = serializedObject.FindProperty("reverseCameraRateMultiplier");

        radiusProp = serializedObject.FindProperty("radius");
        poleMarginDegProp = serializedObject.FindProperty("poleMarginDeg");
        pointSpacingProp = serializedObject.FindProperty("pointSpacing");
        loopSpacingProp = serializedObject.FindProperty("loopSpacing");

        drawConnectionsProp = serializedObject.FindProperty("drawConnections");
        gizmoColorProp = serializedObject.FindProperty("gizmoColor");
        cubeSizeProp = serializedObject.FindProperty("cubeSize");

        baseSurroundingSplineProp = serializedObject.FindProperty("_baseSurroundingSpline");
        baseModelSplineProp = serializedObject.FindProperty("_baseModelSpline");
        surroundingSplineProp = serializedObject.FindProperty("_surroundingSpline");
        modelSplineProp = serializedObject.FindProperty("_modelSpline");
        cameraHolderProp = serializedObject.FindProperty("_cameraHolder");

        // reverse look & landmarks props
        lookOrbitDistanceProp = serializedObject.FindProperty("lookOrbitDistance");
        lookOrbitRadiusProp = serializedObject.FindProperty("lookOrbitRadius");
        lookOrbitRevsPerSecondProp = serializedObject.FindProperty("lookOrbitRevsPerSecond");
        landmarkHoldSecondsProp = serializedObject.FindProperty("landmarkHoldSeconds");
        approachBlendDistanceProp = serializedObject.FindProperty("approachBlendDistance");
        stopDistanceProp = serializedObject.FindProperty("stopDistance");
        landmarksProp = serializedObject.FindProperty("landmarks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var comp = (ModelPreviewRecorder)target;

        EditorGUILayout.PropertyField(navigationProp);

        EditorGUILayout.LabelField("Spline Templates & Refs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(baseSurroundingSplineProp);
        EditorGUILayout.PropertyField(baseModelSplineProp);
        EditorGUILayout.PropertyField(surroundingSplineProp);
        EditorGUILayout.PropertyField(modelSplineProp);
        EditorGUILayout.PropertyField(cameraHolderProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera Speeds", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(surroundingCameraRateMultiplierProp);
        EditorGUILayout.PropertyField(modelCameraRateMultiplierProp);
        EditorGUILayout.PropertyField(reverseCameraRateMultiplierProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spherical Spiral Waypoints", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(radiusProp);
        EditorGUILayout.PropertyField(poleMarginDegProp);
        EditorGUILayout.PropertyField(pointSpacingProp);
        EditorGUILayout.PropertyField(loopSpacingProp);
        EditorGUILayout.PropertyField(drawConnectionsProp);
        EditorGUILayout.PropertyField(gizmoColorProp);
        EditorGUILayout.PropertyField(cubeSizeProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Model File", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(modelPathProp);
        DrawFilePopup();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Reverse Look (Orbit)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lookOrbitDistanceProp);
        EditorGUILayout.PropertyField(lookOrbitRadiusProp);
        EditorGUILayout.PropertyField(lookOrbitRevsPerSecondProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Landmarks", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(landmarkHoldSecondsProp);
        EditorGUILayout.PropertyField(approachBlendDistanceProp);
        EditorGUILayout.PropertyField(stopDistanceProp);
        EditorGUILayout.PropertyField(landmarksProp, true);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild Waypoints"))
                comp.Rebuild();

            if (GUILayout.Button("Clear Waypoints"))
                comp.ClearWaypoints();
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Surrounding Spline"))
                comp.GenerateSurroundingSpline();

            if (GUILayout.Button("Instantiate Recording Splines"))
                comp.InstantianteRecordingSplines();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reproject Landmarks"))
                comp.ProjectLandmarksOntoModelSpline();
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Start Navigation"))
                comp.StartNavigation();

            if (GUILayout.Button("Stop Navigation"))
                comp.StopNavigation();
        }

        serializedObject.ApplyModifiedProperties();
    }

    void DrawFilePopup()
    {
        string root = modelPathProp.stringValue;

        // Gather files
        List<string> files = new List<string>();
        if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
        {
            foreach (var p in extensions)
                files.AddRange(Directory.GetFiles(root, p, SearchOption.TopDirectoryOnly));
        }

        EditorGUILayout.LabelField("Dataset File", EditorStyles.miniBoldLabel);

        if (files.Count > 0)
        {
            var displayNames = files.Select(Path.GetFileName).Distinct().OrderBy(n => n).ToArray();

            // Current selection index based on existing fileName
            string currentName = Path.GetFileName(fileNameProp.stringValue);
            int currentIndex = Mathf.Max(0, System.Array.IndexOf(displayNames, currentName));

            int newIndex = EditorGUILayout.Popup("File Name", currentIndex, displayNames);
            if (newIndex < 0) newIndex = 0;

            // Only store the file name (not full path)
            fileNameProp.stringValue = displayNames[newIndex];
        }
        else
        {
            EditorGUILayout.HelpBox(
                "No dataset files found. Check the folder path or add files.",
                MessageType.Info
            );
            // Fallback: allow manual entry if nothing is found
            fileNameProp.stringValue = EditorGUILayout.TextField("File Name", fileNameProp.stringValue);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh"))
            {
                // Forcing a repaint is enough; we re-scan each OnInspectorGUI call
                Repaint();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(root) || !Directory.Exists(root)))
            {
                if (GUILayout.Button("Open Folder"))
                {
    #if UNITY_EDITOR_WIN
                    EditorUtility.RevealInFinder(root.Replace("/", "\\"));
    #else
                    EditorUtility.RevealInFinder(root);
    #endif
                }
            }
        }
    }
}
#endif
