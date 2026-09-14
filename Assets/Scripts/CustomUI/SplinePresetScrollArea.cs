using System;
using System.Collections.Generic;
using System.IO;
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
    public class SplinePresetScrollArea : FilterableScrollArea<SplinePresetScrollAreaElement, SplinePreset>
    {
        public GameObject ListElementsPrefab;
        public event Func<int, bool> OnSplinePresetRead;
        public GameObject PreviewFlagObject;
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


        protected override List<SplinePreset> LoadDataList()
        {
            var splinePresetList = new List<SplinePreset>();
            var fileInfo = new DirectoryInfo(FileManager.SplinePresetsDataPath).GetFiles();
            foreach (var file in fileInfo)
            {
                var splinePreset = new SplinePreset();
                var filePath = FileManager.SplinePresetsDataPath + file.Name;
                var res = splinePreset.ReadFromFile(filePath);
                var success = OnSplinePresetRead?.Invoke(res) ?? true;
                if (success)
                {
                    splinePresetList.Add(splinePreset);
                }
            }
            return splinePresetList;
        }

        protected override SplinePresetScrollAreaElement AddElement(SplinePreset splinePreset, int index)
        {
            var splinePresetScrollAreaObject = CommonUtils.Instantiate(ListElementsPrefab, ContentGameObject.transform);
            splinePresetScrollAreaObject.transform.SetSiblingIndex(index);
            var splinePresetScrollAreaElement = splinePresetScrollAreaObject.GetComponent<SplinePresetScrollAreaElement>();
            splinePresetScrollAreaElement.Init(this, splinePreset);
            splinePresetScrollAreaElement.OnSetAsSelected += () => PreviewFlagObject.SetActive(true);
            splinePresetScrollAreaElement.OnSetAsNotSelected += () => PreviewFlagObject.SetActive(false);
            if (OnScrollAreaElementClicked != null)
            {
                splinePresetScrollAreaElement.ToggleController.OnSelected += () => OnScrollAreaElementClicked.Invoke(splinePresetScrollAreaElement);
                splinePresetScrollAreaElement.ToggleController.OnDeselected += () => OnScrollAreaElementClicked.Invoke(splinePresetScrollAreaElement);
            }
            splinePresetScrollAreaElement.UpdateContent();
            return splinePresetScrollAreaElement;
        }

        protected override SplinePreset GetElementData(SplinePresetScrollAreaElement element) => element.Data;
        protected override void SetElementData(SplinePresetScrollAreaElement element, SplinePreset data) => element.Data = data;
        protected override void UpdateElementPreview(SplinePresetScrollAreaElement element) => element.UpdateContent();
        protected override string GetFileName(SplinePreset data) => data.FileName;
        protected override long GetFileID(SplinePreset data) => data.FileID;

        protected override void OnEmptyComplete()
        {
            if (SelectedElement != null) SelectedElement.SetAsNotSelected();
        }

        public void DeleteSelectedElement()
        {
            ElementList.Remove(ElementList.Where(element => element.Data == SelectedElement.Data).First());
            Destroy(SelectedElement.gameObject);
        }
    }
}