using ReminderAgent.Domain;
using ReminderAgent.Interfaces;
using ReminderAgent.Infrastructure;

namespace ReminderAgent.Infrastructure
{
    /// <summary>
    /// Provides functionality to compute similarity between embeddings
    /// and retrieve the most relevant assets based on semantic similarity.
    /// </summary>
    public class SimilarityService : ISimilarityService
    {
        /// <summary>
        /// Computes the cosine similarity between two embedding vectors.
        /// </summary>
        /// <param name="vectorA">The first embedding vector.</param>
        /// <param name="vectorB">The second embedding vector.</param>
        /// <returns>
        /// A similarity score between 0 and 1, where 1 indicates identical vectors.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the input vectors have different lengths.
        /// </exception>
        public float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                throw new ArgumentException(
                    $"Embedding vectors must be the same length. " +
                    $"Got {vectorA.Length} and {vectorB.Length}.");

            double dot = 0.0;
            double magA = 0.0;
            double magB = 0.0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dot += vectorA[i] * vectorB[i];
                magA += vectorA[i] * vectorA[i];
                magB += vectorB[i] * vectorB[i];
            }

            double similarity = dot / (Math.Sqrt(magA) * Math.Sqrt(magB) + 1e-8);

            // Clamp to [0, 1] to handle any floating-point rounding edge cases
            return (float)Math.Clamp(similarity, 0.0, 1.0);
        }

        /// <summary>
        /// Computes similarity scores between a query embedding and a collection of assets,
        /// returning the top-K most similar assets above a given threshold.
        /// </summary>
        /// <param name="queryEmbedding">The embedding vector representing the search query.</param>
        /// <param name="assets">The collection of assets to compare against.</param>
        /// <param name="topK">The maximum number of top results to return (default is 5).</param>
        /// <param name="threshold">
        /// The minimum similarity score required for an asset to be included (default is 0.5).
        /// </param>
        /// <returns>
        /// A list of the most similar assets ordered by descending similarity score.
        /// </returns>
        public Task<List<Asset>> GetTopKSimilarAsync(float[] queryEmbedding, 
                    IEnumerable<Asset> assets, int topK = 5, float threshold = 0.5f)
        {
            FileLogger.Info($"GetTopKSimilarAsync | topK: {topK} | threshold: {threshold}");

            var assetList = assets.ToList();
            FileLogger.Info($"GetTopKSimilarAsync | Total assets to score: {assetList.Count}");

            var results = assetList
                .Select(a =>
                {
                    float[]? vec = a.Embedding;
                    return new { Asset = a, Vector = vec };
                })
                .Where(x => x.Vector != null && x.Vector.Length > 0)
                .Select(x => new
                {
                    x.Asset,
                    Score = CosineSimilarity(queryEmbedding, x.Vector!)
                })
                .Where(x => x.Score >= threshold)
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Asset)
                .ToList();

            FileLogger.Info($"GetTopKSimilarAsync | Matched above threshold: {results.Count}");
            return Task.FromResult(results);
        }
    }
}