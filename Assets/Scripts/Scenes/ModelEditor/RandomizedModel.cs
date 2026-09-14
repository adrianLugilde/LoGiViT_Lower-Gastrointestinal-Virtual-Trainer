using System;
using UnityEngine;
using TMPro;
using SplineMesh;
using System.Collections.Generic;
using ImgSpc.Exporters;
using Messages;
using LargeIntestine;
using Newtonsoft.Json;

public class RandomizedModel : JsonData
{
    /**** JsonData Implementation ****/
    public override string DataPath => FileManager.TrainigsDataPath;
    public override string FileExtension => FileManager.TrainingsFileExtension;
    public override JsonSerializerSettings JsonSettings => JsonSerializationSettings.ModelJsonSettings;
    
    public List<SplineNode> splineNodes;
    public List<BlendshapesWeights> renderersBlendshapesWeights;
    
    public int Build(Spline spline, List<BlendshapesWeights> renderersBlendshapesWeights)
    {
        this.splineNodes = spline.nodes;
        this.renderersBlendshapesWeights = renderersBlendshapesWeights;

        return BasicMessages.None;
    }

    public override string ToJson(JsonSerializerSettings jsonSerializerSettings = null, bool indented = true)
    {
        var settings = jsonSerializerSettings ?? JsonSettings;
        var newModel = new
        {
            this.splineNodes,
            this.renderersBlendshapesWeights
        };
        return indented ? JsonConvert.SerializeObject(newModel, Formatting.Indented, settings) :
            JsonConvert.SerializeObject(newModel, settings);
    }

    public RandomizedModel()
    {
        FileID = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        splineNodes = new List<SplineNode>();
    }

    public RandomizedModel(string filePath)
    {
        ReadFromFile(filePath);
    }

    public void PreviewDataIn(GameObject dataHolder)
    {
        /*TMP_InputField[] inputFields = dataHolder.GetComponentsInChildren<TMP_InputField>(true);
        inputFields[0].text = fileID.ToString();
        inputFields[1].text = fileName;
        inputFields[2].text = description;*/
    }

    public void Save(ImgSpcExporter meshExporter, FileManager.OnFileOperationDone onSaveResult, CustomDelegates.OnConfirm onOverwriteConfirmed)
    {
        var success = base.Save(onSaveResult, onOverwriteConfirmed);
        if (success) SaveModelMesh(meshExporter);
    }

    private void SaveModelMesh(ImgSpcExporter meshExporter)
    {
        /*var destPath = Path.Combine(FileManager.meshesDataPath, fileID.ToString() + "." + meshExportMethod);
        MeshUtils.ExportMesh(meshExporter, meshExportMethod, destPath);*/
    }

    public void Import(string sourcePath, FileManager.OnFileOperationDone onSaveResult, CustomDelegates.OnConfirm onOverwriteConfirmed)
    {
        base.Load(sourcePath, onSaveResult, onOverwriteConfirmed);
    }

    public new void Download(string destPath, FileManager.OnFileOperationDone onFileDownloaded, CustomDelegates.OnConfirm onOverwriteConfirmed)
    {
        base.Download(destPath, onFileDownloaded, onOverwriteConfirmed);
    }

    public new void Overwrite(string destPath, FileManager.OnFileOperationDone onSaveResult)
    {
        base.Overwrite(destPath, onSaveResult);
    }

    public void _Delete(string filePath, FileManager.OnFileOperationDone onFileDeleted)
    {
        base.Delete(filePath, onFileDeleted);
    }
}