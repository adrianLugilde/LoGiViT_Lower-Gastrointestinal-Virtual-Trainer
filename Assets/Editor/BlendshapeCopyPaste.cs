using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds Copy / Paste Blendshape Values to the SkinnedMeshRenderer context menu
/// (the gear icon or right-click on the component header).
/// Paste matches shapes by name, skipping any that don't exist on the target mesh.
/// </summary>
public static class BlendshapeCopyPaste
{
    private static Dictionary<string, float> _clipboard;

    [MenuItem("CONTEXT/SkinnedMeshRenderer/Copy Blendshape Values")]
    static void Copy(MenuCommand cmd)
    {
        var smr = (SkinnedMeshRenderer)cmd.context;
        var mesh = smr.sharedMesh;
        if (mesh == null) return;

        _clipboard = new Dictionary<string, float>(mesh.blendShapeCount);
        for (int i = 0; i < mesh.blendShapeCount; i++)
            _clipboard[mesh.GetBlendShapeName(i)] = smr.GetBlendShapeWeight(i);

        Debug.Log($"[Blendshapes] Copied {_clipboard.Count} values from '{smr.name}'.");
    }

    [MenuItem("CONTEXT/SkinnedMeshRenderer/Paste Blendshape Values")]
    static void Paste(MenuCommand cmd)
    {
        var smr = (SkinnedMeshRenderer)cmd.context;
        var mesh = smr.sharedMesh;
        if (mesh == null) return;

        Undo.RecordObject(smr, "Paste Blendshape Values");

        int matched = 0;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            if (_clipboard.TryGetValue(name, out float value))
            {
                smr.SetBlendShapeWeight(i, value);
                matched++;
            }
        }

        Debug.Log($"[Blendshapes] Pasted {matched}/{mesh.blendShapeCount} values onto '{smr.name}'.");
    }

    [MenuItem("CONTEXT/SkinnedMeshRenderer/Paste Blendshape Values", validate = true)]
    static bool PasteValidate(MenuCommand cmd) => _clipboard != null && _clipboard.Count > 0;
}
