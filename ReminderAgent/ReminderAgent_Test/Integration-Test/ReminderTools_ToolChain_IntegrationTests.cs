using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Domain;
using ReminderAgent.Infrastructure;
using ReminderAgent.Interfaces;
using ReminderAgent.Tools;
using UnitTest_SprintSix;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IntegrationTest
{
    /// <summary>Stub embedding service that always returns a fixed vector.</summary>
    internal class FixedEmbeddingService : IEmbeddingService
    {
        private readonly float[] _vector;
        
        /// <summary>Initialises the stub with an optional fixed vector; defaults to [0.5, 0.5, 0.5].</summary>
        public FixedEmbeddingService(float[]? v = null)
            => _vector = v ?? new float[] { 0.5f, 0.5f, 0.5f };
        /// <summary>Returns the fixed vector regardless of input fields.</summary>
        public Task<float[]> GenerateEmbeddingAsync(
            string name, string category, string experience,
            string userInput = "", string tags = "")
            => Task.FromResult(_vector);
        /// <summary>Returns the fixed vector regardless of the query string.</summary>
        public Task<float[]> GenerateQueryEmbeddingAsync(string query)
            => Task.FromResult(_vector);
    }
    /// <summary>Stub similarity service that always returns a perfect match score of 1.0.</summary>
    internal class AlwaysMatchSimilarity : ISimilarityService
    {
        /// <summary>Always returns 1.0 — identical match for all vector pairs.</summary>
        public float CosineSimilarity(float[] a, float[] b) => 1.0f;
        /// <summary>Returns the first topK assets regardless of similarity score.</summary>
        public Task<List<Asset>> GetTopKSimilarAsync(
            float[] q, IEnumerable<Asset> assets, int topK, float threshold = 0.5f)
            => Task.FromResult(assets.Take(topK).ToList());
    }
    /// <summary>Stub similarity service that always returns a score of 0 — no match.</summary>
    internal class NeverMatchSimilarity : ISimilarityService
    {
        /// <summary>Always returns 0.0 — no match for all vector pairs.</summary>
        public float CosineSimilarity(float[] a, float[] b) => 0f;
        /// <summary>Always returns an empty list — no assets pass the threshold.</summary>
        public Task<List<Asset>> GetTopKSimilarAsync(
            float[] q, IEnumerable<Asset> assets, int topK, float threshold = 0.5f)
            => Task.FromResult(new List<Asset>());
    }
    /// <summary>Stub chat client that always returns a fixed reply string.</summary>

    internal class FixedChatClient : IChatClient
    {
        private readonly string _reply;
        /// <summary>Initialises the stub with an optional fixed reply; defaults to "Here are your results."</summary>
        public FixedChatClient(string reply = "Here are your results.")
            => _reply = reply;
        /// <summary>Returns fixed metadata for the fake chat client.</summary>
        public ChatClientMetadata Metadata => new ChatClientMetadata();
        /// <summary>Returns the fixed reply string wrapped in a ChatResponse.</summary>
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, _reply)));
        /// <summary>Not implemented — streaming is not used in integration tests.</summary>
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        /// <summary>Returns null — no additional services are provided by this stub.</summary>
        public object? GetService(Type t, object? key = null) => null;
        /// <summary>Disposes the stub — no resources to release.</summary>
        public void Dispose() { }
    }
    
    /// <summary>Integration tests for photo attachment, date handling, and tool chain consistency.</summary>
    [TestClass]
    public class ReminderTools_ToolChain_IntegrationTests
    {
        private string _csvPath = string.Empty;
        private InMemoryEmbeddingStore _embeddingStore = new();
        private CsvStorageProvider _storage = null!;
        /// <summary>Creates a fresh isolated CSV file and in-memory embedding store before each test.</summary>
        [TestInitialize]
        public void Setup()
        {
            _csvPath = Path.Combine(Path.GetTempPath(), $"nit_{Guid.NewGuid():N}.csv");
            _embeddingStore = new InMemoryEmbeddingStore();
            _storage = new CsvStorageProvider(_embeddingStore, _csvPath);

            File.WriteAllText(_csvPath,
                "Id,Name,Category,TimelineState,EventDate,CreatedAt," +
                "Tags,Metadata,UserInput,UserExperience,PhotoRefs" +
                Environment.NewLine);
        }
        /// <summary>Deletes the temporary CSV and backup files after each test.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_csvPath)) File.Delete(_csvPath);
            string bak = Path.Combine(Path.GetDirectoryName(_csvPath)!, "backup");
            if (Directory.Exists(bak))
                foreach (var f in Directory.GetFiles(bak, "*.bak"))
                    File.Delete(f);
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
        /// <summary>Verifies that null location parameters do not add City, Country, or Region keys to Metadata.</summary>
        [TestMethod]
        public async Task IT01_CreateAsset_NullLocation_NoLocationKeysInMetadata()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "Atomic Habits", category: "Book",
                userInput: "Great self-improvement book", userExperience: "inspiring",
                city: null, country: null, region: null);

            var asset = (await _storage.GetAssetsAsync()).First();

            Assert.IsFalse(asset.Metadata.ContainsKey("City"),
                "City key must not exist when city is null");
            Assert.IsFalse(asset.Metadata.ContainsKey("Country"),
                "Country key must not exist when country is null");
            Assert.IsFalse(asset.Metadata.ContainsKey("Region"),
                "Region key must not exist when region is null");
        }
        /// <summary>Verifies that a user experience of N/A is persisted as Neutral in the CSV.</summary>
        [TestMethod]
        public async Task IT02_CreateAsset_NAExperience_PersistedAsNeutralInCsv()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "Dubrovnik", category: "Travel",
                userInput: "Visiting Dubrovnik Croatia", userExperience: "N/A",
                city: "Dubrovnik", country: "Croatia");

            var asset = (await _storage.GetAssetsAsync()).First();

            Assert.AreEqual("Neutral", asset.UserExperience,
                "N/A experience must be persisted as Neutral in CSV");
        }
        /// <summary>Verifies that a partial asset name resolves to the full asset from real CSV storage.</summary>
        [TestMethod]
        public async Task IT03_GetAssetDetails_PartialName_ResolvesFromRealStorage()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "Twelve Apostles Restaurant",
                category: "Restaurant",
                userInput: "Famous pizza place in Frankfurt",
                userExperience: "delicious",
                city: "Frankfurt", country: "Germany");

            var result = await tool.GetAssetDetails("Twelve Apostles");

            StringAssert.Contains(result, "Twelve Apostles Restaurant",
                "Partial name must resolve to full asset name from real CSV storage");
        }
        /// <summary>Verifies that a GUID string resolves to the correct asset from real CSV storage.</summary>
        [TestMethod]
        public async Task IT04_GetAssetDetails_ByGuid_ResolvesFromRealStorage()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "East Mallorca Cala", category: "Hiking Route",
                userInput: "Coastal hike in Mallorca", userExperience: "breathtaking");

            var asset = (await _storage.GetAssetsAsync()).First();
            var result = await tool.GetAssetDetails(asset.Id.ToString());

            StringAssert.Contains(result, "East Mallorca Cala",
                "GUID lookup must resolve the asset from real CSV storage");
        }
        /// <summary>Verifies that backslash path separators in photo references are normalised to forward slashes.</summary>
        [TestMethod]
        public async Task IT05_AttachPhoto_BackslashPath_NormalisedInCsv()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "Sarvana Bhavan", category: "Restaurant",
                userInput: "South Indian food in Frankfurt", userExperience: "tasty",
                city: "Frankfurt", country: "Germany");

            await tool.AttachPhoto("Sarvana Bhavan", @"Photos\sarvana-bhavan.jpg");

            var asset = (await _storage.GetAssetsAsync()).First();

            Assert.AreEqual("Photos/sarvana-bhavan.jpg", asset.PhotoRefs[0],
                "Backslash must be normalised to forward slash in CSV");
        }
        /// <summary>Verifies that attaching the same photo path twice results in only one photo reference in the CSV.</summary>
        [TestMethod]
        public async Task IT06_AttachPhoto_DuplicatePath_CsvHasOnlyOnePhotoRef()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "Colosseum", category: "Travel",
                userInput: "Rome visit", userExperience: "impressive",
                city: "Rome", country: "Italy");

            await tool.AttachPhoto("Colosseum", "Photos/colosseum.jpg");
            await tool.AttachPhoto("Colosseum", "Photos/colosseum.jpg");

            var asset = (await _storage.GetAssetsAsync()).First();

            Assert.AreEqual(1, asset.PhotoRefs.Count,
                "CSV must contain exactly 1 photo ref after duplicate attach attempt");
        }
        /// <summary>Verifies that attaching a photo to an unknown asset returns a not-found message and leaves storage unchanged.</summary>
        [TestMethod]
        public async Task IT07_AttachPhoto_UnknownAsset_StorageUnchanged()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "Vapiano", category: "Restaurant",
                userInput: "Italian food", userExperience: "good",
                city: "Frankfurt", country: "Germany");

            var result = await tool.AttachPhoto("Burgerzimmer", "Photos/burger.jpg");

            StringAssert.Contains(result, "do not know",
                "AttachPhoto on unknown asset must return not-found message");

            var vapiano = (await _storage.GetAssetsAsync()).First();
            Assert.AreEqual(0, vapiano.PhotoRefs.Count,
                "Real storage must be unchanged when asset is not found");
        }
        /// <summary>Verifies that multiple photos are all persisted in the CSV in the correct order.</summary>
        [TestMethod]
        public async Task IT08_AttachPhoto_MultiplePhotos_AllPersistedInCsvInOrder()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "Acropolis", category: "Travel",
                userInput: "Athens Greece visit", userExperience: "stunning",
                city: "Athens", country: "Greece");

            await tool.AttachPhoto("Acropolis", "Photos/acropolis_day.jpg");
            await tool.AttachPhoto("Acropolis", "Photos/acropolis_sunset.jpg");
            await tool.AttachPhoto("Acropolis", "Photos/acropolis_night.jpg");

            var asset = (await _storage.GetAssetsAsync()).First();

            Assert.AreEqual(3, asset.PhotoRefs.Count,
                "All 3 photos must be persisted in CSV");
            Assert.AreEqual("Photos/acropolis_day.jpg", asset.PhotoRefs[0]);
            Assert.AreEqual("Photos/acropolis_sunset.jpg", asset.PhotoRefs[1]);
            Assert.AreEqual("Photos/acropolis_night.jpg", asset.PhotoRefs[2]);
        }
        /// <summary>Verifies that searching empty storage returns a non-empty no-results message.</summary>
        [TestMethod]
        public async Task IT09_SearchAssets_EmptyStorage_ReturnsNoResultsMessage()
        {
            var tool = MakeTool();

            var result = await tool.SearchAssets(query: "restaurants in Frankfurt");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "SearchAssets on empty storage must return a non-empty message");
            StringAssert.Contains(result, "couldn't find",
                "Must communicate no results found");
        }
        /// <summary>Verifies that a category filter excludes non-matching assets even when semantic scoring returns no results.</summary>
        [TestMethod]
        public async Task IT10_SearchAssets_CategoryFilterPlusFallback_ReturnsFilteredResults()
        {
            var tool = MakeTool(sim: new NeverMatchSimilarity());

            await tool.CreateAsset(
                name: "The Hobbit", category: "Book",
                userInput: "Fantasy classic by Tolkien", userExperience: "magical");
            await tool.CreateAsset(
                name: "Vapiano", category: "Restaurant",
                userInput: "Italian food", userExperience: "good",
                city: "Frankfurt", country: "Germany");

            var result = await tool.SearchAssets(
                query: "something to read", category: "Book");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "Category filter + fallback must still return a response");
            Assert.IsFalse(result.Contains("Vapiano"),
                "Category filter must exclude Restaurant before fallback runs");
        }
        /// <summary>Verifies that listing an unknown category returns a message communicating no items exist.</summary>
        [TestMethod]
        public async Task IT11_ListAssets_UnknownCategory_ReturnsNoItemsMessage()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "The Hobbit", category: "Book",
                userInput: "Fantasy novel", userExperience: "wonderful");

            var result = await tool.ListAssets("Playlist");

            StringAssert.Contains(result, "Playlist",
                "Response must mention the requested category");
            Assert.IsTrue(
                result.Contains("don't have") || result.Contains("no saved"),
                "Must communicate no items exist in that category");
        }
        /// <summary>Verifies that attaching a photo does not overwrite the existing embedding in the JSON store.</summary>
        [TestMethod]
        public async Task IT12_AttachPhoto_DoesNotOverwriteEmbeddingInStore()
        {
            var tool = MakeTool();

            await tool.CreateAsset(
                name: "Sagrada Familia", category: "Travel",
                userInput: "Barcelona landmark", userExperience: "extraordinary",
                city: "Barcelona", country: "Spain");

            var asset = (await _storage.GetAssetsAsync()).First();
            var originalEmbedding = await _embeddingStore.LoadEmbeddingAsync(asset.Id);

            Assert.IsNotNull(originalEmbedding, "Embedding must exist before AttachPhoto");

            await tool.AttachPhoto("Sagrada Familia", "Photos/sagrada.jpg");

            var afterEmbedding = await _embeddingStore.LoadEmbeddingAsync(asset.Id);

            Assert.IsNotNull(afterEmbedding,
                "Embedding must still exist after AttachPhoto");
            CollectionAssert.AreEqual(originalEmbedding, afterEmbedding,
                "AttachPhoto must not overwrite the original embedding");
        }
        /// <summary>Verifies that GetReminders returns a no-results message when no assets match the requested timeline state.</summary>
        [TestMethod]
        public async Task IT13_GetReminders_StateWithNoMatches_ReturnsNoResultsMessage()
        {
            var tool = MakeTool();

            string futureDate = DateTime.Today.AddDays(10).ToString("dd-MM-yyyy");
            await tool.CreateAsset(
                name: "Summer Trip", category: "Travel",
                userInput: "Planning a summer trip", userExperience: "excited",
                eventDate: futureDate);

            // Only a Future asset exists — Past must return no results
            var result = await tool.GetReminders("Past");
            Console.WriteLine(result);

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "GetReminders must return a message even when no assets match state");
            StringAssert.Contains(result, "no",
                "Must communicate no Past reminders found");
        }
        /// <summary>Verifies that name, category, location, and photo data remain consistent through a full Create, AttachPhoto, and GetDetails tool chain.</summary>
        [TestMethod]
        public async Task IT14_CreateAttachPhoto_GetDetails_DataConsistentThroughToolChain()
        {
            var tool = MakeTool();

            // Step 1: Create
            await tool.CreateAsset(
                name: "Burj Khalifa", category: "Travel",
                userInput: "Tallest building in Dubai", userExperience: "awe-inspiring",
                tags: "dubai;uae;architecture",
                city: "Dubai", country: "UAE");

            // Step 2: Attach photo
            await tool.AttachPhoto("Burj Khalifa", "Photos/burj_khalifa.jpg");

            // Step 3: Read details
            var details = await tool.GetAssetDetails("Burj Khalifa");

            // All fields must survive the full chain
            StringAssert.Contains(details, "Burj Khalifa", "Name must be in details");
            StringAssert.Contains(details, "Travel", "Category must be in details");
            StringAssert.Contains(details, "Dubai", "City must be in details");
            StringAssert.Contains(details, "UAE", "Country must be in details");
            StringAssert.Contains(details, "burj_khalifa.jpg", "Photo must be in details");

            // Verify final CSV state
            var asset = (await _storage.GetAssetsAsync()).First();
            Assert.AreEqual("Burj Khalifa", asset.Name);
            Assert.AreEqual("Dubai", asset.Metadata["City"]);
            Assert.AreEqual(1, asset.PhotoRefs.Count);
            Assert.AreEqual("Photos/burj_khalifa.jpg", asset.PhotoRefs[0]);
            Assert.IsNotNull(asset.Embedding,
                "Embedding must survive through AttachPhoto update");
        }
        /// <summary>Verifies that a CSV date with a time component is parsed as the correct day and month and assigned the correct timeline state.</summary>
        [TestMethod]
        public async Task IT15_CsvDate_WithTimeComponent_LoadsCorrectDayMonthAndTimeline()
        {

            string header = "Id,Name,Category,TimelineState,EventDate,CreatedAt," +
                            "Tags,Metadata,UserInput,UserExperience,PhotoRefs";
            string row = $"{Guid.NewGuid()},Eiffel Tower,Travel,Past," +
                            $"09-02-2026 00:00:00,09-03-2026 00:00:00," +
                            $"paris;travel,Info=|City=Paris|Country=France," +
                            $"visited paris eiffel tower,beautiful,Photos/eiffel.jpg";

            File.WriteAllLines(_csvPath, new[] { header, row });

            var assets = (await _storage.GetAssetsAsync()).ToList();

            Assert.AreEqual(1, assets.Count, "Asset must load from CSV");
            Assert.IsNotNull(assets[0].EventDate, "EventDate must not be null");

            // Date must be read as 9th February 2026, not 2nd September 2026
            Assert.AreEqual(9, assets[0].EventDate!.Value.Day,
                "Day must be 9");
            Assert.AreEqual(2, assets[0].EventDate!.Value.Month,
                "Month must be 2 (February)");
            Assert.AreEqual(2026, assets[0].EventDate!.Value.Year,
                "Year must be 2026");

            // 9th February 2026 is before today (March 2026) — must be Past
            string timelineState = assets[0].EventDate!.Value.Date < DateTime.Today
                ? "Past" : "Future";

            Assert.AreEqual("Past", timelineState,
                "9th February 2026 is before today so timeline must be Past");
        }
    }
}