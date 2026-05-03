using Microsoft.Extensions.AI;
using ReminderAgent.Infrastructure;
using ReminderAgent.Interfaces;
using ReminderAgent.Tools;
using UnitTest_SprintSix;

namespace IntegrationTest
{
    /// <summary>Integration tests for CSV round-trips, photo persistence, timeline filtering, and location-based search.</summary>
    [TestClass]
    public class ReminderTools_CsvPersistence_IntegrationTests
    {
        private string _csvPath = string.Empty;
        private InMemoryEmbeddingStore _embeddingStore = new();
        private CsvStorageProvider _storage = null!;
        /// <summary>Standard CSV header used for all test file initialisations.</summary>
        private static readonly string CsvHeader =
            "Id,Name,Category,TimelineState,EventDate,CreatedAt," +
            "Tags,Metadata,UserInput,UserExperience,PhotoRefs";

        /// <summary>Creates a fresh isolated CSV file and in-memory embedding store before each test.</summary>
        [TestInitialize]
        public void Setup()
        {
            _csvPath = Path.Combine(Path.GetTempPath(), $"nit2_{Guid.NewGuid():N}.csv");
            _embeddingStore = new InMemoryEmbeddingStore();
            _storage = new CsvStorageProvider(_embeddingStore, _csvPath);
            File.WriteAllText(_csvPath, CsvHeader + Environment.NewLine);
        }
        /// <summary>Deletes the temporary CSV file after each test.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_csvPath)) File.Delete(_csvPath);
        }
        /// <summary>Creates a ReminderTool with optional stub overrides for similarity, chat, and embedding.</summary>
        private ReminderTools MakeTool(
            ISimilarityService? sim = null,
            IChatClient? chat = null,
            IEmbeddingService? emb = null)
            => new ReminderTools(
                _storage,
                emb ?? new FixedEmbeddingService(),
                sim ?? new AlwaysMatchSimilarity(),
                chat ?? new FixedChatClient());

        /// <summary>Helper that creates an asset with sensible defaults to reduce boilerplate in test methods.</summary>
        private async Task<string> QuickCreate(
            ReminderTools tools,
            string name,
            string category = "Travel",
            string experience = "good",
            DateTime? eventDate = null,
            string? city = null,
            string? country = null,
            string? region = null,
            string? tags = null)
            => await tools.CreateAsset(
                name: name,
                category: category,
                userInput: $"I experienced {name}",
                userExperience: experience,
                tags: tags,
                eventDate: (eventDate ?? DateTime.Today.AddMonths(-1))
                               .ToString("dd-MM-yyyy"),
                city: city,
                country: country,
                region: region);

