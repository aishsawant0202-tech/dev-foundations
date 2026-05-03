using ReminderAgent;
using ReminderAgent.Domain;
using UnitTest_SprintOne;
//using Assert = Xunit.Assert;
//using Xunit;
namespace UnitTest_SprintOne


{
    /// <summary>Unit tests for the KernelTest class verifying asset creation behaviour.</summary>
    [TestClass]
    public class KernelTest
    {
        /// <summary>Verifies that a long user input is truncated to 20 characters followed by ellipsis when creating an asset.</summary>


        [TestMethod]
        public void TestMethod1()
        {
            // Arrange
            string userInput = "This is a very long input that exceeds twenty characters";
            string agentResponse = "Processed by AI";


            // Act
            var asset = ReminderAgentHelperTest.CreateAsset(userInput, agentResponse);


            // Assert
            Assert.AreEqual("This is a very long ...", asset.Name); // Name should be truncated
            Assert.AreEqual("AI Extraction", asset.Category);
            Assert.AreEqual("Present", asset.TimelineState);
            Assert.AreEqual(userInput, asset.Metadata["RawInput"]);
            Assert.AreEqual(agentResponse, asset.Metadata["AgentResponse"]);
        }
        /// <summary>Verifies that a short user input is not truncated and is used as the asset name without modification.</summary>
        [TestMethod]
        public void CreateAsset_ShouldNotTruncateShortUserInput()
        {
            // Arrange
            string userInput = "Short input";
            string agentResponse = "AI says hello";


            // Act
            var asset = ReminderAgentHelperTest.CreateAsset(userInput, agentResponse);


            // Assert
            Assert.AreEqual(userInput, asset.Name); // Name stays the same
            Assert.AreEqual("AI Extraction", asset.Category);
            Assert.AreEqual("Present", asset.TimelineState);
            Assert.AreEqual(userInput, asset.Metadata["RawInput"]);
            Assert.AreEqual(agentResponse, asset.Metadata["AgentResponse"]);
        }
    }
}