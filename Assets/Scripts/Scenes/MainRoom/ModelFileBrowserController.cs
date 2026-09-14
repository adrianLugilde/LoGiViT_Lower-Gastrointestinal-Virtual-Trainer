using System;
using System.IO;
using Messages;
using SimpleFileBrowser;

//TODO OPTIMIZE THIS CLASS
public class ModelFileBrowserController
{
    //public UIOverlayController UIOverlayController;
    public Func<Model> GetCurrentModel;
    public Func<Model> GetSelectedModel;
    public event Action<Model> SetCurrentModel;
    public event Action<Model> OnModelFileImportedCallback;


    public void FileBrowserImport(FileBrowser.OnSuccess onSuccess, FileBrowser.OnCancel onCancel, string fileBrowserTitle, string fileBrowserButtonText)
    {
        if (FileBrowser.ShowLoadDialog(onSuccess, onCancel, FileBrowser.PickMode.Files, false, null, null, fileBrowserTitle, fileBrowserButtonText))
        {
            //UIOverlayController?.OnFileBrowserOpen();
        }
    }

    public void FileBrowserExport(FileBrowser.OnSuccess onSuccess, FileBrowser.OnCancel onCancel, string fileInitialName, string fileBrowserTitle, string fileBrowserButtonText)
    {
        if (FileBrowser.ShowSaveDialog(onSuccess, onCancel, FileBrowser.PickMode.Files, false, null, fileInitialName, fileBrowserTitle, fileBrowserButtonText))
        {
            //UIOverlayController?.OnFileBrowserOpen();
        }
    }

    /// <summary>
    /// Initiates the import process for a model file.
    /// </summary>
    public void ImportModelFile()
    {
        //TODO APPLY LOCALIZATION HERE
        //FileBrowserImport(OnModelFileSelectedForImport, UIOverlayController.OnFileBrowserCancel, "Select the model file to import", "Select");
    }

    /// <summary>
    /// Callback for when a model file is selected for import.
    /// </summary>
    /// <param name="paths">Array of selected file paths.</param>
    public void OnModelFileSelectedForImport(string[] paths)
    {
        var sourcePath = paths[0];
        if (!File.Exists(sourcePath))
        {
            //UIOverlayController.msgController.ManageMessageCode(FileErrors.FileNotFound);
            //UIOverlayController.OnFileBrowserCancel();
            return;
        }

        var currentModel = GetCurrentModel?.Invoke();
        if (currentModel == null)
            SetCurrentModel?.Invoke(new Model());
        currentModel = GetCurrentModel?.Invoke();
        currentModel.Import(sourcePath, OnModelFileImported, currentModel.Overwrite);
    }

    /// <summary>
    /// Callback for when a model file has been successfully imported.
    /// </summary>
    public void OnModelFileImported()
    {
        //UIOverlayController.DisplayModalMessage(UIOverlayController.msgController.GetMessageLocalizedString(FileMessages.FileImported, null));
        OnModelFileImportedCallback?.Invoke(GetCurrentModel?.Invoke());
        //mainMenuUIController.modelEditionScrollArea.UpdateContent(_getCurrentModel?.Invoke());
        //UIOverlayController.OnFileBrowserCancel();
        ApplicationManager.Instance.ClearCurrentModel();
    }

    /// <summary>
    /// Initiates the export process for a model file.
    /// </summary>
    public void ExportModelFile()
    {
        //FileBrowserExport(OnModelFileSelectedForExport, uiOverlayController.OnFileBrowserCancel, FileManager.GetFileNameWithExtension(mainMenuUIController.GetSelectedModel()), "Select the destination folder", "Select");
        //FileBrowserExport(OnModelFileSelectedForExport, UIOverlayController.OnFileBrowserCancel, FileManager.GetFileNameWithExtension(GetSelectedModel()), "Select the destination folder", "Select");
        //TODO APPLY LOCALIZATION HERE
    }

    /// <summary>
    /// Callback for when a destination folder is selected for exporting a model file.
    /// </summary>
    /// <param name="paths">Array of selected file paths.</param>
    public void OnModelFileSelectedForExport(string[] paths)
    {
        var selectedModel = GetSelectedModel();
        //selectedModel.Download(paths[0], UIOverlayController.OnFileExported, selectedModel.Overwrite);
    }
}