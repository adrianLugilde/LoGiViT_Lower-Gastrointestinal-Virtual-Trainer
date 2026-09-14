using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Text;
using LargeIntestine;
using Messages;
using Newtonsoft.Json;

/// <summary>
/// Static utility class for file management operations.
/// Path constants and initialization are handled here.
/// Most file operations are now handled directly by JsonData subclasses.
/// </summary>
public static class FileManager
{
    #region Path Constants
    public const string trainingsPartialPath = "Trainings/";
    public const string meshesPartialPath = "Meshes/";
    public const string modelPreviewsPartialPath = "ModelPreviews/";
    public const string trainingRankingsPartialPath = "TrainingRankings/";
    public const string defaultFilesPartialPath = "DefaultFiles/";
    public const string splinePresetsPartialPath = "SplinePresets/";
    public const string liGenerationConfigurationsPartialPath = "LIGenerationConfigurations/";
    public const string diseasesPartialPath = "Diseases/";
    public const string materialResourcesPath = "Materials";
    public const string meshesResourcesPath = "Meshes";
    public const string diseaseSpritesResourcesPath = "2D Textures/Sprites/Diseases";
    public const string diseaseImagesResourcesPath = "2D Textures/Images/Diseases";
    public const string diseaseMeshesResourcesPath = meshesResourcesPath + "/Diseases";
    public const string coverageRankingFileName = "CoverageTrainingRanking.json";
    public const string polypRankingFileName = "PolypTrainingRanking.json";
    #endregion

    #region Data Paths (initialized at runtime)
    public static string TrainigsDataPath { get; private set; }
    public static string MeshesDataPath { get; private set; }
    public static string ModelPreviewsDataPath { get; private set; }
    public static string TrainingRankingsDataPath { get; private set; }
    public static string DefaultFilesPath { get; private set; }
    public static string SplinePresetsDataPath { get; private set; }
    public static string DiseasesFilePath { get; private set; }
    public static string LIGenerationConfigurationsDataPath { get; private set; }
    public static string StreamingAssetsPath { get; private set; }
    #endregion

    #region File Extensions
    public const string TrainingsFileExtension = ".tc";
    public const string ModelPreviewsExtension = ".png";
    public const string DiseaseFileExtension = ".ds";
    public const string LibcFileExtension = ".libc";
    public const string LIGenerationConfigurationFileExtension = ".ligc";
    public const string SpinePresetFileExtension = ".sp";
    #endregion

    public static bool IsEncryptationEnabled { get; private set; }

    private static List<string> _relativePaths = new List<string>();

    public static MessageController MsgController;
    public static bool IsInitialized { get; private set; } = false;
    
    public static void init(bool encryptationEnabled)
    {
        IsEncryptationEnabled = encryptationEnabled;
        //FileBrowser.AddQuickLink("Users", "C:\\Users", null);
        TrainigsDataPath = Path.Combine(Application.persistentDataPath, trainingsPartialPath);
        MeshesDataPath = Path.Combine(Application.persistentDataPath, meshesPartialPath);
        ModelPreviewsDataPath = Path.Combine(Application.persistentDataPath, modelPreviewsPartialPath);
        TrainingRankingsDataPath = Path.Combine(Application.persistentDataPath, trainingRankingsPartialPath);
        SplinePresetsDataPath = Path.Combine(Application.persistentDataPath, splinePresetsPartialPath);
        DiseasesFilePath = Path.Combine(Application.persistentDataPath, diseasesPartialPath);
        LIGenerationConfigurationsDataPath = Path.Combine(Application.persistentDataPath, liGenerationConfigurationsPartialPath);
        StreamingAssetsPath = Application.streamingAssetsPath;
        DefaultFilesPath = Path.Combine(StreamingAssetsPath, defaultFilesPartialPath);
        _relativePaths.Add(TrainigsDataPath);
        _relativePaths.Add(MeshesDataPath);
        _relativePaths.Add(ModelPreviewsDataPath);
        _relativePaths.Add(TrainingRankingsDataPath);
        _relativePaths.Add(SplinePresetsDataPath);
        _relativePaths.Add(DiseasesFilePath);
        _relativePaths.Add(LIGenerationConfigurationsDataPath);
        Debug.Log(TrainigsDataPath);
        Debug.Log(MeshesDataPath);
        Debug.Log(ModelPreviewsDataPath);
        Debug.Log(TrainingRankingsDataPath);
        Debug.Log(SplinePresetsDataPath);
        Debug.Log(DiseasesFilePath);
        Debug.Log(LIGenerationConfigurationsDataPath);

        BuildRelativePaths();
        IsInitialized = true;
    }

