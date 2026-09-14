using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using UnityEngine.Localization.Components;

public class ForceLayoutRebuild : MonoBehaviour
{
    RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    /*void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        Rebuild();
    }*/


    void OnEnable()
    {
        foreach (var lse in GetComponentsInChildren<LocalizeStringEvent>(true))
            lse.OnUpdateString.AddListener(OnStringUpdated);

        StartCoroutine(WaitForLocalizationThenRebuild());
    }

    /*void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }*/

    void OnDisable()
    {
        foreach (var lse in GetComponentsInChildren<LocalizeStringEvent>(true))
            lse.OnUpdateString.RemoveListener(OnStringUpdated);
    }

    void OnStringUpdated(string value)
    {
        StartCoroutine(RebuildNextFrame());
    }

    void Start()
    {
        StartCoroutine(WaitForLocalizationThenRebuild());
    }

    System.Collections.IEnumerator WaitForLocalizationThenRebuild()
    {
        yield return LocalizationSettings.InitializationOperation;
        yield return null;
        Rebuild();
        yield return null; // let TMP rebuild meshes
        Rebuild();         // rebuild layout again with correct mesh sizes
    }

    System.Collections.IEnumerator RebuildNextFrame()
    {
        yield return null;
        Rebuild();
        yield return null;
        Rebuild();
    }

    void OnRectTransformDimensionsChange()
    {
        // unregister all, re-register (catches newly spawned LSEs)
        foreach (var lse in GetComponentsInChildren<LocalizeStringEvent>(true))
            lse.OnUpdateString.RemoveListener(OnStringUpdated);
        foreach (var lse in GetComponentsInChildren<LocalizeStringEvent>(true))
            lse.OnUpdateString.AddListener(OnStringUpdated);

        StartCoroutine(RebuildNextFrame());
    }

    void OnLocaleChanged(Locale locale)
    {
        StartCoroutine(RebuildNextFrame());
    }

    void Rebuild()
    {
        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
            tmp.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }
}