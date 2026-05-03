// Not using reminder-plugin because in later stage added reminder tool

/*using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ReminderAgent.Domain;
using ReminderAgent.Interfaces;
using ReminderAgent.Plugins;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTest_ReminderAgent;

[TestClass]
public class Plugin_unitTest2
{
    private Mock<IStorageProvider> _mockStorage;
    private ReminderPlugin _plugin;

    [TestInitialize]
    public void Setup()
    {
        _mockStorage = new Mock<IStorageProvider>();
        _plugin = new ReminderPlugin(_mockStorage.Object);
    }

    [TestMethod]
    public async Task TestMethod1_FormatsDataCorrectly()
    {
        // ARRANGE: Create fake data based on your Asset structure
        var fakeAssets = new List<Asset>
        {
            new Asset {
                Name = "The Hobbit",
                Category = "Book",
                TimelineState = "Past",
                CreatedAt = new System.DateTime(2023, 10, 01)
            }
        };

        _mockStorage.Setup(s => s.GetAssetsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(fakeAssets);

        // ACT
        var result = await _plugin.GetReminders();

        // ASSERT: Matching your specific formatting: "- Name (Category) | State: X"
        Assert.IsTrue(result.Contains("Here are all the reminders I found"), "Header mismatch");
        Assert.IsTrue(result.Contains("- The Hobbit (Book)"), "Asset line format mismatch");
        Assert.IsTrue(result.Contains("State: Past"), "Timeline state formatting mismatch");
    }

    [TestMethod]
    public async Task TestMethod2_HandlesNoDataGracefully()
    {
        // ARRANGE: Mock an empty database return
        _mockStorage.Setup(s => s.GetAssetsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new List<Asset>());

        // ACT
        var result = await _plugin.GetReminders();

        // ASSERT: Matching your exact code's return string
        // "I searched your records, but I didn't find any reminders yet."
        Assert.IsTrue(result.Contains("I searched your records"), "The 'not found' header message changed");
        Assert.IsTrue(result.Contains("didn't find any reminders"), "The 'not found' body message changed");
    }
} */