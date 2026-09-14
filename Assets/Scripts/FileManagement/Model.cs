using System;
using UnityEngine;
using TMPro;
using SplineMesh;
using System.Collections.Generic;
using System.IO;
using GeometryUtils;
using ImgSpc.Exporters;
using Messages;
using LargeIntestine;
using Newtonsoft.Json;

public class Model : JsonData
{
    private const string _defaultGenerationConfigurationFile = "108217697723513";
    
    /**** JsonData Implementation ****/
    public override string DataPath => FileManager.TrainigsDataPath;
    public override string FileExtension => FileManager.TrainingsFileExtension;
    public override JsonSerializerSettings JsonSettings => JsonSerializationSettings.ModelJsonSettings;
    
    /**** Serialized variables ****/
    public string Description { get; set; } = "No description was provided";
    public string GenerationConfigurationFile { get; set; } = _defaultGenerationConfigurationFile;
    public List<SplineNode> SplineNodes { get; set; }
    public List<float> SegmentsStartValueInSpline { get; set; }
    public List<SerializableDictionary<string, float>> RenderersBlendshapesDicts { get; set; } 
    public List<Disease> Diseases { get; set; }
    public NoiseSettings NoiseSettings { get; set; } = null;
    public bool HasMesh { get; set; } = false;
    /**** Non-serialized variables ****/
    [NonSerialized] private string meshExportMethod = "stl";
    private int modelPreviewWidth = 2000;
    private int modelPreviewHeight = 2000;


    public int Update(string fileName = null, string description = null, List<SplineNode> splineNodes = null, List<float> segmentsStartValueInSpline = null, List<SerializableDictionary<string, float>> renderersBlendshapesDicts = null, List<Disease> diseases = null, SkinnedMeshRenderer liSmr = null, string generationConfigurationFile = null, NoiseSettings noiseSettings = null)
    {
        if (FileID == 0) FileID = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        FileName = fileName ?? FileID.ToString();
        if (FileName == "") return FileErrors.MissingName;
        if (description != null) Description = description;
        this.SplineNodes = splineNodes;
        this.SegmentsStartValueInSpline = segmentsStartValueInSpline;
        this.RenderersBlendshapesDicts = renderersBlendshapesDicts;
        this.Diseases = diseases ?? new List<Disease>();
        this.NoiseSettings = noiseSettings;
        if(generationConfigurationFile != null)
        GenerationConfigurationFile = generationConfigurationFile ?? _defaultGenerationConfigurationFile;
        return BasicMessages.None;
    }

    public int LoadIntoSMR(SkinnedMeshRenderer smr, MeshCollider mc = null, Material[] fallbackMaterials = null)
    {
        var destPath = Path.Combine(FileManager.MeshesDataPath, FileID + ".binmesh");
        if (File.Exists(destPath))
        {
            MeshModelIO.LoadInto(smr, destPath, meshCollider: mc, gzipped: true, fallbackMaterials: fallbackMaterials);
            return BasicMessages.None;
        }
        return FileErrors.FileNotFound;
    }

    public int SaveModelPreview(Texture2D texture)
    {
        var path = Path.Combine(FileManager.ModelPreviewsDataPath, FileID + ".png");
        File.WriteAllBytes(path, texture.EncodeToPNG());
        return BasicMessages.None;
    }

    public int LoadModelPreview(out Texture2D modelPreview)
    {
        modelPreview = null;
        var path = Path.Combine(FileManager.ModelPreviewsDataPath, FileID + ".png");
        if (!File.Exists(path)) return FileErrors.FileNotFound;
        var bytes = File.ReadAllBytes(path);
        modelPreview = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        modelPreview.LoadImage(bytes, markNonReadable: false);
        return BasicMessages.None;
    }

    /// <summary>
    /// Override to check if mesh file exists after loading.
    /// </summary>
    protected override int FromJson(string jsonContents)
    {
        var result = base.FromJson(jsonContents);
        if (result == BasicMessages.None)
            HasMesh = CheckModelMesh();
        return result;
    }
    
    /// <summary>
    /// Loads model data from the default path (based on FileName).
    /// This is a convenience method that calls ReadFromFile with GetFullPath().
    /// </summary>
    public int LoadFromDefaultPath()
    {
        return ReadFromFile(GetFullPath());
    }

    private bool CheckModelMesh()
    {
        if (FileManager.IsInitialized)
        {
            var destPath = Path.Combine(FileManager.MeshesDataPath, FileID + ".binmesh");
            return File.Exists(destPath);
        } else {
            return false;
        }
    }

