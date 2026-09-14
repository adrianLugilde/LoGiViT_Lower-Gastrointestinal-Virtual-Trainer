using UnityEditor;
using UnityEngine;
using LargeIntestine;

[CustomEditor(typeof(LargeIntestineModelGenerator))]
internal class LargeIntestineModelGeneratorEditor : Editor
{
    private bool _meshNoiseFoldout = true;
    private bool _upVectorFoldout = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var gen = (LargeIntestineModelGenerator)target;
        var noise = gen.GetNoiseSettings();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Anatomical Surface Noise", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        _meshNoiseFoldout = EditorGUILayout.Foldout(_meshNoiseFoldout, "Surface Bump Noise", true);
        if (_meshNoiseFoldout)
        {
            EditorGUI.indentLevel++;
            noise.MeshNoiseEnabled    = EditorGUILayout.Toggle(new GUIContent("Enabled",          "Master switch for radial vertex displacement noise on the mesh surface."), noise.MeshNoiseEnabled);
            noise.RadialAmplitude     = EditorGUILayout.FloatField(new GUIContent("Radial Amplitude",  "Max radial vertex displacement (local mesh units). Typical: 0.001–0.02."), noise.RadialAmplitude);
            noise.AxialFrequency      = EditorGUILayout.FloatField(new GUIContent("Axial Frequency",   "Noise cycles along full spline length. Higher = tighter bumps along tube axis."), noise.AxialFrequency);
            noise.AngularFrequency    = EditorGUILayout.FloatField(new GUIContent("Angular Frequency", "Circumferential noise frequency — lobes around cross-section. Advanced."), noise.AngularFrequency);
            noise.CenterFade          = EditorGUILayout.FloatField(new GUIContent("Center Fade",       "Flat-zero zone width at haustral fold center (0 = off). Advanced."), noise.CenterFade);
            EditorGUI.indentLevel--;
        }

        _upVectorFoldout = EditorGUILayout.Foldout(_upVectorFoldout, "Up-Vector Twist Noise", true);
        if (_upVectorFoldout)
        {
            EditorGUI.indentLevel++;
            noise.UpVectorNoiseEnabled       = EditorGUILayout.Toggle(new GUIContent("Enabled",          "Master switch for roll-variation noise on spline up-vectors."), noise.UpVectorNoiseEnabled);
            noise.UpVectorNoiseMaxAngle      = EditorGUILayout.FloatField(new GUIContent("Max Angle",       "Max up-vector rotation around spline tangent (degrees). 0 = no twist."), noise.UpVectorNoiseMaxAngle);
            noise.UpVectorNoiseFrequency     = EditorGUILayout.FloatField(new GUIContent("Frequency",       "Perlin frequency for up-vector angle sampling along spline. Advanced."), noise.UpVectorNoiseFrequency);
            noise.UpVectorNoiseMaxStepAngle  = EditorGUILayout.FloatField(new GUIContent("Max Step Angle",  "Max angular delta between adjacent nodes (degrees). Caps sudden flips. Advanced."), noise.UpVectorNoiseMaxStepAngle);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2f);
        noise.Seed = EditorGUILayout.IntField(new GUIContent("Seed", "Seed for all noise functions. Same seed + config = identical pattern every generation."), noise.Seed);

        if (EditorGUI.EndChangeCheck())
        {
            gen.ApplyNoiseSettings(noise);
            EditorUtility.SetDirty(target);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Load Generation Configuration"))
            gen.LoadCustomGenerationConfiguration();
        else if (GUILayout.Button("Generate Segmented model"))
            gen.GenerateSegmentedModel();
        else if (GUILayout.Button("Generate Welded Model"))
            gen.GenerateWeldedModel();
        else if (GUILayout.Button("Generate Visual Model"))
            gen.GenerateVisualModel();
        else if (GUILayout.Button("Update Up Vectors"))
            gen.UpdateUpVectors();
        else if (GUILayout.Button("Save model"))
            gen.SaveModel();
        else if (GUILayout.Button("Load model"))
            gen.LoadModel();
        else if (GUILayout.Button("Update Config from Renderers"))
            gen.UpdateConfigurationFromRenderers();
        else if (GUILayout.Button("Apply Anatomical Distribution"))
            gen.ApplyAnatomicalDistribution();
        else if (GUILayout.Button("Reset Characteristic Curves to Defaults"))
            gen.ResetCharacteristicCurvesToDefaults();
        else if (GUILayout.Button("Reset Generator"))
            gen.Reset();
    }
}
