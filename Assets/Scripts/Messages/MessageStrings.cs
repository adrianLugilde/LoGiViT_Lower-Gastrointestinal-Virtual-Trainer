using System.Collections.Generic;
using UnityEngine.Localization;


namespace Messages
{

    public class BasicMessages
    {
        public const int InDevelopment = -1;
        public const int None = 0;
        public const int Cancel = 1;
        public const int Unexpected = 2;
        public const int RelativePathsCreation = 3;
        public const int SearchWithoutResults = 4;
        public const int SettingsChangesNotSaved = 5;
    }

    public class FileErrors
    {
        public const int FileNotFound = 100;
        public const int FileAlreadyExists = 101;
        public const int InvalidExtension = 102;
        public const int FileParsing = 103;
        public const int MissingName = 104;
    }


    public class FileMessages
    {
        public const int ConfirmDelete = 200;
        public const int FileDeleted = 201;
        public const int FileImported = 202;
        public const int FileExported = 203;
        public const int ChangesNotSaved = 204;
        public const int FileSaved = 205;
        public const int InputNameAndDescription = 206;
    }

    public class ModelEditorMessages
    {
        public const int ConfirmDiseaseEditionEnabling = 300;
        public const int ConfirmDiseaseEditionDisabling = 301;
        public const int InvalidPresetSaveSelection = 302;
        public const int ConfirmPresetPreviewApplyOnExit = 303;
        public const int ConfirmDiseasePlacementLocation = 304;
        public const int PresetChangesNotSaved = 305;
        public const int ModelChangesNotSaved = 306;
        public const int ConfirmExitEditor = 307;
    }

    public class DisplayModeMessages
    {
        public const int VRDisplayModeNotAvailable = 400;
        public const int ConfirmSaveForDisplayModeChange = 401;
    }

    public class TrainingMessages
    {
        public const int InvalidTimeLimit = 500;
        public const int ConfirmTrainingRestart = 501;
        public const int TrainingStarted = 502;
        public const int TrainingPaused = 503;
        public const int ConfirmExitRoom = 504;
        public const int ConfirmSaveTrainingResults = 505;
        public const int TrainingResultsSaved = 506;
        public const int TrainingResultsNotSaved = 507;
    }

    public class PolypTrainingMessages
    {
        public const int NoPolypsDetected = 600;
        public const int InvalidPolypDetection = 601;
        public const int PolypDetected = 602;
    }

    public class AIMessages
    {
        public const int AITTSDisabled = 700;
        public const int AITTSEnabled = 701;
        public const int RecordingAISTTInput = 702;
    }

    public class MessageStrings
    {
        public LocalizedString GetMessageLocalizedString(int msgCode)
        {
            if (messages.TryGetValue(msgCode, out LocalizedString msg))
            {
                return msg;
            }
            else
            {
                messages.TryGetValue(BasicMessages.Unexpected, out msg);
                return msg;
            }
        }