    public Model() {}
    
    public Model(string fileName, string description, 
        List<SplineNode> splineNodes, List<float> segmentsStartValueInSpline, 
        List<SerializableDictionary<string, float>> renderersBlendshapesDicts, List<Disease> diseases,
        string generationConfigurationFile)
        : this(DateTimeOffset.Now.ToUnixTimeMilliseconds(), fileName, description, 
            splineNodes, segmentsStartValueInSpline, renderersBlendshapesDicts, diseases,
            generationConfigurationFile)
    { }

    /// <summary>
    /// Constructor for JSON deserialization
    /// </summary>
    public Model(long fileID, string fileName, string description, 
        List<SplineNode> splineNodes, List<float> segmentsStartValueInSpline, 
        List<SerializableDictionary<string, float>> renderersBlendshapesDicts, List<Disease> diseases,
        string generationConfigurationFile)
    {
        this.FileID = fileID;
        this.FileName = fileName ?? fileID.ToString();
        this.Description = description ?? "No description was provided";
        this.SplineNodes = splineNodes ?? new List<SplineNode>();
        this.SegmentsStartValueInSpline = segmentsStartValueInSpline ?? new List<float>();
        this.RenderersBlendshapesDicts = renderersBlendshapesDicts ?? new List<SerializableDictionary<string, float>>();
        this.Diseases = diseases ?? new List<Disease>();
        GenerationConfigurationFile = generationConfigurationFile ?? _defaultGenerationConfigurationFile;
        HasMesh = CheckModelMesh();
    }

    public Model(Model otherModel, bool copyFileID = false)
    {
        FileID = copyFileID ? otherModel.FileID : DateTimeOffset.Now.ToUnixTimeMilliseconds();
        FileName = otherModel.FileName;
        Description = otherModel.Description;
        SplineNodes = new List<SplineNode>(otherModel.SplineNodes);
        SegmentsStartValueInSpline = new List<float>(otherModel.SegmentsStartValueInSpline);
        RenderersBlendshapesDicts = new List<SerializableDictionary<string, float>>(otherModel.RenderersBlendshapesDicts);
        Diseases = new List<Disease>(otherModel.Diseases);
        NoiseSettings = otherModel.NoiseSettings != null ? new NoiseSettings(otherModel.NoiseSettings) : null;
    }


    public void PreviewDataIn(GameObject dataHolder)
    {
        TMP_InputField[] inputFields = dataHolder.GetComponentsInChildren<TMP_InputField>(true);
        inputFields[0].text = FileID.ToString();
        inputFields[1].text = FileName;
        inputFields[2].text = Description;

    }

    public void PreviewDataIn(params TMP_InputField[] inputFields)
    {
        inputFields[0].text = FileName;
        inputFields[1].text = Description;
    }

    public void Save(ImgSpcExporter meshExporter, FileManager.OnFileOperationDone onSaveResult, CustomDelegates.OnConfirm onOverwriteConfirmed)
    {
        var success = base.Save(onSaveResult, onOverwriteConfirmed);
        if (success) SaveModelMesh(meshExporter);
    }

    public void Save(SkinnedMeshRenderer smr, Camera screenshotCamera, FileManager.OnFileOperationDone onSaveResult, CustomDelegates.OnConfirm onOverwriteConfirmed)
    {
        var success = base.Save(onSaveResult, onOverwriteConfirmed);
        if (success)
        {
            CustomCameraScreenshot.TakeScreenshot(FileManager.ModelPreviewsDataPath, FileID + ".png", screenshotCamera, modelPreviewWidth, modelPreviewHeight);
            if (Diseases?.Count > 0)
            {
                Debug.Log("Savming model mesh with diseases in " + Path.Combine(FileManager.MeshesDataPath, FileID + ".binmesh"));
                MeshModelIO.Save(Path.Combine(FileManager.MeshesDataPath, FileID + ".binmesh"), smr, gzip: true);
            }
        }
    }

    private void SaveModelMesh(ImgSpcExporter meshExporter)
    {
        var destPath = Path.Combine(FileManager.MeshesDataPath, FileID.ToString() + "." + meshExportMethod);
        MeshUtils.ExportMesh(meshExporter, meshExportMethod, destPath);
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
        var meshFilePath = Path.Combine(FileManager.MeshesDataPath, Path.GetFileNameWithoutExtension(filePath) + "." + meshExportMethod);
        base.Delete(meshFilePath, null);
    }
}