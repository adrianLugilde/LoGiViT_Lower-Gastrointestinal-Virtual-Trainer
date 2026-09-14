using UnityEngine.Localization;
using System.Collections.Generic;

namespace AIUtility
{
    public static class AIPromptsHelper
    {
        //System prompts
        private static readonly Dictionary<SystemPrompt, LocalizedString> systemPrompts = new Dictionary<SystemPrompt, LocalizedString>
        {
            { SystemPrompt.Default, new LocalizedString("AITable", "defaultSystemPrompt") },
            { SystemPrompt.CoverageTrainingHelper, new LocalizedString("AITable", "coverageTrainingHelperSystemPrompt") },
            { SystemPrompt.PolypTrainingHelper, new LocalizedString("AITable", "polypTrainingHelperSystemPrompt") },
            { SystemPrompt.CoverageTrainingSupervisor, new LocalizedString("AITable", "coverageTrainingSupervisorSystemPrompt") },
            { SystemPrompt.PolypTrainingSupervisor, new LocalizedString("AITable", "polypTrainingSupervisorSystemPrompt") }
        };

        //Standard application prompts
        private static readonly Dictionary<StandardPrompt, LocalizedString> standardPrompts = new Dictionary<StandardPrompt, LocalizedString>
        {
            { StandardPrompt.ExcesiveSpeedLvlOne, new LocalizedString("AITable", "excesiveSpeedLvlOne") },
            { StandardPrompt.ExcesiveSpeedLvlTwo, new LocalizedString("AITable", "excesiveSpeedLvlTwo") },
            { StandardPrompt.ExcesiveSpeedLvlThree, new LocalizedString("AITable", "excesiveSpeedLvlThree") },
            { StandardPrompt.ExcesiveForwardAngulation, new LocalizedString("AITable", "excesiveForwardAngulation") },
            { StandardPrompt.CecumReached, new LocalizedString("AITable", "cecumReached") },
            { StandardPrompt.ExitingSegment, new LocalizedString("AITable", "exitingSegment") },
            { StandardPrompt.InvalidPolypDetection, new LocalizedString("AITable", "invalidPolypDetection") },
            { StandardPrompt.IncorrectSizeIdentification, new LocalizedString("AITable", "polypIncorrectSizeIdentification") },
            { StandardPrompt.IncorrectLocationIdentification, new LocalizedString("AITable", "polypIncorrectLocationIdentification") },
            { StandardPrompt.IncorrectJNETIdentification, new LocalizedString("AITable", "polypIncorrectJNETIdentification") },
            { StandardPrompt.IncorrectParisIdentification, new LocalizedString("AITable", "polypIncorrectParisIdentification") }
        };

        public enum SystemPrompt
        {
            Default,
            CoverageTrainingHelper,
            CoverageTrainingSupervisor,
            PolypTrainingHelper,
            PolypTrainingSupervisor
        }

        public enum StandardPrompt
        {
            ExcesiveSpeedLvlOne,
            ExcesiveSpeedLvlTwo,
            ExcesiveSpeedLvlThree,
            ExcesiveForwardAngulation,
            CecumReached,
            ExitingSegment,
            InvalidPolypDetection,
            IncorrectSizeIdentification,
            IncorrectLocationIdentification,
            IncorrectJNETIdentification,
            IncorrectParisIdentification,
        }

        public static string GetPromptString<T>(T prompt) where T : struct
        {
            if (prompt is SystemPrompt systemPrompt)
            {
                return systemPrompts[systemPrompt].GetLocalizedString();
            }
            else if (prompt is StandardPrompt standardPrompt)
            {
                return standardPrompts[standardPrompt].GetLocalizedString();
            }
            throw new System.ArgumentException("Invalid prompt type");
        }

        public static string GetPromptStringWithArgs<T>(T prompt, params object[] args) where T : struct
        {
            if (prompt is SystemPrompt systemPrompt)
            {
                return GetLocalizedStringWithArgs(systemPrompts[systemPrompt], args);
            }
            else if (prompt is StandardPrompt standardPrompt)
            {
                return GetLocalizedStringWithArgs(standardPrompts[standardPrompt], args);
            }
            throw new System.ArgumentException("Invalid prompt type");
        }

        private static string GetLocalizedStringWithArgs(LocalizedString localizedString, params object[] args)
        {
            localizedString.Arguments = args;
            return localizedString.GetLocalizedString();
        }
    }
}