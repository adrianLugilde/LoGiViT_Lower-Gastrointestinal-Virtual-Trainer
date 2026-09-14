using Messages;
using Newtonsoft.Json;
using SplineMesh;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SplinePreset : JsonData
{
    /**** JsonData Implementation ****/
    public override string DataPath => FileManager.SplinePresetsDataPath;
    public override string FileExtension => FileManager.SpinePresetFileExtension;
    public override JsonSerializerSettings JsonSettings => JsonSerializationSettings.SplinePresetJsonSettings;
    
    public int startNodeIdx = 0;
    public string description = "No description was provided";
    public List<SplineNode> modifiedNodes;

    public SplinePreset()
    {
        FileID = DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    public SplinePreset(string filePath)
    {
        ReadFromFile(filePath);
    }

    public SplinePreset(SplinePreset other)
    {
        FileID = other.FileID;
        FileName = other.FileName;
        description = other.description;
        modifiedNodes = new List<SplineNode>(other.modifiedNodes);
        startNodeIdx = other.startNodeIdx;
    }

    public int Build(GameObject dataHolder, List<SplineNode> nodes, int startNodeIdx)
    {
        var inputFields = dataHolder.GetComponentsInChildren<TMP_InputField>(true);
        var fileIDText = inputFields[0].text;
        FileID = fileIDText != string.Empty ? long.Parse(fileIDText) : DateTimeOffset.Now.ToUnixTimeMilliseconds();
        FileName = inputFields[1].text;
        if (FileName == string.Empty) return FileErrors.MissingName;
        if (inputFields[2].text != string.Empty) description = inputFields[2].text;
        this.modifiedNodes = nodes;
        this.startNodeIdx = startNodeIdx;
        return BasicMessages.None;
    }

    public int Build(TMP_InputField[] inputFields, List<SplineNode> nodes, int startNodeIdx)
    {
        var fileIDText = inputFields[0].text;
        FileID = fileIDText != string.Empty ? long.Parse(fileIDText) : DateTimeOffset.Now.ToUnixTimeMilliseconds();
        FileName = inputFields[1].text;
        if (FileName == string.Empty) return FileErrors.MissingName;
        if (inputFields[2].text != string.Empty) description = inputFields[2].text;
        this.modifiedNodes = nodes;
        this.startNodeIdx = startNodeIdx;
        return BasicMessages.None;
    }

    /// <summary>
    /// Preview the data in the provided GameObject.
    /// </summary>
    public void PreviewDataIn(GameObject dataHolder)
    {
        TMP_InputField[] inputFields = dataHolder.GetComponentsInChildren<TMP_InputField>(true);
        inputFields[0].text = FileID.ToString();
        inputFields[1].text = FileName;
        inputFields[2].text = description;
    }

    public void PreviewDataIn(params TMP_InputField[] inputFields)
    {
        inputFields[0].text = FileID.ToString();
        inputFields[1].text = FileName;
        inputFields[2].text = description;
    }

    public void Save(FileManager.OnFileOperationDone onSaveResult, CustomDelegates.OnConfirm onOverwriteConfirmed)
    {
        base.Save(onSaveResult, onOverwriteConfirmed);
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
