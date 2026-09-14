using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace CustomUI
{
    /// <summary>
    /// ModelScrollArea is a scrollable area that displays models.
    /// It allows users to filter, select, and manage models.
    /// </summary>
    /// <remarks>
    /// This class extends FilterableScrollArea to provide functionality for displaying and managing models.
    /// It includes methods for loading data, adding elements, and handling user interactions.
    /// </remarks>
    public class ModelScrollArea : FilterableScrollArea<ModelScrollAreaElement, Model>
    {
        public GameObject ListElementsPrefab;
        public Func<int, bool> OnModelRead;

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

        /*protected override List<Model> LoadDataList()
        {
            var modelList = new List<Model>();
            var fileInfo = new DirectoryInfo(FileManager.trainigsDataPath).GetFiles();
            foreach (var file in fileInfo)
            {
                var filePath = FileManager.trainigsDataPath + file.Name;
                var res = FileManager.DeserializeJsonFile(filePath, out Model model, JsonSerializationSettings.ModelJsonSettings);
                var success = OnModelRead?.Invoke(res) ?? true;
                if (success)
                {
                    modelList.Add(model);
                }
            }
            return modelList;
        }*/

        protected override List<Model> LoadDataList()
        {
            var models = new List<Model>();

            // Iterate lazily; do not allocate FileInfo[]
            foreach (var path in Directory.EnumerateFiles(FileManager.TrainigsDataPath, "*" + FileManager.TrainingsFileExtension))
            {
                var model = new Model();
                model.ReadFromFile(path);
                models.Add(model);
                /*// Preserve your original callback behavior (uses BasicMessages code from FileManager.ReadContentFrom)
                var res = FileManager.ReadContentFrom(path, out var json);
                var ok = OnModelRead?.Invoke(res) ?? true;
                if (!ok || string.IsNullOrEmpty(json)) continue;

                var basicModelLoaded = TryReadHeader(json);
                if (basicModelLoaded != null)
                {
                    models.Add(basicModelLoaded);
                }*/
            }

            return models.OrderBy(m => m.FileName).ToList();
        }

        /*private static Model TryReadHeader(string json)
        {
            string fileName = null;
            long fileID = 0;
            string description = "";

            using (var sr = new StringReader(json))
            using (var reader = new JsonTextReader(sr))
            {
                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.PropertyName) continue;

                    var prop = (string)reader.Value;
                    if (prop == "fileName")
                    {
                        reader.Read();
                        fileName = reader.Value as string;
                    }
                    else if (prop == "fileID")
                    {
                        reader.Read();
                        if (reader.Value != null) fileID = Convert.ToInt64(reader.Value);
                    }
                    else if (prop == "description")
                    {
                        reader.Read();
                        description = reader.Value?.ToString() ?? "";
                    }

                    // Early exit if we already have the 3 fields
                    if (fileName != null && fileID != 0 && description != null)
                    {
                        // Keep reading would only waste time
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(fileName)) return null;

            var model = new Model(fileID, fileName, description);
            return model;
        }*/

        protected override ModelScrollAreaElement AddElement(Model model, int index)
        {
            var modelScrollArealObject = CommonUtils.Instantiate(ListElementsPrefab, ContentGameObject.transform);
            modelScrollArealObject.transform.SetSiblingIndex(index);
            var modelScrollAreaElement = modelScrollArealObject.GetComponent<ModelScrollAreaElement>();
            modelScrollAreaElement.Init(this, model);
            if (OnScrollAreaElementClicked != null)
            {
                modelScrollAreaElement.ToggleController.OnSelected += () => OnScrollAreaElementClicked.Invoke(modelScrollAreaElement);
                modelScrollAreaElement.ToggleController.OnDeselected += () => OnScrollAreaElementClicked.Invoke(modelScrollAreaElement);
            }
            modelScrollAreaElement.UpdateContent();
            return modelScrollAreaElement;
        }

        protected override Model GetElementData(ModelScrollAreaElement element) => element.Data;
        protected override void SetElementData(ModelScrollAreaElement element, Model data) => element.Data = data;
        protected override void UpdateElementPreview(ModelScrollAreaElement element) => element.UpdateContent();
        protected override string GetFileName(Model data) => data.FileName;
        protected override long GetFileID(Model data) => data.FileID;


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