        /// <summary>Verifies that city, country, and region fields survive a full CSV serialisation and deserialisation round-trip.</summary>
        [TestMethod]
        public async Task IT01_CreateAsset_AllLocationFields_SurviveCsvRoundTrip()
        {
            var tool = MakeTool();

            await QuickCreate(tool,
                name: "Vapiano",
                category: "Restaurant",
                city: "Frankfurt",
                country: "Germany",
                region: "Hesse");

            // Force a fresh read from the actual CSV file
            var reloaded = (await _storage.GetAssetsAsync()).ToList();
            Assert.AreEqual(1, reloaded.Count, "Exactly one asset must be in the CSV");
            Assert.AreEqual("Frankfurt", reloaded[0].Metadata["City"],
                "City must survive CSV serialisation round-trip");
            Assert.AreEqual("Germany", reloaded[0].Metadata["Country"],
                "Country must survive CSV serialisation round-trip");
            Assert.AreEqual("Hesse", reloaded[0].Metadata["Region"],
                "Region must survive CSV serialisation round-trip");
        }
        /// <summary>Verifies that tags survive a full CSV serialisation and deserialisation round-trip.</summary>
        [TestMethod]
        public async Task IT02_CreateAsset_Tags_SurviveCsvRoundTrip()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "Atomic Habits",
                category: "Book",
                userInput: "Great book on habits",
                userExperience: "inspiring",
                tags: "self-improvement,productivity,habits");

            var reloaded = (await _storage.GetAssetsAsync()).ToList();

            Assert.AreEqual(1, reloaded.Count);
            Assert.IsTrue(reloaded[0].Tags.Count >= 1,
                "Tags must deserialise into a non-empty list after CSV round-trip");
            Assert.IsTrue(reloaded[0].Tags.Any(t => t.Contains("habit")),
                "At least one tag containing 'habit' must survive the round-trip");
        }
        /// <summary>Verifies that an asset saved with a past date is assigned TimelineState of Past after CSV round-trip.</summary>
        [TestMethod]
        public async Task IT03_CreateAsset_PastDate_TimelineStateIsPastInCsv()
        {
            var tool = MakeTool();
            string pastDate = DateTime.Today.AddDays(-30).ToString("dd-MM-yyyy");

            await tool.CreateAsset(
                name: "Colosseum",
                category: "Travel",
                userInput: "Visited Rome",
                userExperience: "breathtaking",
                eventDate: pastDate,
                city: "Rome",
                country: "Italy");

            var reloaded = (await _storage.GetAssetsAsync()).ToList();

            Assert.AreEqual(1, reloaded.Count);
            Assert.AreEqual("Past", reloaded[0].TimelineState,
                "Past-dated asset must have TimelineState='Past' after CSV round-trip");
            Assert.IsTrue(reloaded[0].EventDate!.Value.Date < DateTime.Today,
                "Reloaded EventDate must be in the past");
        }
        /// <summary>Verifies that an asset saved with a future date is assigned TimelineState of Future after CSV round-trip.</summary>
        [TestMethod]
        public async Task IT04_CreateAsset_FutureDate_TimelineStateIsFutureInCsv()
        {
            var tool = MakeTool();
            string futureDate = DateTime.Today.AddDays(30).ToString("dd-MM-yyyy");

            await tool.CreateAsset(
                name: "Tokyo Trip",
                category: "Travel",
                userInput: "Planning a trip to Tokyo",
                userExperience: "excited",
                eventDate: futureDate,
                city: "Tokyo",
                country: "Japan");

            var reloaded = (await _storage.GetAssetsAsync()).ToList();

            Assert.AreEqual(1, reloaded.Count);
            Assert.AreEqual("Future", reloaded[0].TimelineState,
                "Future-dated asset must have TimelineState='Future' after CSV round-trip");
            Assert.IsTrue(reloaded[0].EventDate!.Value.Date > DateTime.Today,
                "Reloaded EventDate must be in the future");
        }
        /// <summary>Verifies that saving the same asset twice creates only one row in the CSV.</summary>
        [TestMethod]
        public async Task IT05_CreateAsset_DuplicateName_OnlyOneRowInCsv()
        {
            var tool = MakeTool();

            await QuickCreate(tool, "Vapiano", "Restaurant");
            await QuickCreate(tool, "Vapiano", "Restaurant"); // duplicate

            var reloaded = (await _storage.GetAssetsAsync()).ToList();

            Assert.AreEqual(1, reloaded.Count,
                "Duplicate save must not create a second row in the CSV file");
        }
        /// <summary>Verifies that a single photo reference survives a full AttachPhoto, CSV write, and reload cycle.</summary>
        [TestMethod]
        public async Task IT06_AttachPhoto_PhotoRef_SurvivesCsvRoundTrip()
        {
            var tool = MakeTool();

            await QuickCreate(tool, "Eiffel Tower", "Travel",
                city: "Paris", country: "France");

            await tool.AttachPhoto("Eiffel Tower", "Photos/eiffel.jpg");

            var reloaded = (await _storage.GetAssetsAsync()).ToList();

            Assert.AreEqual(1, reloaded.Count);
            Assert.AreEqual(1, reloaded[0].PhotoRefs.Count,
                "Photo ref count must be 1 after reload from CSV");
            Assert.AreEqual("Photos/eiffel.jpg", reloaded[0].PhotoRefs[0],
                "Photo path must survive UpdateAssetAsync → CSV write → reload");
        }
        /// <summary>Verifies that two sequentially attached photos both survive CSV write and reload in the correct order.</summary>
        [TestMethod]
        public async Task IT07_AttachPhoto_TwoSequentialPhotos_BothSurviveCsv()
        {
            var tool = MakeTool();

            await QuickCreate(tool, "Acropolis", "Travel",
                city: "Athens", country: "Greece");

            await tool.AttachPhoto("Acropolis", "Photos/acropolis_day.jpg");
            await tool.AttachPhoto("Acropolis", "Photos/acropolis_night.jpg");

            var reloaded = (await _storage.GetAssetsAsync()).ToList();

            Assert.AreEqual(2, reloaded[0].PhotoRefs.Count,
                "Both photo refs must survive two sequential AttachPhoto → CSV writes");
            Assert.AreEqual("Photos/acropolis_day.jpg", reloaded[0].PhotoRefs[0]);
            Assert.AreEqual("Photos/acropolis_night.jpg", reloaded[0].PhotoRefs[1]);
        }
        /// <summary>Verifies that attaching a photo to an unknown asset returns an error and leaves the CSV empty.</summary>
        [TestMethod]
        public async Task IT08_AttachPhoto_UnknownAsset_ReturnsErrorAndCsvIsEmpty()
        {
            var tool = MakeTool();

            var result = await tool.AttachPhoto("Nonexistent Place", "Photos/test.jpg");

            Assert.IsTrue(
                result.Contains("do not know") || result.Contains("not found") ||
                result.Contains("hasn't been saved"),
                "Attaching to unknown asset must return a clear error message");

            var reloaded = (await _storage.GetAssetsAsync()).ToList();
            Assert.AreEqual(0, reloaded.Count,
                "No CSV row must be created when AttachPhoto target is unknown");
        }
        /// <summary>Verifies that all fields saved to CSV are returned correctly by GetAssetDetails.</summary>
        [TestMethod]
        public async Task IT09_CreateAsset_ThenGetAssetDetails_AllFieldsFromRealCsv()
        {
            var tool = MakeTool();

            var createResult = await tool.CreateAsset(
                name: "Vapiano Frankfurt",
                category: "Restaurant",
                userInput: "Had amazing pasta there",
                userExperience: "Wonderful",
                city: "Frankfurt",
                country: "Germany",
                region: "Hesse");

            // GetAssetDetails reads from the real CSV
            var details = await tool.GetAssetDetails("Vapiano Frankfurt");

            StringAssert.Contains(createResult, "Successfully remembered",
                "CreateAsset must succeed before GetAssetDetails can be tested");
            StringAssert.Contains(details, "Vapiano Frankfurt",
                "Name must appear in details read from real CSV");
            StringAssert.Contains(details, "Restaurant",
                "Category saved to CSV must appear in details");
            StringAssert.Contains(details, "Frankfurt",
                "City saved to CSV must appear in details");
            StringAssert.Contains(details, "Wonderful",
                "UserExperience saved to CSV must appear in details");
            StringAssert.Contains(details, "Hesse",
                "Region saved to CSV must appear in details");
        }
        /// <summary>Verifies that a partial asset name resolves the full asset name and city from real CSV.</summary>
        [TestMethod]
        public async Task IT10_GetAssetDetails_PartialName_ResolvesFromRealCsv()
        {
            var tool = MakeTool();

            await QuickCreate(tool, "Sagrada Familia Barcelona", "Travel",
                city: "Barcelona", country: "Spain");

            var result = await tool.GetAssetDetails("Sagrada Familia");

            StringAssert.Contains(result, "Sagrada Familia Barcelona",
                "Partial name must resolve the full asset name read from real CSV");
            StringAssert.Contains(result, "Barcelona",
                "City read from real CSV must appear in partial-name result");
        }
        /// <summary>Verifies that a stale Present timeline state is dynamically recalculated to Past based on the EventDate in the CSV.</summary>
        [TestMethod]
        public async Task IT11_GetReminders_StalePresentInCsv_DynamicallyRecalculatedToPast()
        {
            var tool = MakeTool();

            string pastDate = DateTime.Today.AddDays(-30).ToString("dd-MM-yyyy");
            await tool.CreateAsset(
                name: "Paris Visit",
                category: "Travel",
                userInput: "Visited Paris last month",
                userExperience: "lovely",
                eventDate: pastDate,
                city: "Paris",
                country: "France");

            // Corrupt TimelineState to simulate stale CSV
            var asset = (await _storage.GetAssetsAsync()).First();
            asset.TimelineState = "Present"; 
            await _storage.UpdateAssetAsync(asset);

            // ACT
            var result = await tool.GetReminders(state: "Past");

            // ASSERT — storage level (not response string)
            // Verify the asset exists in CSV with a past date
            var reloaded = (await _storage.GetAssetsAsync()).ToList();
            Assert.AreEqual(1, reloaded.Count,
                "Asset must exist in CSV after stale state corruption");

            Assert.IsTrue(reloaded[0].EventDate!.Value.Date < DateTime.Today,
                "EventDate must be in the past — this is the source for dynamic recalculation");

            // ASSERT — response level
            Assert.IsFalse(
                result.Contains("no Past reminders") ||
                result.Contains("I found no"),
                "GetReminders must NOT say 'no results' — stale Present state " +
                "must be recalculated to Past from the real EventDate in CSV");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "GetReminders must return a non-empty response when past asset exists");
        }
        /// <summary>Verifies that a null state filter returns all three timeline assets from the real CSV.</summary>
        [TestMethod]
        public async Task IT12_GetReminders_NullState_ReturnsAllTimelinesFromRealCsv()
        {
            var tool = MakeTool();

            await QuickCreate(tool, "Past Book", "Book",
                eventDate: DateTime.Today.AddDays(-20));
            await QuickCreate(tool, "Present Lunch", "Restaurant",
                eventDate: DateTime.Today);
            await QuickCreate(tool, "Future Concert", "Event",
                eventDate: DateTime.Today.AddDays(20));

            // ACT
            var result = await tool.GetReminders(state: null);

            // ASSERT — storage level (source of truth) 
            var reloaded = (await _storage.GetAssetsAsync()).ToList();
            Assert.AreEqual(3, reloaded.Count,
                "All 3 assets must be present in the real CSV file");

            Assert.IsTrue(reloaded.Any(a => a.Name == "Past Book"),
                "Past Book must exist in CSV");
            Assert.IsTrue(reloaded.Any(a => a.Name == "Present Lunch"),
                "Present Lunch must exist in CSV");
            Assert.IsTrue(reloaded.Any(a => a.Name == "Future Concert"),
                "Future Concert must exist in CSV");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "GetReminders must return a non-empty response when 3 assets exist in CSV");

            Assert.IsFalse(
                result.Contains("I found no") || result.Contains("no total reminders"),
                "Null state filter must NOT say no results when 3 CSV rows exist");
        }
        
        /// <summary>Verifies that a category filter excludes assets from other categories when reading from real CSV.</summary>
        [TestMethod]
        public async Task IT13_SearchAssets_CategoryFilter_OnlyMatchingCategoryFromRealCsv()
        {
            var tool = MakeTool(sim: new AlwaysMatchSimilarity());

            await QuickCreate(tool, "The Hobbit", "Book");
            await QuickCreate(tool, "Vapiano", "Restaurant",
                city: "Frankfurt", country: "Germany");
            await QuickCreate(tool, "Lofoten Islands", "Travel",
                city: "Lofoten", country: "Norway");

            var result = await tool.SearchAssets(
                query: "something to read", category: "Book");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "SearchAssets with category filter must return a non-empty response");
            Assert.IsFalse(result.Contains("Vapiano"),
                "Restaurant must be excluded when filtering by Book from real CSV");
            Assert.IsFalse(result.Contains("Lofoten"),
                "Travel must be excluded when filtering by Book from real CSV");
        }
        /// <summary>Verifies that a date range filter excludes assets outside the range when EventDate is read from real CSV.</summary>
        [TestMethod]
        public async Task IT14_SearchAssets_DateRangeFilter_ExcludesOldAssetsFromRealCsv()
        {
            var tool = MakeTool(sim: new AlwaysMatchSimilarity());

            await QuickCreate(tool, "Old Restaurant", "Restaurant",
                eventDate: DateTime.Today.AddMonths(-6));

            await QuickCreate(tool, "Recent Hike", "Hiking Route",
                eventDate: DateTime.Today.AddDays(-3));

            var result = await tool.SearchAssets(
                query: "things I did",
                dateRange: "last week");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "SearchAssets with date range must return a non-empty response");
            Assert.IsFalse(result.Contains("Old Restaurant"),
                "Asset from 6 months ago must be excluded by last week date range " +
                "when EventDate is read from real CSV");
        }
        /// <summary>Verifies that a location filter excludes assets from a different city when City is read from real CSV metadata.</summary>
        [TestMethod]
        public async Task IT15_SearchAssets_LocationFilter_ExcludesWrongCityFromRealCsv()
        {
            var tool = MakeTool(sim: new AlwaysMatchSimilarity());

            await QuickCreate(tool, "Vapiano", "Restaurant",
                city: "Frankfurt", country: "Germany");
            await QuickCreate(tool, "Colosseum", "Travel",
                city: "Rome", country: "Italy");

            var result = await tool.SearchAssets(
                query: "places I visited",
                location: "Frankfurt");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "SearchAssets with location filter must return a response");
            Assert.IsFalse(result.Contains("Colosseum"),
                "Rome asset must be excluded by Frankfurt location filter " +
                "when City is read from real CSV metadata");
        }
    }
}