    public static int BuildRelativePaths()
    {
        try
        {
            foreach (var partialPath in _relativePaths)
            {
                if (!Directory.Exists(partialPath))
                {
                    GenerateDefaultFiles(partialPath);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to create relative paths with exception " + e);
            return BasicMessages.RelativePathsCreation;
        }
        return BasicMessages.None;
    }

    private static void GenerateDefaultFiles(string partialPath)
    {
        Debug.LogWarning(partialPath);
        var partialRelativePath = new DirectoryInfo(partialPath).Name + "/";
        string sourcePath;
        FileInfo[] fileInfo;
        switch (partialRelativePath)
        {
            case splinePresetsPartialPath:
                Directory.CreateDirectory(partialPath);
                sourcePath = Path.Combine(DefaultFilesPath, splinePresetsPartialPath);
                fileInfo = new DirectoryInfo(sourcePath).GetFiles();
                foreach (var file in fileInfo)
                {
                    if (file.Extension != ".meta")
                    {
                        File.Copy(Path.Combine(sourcePath, file.FullName), Path.Combine(partialPath, file.Name));
                    }
                }
                break;
            case diseasesPartialPath:
                Directory.CreateDirectory(partialPath);
                sourcePath = Path.Combine(DefaultFilesPath, diseasesPartialPath);
                fileInfo = new DirectoryInfo(sourcePath).GetFiles();
                foreach (var file in fileInfo)
                {
                    if (file.Extension != ".meta")
                    {
                        File.Copy(Path.Combine(sourcePath, file.FullName), Path.Combine(partialPath, file.Name));
                    }
                }
                break;
            default:
                Directory.CreateDirectory(partialPath);
                break;
        }
    }

    public static int GetFileName(string path, out string fileName, string extension = "")
    {
        fileName = "";
        if (extension != "" && Path.GetExtension(path) != extension) return FileErrors.InvalidExtension;
        fileName = Path.GetFileName(path);
        return BasicMessages.None;
    }


    public static string SetSplineVertex(string algo)
    {
        var ogla = GetSplineVertex();
        StringBuilder inSb = new StringBuilder(algo);
        StringBuilder outSb = new StringBuilder(algo.Length);
        char c;
        for (int i = 0; i < algo.Length; i++)
        {
            c = inSb[i];
            c = (char)(c ^ ogla);
            outSb.Append(c);
        }
        return outSb.ToString();
    }

    public static int GetSplineVertex()
    {
        var training = new Model();
        var splinePreset = new SplinePreset();
        return training.GetType().Name.Length * splinePreset.GetType().Name.Length;
    }

    #region Utility Methods
    
    public static int ReadContentFrom(string sourcePath, out string fileContent)
    {
        var res = BasicMessages.None;
        fileContent = "";
        try
        {
            fileContent = File.ReadAllText(sourcePath);
        }
        catch
        {
            return FileErrors.FileParsing;
        }
        return res;
    }

    public static int DeserializeJsonFile<T>(string sourcePath, out T obj, JsonSerializerSettings jsonSerializationSettings = null)
    {
        var res = BasicMessages.None;
        obj = default(T);
        res = ReadContentFrom(sourcePath, out string fileContent);
        if (res == BasicMessages.None)
        {
            obj = jsonSerializationSettings != null ? JsonConvert.DeserializeObject<T>(fileContent, jsonSerializationSettings) :
                JsonConvert.DeserializeObject<T>(fileContent);
        }
        return res;
    }

    public static void Delete(string filePath, OnFileOperationDone onFileDeleted = null)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
        onFileDeleted?.Invoke();
    }

    public static Tuple<string, string> GetFileContentAndName(string[] paths)
    {
        var sourcePath = paths[0];
        return new Tuple<string, string>(File.ReadAllText(sourcePath), Path.GetFileNameWithoutExtension(sourcePath));
    }
    
    #endregion

    #region Training Rankings
    
    public static List<T> LoadTrainingRankingData<T>()
    {
        var fileName = typeof(T).Name switch
        {
            nameof(CoverageTrainingResult) => coverageRankingFileName,
            nameof(PolypTrainingResult) => polypRankingFileName,
            _ => string.Empty
        };

        var path = Path.Combine(TrainingRankingsDataPath, fileName);
        
        if (!File.Exists(path))
            return new List<T>();

        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
    }

    public static void SaveTrainingRankingData<T>(List<T> rankingData)
    {
        var fileName = typeof(T).Name switch
        {
            nameof(CoverageTrainingResult) => coverageRankingFileName,
            nameof(PolypTrainingResult) => polypRankingFileName,
            _ => string.Empty
        };

        var path = Path.Combine(TrainingRankingsDataPath, fileName);
        var json = JsonConvert.SerializeObject(rankingData, Formatting.Indented);
        File.WriteAllText(path, json);
    }
    
    #endregion

    public delegate void OnFileOperationDone();
}