        public readonly Dictionary<int, LocalizedString> messages = new Dictionary<int, LocalizedString>()
        {
            [BasicMessages.InDevelopment] = new LocalizedString("MessagesTable", "inDevelopment"),
            [BasicMessages.Unexpected] = new LocalizedString("MessagesTable", "unexpected"),
            [BasicMessages.SearchWithoutResults] = new LocalizedString("MessagesTable", "searchWithoutResults"),
            [BasicMessages.RelativePathsCreation] = new LocalizedString("MessagesTable", "relativePathsCreation"),
            [BasicMessages.SettingsChangesNotSaved] = new LocalizedString("MessagesTable", "settingsChangesNotSaved"),
            [FileErrors.FileNotFound] = new LocalizedString("MessagesTable", "fileNotFound"),
            [FileErrors.InvalidExtension] = new LocalizedString("MessagesTable", "invalidExtension"),
            [FileErrors.FileParsing] = new LocalizedString("MessagesTable", "fileParsing"),
            [FileErrors.MissingName] = new LocalizedString("MessagesTable", "missingName"),
            [FileErrors.FileAlreadyExists] = new LocalizedString("MessagesTable", "fileAlreadyExists"),
            [FileMessages.ChangesNotSaved] = new LocalizedString("MessagesTable", "changesNotSaved"),
            [FileMessages.ConfirmDelete] = new LocalizedString("MessagesTable", "confirmDelete"),
            [FileMessages.FileDeleted] = new LocalizedString("MessagesTable", "fileDeleted"),
            [FileMessages.FileImported] = new LocalizedString("MessagesTable", "fileImported"),
            [FileMessages.FileExported] = new LocalizedString("MessagesTable", "fileExported"),
            [FileMessages.FileSaved] = new LocalizedString("MessagesTable", "fileSaved"),
            [FileMessages.InputNameAndDescription] = new LocalizedString("MessagesTable", "inputNameAndDescription"),
            [ModelEditorMessages.ConfirmDiseaseEditionEnabling] = new LocalizedString("MessagesTable", "confirmDiseaseEditionEnabling"),
            [ModelEditorMessages.ConfirmDiseaseEditionDisabling] = new LocalizedString("MessagesTable", "confirmDiseaseEditionDisabling"),
            [ModelEditorMessages.InvalidPresetSaveSelection] = new LocalizedString("MessagesTable", "invalidPresetSaveSelection"),
            [ModelEditorMessages.ConfirmPresetPreviewApplyOnExit] = new LocalizedString("MessagesTable", "confirmPresetPreviewApplyOnExit"),
            [ModelEditorMessages.ConfirmDiseasePlacementLocation] = new LocalizedString("MessagesTable", "confirmDiseasePlacementLocation"),
            [ModelEditorMessages.PresetChangesNotSaved] = new LocalizedString("MessagesTable", "presetChangesNotSaved"),
            [ModelEditorMessages.ModelChangesNotSaved] = new LocalizedString("MessagesTable", "modelChangesNotSaved"),
            [ModelEditorMessages.ConfirmExitEditor] = new LocalizedString("MessagesTable", "confirmExitEditor"),
            [DisplayModeMessages.VRDisplayModeNotAvailable] = new LocalizedString("MessagesTable", "vrDisplayModeNotAvailable"),
            [DisplayModeMessages.ConfirmSaveForDisplayModeChange] = new LocalizedString("MessagesTable", "confirmSaveForDisplayModeChange"),
            [TrainingMessages.InvalidTimeLimit] = new LocalizedString("MessagesTable", "invalidTimeLimit"),
            [TrainingMessages.ConfirmTrainingRestart] = new LocalizedString("MessagesTable", "confirmTrainingRestart"),
            [TrainingMessages.TrainingStarted] = new LocalizedString("MessagesTable", "trainingStarted"),
            [TrainingMessages.TrainingPaused] = new LocalizedString("MessagesTable", "trainingPaused"),
            [TrainingMessages.ConfirmExitRoom] = new LocalizedString("MessagesTable", "confirmExitRoom"),
            [TrainingMessages.ConfirmSaveTrainingResults] = new LocalizedString("MessagesTable", "confirmSaveTrainingResults"),
            [TrainingMessages.TrainingResultsSaved] = new LocalizedString("MessagesTable", "trainingResultsSaved"),
            [TrainingMessages.TrainingResultsNotSaved] = new LocalizedString("MessagesTable", "trainingResultsNotSaved"),
            [PolypTrainingMessages.NoPolypsDetected] = new LocalizedString("MessagesTable", "noPolypsDetected"),
            [PolypTrainingMessages.InvalidPolypDetection] = new LocalizedString("MessagesTable", "invalidPolypDetection"),
            [AIMessages.AITTSEnabled] = new LocalizedString("AITable", "AITTSEnabled"),
            [AIMessages.AITTSDisabled] = new LocalizedString("AITable", "AITTSDisabled"),
            [AIMessages.RecordingAISTTInput] = new LocalizedString("AITable", "listeningSTTInput")
        };
    }
}