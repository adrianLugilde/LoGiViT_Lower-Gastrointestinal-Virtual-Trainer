using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CustomUI
{
    /// <summary>
    /// SplinePresetScrollArea is a scrollable area that displays spline presets.
    /// It allows users to filter, select, and manage spline presets.
    /// </summary>
    /// <remarks>
    /// This class extends FilterableScrollArea to provide functionality for displaying and managing spline presets.
    /// It includes methods for loading data, adding elements, and handling user interactions.
    /// </remarks>

    public class DiseaseScrollArea : FilterableScrollArea<DiseaseScrollAreaElement, Disease>
    {
        public GameObject ListElementsPrefab;
        public event Func<int, bool> OnDiseaseRead;
        public List<SplinePreset> DefaultDiseaseElements;
        public bool NeedsUpdate = true;

        public override void Fill()
        {
            if (!NeedsUpdate) return;
            base.Fill();
            NeedsUpdate = false;
        }

        public void ClearSelection()
        {
            SelectedElement?.SetAsNotSelected();
        }

        protected override List<Disease> LoadDataList()
        {
            var diseaseList = new List<Disease>();
            var catalog = PolypProfileCatalog.Load();
            if (catalog == null)
            {
                Debug.LogError("[DiseaseScrollArea] PolypProfileCatalog not found in Resources.");
                return diseaseList;
            }
            foreach (var profile in catalog.GetAll())
            {
                if (profile == null) continue;
                var disease = profile.CreatePolypInstance();
                var success = OnDiseaseRead?.Invoke(0) ?? true;
                if (success)
                    diseaseList.Add(disease);
            }
            return diseaseList;
        }

        protected override DiseaseScrollAreaElement AddElement(Disease disease, int index)
        {
            var diseaseScrollAreaObject = CommonUtils.Instantiate(ListElementsPrefab, ContentGameObject.transform);
            diseaseScrollAreaObject.transform.SetSiblingIndex(index);
            var diseaseScrollAreaElement = diseaseScrollAreaObject.GetComponent<DiseaseScrollAreaElement>();
            diseaseScrollAreaElement.Init(this, disease);
            if (OnScrollAreaElementClicked != null)
            {
                //dsceGameObject.GetComponent<LeanButton>().OnClick.AddListener(() => OnDiseaseScrollAreaElementClicked(dsce));
                diseaseScrollAreaElement.ToggleController.OnSelected += () => OnScrollAreaElementClicked.Invoke(diseaseScrollAreaElement);
                diseaseScrollAreaElement.ToggleController.OnDeselected += () => OnScrollAreaElementClicked.Invoke(diseaseScrollAreaElement);
            }
            diseaseScrollAreaElement.UpdateContent();
            return diseaseScrollAreaElement;
        }

        protected override Disease GetElementData(DiseaseScrollAreaElement element) => element.Data;
        protected override void SetElementData(DiseaseScrollAreaElement element, Disease data) => element.Data = data;
        protected override void UpdateElementPreview(DiseaseScrollAreaElement element) => element.UpdateContent();
        protected override string GetFileName(Disease data) => data.Name;
        protected override long GetFileID(Disease data) => data.FileID;

        /*protected override void ClearElements()
        {
            foreach (var element in ContentGameObject.transform.GetComponentsInChildren<DiseaseScrollAreaElement>(true))
            {
                if (element.Data.fileID != 0)
                {
                    Destroy(element.gameObject);
                }
                else
                {
                    ElementList.Add(element);
                }
            }
        }*/

        protected override void OnEmptyComplete()
        {
            if (SelectedElement != null) SelectedElement.SetAsNotSelected();
        }

        public void DeleteSelectedElement()
        {
            ElementList.Remove(ElementList.Where(element => element.Data == SelectedElement.Data).First());
            Destroy(SelectedElement.gameObject);
        }

        // Uncomment and update if needed
        /*public void UpdateContent(Disease disease)
        {
            var diseaseList = ElementList.Select(dsce => dsce.Data).ToList();
            var itemIndex = diseaseList.FindIndex(item => item.fileID == disease.fileID || string.Equals(item.Name, disease.Name, System.StringComparison.OrdinalIgnoreCase));
            if (itemIndex != -1)
            {
                diseaseList[itemIndex] = disease;
                ElementList[itemIndex].Data = disease;
                ElementList[itemIndex].UpdatePreview();
            }
            else
            {
                diseaseList.Add(disease);
                diseaseList = diseaseList.OrderBy(disease => disease.Name).ToList();
                ElementList.Add(AddElement(disease, diseaseList.IndexOf(disease)));
            }
        }*/
    }
}