using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Infrastructure;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTest_SprintSix
{
    /// <summary>
    /// Contains unit tests for validating the functionality of the JsonEmbeddingStore class,
    /// including persistence, deletion, and concurrency behavior.
    /// </summary>
    [TestClass]
    public class JsonEmbeddingStoreTests
    {
        /// <summary>
        /// Temporary directory used for storing test data.
        /// </summary>
        private string _tempDir = string.Empty;
        /// <summary>
        /// Initializes a temporary directory before each test.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }
        /// <summary>
        /// Cleans up the temporary directory after each test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        /// <summary>
        /// Creates a new instance of JsonEmbeddingStore using the temporary directory.
        /// </summary>
        /// <returns>A configured JsonEmbeddingStore instance.</returns>
        private JsonEmbeddingStore MakeStore() => new JsonEmbeddingStore(_tempDir);

        /// <summary>
        /// Verifies that saving and loading an embedding returns the original data.
        /// </summary>
        [TestMethod]
        public async Task SaveAndLoad_RoundTrip_ReturnsOriginalEmbedding()
        {
            var store  = MakeStore();
            var id     = Guid.NewGuid();
            var vector = new float[] { 0.1f, 0.5f, 0.9f };

            await store.SaveEmbeddingAsync(id, vector);
            var loaded = await store.LoadEmbeddingAsync(id);

            Assert.IsNotNull(loaded);
            CollectionAssert.AreEqual(vector, loaded,
                "Loaded embedding must exactly match saved embedding");
        }
        /// <summary>
        /// Verifies that saving an embedding with an existing ID overwrites the previous entry.
        /// </summary>
        [TestMethod]
        public async Task SaveEmbeddingAsync_Overwrites_ExistingEntry()
        {
            var store = MakeStore();
            var id    = Guid.NewGuid();

            await store.SaveEmbeddingAsync(id, new float[] { 0.1f, 0.2f });
            await store.SaveEmbeddingAsync(id, new float[] { 0.9f, 0.8f });

            var loaded = await store.LoadEmbeddingAsync(id);
            Assert.AreEqual(0.9f, loaded![0], 0.0001f, "Overwritten value should be returned");
            Assert.AreEqual(0.8f, loaded![1], 0.0001f);
        }
        /// <summary>
        /// Verifies that loading an embedding with an unknown ID returns null.
        /// </summary>
        [TestMethod]
        public async Task LoadEmbeddingAsync_UnknownId_ReturnsNull()
        {
            var store  = MakeStore();
            var result = await store.LoadEmbeddingAsync(Guid.NewGuid());

            Assert.IsNull(result, "Loading an unknown GUID should return null");
        }
        /// <summary>
        /// Verifies that all saved embeddings are returned.
        /// </summary>
        [TestMethod]
        public async Task LoadAllEmbeddingsAsync_ReturnsAllSaved()
        {
            var store = MakeStore();
            var ids   = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

            foreach (var id in ids)
                await store.SaveEmbeddingAsync(id, new float[] { 0.1f });

            var all = await store.LoadAllEmbeddingsAsync();

            Assert.AreEqual(3, all.Count, "All saved embeddings should be returned");
            foreach (var id in ids)
                Assert.IsTrue(all.ContainsKey(id), $"GUID {id} should be present");
        }
        /// <summary>
        /// Verifies that an empty store returns an empty dictionary.
        /// </summary>
        [TestMethod]
        public async Task LoadAllEmbeddingsAsync_EmptyStore_ReturnsEmptyDictionary()
        {
            var store  = MakeStore();
            var result = await store.LoadAllEmbeddingsAsync();

            Assert.AreEqual(0, result.Count, "Empty store should return empty dictionary");
        }

        /// <summary>
        /// Verifies that deleting an existing embedding removes it from the store.
        /// </summary>

        [TestMethod]
        public async Task DeleteEmbeddingAsync_ExistingId_RemovesEntry()
        {
            var store = MakeStore();
            var id    = Guid.NewGuid();
            await store.SaveEmbeddingAsync(id, new float[] { 0.5f });

            await store.DeleteEmbeddingAsync(id);
            var result = await store.LoadEmbeddingAsync(id);

            Assert.IsNull(result, "Deleted embedding should not be retrievable");
        }
        /// <summary>
        /// Verifies that deleting a non-existent embedding does not throw an exception.
        /// </summary>
        [TestMethod]
        public async Task DeleteEmbeddingAsync_NonExistentId_DoesNotThrow()
        {
            var store = MakeStore();

            // Should complete without exception
            await store.DeleteEmbeddingAsync(Guid.NewGuid());
        }
        /// <summary>
        /// Verifies that deleting one embedding does not affect others.
        /// </summary>
        [TestMethod]
        public async Task DeleteEmbeddingAsync_OnlyDeletesTarget()
        {
            var store = MakeStore();
            var id1   = Guid.NewGuid();
            var id2   = Guid.NewGuid();
            await store.SaveEmbeddingAsync(id1, new float[] { 0.1f });
            await store.SaveEmbeddingAsync(id2, new float[] { 0.2f });

            await store.DeleteEmbeddingAsync(id1);

            var all = await store.LoadAllEmbeddingsAsync();
            Assert.IsFalse(all.ContainsKey(id1), "id1 should be deleted");
            Assert.IsTrue(all.ContainsKey(id2),  "id2 should still exist");
        }

        /// <summary>
        /// Verifies that data persists across different instances of the store.
        /// </summary>

        [TestMethod]
        public async Task Persistence_NewStoreInstance_LoadsSavedData()
        {
            var id     = Guid.NewGuid();
            var vector = new float[] { 1.0f, 2.0f, 3.0f };

            var store1 = MakeStore();
            await store1.SaveEmbeddingAsync(id, vector);

            // New instance, same directory
            var store2 = MakeStore();
            var loaded = await store2.LoadEmbeddingAsync(id);

            Assert.IsNotNull(loaded,                 "Data should persist across instances");
            Assert.AreEqual(1.0f, loaded![0], 0.0001f);
            Assert.AreEqual(2.0f, loaded![1], 0.0001f);
            Assert.AreEqual(3.0f, loaded![2], 0.0001f);
        }
        /// <summary>
        /// Verifies that large embedding vectors are correctly saved and loaded.
        /// </summary>
        [TestMethod]
        public async Task Persistence_LargeEmbeddingVector_RoundTrips()
        {
            var store  = MakeStore();
            var id     = Guid.NewGuid();
            // text-embedding-3-small produces 1536-dimensional vectors
            var vector = Enumerable.Range(0, 1536)
                                   .Select(i => (float)i / 1536f)
                                   .ToArray();

            await store.SaveEmbeddingAsync(id, vector);
            var loaded = await store.LoadEmbeddingAsync(id);

            Assert.AreEqual(1536, loaded!.Length,             "1536-dim vector should round-trip");
            Assert.AreEqual(vector[0],    loaded[0],    0.0001f);
            Assert.AreEqual(vector[1535], loaded[1535], 0.0001f);
        }

        /// <summary>
        /// Verifies that concurrent save operations do not corrupt the data store.
        /// </summary>

        [TestMethod]
        public async Task ConcurrentSaves_DoNotCorruptStore()
        {
            var store = MakeStore();
            var tasks = Enumerable.Range(0, 20)
                                  .Select(i => store.SaveEmbeddingAsync(
                                      Guid.NewGuid(), new float[] { (float)i }))
                                  .ToList();

            await Task.WhenAll(tasks);

            var all = await store.LoadAllEmbeddingsAsync();
            Assert.AreEqual(20, all.Count, "All 20 concurrent saves should be present");
        }
    }
}
