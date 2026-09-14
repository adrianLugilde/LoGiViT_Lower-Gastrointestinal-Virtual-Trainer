using UnityEngine;
using System;
using UnityEngine.UI;
using TMPro;

namespace CustomUI
{
    /// <summary>
    /// UIDiseaseSCE is a scrollable UI element that represents a disease in the game.
    /// It inherits from ScrollElementBase and provides functionality to display disease information.
    /// </summary>
    /// <remarks>
    /// This class is used to create a disease element in a scrollable area, displaying its mesh, materials, and image.
    /// </remarks>

    [Serializable]
    public class DiseaseScrollAreaElement : ScrollAreaElementBase<DiseaseScrollArea, Disease>
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _parisClassText;
        [SerializeField] private TextMeshProUGUI _jnetClassText;
        [SerializeField] private Image _diseaseImage;
        [HideInInspector] public ToggleController ToggleController;

        protected override void OnInitComplete()
        {
            ToggleController = GetComponent<ToggleController>();

        }

        public override void UpdateContent()
        {
            _diseaseImage.sprite = Data.MeshSprite;
            _nameText.text = Data.Name;
            _descriptionText.text = Data.Description;
            if (Data is Polyp polyp)
            {
                _parisClassText.text = polyp.ParisClass.ToString();
                _jnetClassText.text = polyp.JnetClass.ToString();
            }
            else
            {
                _parisClassText.text = "N/A";
                _jnetClassText.text = "N/A";
            }
        }

        protected override void SetSelectedElement(ScrollAreaElementBase<DiseaseScrollArea, Disease> element)
        {
            ScrollArea.SetSelectedElement(element as DiseaseScrollAreaElement);
        }

        protected override void OnSetAsNotSelectedComplete()
        {
            //Debug.Log("Deselecting element " + Data.fileName);
            if (ToggleController != null && ToggleController.IsSelected)
            {
                ToggleController.Deselect();
            }
        }
    }
}