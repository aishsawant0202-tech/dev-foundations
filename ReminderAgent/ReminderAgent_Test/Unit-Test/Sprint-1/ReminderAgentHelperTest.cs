using System.Collections.Generic;
using ReminderAgent.Domain;

namespace UnitTest_SprintOne
{
    /// <summary>Helper class that creates Asset objects from user input and AI response for use in unit tests.</summary>
    public static class ReminderAgentHelperTest
    {
        /// <summary>Creates an Asset with the given user input and agent response, truncating the name to 20 characters if it exceeds that length.</summary>
        /// <param name="userInput">The raw input string provided by the user.</param>
        /// <param name="agentResponse">The response string returned by the AI agent.</param>
        /// <returns>A new Asset with the name, category, timeline state, and metadata populated from the provided inputs.</returns>/// <summary>Creates an Asset with the given user input and agent response, truncating the name to 20 characters if it exceeds that length.</summary>
        /// <param name="userInput">The raw input string provided by the user.</param>
        /// <param name="agentResponse">The response string returned by the AI agent.</param>
        /// <returns>A new Asset with the name, category, timeline state, and metadata populated from the provided inputs.</returns>  
        public static Asset CreateAsset(string userInput, string agentResponse)
        {
            return new Asset
            {
                Name = userInput.Length > 20 ? userInput.Substring(0, 20) + "..." : userInput,
                Category = "AI Extraction",
                TimelineState = "Present",
                Metadata = new Dictionary<string, string>
                {
                    { "RawInput", userInput },
                    { "AgentResponse", agentResponse }
                }
            };
        }
    }
}




