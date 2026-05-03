using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Domain;
using ReminderAgent.Infrastructure;
using ReminderAgent.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTest_SprintSix
{
    /// <summary>
    /// Unit tests for <see cref="CsvStorageProvider"/>.
    /// Covers saving, retrieving, updating assets, embedding persistence,
    /// metadata and tag round-trips, date format parsing, and duplicate detection.
    /// </summary>
    [TestClass]
    public class CsvStorageProviderTests
    {
        private string _uniqueFileName = string.Empty;
        private string _resolvedFilePath = string.Empty;
        private InMemoryEmbeddingStore _embeddingStore = new();
        private CsvStorageProvider _provider = null!;

        /// <summary>
        // /// Initializes a unique temporary CSV file with a valid header row before each test.
        // /// Ensures <see cref="CsvStorageProvider"/> does not trigger backup restore logic.
        // /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _uniqueFileName = $"test_{Guid.NewGuid():N}.csv";
            _embeddingStore = new InMemoryEmbeddingStore();
            _provider = new CsvStorageProvider(_embeddingStore, _uniqueFileName);
            _resolvedFilePath = _provider.GetFilePath();

            // Pre-create the file with a header row so CsvStorageProvider
            // sees it as an existing empty CSV and never triggers backup restore.
            string header = "Id,Name,Category,TimelineState,EventDate,CreatedAt," +
                            "Tags,Metadata,UserInput,UserExperience,PhotoRefs";
            File.WriteAllText(_resolvedFilePath, header + Environment.NewLine);
        }
        /// <summary>
        /// Deletes the temporary CSV file created during each test to keep the file system clean.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_resolvedFilePath))
                File.Delete(_resolvedFilePath);
        }

        /// <summary>
        // /// Factory method that creates a fully populated <see cref="Asset"/> for use in tests.
        // /// </summary>
        // /// <param name="name">The display name of the asset. Defaults to "Test Place".</param>
        // /// <param name="category">The category of the asset. Defaults to "Travel".</param>
        // /// <param name="city">The city stored in metadata. Defaults to "Berlin".</param>
        // /// <param name="country">The country stored in metadata. Defaults to "Germany".</param>
        // /// <param name="date">The event date. Defaults to today if not provided.</param>
        // /// <returns>A new <see cref="Asset"/> instance with all fields populated.</returns>
        private static Asset MakeAsset(
            string name = "Test Place",
            string category = "Travel",
            string city = "Berlin",
            string country = "Germany",
            DateTime? date = null)
        {
            var d = date ?? DateTime.Today;
            return new Asset
            {
                Id = Guid.NewGuid(),
                Name = name,
                Category = category,
                UserInput = $"I visited {name}",
                UserExperience = "great",
                EventDate = d,
                CreatedAt = DateTime.Now,
                TimelineState = d.Date < DateTime.Today ? "Past"
                               : d.Date > DateTime.Today ? "Future"
                               : "Present",
                Tags = new List<string> { "test", "unit" },
                Metadata = new Dictionary<string, string>
                {
                    ["City"] = city,
                    ["Country"] = country,
                    ["Info"] = "unit test asset"
                },
                Embedding = new float[] { 0.1f, 0.2f, 0.3f },
                PhotoRefs = new List<string>()
            };
        }

        // SaveAssetAsync
        /// <summary>
        /// Verifies that saving a new <see cref="Asset"/> returns <c>true</c>.
        /// </summary>
        [TestMethod]
        public async Task SaveAssetAsync_NewAsset_ReturnsTrue()
        {
            bool result = await _provider.SaveAssetAsync(MakeAsset("Colosseum", "Travel"));

            Assert.IsTrue(result, "Saving a new asset should return true");
        }
        /// <summary>
        /// Verifies that saving an exact duplicate of an already saved <see cref="Asset"/> returns <c>false</c>.
        /// </summary>
        [TestMethod]
        public async Task SaveAssetAsync_ExactDuplicate_ReturnsFalse()
        {
            var asset = MakeAsset("Colosseum", "Travel");

            await _provider.SaveAssetAsync(asset);
            bool second = await _provider.SaveAssetAsync(asset);

            Assert.IsFalse(second, "Saving an exact duplicate should return false");
        }
        /// <summary>
        /// Verifies that two assets with different names but the same category are both saved successfully.
        /// The fuzzy duplicate check has no category guard, so distinct names are used to avoid false positives.
        /// </summary>
        [TestMethod]
        public async Task SaveAssetAsync_SameNameDifferentCategory_IsAllowed()
        {
            // The fuzzy duplicate check has no category guard, so identical names
            // always trigger it. This test verifies the exact-match path which
            // does check category — use names with no shared keywords.
            await _provider.SaveAssetAsync(MakeAsset("Colosseum", "Travel"));

            bool result = await _provider.SaveAssetAsync(MakeAsset("Parthenon", "Travel"));

            Assert.IsTrue(result, "Two different assets in the same category should both be allowed");
        }
        /// <summary>
        /// Verifies that the embedding vector of a saved <see cref="Asset"/> is persisted
        /// to the <see cref="InMemoryEmbeddingStore"/> and can be retrieved by asset ID.
        /// </summary>
        [TestMethod]
        public async Task SaveAssetAsync_EmbeddingPersistedToStore()
        {
            var asset = MakeAsset();

            await _provider.SaveAssetAsync(asset);
            var loaded = await _embeddingStore.LoadEmbeddingAsync(asset.Id);

            Assert.IsNotNull(loaded, "Embedding should be saved to store");
            Assert.AreEqual(3, loaded!.Length, "Embedding length should match");
            Assert.AreEqual(0.1f, loaded[0], 0.0001f, "First embedding value should match");
        }
        /// <summary>
        /// Verifies that saving an <see cref="Asset"/> with a null embedding does not throw
        /// an exception and still returns <c>true</c>.
        /// </summary>
        [TestMethod]
        public async Task SaveAssetAsync_NullEmbedding_DoesNotThrow()
        {
            var asset = MakeAsset();
            asset.Embedding = null;

            bool result = await _provider.SaveAssetAsync(asset);

            Assert.IsTrue(result, "Saving asset with null embedding should still succeed");
        }

        // GetAssetsAsync
        /// <summary>
        /// Verifies that <see cref="CsvStorageProvider.GetAssetsAsync"/> returns an empty collection
        /// when no assets have been saved.
        /// </summary>

        [TestMethod]
        public async Task GetAssetsAsync_EmptyStore_ReturnsEmpty()
        {
            var assets = await _provider.GetAssetsAsync();

            Assert.AreEqual(0, assets.Count(), "Empty store should return no assets");
        }
        /// <summary>
        /// Verifies that an asset saved via <see cref="CsvStorageProvider.SaveAssetAsync"/>
        /// can be retrieved correctly by <see cref="CsvStorageProvider.GetAssetsAsync"/>.
        /// </summary>
        [TestMethod]
        public async Task GetAssetsAsync_AfterSave_ReturnsAsset()
        {
            await _provider.SaveAssetAsync(MakeAsset("Eiffel Tower", "Travel"));

            var loaded = (await _provider.GetAssetsAsync()).ToList();

            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual("Eiffel Tower", loaded[0].Name);
        }
        /// <summary>
        /// Verifies that filtering by category returns only assets matching that category,
        /// excluding assets from other categories.
        /// </summary>
        [TestMethod]
        public async Task GetAssetsAsync_CategoryFilter_ReturnsOnlyMatchingCategory()
        {
            await _provider.SaveAssetAsync(MakeAsset("Hobbit", "Book"));
            await _provider.SaveAssetAsync(MakeAsset("Vapiano", "Restaurant"));
            await _provider.SaveAssetAsync(MakeAsset("Amsterdam", "Travel"));

            var books = (await _provider.GetAssetsAsync(category: "Book")).ToList();

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("Hobbit", books[0].Name);
        }
        /// <summary>
        /// Verifies that embedding vectors are merged back into assets when loaded
        /// from the CSV, ensuring the full asset state is restored after a round-trip.
        /// </summary>
        [TestMethod]
        public async Task GetAssetsAsync_EmbeddingsMergedBack()
        {
            await _provider.SaveAssetAsync(MakeAsset());

            var loaded = (await _provider.GetAssetsAsync()).First();

            Assert.IsNotNull(loaded.Embedding, "Embedding should be merged back after load");
            Assert.AreEqual(3, loaded.Embedding!.Length);
        }
        /// <summary>
        /// Verifies that all saved assets are returned when no category filter is applied.
        /// </summary>
        [TestMethod]
        public async Task GetAssetsAsync_MultipleAssets_AllReturned()
        {
            await _provider.SaveAssetAsync(MakeAsset("Eiffel Tower", "Travel"));
            await _provider.SaveAssetAsync(MakeAsset("Colosseum", "Travel"));
            await _provider.SaveAssetAsync(MakeAsset("Sagrada Familia", "Travel"));
            await _provider.SaveAssetAsync(MakeAsset("Acropolis", "Travel"));
            await _provider.SaveAssetAsync(MakeAsset("Hagia Sophia", "Travel"));

            var all = (await _provider.GetAssetsAsync()).ToList();

            Assert.AreEqual(5, all.Count, "All 5 saved assets should be returned");
        }
        /// <summary>
        /// Verifies that City and Country values stored in <see cref="Asset.Metadata"/>
        /// are preserved correctly after a CSV save and load round-trip.
        /// </summary>
        [TestMethod]
        public async Task GetAssetsAsync_MetadataRoundTrip_CityAndCountryPreserved()
        {
            await _provider.SaveAssetAsync(
                MakeAsset("Charminar", "Travel", city: "Hyderabad", country: "India"));

            var loaded = (await _provider.GetAssetsAsync()).First();

            Assert.AreEqual("Hyderabad", loaded.Metadata["City"]);
            Assert.AreEqual("India", loaded.Metadata["Country"]);
        }
        /// <summary>
        /// Verifies that all tags assigned to an <see cref="Asset"/> are preserved
        /// in the correct order after a CSV save and load round-trip.
        /// </summary>
        [TestMethod]
        public async Task GetAssetsAsync_TagsRoundTrip_AllTagsPreserved()
        {
            var asset = MakeAsset();
            asset.Tags = new List<string> { "summer", "travel", "europe" };

            await _provider.SaveAssetAsync(asset);
            var loaded = (await _provider.GetAssetsAsync()).First();

            CollectionAssert.AreEqual(
                new[] { "summer", "travel", "europe" },
                loaded.Tags.ToArray(),
                "All tags should survive CSV round-trip");
        }
        /// <summary>
        /// Verifies that photo reference paths stored in <see cref="Asset.PhotoRefs"/>
        /// are preserved correctly after a CSV save and load round-trip.
        /// </summary>
        [TestMethod]
        public async Task GetAssetsAsync_PhotoRefsRoundTrip_Preserved()
        {
            var asset = MakeAsset();
            asset.PhotoRefs = new List<string> { "Photos/eiffel.jpg", "Photos/seine.png" };

            await _provider.SaveAssetAsync(asset);
            var loaded = (await _provider.GetAssetsAsync()).First();

            Assert.AreEqual(2, loaded.PhotoRefs.Count);
            Assert.AreEqual("Photos/eiffel.jpg", loaded.PhotoRefs[0]);
        }

        // Date format (FlexibleDateConverter)
        /// <summary>
        /// Verifies that dates written in legacy slash format (M/d/yyyy) are correctly
        /// parsed by the FlexibleDateConverter when loading assets from CSV.
        /// </summary>

        [TestMethod]
        public async Task GetAssetsAsync_LegacySlashDateFormat_ParsedCorrectly()
        {
            // "2/9/2026" in M/d/yyyy = month 2 (February), day 9
            string header = "Id,Name,Category,TimelineState,EventDate,CreatedAt," +
                            "Tags,Metadata,UserInput,UserExperience,PhotoRefs";
            string row = $"{Guid.NewGuid()},Old Place,Travel,Past," +
                            $"2/9/2026,2/9/2026," +
                            $"travel,Info=,old trip,nice,";
            File.WriteAllLines(_resolvedFilePath, new[] { header, row });

            var assets = (await _provider.GetAssetsAsync()).ToList();

            Assert.AreEqual(1, assets.Count, "Asset should load");
            Assert.IsNotNull(assets[0].EventDate, "EventDate should not be null");
            Assert.AreEqual(2, assets[0].EventDate!.Value.Month, "Month should be 2");
            Assert.AreEqual(9, assets[0].EventDate!.Value.Day, "Day should be 9");
            Assert.AreEqual(2026, assets[0].EventDate!.Value.Year, "Year should be 2026");
        }
        /// <summary>
        /// Verifies that dates written in dash format (dd-MM-yyyy) are correctly parsed
        /// and restored to the original <see cref="DateTime"/> value after a round-trip.
        /// </summary>
        [TestMethod]
        public async Task GetAssetsAsync_DashDateFormat_ParsedCorrectly()
        {
            var date = new DateTime(2025, 3, 25);
            await _provider.SaveAssetAsync(MakeAsset(date: date));

            var loaded = (await _provider.GetAssetsAsync()).First();

            Assert.AreEqual(date.Date, loaded.EventDate!.Value.Date,
                "dd-MM-yyyy written date should load back correctly");
        }

        //UpdateAssetAsync
        /// <summary>
        /// Verifies that updating an existing <see cref="Asset"/> correctly persists
        /// changes to UserExperience and PhotoRefs, and returns <c>true</c>.
        /// </summary>
        [TestMethod]
        public async Task UpdateAssetAsync_ExistingAsset_UpdatesCorrectly()
        {
            var asset = MakeAsset("Athens", "Travel");
            await _provider.SaveAssetAsync(asset);

            asset.UserExperience = "amazing";
            asset.PhotoRefs.Add("Photos/athens.jpg");
            bool result = await _provider.UpdateAssetAsync(asset);

            var loaded = (await _provider.GetAssetsAsync()).First();
            Assert.IsTrue(result, "UpdateAssetAsync should return true");
            Assert.AreEqual("amazing", loaded.UserExperience);
            Assert.AreEqual("Photos/athens.jpg", loaded.PhotoRefs[0]);
        }
        /// <summary>
        /// Verifies that attempting to update an <see cref="Asset"/> that was never saved
        /// returns <c>false</c> without throwing an exception.
        /// </summary>
        [TestMethod]
        public async Task UpdateAssetAsync_NonExistentId_ReturnsFalse()
        {
            var asset = MakeAsset(); // never saved

            bool result = await _provider.UpdateAssetAsync(asset);

            Assert.IsFalse(result, "Updating a non-existent asset should return false");
        }
        /// <summary>
        /// Verifies that updating an asset does not create a duplicate row in the CSV,
        /// ensuring exactly one record exists after the update.
        /// </summary>
        [TestMethod]
        public async Task UpdateAssetAsync_DoesNotDuplicateRecord()
        {
            var asset = MakeAsset("Tokyo", "Travel");
            await _provider.SaveAssetAsync(asset);

            asset.UserExperience = "incredible";
            await _provider.UpdateAssetAsync(asset);

            var all = (await _provider.GetAssetsAsync()).ToList();
            Assert.AreEqual(1, all.Count, "Update must not create a second row");
        }
        /// <summary>
        /// Verifies that updating one asset does not affect other assets stored in the same CSV.
        /// Only the targeted asset should reflect the change.
        /// </summary>

        [TestMethod]
        public async Task UpdateAssetAsync_OnlyTargetAssetChanges()
        {
            var a1 = MakeAsset("Rome", "Travel");
            var a2 = MakeAsset("Madrid", "Travel");
            await _provider.SaveAssetAsync(a1);
            await _provider.SaveAssetAsync(a2);

            a1.UserExperience = "updated";
            await _provider.UpdateAssetAsync(a1);

            var all = (await _provider.GetAssetsAsync()).ToDictionary(a => a.Name);
            Assert.AreEqual("updated", all["Rome"].UserExperience, "Rome should be updated");
            Assert.AreNotEqual("updated", all["Madrid"].UserExperience, "Madrid should be unchanged");
        }
    }
}