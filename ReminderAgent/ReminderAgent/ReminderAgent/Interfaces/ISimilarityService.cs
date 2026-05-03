using ReminderAgent.Domain;
namespace ReminderAgent.Interfaces
{
    /// <summary>
    /// Defines a contract for computing semantic similarity between embeddings
    /// and retrieving the most relevant assets.
    /// </summary>
    public interface ISimilarityService
    {
        /// <summary>
        /// Computes the cosine similarity between two embedding vectors.
        /// </summary>
        /// <param name="vectorA">The first embedding vector.</param>
        /// <param name="vectorB">The second embedding vector.</param>
        /// <returns>
        /// A similarity score between 0 and 1, where 1 indicates identical vectors.
        /// </returns>
        float CosineSimilarity(float[] vectorA, float[] vectorB);

        /// <summary>
        /// Computes similarity scores between a query embedding and a collection of assets,
        /// returning the top-K most similar assets above a specified threshold.
        /// </summary>
        /// <param name="queryEmbedding">The embedding vector representing the search query.</param>
        /// <param name="assets">The collection of assets to compare against.</param>
        /// <param name="topK">The maximum number of results to return (default is 5).</param>
        /// <param name="threshold">
        /// The minimum similarity score required for an asset to be included (default is 0.5).
        /// </param>
        /// <returns>
        /// A task containing a list of the most similar assets ordered by descending similarity.
        /// </returns>
        Task<List<Asset>> GetTopKSimilarAsync(
            float[] queryEmbedding,
            IEnumerable<Asset> assets,
            int topK = 5,
            float threshold = 0.5f);
    }
}
