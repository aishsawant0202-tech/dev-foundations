using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Domain;
using ReminderAgent.Infrastructure;
using ReminderAgent.Interfaces;
using ReminderAgent.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnitTest_SprintSix;

namespace IntegrationTest
{
    /// <summary>Integration tests for CSV round-trips, embedding merging, scalability, and provider restart scenarios.</summary>
    [TestClass]
    [DoNotParallelize]
    public class EmbeddingService_CsvProvider_IntegrationTests
    {
        private FakeEmbeddingGeneratorThree _fake     = null!;
        private EmbeddingService            _embedSvc = null!;
        private SimilarityService           _simSvc   = null!;

        private string                 _csvFileName   = string.Empty;
        private string                 _csvFilePath   = string.Empty;
        private InMemoryEmbeddingStore _csvEmbedStore = new();
        private CsvStorageProvider     _csvProvider   = null!;
        /// <summary>Standard CSV header used for all test file initialisations.</summary>
        private static readonly string CsvHeader =
            "Id,Name,Category,TimelineState,EventDate,CreatedAt," +
            "Tags,Metadata,UserInput,UserExperience,PhotoRefs";
        /// <summary>Creates fresh service instances and an isolated CSV file before each test.</summary>

        [TestInitialize]
        public void Setup()
        {
            _fake     = new FakeEmbeddingGeneratorThree();
            _embedSvc = new EmbeddingService(_fake);
            _simSvc   = new SimilarityService();

            _csvFileName   = $"it3_{Guid.NewGuid():N}.csv";
            _csvEmbedStore = new InMemoryEmbeddingStore();
            _csvProvider   = new CsvStorageProvider(_csvEmbedStore, _csvFileName);
            _csvFilePath   = _csvProvider.GetFilePath();
            File.WriteAllText(_csvFilePath, CsvHeader + Environment.NewLine);
        }
        /// <summary>Deletes the temporary CSV file after each test.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_csvFilePath))
                File.Delete(_csvFilePath);
        }

        /// <summary>Fake embedding generator that returns a configurable fixed vector and tracks call count and last input.</summary>

        public class FakeEmbeddingGeneratorThree : IEmbeddingGenerator<string, Embedding<float>>
        {
            /// <summary>Gets or sets the vector returned by every GenerateAsync call.</summary>
            public float[]  VectorToReturn { get; set; } = new float[] { 0f };
            /// <summary>Gets the last input string passed to GenerateAsync.</summary>
            public string?  LastInput      { get; private set; }
            /// <summary>Gets the total number of times GenerateAsync has been called.</summary>
            public int      CallCount      { get; private set; }
            /// <summary>Returns the configured fixed vector and records the first input value.</summary>
            public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
                IEnumerable<string>         values,
                EmbeddingGenerationOptions? options           = null,
                CancellationToken           cancellationToken = default)
            {
                CallCount++;
                foreach (var v in values) { LastInput = v; break; }
                var embedding  = new Embedding<float>(VectorToReturn);
                var collection = new GeneratedEmbeddings<Embedding<float>>(new[] { embedding });
                return Task.FromResult(collection);
            }
            /// <summary>Returns metadata identifying this as the FakeGeneratorThree.</summary>

            public EmbeddingGeneratorMetadata Metadata =>
                new EmbeddingGeneratorMetadata("FakeGeneratorThree");
            /// <summary>Returns null — no additional services are provided by this stub.</summary>

