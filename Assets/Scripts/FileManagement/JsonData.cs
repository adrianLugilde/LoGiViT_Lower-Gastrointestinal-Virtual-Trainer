using Messages;
using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;

[Serializable]
public abstract class JsonData
{
    [SerializeField] public string FileName = "";
    [NonSerialized] public string PreviousName = "";
    [HideInInspector, SerializeField] public long FileID = 0;

    /// <summary>
    /// The folder path where files of this type are stored.
    /// Excluded from serialization - this is configuration, not data.
    /// </summary>
    [JsonIgnore]
    public abstract string DataPath { get; }
    
    /// <summary>
    /// The file extension for this type (e.g., ".tc", ".sp").
    /// Excluded from serialization - this is configuration, not data.
    /// </summary>
    [JsonIgnore]
    public abstract string FileExtension { get; }
    
    /// <summary>
    /// Optional JSON serialization settings for this type.
    /// Excluded from serialization to avoid circular references.
    /// </summary>
    [JsonIgnore]
    public virtual JsonSerializerSettings JsonSettings => null;

    /// <summary>
    /// Gets the full file path including folder and extension
    /// </summary>
    public virtual string GetFullPath(string overrideFileName = null)
    {
        var name = overrideFileName ?? FileName;
        return Path.Combine(DataPath, name + FileExtension);
    }

    public virtual string ToJson(JsonSerializerSettings jsonSerializerSettings = null, bool indented = true)
    {
        var settings = jsonSerializerSettings ?? JsonSettings;
        return indented ? JsonConvert.SerializeObject(this, Formatting.Indented, settings) :
            JsonConvert.SerializeObject(this, settings);
    }

    protected virtual int FromJson(string jsonContents)
    {
        try
        {
            JsonConvert.PopulateObject(jsonContents, this, JsonSettings);
            PreviousName = FileName;
        }
        catch
        {
            jsonContents = FileManager.SetSplineVertex(jsonContents);
            try
            {
                JsonConvert.PopulateObject(jsonContents, this, JsonSettings);
                PreviousName = FileName;
            }
            catch (Exception ex)
            {
                Debug.Log("ERROR FROM JSON: " + ex.Message);
                return FileErrors.FileParsing;
            }
        }
        return BasicMessages.None;
    }

    /// <summary>
    /// Saves this item to disk, optionally asking for overwrite confirmation
    /// </summary>
    public virtual bool Save(FileManager.OnFileOperationDone onSaveResult = null, 
        CustomDelegates.OnConfirm onOverwriteConfirmed = null, 
        string destPath = null)
    {
        var filePath = destPath ?? GetFullPath();
        
        if (File.Exists(filePath))
        {
            if (IsSameFileID(filePath))
            {
                // Same file, just save it
                WriteToFile(filePath, onSaveResult);
                return true;
            }
            else if (onSaveResult != null && onOverwriteConfirmed != null)
            {
                // Different file with same name, ask for confirmation
                FileManager.MsgController.ManageMessageCode(FileErrors.FileAlreadyExists,
                    GetType(),
                    new CustomDelegates.DefaultDelegate(() => onOverwriteConfirmed(filePath, onSaveResult)));
                return false;
            }
            else
            {
                // Overwrite without confirmation
                WriteToFile(filePath, null);
                return true;
            }
        }
        else
        {
            WriteToFile(filePath, onSaveResult);
            return true;
        }
    }

    /// <summary>
    /// Writes this item to a file
    /// </summary>
    protected virtual void WriteToFile(string destPath, FileManager.OnFileOperationDone onSave = null)
    {
        var fileContent = ToJson();
        
        // Delete old file if name changed
        if (!string.IsNullOrEmpty(PreviousName) && PreviousName != FileName)
        {
            var oldPath = GetFullPath(PreviousName);
            if (File.Exists(oldPath))
                File.Delete(oldPath);
        }
        
        if (FileManager.IsEncryptationEnabled)
            fileContent = FileManager.SetSplineVertex(fileContent);
            
        File.WriteAllText(destPath, fileContent);
        PreviousName = FileName;
        
        onSave?.Invoke();
    }

    /// <summary>
    /// Reads this item from a file path
    /// </summary>
    public virtual int ReadFromFile(string sourcePath)
    {
        Debug.Log($"Reading from file: {sourcePath}");
        try
        {
            var content = File.ReadAllText(sourcePath);
            return FromJson(content);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to read file: {sourcePath}. Error: {e.Message}");
            return FileErrors.FileParsing;
        }
    }

    /// <summary>
    /// Loads from source and saves to default location
    /// </summary>
    public virtual void Load(string sourcePath, FileManager.OnFileOperationDone onFileLoaded = null, 
        CustomDelegates.OnConfirm onOverwriteConfirmed = null)
    {
        var result = ReadFromFile(sourcePath);
        if (!FileManager.MsgController.ManageMessageCode(result)) 
            return;
            
        if (onFileLoaded == null && onOverwriteConfirmed == null) 
            return;
            
        var destPath = GetFullPath();
        if (File.Exists(destPath))
        {
            FileManager.MsgController.ManageMessageCode(FileErrors.FileAlreadyExists,
                GetType(),
                new CustomDelegates.DefaultDelegate(() => onOverwriteConfirmed(destPath, onFileLoaded)));
        }
        else
        {
            WriteToFile(destPath, onFileLoaded);
        }
    }

    /// <summary>
    /// Downloads (saves) to a specific destination path
    /// </summary>
    public virtual void Download(string destPath, FileManager.OnFileOperationDone onFileDownloaded = null, 
        CustomDelegates.OnConfirm onOverwriteConfirmed = null)
    {
        if (File.Exists(destPath))
        {
            FileManager.MsgController.ManageMessageCode(FileErrors.FileAlreadyExists,
                GetType(),
                new CustomDelegates.DefaultDelegate(() => onOverwriteConfirmed(destPath, onFileDownloaded)));
        }
        else
        {
            WriteToFile(destPath, onFileDownloaded);
        }
    }

    /// <summary>
    /// Overwrites file at destination without confirmation
    /// </summary>
    public virtual void Overwrite(string destPath, FileManager.OnFileOperationDone onSaveResult = null)
    {
        WriteToFile(destPath, onSaveResult);
    }

    /// <summary>
    /// Deletes the file at the given path
    /// </summary>
    public virtual void Delete(string filePath, FileManager.OnFileOperationDone onFileDeleted = null)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
        onFileDeleted?.Invoke();
    }

    /// <summary>
    /// Checks if a file at the given path has the same FileID as this item
    /// </summary>
    protected virtual bool IsSameFileID(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            // Quick parse just for FileID
            var tempObj = JsonConvert.DeserializeObject<FileIdHolder>(content);
            return tempObj?.FileID == FileID;
        }
        catch
        {
            return false;
        }
    }

    // Helper class for quick FileID parsing
    private class FileIdHolder
    {
        public long FileID { get; set; }
    }
}