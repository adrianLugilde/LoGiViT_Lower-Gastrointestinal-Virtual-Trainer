using System;
using System.IO;
using SimpleFileBrowser;

namespace ModelEditor
{

    public class PresetsFileBrowserController
    {
        public Func<SplinePreset> GetCurrentPreset;
        public Func<SplinePreset> GetSelectedPreset;
        public Action<SplinePreset> SetCurrentPreset;
        public Action<SplinePreset> OnPresetFileImportedCallback;
        public Action OnFileBrowserOpen;
        public Action OnFileBrowserCancel;
        public Action OnFileNotFound;
        public FileManager.OnFileOperationDone OnPresetFileImported;
        public FileManager.OnFileOperationDone OnPresetFileExported;


        public void FileBrowserImport(FileBrowser.OnSuccess onSuccess, FileBrowser.OnCancel onCancel, string fileBrowserTitle, string fileBrowserButtonText)
        {
            if (FileBrowser.ShowLoadDialog(onSuccess, onCancel, FileBrowser.PickMode.Files, false, null, null, fileBrowserTitle, fileBrowserButtonText))
            {
                OnFileBrowserOpen?.Invoke();
            }
        }

        public void FileBrowserExport(FileBrowser.OnSuccess onSuccess, FileBrowser.OnCancel onCancel, string fileInitialName, string fileBrowserTitle, string fileBrowserButtonText)
        {
            if (FileBrowser.ShowSaveDialog(onSuccess, onCancel, FileBrowser.PickMode.Files, false, null, fileInitialName, fileBrowserTitle, fileBrowserButtonText))
            {
                OnFileBrowserOpen?.Invoke();
            }
        }

        /// <summary>
        /// Initiates the import process for a model file.
        /// </summary>
        public void ImportModelFile()
        {
            //TODO APPLY LOCALIZATION HERE
            FileBrowserImport(OnPresetFileSelectedForImport, () => OnFileBrowserCancel?.Invoke(), "Select the model file to import", "Select");
        }

        /// <summary>
        /// Callback for when a model file is selected for import.
        /// </summary>
        /// <param name="paths">Array of selected file paths.</param>
        public void OnPresetFileSelectedForImport(string[] paths)
        {
            var sourcePath = paths[0];
            if (!File.Exists(sourcePath))
            {
                OnFileNotFound?.Invoke();
                //UIOverlayController.msgController.ManageMessageCode(FileErrors.FileNotFound);
                //UIOverlayController.OnFileBrowserCancel();
                return;
            }

            var currentPreset = GetCurrentPreset?.Invoke();
            if (currentPreset == null)
                SetCurrentPreset?.Invoke(new SplinePreset());
            currentPreset = GetCurrentPreset?.Invoke();
            currentPreset.Import(sourcePath, OnPresetFileImported, currentPreset.Overwrite);
        }

        /// <summary>
        /// Callback for when a model file has been successfully imported.
        /// </summary>
        /*public void OnPresetFileImported()
        {
            UIOverlayController.DisplayModalMessage(UIOverlayController.msgController.GetMessage(FileMessages.FileImported, null));
            OnPresetFileImportedCallback?.Invoke(GetCurrentPreset?.Invoke());
            //mainMenuUIController.modelEditionScrollArea.UpdateContent(_getCurrentModel?.Invoke());
            UIOverlayController.OnFileBrowserCancel();
            ApplicationManager.Instance.ClearCurrentModel();
        }*/

        /// <summary>
        /// Initiates the export process for a model file.
        /// </summary>
        public void ExportModelFile()
        {
            //FileBrowserExport(OnModelFileSelectedForExport, uiOverlayController.OnFileBrowserCancel, FileManager.GetFileNameWithExtension(mainMenuUIController.GetSelectedModel()), "Select the destination folder", "Select");
            var preset = GetSelectedPreset();
            var fileNameWithExt = preset.FileName + preset.FileExtension;
            FileBrowserExport(OnModelFileSelectedForExport, () => OnFileBrowserCancel?.Invoke(), fileNameWithExt, "Select the destination folder", "Select");
            //TODO APPLY LOCALIZATION HERE
        }

        /// <summary>
        /// Callback for when a destination folder is selected for exporting a model file.
        /// </summary>
        /// <param name="paths">Array of selected file paths.</param>
        public void OnModelFileSelectedForExport(string[] paths)
        {
            var selectedModel = GetSelectedPreset();
            selectedModel.Download(paths[0], OnPresetFileExported, selectedModel.Overwrite);
        }
    }
}
