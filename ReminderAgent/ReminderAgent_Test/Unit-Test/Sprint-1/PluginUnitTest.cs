
// Not using reminder-plugin because in later stage added reminder tool

/*using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Plugins;
using ReminderAgent.Interfaces;
using ReminderAgent.Domain;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
/*
namespace UnitTest_SprintOne
{
    // --- 1. THE MOCK (The "Fake Cabinet") ---
    // We put this here so the test file has everything it needs in one place.
    
    public class MockStorageProvider : IStorageProvider
    {
        public List<Asset> SavedAssets = new List<Asset>();

        public async Task<bool> SaveAssetAsync(Asset asset)
        {
            SavedAssets.Add(asset);
            return await Task.FromResult(true);
        }
        // Adding THIS NEW METHOD TO FIX THE ERROR:
        public Task<IEnumerable<Asset>> GetAssetsAsync(
            string? category = null,
            string? tag = null,
            string? timelineState = null)
        {
            // For a mock, we are just returning an empty list or a small fake list
            IEnumerable<Asset> mockList = new List<Asset>();
            return Task.FromResult(mockList);
        }
    }

    // --- 2. THE TEST CLASS (The "Training School") ---
    [TestClass]
    public class PluginUnitTest
    {
        private MockStorageProvider _mockStorage = null!;
        private ReminderPlugin _plugin = null!;

        [TestInitialize]
        public void Setup()
        {
            // We create a fresh fake cabinet and a fresh plugin for every test
            _mockStorage = new MockStorageProvider();
            _plugin = new ReminderPlugin(_mockStorage);
        }

        [TestMethod]
        public async Task CreateAsset_WhenFutureContext_ShouldMapToFutureState()
        {
            // --- ARRANGE ---
            string name = "Graduation Party";
            string time = "next month";

            // --- ACT ---
            await _plugin.CreateAsset(
                name: name,
                category: "Event",
                timeContext: time,
                userInput: "",
                userExperience: ""
            );

            // --- ASSERT ---
            var savedAsset = _mockStorage.SavedAssets.FirstOrDefault();

            Assert.IsNotNull(savedAsset, "The asset was not saved to the mock storage.");
            Assert.AreEqual("Future", savedAsset.TimelineState);
            Assert.AreEqual(name, savedAsset.Name);
        }

        [TestMethod]
        public async Task CreateAsset_WhenPastContext_ShouldMapToPastState()
        {
            // --- ARRANGE ---
            string name = "Old Trip to Berlin";
            string time = "last year";

            // --- ACT ---
            await _plugin.CreateAsset(
                name: name,
                category: "Travel",
                timeContext: time,
                userInput: "",
                userExperience: ""
            );

            // --- ASSERT ---
            var savedAsset = _mockStorage.SavedAssets.FirstOrDefault();

            Assert.IsNotNull(savedAsset, "The asset was not saved to the mock storage.");
            Assert.AreEqual("Past", savedAsset.TimelineState);
        }
    }
}*/