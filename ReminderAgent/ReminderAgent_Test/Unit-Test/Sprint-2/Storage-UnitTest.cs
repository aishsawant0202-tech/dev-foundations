using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Domain;
using ReminderAgent.Infrastructure;
/*
namespace UnitTest_ReminderAgent;

[TestClass]
public class Storage_UnitTest
{
    [TestMethod]
    public async Task ShouldSetPathInDocumentationFolderAsync()
    {
        // 1. Arrange
        string test_FileName = "test_reminders.csv";
        var storage = new CsvStorageProvider(test_FileName);
        string fullPath = storage.GetFilePath();
        Console.WriteLine($"Full file path: {fullPath}");

        // 2. Clean Up: Ensure a fresh start to avoid header errors
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            Name = "Milk",
            Category = "Groceries",
            TimelineState = "Present",
            CreatedAt = DateTime.Now
        };

        // 3. Act
        bool success = await storage.SaveAssetAsync(asset);

        // 4. Assert
        Assert.IsTrue(success, "SaveAssetAsync should return true");
        Assert.IsTrue(File.Exists(fullPath), $"File should exist at {fullPath}");
        

        // Verify the folder name is 'UserData' as per your dynamic path logic
        Assert.Contains("UserData", fullPath);


    }
    [TestMethod]
    public async Task ShouldHandleMetadataCorrectly()
    {
        // 1. Arrange
        string testFileName = "test_sprint2.csv";
        var storage = new CsvStorageProvider(testFileName);
        string fullPath = storage.GetFilePath();

        // 2. THE FIX: Delete the corrupted file if it exists
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        var asset = new Asset
        {
            Name = "Buy Milk",
            Category = "Tasks",
            Metadata = new Dictionary<string, string> { { "Priority", "High" }, { "Store", "Lidl" } }
        };

        // Act
        bool success = await storage.SaveAssetAsync(asset);

        // Assert
        Assert.IsTrue(success);
    }
}*/
