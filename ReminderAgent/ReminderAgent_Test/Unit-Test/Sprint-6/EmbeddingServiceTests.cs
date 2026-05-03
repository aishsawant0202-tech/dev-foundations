using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTest_SprintSix
{
    /// <summary>
    /// Unit tests for <see cref="EmbeddingService"/>.
    /// Validates embedding generation for assets and search queries using a
    /// <see cref="FakeEmbeddingGenerator"/> to replace the real OpenAI API during testing.
    /// </summary>
    [TestClass]
    public class EmbeddingServiceTests
    {
        private FakeEmbeddingGenerator _fakeGenerator = null!;
        private EmbeddingService _sut = null!;
        /// <summary>
        /// Initializes a fresh <see cref="FakeEmbeddingGenerator"/> and <see cref="EmbeddingService"/>
        /// instance before each test to ensure test isolation.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _fakeGenerator = new FakeEmbeddingGenerator();
            _sut = new EmbeddingService(_fakeGenerator);
        }
        /// <summary>
        /// Performs any necessary cleanup after each test. No resources require disposal in this suite.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            // nothing to clean up
        }

        // GenerateEmbeddingAsync
        /// <summary>
        /// Verifies that <see cref="EmbeddingService.GenerateEmbeddingAsync"/> returns the exact
        /// float vector produced by the underlying embedding generator.
        /// </summary>

        [TestMethod]
        public async Task GenerateEmbeddingAsync_ReturnsVector_FromGenerator()
        {
            var expected = new float[] { 0.1f, 0.2f, 0.3f };
            _fakeGenerator.VectorToReturn = expected;

            var result = await _sut.GenerateEmbeddingAsync(
                name: "Vapiano",
                category: "Restaurant",
                userExperience: "Wonderful");

            CollectionAssert.AreEqual(expected, result,
                "Returned vector must match what the generator produced");
        }
        /// <summary>
        /// Verifies that all five fields — name, category, userExperience, userInput, and tags —
        /// are combined into a single string before being passed to the embedding generator.
        /// </summary>
        [TestMethod]
        public async Task GenerateEmbeddingAsync_CombinesAllFiveFields_IntoOneString()
        {
            _fakeGenerator.VectorToReturn = new float[] { 1f };

            await _sut.GenerateEmbeddingAsync(
                name: "Vapiano",
                category: "Restaurant",
                userExperience: "Wonderful",
                userInput: "great italian food",
                tags: "italian;frankfurt");

            // The combined string passed to the generator must contain all 5 fields
            Assert.IsNotNull(_fakeGenerator.LastInput, "Generator must have been called");
            StringAssert.Contains(_fakeGenerator.LastInput, "Vapiano",            "Must contain name");
            StringAssert.Contains(_fakeGenerator.LastInput, "Restaurant",         "Must contain category");
            StringAssert.Contains(_fakeGenerator.LastInput, "Wonderful",          "Must contain userExperience");
            StringAssert.Contains(_fakeGenerator.LastInput, "great italian food", "Must contain userInput");
            StringAssert.Contains(_fakeGenerator.LastInput, "italian;frankfurt",  "Must contain tags");
        }
        /// <summary>
        /// Verifies that omitting the optional <c>userInput</c> and <c>tags</c> parameters
        /// does not cause an exception and still returns a valid result.
        /// </summary>
        [TestMethod]
        public async Task GenerateEmbeddingAsync_OptionalParams_OmittedWithoutThrowing()
        {
            _fakeGenerator.VectorToReturn = new float[] { 1f };

            // userInput and tags are optional — omitting must not throw
            var result = await _sut.GenerateEmbeddingAsync(
                name: "The Hobbit",
                category: "Book",
                userExperience: "Amazing");

            Assert.IsNotNull(result, "Result must not be null even when optional params omitted");
        }
        /// <summary>
        /// Verifies that all float values in the embedding vector are preserved exactly
        /// as they pass through the <see cref="EmbeddingService"/> pipeline without any data loss.
        /// </summary>
        [TestMethod]
        public async Task GenerateEmbeddingAsync_VectorValues_RoundTripWithoutLoss()
        {
            // Verifies the float[] is preserved exactly through the pipeline
            var expected = new float[] { 1f, 2f, 3f, 4f, 5f };
            _fakeGenerator.VectorToReturn = expected;

            var result = await _sut.GenerateEmbeddingAsync(
                name: "Test",
                category: "Test",
                userExperience: "Test");

            Assert.AreEqual(5, result.Length, "Vector length must be preserved");
            CollectionAssert.AreEqual(expected, result, "All float values must round-trip exactly");
        }
        /// <summary>
        /// Verifies that the embedding generator is invoked exactly once per call
        /// to <see cref="EmbeddingService.GenerateEmbeddingAsync"/>.
        /// </summary>
        [TestMethod]
        public async Task GenerateEmbeddingAsync_CallsGenerator_ExactlyOnce()
        {
            _fakeGenerator.VectorToReturn = new float[] { 1f };

            await _sut.GenerateEmbeddingAsync("A", "B", "C");

            Assert.AreEqual(1, _fakeGenerator.CallCount,
                "Generator must be called exactly once per embedding request");
        }
        /// <summary>
        /// Verifies that <see cref="EmbeddingService.GenerateEmbeddingAsync"/> returns
        /// a non-empty float array for a standard set of inputs.
        /// </summary>
        [TestMethod]
        public async Task GenerateEmbeddingAsync_Returns_NonEmptyArray()
        {
            _fakeGenerator.VectorToReturn = new float[] { 0.5f, 0.6f, 0.7f };

            var result = await _sut.GenerateEmbeddingAsync(
                name: "Black Forest",
                category: "Travel",
                userExperience: "Relaxing");

            Assert.IsTrue(result.Length > 0, "Result must be a non-empty float array");
        }
        /// <summary>
        /// Verifies that providing all optional parameters alongside the required ones
        /// does not throw an exception and produces a valid result with exactly one generator call.
        /// </summary>
        [TestMethod]
        public async Task GenerateEmbeddingAsync_WithAllParams_DoesNotThrow()
        {
            _fakeGenerator.VectorToReturn = new float[] { 0.1f };

            var result = await _sut.GenerateEmbeddingAsync(
                name: "Colosseum",
                category: "Travel",
                userExperience: "Breathtaking",
                userInput: "visited the Colosseum in Rome",
                tags: "rome;italy;travel;history");

            Assert.IsNotNull(result);
            Assert.AreEqual(1, _fakeGenerator.CallCount);
        }

        /// <summary>
        // /// Verifies that <see cref="EmbeddingService.GenerateQueryEmbeddingAsync"/> returns
        // /// the exact float vector produced by the underlying embedding generator.
        // /// </summary>
        [TestMethod]
        public async Task GenerateQueryEmbeddingAsync_ReturnsVector_FromGenerator()
        {
            var expected = new float[] { 0.5f, 0.6f };
            _fakeGenerator.VectorToReturn = expected;

            var result = await _sut.GenerateQueryEmbeddingAsync("restaurants in Frankfurt");

            CollectionAssert.AreEqual(expected, result,
                "Returned vector must match what the generator produced");
        }
        /// <summary>
        /// Verifies that the query string is passed to the embedding generator completely
        /// unchanged — no trimming, formatting, or modification of any kind.
        /// </summary>
        [TestMethod]
        public async Task GenerateQueryEmbeddingAsync_PassesQueryString_Unchanged()
        {
            _fakeGenerator.VectorToReturn = new float[] { 1f };

            await _sut.GenerateQueryEmbeddingAsync("what did I find relaxing?");

            Assert.AreEqual("what did I find relaxing?", _fakeGenerator.LastInput,
                "Query string must reach the generator exactly as provided — no modification");
        }
        /// <summary>
        /// Verifies that passing an empty string to <see cref="EmbeddingService.GenerateQueryEmbeddingAsync"/>
        /// does not throw an exception and still returns a valid result.
        /// </summary>
        [TestMethod]
        public async Task GenerateQueryEmbeddingAsync_EmptyString_DoesNotThrow()
        {
            _fakeGenerator.VectorToReturn = new float[] { 0f };

            var result = await _sut.GenerateQueryEmbeddingAsync(string.Empty);

            Assert.IsNotNull(result, "Empty query should still return a result without throwing");
        }
        /// <summary>
        /// Verifies that the embedding generator is invoked exactly once per call
        /// to <see cref="EmbeddingService.GenerateQueryEmbeddingAsync"/>.
        /// </summary>
        [TestMethod]
        public async Task GenerateQueryEmbeddingAsync_CallsGenerator_ExactlyOnce()
        {
            _fakeGenerator.VectorToReturn = new float[] { 1f };

            await _sut.GenerateQueryEmbeddingAsync("show me inspiring things");

            Assert.AreEqual(1, _fakeGenerator.CallCount,
                "Generator must be called exactly once per query");
        }
        /// <summary>
        /// Verifies that <see cref="EmbeddingService.GenerateQueryEmbeddingAsync"/> returns
        /// a non-empty float array for a standard search query string.
        /// </summary>
        [TestMethod]
        public async Task GenerateQueryEmbeddingAsync_Returns_NonEmptyArray()
        {
            _fakeGenerator.VectorToReturn = new float[] { 0.3f, 0.4f };

            var result = await _sut.GenerateQueryEmbeddingAsync("books I read last month");

            Assert.IsTrue(result.Length > 0, "Result must be a non-empty float array");
        }
    }
    /// <summary>
    /// A test double for <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> that replaces
    /// the real OpenAI API during unit testing. Captures the last input string, tracks the number
    /// of calls made, and returns a configurable float vector.
    /// </summary>
    public class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        /// <summary>
        /// Gets or sets the float vector that will be returned by <see cref="GenerateAsync"/>.
        /// Defaults to a single zero-value vector.
        /// </summary>
        public float[] VectorToReturn { get; set; } = new float[] { 0f };
        /// <summary>
        /// Gets the last input string that was passed to <see cref="GenerateAsync"/>.
        /// Used in tests to assert that the correct combined text reached the generator.
        /// </summary>
        public string? LastInput { get; private set; }
        /// <summary>
        /// Gets the total number of times <see cref="GenerateAsync"/> has been called.
        /// Used in tests to verify the generator is invoked the expected number of times.
        /// </summary>
        public int CallCount { get; private set; }
        /// <summary>
        /// Simulates embedding generation by capturing the first input value,
        /// incrementing the call counter, and returning the configured <see cref="VectorToReturn"/>.
        /// </summary>
        /// <param name="values">The input strings to embed. Only the first value is captured.</param>
        /// <param name="options">Optional embedding generation options. Not used by this fake.</param>
        /// <param name="cancellationToken">Token to observe for cancellation requests.</param>
        /// <returns>A <see cref="GeneratedEmbeddings{TEmbedding}"/> containing the configured vector.</returns>
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            // Capture the first input for assertion in tests
            foreach (var v in values)
            {
                LastInput = v;
                break;
            }

            var embedding  = new Embedding<float>(VectorToReturn);
            var collection = new GeneratedEmbeddings<Embedding<float>>(new[] { embedding });
            return Task.FromResult(collection);
        }
        /// <summary>
        /// Gets the metadata for this fake generator, identifying it as "FakeGenerator".
        /// </summary>
        public EmbeddingGeneratorMetadata Metadata =>
            new EmbeddingGeneratorMetadata("FakeGenerator");
        /// <summary>
        /// Returns <c>null</c> for all service type requests, as this fake does not provide any services.
        /// </summary>
        /// <param name="serviceType">The type of service requested.</param>
        /// <param name="key">An optional key identifying the service instance.</param>
        /// <returns>Always <c>null</c>.</returns>
        public object? GetService(Type serviceType, object? key = null) => null;
 
        /// <summary>
        /// Disposes this instance. No resources require disposal in this fake implementation.
        /// </summary>
        

        public void Dispose() { }
    }
}
