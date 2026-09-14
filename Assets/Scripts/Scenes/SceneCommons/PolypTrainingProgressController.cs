using UnityEditor;
using UnityEngine;
using static AIUtility.AIPromptsHelper;
using System;

public class PolypTrainingProgressController : TrainingProgressController
{
    private string invalidPolypDetectionString = "";
    public event Action InvalidPolypDetection;

    protected override void UpdatePromptStrings()
    {
        base.UpdatePromptStrings();
        invalidPolypDetectionString = GetPromptString(StandardPrompt.InvalidPolypDetection);
    }

    public void OnInvalidPolypDetection()
    {
        SendLLMRequest?.Invoke(invalidPolypDetectionString, supervisorSystemPromptString);
    }

    public void OnIncorrectPolypIdentification(PolypTraining.PolypIdentificationAnswer polypIdentificationAnswer)
    {
        if (!polypIdentificationAnswer.IsSizeCorrect)
        {
            SendSupervisorRequest(StandardPrompt.IncorrectSizeIdentification, polypIdentificationAnswer.Polyp.GetSizeString(), polypIdentificationAnswer.Size);
        }
        if (!polypIdentificationAnswer.IsLocationCorrect)
        {
            SendSupervisorRequest(StandardPrompt.IncorrectLocationIdentification, polypIdentificationAnswer.Polyp.Location, polypIdentificationAnswer.Location);
        }
        if (!polypIdentificationAnswer.IsJnetClassCorrect)
        {
            SendSupervisorRequest(StandardPrompt.IncorrectJNETIdentification, polypIdentificationAnswer.Polyp.JnetClass, polypIdentificationAnswer.JnetClass);
        }
        if (!polypIdentificationAnswer.IsParisClassCorrect)
        {
            SendSupervisorRequest(StandardPrompt.IncorrectParisIdentification, polypIdentificationAnswer.Polyp.ParisClass, polypIdentificationAnswer.ParisClass);
        }
    }

    private void SendSupervisorRequest(StandardPrompt prompt, params object[] args)
    {
        SendLLMRequest?.Invoke(GetPromptStringWithArgs(prompt, args), supervisorSystemPromptString);
    }

    private void TestInvalidPolypDetection()
    {
        OnInvalidPolypDetection();
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(PolypTrainingProgressController))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PolypTrainingProgressController myScript = (PolypTrainingProgressController)target;
            if (GUILayout.Button("TestInvalidPolypDetection"))
            {
                myScript.TestInvalidPolypDetection();
            }
        }
    }
#endif
}