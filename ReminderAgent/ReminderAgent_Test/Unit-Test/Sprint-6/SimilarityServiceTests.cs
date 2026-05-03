using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Domain;
using ReminderAgent.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTest_SprintSix
{
    /// <summary>
    /// Contains unit tests for validating the behavior of the SimilarityService,
    /// including cosine similarity calculations and top-K similarity search.
    /// </summary>
    [TestClass]
    public class SimilarityServiceTests
    {
        /// <summary>
        /// System under test: instance of SimilarityService.
        /// </summary>
        private SimilarityService _sut = null!;
        /// <summary>
        /// Initializes the SimilarityService before each test.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _sut = new SimilarityService();
        }
        /// <summary>
        /// Cleans up resources after each test execution.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            
        }
        /// <summary>
        /// Verifies that identical vectors return a cosine similarity of 1.0.
        /// </summary>

        [TestMethod]
        public void CosineSimilarity_IdenticalVectors_ReturnsOne()
        {
            float[] v = { 1f, 2f, 3f };

            var result = _sut.CosineSimilarity(v, v);

            Assert.AreEqual(1.0f, result, 0.0001f,
                "Identical vectors must return cosine similarity of 1.0");
        }
        /// <summary>
        /// Verifies that orthogonal vectors return a cosine similarity of 0.0.
        /// </summary>
        [TestMethod]
        public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
        {
            float[] a = { 1f, 0f };
            float[] b = { 0f, 1f };

            var result = _sut.CosineSimilarity(a, b);

            Assert.AreEqual(0.0f, result, 0.0001f,
                "Orthogonal vectors must return 0.0");
        }
        /// <summary>
        /// Verifies that opposite vectors are clamped to a similarity of 0.0.
        /// </summary>
        [TestMethod]
        public void CosineSimilarity_OppositeVectors_ClampedToZero()
        {
            // Raw cosine = -1.0, clamped to [0,1] → 0.0
            float[] a = {  1f, 0f };
            float[] b = { -1f, 0f };

            var result = _sut.CosineSimilarity(a, b);

            Assert.AreEqual(0.0f, result, 0.0001f,
                "Opposite vectors: raw cosine -1 must be clamped to 0.0");
        }
        /// <summary>
        /// Verifies that zero vectors do not cause exceptions and return 0.0.
        /// </summary>
        [TestMethod]
        public void CosineSimilarity_ZeroVector_DoesNotThrow_ReturnsZero()
        {
            // Epsilon (1e-8) prevents divide-by-zero
            float[] a = { 0f, 0f };
            float[] b = { 1f, 2f };

            var result = _sut.CosineSimilarity(a, b);

            Assert.AreEqual(0.0f, result, 0.0001f,
                "Zero vector must not throw and must return 0.0");
        }
        /// <summary>
        /// Verifies that scaling a vector does not change cosine similarity.
        /// </summary>
        [TestMethod]
        public void CosineSimilarity_ScaledVector_ReturnsSameScore()
        {
            // Cosine similarity is scale-invariant
            float[] a = { 1f, 2f, 3f };
            float[] b = { 3f, 6f, 9f }; // a × 3

            var result = _sut.CosineSimilarity(a, b);

            Assert.AreEqual(1.0f, result, 0.0001f,
                "Scaling a vector must not change cosine similarity");
        }
        /// <summary>
        /// Verifies that cosine similarity results are always within the range [0, 1].
        /// </summary>
        [TestMethod]
        public void CosineSimilarity_Result_AlwaysBetweenZeroAndOne()
        {
            float[] a = { 0.5f, 0.5f };
            float[] b = { 0.3f, 0.9f };

            var result = _sut.CosineSimilarity(a, b);

            Assert.IsTrue(result >= 0.0f && result <= 1.0f,
                $"Result {result} must be in [0, 1]");
        }
        /// <summary>
        /// Verifies that mismatched vector lengths throw an ArgumentException.
        /// </summary>
        [TestMethod]
        public void CosineSimilarity_MismatchedLengths_ThrowsArgumentException()
        {
            float[] a = { 1f, 2f };
            float[] b = { 1f, 2f, 3f };

            try
            {
                _sut.CosineSimilarity(a, b);
                Assert.Fail("Expected ArgumentException was not thrown");
            }
            catch (ArgumentException)
            {
               
            }
        }
        /// <summary>
        /// Verifies that the exception message includes both vector lengths.
        /// </summary>
        [TestMethod]
        public void CosineSimilarity_MismatchedLengths_ErrorMessage_ContainsBothLengths()
        {
            float[] a = { 1f, 2f };
            float[] b = { 1f, 2f, 3f };

            try
            {
                _sut.CosineSimilarity(a, b);
                Assert.Fail("Expected ArgumentException was not thrown");
            }
            catch (ArgumentException ex)
            {
                StringAssert.Contains(ex.Message, "2");
                StringAssert.Contains(ex.Message, "3");
            }
        }

        /// <summary>
        /// Verifies that the most similar asset appears first in the results.
        /// </summary>

        [TestMethod]
        public async Task GetTopKSimilarAsync_ReturnsBestMatch_First()
        {
            var query = new float[] { 1f, 0f };

            var assets = new List<Asset>
            {
                new() { Id = Guid.NewGuid(), Name = "Vapiano",      Embedding = new float[] { 0.8f, 0.2f }},
                new() { Id = Guid.NewGuid(), Name = "Black Forest",  Embedding = new float[] { 1f,   0f   }},
            };

            var results = await _sut.GetTopKSimilarAsync(query, assets, topK: 5, threshold: 0.5f);

            Assert.AreEqual("Black Forest", results[0].Name,
                "Asset with highest similarity must appear first");
        }
        /// <summary>
        /// Verifies that the number of results does not exceed the specified topK limit.
        /// </summary>
        [TestMethod]
        public async Task GetTopKSimilarAsync_RespectsTopKLimit()
        {
            var query = new float[] { 1f, 0f };

            var assets = Enumerable.Range(1, 10)
                .Select(i => new Asset
                {
                    Id         = Guid.NewGuid(),
                    Name       = $"Asset{i}",
                    Embedding  = new float[] { 1f, 0f },
                })
                .ToList();

            var results = await _sut.GetTopKSimilarAsync(query, assets, topK: 3, threshold: 0.0f);

            Assert.AreEqual(3, results.Count,
                "Result count must not exceed topK");
        }
        /// <summary>
        /// Verifies that assets below the similarity threshold are excluded.
        /// </summary>
        [TestMethod]
        public async Task GetTopKSimilarAsync_ExcludesAssets_BelowThreshold()
        {
            var query = new float[] { 1f, 0f };

            var assets = new List<Asset>
            {
                new() { Id = Guid.NewGuid(), Name = "LowScore",  Embedding = new float[] { 0f, 1f } },
                new() { Id = Guid.NewGuid(), Name = "HighScore", Embedding = new float[] { 1f, 0f } }
            };

            var results = await _sut.GetTopKSimilarAsync(query, assets, topK: 5, threshold: 0.5f);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("HighScore", results[0].Name);
        }
        /// <summary>
        /// Verifies that assets with null embeddings are skipped.
        /// </summary>
        [TestMethod]
        public async Task GetTopKSimilarAsync_SkipsAssets_WithNullEmbedding()
        {
            var query = new float[] { 1f, 0f };

            var assets = new List<Asset>
            {
                new() { Id = Guid.NewGuid(), Name = "NoVector",  Embedding = null },
                new() { Id = Guid.NewGuid(), Name = "HasVector", Embedding = new float[] { 1f, 0f } },
            };

            var results = await _sut.GetTopKSimilarAsync(query, assets, topK: 5, threshold: 0.0f);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("HasVector", results[0].Name);
        }
        /// <summary>
        /// Verifies that an empty asset list returns an empty result.
        /// </summary>
        [TestMethod]
        public async Task GetTopKSimilarAsync_EmptyAssetList_ReturnsEmpty()
        {
            var query = new float[] { 1f, 0f };

            var results = await _sut.GetTopKSimilarAsync(
                query, Enumerable.Empty<Asset>(), topK: 5, threshold: 0.5f);

            Assert.AreEqual(0, results.Count,
                "Empty asset list must return empty result");
        }
        /// <summary>
        /// Verifies that no results are returned when no assets meet the threshold.
        /// </summary>
        [TestMethod]
        public async Task GetTopKSimilarAsync_NothingAboveThreshold_ReturnsEmpty()
        {
            var query = new float[] { 1f, 0f };

            var assets = new List<Asset>
            {
                new() { Id = Guid.NewGuid(), Name = "Unrelated", Embedding = new float[] { 0f, 1f } }
            };

            var results = await _sut.GetTopKSimilarAsync(
                query, assets, topK: 5, threshold: 0.9f);

            Assert.AreEqual(0, results.Count,
                "No asset above threshold must return empty list");
        }
        /// <summary>
        /// Verifies that a threshold of 0.75 filters results correctly.
        /// </summary>
        [TestMethod]
        public async Task GetTopKSimilarAsync_SearchAssets_Threshold_075_FiltersCorrectly()
        {
            var query = new float[] { 1f, 0f };

            var assets = new List<Asset>
            {
                new() { Id = Guid.NewGuid(), Name = "HighMatch", Embedding = new float[] { 1f,    0f   } }, // 1.0  ✓
                new() { Id = Guid.NewGuid(), Name = "MidMatch",  Embedding = new float[] { 0.7f,  0.3f } }, // ~0.92 ✓
                new() { Id = Guid.NewGuid(), Name = "LowMatch",  Embedding = new float[] { 0.3f,  0.7f } }, // ~0.39 ✗
            };

            var results = await _sut.GetTopKSimilarAsync(query, assets, topK: 5, threshold: 0.75f);

            Assert.AreEqual(2, results.Count,
                "Only assets above 0.75 threshold must be returned");
        }
        /// <summary>
        /// Verifies that a lower threshold allows more permissive matching.
        /// </summary>
        [TestMethod]
        public async Task GetTopKSimilarAsync_SemanticSearch_Threshold_025_IsMorePermissive()
        {
            var query = new float[] { 1f, 0f };

            var assets = new List<Asset>
            {
                new() { Id = Guid.NewGuid(), Name = "LooseMatch", Embedding = new float[] { 0.5f, 0.5f } }, // ~0.71 ✓
                new() { Id = Guid.NewGuid(), Name = "NoMatch",    Embedding = new float[] { 0f,   1f   } }, // 0.0  ✗
            };

            var results = await _sut.GetTopKSimilarAsync(query, assets, topK: 5, threshold: 0.25f);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("LooseMatch", results[0].Name);
        }
    }
}
