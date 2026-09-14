
using TMPro;
using UnityEngine;

namespace CustomUI
{
    /// <summary>
    /// UISplinePresetSCE is a scrollable UI element that represents a spline preset in the game.
    /// It inherits from ScrollElementBase and provides functionality to display spline preset information.
    /// </summary>
    /// <remarks>
    /// This class is used to create a spline preset element in a scrollable area, displaying its name and description.
    /// </remarks>

    public class SplinePresetScrollAreaElement : ScrollAreaElementBase<SplinePresetScrollArea, SplinePreset>
    {
        [SerializeField] private TextMeshProUGUI _splinePresetNameText;
        [SerializeField] private TextMeshProUGUI _splinePresetDescriptionText;
        [HideInInspector] public ToggleController ToggleController;

        protected override void OnInitComplete()
        {
            ToggleController = GetComponent<ToggleController>();
        }

        public override void UpdateContent()
        {
            _splinePresetNameText.text = Data.FileName;
            _splinePresetDescriptionText.text = Data.description;
        }

        protected override void SetSelectedElement(ScrollAreaElementBase<SplinePresetScrollArea, SplinePreset> element)
        {
            ScrollArea.SetSelectedElement(element as SplinePresetScrollAreaElement);
        }

        protected override void OnSetAsSelectedComplete()
        {
            ScrollArea.PreviewFlagObject.gameObject.SetActive(true);
        }

        protected override void OnSetAsNotSelectedComplete()
        {
            //Debug.Log("Deselecting element " + Data.fileName);
            ScrollArea.PreviewFlagObject.gameObject.SetActive(false);
            if (ToggleController != null && ToggleController.IsSelected)
            {
                ToggleController.Deselect();
            }
        }
    }
}