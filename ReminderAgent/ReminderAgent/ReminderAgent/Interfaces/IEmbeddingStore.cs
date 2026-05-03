namespace ReminderAgent.Interfaces
{
    /// <summary>
    /// Defines a contract for persisting and retrieving embedding vectors
    /// associated with asset identifiers.
    /// </summary>
    public interface IEmbeddingStore
    {
        /// <summary>
        /// Saves or updates the embedding vector for the specified asset identifier.
        /// </summary>
        /// <param name="assetId">The unique identifier of the asset.</param>
        /// <param name="embedding">The embedding vector to store.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveEmbeddingAsync(Guid assetId, float[] embedding);

        /// <summary>
        /// Retrieves the embedding vector for the specified asset identifier.
        /// </summary>
        /// <param name="assetId">The unique identifier of the asset.</param>
        /// <returns>
        /// A task containing the embedding vector if found; otherwise, null.
        /// </returns>
        Task<float[]?> LoadEmbeddingAsync(Guid assetId);

        /// <summary>
        /// Loads all stored embeddings.
        /// </summary>
        /// <returns>
        /// A task containing a dictionary mapping asset identifiers to embedding vectors.
        /// </returns>
        Task<Dictionary<Guid, float[]>> LoadAllEmbeddingsAsync();

        /// <summary>
        /// Deletes the embedding associated with the specified asset identifier.
        /// </summary>
        /// <param name="assetId">The unique identifier of the asset.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteEmbeddingAsync(Guid assetId);
    }
}