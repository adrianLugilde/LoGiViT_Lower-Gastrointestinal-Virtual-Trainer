using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CustomUI
{
    /// <summary>
    /// ModelScrollAreaElement is a scrollable UI element that represents a model in the game.
    /// It inherits from ScrollAreaElementBase and provides functionality to display model information.
    /// /// </summary>
    /// /// <remarks>
    /// This class is used to create a model element in a scrollable area, displaying its name and description.
    /// /// </remarks>
    public class ModelScrollAreaElement : ScrollAreaElementBase<ModelScrollArea, Model>
    {
        [SerializeField] private TextMeshProUGUI _modelNameText;
        [SerializeField] private TextMeshProUGUI _modelDescriptionText;
        [SerializeField] private RawImage _modelPreviewImage;
        [HideInInspector] public ToggleController ToggleController;

        protected override void OnInitComplete()
        {
            ToggleController = GetComponent<ToggleController>();
        }

        public override void UpdateContent()
        {
            _modelNameText.text = Data.FileName;
            _modelDescriptionText.text = Data.Description;
            Data.LoadModelPreview(out Texture2D modelPreview);
            _modelPreviewImage.texture = modelPreview;
        }

        protected override void SetSelectedElement(ScrollAreaElementBase<ModelScrollArea, Model> element)
        {
            ScrollArea.SetSelectedElement(element as ModelScrollAreaElement);
        }

        public void Clearselection()
        {
            SetAsNotSelected();
        }

        protected override void OnSetAsNotSelectedComplete()
        {
            Debug.Log("Deselecting element " + Data.FileName);
            if (ToggleController != null && ToggleController.IsSelected)
            {
                ToggleController.Deselect();
            }
        }
    }
}