            public object? GetService(Type serviceType, object? key = null) => null;
            /// <summary>Disposes the stub — no resources to release.</summary>
            public void Dispose() { }
        }
        /// <summary>Stub embedding generator that always throws a configurable exception to simulate API failures.</summary>
        public class ThrowingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
        {
            private readonly Exception _ex;
            /// <summary>Initialises the stub with the exception to throw on every GenerateAsync call.</summary>
            public ThrowingEmbeddingGenerator(Exception ex) => _ex = ex;
            /// <summary>Always throws the configured exception.</summary>
            public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
                IEnumerable<string>         values,
                EmbeddingGenerationOptions? options           = null,
                CancellationToken           cancellationToken = default)
                => throw _ex;
            /// <summary>Returns metadata identifying this as the ThrowingGenerator.</summary>
            public EmbeddingGeneratorMetadata Metadata =>
                new EmbeddingGeneratorMetadata("ThrowingGenerator");
            /// <summary>Returns null — no additional services are provided by this stub.</summary>
            public object? GetService(Type serviceType, object? key = null) => null;
            /// <summary>Disposes the stub — no resources to release.</summary>
            public void Dispose() { }
        }
        /// <summary>Stub chat client that always returns a fixed reply string.</summary>
        internal class FixedChatClientThree : IChatClient
        {
            private readonly string _reply;
            /// <summary>Initialises the stub with an optional fixed reply; defaults to "Here are your results."</summary>
            public FixedChatClientThree(string reply = "Here are your results.")
                => _reply = reply;
            /// <summary>Returns fixed metadata for the fake chat client.</summary>
            public ChatClientMetadata Metadata => new ChatClientMetadata();
            /// <summary>Returns the fixed reply string wrapped in a ChatResponse.</summary>
            public Task<ChatResponse> GetResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions?             options           = null,
                CancellationToken        cancellationToken = default)
                => Task.FromResult(
                    new ChatResponse(new ChatMessage(ChatRole.Assistant, _reply)));
            /// <summary>Not implemented — streaming is not used in integration tests.</summary>
            public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions?             options           = null,
                CancellationToken        cancellationToken = default)
                => throw new NotImplementedException();
            /// <summary>Returns null — no additional services are provided by this stub.</summary>
            public object? GetService(Type t, object? key = null) => null;
            /// <summary>Disposes the stub — no resources to release.</summary>
            public void Dispose() { }
        }
        /// <summary>Stub similarity service that always returns a perfect match score of 1.0.</summary>
        internal class AlwaysMatchSimilarityThree : ISimilarityService
        {
            /// <summary>Always returns 1.0 — identical match for all vector pairs.</summary>
            public float CosineSimilarity(float[] a, float[] b) => 1.0f;
            /// <summary>Returns the first topK assets regardless of similarity score.</summary>
            public Task<List<Asset>> GetTopKSimilarAsync(
                float[] q, IEnumerable<Asset> assets, int topK, float threshold = 0.5f)
                => Task.FromResult(assets.Take(topK).ToList());
        }
        /// <summary>Stub similarity service that always returns a score of 0 — no match.</summary>
        internal class NeverMatchSimilarityThree : ISimilarityService
        {
            /// <summary>Always returns 0.0 — no match for all vector pairs.</summary>
            public float CosineSimilarity(float[] a, float[] b) => 0f;
            /// <summary>Always returns an empty list — no assets pass the threshold.</summary>
            public Task<List<Asset>> GetTopKSimilarAsync(
                float[] q, IEnumerable<Asset> assets, int topK, float threshold = 0.5f)
                => Task.FromResult(new List<Asset>());
        }

        /// <summary>Creates a fully populated Asset with default test values and an optional name, category, and date.</summary>

        private static Asset MakeCsvAsset(
            string name = "Test Place", string category = "Travel", DateTime? date = null)
            => new Asset
            {
                Id             = Guid.NewGuid(),
                Name           = name,
                Category       = category,
                UserInput      = $"I visited {name}",
                UserExperience = "great",
                EventDate      = date ?? DateTime.Today.AddDays(-5),
                CreatedAt      = DateTime.Now,
                TimelineState  = "Past",
                Tags           = new List<string> { "test" },
                Metadata       = new Dictionary<string, string>
                {
                    ["City"]    = "Berlin",
                    ["Country"] = "Germany",
                    ["Info"]    = "gap test"
                },
                Embedding  = new float[] { 0.1f, 0.2f, 0.3f },
                PhotoRefs  = new List<string>()
            };
        /// <summary>Creates a ReminderTool with optional stub overrides for similarity and chat client.</summary>
        private ReminderTools MakeTool(
            ISimilarityService? sim  = null,
            IChatClient?        chat = null)
            => new ReminderTools(
                _csvProvider,
                new FixedEmbeddingService(),
                sim  ?? new AlwaysMatchSimilarityThree(),
                chat ?? new FixedChatClientThree());
        /// <summary>Helper that creates an asset with sensible defaults to reduce boilerplate in test methods.</summary>
        
        private async Task<string> QuickCreate(
            ReminderTools tools,
            string       name,
            string       category   = "Travel",
            string       experience = "good",
            DateTime?    eventDate  = null,
            string?      city       = null,
            string?      country    = null)
            => await tools.CreateAsset(
                name:           name,
                category:       category,
                userInput:      $"I experienced {name}",
                userExperience: experience,
                eventDate:      (eventDate ?? DateTime.Today.AddMonths(-1))
                                    .ToString("dd-MM-yyyy"),
                city:    city,
                country: country);

        /// <summary>Verifies that two saved assets both survive a full CSV round-trip with correct names.</summary>

        [TestMethod]
        public async Task IT01_TwoAssetsSaved_BothRoundTripCorrectly()
        {
            var assetA = MakeCsvAsset("Eiffel Tower");
            var assetB = MakeCsvAsset("Big Ben");

            await _csvProvider.SaveAssetAsync(assetA);
            await _csvProvider.SaveAssetAsync(assetB);
            var loaded = (await _csvProvider.GetAssetsAsync()).ToList();

            Assert.AreEqual(2, loaded.Count,
                "Both saved assets must be returned by GetAssetsAsync");

            var names = loaded.Select(a => a.Name).ToList();
            CollectionAssert.Contains(names, "Eiffel Tower",
                "Eiffel Tower must survive the CSV round-trip");
            CollectionAssert.Contains(names, "Big Ben",
                "Big Ben must survive the CSV round-trip");
        }
        /// <summary>Verifies that an asset name containing CSV special characters round-trips as exactly one row.</summary>
        [TestMethod]
        public async Task IT02_AssetNameWithCsvSpecialChars_RoundTripsAsOneRow()
        {
            var injectionName = "He said \"hello\", world\nnewline";
            var asset         = MakeCsvAsset(injectionName);

            bool saved  = await _csvProvider.SaveAssetAsync(asset);
            var  loaded = (await _csvProvider.GetAssetsAsync()).ToList();

            Assert.IsTrue(saved,
                "Save must succeed even when name contains CSV special characters");
            Assert.AreEqual(1, loaded.Count,
                "Special characters must not split the row — exactly 1 asset must reload");
            Assert.AreEqual(injectionName, loaded[0].Name,
                "Asset name must round-trip byte-for-byte through CSV serialisation");
        }
        
        /// <summary>Verifies that UpdateAssetAsync overwrites the existing row and the total count remains one.</summary>
        [TestMethod]
        public async Task IT03_UpdateAsset_OverwritesExistingRow_CountRemainsOne()
        {
            var asset = MakeCsvAsset("Colosseum");
            await _csvProvider.SaveAssetAsync(asset);

            Assert.AreEqual(1, (await _csvProvider.GetAssetsAsync()).Count(),
                "Pre-condition: exactly 1 asset must exist after first save");

            asset.UserExperience = "absolutely breathtaking";
            bool updated = await _csvProvider.UpdateAssetAsync(asset);

            Assert.IsTrue(updated,
                "UpdateAssetAsync must return true for a known asset");

            var all = (await _csvProvider.GetAssetsAsync()).ToList();
            Assert.AreEqual(1, all.Count,
                "UpdateAssetAsync must overwrite the existing row — count must remain 1");
            Assert.AreEqual("absolutely breathtaking", all[0].UserExperience,
                "The overwritten row must contain the updated UserExperience value");
        }
        /// <summary>Verifies that an API failure in EmbeddingService propagates as an HttpRequestException to the caller.</summary>

        [TestMethod]
        public async Task IT04_EmbeddingServiceApiFailure_ExceptionPropagatesOutOfService()
        {
            var throwingGen = new ThrowingEmbeddingGenerator(
                new HttpRequestException("Simulated OpenAI timeout"));
            var sut = new EmbeddingService(throwingGen);

            Exception? caught = null;
            try   { await sut.GenerateEmbeddingAsync("Place", "Travel", "Great"); }
            catch (Exception ex) { caught = ex; }

            Assert.IsNotNull(caught,
                "EmbeddingService must not swallow API errors — exception must reach the caller");
            Assert.IsInstanceOfType(caught, typeof(HttpRequestException),
                "The original HttpRequestException type must be preserved");
        }
        /// <summary>Verifies that a mismatched embedding dimension either throws ArgumentException or silently skips the bad asset.</summary>
        [TestMethod]
        public async Task IT05_DimensionMismatch_InAssetList_NeverReturnsGarbageScore()
        {
            var query     = new float[] { 1f, 0f, 0f };
            var goodAsset = new Asset
            {
                Id         = Guid.NewGuid(),
                Name       = "GoodAsset",
                Embedding  = new float[] { 1f, 0f, 0f },
            };
            var badAsset = new Asset
            {
                Id         = Guid.NewGuid(),
                Name       = "BadAsset",
                Embedding  = new float[] { 1f, 0f },   // wrong dimensiom
            };

            Exception?   thrown  = null;
            List<Asset>? results = null;
            try
            {
                results = await _simSvc.GetTopKSimilarAsync(
                    query, new List<Asset> { badAsset, goodAsset },
                    topK: 5, threshold: 0.0f);
            }
            catch (Exception ex) { thrown = ex; }

            if (thrown != null)
                Assert.IsInstanceOfType(thrown, typeof(ArgumentException),
                    "Dimension mismatch must throw ArgumentException, not a generic crash");
            else
            {
                Assert.IsNotNull(results);
                Assert.IsFalse(results!.Any(r => r.Name == "BadAsset"),
                    "Mismatched asset must be skipped");
                Assert.IsTrue(results.Any(r => r.Name == "GoodAsset"),
                    "Compatible asset must still be returned");
            }
        }
        /// <summary>Verifies that scoring 10,000 assets completes in under 3 seconds and returns the correct top-5 results.</summary>
        [TestMethod]
        public async Task IT06_TenThousandAssets_TopK5_CompletesUnder3Seconds_CorrectResultReturned()
        {
            const int Count = 10_000;
            const int Dims  = 128;
            var rng = new Random(42);

            var assets = Enumerable.Range(0, Count)
                .Select(i => new Asset
                {
                    Id         = Guid.NewGuid(),
                    Name       = $"Asset_{i}",
                    Embedding  = Enumerable.Range(0, Dims)
                                           .Select(_ => (float)rng.NextDouble())
                                           .ToArray(),
                })
                .ToList();

            var knownTop = assets[42];
            var query    = (float[])knownTop.Embedding!.Clone();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = await _simSvc.GetTopKSimilarAsync(
                query, assets, topK: 5, threshold: 0.0f);
            sw.Stop();

            Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(3),
                $"Scoring {Count} assets took {sw.Elapsed.TotalSeconds:F2}s — exceeds 3s budget");
            Assert.AreEqual(5, results.Count,
                "Exactly topK=5 results must be returned");
            Assert.IsTrue(results.Any(r => r.Name == knownTop.Name),
                "The asset identical to the query must appear in the top 5 results");
        }
        /// <summary>Verifies that a category filter excludes non-matching assets when reading from real CSV.</summary>
        [TestMethod]
        public async Task IT07_SearchAssets_CategoryFilter_OnlyMatchingCategoryFromRealCsv()
        {
            var tool = MakeTool(sim: new AlwaysMatchSimilarityThree());

            await QuickCreate(tool, "The Hobbit",      "Book",       city: "Oxford",    country: "UK");
            await QuickCreate(tool, "Vapiano",         "Restaurant", city: "Frankfurt", country: "Germany");
            await QuickCreate(tool, "Lofoten Islands", "Travel",     city: "Lofoten",   country: "Norway");

            var result = await tool.SearchAssets(
                query: "something to read", category: "Book");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "SearchAssets with category filter must return a non-empty response");
            Assert.IsFalse(result.Contains("Vapiano"),
                "Restaurant must be excluded when filtering by Book from real CSV");
            Assert.IsFalse(result.Contains("Lofoten"),
                "Travel must be excluded when filtering by Book from real CSV");

            var books = (await _csvProvider.GetAssetsAsync(category: "Book")).ToList();
            Assert.AreEqual(1, books.Count,
                "Only 1 Book must exist in the real CSV after saving 3 different categories");
            Assert.AreEqual("The Hobbit", books[0].Name,
                "The Book asset name must match what was saved to the real CSV");
        }
        /// <summary>Verifies that both SearchAssets and SearchRemindersSemantic return results from real CSV when similarity is 1.0.</summary>
        [TestMethod]
        public async Task IT08_SearchRemindersSemantic_LowerThreshold_BothToolsReturnResultsFromRealCsv()
        {
            var fakeGen = new FakeEmbeddingGeneratorThree
            {
                VectorToReturn = new float[] { 1f, 0f, 0f }
            };
            var realEmbedSvc = new EmbeddingService(fakeGen);

            var tool = new ReminderTools(
                _csvProvider,
                realEmbedSvc,
                _simSvc,
                new FixedChatClientThree());

            var asset = MakeCsvAsset("Black Forest Hike", "Hiking Route");
            asset.Embedding  = new float[] { 1f, 0f, 0f };
            await _csvProvider.SaveAssetAsync(asset);

            var searchResult   = await tool.SearchAssets(query: "relaxing hike");
            var semanticResult = await tool.SearchRemindersSemantic(query: "relaxing hike");

            Assert.IsFalse(string.IsNullOrWhiteSpace(searchResult),
                "SearchAssets must return a result from real CSV when similarity = 1.0");
            Assert.IsFalse(string.IsNullOrWhiteSpace(semanticResult),
                "SearchRemindersSemantic must return a result from real CSV when similarity = 1.0");

            var loaded = (await _csvProvider.GetAssetsAsync()).ToList();
            Assert.AreEqual(1, loaded.Count,
                "Exactly one asset must be present in the real CSV for this test");
        }
        /// <summary>Verifies that CreateAsset writes exactly one row to the real CSV and saves the embedding to the store.</summary>
       
        [TestMethod]
        public async Task IT09_CreateAsset_ValidInput_OneRowWrittenToRealCsvWithEmbedding()
        {
            var tool = MakeTool();

            var result = await tool.CreateAsset(
                name:           "Frankfurt Library",
                category:       "Place",
                userInput:      "Visited last week",
                userExperience: "Quiet and cozy",
                city:           "Frankfurt",
                country:        "Germany");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "CreateAsset must return a non-empty response for valid input");
            StringAssert.Contains(result, "Successfully remembered",
                "CreateAsset must return a success message");

            var reloaded = (await _csvProvider.GetAssetsAsync()).ToList();
            Assert.AreEqual(1, reloaded.Count,
                "Exactly one row must exist in the real CSV after one CreateAsset call");
            Assert.AreEqual("Frankfurt Library", reloaded[0].Name,
                "Asset name must be readable from real CSV");
            Assert.AreEqual("Place", reloaded[0].Category,
                "Category must be readable from real CSV");
            Assert.AreEqual("Frankfurt", reloaded[0].Metadata["City"],
                "City must survive CSV Metadata encoding and be readable from disk");

            var embedding = await _csvEmbedStore.LoadEmbeddingAsync(reloaded[0].Id);
            Assert.IsNotNull(embedding,
                "Embedding must be saved to the embedding store when CreateAsset is called");
        }
        
        /// <summary>Verifies that two sequential AttachPhoto calls both persist their photo references in the real CSV.</summary>
        
        [TestMethod]
        public async Task IT10_AttachPhoto_TwoSequentialCalls_BothPhotoRefsPersistInRealCsv()
        {
            var tool = MakeTool();

            await QuickCreate(tool, "Vapiano", "Restaurant",
                city: "Frankfurt", country: "Germany");

            var firstResult  = await tool.AttachPhoto("Vapiano", "Photos/vapiano_interior.jpg");
            var secondResult = await tool.AttachPhoto("Vapiano", "Photos/vapiano_food.jpg");

            StringAssert.Contains(firstResult, "Done!",
                "First AttachPhoto must succeed");
            StringAssert.Contains(secondResult, "Done!",
                "Second AttachPhoto must succeed");

            var reloaded = (await _csvProvider.GetAssetsAsync()).ToList();
            Assert.AreEqual(1, reloaded.Count,
                "AttachPhoto must not create additional rows in the CSV");
            Assert.AreEqual(2, reloaded[0].PhotoRefs.Count,
                "Both photo refs must be present after two sequential AttachPhoto → CSV writes");
            Assert.AreEqual("Photos/vapiano_interior.jpg", reloaded[0].PhotoRefs[0],
                "First photo ref must survive the second UpdateAssetAsync write");
            Assert.AreEqual("Photos/vapiano_food.jpg", reloaded[0].PhotoRefs[1],
                "Second photo ref must be present in the real CSV");
        }
        /// <summary>Verifies that embeddings are correctly merged back into assets after a provider restart.</summary>
        [TestMethod]
        public async Task IT11_ProviderRestart_EmbeddingMergedBack_ValuesIntact()
        {
            var asset = MakeCsvAsset("Sagrada Familia");
            await _csvProvider.SaveAssetAsync(asset);

            var restartedProvider = new CsvStorageProvider(_csvEmbedStore, _csvFileName);
            var reloaded = (await restartedProvider.GetAssetsAsync()).ToList();

            Assert.AreEqual(1, reloaded.Count,
                "Asset must be present after provider restart");
            Assert.AreEqual("Sagrada Familia", reloaded[0].Name,
                "Asset name must survive the CSV reload");
            Assert.IsNotNull(reloaded[0].Embedding,
                "Embedding must be merged back from the store into the reloaded asset");
            Assert.AreEqual(3, reloaded[0].Embedding!.Length,
                "Merged embedding must have the correct dimension");
            Assert.AreEqual(0.1f, reloaded[0].Embedding[0], 0.0001f,
                "Embedding values must survive the full save → restart → reload cycle");
        }
        /// <summary>Verifies that updating one asset persists changed fields and photo references without affecting sibling assets.</summary>
        [TestMethod]
        public async Task IT12_UpdateAsset_ChangedFieldAndPhotoRef_PersistThroughReload_SiblingUntouched()
        {
            var assetA = MakeCsvAsset("Rome");
            var assetB = MakeCsvAsset("Madrid");
            await _csvProvider.SaveAssetAsync(assetA);
            await _csvProvider.SaveAssetAsync(assetB);

            assetA.UserExperience = "incredible";
            assetA.PhotoRefs.Add("Photos/rome.jpg");

            bool updated = await _csvProvider.UpdateAssetAsync(assetA);
            Assert.IsTrue(updated, "UpdateAssetAsync must return true for a known asset");

            var all = (await _csvProvider.GetAssetsAsync())
                .ToDictionary(a => a.Name);

            Assert.AreEqual(2, all.Count,
                "Update must not create a duplicate — exactly 2 assets must exist");
            Assert.AreEqual("incredible", all["Rome"].UserExperience,
                "Updated UserExperience must persist through the CSV round-trip");
            Assert.AreEqual(1, all["Rome"].PhotoRefs.Count,
                "PhotoRef added during update must persist through the CSV round-trip");
            Assert.AreEqual("Photos/rome.jpg", all["Rome"].PhotoRefs[0],
                "Photo path must survive CSV round-trip exactly");
            Assert.AreNotEqual("incredible", all["Madrid"].UserExperience,
                "Sibling asset must not be affected by the update");
        }
        /// <summary>Verifies that three saved assets all reload with correct names and merged embeddings after a provider restart.</summary>
        [TestMethod]
        public async Task IT13_ThreeAssetsSaved_AllThreeReloadWithCorrectNamesAndEmbeddings()
        {
            var assetA = MakeCsvAsset("Athens");
            var assetB = MakeCsvAsset("Barcelona");
            var assetC = MakeCsvAsset("Cairo");

            await _csvProvider.SaveAssetAsync(assetA);
            await _csvProvider.SaveAssetAsync(assetB);
            await _csvProvider.SaveAssetAsync(assetC);

            var reloadedProvider = new CsvStorageProvider(_csvEmbedStore, _csvFileName);
            var all = (await reloadedProvider.GetAssetsAsync()).ToList();

            Assert.AreEqual(3, all.Count,
                "All three saved assets must be present after reload");

            var names = all.Select(a => a.Name).ToList();
            CollectionAssert.Contains(names, "Athens",    "Athens must be present after reload");
            CollectionAssert.Contains(names, "Barcelona", "Barcelona must be present after reload");
            CollectionAssert.Contains(names, "Cairo",     "Cairo must be present after reload");

            Assert.IsTrue(all.All(a => a.Embedding != null && a.Embedding.Length == 3),
                "Every reloaded asset must have its 3-dimensional embedding merged back");
        }
        /// <summary>Verifies that all asset fields survive a full CreateAsset, CSV write, and GetAssetDetails round-trip.</summary>
        [TestMethod]
        public async Task IT14_CreateAsset_ThenGetAssetDetails_AllFieldsSurviveRealCsvRoundTrip()
        {
            var tool = MakeTool();

            var createResult = await tool.CreateAsset(
                name:           "Vapiano Frankfurt",
                category:       "Restaurant",
                userInput:      "Had amazing pasta there",
                userExperience: "Wonderful",
                city:           "Frankfurt",
                country:        "Germany",
                region:         "Hesse");

            var detailsResult = await tool.GetAssetDetails("Vapiano Frankfurt");

            StringAssert.Contains(createResult, "Successfully remembered",
                "CreateAsset must return a success message — " +
                "pre-condition before GetAssetDetails is meaningful");
            StringAssert.Contains(detailsResult, "Vapiano Frankfurt",
                "Name must survive CSV serialisation and appear in GetAssetDetails output");
            StringAssert.Contains(detailsResult, "Restaurant",
                "Category must survive CSV serialisation and appear in GetAssetDetails output");
            StringAssert.Contains(detailsResult, "Frankfurt",
                "City stored in Metadata must survive CSV encoding and appear in output");
            StringAssert.Contains(detailsResult, "Germany",
                "Country stored in Metadata must survive CSV encoding and appear in output");
            StringAssert.Contains(detailsResult, "Hesse",
                "Region stored in Metadata must survive CSV encoding and appear in output");
            StringAssert.Contains(detailsResult, "Wonderful",
                "UserExperience must survive CSV serialisation and appear in output");

            var reloaded = (await _csvProvider.GetAssetsAsync()).ToList();
            Assert.AreEqual(1, reloaded.Count,
                "Exactly one row must exist in the real CSV after one CreateAsset call");
            Assert.AreEqual("Vapiano Frankfurt", reloaded[0].Name,
                "Name must be readable directly from the CSV file");
            Assert.AreEqual("Restaurant", reloaded[0].Category,
                "Category must be readable directly from the CSV file");
            Assert.AreEqual("Frankfurt", reloaded[0].Metadata["City"],
                "City must be readable from the CSV Metadata column after round-trip");
            Assert.AreEqual("Germany", reloaded[0].Metadata["Country"],
                "Country must be readable from the CSV Metadata column after round-trip");
            Assert.AreEqual("Hesse", reloaded[0].Metadata["Region"],
                "Region must be readable from the CSV Metadata column after round-trip");
            Assert.AreEqual("Wonderful", reloaded[0].UserExperience,
                "UserExperience must be readable directly from the CSV file");
        }
        /// <summary>Verifies that a stale Present timeline state written to CSV is dynamically recalculated to Past by GetReminders.</summary>
        [TestMethod]
        public async Task IT15_GetReminders_StalePresentWrittenToCsv_DynamicallyRecalculatedToPast()
        {
            var tool = MakeTool();

            string pastDate = DateTime.Today.AddDays(-30).ToString("dd-MM-yyyy");
            await tool.CreateAsset(
                name:           "Paris Visit",
                category:       "Travel",
                userInput:      "Visited Paris last month",
                userExperience: "lovely",
                eventDate:      pastDate,
                city:           "Paris",
                country:        "France");

            var beforeCorruption = (await _csvProvider.GetAssetsAsync()).ToList();
            Assert.AreEqual(1, beforeCorruption.Count,
                "Pre-condition: one asset must exist in CSV before state corruption");
            Assert.AreEqual("Past", beforeCorruption[0].TimelineState,
                "Pre-condition: TimelineState must be Past before we corrupt it on disk");

            var asset = beforeCorruption[0];
            asset.TimelineState = "Present";
            await _csvProvider.UpdateAssetAsync(asset);

            var afterCorruption = (await _csvProvider.GetAssetsAsync()).ToList();
            Assert.AreEqual("Present", afterCorruption[0].TimelineState,
                "Stale Present state must be on disk before calling GetReminders");
            Assert.IsTrue(afterCorruption[0].EventDate!.Value.Date < DateTime.Today,
                "EventDate must still be in the past after TimelineState was corrupted");

            var result = await tool.GetReminders(state: "Past");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                "GetReminders must return a non-empty response");
            Assert.IsFalse(result.Contains("No reminders") || result.Contains("nothing"),
                "GetReminders must find the stale-Present asset recalculated to Past");

            var reloaded = (await _csvProvider.GetAssetsAsync()).ToList();
            Assert.AreEqual(1, reloaded.Count,
                "Asset must still exist in CSV after GetReminders");
            Assert.AreEqual("Paris Visit", reloaded[0].Name,
                "Asset name must be intact after GetReminders call");
            Assert.IsTrue(reloaded[0].EventDate!.Value.Date < DateTime.Today,
                "EventDate must remain in the past — recalculation must not corrupt the date");
        }
    }
}