using System;
using UnityEngine.Localization;

namespace Messages
{
    public class MessageController
    {
        MessageStrings msgStrings;

        /*public string GetMessage(int msgCode, Type type = null)
        {
            if (msgStrings.messages.TryGetValue(msgCode, out string msg))
            {
                return msg;
            }
            else
            {
                msgStrings.messages.TryGetValue(BasicMessages.Unexpected, out msg);
                return msg;
            }
        }

        //TODO keep or not the file type distinction. is it really needed?
        public bool ManageMessageCode(int resultCode, Type fileType = null, CustomDelegates.DefaultDelegate onConfirm = null)
        {
            Debug.LogWarning("ManageResult: " + resultCode);
            switch (resultCode)
            {
                case BasicMessages.None:
                    return true;
                case BasicMessages.Cancel:
                    break;
                case FileErrors.MissingName:
                case FileErrors.FileNotFound:
                case FileErrors.InvalidExtension:
                case FileErrors.FileParsing:
                    uiController.DisplayModalMessage(GetMessage(resultCode, fileType), UI_Controller.ModalType.Error);
                    break;
                case BasicMessages.SearchWithoutResults:
                    uiController.DisplayModalMessage(GetMessage(resultCode, fileType), UI_Controller.ModalType.Warning);
                    break;
                case FileMessages.ChangesNotSaved:
                    uiController.DisplayModalMessage(GetMessage(resultCode, fileType), UI_Controller.ModalType.Warning, onConfirm);
                    break;
                case FileErrors.FileAlreadyExists:
                    uiController.DisplayModalMessage(GetMessage(resultCode, fileType), UI_Controller.ModalType.Confirm, onConfirm);
                    break;
                default:
                    uiController.DisplayModalMessage(GetMessage(BasicMessages.Unexpected, fileType), UI_Controller.ModalType.Error);
                    break;
            }
            return false;
        }*/

        public LocalizedString GetMessageLocalizedString(int msgCode, Type type = null)
        {
            if (msgStrings.messages.TryGetValue(msgCode, out LocalizedString msg))
            {
                return msg;
            }
            else
            {
                msgStrings.messages.TryGetValue(BasicMessages.Unexpected, out msg);
                return msg;
            }
        }

        public bool ManageMessageCode(int resultCode, Type fileType = null, CustomDelegates.DefaultDelegate onConfirm = null)
        {
            //Debug.LogWarning("ManageResult: " + resultCode);
            switch (resultCode)
            {
                case BasicMessages.None:
                    return true;
                case BasicMessages.Cancel:
                    break;
                case FileErrors.MissingName:
                case FileErrors.FileNotFound:
                case FileErrors.InvalidExtension:
                case FileErrors.FileParsing:
                    //uiOverlayController.DisplayModalMessage(GetMessageLocalizedString(resultCode, fileType), UIOverlayController.ModalType.Error);
                    break;
                case BasicMessages.SearchWithoutResults:
                    //uiOverlayController.DisplayModalMessage(GetMessageLocalizedString(resultCode, fileType), UIOverlayController.ModalType.Warning);
                    break;
                case FileMessages.ChangesNotSaved:
                    //uiOverlayController.DisplayModalMessage(GetMessageLocalizedString(resultCode, fileType), UIOverlayController.ModalType.Warning, onConfirm);
                    break;
                case FileErrors.FileAlreadyExists:
                    //uiOverlayController.DisplayModalMessage(GetMessageLocalizedString(resultCode, fileType), UIOverlayController.ModalType.Confirm, onConfirm);
                    break;
                default:
                    //uiOverlayController.DisplayModalMessage(GetMessageLocalizedString(BasicMessages.Unexpected, fileType), UIOverlayController.ModalType.Error);
                    break;
            }
            return false;
        }
    }
}