using ReminderAgent;
using ReminderAgent.Domain;
namespace UnitTest_SprintOne
{
    /// <summary>Unit tests for the Asset entity verifying default initialisation and data storage behaviour.</summary>
    [TestClass]
    public sealed class AssetTest
    {
        /// <summary>Verifies that a new Asset initialises with a valid GUID, non-null collections, and a default TimelineState of Present.</summary>
        [TestMethod]
        public void TestMethod1()
        {
            // --- ARRANGE & ACT ---
            var asset = new Asset();
            // --- ASSERT ---
            Assert.IsNotNull(asset);
            // Verify Guid is not empty
            Assert.AreNotEqual(Guid.Empty, asset.Id);

            // Verify collections are initialized (prevents NullReferenceException)
            Assert.IsNotNull(asset.Tags);
            Assert.IsNotNull(asset.Metadata);

            // Verify default timeline state
            Assert.AreEqual("Present", asset.TimelineState);
        }

        /// <summary>Verifies that name, tags, and metadata values are correctly stored and retrieved from an Asset instance.</summary>
        [TestMethod]
        public void TestMethod2()
        {
            // --- ARRANGE ---
            var asset = new Asset();
            var testName = "Buy Milk";

            // --- ACT ---
            asset.Name = testName;
            asset.Tags.Add("Grocery");
            asset.Metadata["Location"] = "Rewe";

            // --- ASSERT ---
            Assert.AreEqual(testName, asset.Name);
            Assert.Contains("Grocery", asset.Tags);
            Assert.IsTrue(asset.Metadata.ContainsKey("Location"));
            Assert.AreEqual("Rewe", asset.Metadata["Location"]);
        }

    }
}
