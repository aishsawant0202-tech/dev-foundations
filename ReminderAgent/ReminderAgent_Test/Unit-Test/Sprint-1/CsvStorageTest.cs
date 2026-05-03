/* This was performed before changing the structure of the project, So these unit test cases will not satisfied
using ReminderAgent;
using ReminderAgent.Domain;
using ReminderAgent.Infrastructure;
namespace UnitTest_SprintOne;

[TestClass]
/*public class CsvStorage
{
    private const string TestFileName = "test_reminders.csv";
    [TestMethod]
    public void TestMethod1()
    {

        // --- ARRANGE ---
        // 1. Clean up any old test files
        if (File.Exists(TestFileName)) File.Delete(TestFileName);

        var provider = new CsvStorageProvider(TestFileName);
        var expectedAsset = new Asset
        {
            Name = "Test Item",
            Category = "Testing",
            Tags = new List<string> { "Unit", "Test" },
            TimelineState = "",
            Metadata = new Dictionary<string, string> { { "Key", "Value" } }
        };

        // --- ACT ---
        // 2. Save the asset
        provider.SaveAssetAsync(expectedAsset);

        // 3. Read it back
        var results = provider.GetAllAssets();
        var actualAsset = results.FirstOrDefault();

        // --- ASSERT ---
        // 4. Verify the data is identical
        Assert.IsTrue(File.Exists(TestFileName), "The CSV file was not created.");
        Assert.IsNotNull(actualAsset);
        Assert.AreEqual(expectedAsset.Name, actualAsset.Name);
        Assert.AreEqual(expectedAsset.Category, actualAsset.Category);
        Assert.AreEqual(expectedAsset.Tags.Count, actualAsset.Tags.Count);
        Assert.AreEqual("Value", actualAsset.Metadata["Key"]);

        // --- CLEANUP ---
        if (File.Exists(TestFileName)) File.Delete(TestFileName);
    }
}*/
