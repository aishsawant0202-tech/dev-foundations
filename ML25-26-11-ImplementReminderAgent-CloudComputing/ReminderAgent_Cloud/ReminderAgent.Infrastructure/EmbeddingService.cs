using Microsoft.Extensions.AI;
using ReminderAgent.Common;
using ReminderAgent.Infrastructure;
namespace ReminderAgent.Infrastructure
{
    /// <summary>
    /// Provides functionality to generate vector embeddings for text inputs
    /// using an underlying embedding generator (e.g., OpenAI text-embedding model).
    /// </summary>
    public class EmbeddingService : IEmbeddingService
    {
        #region Fields

        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddingService"/> class.
        /// </summary>
        /// <param name="embeddingGenerator">
        /// The embedding generator used to create vector representations of text.
        /// </param>

        public EmbeddingService(
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _embeddingGenerator = embeddingGenerator;
        }

        #endregion

        #region Embedding Generation

        /// <summary>
        /// Generates an embedding vector for a reminder during the save operation.
        /// Combines multiple fields into a single structured input string.
        /// </summary>
        /// <param name="name">The name of the reminder.</param>
        /// <param name="category">The category of the reminder.</param>
        /// <param name="userExperience">User-provided experience or description.</param>
        /// <param name="userInput">Additional user input details (optional).</param>
        /// <param name="tags">Associated tags (optional).</param>
        /// <returns>
        /// A float array representing the generated embedding vector.
        /// </returns>
        public async Task<float[]> GenerateEmbeddingAsync(
            string name,
            string category,
            string userExperience,
            string userInput = "",
            string tags = "")
        {
            string combined =
                $"Name: {name}\n" +
                $"Category: {category}\n" +
                $"Experience: {userExperience}\n" +
                $"Details: {userInput}\n" +
                $"Tags: {tags}";

            var embedding = await _embeddingGenerator.GenerateAsync(combined);
            return embedding.Vector.ToArray();
        }

        /// <summary>
        /// Generates an embedding vector for a search query.
        /// Used in semantic search to find similar reminders.
        /// </summary>
        /// <param name="query">The search query text.</param>
        /// <returns>
        /// A float array representing the query embedding vector.
        /// </returns>
        public async Task<float[]> GenerateQueryEmbeddingAsync(string query)
        {
            var embedding = await _embeddingGenerator.GenerateAsync(query);
            return embedding.Vector.ToArray();
        }

        #endregion
    }

}
