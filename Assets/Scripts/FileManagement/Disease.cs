using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[Serializable]
public class Disease : JsonData
{
    /**** JsonData Implementation ****/
    public override string DataPath => FileManager.DiseasesFilePath;
    public override string FileExtension => FileManager.DiseaseFileExtension;
    public override JsonSerializerSettings JsonSettings => JsonSerializationSettings.DiseaseJsonSettings;

    // Serialized — placement data
    [SerializeField] private DiseaseType type = DiseaseType.Polyp;
    [SerializeField, JsonProperty] private string polypProfileId;
    [SerializeField, JsonProperty] private IntestineLocation? location = null;
    [SerializeField, JsonProperty] private int submeshIndex;

    // Runtime — populated from PolypProfile by DiseaseJsonConverter
    [JsonIgnore] private string name;
    [JsonIgnore] private string description;
    [JsonIgnore] private Mesh mesh;
    [JsonIgnore] private Material[] materials;
    [JsonIgnore] private Sprite meshSprite;
    [JsonIgnore] private Texture image;

    public DiseaseType Type
    {
        get => type;
        private set => type = value;
    }

    public string PolypProfileId
    {
        get => polypProfileId;
        set => polypProfileId = value;
    }

    public string Name
    {
        get => name;
        set => name = value;
    }

    public string Description
    {
        get => description;
        set => description = value;
    }

    public IntestineLocation? Location
    {
        get => location;
        set => location = value;
    }

    public int SubmeshIndex
    {
        get => submeshIndex;
        set => submeshIndex = value;
    }

    public Mesh Mesh
    {
        get => mesh;
        set => mesh = value;
    }

    public Material[] Materials
    {
        get => materials;
        set => materials = value;
    }

    public Sprite MeshSprite
    {
        get => meshSprite;
        set => meshSprite = value;
    }

    public Texture Image
    {
        get => image;
        set => image = value;
    }

    public string ImageName => image != null ? image.name : null;
    public string MeshImageName => meshSprite != null ? meshSprite.name : null;

    public Disease() { }

    public Disease(DiseaseType type) => Type = type;

    public virtual Disease Clone()
    {
        return new Disease(type)
        {
            PolypProfileId = PolypProfileId,
            Name = Name,
            Description = Description,
            Location = Location,
            Mesh = Mesh,
            SubmeshIndex = SubmeshIndex,
            Materials = Materials,
            MeshSprite = MeshSprite,
            Image = Image
        };
    }

    public enum IntestineLocation
    {
        Rectum = 0,
        RectumSigmoid = 1,
        Descending = 2,
        SplenicFlexure = 3,
        Transverse = 4,
        HepaticFlexure = 5,
        Ascending = 6,
        Cecum = 7,
        IlleocecalValve = 8,
    }

    private static readonly Dictionary<IntestineLocation, LocalizedString> intestineLocationStrings = new Dictionary<IntestineLocation, LocalizedString>
    {
        { IntestineLocation.Rectum,          new LocalizedString("IntestineStructureTable", "Rectum") },
        { IntestineLocation.RectumSigmoid,   new LocalizedString("IntestineStructureTable", "RectumSigmoid") },
        { IntestineLocation.Descending,      new LocalizedString("IntestineStructureTable", "Descending") },
        { IntestineLocation.Transverse,      new LocalizedString("IntestineStructureTable", "Transverse") },
        { IntestineLocation.Ascending,       new LocalizedString("IntestineStructureTable", "Ascending") },
        { IntestineLocation.Cecum,           new LocalizedString("IntestineStructureTable", "Cecum") },
        { IntestineLocation.IlleocecalValve, new LocalizedString("IntestineStructureTable", "IlleocecalValve") },
        { IntestineLocation.HepaticFlexure,  new LocalizedString("IntestineStructureTable", "HepaticFlexure") },
        { IntestineLocation.SplenicFlexure,  new LocalizedString("IntestineStructureTable", "SplenicFlexure") },
    };

    public static string GetLocationString(IntestineLocation location) =>
        intestineLocationStrings[location].GetLocalizedString();

    public static string GetLocationStringReference(IntestineLocation location) =>
        intestineLocationStrings[location].GetLocalizedString();

    public enum DiseaseType
    {
        Polyp,
        Diverticula
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
